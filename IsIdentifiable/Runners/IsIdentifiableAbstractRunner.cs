using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FAnsi;
using FAnsi.Discovery;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Rules;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using YamlDotNet.Serialization;
using System.Threading;
using System.IO.Abstractions;
using IsIdentifiable.AllowLists;

namespace IsIdentifiable.Runners;

/// <summary>
/// Base class for all classes which evaluate data sources to detect identifiable data.
/// Subclass to add support for new data sources.  CurrentFailure sources include reading from
/// CSV files, Dicom files and database tables.
/// </summary>
public abstract class IsIdentifiableAbstractRunner : IDisposable
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    private readonly IsIdentifiableBaseOptions _opts;

    /// <summary>
    /// FileSystem to use for I/O
    /// </summary>
    protected readonly IFileSystem FileSystem;

    /// <summary>
    /// Collection of methods which will be used to aggregate or persist <see cref="Failure"/>
    /// instances as they are detected by this runner.  A report may total up e.g. by column
    /// or may just write all the values out in full (serialize) for later review
    /// </summary>
    public readonly List<IFailureReport> Reports = new();

    // DDMMYY + 4 digits 
    // \b bounded i.e. not more than 10 digits
    private readonly Regex _chiRegex = new(@"\b[0-3][0-9][0-1][0-9][0-9]{6}\b");
    readonly Regex _postcodeRegex = new(@"\b((GIR 0AA)|((([A-Z-[QVX]][0-9][0-9]?)|(([A-Z-[QVX]][A-Z-[IJZ]][0-9][0-9]?)|(([A-Z-[QVX]][0-9][A-HJKSTUW])|([A-Z-[QVX]][A-Z-[IJZ]][0-9][ABEHMNPRVWXY]))))\s?[0-9][A-Z-[CIKMOV]]{2}))\b", RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches a 'symbol' (digit followed by an optional th, rd or separator) then a month name (e.g. Jan or January)
    /// </summary>
    readonly Regex _symbolThenMonth = new(@"\d+((th)|(rd)|(st)|[\-/\\])?\s?((Jan(uary)?)|(Feb(ruary)?)|(Mar(ch)?)|(Apr(il)?)|(May)|(June?)|(July?)|(Aug(ust)?)|(Sep(tember)?)|(Oct(ober)?)|(Nov(ember)?)|(Dec(ember)?))", RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches a month name (e.g. Jan or January) followed by a 'symbol' (digit followed by an optional th, rd or separator) then a
    /// </summary>
    readonly Regex _monthThenSymbol = new(@"((Jan(uary)?)|(Feb(ruary)?)|(Mar(ch)?)|(Apr(il)?)|(May)|(June?)|(July?)|(Aug(ust)?)|(Sep(tember)?)|(Oct(ober)?)|(Nov(ember)?)|(Dec(ember)?))[\s\-/\\]?\d+((th)|(rd)|(st))?", RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches digits followed by a separator (: - \ etc) followed by more digits with optional AM / PM / GMT at the end
    /// However this looks more like a time than a date and I would argue that times are not PII?
    /// It's also not restrictive enough so matches too many non-PII numerics.
    /// </summary>
    readonly Regex _date = new(
        @"\b\d+([:\-/\\]\d+)+\s?((AM)|(PM)|(GMT))?\b", RegexOptions.IgnoreCase);

    // The following regex were adapted from:
    // https://www.oreilly.com/library/view/regular-expressions-cookbook/9781449327453/ch04s04.html
    // Separators are space slash dash

    /// <summary>
    /// Matches year last, i.e d/m/y or m/d/y
    /// </summary>
    readonly Regex _dateYearLast = new(
        @"\b(?:(1[0-2]|0?[1-9])[ ]?[/-][ ]?(3[01]|[12][0-9]|0?[1-9])|(3[01]|[12][0-9]|0?[1-9])[ ]?[/-][ ]?(1[0-2]|0?[1-9]))[ ]?[/-][ ]?(?:[0-9]{2})?[0-9]{2}(\b|T)" // year last
    );
    /// <summary>
    /// Matches year first, i.e y/m/d or y/d/m
    /// </summary>
    readonly Regex _dateYearFirst = new(
        @"\b(?:[0-9]{2})?[0-9]{2}[ ]?[/-][ ]?(?:(1[0-2]|0?[1-9])[ ]?[/-][ ]?(3[01]|[12][0-9]|0?[1-9])|(3[01]|[12][0-9]|0?[1-9])[ ]?[/-][ ]?(1[0-2]|0?[1-9]))(\b|T)" // year first
    );
    /// <summary>
    /// Matches year missing, i.e d/m or m/d
    /// </summary>
    readonly Regex _dateYearMissing = new(
        @"\b(?:(1[0-2]|0?[1-9])[ ]?[/-][ ]?(3[01]|[12][0-9]|0?[1-9])|(3[01]|[12][0-9]|0?[1-9])[ ]?[/-][ ]?(1[0-2]|0?[1-9]))(\b|T)" // year missing
    );


    /// <summary>
    /// List of columns/tags which should not be processed.  This is automatically handled by the <see cref="Validate"/> method.
    /// <para>This is a case insensitive hash collection based on <see cref="IsIdentifiableBaseOptions.SkipColumns"/></para>
    /// </summary>
    private readonly HashSet<string> _skipColumns = new(StringComparer.CurrentCultureIgnoreCase);

    private readonly HashSet<string> _AllowList;

    /// <summary>
    /// Custom rules you want to apply e.g. always ignore column X if value is Y
    /// </summary>
    public List<IAppliableRule> CustomRules { get; set; } = new();

    /// <summary>
    /// Custom Allowlist rules you want to apply e.g. always ignore a failure if column is X AND value is Y
    /// </summary>
    public List<IAppliableRule> CustomAllowListRules { get; set; } = new();

    /// <summary>
    /// One cache per field in the data being evaluated, records the recent values passed to <see cref="Validate(string, string)"/> and the results to avoid repeated lookups
    /// </summary>
    public ConcurrentDictionary<string, MemoryCache> Caches { get; set; } = new();

    /// <summary>
    /// The maximum size of a Cache before we clear it out to prevent running out of RAM
    /// </summary>
    public int MaxValidationCacheSize { get; set; }

    /// <summary>
    /// Total number of calls to <see cref="Validate(string, string)"/> that were returned from the cache
    /// </summary>
    public long ValidateCacheHits { get; set; }

    /// <summary>
    /// Total number of calls to <see cref="Validate(string, string)"/> that were missing from the cache and run directly
    /// </summary>
    public long ValidateCacheMisses { get; set; }

    /// <summary>
    /// Total number of <see cref="FailurePart"/> identified during lifetime (see <see cref="Validate(string, string)"/>)
    /// </summary>
    public int CountOfFailureParts { get; protected set; }

    /// <summary>
    /// Duration the class has existed for
    /// </summary>
    private Stopwatch Lifetime { get; }

    /// <summary>
    /// Set this to a positive number to output on <see cref="LogProgressLevel"/> about the number
    /// of <see cref="LogProgressNoun"/> done
    /// </summary>
    public int? LogProgressEvery = null;

    /// <summary>
    /// Set the level for progress messages (e.g. done x records).  Note that you must
    /// also set <see cref="LogProgressEvery"/> for this to take effect
    /// </summary>
    public LogLevel LogProgressLevel = LogLevel.Trace;

    /// <summary>
    /// The unit of operation e.g. "rows", "records", "files" when reporting progress
    /// </summary>
    public string LogProgressNoun { get; protected set; } = "items";

    /// <summary>
    /// Cumulative total of completed items (see <see cref="LogProgressNoun"/>).  Dependent
    /// on subclasses correctly calling <see cref="DoneRows(int)"/>
    /// </summary>
    private long _done;

    /// <summary>
    /// Creates an instance and sets up <see cref="Reports"/> and output formats as specified
    /// in <paramref name="opts"/>
    /// </summary>
    /// <param name="opts">Configuration class that stores where rules should be loaded from, whether to consider dates identifiable etc</param>
    /// <param name="fileSystem"></param>
    /// <param name="customReports">Any custom reports that should be added in addition to those specified in <paramref name="opts"/></param>
    /// <exception cref="Exception"></exception>
    protected IsIdentifiableAbstractRunner(IsIdentifiableBaseOptions opts, IFileSystem fileSystem, params IFailureReport[] customReports)
    {
        FileSystem = fileSystem;

        Lifetime = Stopwatch.StartNew();
        _opts = opts;
        _opts.ValidateOptions();
        MaxValidationCacheSize = opts.MaxValidationCacheSize ?? IsIdentifiableBaseOptions.MaxValidationCacheSizeDefault;

        var targetName = _opts.GetTargetName(FileSystem);

        if (opts.ColumnReport)
            Reports.Add(new ColumnFailureReport(targetName, fileSystem));

        if (opts.ValuesReport)
            Reports.Add(new FailingValuesReport(targetName, fileSystem));
            
        if (opts.StoreReport)
            Reports.Add(new FailureStoreReport(targetName, _opts.MaxCacheSize ?? IsIdentifiableBaseOptions.MaxCacheSizeDefault,FileSystem));

        // add custom reports
        foreach (var report in customReports)
            Reports.Add(report);

        // complain if no reports
        if (!Reports.Any())
            throw new Exception("No reports have been specified, use the relevant command line flag e.g. --ColumnReport");

        Reports.ForEach(r => r.AddDestinations(_opts));

        if (!string.IsNullOrWhiteSpace(_opts.SkipColumns))
            foreach (var c in _opts.SkipColumns.Split(','))
                _skipColumns.Add(c);

        // is there a single rules yaml file specified?
        if (!string.IsNullOrWhiteSpace(opts.RulesFile))
        {
            var fi = FileSystem.FileInfo.New(_opts.RulesFile);
            if (fi.Exists)
            {
                LoadRules(FileSystem.File.ReadAllText(fi.FullName));
            }   
            else
            {
                // file specified did not exist... but that's ok if it's the default (_rules.yaml)
                if (_opts.RulesFile != IsIdentifiableBaseOptions.DefaultRulesFile)
                {
                    throw new Exception($"Error reading {_opts.RulesFile}");
                }
            }
                
                    
        }

        if (!string.IsNullOrWhiteSpace(opts.RulesDirectory))
        {
            var di = FileSystem.DirectoryInfo.New(opts.RulesDirectory);
            var loadedAtLeastOne = false;
            foreach (var fi in di.GetFiles("*.yaml"))
            {
                _logger.Info($"Loading rules from {fi.Name}");
                var any = LoadRules(FileSystem.File.ReadAllText(fi.FullName));

                if(!any)
                {
                    _logger.Warn($"_rules file {fi.FullName} had no rules in it");
                }
                loadedAtLeastOne = loadedAtLeastOne || any; 
            }

            if(!loadedAtLeastOne)
            {
                throw new Exception($"RulesDirectory {opts.RulesDirectory} did not contain any rules files containing rules");
            }
        }

        SortRules();

        IAllowListSource source;

        try
        {
            source = GetAllowListSource();
        }
        catch (Exception e)
        {
            throw new Exception("Error getting AllowList Source", e);
        }
            
        if (source != null)
        {
            _logger.Info("Fetching Allowlist...");
            try
            {
                _AllowList = new HashSet<string>(source.GetAllowList(),StringComparer.CurrentCultureIgnoreCase);
            }
            catch (Exception e)
            {
                throw new Exception($"Error fetching values for {nameof(IAllowListSource)} {source.GetType().Name}", e);
            }

            _logger.Info($"Allowlist built with {_AllowList.Count} exact strings");
        }
    }

    /// <summary>
    /// Sorts <see cref="CustomRules"/> according to their action.  This ensures that
    /// <see cref="RuleAction.Ignore"/> rules operate before <see cref="RuleAction.Report"/>
    /// preventing conflicting rules.
    /// </summary>
    public void SortRules()
    {
        CustomRules = CustomRules.OrderByDescending(OrderWeight).ToList();
    }

    private int OrderWeight(IAppliableRule arg)
    {
        return arg switch
        {
            IRegexRule irule => irule.Action switch
            {
                RuleAction.None => -6000,
                //ignore rules float to the top
                RuleAction.Ignore => 100,
                //then consider the report explicit rules (by pattern)
                RuleAction.Report => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(arg),$"Invalid action {irule.Action} for {nameof(IRegexRule)} {arg}")
            },
            //socket rules sink to the bottom
            SocketRule => -5000,
            //ConsensusRules should sink to the bottom but just above SocketRules (if any)
            ConsensusRule => -3000,
            _ => -50
        };

        //some odd custom rule type that is not a socket or basic rule, do them after the regular reports but before sockets
    }
        
    /// <summary>
    /// Generates a deserializer suitable for deserialzing <see cref="RuleSet"/> for use with this class (see also <see cref="LoadRules(string)"/>)
    /// </summary>
    /// <returns></returns>
    public static IDeserializer GetDeserializer()
    {
        var builder = new DeserializerBuilder();
        builder.WithTagMapping("!SocketRule", typeof(SocketRule));
        builder.WithTagMapping("!AllowlistRule", typeof(AllowlistRule));
        builder.WithTagMapping("!IRegexRule", typeof(IRegexRule));

        return builder.Build();
    }

    /// <summary>
    /// Deserializes the given <paramref name="yaml"/> into a collection of <see cref="IRegexRule"/>
    /// which are added to <see cref="CustomRules"/>
    /// </summary>
    /// <param name="yaml"></param>
    /// <returns>True if the yaml read was deserialized into a <see cref="RuleSet"/> with at least 1 rule</returns>
    public bool LoadRules(string yaml)
    {
        var result = false;

        _logger.Info("Loading _rules Yaml");
        _logger.Debug($"Loading _rules Yaml:{Environment.NewLine}{yaml}");
        var deserializer = GetDeserializer();
        var ruleSet = deserializer.Deserialize<RuleSet>(yaml);

        // an empty or comments only yaml file will give a null return value
        if (ruleSet == null)
            return false;

        if(ruleSet.BasicRules != null)
        {
            CustomRules.AddRange(ruleSet.BasicRules);
            result = result || ruleSet.BasicRules.Any();
        }
            

        if(ruleSet.SocketRules != null)
        {
            CustomRules.AddRange(ruleSet.SocketRules);
            result = result || ruleSet.SocketRules.Any();
        }

        if (ruleSet.ConsensusRules != null)
        {
            CustomRules.AddRange(ruleSet.ConsensusRules);
            result = result || ruleSet.ConsensusRules.Any();
        }
            

        if(ruleSet.AllowlistRules != null)
        {
            CustomAllowListRules.AddRange(ruleSet.AllowlistRules);
            result = result || ruleSet.AllowlistRules.Any();
        }

        return result;
    }

    /// <summary>
    /// When overridden fetches and evaluates all data from the source
    /// and streams failing (identifiable) data reports to <see cref="Reports"/>
    /// </summary>
    /// <returns>0 for success</returns>
    public abstract int Run();
        
    /// <summary>
    /// Returns each subsection of <paramref name="fieldValue"/> which violates validation rules (e.g. the CHI found).
    /// </summary>
    /// <param name="fieldName"></param>
    /// <param name="fieldValue"></param>
    /// <returns></returns>
    protected virtual IEnumerable<FailurePart> Validate(string fieldName, string fieldValue)
    {
        // make sure that we have a cache for this column name
        var cache = Caches.GetOrAdd(fieldName,(v)=>new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = MaxValidationCacheSize
        }));
            
        //if we have the cached result use it
        if(cache.TryGetValue(fieldValue ?? "NULL",out FailurePart[] result))
        {
            ValidateCacheHits++;
            CountOfFailureParts += result.Length;
            return result;
        }
            
        ValidateCacheMisses++;

        //otherwise run ValidateImpl and cache the result
        var freshResult = ValidateImpl(fieldName,fieldValue).ToArray();
        CountOfFailureParts += freshResult.Length;
        return cache.Set(fieldValue?? "NULL", freshResult, new MemoryCacheEntryOptions() {
            Size=1
        });
    }
        
    /// <summary>
    /// Actual implementation of <see cref="Validate(string, string)"/> after a cache miss has occurred.  This method is only called when a cached answer is not found for the given <paramref name="fieldName"/> and <paramref name="fieldValue"/> pair
    /// </summary>
    /// <param name="fieldName"></param>
    /// <param name="fieldValue"></param>
    /// <returns></returns>
    protected virtual IEnumerable<FailurePart> ValidateImpl(string fieldName, string fieldValue)
    {
        if (_skipColumns.Contains(fieldName))
            yield break;

        if (string.IsNullOrWhiteSpace(fieldValue))
            yield break;

        // Carets (^) are synonymous with space in some dicom tags
        fieldValue = fieldValue.Replace('^', ' ');

        //if there is a Allowlist and it says to ignore the (full string) value
        if (_AllowList?.Contains(fieldValue.Trim()) == true)
            yield break;
                    
        //for each custom rule
        foreach (var rule in CustomRules)
        {
            switch (rule.Apply(fieldName, fieldValue, out var parts))
            {
                case RuleAction.None:
                    break;
                //if rule is to skip the cell (i.e. don't run other classifiers)
                case RuleAction.Ignore:
                    yield break;
                    
                //if the rule is to report it then report as a failure but also run other classifiers
                case RuleAction.Report:
                    foreach (var p in parts)
                        if (CustomAllowListRules.Cast<AllowlistRule>().All(allowListRule => allowListRule.ApplyAllowlistRule(fieldName, fieldValue, p) != RuleAction.Ignore))
                            yield return p;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        //does the string contain chis which represent an actual date?
        foreach (var m in _chiRegex.Matches(fieldValue).Where(m => DateTime.TryParseExact(m.Value[..6], "ddMMyy",
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out _)))
            yield return new FailurePart(m.Value, FailureClassification.PrivateIdentifier, m.Index);

        if (!_opts.IgnorePostcodes)
            foreach (Match m in _postcodeRegex.Matches(fieldValue))
                yield return new FailurePart(m.Value, FailureClassification.Postcode, m.Index);

        if (!_opts.IgnoreDatesInText)
        {
            foreach (Match m in _dateYearFirst.Matches(fieldValue))
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            foreach (Match m in _dateYearLast.Matches(fieldValue))
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            // XXX this may cause a duplicate failure if one above yields
            foreach (Match m in _dateYearMissing.Matches(fieldValue))
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            foreach (Match m in _symbolThenMonth.Matches(fieldValue))
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            foreach (Match m in _monthThenSymbol.Matches(fieldValue))
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

        }
    }

    /// <summary>
    /// Records the provided failure to all selected reports
    /// </summary>
    /// <param name="f"></param>
    protected void AddToReports(Failure f)
    {
        Reports.ForEach(r => r.Add(f));
    }
    

    /// <summary>
    /// Tells all selected reports that the <paramref name="numberOfRowsDone"/> have been processed (this is a += operation 
    /// not substitution i.e. call it with 10 then 10 again then 10 leads to 30 rows done.
    /// </summary>
    /// <param name="numberOfRowsDone">Number of rows done since the last call to this method</param>
    protected void DoneRows(int numberOfRowsDone)
    {
        LogProgress(Interlocked.Add(ref _done, numberOfRowsDone),false);
        Reports.ForEach(r => r.DoneRows(numberOfRowsDone));
    }

    /// <summary>
    /// Call once you have done all validation, this method will write the report results to the final destination 
    /// e.g. CSV etc
    /// </summary>
    protected void CloseReports()
    {
        Reports.ForEach(r => r.CloseReport());
        LogProgress(_done, true);
    }

    private IAllowListSource GetAllowListSource()
    {
        IAllowListSource source = null;

        if (!string.IsNullOrWhiteSpace(_opts.AllowlistCsv))
        {
            // If there's a file AllowList
            source = new CsvAllowList(_opts.AllowlistCsv, FileSystem);
            _logger.Info($"Loaded a Allowlist from {FileSystem.Path.GetFullPath(_opts.AllowlistCsv)}");
        }
        else if (!string.IsNullOrWhiteSpace(_opts.AllowlistConnectionString) && _opts.AllowlistDatabaseType.HasValue)
        {
            // If there's a database AllowList
            var tbl = GetServer(_opts.AllowlistConnectionString, _opts.AllowlistDatabaseType.Value, _opts.AllowlistTableName);
            var col = tbl.DiscoverColumn(_opts.AllowlistColumn);
            source = new DiscoveredColumnAllowList(col);
            _logger.Info($"Loaded a Allowlist from {tbl.GetFullyQualifiedName()}");
        }

        return source;
    }

    /// <summary>
    /// Connects to the specified database and returns a managed object for interacting with it.
    /// 
    /// <para>This method will check that the table exists on the server</para>
    /// </summary>
    /// <param name="databaseConnectionString">Connection string (which must include database element)</param>
    /// <param name="databaseType">The DBMS provider of the database referenced by <paramref name="databaseConnectionString"/></param>
    /// <param name="tableName">Unqualified table name e.g. "mytable"</param>
    /// <returns></returns>
    protected DiscoveredTable GetServer(string databaseConnectionString, DatabaseType databaseType, string tableName)
    {
        var db = GetServer(databaseConnectionString, databaseType);
        var tbl = db.ExpectTable(tableName);

        if (!tbl.Exists())
            throw new Exception("Table did not exist");

        _logger.Log(LogLevel.Info, $"Found Table '{tbl.GetRuntimeName()}'");

        return tbl;
    }

    /// <summary>
    /// Connects to the specified database and returns a managed object for interacting with it.
    /// </summary>
    /// <param name="databaseConnectionString">Connection string (which must include database element)</param>
    /// <param name="databaseType">The DBMS provider of the database referenced by <paramref name="databaseConnectionString"/></param>
    /// <returns></returns>
    private static DiscoveredDatabase GetServer(string databaseConnectionString, DatabaseType databaseType)
    {
        var server = new DiscoveredServer(databaseConnectionString, databaseType);

        var db = server.GetCurrentDatabase()??throw new Exception("No current database");

        return db;
    }


    /// <summary>
    /// Logs how many records have been done if <see cref="LogProgressEvery"/> matches
    /// a tick in <paramref name="progress"/>.
    /// </summary>
    /// <param name="progress">How many have you done so far?</param>
    /// <param name="force">True to ignore whether <paramref name="progress"/> is a multiple of <see cref="LogProgressEvery"/></param>
    private void LogProgress(long progress, bool force)
    {
        if (LogProgressEvery == null && !force)
            return;

        if(force || progress % LogProgressEvery.Value == 0)
        {
            _logger.Log(LogProgressLevel, $"Done {progress} {LogProgressNoun}");
        }        
    }

    /// <summary>
    /// Closes and disposes of resources including outputting final totals into logs
    /// and disposing rules which require custom disposing (e.g. closing sockets to
    /// NLP services).
    /// </summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var d in CustomRules.OfType<IDisposable>()) 
            d.Dispose();

        _logger?.Info($"Total runtime for {GetType().Name}:{Lifetime.Elapsed}");
        _logger?.Info($"ValidateCacheHits:{ValidateCacheHits} Total ValidateCacheMisses:{ValidateCacheMisses}");
        _logger?.Info($"Total FailurePart identified: {CountOfFailureParts}");
    }
}

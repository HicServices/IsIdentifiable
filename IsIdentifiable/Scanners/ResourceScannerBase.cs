using FAnsi.Discovery;
using IsIdentifiable.AllowLists;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Rules;
using IsIdentifiable.Util;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace IsIdentifiable.Scanners;

/// <summary>
/// Base class for all classes which evaluate data sources to detect identifiable data.
/// Subclass to add support for new data sources.  Current sources include reading from
/// CSV files, Dicom files and database tables.
/// </summary>
public abstract class ResourceScannerBase : IDisposable, IResourceScanner
{
    /// <inheritdoc/>
    public int FailureCount { get; private set; }

    protected readonly ILogger Logger;

    // TODO(rkm 2023-07-05) See if this is still used after refactoring to inject all reports
    protected readonly IFileSystem FileSystem;

    protected volatile int ItemsProcessed;

    private readonly List<IFailureReport> _reports = new();
    private readonly HashSet<string> _skipKeys = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly HashSet<string>? _allowList;

    // TODO(rkm 2023-07-03) Refactor into SortedList
    private List<IAppliableRule> _customRules = new();

    private readonly List<IAppliableRule> _customAllowListRules = new();
    private readonly ConcurrentDictionary<string, MemoryCache> _caches = new();
    private readonly int _validationCacheLimit;
    private readonly bool _ignoreDatesInText;
    private readonly bool _ignorePostcodes;
    private long _validationCacheHits;
    private long _validationCacheMisses;
    private int _countOfFailureParts;
    private readonly Stopwatch _lifetime = Stopwatch.StartNew();
    private readonly int _logProgressEvery = 0;

    protected ResourceScannerBase(
        ScannerBaseOptions options,
        IFileSystem fileSystem,
        params IFailureReport[] reports
    )
    {
        if (!_reports.Any())
            throw new Exception("At least one report must be specified");

        Logger = LogManager.GetLogger(GetType().Name);
        FileSystem = fileSystem;
        _validationCacheLimit = options.ValidationCacheLimit;
        _ignoreDatesInText = options.IgnoreDatesInText;
        _ignorePostcodes = options.IgnorePostcodes;

        if (!string.IsNullOrWhiteSpace(options.SkipColumns))
            foreach (var c in options.SkipColumns.Split(','))
                _skipKeys.Add(c);

        // TODO(rkm 2023-07-03) Inject all rules
        if (!string.IsNullOrWhiteSpace(options.RulesFile))
        {
            var fi = FileSystem.FileInfo.New(options.RulesFile);
            if (!fi.Exists)
                throw new Exception($"Specified rules file does not exist: {options.RulesFile}");

            LoadRules(FileSystem.File.ReadAllText(fi.FullName));
        }
        else if (!string.IsNullOrWhiteSpace(options.RulesDirectory))
        {
            var di = FileSystem.DirectoryInfo.New(options.RulesDirectory);
            var loadedAtLeastOne = false;
            foreach (var fi in di.GetFiles("*.yaml"))
            {
                Logger.Info($"Loading rules from {fi.Name}");
                LoadRules(FileSystem.File.ReadAllText(fi.FullName));
                loadedAtLeastOne = true;
            }

            if (!loadedAtLeastOne)
                throw new Exception($"RulesDirectory {options.RulesDirectory} did not contain any rules files containing rules");
        }
        else
        {
            throw new Exception($"Exactly one of RulesFile or RulesDirectory must be specified");
        }

        _customRules = _customRules.OrderByDescending(OrderWeight).ToList();

        // TODO(rkm 2023-07-03) Inject this via constructor
        IAllowListSource? source = null;

        if (!string.IsNullOrWhiteSpace(options.AllowListFile))
        {
            source = new CsvAllowList(options.AllowListFile, FileSystem);
            Logger.Info($"Loaded a Allowlist from {FileSystem.Path.GetFullPath(options.AllowListFile)}");
        }
        else if (!string.IsNullOrWhiteSpace(options.AllowListConnectionString) && options.AllowListDatabaseType.HasValue)
        {
            var databaseType = options.AllowListDatabaseType ?? throw new ArgumentException(nameof(options.AllowListDatabaseType));
            var server = new DiscoveredServer(options.AllowListConnectionString, databaseType);
            var table = server.GetCurrentDatabase().ExpectTable(options.AllowListTableName);
            var col = table.DiscoverColumn(options.AllowListColumn);
            source = new DiscoveredColumnAllowList(col);
            Logger.Info($"Loaded a Allowlist from {table.GetFullyQualifiedName()}");
        }

        if (source != null)
        {
            Logger.Info("Fetching Allowlist");

            try
            {
                _allowList = new HashSet<string>(source.GetAllowList(), StringComparer.CurrentCultureIgnoreCase);
            }
            catch (Exception e)
            {
                throw new Exception($"Error fetching values for {nameof(IAllowListSource)} {source.GetType().Name}", e);
            }

            Logger.Info($"Allowlist built with {_allowList.Count} exact strings");
        }
    }

    /// <summary>
    /// Sorts <see cref="_customRules"/> according to their action.  This ensures that
    /// <see cref="RuleAction.Ignore"/> rules operate before <see cref="RuleAction.Report"/>
    /// preventing conflicting rules.
    /// </summary>
    public void SortRules()
    {
        // TODO(rkm 2023-06-30) Change to sorted list?
        _customRules = _customRules.OrderByDescending(OrderWeight).ToList();
    }

    private int OrderWeight(IAppliableRule arg)
    {
        return arg switch
        {
            RegexRule irule => irule.Action switch
            {
                RuleAction.None => -6000,
                //ignore rules float to the top
                RuleAction.Ignore => 100,
                //then consider the report explicit rules (by pattern)
                RuleAction.Report => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(arg), $"Invalid action {irule.Action} for {nameof(RegexRule)} {arg}")
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
    /// Deserializes the given <paramref name="yaml"/> into a collection of <see cref="RegexRule"/>
    /// which are added to <see cref="_customRules"/>
    /// </summary>
    /// <param name="yaml"></param>
    /// <returns>True if the yaml read was deserialized into a <see cref="RuleSet"/> with at least 1 rule</returns>
    private void LoadRules(string yaml)
    {
        Logger.Info("Loading Rules Yaml");
        Logger.Debug($"Loading Rules Yaml:{Environment.NewLine}{yaml}");

        var ruleSet = RuleHelpers.GetRuleDeserializer().Deserialize<RuleSet>(yaml) ?? throw new ArgumentException($"Specififed file did not contain any rules");

        var foundRules = false;

        if (ruleSet.BasicRules != null)
        {
            _customRules.AddRange(ruleSet.BasicRules);
            foundRules = true;
        }

        if (ruleSet.SocketRules != null)
        {
            _customRules.AddRange(ruleSet.SocketRules);
            foundRules = true;
        }

        if (ruleSet.ConsensusRules != null)
        {
            _customRules.AddRange(ruleSet.ConsensusRules);
            foundRules = true;
        }

        if (ruleSet.AllowlistRules != null)
        {
            _customAllowListRules.AddRange(ruleSet.AllowlistRules);
            foundRules = true;
        }

        if (!foundRules)
            throw new ArgumentException($"Specififed file did not contain any rules");
    }

    /// <summary>
    /// Returns each subsection of <paramref name="fieldValue"/> which violates validation rules (e.g. the CHI found).
    /// </summary>
    /// <param name="fieldName"></param>
    /// <param name="fieldValue"></param>
    /// <returns></returns>
    protected virtual IEnumerable<FailurePart> Validate(string fieldName, string fieldValue)
    {
        // make sure that we have a cache for this column name
        var cache = _caches.GetOrAdd(fieldName, (v) => new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = _validationCacheLimit
        }));

        //if we have the cached result use it
        if (cache.TryGetValue(fieldValue, out FailurePart[]? result))
        {
            _validationCacheHits++;
            _countOfFailureParts += result!.Length;
            return result;
        }

        _validationCacheMisses++;

        //otherwise run ValidateImpl and cache the result
        var freshResult = ValidateImpl(fieldName, fieldValue).ToArray();
        _countOfFailureParts += freshResult.Length;
        return cache.Set(fieldValue, freshResult, new MemoryCacheEntryOptions()
        {
            Size = 1
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
        if (_skipKeys.Contains(fieldName))
            yield break;

        if (string.IsNullOrWhiteSpace(fieldValue))
            yield break;

        // Carets (^) are synonymous with space in some dicom tags
        fieldValue = fieldValue.Replace('^', ' ');

        //if there is a Allowlist and it says to ignore the (full string) value
        if (_allowList?.Contains(fieldValue.Trim()) == true)
            yield break;

        //for each custom rule
        foreach (var rule in _customRules)
        {
            var ruleAction = rule.Apply(fieldName, fieldValue, out var parts);
            switch (ruleAction)
            {
                case RuleAction.None:
                    break;
                //if rule is to skip the cell (i.e. don't run other classifiers)
                case RuleAction.Ignore:
                    yield break;

                //if the rule is to report it then report as a failure but also run other classifiers
                case RuleAction.Report:
                    foreach (var p in parts)
                        if (_customAllowListRules.Cast<AllowlistRule>().All(allowListRule => allowListRule.ApplyAllowlistRule(fieldName, fieldValue, p) != RuleAction.Ignore))
                            yield return p;
                    break;
                default:
                    throw new Exception($"No case for {ruleAction}");
            }
        }

        //does the string contain chis which represent an actual date?
        foreach (var m in RegexConstants.ChiRegex.Matches(fieldValue).Where(m => DateTime.TryParseExact(m.Value[..6], "ddMMyy",
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out _)))
            yield return new FailurePart(m.Value, FailureClassification.PrivateIdentifier, m.Index);

        if (!_ignorePostcodes)
            foreach (Match m in RegexConstants.PostcodeRegex.Matches(fieldValue).Cast<Match>())
                yield return new FailurePart(m.Value, FailureClassification.Postcode, m.Index);

        if (!_ignoreDatesInText)
        {
            foreach (Match m in RegexConstants.DateYearFirst.Matches(fieldValue).Cast<Match>())
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            foreach (Match m in RegexConstants.DateYearLast.Matches(fieldValue).Cast<Match>())
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            // XXX this may cause a duplicate failure if one above yields
            foreach (Match m in RegexConstants.DateYearMissing.Matches(fieldValue).Cast<Match>())
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            foreach (Match m in RegexConstants.SymbolThenMonth.Matches(fieldValue).Cast<Match>())
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);

            foreach (Match m in RegexConstants.MonthThenSymbol.Matches(fieldValue).Cast<Match>())
                yield return new FailurePart(m.Value.TrimEnd(), FailureClassification.Date, m.Index);
        }
    }

    protected void NotifyNewFailure(Failure f)
    {
        ++FailureCount;
        _reports.ForEach(r => r.Add(f));
    }

    protected void NotifyDoneRows(int numberOfRowsDone)
    {
        LogProgress(Interlocked.Add(ref ItemsProcessed, numberOfRowsDone), false);
        _reports.ForEach(r => r.DoneRows(numberOfRowsDone));
    }

    protected void CloseReports()
    {
        LogProgress(ItemsProcessed, true);
        _reports.ForEach(r => r.CloseReport());
    }

    private void LogProgress(long progress, bool force)
    {
        if (_logProgressEvery > 0 && !force)
            return;

        if (force || progress % _logProgressEvery == 0)
            Logger.Debug($"Done {progress} {LogProgressNoun()}");
    }

    protected abstract string LogProgressNoun();

    /// <summary>
    /// Closes and disposes of resources including outputting final totals into logs
    /// and disposing rules which require custom disposing (e.g. closing sockets to
    /// NLP services).
    /// </summary>
    public virtual void Dispose()
    {
        DisposeImpl();

        GC.SuppressFinalize(this);
        foreach (var d in _customRules.OfType<IDisposable>())
            d.Dispose();

        Logger?.Info($"Total runtime for {GetType().Name}:{_lifetime.Elapsed}");
        Logger?.Info($"_validationCacheHits:{_validationCacheHits} Total _validationCacheMisses:{_validationCacheMisses}");
        Logger?.Info($"Total FailurePart identified: {_countOfFailureParts}");
    }

    protected abstract void DisposeImpl();

    // TODO(rkm 2023-07-05) Implement here & in derived classes
    public void Reset()
    {
        FailureCount = 0;
        throw new NotImplementedException();
    }
}

// todo constructor opts class to separate
// todo separate output file to elsewhere

using FAnsi.Discovery;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting.UpdateStrategies;
using IsIdentifiable.Reporting.Destinations;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Rules;
using IsIdentifiable.Rules.Storage;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;

namespace IsIdentifiable.Redacting;

/// <summary>
/// CLI no user interaction mode for running the reviewer application.  In this mode all <see cref="Failure"/> in a table are
/// run through an existing rules base (for detecting true/false positives) and the database is updated to perform redactions.  
/// Any failures not covered by existing rules are routed to <see cref="IsIdentifiableReviewerOptions.UnattendedOutputPath"/>
/// </summary>
public class DatabaseRedactor
{
    private readonly Target _target;
    private readonly ReportReader _reportReader;

    private readonly IRegexRuleStore _ignoreActionRuleStore;
    private readonly IRegexRuleStore _reportActionRuleStore;

    private readonly IFileSystem _fileSystem;
    private readonly IFileInfo _outputFile;

    private readonly DiscoveredServer _discoveredServer;
    readonly Dictionary<DiscoveredTable, DiscoveredColumn> _primaryKeys = new();

    /// <summary>
    /// The number of <see cref="Failure"/> that were redacted in the database.  Where there are multiple UPDATE statements run per failure, Redacts will only be incremented once.
    /// </summary>
    public int Redacts;

    /// <summary>
    /// The number of input <see cref="Failure"/> that were ignored as false positives based on existing ignore rules
    /// </summary>
    public int Ignores;

    /// <summary>
    /// The number of input <see cref="Failure"/> that were not covered by any existing rules
    /// </summary>
    public int Unresolved;

    /// <summary>
    /// Total number of <see cref="Failure"/> processed so far
    /// </summary>
    public int Total;

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<IRegexRule, int> _reportRulesUsed = new();
    private readonly Dictionary<IRegexRule, int> _ignoreRulesUsed = new();

    private readonly IUpdateStrategy _updateStrategy = new RegexUpdateStrategy();


    /// <summary>
    /// Creates a new instance that will connect to the database server (<paramref name="target"/>) and perform redactions using the <paramref name="updater"/>
    /// </summary>
    /// <param name="options"></param>
    /// <param name="discoveredServer"></param>
    /// <param name="ignoreActionRuleStore"></param>
    /// <param name="reportActionRuleStore"></param>
    /// <param name="fileSystem"></param>
    public DatabaseRedactor(DatabaseRedactorCliOptions options, DiscoveredServer discoveredServer, RegexRuleStore ignoreActionRuleStore, RegexRuleStore reportActionRuleStore, IFileSystem fileSystem)
    {
        _discoveredServer = discoveredServer;

        _ignoreActionRuleStore = ignoreActionRuleStore;
        _reportActionRuleStore = reportActionRuleStore;

        _fileSystem = fileSystem;

        if (string.IsNullOrWhiteSpace(options.FailuresCsv))
            throw new Exception("Unattended requires a file of errors to process");

        var fi = _fileSystem.FileInfo.New(options.FailuresCsv);

        if (!fi.Exists)
            throw new System.IO.FileNotFoundException($"Could not find Failures file '{fi.FullName}'");

        _reportReader = new ReportReader(fi);

        if (string.IsNullOrWhiteSpace(options.UnattendedOutputPath))
            throw new Exception("An output path must be specified for Failures that could not be resolved");

        _outputFile = _fileSystem.FileInfo.New(options.UnattendedOutputPath);
    }

    /// <summary>
    /// Connects to the database and runs all failures through the rules base performing redactions as required
    /// </summary>
    /// <returns></returns>
    public void Run()
    {
        //In RulesOnly mode this will be null
        var server = _target?.Discover();
        var errors = new List<Exception>();

        var storeReport = new FailureStoreReport(_outputFile.Name, 100, _fileSystem);

        var sw = new Stopwatch();
        sw.Start();

        using (var storeReportDestination = new CsvDestination(new IsIdentifiableDicomFileOptions(), _outputFile, _fileSystem))
        {
            storeReport.AddDestination(storeReportDestination);


            while (_reportReader.Next())
            {
                var failure = _reportReader.CurrentFailure;
                var isCoveredByReportRule = _reportActionRuleStore.HasRuleCovering(failure, out var rule);

                if (isCoveredByReportRule)
                {
                    try
                    {
                        Redact(failure, rule);
                    }
                    catch (Exception e)
                    {
                        errors.Add(e);
                        continue;
                    }

                    if (!_reportRulesUsed.TryAdd(rule, 1))
                        _reportRulesUsed[rule]++;

                    Redacts++;
                }
                else
                {
                    var isCoveredByIgnoreRule = _ignoreActionRuleStore.HasRuleCovering(failure, out rule);

                    if (isCoveredByIgnoreRule)
                    {
                        if (!_ignoreRulesUsed.TryAdd(rule, 1))
                            _ignoreRulesUsed[rule]++;

                        Ignores++;
                    }
                    else
                    {
                        // we can't process it unattended
                        storeReport.Add(_reportReader.CurrentFailure);
                        Unresolved++;
                    }
                }

                Total++;

                if (Total % 10000 == 0 || sw.ElapsedMilliseconds > 5000)
                {
                    Log($"Done {Total:N0} u={Redacts:N0} i={Ignores:N0} o={Unresolved:N0} err={errors.Count:N0}", true);
                    sw.Restart();
                }
            }

            storeReport.CloseReport();
        }

        Log($"Ignore _rules Used:{Environment.NewLine}{string.Join(Environment.NewLine, _ignoreRulesUsed.OrderBy(k => k.Value).Select(k => $"{k.Key.IfPattern} - {k.Value:N0}"))}", false);
        Log($"Update _rules Used:{Environment.NewLine}{string.Join(Environment.NewLine, _reportRulesUsed.OrderBy(k => k.Value).Select(k => $"{k.Key.IfPattern} - {k.Value:N0}"))}", false);
        Log($"Errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => e.ToString()))}", false);
        Log($"Finished {Total:N0} updates={Redacts:N0} ignored={Ignores:N0} out={Unresolved:N0} err={errors.Count:N0}", true);
    }

    // todo
    private void Redact(Failure failure, IRegexRule usingRule)
    {
        var syntax = _discoveredServer.GetQuerySyntaxHelper();

        //the fully specified name e.g. [mydb]..[mytbl]
        var tableName = failure.Resource;

        var tokens = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        var db = tokens.First();
        tableName = tokens.Last();

        if (string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(tableName) || string.Equals(db, tableName))
            throw new NotSupportedException($"Could not understand table name {failure.Resource}, maybe it is not full specified with a valid database and table name?");

        db = syntax.GetRuntimeName(db);
        tableName = syntax.GetRuntimeName(tableName);

        var table = _discoveredServer.ExpectDatabase(db).ExpectTable(tableName);

        //if we've never seen this table before
        if (!_primaryKeys.ContainsKey(table))
        {
            var pk = table.DiscoverColumns().SingleOrDefault(k => k.IsPrimaryKey);
            _primaryKeys.Add(table, pk);
        }

        using var con = _discoveredServer.GetConnection();
        con.Open();

        var commands = _updateStrategy.GetUpdateSql(table, _primaryKeys, failure, usingRule).Select(sql => _discoveredServer.GetCommand(sql, con)).ToList();
        foreach (var command in commands)
            command.ExecuteNonQuery();
    }

    private void Log(string msg, bool toConsole)
    {
        _logger.Info(msg);
        if (toConsole)
            Console.WriteLine(msg);
    }
}

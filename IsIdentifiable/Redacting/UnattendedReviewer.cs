using FAnsi.Discovery;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Destinations;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Rules;
using IsIdentifiable.Util;
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
public class UnattendedReviewer
{
    private readonly DatabaseTargetOptions? _targetOptions;
    private readonly ReportReader _reportReader;
    private readonly RowUpdater _updater;
    private readonly IgnoreRuleGenerator _ignorer;
    private readonly IFileSystem _fileSystem;
    private readonly IFileInfo _outputFile;

    /// <summary>
    /// The number of <see cref="Failure"/> that were redacted in the database.  Where there are multiple UPDATE statements run per failure, Updates will only be incremented once.
    /// </summary>
    public int Updates = 0;

    /// <summary>
    /// The number of input <see cref="Failure"/> that were ignored as false positives based on existing ignore rules
    /// </summary>
    public int Ignores = 0;

    /// <summary>
    /// The number of input <see cref="Failure"/> that were not covered by any existing rules
    /// </summary>
    public int Unresolved = 0;

    /// <summary>
    /// Total number of <see cref="Failure"/> processed so far
    /// </summary>
    public int Total = 0;
    private readonly Logger _log;

    readonly Dictionary<RegexRule, int> _updateRulesUsed = new();
    readonly Dictionary<RegexRule, int> _ignoreRulesUsed = new();

    /// <summary>
    /// Creates a new instance that will connect to the database server (<paramref name="target"/>) and perform redactions using the <paramref name="updater"/>
    /// </summary>
    /// <param name="opts">CLI options for the process</param>
    /// <param name="target">DBMS to connect to for redacting</param>
    /// <param name="ignorer">Rules base for detecting false positives</param>
    /// <param name="updater">Rules base for redacting true positives</param>
    /// <param name="fileSystem"></param>
    public UnattendedReviewer(IsIdentifiableReviewerOptions opts, DatabaseTargetOptions? target, IgnoreRuleGenerator ignorer, RowUpdater updater, IFileSystem fileSystem)
    {
        _log = LogManager.GetCurrentClassLogger();

        _fileSystem = fileSystem;

        if (string.IsNullOrWhiteSpace(opts.FailuresCsv))
            throw new Exception("Unattended requires a file of errors to process");

        var fi = _fileSystem.FileInfo.New(opts.FailuresCsv);

        if (!fi.Exists)
            throw new System.IO.FileNotFoundException($"Could not find Failures file '{fi.FullName}'");

        // TODO(rkm 2023-07-06) Should always be true here
        //if (!opts.OnlyRules)
        //    _targetOptions = target ?? throw new Exception("A single Target must be supplied for database updates");

        _reportReader = new ReportReader(fi);

        if (string.IsNullOrWhiteSpace(opts.UnattendedOutputPath))
            throw new Exception("An output path must be specified for Failures that could not be resolved");

        _outputFile = _fileSystem.FileInfo.New(opts.UnattendedOutputPath);

        _ignorer = ignorer;
        _updater = updater;
    }

    /// <summary>
    /// Connects to the database and runs all failures through the rules base performing redactions as required
    /// </summary>
    /// <returns></returns>
    public int Run()
    {
        DiscoveredServer? server = null;
        if (_targetOptions != null)
            server = DatabaseTargetHelpers.GetDiscoveredServer(_targetOptions);

        var errors = new List<Exception>();

        var storeReport = new FailureStoreReport(_outputFile.Name, 100, _fileSystem);

        var sw = new Stopwatch();
        sw.Start();

        using (var storeReportDestination = new CsvDestination(new CsvReportDestinationOptions(), _outputFile))
        {
            RegexRule updateRule;

            storeReport.AddDestination(storeReportDestination);

            while (_reportReader.Next())
            {
                bool noUpdate;

                try
                {
                    noUpdate = _updater.OnLoad(server, _reportReader.Current, out updateRule);
                }
                catch (Exception e)
                {
                    errors.Add(e);
                    continue;
                }

                //is it novel for updater
                if (noUpdate)
                    //is it novel for ignorer
                    if (_ignorer.OnLoad(_reportReader.Current, out var ignoreRule))
                    {
                        //we can't process it unattended
                        storeReport.Add(_reportReader.Current);
                        Unresolved++;
                    }
                    else
                    {
                        if (!_ignoreRulesUsed.TryAdd(ignoreRule, 1))
                            _ignoreRulesUsed[ignoreRule]++;
                        Ignores++;
                    }
                else
                {
                    if (!_updateRulesUsed.TryAdd(updateRule, 1))
                        _updateRulesUsed[updateRule]++;
                    Updates++;
                }

                Total++;

                if (Total % 10000 == 0 || sw.ElapsedMilliseconds > 5000)
                {
                    Log($"Done {Total:N0} u={Updates:N0} i={Ignores:N0} o={Unresolved:N0} err={errors.Count:N0}", true);
                    sw.Restart();
                }
            }

            storeReport.CloseReport();
        }

        Log($"Ignore Rules Used:{Environment.NewLine}{string.Join(Environment.NewLine, _ignoreRulesUsed.OrderBy(k => k.Value).Select(k => $"{k.Key.IfPattern} - {k.Value:N0}"))}", false);
        Log($"Update Rules Used:{Environment.NewLine}{string.Join(Environment.NewLine, _updateRulesUsed.OrderBy(k => k.Value).Select(k => $"{k.Key.IfPattern} - {k.Value:N0}"))}", false);
        Log($"Errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => e.ToString()))}", false);
        Log($"Finished {Total:N0} updates={Updates:N0} ignored={Ignores:N0} out={Unresolved:N0} err={errors.Count:N0}", true);

        return 0;
    }

    private void Log(string msg, bool toConsole)
    {
        _log.Info(msg);
        if (toConsole)
            Console.WriteLine(msg);
    }
}

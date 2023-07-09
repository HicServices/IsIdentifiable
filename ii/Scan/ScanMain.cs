using CommandLine;
using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using IsIdentifiable.Options;
using IsIdentifiable.Reporting.Reports;
using IsIdentifiable.Scanners;
using NLog;
using System;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;

namespace ii.Scan;

internal static class ScanMain
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public static int Run(string[] args, IFileSystem fileSystem)
    {
        var allVerbTypes = (
            from t in Assembly.GetExecutingAssembly().GetTypes()
            where
                t.Namespace == typeof(ScanMain).Namespace &&
                t.IsClass &&
                t.Name.EndsWith("Verb")
            select t
        );

        // Skip the "scan" argument which was parsed first
        args = args[1..];

        return ParserHelpers
            .GetDefaultParser()
            .ParseArguments(args, allVerbTypes.ToArray())
            .MapResult(
                (DicomFileCliVerb o) => ScanDicomFiles(o, fileSystem),
                (CSVFileCliVerb o) => ScanCSVFiles(o, fileSystem),
                (RelationalDatabaseCliVerb o) => ScanRelationalDatabase(o, fileSystem),
                (MongoDBCliVerb o) => ScanMongoDB(o, fileSystem),
                _ => 1
            );
    }

    private static int ScanDicomFiles(DicomFileCliVerb cliOptions, IFileSystem fileSystem)
    {
        var allOptions = IiYamlOptions.LoadFrom(fileSystem.FileInfo.New(cliOptions.YamlConfigPath));
        var options = allOptions?.DicomFileScannerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(DicomFileScannerOptions)} key", nameof(cliOptions));

        using var runner = new DicomFileScanner(options, fileSystem);
        return ScanFiles(options, fileSystem, runner, cliOptions.FileOrDir);
    }

    private static int ScanCSVFiles(CSVFileCliVerb cliOptions, IFileSystem fileSystem)
    {
        var allOptions = IiYamlOptions.LoadFrom(fileSystem.FileInfo.New(cliOptions.YamlConfigPath));
        var options = allOptions?.CSVFileScannerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(RelationalDatabaseScannerOptions)} key", nameof(cliOptions));

        using var runner = new CsvFileScanner(options, fileSystem, cliOptions.StopAfter);
        return ScanFiles(options, fileSystem, runner, cliOptions.FileOrDir);
    }

    private static int ScanFiles(
        IFileScannerOptions options,
        IFileSystem fileSystem,
        IFileScanner fileScanner,
        string fileOrDir
    )
    {
        var fi = fileSystem.FileInfo.New(fileOrDir);
        var di = fileSystem.DirectoryInfo.New(fileOrDir);

        if (fi.Exists)
        {
            fileScanner.Scan(fi);
        }
        else if (di.Exists)
        {
            var unhandledErrors = ScanDirectory(options, fileScanner, di);
            _logger.Info($"Unhandled errors while processing: {unhandledErrors}");
        }
        else
        {
            throw new System.IO.FileNotFoundException($"Could not find file or directory '{fileOrDir}'");
        }

        return fileScanner.FailureCount;
    }

    private static int ScanDirectory(
        IFileScannerOptions options,
        IFileScanner fileScanner,
        IDirectoryInfo di
    )
    {
        _logger.Debug($"Scanning {di}");
        var unhandledErrors = 0;

        foreach (var fi in di.GetFiles(options.SearchPattern))
            try
            {
                fileScanner.Scan(fi);
            }
            catch (Exception e)
            {
                if (options.StopOnError)
                    throw;

                _logger.Error(e, $"Exception while validating {fi}");
                ++unhandledErrors;
            }

        foreach (var subDir in di.GetDirectories(options.SearchPattern))
            unhandledErrors += ScanDirectory(options, fileScanner, subDir);

        return unhandledErrors;
    }

    private static int ScanRelationalDatabase(RelationalDatabaseCliVerb cliOptions, IFileSystem fileSystem)
    {
        var allOptions = IiYamlOptions.LoadFrom(fileSystem.FileInfo.New(cliOptions.YamlConfigPath));
        var options = allOptions.RelationalDatabaseScannerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(RelationalDatabaseScannerOptions)} key", nameof(cliOptions));

        var databaseTargetOptions = DatabaseTargetOptionsExtensions.OptionsFrom(allOptions.DatabaseTargets, cliOptions, "from-cli");

        ImplementationManager.Load<MicrosoftSQLImplementation>();
        ImplementationManager.Load<MySqlImplementation>();
        ImplementationManager.Load<PostgreSqlImplementation>();
        ImplementationManager.Load<OracleImplementation>();

        using var runner = new RelationalDatabaseScanner(options, databaseTargetOptions, fileSystem, cliOptions.StopAfter);
        runner.Scan(cliOptions.TableName);

        return runner.FailureCount;
    }

    private static int ScanMongoDB(MongoDBCliVerb cliOptions, IFileSystem fileSystem)
    {
        var allOptions = IiYamlOptions.LoadFrom(fileSystem.FileInfo.New(cliOptions.YamlConfigPath));
        var options = allOptions.MongoDBScannerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(RelationalDatabaseScannerOptions)} key", nameof(cliOptions));

        string? query = null;
        if (!string.IsNullOrWhiteSpace(cliOptions.QueryFile))
            query = fileSystem.File.ReadAllText(cliOptions.QueryFile);

        TreeFailureReport? treeFailureReport = null;
        if (cliOptions.GenerateTreeReport)
        {
            var name = $"MongoDB-{options.DatabaseName}-{options.CollectionName}-";
            treeFailureReport = new TreeFailureReport(name, fileSystem);
        }

        using var runner = new MongoDBScanner(options, treeFailureReport, fileSystem);
        runner.Scan(query);
        return runner.FailureCount;
    }
}

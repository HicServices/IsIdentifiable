using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using System;
using System.IO.Abstractions;

namespace ii.Redact;

internal static class RedactMain
{
    public static int Run(string[] args, IFileSystem fileSystem)
    {
        var cliOptions = ParserHelpers
           .GetDefaultParser()
           .ParseArguments<RedactCliVerb>(args)
           .Value;

        if (cliOptions == null)
            return 1;

        var allOptions = IiYamlOptions.LoadFrom(fileSystem.FileInfo.New(cliOptions.YamlConfigPath));
        var _ = allOptions.RedacterOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(RedacterOptions)} key", nameof(args));

        var databaseTargetOptions = DatabaseTargetOptionsExtensions.OptionsFrom(allOptions.DatabaseTargets, cliOptions, "from-cli");

        ImplementationManager.Load<MicrosoftSQLImplementation>();
        ImplementationManager.Load<MySqlImplementation>();
        ImplementationManager.Load<PostgreSqlImplementation>();
        ImplementationManager.Load<OracleImplementation>();

        var updater = new RowUpdater(fileSystem, fileSystem.FileInfo.New(cliOptions.ReportList))
        {
            RulesOnly = true,
            RulesFactory = new MatchProblemValuesPatternFactory()
        };

        var ignorer = new IgnoreRuleGenerator(fileSystem, fileSystem.FileInfo.New(cliOptions.IgnoreList));

        var failuresFileInfo = fileSystem.FileInfo.New(cliOptions.FailuresCsv);
        IFileInfo outputFileInfo;
        if (!string.IsNullOrWhiteSpace(cliOptions.OutputPath))
            outputFileInfo = fileSystem.FileInfo.New(cliOptions.OutputPath);
        else
            outputFileInfo = fileSystem.FileInfo.New(cliOptions.FailuresCsv.Replace(".csv", ".remaining.csv"));

        var scanner = new UnattendedReviewer(databaseTargetOptions, ignorer, updater, failuresFileInfo, outputFileInfo);
        return scanner.Run();
    }
}

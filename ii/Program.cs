using CommandLine;
using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using FellowOakDicom;
using IsIdentifiable.Helpers;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using IsIdentifiable.Rules;
using IsIdentifiable.Rules.Storage;
using IsIdentifiable.Runners;
using Microsoft.Extensions.FileSystemGlobbing;
using NLog;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using YamlDotNet.Serialization;

namespace ii;

public static class Program
{
    const string SettingsFile = "ii-settings.yaml";
    static GlobalOptions? GlobalOptions;


    public static string? CutSettingsFileArgs(string[] args, out string[] newArgs)
    {
        var idx = Array.IndexOf(args, "-y");

        // do we have 2 elements "-y" then "somepath"?
        if (idx != -1 && idx < args.Length - 1)
        {
            var settingsFileLocation = args[idx + 1];

            // remove -y myfile
            newArgs = args.Take(idx).ToArray();

            newArgs = args.Skip(idx + 2).Aggregate(newArgs, (current, after) => current.Append(after).ToArray());

            return settingsFileLocation;
        }

        // no change
        newArgs = args;
        return null;
    }

    public static int Main(string[] args)
    {

        var explicitLocation = CutSettingsFileArgs(args, out var newArgs);
        args = newArgs;

        var settingsFileLocation = SettingsFile;

        var fileSystem = new FileSystem();

        // if user specified -y on command line
        if (!string.IsNullOrWhiteSpace(explicitLocation))
        {
            //verify the file they told us about is real
            if (!fileSystem.File.Exists(explicitLocation))
            {
                Console.Error.WriteLine($"Could not find file: {explicitLocation}");
                return 1;
            }

            // if it is then use it instead of ii-settings.yaml
            settingsFileLocation = explicitLocation;
        }

        // load GlobalOptions
        if (fileSystem.File.Exists(settingsFileLocation))
        {
            try
            {
                GlobalOptions = Deserialize(settingsFileLocation, fileSystem);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not deserialize {SettingsFile}:{ex}");
                return 1;
            }
        }

        // Disable fo-dicom's DICOM validation globally from here
        new DicomSetupBuilder().SkipValidation();

        var defaults = Parser.Default.Settings;
        using var parser = new Parser(settings =>
        {
            settings.CaseInsensitiveEnumValues = true;
            settings.CaseSensitive = false;
            settings.EnableDashDash = defaults.EnableDashDash;
            settings.HelpWriter = defaults.HelpWriter;
            settings.IgnoreUnknownArguments = false;
            settings.MaximumDisplayWidth = defaults.MaximumDisplayWidth;
            settings.ParsingCulture = defaults.ParsingCulture;
        });

        var res = parser.ParseArguments<IsIdentifiableRelationalDatabaseOptions,
                IsIdentifiableDicomFileOptions,
                IsIdentifiableMongoOptions,
                IsIdentifiableFileGlobOptions,
                IsIdentifiableReviewerOptions>(args)
            .MapResult(
                (IsIdentifiableRelationalDatabaseOptions o) => Run(o, fileSystem),
                (IsIdentifiableDicomFileOptions o) => Run(o, fileSystem),
                (IsIdentifiableMongoOptions o) => Run(o, fileSystem),
                (IsIdentifiableFileGlobOptions o) => Run(o, fileSystem),
                (IsIdentifiableReviewerOptions o) => Run(o, fileSystem),

                // return exit code 0 for user requests for help
                errors => args.Any(a => a.Equals("--help", StringComparison.InvariantCultureIgnoreCase)) ? 0 : 1);

        return res;
    }

    public static GlobalOptions? Deserialize(string settingsFileLocation, IFileSystem fileSystem)
    {
        var deserializer = new DeserializerBuilder()
                        .IgnoreUnmatchedProperties()
                        .Build();

        return deserializer.Deserialize<GlobalOptions>(fileSystem.File.ReadAllText(settingsFileLocation));
    }

    private static int Run(IsIdentifiableReviewerOptions opts, IFileSystem fileSystem)
    {
        Inherit(opts);

        var logger = LogManager.GetCurrentClassLogger();

        if (!opts.OnlyRules)
        {
            ImplementationManager.Load<MicrosoftSQLImplementation>();
            ImplementationManager.Load<MySqlImplementation>();
            ImplementationManager.Load<PostgreSqlImplementation>();
            ImplementationManager.Load<OracleImplementation>();

            logger.Info("Running Connection Tests");

            List<Target> targets;

            try
            {

                IsIdentifiableBaseOptions.LoadTargets(opts, logger, fileSystem, out targets);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error Validating Targets");
                return 1;
            }

            foreach (var target in targets)
            {
                var discoveredServer = target.Discover();
                if (discoveredServer.Exists())
                {
                    logger.Info($"Successfully connected to {target.Name}");
                }
                else
                {
                    logger.Error($"Failed to connect to {target.Name}");
                    return 1;
                }
            }


            return 0;
        }

        var ignoreActionRuleStore = new YamlRegexRuleStore(
            new RegexRuleGenerator(RegexRuleFactory.IfFPatternForWholeProblemValue, RuleAction.Ignore),
            fileSystem.FileInfo.New(opts.IgnoreList),
            new DateTimeProvider()
        );

        var reportActionRuleStore = new YamlRegexRuleStore(
            new RegexRuleGenerator(RegexRuleFactory.IfPatternForRuleMatchingProblemValues, RuleAction.Report),
            fileSystem.FileInfo.New(opts.Reportlist),
            new DateTimeProvider()
        );

        if (!string.IsNullOrWhiteSpace(opts.UnattendedOutputPath))
        {
            //run unattended
            if (opts.targets.Count != 1)
                throw new Exception("Unattended requires a single entry in Targets");

            var unattended = new DatabaseRedactor(_reviewerOptions, _targets.Single(), _ignoreActionRuleStore, _reportActionRuleStore, _fileSystem);
            unattended.Run();

            return 0;
        }

        var reviewer = new ReviewerRunner(GlobalOptions?.IsIdentifiableOptions, opts, fileSystem);
        return reviewer.Run();
    }


    private static int Run(IsIdentifiableDicomFileOptions opts, IFileSystem fileSystem)
    {
        var result = Inherit(opts, fileSystem);

        if (result != 0)
            return result;

        using var runner = new DicomFileRunner(opts, fileSystem)
        {
            ThrowOnError = false,
            LogProgressEvery = 1000
        };
        return runner.Run();
    }

    private static int Run(IsIdentifiableRelationalDatabaseOptions opts, IFileSystem fileSystem)
    {
        ImplementationManager.Load<MicrosoftSQLImplementation>();
        ImplementationManager.Load<MySqlImplementation>();
        ImplementationManager.Load<PostgreSqlImplementation>();
        ImplementationManager.Load<OracleImplementation>();

        var result = Inherit(opts, fileSystem);

        if (result != 0)
            return result;

        using var runner = new DatabaseRunner(opts, fileSystem)
        {
            LogProgressEvery = 1000
        };
        return runner.Run();
    }

    private static int Run(IsIdentifiableFileGlobOptions opts, IFileSystem fileSystem)
    {
        var result = Inherit(opts, fileSystem);

        if (result != 0)
            return result;

        if (opts.File == null)
        {
            throw new Exception("You must specify a File or Directory indicate which files to work on");
        }

        // if user has specified the full path of a file to -f
        if (fileSystem.File.Exists(opts.File.FullName))
        {
            // Run on the file
            opts.File = fileSystem.FileInfo.New(opts.File.FullName);

            using var runner = new FileRunner(opts, fileSystem)
            {
                LogProgressEvery = 1000
            };

            return runner.Run();
        }

        // user has specified a directory as -f
        if (fileSystem.Directory.Exists(opts.File.FullName))
        {
            Matcher matcher = new();
            matcher.AddInclude(opts.Glob);
            result = 0;

            foreach (var match in matcher.GetResultsInFullPath(opts.File.FullName))
            {
                // set file to operate on to the current file
                opts.File = fileSystem.FileInfo.New(match);

                using var runner = new FileRunner(opts, fileSystem)
                {
                    LogProgressEvery = 1000
                };
                var thisResult = runner.Run();

                // if we don't yet have any errors
                if (result == 0)
                {
                    // take any errors (or 0 if it went fine)
                    result = thisResult;
                }
            }

            return result;
        }
        else
        {
            throw new System.IO.DirectoryNotFoundException($"Could not find a file or directory called '{opts.File}'");
        }
    }
    private static int Run(IsIdentifiableMongoOptions opts, IFileSystem fileSystem)
    {
        Inherit(opts, fileSystem);

        using var runner = new MongoRunner(opts, fileSystem)
        {
            LogProgressEvery = 1000
        };
        return runner.Run();
    }
    private static void Inherit(IsIdentifiableReviewerOptions opts)
    {
        if (GlobalOptions?.IsIdentifiableReviewerOptions != null)
        {
            opts.InheritValuesFrom(GlobalOptions.IsIdentifiableReviewerOptions);
        }
    }
    private static int Inherit(IsIdentifiableBaseOptions opts, IFileSystem fileSystem)
    {
        if (GlobalOptions?.IsIdentifiableOptions != null)
        {
            opts.InheritValuesFrom(GlobalOptions.IsIdentifiableOptions);
        }

        return opts.UpdateConnectionStringsToUseTargets(out _, fileSystem);
    }

}

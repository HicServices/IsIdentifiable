using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using NLog;
using System;
using System.IO.Abstractions;
using Terminal.Gui;
using YamlDotNet.Serialization;

namespace ii.Review;

internal static class ReviewMain
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public static int Run(string[] args, IFileSystem fileSystem)
    {
        var cliOptions = ParserHelpers
            .GetDefaultParser()
            .ParseArguments<ReviewCliVerb>(args)
            .Value;

        // TODO(rkm 2023-07-05) Check errors are appropriately printed
        if (cliOptions == null)
            return 1;

        var allOptions = IsIdentifiableOptions.Load<IiOptions>(fileSystem.FileInfo.New(cliOptions.YamlConfigPath));
        var options = allOptions.ReviewerOptions ??
            throw new ArgumentException($"Yaml file did not contain a {typeof(ReviewerOptions)} key", nameof(cliOptions));

        Application.UseSystemConsole = cliOptions.UseSystemConsole;

        if (fileSystem.File.Exists(options.ThemeFile))
        {
            TerminalGuiTheme? theme;
            try
            {
                var themeText = fileSystem.File.ReadAllText(options.ThemeFile);
                theme = new Deserializer().Deserialize<TerminalGuiTheme>(themeText);
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Could not deserialize theme", ex.Message);
                return 1;
            }

            Colors.Base = theme.Base.GetScheme();
            Colors.Dialog = theme.Dialog.GetScheme();
            Colors.Error = theme.Error.GetScheme();
            Colors.Menu = theme.Menu.GetScheme();
            Colors.TopLevel = theme.TopLevel.GetScheme();
        }

        var updater = new RowUpdater(fileSystem, fileSystem.FileInfo.New(cliOptions.ReportList))
        {
            RulesOnly = true,
            RulesFactory = new MatchProblemValuesPatternFactory()
        };

        var ignorer = new IgnoreRuleGenerator(fileSystem, fileSystem.FileInfo.New(cliOptions.IgnoreList));

        Console.WriteLine("Press any key to launch GUI");
        Console.ReadKey();

        int rc = 0;

        try
        {
            RunApplication(ignorer, updater, fileSystem);
        }
        catch (Exception e)
        {
            _logger.Error(e, $"Application crashed");
            rc = 1;

            var tries = 5;
            while (Application.Top != null && tries-- > 0)
                try
                {
                    Application.RequestStop();
                }
                catch (Exception)
                {
                    Console.Error.WriteLine($"Failed to terminate GUI on crash (tries={tries})");
                }
        }
        finally
        {
            Application.Shutdown();
        }

        return rc;
    }

    private static void RunApplication(
        IgnoreRuleGenerator ignorer,
        RowUpdater updater,
        IFileSystem fileSystem
    )
    {
        Application.Init();

        var top = Application.Top;

        // TODO(rkm 2023-07-06) Fix null options
        using var mainWindow = new MainWindow(
            null,
            null,
            ignorer,
            updater,
            fileSystem
        );

        // Creates the top-level window to show
        using var win = new Window("IsIdentifiable Reviewer")
        {
            X = 0,
            Y = 1, // Leave one row for the toplevel menu

            // By using Dim.Fill(), it will automatically resize without manual intervention
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        top.Add(win);
        top.Add(mainWindow.Menu);
        win.Add(mainWindow.Body);
        Application.Run(top);
    }
}

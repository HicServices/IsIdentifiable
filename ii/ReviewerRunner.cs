using IsIdentifiable.Options;
using IsIdentifiable.Rules.Storage;
using NLog;
using System;
using System.IO.Abstractions;
using Terminal.Gui;
using YamlDotNet.Serialization;

namespace ii;

public class ReviewerRunner
{
    private readonly IsIdentifiableBaseOptions? _analyserOpts;
    private readonly IsIdentifiableReviewerOptions _reviewerOptions;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    private readonly YamlRegexRuleStore _ignoreActionRuleStore;
    private readonly YamlRegexRuleStore _reportActionRuleStore;

    public ReviewerRunner(IsIdentifiableBaseOptions? analyserOpts, IsIdentifiableReviewerOptions reviewerOptions, IFileSystem fileSystem)
    {
        _analyserOpts = analyserOpts;
        _reviewerOptions = reviewerOptions;
        _fileSystem = fileSystem;

        _logger = LogManager.GetCurrentClassLogger();

    }

    /// <summary>
    /// Runs the reviewer gui or redaction mode
    /// </summary>
    /// <returns></returns>
    public int Run()
    {


        try
        {
            Console.WriteLine("Press any key to launch GUI");
            Console.ReadKey();

            if (_reviewerOptions.UseSystemConsole)
                Application.UseSystemConsole = true;

            //run interactive
            Application.Init();

            if (_reviewerOptions.Theme != null && _fileSystem.File.Exists(_reviewerOptions.Theme))
            {
                try
                {
                    var des = new Deserializer();
                    var theme = des.Deserialize<TerminalGuiTheme>(_fileSystem.File.ReadAllText(_reviewerOptions.Theme));

                    Colors.Base = theme.Base.GetScheme();
                    Colors.Dialog = theme.Dialog.GetScheme();
                    Colors.Error = theme.Error.GetScheme();
                    Colors.Menu = theme.Menu.GetScheme();
                    Colors.TopLevel = theme.TopLevel.GetScheme();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Could not deserialize theme", ex.Message);
                }
            }

            var top = Application.Top;

            using var mainWindow = new MainWindow(_analyserOpts ?? new IsIdentifiableBaseOptions(), _reviewerOptions, _ignoreActionRuleStore, _reportActionRuleStore, _fileSystem);


            // Creates the top-level window to show
            var win = new Window("IsIdentifiable Reviewer")
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

            return 0;

        }
        catch (Exception e)
        {
            _logger.Error(e, $"Application crashed");

            var tries = 5;
            while (Application.Top != null && tries-- > 0)
                try
                {
                    Application.RequestStop();
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to terminate GUI on crash");
                }

            return 99;
        }
        finally
        {
            Application.Shutdown();
        }
    }
}

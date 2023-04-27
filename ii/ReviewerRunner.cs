using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using NLog;
using Terminal.Gui;
using YamlDotNet.Serialization;

namespace ii;

public class ReviewerRunner
{
    private readonly IsIdentifiableBaseOptions? _analyserOpts;
    private readonly IsIdentifiableReviewerOptions _reviewerOptions;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly List<Target> _targets;
    private readonly RowUpdater _rowUpdater;
    private readonly IgnoreRuleGenerator _ignoreRuleGenerator;

    public ReviewerRunner(IsIdentifiableBaseOptions? analyserOpts, IsIdentifiableReviewerOptions reviewerOptions, IFileSystem fileSystem)
    {
        _analyserOpts = analyserOpts;
        _reviewerOptions = reviewerOptions;
        _fileSystem = fileSystem;

        _logger = LogManager.GetCurrentClassLogger();
        IsIdentifiableBaseOptions.LoadTargets(_reviewerOptions, _logger, _fileSystem, out _targets);

        if (_reviewerOptions.OnlyRules)
        {
            _logger.Info("Skipping Connection Tests");
        }
        else
        {
            _logger.Info("Running Connection Tests");

            ImplementationManager.Load<MicrosoftSQLImplementation>();
            ImplementationManager.Load<MySqlImplementation>();
            ImplementationManager.Load<PostgreSqlImplementation>();
            ImplementationManager.Load<OracleImplementation>();

            try
            {
                foreach (var t in _targets)
                    Console.WriteLine(t.Discover().Exists()
                        ? $"Successfully connected to {t.Name}"
                        : $"Failed to connect to {t.Name}");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error Validating Targets");
                throw;
            }
        }

        //for updater try to match the ProblemValue words
        _rowUpdater = new RowUpdater(_fileSystem, _fileSystem.FileInfo.New(_reviewerOptions.Reportlist))
        {
            RulesOnly = _reviewerOptions.OnlyRules,
            RulesFactory = new MatchProblemValuesPatternFactory()
        };

        //for Ignorer match the whole string
        _ignoreRuleGenerator = new IgnoreRuleGenerator(_fileSystem, _fileSystem.FileInfo.New(_reviewerOptions.IgnoreList));
    }

    /// <summary>
    /// Runs the reviewer gui or redaction mode
    /// </summary>
    /// <returns></returns>
    public int Run()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_reviewerOptions.UnattendedOutputPath))
            {
                //run unattended
                if (_targets.Count != 1)
                    throw new Exception("Unattended requires a single entry in Targets");

                var unattended = new UnattendedReviewer(_reviewerOptions, _targets.Single(), _ignoreRuleGenerator, _rowUpdater, _fileSystem);
                return unattended.Run();
            }
            else
            {
                Console.WriteLine("Press any key to launch GUI");
                Console.ReadKey();


                if (_reviewerOptions.UseSystemConsole)
                {
                    Application.UseSystemConsole = true;
                }


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

                using var mainWindow = new MainWindow(_analyserOpts ?? new IsIdentifiableBaseOptions(), _reviewerOptions, _ignoreRuleGenerator, _rowUpdater, _fileSystem);


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
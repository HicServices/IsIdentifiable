using ii.Review.Views;
using ii.Review.Views.Manager;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using IsIdentifiable.Rules;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace ii.Review;

internal class MainWindow : IRulePatternFactory, IDisposable
{
    /// <summary>
    /// The report CSV file that is currently open
    /// </summary>
    public ReportReader? CurrentReport { get; set; }

    /// <summary>
    /// Generates suggested ignore rules for false positives
    /// </summary>
    public IgnoreRuleGenerator Ignorer { get; }

    /// <summary>
    /// Updates the database to perform redactions (when not operating in Rules Only mode)
    /// </summary>
    public RowUpdater Updater { get; }

    private readonly FailureView _valuePane;
    private readonly Label _info;
    private readonly SpinnerView _spinner;
    private readonly TextField _gotoTextField;
    private readonly IRulePatternFactory _origUpdaterRulesFactory;
    private readonly IRulePatternFactory _origIgnorerRulesFactory;
    private readonly Label _ignoreRuleLabel;
    private readonly Label _updateRuleLabel;
    private readonly Label _currentReportLabel;

    /// <summary>
    /// Record of new rules added (e.g. Ignore with pattern X) along with the index of the failure.  This allows undoing user decisions
    /// </summary>
    readonly Stack<MainWindowHistory> History = new();

    readonly ColorScheme _greyOnBlack = new()
    {
        Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
        HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
        Disabled = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
        Focus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
    };
    private readonly MenuItem miCustomPatterns;
    private readonly RulesView rulesView;
    private readonly AllRulesManagerView rulesManager;
    private readonly IFileSystem _fileSystem;

    public MenuBar Menu { get; private set; }

    public View Body { get; private set; }

    Task? taskToLoadNext;
    private const string PatternHelp = @"x - clears currently typed pattern
F - creates a regex pattern that matches the full input value
G - creates a regex pattern that matches only the failing part(s)
\d - replaces all digits with regex wildcards
\c - replaces all characters with regex wildcards
\d\c - replaces all digits and characters with regex wildcards";

    private readonly View viewMain;

    public MainWindow(ResourceScannerBaseOptions? analyserOpts, IgnoreRuleGenerator ignorer, RowUpdater updater, IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;

        Ignorer = ignorer;
        Updater = updater;
        _origUpdaterRulesFactory = updater.RulesFactory;
        _origIgnorerRulesFactory = ignorer.RulesFactory;

        Menu = new MenuBar(new MenuBarItem[] {
            new("_File (F9)", new MenuItem [] {
                new("_Open Report",null, OpenReport),
                new("_Quit", null, static () => Application.RequestStop())
            }),
            new("_Options", new MenuItem [] {
                miCustomPatterns = new MenuItem("_Custom Patterns",null,ToggleCustomPatterns){CheckType = MenuItemCheckStyle.Checked,Checked = false}
            })
        });


        viewMain = new View() { Width = Dim.Fill(), Height = Dim.Fill() };
        rulesView = new RulesView();
        rulesManager = new AllRulesManagerView(analyserOpts, fileSystem);

        _info = new Label("Info")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill() - 1,
            Height = 1,
            ColorScheme = _greyOnBlack
        };

        _valuePane = new FailureView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 10,
        };

        var frame = new FrameView("Options")
        {
            X = 0,
            Y = 12,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var ignoreButton = new Button("Ignore")
        {
            X = 0
        };
        ignoreButton.Clicked += Ignore;
        frame.Add(ignoreButton);

        var updateButton = new Button("Update")
        {
            X = 11
        };
        updateButton.Clicked += Update;
        frame.Add(updateButton);

        _gotoTextField = new TextField("1")
        {
            X = 28,
            Width = 5
        };
        _gotoTextField.TextChanged += (s) => GoTo();
        frame.Add(_gotoTextField);
        frame.Add(new Label(23, 0, "GoTo:"));

        var prevButton = new Button("Prev")
        {
            X = 0,
            Y = 1
        };
        prevButton.Clicked += () => GoToRelative(-1);
        frame.Add(prevButton);

        var nextButton = new Button("Next")
        {
            X = 11,
            Y = 1
        };
        nextButton.Clicked += () => GoToRelative(1);
        frame.Add(nextButton);

        var undoButton = new Button("unDo")
        {
            X = 11,
            Y = 2
        };
        undoButton.Clicked += Undo;
        frame.Add(undoButton);

        frame.Add(new Label(0, 4, "Default Patterns"));

        _ignoreRuleLabel = new Label() { X = 0, Y = 5, Text = "Ignore:", Width = 30, Height = 1 }; ;
        _updateRuleLabel = new Label() { X = 0, Y = 6, Text = "Update:", Width = 30, Height = 1 }; ;
        _currentReportLabel = new Label() { X = 0, Y = 8, Text = "Report:", Width = 30, Height = 1 };

        frame.Add(_ignoreRuleLabel);
        frame.Add(_updateRuleLabel);
        frame.Add(_currentReportLabel);

        // always run rules only mode for the manual gui
        Updater.RulesOnly = true;

        viewMain.Add(_info);

        _spinner = new SpinnerView
        {
            X = Pos.Right(_info)
        };
        viewMain.Add(_spinner);
        _spinner.Visible = false;

        viewMain.Add(_valuePane);
        viewMain.Add(frame);

        // TODO(rkm 2023-07-09)
        //if (!string.IsNullOrWhiteSpace(opts.FailuresCsv))
        //    OpenReport(opts.FailuresCsv, (e) => throw e);

        var tabView = new TabView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        tabView.Style.ShowBorder = false;
        tabView.Style.ShowTopLine = false;
        tabView.Style.TabsOnBottom = true;
        tabView.ApplyStyleChanges();

        tabView.AddTab(new TabView.Tab("Sequential", viewMain), true);
        tabView.AddTab(new TabView.Tab("Tree View", rulesView), false);
        tabView.AddTab(new TabView.Tab("Rules Manager", rulesManager), false);

        tabView.SelectedTabChanged += TabView_SelectedTabChanged;

        Body = tabView;
    }

    private void TabView_SelectedTabChanged(object? sender, TabView.TabChangedEventArgs e)
    {
        // sync the rules up in case people are adding new ones using the other UIs
        rulesManager.RebuildTree();
    }

    private void ToggleCustomPatterns()
    {
        miCustomPatterns.Checked = !miCustomPatterns.Checked;

        Updater.RulesFactory = miCustomPatterns.Checked ? this : _origUpdaterRulesFactory;
        Ignorer.RulesFactory = miCustomPatterns.Checked ? this : _origIgnorerRulesFactory;
    }

    private void Undo()
    {
        if (History.Count == 0)
        {
            Helpers.ShowMessage("History Empty", "Cannot undo, history is empty");
            return;
        }

        var popped = History.Pop();

        //undo file history
        popped.OutputBase.Undo();

        //wind back UI
        GoTo(popped.Index);
    }

    private void GoToRelative(int offset)
    {
        if (CurrentReport == null)
            return;

        GoTo(CurrentReport.CurrentIndex + offset);
    }

    private void GoTo()
    {
        if (CurrentReport == null)
            return;

        try
        {
            var val = _gotoTextField.Text?.ToString();
            if (val != null)
            {
                GoTo(int.Parse(val));
            }

        }
        catch (FormatException)
        {
            //use typed in 'hello there! or some such'
        }
    }

    private void GoTo(int page)
    {
        if (CurrentReport == null)
            return;
        try
        {
            CurrentReport.GoTo(page);
            _info.Text = CurrentReport.DescribeProgress();
            SetupToShow(CurrentReport.CurrentFailure);
        }
        catch (Exception e)
        {
            Helpers.ShowException("Failed to GoTo", e);
        }

    }

    private void SetupToShow(Failure? f)
    {
        _valuePane.CurrentFailure = f;

        if (f != null)
        {
            _ignoreRuleLabel.Text = $"Ignore:{_origIgnorerRulesFactory.GetPattern(Ignorer, f)}";
            _updateRuleLabel.Text = $"Update:{_origUpdaterRulesFactory.GetPattern(Ignorer, f)}";
        }
        else
        {
            _ignoreRuleLabel.Text = "Ignore:";
            _updateRuleLabel.Text = "Update:";
        }
    }


    private void BeginNext()
    {
        taskToLoadNext = Task.Run(Next);
    }

    private void Next()
    {
        if (_valuePane.CurrentFailure == null || CurrentReport == null)
            return;

        _spinner.Visible = true;

        var skipped = 0;
        var updated = 0;
        try
        {
            while (CurrentReport.Next())
            {
                var next = CurrentReport.CurrentFailure;

                //prefer rules that say we should update the database with redacted over rules that say we should ignore the problem
                if (!Updater.OnLoad(null, next, out _))
                    updated++;
                else if (!Ignorer.OnLoad(next, out _))
                    skipped++;
                else
                {
                    SetupToShow(next);

                    break;
                }
            }
        }
        catch (Exception e)
        {
            Helpers.ShowException("Error moving to next record", e);
        }
        finally
        {
            _spinner.Visible = false;
        }

        StringBuilder info = new();

        info.Append(CurrentReport.DescribeProgress());

        if (skipped > 0)
            info.Append($" Skipped {skipped}");
        if (updated > 0)
            info.Append($" Auto Updated {updated}");

        if (CurrentReport.Exhausted)
        {
            info.Append(" (End of Failures)");
        }

        _info.Text = info.ToString();
    }

    private void Ignore()
    {
        if (_valuePane.CurrentFailure == null || CurrentReport == null)
            return;

        if (taskToLoadNext != null && !taskToLoadNext.IsCompleted)
        {
            MessageBox.Query("StillLoading", "Load next is still running");
            return;
        }

        try
        {
            Ignorer.Add(_valuePane.CurrentFailure);
            History.Push(new MainWindowHistory(CurrentReport.CurrentIndex, Ignorer));
        }
        catch (OperationCanceledException)
        {
            //if user cancels adding the ignore then stay on the same record
            return;
        }
        BeginNext();
    }
    private void Update()
    {
        if (_valuePane.CurrentFailure == null || CurrentReport == null)
            return;

        if (taskToLoadNext != null && !taskToLoadNext.IsCompleted)
        {
            MessageBox.Query("StillLoading", "Load next is still running");
            return;
        }

        try
        {
            // TODO(rkm 2021-04-09) Server always passed as null here, but Update seems to require it?
            Updater.Update(null!, _valuePane.CurrentFailure, null! /*create one yourself*/);

            History.Push(new MainWindowHistory(CurrentReport.CurrentIndex, Updater));
        }
        catch (OperationCanceledException)
        {
            //if user cancels updating then stay on the same record
            return;
        }
        catch (Exception e)
        {
            Helpers.ShowException("Failed to update database", e);
            return;
        }

        BeginNext();
    }

    private void OpenReport()
    {
        using var ofd = new OpenDialog("Load CSV Report", "Enter file path to load")
        {
            AllowedFileTypes = new[] { ".csv" },
            CanChooseDirectories = false,
            AllowsMultipleSelection = false
        };

        Application.Run(ofd);

        var f = ofd.FilePaths?.SingleOrDefault();

        Exception? ex = null;
        OpenReport(f, (e) => ex = e);

        if (ex != null)
        {
            Helpers.ShowException("Failed to Load", ex);
        }
    }

    private void OpenReport(string? path, Action<Exception> exceptionHandler)
    {
        if (path == null)
            return;

        var cts = new CancellationTokenSource();

        using var btn = new Button("Cancel");
        void cancelFunc() { cts.Cancel(); }
        void closeFunc() { Application.RequestStop(); }
        btn.Clicked += cancelFunc;

        using var dlg = new Dialog("Opening", Constants.DlgWidth, 5, btn);
        var rows = new Label($"Loaded: 0 rows")
        {
            Width = Dim.Fill()
        };
        dlg.Add(rows);

        var done = false;

        Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), (s) =>
        {
            dlg.SetNeedsDisplay();
            return !done;
        });

        Task.Run(() =>
        {
            try
            {
                CurrentReport = new ReportReader(_fileSystem.FileInfo.New(path), (s) =>
                    rows.Text = $"Loaded: {s:N0} rows", cts.Token);
                SetupToShow(CurrentReport.Failures.FirstOrDefault());
                BeginNext();

                rulesView.LoadReport(CurrentReport, Ignorer, Updater, _origIgnorerRulesFactory);
            }
            catch (Exception e)
            {
                exceptionHandler(e);
                rows.Text = "Error";
            }

        }
        ).ContinueWith((t) =>
        {

            btn.Clicked -= cancelFunc;
            btn.Text = "Done";
            btn.Clicked += closeFunc;
            done = true;

            cts.Dispose();
        });

        _currentReportLabel.Text = $"Report:{_fileSystem.Path.GetFileName(path)}";
        _currentReportLabel.SetNeedsDisplay();

        Application.Run(dlg);
    }

    private static bool GetText(string title, string message, string initialValue, out string chosen,
        Dictionary<string, string> buttons)
    {
        var optionChosen = false;

        using var dlg = new Dialog(title, Math.Min(Console.WindowWidth, Constants.DlgWidth), Constants.DlgHeight);

        var line = Constants.DlgHeight - Constants.DlgBoundary * 2 - 2;

        if (!string.IsNullOrWhiteSpace(message))
        {
            var width = Math.Min(Console.WindowWidth, Constants.DlgWidth) - Constants.DlgBoundary * 2;

            var msg = Helpers.Wrap(message, width - 1).TrimEnd();

            var text = new Label(0, 0, msg)
            {
                Height = line - 1,
                Width = width
            };

            //if it is too long a message
            var newlines = msg.Count(c => c == '\n');
            if (newlines > line - 1)
            {
                var view = new ScrollView(new Rect(0, 0, width, line - 1))
                {
                    ContentSize = new Size(width, newlines + 1),
                    ContentOffset = new Point(0, 0),
                    ShowVerticalScrollIndicator = true,
                    ShowHorizontalScrollIndicator = false
                };
                view.Add(text);
                dlg.Add(view);
            }
            else
                dlg.Add(text);
        }

        var txt = new TextField(0, line++, Constants.DlgWidth - 4, initialValue ?? "");
        dlg.Add(txt);

        var btn = new Button(0, line, "Ok")
        {
            IsDefault = true
        };
        btn.Clicked += () =>
        {
            if (!string.IsNullOrWhiteSpace(txt.Text?.ToString()))
            {
                dlg.Running = false;
                optionChosen = true;
            }
        };
        dlg.Add(btn);


        var x = 10;
        if (buttons != null)
            foreach (var kvp in buttons)
            {
                var button = new Button(x, line, kvp.Key);
                button.Clicked += () => { txt.Text = kvp.Value; };
                dlg.Add(button);
                x += kvp.Key.Length + 5;
            }


        // add help button
        var btnHelp = new Button(0, line, "?")
        {
            X = x
        };
        x += 6;

        btnHelp.Clicked += () =>
        {
            MessageBox.Query("Pattern Help", PatternHelp, "Ok");
        };
        dlg.Add(btnHelp);

        // add cancel button
        var btnCancel = new Button(0, line, "Cancel")
        {
            X = x
        };
        //x += 11;
        btnCancel.Clicked += () =>
        {
            optionChosen = false;
            Application.RequestStop();
        };
        dlg.Add(btnCancel);

        dlg.FocusFirst();


        Application.Run(dlg);

        chosen = txt.Text?.ToString() ?? "";
        return optionChosen;
    }

    public string GetPattern(object sender, Failure failure)
    {
        var defaultFactory = ReferenceEquals(sender, Updater) ? _origUpdaterRulesFactory : _origIgnorerRulesFactory;

        var recommendedPattern = defaultFactory.GetPattern(sender, failure);

        var buttons = new Dictionary<string, string>
        {
            { "x", "" },
            { "F", _origIgnorerRulesFactory.GetPattern(sender, failure) },
            { "G", _origUpdaterRulesFactory.GetPattern(sender, failure) },

            { @"\d", new SymbolsRulesFactory { Mode = SymbolsRuleFactoryMode.DigitsOnly }.GetPattern(sender, failure) },
            { @"\c", new SymbolsRulesFactory { Mode = SymbolsRuleFactoryMode.CharactersOnly }.GetPattern(sender, failure) },
            { @"\d\c", new SymbolsRulesFactory().GetPattern(sender, failure) }
        };

        if (GetText("Pattern", "Enter pattern to match failure", recommendedPattern, out var chosen, buttons))
        {
            Regex regex;

            try
            {
                regex = new Regex(chosen);
            }
            catch (Exception)
            {
                Helpers.ShowMessage("Invalid Regex", "Pattern was not a valid Regex");
                //try again!
                return GetPattern(sender, failure);
            }

            if (!regex.IsMatch(failure.ProblemValue))
            {
                Helpers.GetChoice("Pattern Match Failure", "The provided pattern did not match the original ProblemValue.  Try a different pattern?", out var retry, new[] { "Yes", "No" });

                if (retry == "Yes")
                    return GetPattern(sender, failure);
            }

            if (string.IsNullOrWhiteSpace(chosen))
                throw new Exception("User entered blank Regex pattern");

            return chosen;
        }


        throw new OperationCanceledException("User chose not to enter a pattern");
    }

    public void Dispose()
    {
        _valuePane.Dispose();
        _info.Dispose();
        _spinner.Dispose();
        _gotoTextField.Dispose();
        _ignoreRuleLabel.Dispose();
        _updateRuleLabel.Dispose();
        _currentReportLabel.Dispose();
        rulesView.Dispose();
        rulesManager.Dispose();
        taskToLoadNext?.Dispose();
        viewMain.Dispose();
        Menu.Dispose();
        Body.Dispose();
    }
}

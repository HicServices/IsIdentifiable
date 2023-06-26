using ii.Views;
using ii.Views.Manager;
using IsIdentifiable.Failures;
using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using IsIdentifiable.Rules;
using IsIdentifiable.Rules.Storage;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace ii;

class MainWindow : IDisposable
{
    /// <summary>
    /// The report CSV file that is currently open
    /// </summary>
    public ReportReader? CurrentReport { get; set; }

    private readonly FailureView _valuePane;
    private readonly Label _info;
    private readonly SpinnerView _spinner;
    private readonly TextField _gotoTextField;
    private readonly Label _ignoreRuleLabel;
    private readonly Label _updateRuleLabel;
    private readonly Label _currentReportLabel;

    private readonly IRegexRuleStore _ignoreActionRuleStore;
    private readonly IRegexRuleStore _reportActionRuleStore;

    /// <summary>
    /// Record of new rules added (e.g. Ignore with pattern X) along with the index of the failure.  This allows undoing user decisions
    /// </summary>
    private readonly Stack<int> _history = new();

    private readonly ColorScheme _greyOnBlack = new()
    {
        Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
        HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
        Disabled = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
        Focus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
    };

    private readonly MenuItem _miCustomPatterns;
    private readonly RulesView _rulesView;
    private readonly AllRulesManagerView _rulesManager;

    private readonly IFileSystem _fileSystem;

    public MenuBar Menu { get; private set; }

    public View Body { get; private set; }

    private Task? _taskToLoadNext;

    private readonly View viewMain;

    public MainWindow(
        IsIdentifiableBaseOptions analyserOpts,
        IsIdentifiableReviewerOptions opts,
        RegexRuleStore ignoreActionRuleStore,
        RegexRuleStore reportActionRuleStore,
        IFileSystem fileSystem
    )
    {
        // todo remove
        _fileSystem = fileSystem;

        _ignoreActionRuleStore = ignoreActionRuleStore;
        _reportActionRuleStore = reportActionRuleStore;


        Menu = new MenuBar(new MenuBarItem[] {
            new("_File (F9)", new MenuItem [] {
                new("_Open Report",null, OpenReport),
                new("_Quit", null, static () => Application.RequestStop())
            }),
            new("_Options", new MenuItem [] {
                _miCustomPatterns = new MenuItem("_Custom Patterns",null,ToggleCustomPatterns){CheckType = MenuItemCheckStyle.Checked,Checked = false}
            })
        });


        viewMain = new View() { Width = Dim.Fill(), Height = Dim.Fill() };
        _rulesView = new RulesView(_miCustomPatterns, ignoreActionRuleStore, reportActionRuleStore);
        _rulesManager = new AllRulesManagerView(analyserOpts, opts, fileSystem);

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

        var reportButton = new Button("Report")
        {
            X = 11
        };
        reportButton.Clicked += Report;
        frame.Add(reportButton);

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
        _updateRuleLabel = new Label() { X = 0, Y = 6, Text = "Report:", Width = 30, Height = 1 }; ;
        _currentReportLabel = new Label() { X = 0, Y = 8, Text = "Report:", Width = 30, Height = 1 };

        frame.Add(_ignoreRuleLabel);
        frame.Add(_updateRuleLabel);
        frame.Add(_currentReportLabel);

        viewMain.Add(_info);

        _spinner = new SpinnerView
        {
            X = Pos.Right(_info)
        };
        viewMain.Add(_spinner);
        _spinner.Visible = false;

        viewMain.Add(_valuePane);
        viewMain.Add(frame);

        if (!string.IsNullOrWhiteSpace(opts.FailuresCsv))
            OpenReport(opts.FailuresCsv, (e) => throw e);

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
        tabView.AddTab(new TabView.Tab("Tree View", _rulesView), false);
        tabView.AddTab(new TabView.Tab("Rules Manager", _rulesManager), false);

        tabView.SelectedTabChanged += TabView_SelectedTabChanged;

        Body = tabView;
    }

    private void TabView_SelectedTabChanged(object? sender, TabView.TabChangedEventArgs e)
    {
        // sync the rules up in case people are adding new ones using the other UIs
        _rulesManager.RebuildTree();
    }

    private void ToggleCustomPatterns() => _miCustomPatterns.Checked = !_miCustomPatterns.Checked;

    private void Undo()
    {
        if (_history.Count == 0)
        {
            ViewHelpers.ShowMessage("_history Empty", "Cannot undo, history is empty");
            return;
        }

        var popped = _history.Pop();

        // TODO
        //undo file history
        //popped.OutputBase.Undo();

        //wind back UI
        GoTo(popped);
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
        catch (FormatException _)
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
            ViewHelpers.ShowException("Failed to GoTo", e);
        }
    }

    private void SetupToShow(Failure? failure)
    {
        _valuePane.CurrentFailure = failure;

        if (failure != null)
        {
            _ignoreRuleLabel.Text = $"Ignore:{RegexRuleFactory.IfFPatternForWholeProblemValue(failure)}";
            _updateRuleLabel.Text = $"Report:{RegexRuleFactory.IfPatternForRuleMatchingProblemValues(failure)}";
        }
        else
        {
            _ignoreRuleLabel.Text = "Ignore:";
            _updateRuleLabel.Text = "Report:";
        }
    }

    private void BeginNext() => _taskToLoadNext = Task.Run(Next);

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
                if (_reportActionRuleStore.HasRuleCovering(CurrentReport.CurrentFailure, out var reportActionRule))
                {
                    updated++;
                }
                else if (_ignoreActionRuleStore.HasRuleCovering(CurrentReport.CurrentFailure, out _))
                {
                    skipped++;
                }
                else
                {
                    SetupToShow(CurrentReport.CurrentFailure);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            ViewHelpers.ShowException("Error moving to next record", e);
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
            info.Append(" (End of Failures)");

        _info.Text = info.ToString();
    }

    private void Ignore()
    {
        var failure = _valuePane.CurrentFailure;

        if (failure == null || CurrentReport == null)
            return;

        if (_taskToLoadNext != null && !_taskToLoadNext.IsCompleted)
        {
            MessageBox.Query("StillLoading", "Load next is still running");
            return;
        }

        try
        {
            IRegexRule rule = _ignoreActionRuleStore.DefaultRuleFor(failure);

            if (_miCustomPatterns.Checked)
                rule = ViewHelpers.GetCustomRule(failure, rule);

            _ignoreActionRuleStore.Add(rule);
            _history.Push(CurrentReport.CurrentIndex);
        }
        catch (OperationCanceledException _)
        {
            //if user cancels adding the ignore then stay on the same record
            return;
        }

        BeginNext();
    }

    private void Report()
    {
        var failure = _valuePane.CurrentFailure;

        if (failure == null || CurrentReport == null)
            return;

        if (_taskToLoadNext != null && !_taskToLoadNext.IsCompleted)
        {
            MessageBox.Query("StillLoading", "Load next is still running");
            return;
        }

        try
        {
            IRegexRule rule = _reportActionRuleStore.DefaultRuleFor(failure);

            if (_miCustomPatterns.Checked)
                rule = ViewHelpers.GetCustomRule(failure, rule);

            _reportActionRuleStore.Add(rule);
            _history.Push(CurrentReport.CurrentIndex);
        }
        catch (OperationCanceledException)
        {
            //if user cancels updating then stay on the same record
            return;
        }
        catch (Exception e)
        {
            ViewHelpers.ShowException("Failed to update database", e);
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
            ViewHelpers.ShowException("Failed to Load", ex);
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

        using var dlg = new Dialog("Opening", ViewConstants.DlgWidth, 5, btn);
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
                CurrentReport = new ReportReader(
                    _fileSystem.FileInfo.New(path),
                    (s) => rows.Text = $"Loaded: {s:N0} rows",
                    cts.Token
                );
                SetupToShow(CurrentReport.Failures.FirstOrDefault());
                BeginNext();

                _rulesView.LoadReport(CurrentReport);
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

    public void Dispose()
    {
        _valuePane.Dispose();
        _info.Dispose();
        _spinner.Dispose();
        _gotoTextField.Dispose();
        _ignoreRuleLabel.Dispose();
        _updateRuleLabel.Dispose();
        _currentReportLabel.Dispose();
        _rulesView.Dispose();
        _rulesManager.Dispose();
        _taskToLoadNext?.Dispose();
        viewMain.Dispose();
        Menu.Dispose();
        Body.Dispose();
    }
}

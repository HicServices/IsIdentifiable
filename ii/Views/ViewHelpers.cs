using IsIdentifiable.Failures;
using IsIdentifiable.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Terminal.Gui;

namespace ii.Views
{
    public static class ViewHelpers
    {
        public static void ShowMessage(string title, string body) => RunDialog(title, body, out _, "Ok");

        public static void ShowException(string msg, Exception e)
        {
            var e2 = e;
            const string stackTraceOption = "Stack Trace";
            StringBuilder sb = new();

            while (e2 != null)
            {
                sb.AppendLine(e2.Message);
                e2 = e2.InnerException;
            }

            if (GetChoice(msg, sb.ToString(), out string? chosen, "Ok", stackTraceOption) &&
                string.Equals(chosen, stackTraceOption))
                ShowMessage("Stack Trace", e.ToString());
        }

        public static bool GetChoice<T>(string title, string body, out T? chosen, params T[] options) => RunDialog(title, body, out chosen, options);

        public static bool RunDialog<T>(string title, string message, out T? chosen, params T[] options)
        {
            var result = default(T);
            var optionChosen = false;

            using var dlg = new Dialog(title, Math.Min(Console.WindowWidth, ViewConstants.DlgWidth), ViewConstants.DlgHeight);

            var line = ViewConstants.DlgHeight - (ViewConstants.DlgBoundary) * 2 - options.Length;

            if (!string.IsNullOrWhiteSpace(message))
            {
                var width = Math.Min(Console.WindowWidth, ViewConstants.DlgWidth) - (ViewConstants.DlgBoundary * 2);

                var msg = Wrap(message, width - 1).TrimEnd();

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

            foreach (var value in options)
            {
                var v1 = value;

                var name = value?.ToString() ?? "";

                var btn = new Button(0, line++, name);
                btn.Clicked += () =>
                {
                    result = v1;
                    dlg.Running = false;
                    optionChosen = true;
                };

                dlg.Add(btn);

                if (options.Length == 1)
                    dlg.FocusFirst();
            }

            Application.Run(dlg);

            chosen = result;
            return optionChosen;
        }

        public static IRegexRule GetCustomRule(Failure failure, IRegexRule defaultRule)
        {
            var buttons = new Dictionary<string, string>
            {
                { "Clear", "" },
                { "Full Value", RegexRuleFactory.IfFPatternForWholeProblemValue(failure) },
                { "Failing Parts", RegexRuleFactory.IfPatternForRuleMatchingProblemValues(failure) },
                { @"\d",RegexRuleFactory.IfPatternForRuleMatchingSymbols(failure, SymbolsRuleMode.DigitsOnly) },
                { @"\c", RegexRuleFactory.IfPatternForRuleMatchingSymbols(failure, SymbolsRuleMode.CharactersOnly ) },
                { @"\d\c", RegexRuleFactory.IfPatternForRuleMatchingSymbols(failure, SymbolsRuleMode.Full) },
            };

            while (true)
            {
                if (!GetText("Pattern", "Enter pattern to match failure", defaultRule.IfPattern, buttons, out var chosen))
                    continue;

                if (string.IsNullOrWhiteSpace(chosen))
                {
                    ShowMessage("Invalid Regex", "Pattern was null or empty");
                    continue;
                }

                IRegexRule rule;

                try
                {
                    rule = new RegexRule()
                    {
                        Action = defaultRule.Action,
                        IfColumn = defaultRule.IfColumn,
                        As = defaultRule.As,
                        IfPattern = chosen,
                        CaseSensitive = defaultRule.CaseSensitive,
                    };
                }
                catch (Exception _)
                {
                    ShowMessage("Invalid Regex", "Pattern was not a valid Regex");
                    continue;
                }

                if (!rule.Covers(failure))
                {
                    GetChoice("Pattern Match Failure", "The provided pattern did not match the original ProblemValue. Try a different pattern?", out var retry, new[] { "Yes", "No" });

                    if (retry == "Yes")
                        continue;
                    else
                        throw new OperationCanceledException("User chose not to enter a pattern");
                }

                return rule;
            }
        }

        public static string Wrap(string s, int width)
        {
            var r = new Regex($@"(?:((?>.{{1,{width}}}(?:(?<=[^\S\r\n])[^\S\r\n]?|(?=\r?\n)|$|[^\S\r\n]))|.{{1,16}})(?:\r?\n)?|(?:\r?\n|$))");
            return r.Replace(s, "$1\n");
        }

        private static bool GetText(
            string title,
            string message,
            string initialValue,
            Dictionary<string, string> buttons,
            out string chosen
        )
        {
            const string PatternHelp = @"x - clears currently typed pattern
F - creates a regex pattern that matches the full input value
G - creates a regex pattern that matches only the failing part(s)
\d - replaces all digits with regex wildcards
\c - replaces all characters with regex wildcards
\d\c - replaces all digits and characters with regex wildcards";

            var optionChosen = false;

            using var dlg = new Dialog(title, Math.Min(Console.WindowWidth, ViewConstants.DlgWidth), ViewConstants.DlgHeight);

            var line = ViewConstants.DlgHeight - (ViewConstants.DlgBoundary) * 2 - 2;

            if (!string.IsNullOrWhiteSpace(message))
            {
                var width = Math.Min(Console.WindowWidth, ViewConstants.DlgWidth) - (ViewConstants.DlgBoundary * 2);

                var msg = Wrap(message, width - 1).TrimEnd();

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

            var txt = new TextField(0, line++, ViewConstants.DlgWidth - 4, initialValue ?? "");
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
    }
}

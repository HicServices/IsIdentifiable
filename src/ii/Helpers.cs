using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Terminal.Gui;

namespace ii;

public static class Helpers
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

    public static string Wrap(string s, int width)
    {
        var r = new Regex($@"(?:((?>.{{1,{width}}}(?:(?<=[^\S\r\n])[^\S\r\n]?|(?=\r?\n)|$|[^\S\r\n]))|.{{1,16}})(?:\r?\n)?|(?:\r?\n|$))");
        return r.Replace(s, "$1\n");
    }

    private static bool RunDialog<T>(string title, string message, out T? chosen, params T[] options)
    {
        var result = default(T);
        var optionChosen = false;

        using var dlg = new Dialog(title, Math.Min(Console.WindowWidth, Constants.DlgWidth), Constants.DlgHeight);

        var line = Constants.DlgHeight - Constants.DlgBoundary * 2 - options.Length;

        if (!string.IsNullOrWhiteSpace(message))
        {
            var width = Math.Min(Console.WindowWidth, Constants.DlgWidth) - Constants.DlgBoundary * 2;

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
}

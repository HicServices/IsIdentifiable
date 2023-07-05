using CommandLine;
using ii.Redact;
using ii.Review;
using ii.Scan;
using IsIdentifiable.Reporting.Reports;
using System;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;

namespace ii;

internal static class Program
{
    internal static IFileSystem FileSystem = new FileSystem();

    [Verb("redact", HelpText = "Redact values in a database based on existing rules")]
    private class RedactVerb { }

    [Verb("review", HelpText = $"Review a {nameof(FailureReport)} and generate new rules")]
    private class ReviewVerb { }

    [Verb("scan", HelpText = $"Scan a resource using existing rules and generate a {nameof(FailureReport)}")]
    private class ScanVerb { }

    public static int Main(string[] args)
    {
        var allVerbTypes = (
            from t in Assembly.GetExecutingAssembly().GetTypes()
            where
                t.DeclaringType == typeof(Program) &&
                t.IsClass &&
                t.Name.EndsWith("Verb")
            select t
        );

        if (args.Length < 1)
        {
            var allVerbNames = allVerbTypes.Select(t => t.GetCustomAttribute<VerbAttribute>()!.Name);
            Console.Error.WriteLine($"Error: Must specify one action from: {string.Join(", ", allVerbNames)}");
            return 1;
        }

        var verb = args.Take(1);
        var rest = args[1..] ?? Array.Empty<string>();
        var parser = ParserHelpers.GetDefaultParser();

        try
        {
            return parser
                .ParseArguments(verb, allVerbTypes.ToArray())
                .MapResult(
                    (RedactVerb _) => RedactMain.Run(rest),
                    (ReviewVerb _) => ReviewMain.Run(rest),
                    (ScanVerb _) => ScanMain.Run(rest, FileSystem),
                    _ => 1
                );
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"{e}\n\nError: {e.Message}");
            return 1;
        }
    }
}

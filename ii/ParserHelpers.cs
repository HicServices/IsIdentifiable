using CommandLine;
using System;

namespace ii;
internal static class ParserHelpers
{
    internal static Parser GetDefaultParser()
    {
        ParserSettings defaults = Parser.Default.Settings;
        return new Parser(with =>
        {
            with.CaseInsensitiveEnumValues = true;
            with.CaseSensitive = false;
            with.EnableDashDash = defaults.EnableDashDash;
            with.HelpWriter = defaults.HelpWriter;
            with.IgnoreUnknownArguments = false;
            with.MaximumDisplayWidth = defaults.MaximumDisplayWidth;
            with.ParsingCulture = defaults.ParsingCulture;
        });
    }

    internal static T? TryParse<T>(string[] args) where T : CliVerbBase
    {
        var result = GetDefaultParser().ParseArguments<T>(args);
        if (result.Value == null)
        {
            Console.Error.WriteLine(result.Errors);
            return null;
        }

        return result.Value;
    }
}

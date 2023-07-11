using IsIdentifiable.Failures;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace IsIdentifiable.Rules;

public static class RegexRuleHelpers
{
    /// <summary>
    /// Load a list of rules from the specified file
    /// </summary>
    /// <param name="rulesFile"></param>
    /// <param name="createIfMissing">If the specified file is not found, create it and return an empty list. Otherwise throws a <see cref="FileNotFoundException"/></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static IList<IRegexRule> LoadFrom(IFileInfo rulesFile, bool createIfMissing)
    {
        rulesFile.Refresh();

        if (!rulesFile.Exists)
        {
            if (!createIfMissing)
                throw new FileNotFoundException($"{rulesFile.FullName} does not exist");

            rulesFile.Create();
            return new List<IRegexRule>();
        }

        var existingRules = rulesFile.FileSystem.File.ReadAllText(rulesFile.FullName);

        if (string.IsNullOrWhiteSpace(existingRules))
            return new List<IRegexRule>();

        var deserializer = new Deserializer();

        try
        {
            return deserializer.Deserialize<IList<RegexRule>>(existingRules).ToList<IRegexRule>();
        }
        catch (YamlException e)
        {
            throw new InvalidDataException($"Invalid rules content in file {rulesFile.FullName}", e);
        }
    }

    /// <summary>
    /// Serializes the <paramref name="rule"/> into yaml
    /// </summary>
    /// <param name="rule"></param>
    /// <returns></returns>
    public static string Serialize(IRegexRule rule) =>
        new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .Build()
        .Serialize(new List<IRegexRule>() { rule });

    /// <summary>
    /// Serializes the <paramref name="rule"/> into yaml with a comment at the start
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static string SerializeWithComment(IRegexRule rule, DateTime dateTime)
    {
        var yaml = Serialize(rule);
        return $"#{Environment.UserName} - {dateTime}{Environment.NewLine}{yaml}";
    }

    /// <summary>
    /// Returns a <see cref="IRegexRule"/> matching the given <paramref name="failure"/>,
    /// <paramref name="action"/>, and <paramref name="ifPattern"/>
    /// </summary>
    /// <param name="failure"></param>
    /// <param name="action"></param>
    /// <param name="ifPattern"></param>
    /// <returns></returns>
    public static IRegexRule RuleMatching(Failure failure, RuleAction action, string ifPattern)
    {
        FailureClassification classification;
        if (action == RuleAction.Ignore)
            classification = FailureClassification.None;
        else
            classification = failure.Parts.Select(p => p.Classification).FirstOrDefault();

        return new RegexRule
        {
            Action = action,
            IfColumn = failure.ProblemField,
            IfPattern = ifPattern,
            As = classification,
        };
    }

    /// <summary>
    /// Returns a pattern that matches the full cell value represented by the
    /// <paramref name="failure"/> exactly (with no permitted leading/trailing content)
    /// </summary>
    /// <param name="failure"></param>
    /// <returns></returns>
    public static string IfFPatternForWholeProblemValue(Failure failure) => $"^{Regex.Escape(failure.ProblemValue)}$";

    /// <summary>
    /// Returns a pattern that matches <see cref="FailurePart.Word"/> in <see cref="Failure.ProblemValue"/>.
    /// If the word appears at the start/end of the value then ^ or $ is used.
    /// When there are multiple failing parts anything is permitted inbweteen i.e. .*
    /// </summary>
    /// <param name="failure"></param>
    /// <returns></returns>
    public static string IfPatternForRuleMatchingProblemValues(Failure failure)
    {
        // TODO (rkm 2023-07-11)
        //// source is image pixel data
        //if (failure.ProblemField?.StartsWith(DicomFileRunner.PixelData) ?? false)
        //    return RuleMatching(failure, action, $"^{Regex.Escape(failure.ProblemValue)}$");

        var sb = new StringBuilder();

        var minOffset = failure.Parts.Min(p => p.Offset);
        var maxPartEnding = failure.Parts.Max(p => p.Offset + p.Word.Length);

        if (minOffset == 0)
            sb.Append('^');

        foreach (var p in failure.ConflateParts())
        {
            //match with capture group the given Word
            sb.Append($"({Regex.Escape(p)}).*");
        }

        // TODO (rkm 2023-07-11)
        //// source is image pixel data
        //if (failure.ProblemField?.StartsWith(DicomFileRunner.PixelData) ?? false)
        //    return RuleMatching(failure, action, sb.ToString());

        // If there is a failure part that ends at the end of the input string then the pattern should have a terminator
        // to denote that we only care about problem values ending in this pattern (user can always override that decision)
        return (maxPartEnding == failure.ProblemValue.Length) ? $"{sb.ToString(0, sb.Length - 2)}$" : sb.ToString(0, sb.Length - 2);
    }

    /// <summary>
    /// Returns just the failing parts expressed as digits and wrapped in capture group(s) e.g. ^(\d\d-\d\d-\d\d).*([A-Z][A-Z])
    /// </summary>
    /// <param name="failure"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static string IfPatternForRuleMatchingSymbols(Failure failure, SymbolsRuleMode mode)
    {
        // failures should really have parts!
        if (!failure.Parts.Any())
            throw new ArgumentException("Failure had no Parts");

        // TODO (rkm 2023-07-11)
        // source is image pixel data
        //if (failure.ProblemField?.StartsWith(DicomFileRunner.PixelData) ?? false)
        //    return $"^{Regex.Escape(failure.ProblemValue)}$";

        var sb = new StringBuilder();

        var minOffset = failure.Parts.Min(p => p.Offset);
        var maxPartEnding = failure.Parts.Max(p => p.Offset + p.Word.Length);

        if (minOffset == 0)
            sb.Append('^');

        foreach (var p in failure.ConflateParts())
        {
            //match with capture group the given Word
            sb.Append('(');

            foreach (var cur in p)
            {
                if (char.IsDigit(cur) && mode != SymbolsRuleMode.CharactersOnly)
                    sb.Append("\\d");
                else
                if (char.IsLetter(cur) && mode != SymbolsRuleMode.DigitsOnly)
                    sb.Append(char.IsUpper(cur) ? "[A-Z]" : "[a-z]");
                else
                    sb.Append(Regex.Escape(cur.ToString()));
            }

            sb.Append(").*");
        }

        // If there is a failure part that ends at the end of the input string then the pattern should have a terminator
        // to denote that we only care about problem values ending in this pattern (user can always override that decision)
        return maxPartEnding == failure.ProblemValue.Length ? $"{sb.ToString(0, sb.Length - 2)}$" : sb.ToString(0, sb.Length - 2);
    }
}

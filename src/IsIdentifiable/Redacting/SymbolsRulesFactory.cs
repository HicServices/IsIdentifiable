using IsIdentifiable.Failures;
using IsIdentifiable.Rules;
using IsIdentifiable.Scanners;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IsIdentifiable.Redacting;

/// <summary>
/// Generates Regex patterns for matching <see cref="Failure"/> based on permutations of digits (\d) and/or characters([A-Z] or [a-z]).  See also <seealso cref="SymbolsRuleMode"/>.
/// </summary>
public class SymbolsRulesFactory : IRulePatternFactory
{
    /// <summary>
    /// Whether to generate Regex match patterns using the permutation of characters, digits or both.
    /// </summary>
    public SymbolsRuleMode Mode { get; set; }

    /// <summary>
    /// Returns just the failing parts expressed as digits and wrapped in capture group(s) e.g. ^(\d\d-\d\d-\d\d).*([A-Z][A-Z])
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="failure"></param>
    /// <returns></returns>
    public string GetPattern(object sender, Failure failure)
    {
        // failures should really have parts!
        if (!failure.Parts.Any())
            throw new ArgumentException("Failure had no Parts");

        // source is image pixel data
        if (failure.ProblemField?.StartsWith(DicomFileScanner.PixelData) ?? false)
            return $"^{Regex.Escape(failure.ProblemValue)}$";

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
                if (char.IsDigit(cur) && Mode != SymbolsRuleMode.CharactersOnly)
                    sb.Append("\\d");
                else
                if (char.IsLetter(cur) && Mode != SymbolsRuleMode.DigitsOnly)
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

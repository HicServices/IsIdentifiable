using IsIdentifiable.Failures;
using IsIdentifiable.Scanners;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IsIdentifiable.Redacting;

/// <summary>
/// <see cref="IRulePatternFactory"/> which generates <see cref="Regex"/> rule patterns that match only the <see cref="FailurePart.Word"/> and allowing anything between/before
/// </summary>
public class MatchProblemValuesPatternFactory : IRulePatternFactory
{
    /// <summary>
    /// Returns a pattern that matches <see cref="FailurePart.Word"/> in <see cref="Failure.ProblemValue"/>.  If the word appears at the start/end of the value then ^ or $ is used.  When there are multiple failing parts anything is permitted inbweteen i.e. .*
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="failure"></param>
    /// <returns></returns>
    public string GetPattern(object sender, Failure failure)
    {
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
            sb.Append($"({Regex.Escape(p)}).*");
        }

        // source is image pixel data
        if (failure.ProblemField?.StartsWith(DicomFileScanner.PixelData) ?? false)
            return sb.ToString();

        // If there is a failure part that ends at the end of the input string then the pattern should have a terminator
        // to denote that we only care about problem values ending in this pattern (user can always override that decision)
        return maxPartEnding == failure.ProblemValue.Length ? $"{sb.ToString(0, sb.Length - 2)}$" : sb.ToString(0, sb.Length - 2);
    }
}

using IsIdentifiable.Failures;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace IsIdentifiable.Rules;

public class PartPatternFilterRule : RegexRule
{
    /// <summary>
    /// Combination of <see cref="IfPartPattern"/> and <see cref="CaseSensitive"/>.  Use this to validate
    /// whether the rule should be applied.
    /// </summary>
    protected Regex IfPartPatternRegex;
    private string _ifPartPatternString;

    /// <summary>
    /// The Regex pattern which should be used to match values a specific failing part
    /// </summary>
    public string IfPartPattern
    {
        get => _ifPartPatternString;
        set
        {
            _ifPartPatternString = value;
            RebuildPartRegex();
        }
    }

    /// <summary>
    /// Whether the IfPattern and IfPartPattern are case sensitive (default is false)
    /// </summary>
    public override bool CaseSensitive
    {
        get => base.CaseSensitive;
        set
        {
            base.CaseSensitive = value;
            RebuildPartRegex();
        }
    }

    private static readonly string[] _wordSeparators = new[] { @"\s", "-" };
    private static readonly string _wordSeparatorRegexPart = string.Join('|', _wordSeparators);

    private string _wordBefore;
    public string WordBefore
    {
        get => _wordBefore;
        set
        {
            if (value.Contains('^') || value.Contains('$'))
                throw new ArgumentException("WordBefore should not contain '^' or '$'");

            _wordBefore = value;
        }
    }

    private string _wordAfter;
    public string WordAfter
    {
        get => _wordAfter;
        set
        {
            if (value.Contains('^') || value.Contains('$'))
                throw new ArgumentException("WordAfter should not contain '^' or '$'");

            _wordAfter = value;
        }
    }

    private Regex? _wordBeforeRegex;
    private Regex? _wordAfterRegex;

    private int _usedCount = 0;
    private object _usedCountLock = new();

    public int UsedCount
    {
        get => _usedCount;
    }

    public void IncrementUsed()
    {
        lock (_usedCountLock)
        {
            ++_usedCount;
        }
    }

    // TODO(rkm 2023-07-25) Shouldn't be needed when IfPattern is readonly
    private void RebuildPartRegex()
    {
        if (_ifPartPatternString == null)
            throw new Exception("Illegal rule setup. You must specify IfPartPattern");

        if (!_ifPartPatternString.StartsWith("^") || !_ifPartPatternString.EndsWith("$"))
            throw new ArgumentException("IfPartPattern must be enclosed by ^ and $");

        IfPartPatternRegex = new Regex(_ifPartPatternString, (CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.Compiled);
    }

    public bool Covers(FailurePart failurePart, string problemValue)
    {
        if (As != FailureClassification.None && As != failurePart.Classification)
            return false;

        bool matchesBefore = false;
        if (!string.IsNullOrWhiteSpace(WordBefore))
        {
            var problemValueUpToOffset = problemValue[..(failurePart.Offset + failurePart.Word.Length)];
            _wordBeforeRegex ??= new Regex(@$"\b{WordBefore}({_wordSeparatorRegexPart})+{IfPartPattern.TrimStart('^')}", (CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.Compiled);
            matchesBefore = _wordBeforeRegex.Matches(problemValueUpToOffset).Any();
        }

        bool matchesAfter = false;
        if (!string.IsNullOrWhiteSpace(WordAfter))
        {
            var problemValueFromOffset = problemValue[failurePart.Offset..];
            _wordAfterRegex ??= new Regex(@$"{IfPartPattern.TrimEnd('$')}({_wordSeparatorRegexPart})+{WordAfter}\b", (CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.Compiled);
            matchesAfter = _wordAfterRegex.Matches(problemValueFromOffset).Any();
        }

        if (
            matchesBefore && string.IsNullOrWhiteSpace(WordAfter) ||
            matchesAfter && string.IsNullOrWhiteSpace(WordBefore) ||
            (matchesBefore && matchesAfter)
        )
        {
            return true;
        }
        else if (!string.IsNullOrWhiteSpace(WordBefore) || !string.IsNullOrWhiteSpace(WordAfter))
        {
            return false;
        }

        return IfPartPatternRegex.Matches(failurePart.Word).Any();
    }

    public override string ToString() => $"Pat:'{_ifPartPatternString}' WB:'{WordBefore}' WA:'{WordAfter}' Col:'{IfColumn}' As:'{As}' x{_usedCount:N0}";
}

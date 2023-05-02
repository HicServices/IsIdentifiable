using Equ;
using IsIdentifiable.Failures;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// XXX using RegexOptions.Compiled may result in a large amount of static code
// which is never freed during garbage collection, see
// https://docs.microsoft.com/en-us/dotnet/standard/base-types/compilation-and-reuse-in-regular-expressions
// Note that the Regex Cache is not used in instance methods.

namespace IsIdentifiable.Rules;

/// <summary>
/// A simple Regex based rule that allows flexible white listing or blacklisting of values
/// either in all columns or only a single column
/// </summary>
public class RegexRule : MemberwiseEquatable<RegexRule>, IRegexRule
{
    /// <inheritdoc/>
    public RuleAction Action { get; set; }

    /// <inheritdoc/>
    public string IfColumn { get; set; }

    /// <inheritdoc/>
    public FailureClassification As { get; set; }

    /// <summary>
    /// Combination of <see cref="IfPattern"/> and <see cref="CaseSensitive"/>.  Use this to validate
    /// whether the rule should be applied.
    /// </summary>
    protected Regex IfPatternRegex;
    private string _ifPatternString;
    private bool _caseSensitive;

    /// <inheritdoc/>
    public string IfPattern
    {
        get => _ifPatternString;
        set
        {
            _ifPatternString = value;
            RebuildRegex();
        }
    }

    /// <inheritdoc/>
    public virtual bool CaseSensitive
    {
        get => _caseSensitive;
        set
        {
            _caseSensitive = value;
            RebuildRegex();
        }
    }

    private void RebuildRegex()
    {
        IfPatternRegex = _ifPatternString == null ? null : new Regex(_ifPatternString, (CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.Compiled);
    }

    /// <summary>
    /// Compares the <paramref name="fieldValue"/> with the <see cref="IfPattern"/>, <see cref="CaseSensitive"/> and any
    /// other rule restrictions and returns whether this results in a match.  When matching returns the <see cref="Action"/>
    /// (ignore or report as identifiable etc).
    /// </summary>
    /// <param name="fieldName">The field you are evaluating (column name or tag name)</param>
    /// <param name="fieldValue">The value in the field</param>
    /// <param name="badParts">The bits of the <paramref name="fieldValue"/> (if any) that resulted in the return value</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public virtual RuleAction Apply(string fieldName, string fieldValue, out IEnumerable<FailurePart> badParts)
    {
        badParts = null;

        if (Action == RuleAction.None)
            return RuleAction.None;

        if(IfColumn == null && IfPattern == null)
            throw new Exception("Illegal rule setup.  You must specify either a column or a pattern (or both)");

        if(Action == RuleAction.Report && As == FailureClassification.None)
            throw new Exception("Illegal rule setup.  You must specify 'As' when Action is Report");

        //if there is no column restriction or restriction applies to the current column
        if (string.IsNullOrWhiteSpace(IfColumn) || string.Equals(IfColumn,fieldName,StringComparison.InvariantCultureIgnoreCase))
        {
            // only allocate this variable if there is an action to take
            badParts = new List<FailurePart>();

            //if there is no pattern
            if (IfPattern == null)
            {
                //we are reporting everything in this column? ok fair enough (no pattern just column name)
                if (Action == RuleAction.Report) 
                    ((IList) badParts).Add(new FailurePart(fieldValue, As, 0));

                return Action;
            }

            // if the pattern matches the string we examined
            var matches = IfPatternRegex.Matches(fieldValue);
            if (matches.Any())
            {
                //if we are reporting all failing regexes
                if(Action == RuleAction.Report)
                    foreach (Match match in matches)
                        ((IList) badParts).Add(new FailurePart(match.Value, As, match.Index));

                return Action;
            }


        }

        //our rule does not apply to the current value

        return RuleAction.None;
    }

    /// <inheritdoc/>
    public bool AreIdentical(IRegexRule other, bool requireIdenticalAction = true)
    {
        return
            string.Equals(IfColumn, other.IfColumn,StringComparison.CurrentCultureIgnoreCase) &&
            (!requireIdenticalAction || Action == other.Action) &&
            string.Equals(IfPattern, other.IfPattern,StringComparison.CurrentCultureIgnoreCase);
    }
}

using IsIdentifiable.Failures;

namespace IsIdentifiable.Rules;

/// <summary>
/// Base interface for all regex rules
/// </summary>
public interface IRegexRule : IAppliableRule
{
    /// <summary>
    /// What to do if the rule is found to match the values being examined (e.g.
    /// Allowlist the value or report the value as a validation failure)
    /// </summary>
    RuleAction Action { get; }

    /// <summary>
    /// The column/tag in which to apply the rule.  If empty then the rule applies to all columns
    /// </summary>
    string IfColumn { get; }

    /// <summary>
    /// What you are trying to classify (if <see cref="Action"/> is <see cref="RuleAction.Report"/>)
    /// </summary>
    FailureClassification As { get; }

    /// <summary>
    /// The Regex pattern which should be used to match values with
    /// </summary>
    string IfPattern { get; }

    /// <summary>
    /// Whether the IfPattern match is case sensitive (default is false)
    /// </summary>
    bool CaseSensitive { get; }

    /// <summary>
    /// Returns true if the current and <paramref name="other"/> rule match using the same pattern and col.
    /// </summary>
    /// <param name="other"></param>
    /// <param name="requireIdenticalAction">True (default) if identical must also include the same <see cref="Action"/> for values matching the rule</param>
    /// <returns></returns>
    bool AreIdentical(IRegexRule other, bool requireIdenticalAction = true);

    /// <summary>
    /// Test whether the rule generates a <see cref="RuleAction"/> for the given <paramref name="failure"/>
    /// </summary>
    /// <param name="failure"></param>
    /// <returns></returns>
    bool Covers(Failure failure);
}

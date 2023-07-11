namespace IsIdentifiable.Rules;

/// <summary>
/// Describes what action should be taken by IsIdentifiable when evaluating
/// a string for identifiable information e.g.  Report or Ignore or do nothing.
/// </summary>
public enum RuleAction
{
    /// <summary>
    /// Unused
    /// </summary>
    None = 0,

    /// <summary>
    /// The value should be Allowlisted and ignored by any downstream classifiers that might
    /// otherwise have an issue with it
    /// </summary>
    Ignore,

    /// <summary>
    /// The value violates system rules and likely contains identifiable data.  It should be reported
    /// as a failure.
    /// </summary>
    Report,
}

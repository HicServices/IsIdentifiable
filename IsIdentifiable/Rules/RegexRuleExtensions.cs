using IsIdentifiable.Failures;

namespace IsIdentifiable.Rules
{
    /// <summary>
    /// Extension methods for <see cref="IRegexRule"/>
    /// </summary>
    public static class RegexRuleExtensions
    {
        /// <summary>
        /// Test whether the <paramref name="rule"/> generates a <see cref="RuleAction"/> for the given <paramref name="failure"/>
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="failure"></param>
        /// <returns></returns>
        public static bool Covers(this IRegexRule rule, Failure failure) => rule.Apply(failure.ProblemField, failure.ProblemValue, out _) != RuleAction.None;
    }
}

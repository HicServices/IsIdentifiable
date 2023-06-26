using IsIdentifiable.Failures;
using System.Collections.Generic;

namespace IsIdentifiable.Rules.Storage
{
    public interface IRegexRuleStore : ICollection<IRegexRule>
    {
        /// <summary>
        /// Get the built-in <see cref="IRegexRuleGenerator"/> used to generate new rules
        /// </summary>
        IRegexRuleGenerator RuleGenerator { get; }

        /// <summary>
        /// Returns true if at least one rule covers the given failure
        /// </summary>
        /// <param name="failure">Can be null</param>
        /// <param name="rule">The first rule which covers the failure, or null</param>
        /// <returns></returns>
        bool HasRuleCovering(Failure failure, out IRegexRule rule);

        /// <summary>
        /// Generate a default rule for the given failure
        /// </summary>
        /// <param name="failure"></param>
        /// <returns></returns>
        IRegexRule DefaultRuleFor(Failure failure);

        /// <summary>
        /// Removes the most recently added entry from the store, if one exists
        /// </summary>
        void Undo();
    }
}

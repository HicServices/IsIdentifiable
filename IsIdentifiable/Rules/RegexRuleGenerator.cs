using IsIdentifiable.Failures;
using System;

namespace IsIdentifiable.Rules
{
    public class RegexRuleGenerator : IRegexRuleGenerator
    {
        private readonly Func<Failure, string> _ifPatternFunc;
        private readonly RuleAction _action;

        public RegexRuleGenerator(Func<Failure, string> ifPatternFunc, RuleAction action)
        {
            _ifPatternFunc = ifPatternFunc;
            _action = action;
        }

        public string IfPatternFor(Failure failure) => _ifPatternFunc(failure);

        public IRegexRule GenerateFor(Failure failure) => RegexRuleFactory.RuleMatching(failure, _action, _ifPatternFunc(failure));
    }
}

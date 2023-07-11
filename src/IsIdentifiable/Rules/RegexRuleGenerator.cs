using IsIdentifiable.Failures;
using System;
using System.Linq;

namespace IsIdentifiable.Rules;

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

    public IRegexRule GenerateFor(Failure failure)
    {
        FailureClassification classification;
        if (_action == RuleAction.Ignore)
            classification = FailureClassification.None;
        else
            classification = failure.Parts.Select(p => p.Classification).FirstOrDefault();

        return new RegexRule
        {
            Action = _action,
            IfColumn = failure.ProblemField,
            IfPattern = IfPatternFor(failure),
            As = classification,
        };
    }
}

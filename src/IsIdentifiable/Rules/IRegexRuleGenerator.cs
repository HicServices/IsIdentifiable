using IsIdentifiable.Failures;

namespace IsIdentifiable.Rules;

public interface IRegexRuleGenerator
{
    string IfPatternFor(Failure failure);

    IRegexRule GenerateFor(Failure failure);
}

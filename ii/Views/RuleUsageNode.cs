using IsIdentifiable.Rules;
using IsIdentifiable.Rules.Storage;
using Terminal.Gui.Trees;

namespace ii.Views;

internal class RuleUsageNode : TreeNode
{
    public readonly IRegexRuleStore RuleStore;
    public readonly IRegexRule Rule;
    public int NumberOfTimesUsed { get; }

    public RuleUsageNode(IRegexRuleStore ruleStore, IRegexRule rule, int numberOfTimesUsed)
    {
        RuleStore = ruleStore;
        Rule = rule;
        NumberOfTimesUsed = numberOfTimesUsed;
    }

    public override string ToString()
    {
        return $"Pat:{Rule.IfPattern} Col:{Rule.IfColumn} x{NumberOfTimesUsed:N0}";
    }
}

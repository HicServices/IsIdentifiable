﻿using IsIdentifiable.Redacting;
using IsIdentifiable.Rules;
using Terminal.Gui.Trees;

namespace ii.Views;

internal class RuleUsageNode : TreeNode
{
    public OutBase Rulebase { get; }
    public IsIdentifiableRule Rule { get; }
    public int NumberOfTimesUsed { get; }

    public RuleUsageNode(OutBase rulebase, IsIdentifiableRule rule, int numberOfTimesUsed)
    {
        Rulebase = rulebase;
        Rule = rule;
        NumberOfTimesUsed = numberOfTimesUsed;
    }

    public override string ToString()
    {
        return $"Pat:{Rule.IfPattern} Col:{Rule.IfColumn} x{NumberOfTimesUsed:N0}";
    }
}

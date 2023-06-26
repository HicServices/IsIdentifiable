﻿using IsIdentifiable.Reporting;
using IsIdentifiable.Rules;
using System.Collections.Generic;
using Terminal.Gui.Trees;

namespace ii.Views;

internal class CollidingRulesNode : TreeNode
{
    /// <summary>
    /// An ignore rule that collides with the <see cref="UpdateRule"/> for certain input values
    /// </summary>
    public IsIdentifiableRule IgnoreRule { get; }

    /// <summary>
    /// An update rule that collides with the <see cref="IgnoreRule"/> for certain input values
    /// </summary>
    public IsIdentifiableRule UpdateRule { get; }

    /// <summary>
    /// Input failures that match both the <see cref="IgnoreRule"/> and the <see cref="UpdateRule"/>
    /// </summary>
    public readonly List<Failure> CollideOn;

    public CollidingRulesNode(IsIdentifiableRule ignoreRule, IsIdentifiableRule updateRule, Failure f)
    {
        IgnoreRule = ignoreRule;
        UpdateRule = updateRule;
        CollideOn = new List<Failure>(new[] { f });
    }

    public override string ToString()
    {
        return $"{IgnoreRule.IfPattern} : {UpdateRule.IfPattern} x{CollideOn.Count:N0}";
    }

    /// <summary>
    /// Adds the given failure to the list of input values that collide between these two rules
    /// </summary>
    /// <param name="f"></param>
    internal void Add(Failure f)
    {
        CollideOn.Add(f);
    }
}
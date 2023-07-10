using IsIdentifiable.Rules;
using System;
using System.Collections;
using System.Reflection;

namespace ii.Views.Manager;

internal class RuleTypeNode
{
    public RuleSetFileNode Parent { get; set; }
    private readonly PropertyInfo _prop;

    public IList? Rules { get; internal set; }

    /// <summary>
    /// Creates a new instance 
    /// </summary>
    /// <param name="ruleSet"></param>
    /// <param name="ruleProperty"></param>
    public RuleTypeNode(RuleSetFileNode ruleSet, string ruleProperty)
    {
        _prop = typeof(RuleSet).GetProperty(ruleProperty) ??
                throw new ArgumentException($"No property called {ruleProperty} exists on Type RuleSet");

        Parent = ruleSet;
        Rules = (IList?)_prop.GetValue(ruleSet.GetRuleSet());
    }

    public override string ToString() => _prop.Name;
}

using IsIdentifiable.Rules;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IsIdentifiable.Tests.Rules;

internal class RuleStoreTests
{
    private class MockRuleStore : RuleStore
    {
        public IRegexRule? AddedRule;
        public IRegexRule? DeletedRule;
        public IRegexRule? UndoRule;

        public MockRuleStore(IList<IRegexRule>? rules = null)
        {
            if (rules == null)
                return;
            Rules.AddRange(rules);
        }

        protected override void AddImpl(IRegexRule rule) => AddedRule = rule;
        protected override void DeleteImpl(IRegexRule rule) => DeletedRule = rule;
        protected override void UndoImpl(IRegexRule rule) => UndoRule = rule;
    }

    [Test]
    public void Add_NewRule_Single()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule = new RegexRule();

        // Act

        store.Add(rule);

        // Assert

        Assert.True(store.RulesView.Contains(rule));
        Assert.AreEqual(rule, store.AddedRule);
    }

    [Test]
    public void Add_NewRule_Multiple()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule1 = new RegexRule() { IfColumn = "foo" };
        var rule2 = new RegexRule() { IfColumn = "bar" };

        // Act

        store.Add(rule1);
        store.Add(rule2);

        // Assert

        Assert.True(store.RulesView.Contains(rule1));
        Assert.True(store.RulesView.Contains(rule2));
    }

    [Test]
    public void Add_CachedRule()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule = new RegexRule();

        // Act

        store.Add(rule);
        store.AddedRule = null;
        store.Add(rule);

        // Assert

        Assert.IsNull(store.AddedRule);
    }

    [Test]
    public void Delete_MissingRule()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule = new RegexRule();

        // Act

        var removed = store.Delete(rule);

        // Assert

        Assert.False(removed);
        Assert.IsNull(store.DeletedRule);
    }

    [Test]
    public void Delete_ExistingRule()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule = new RegexRule();
        store.Add(rule);

        // Act

        var removed = store.Delete(rule);

        // Assert

        Assert.True(removed);
        Assert.IsEmpty(store.RulesView);
        Assert.AreEqual(rule, store.DeletedRule);
    }

    [Test]
    public void Undo_NoRules_DoesNotThrow()
    {
        // Arrange

        var store = new MockRuleStore();

        // Act
        // Assert

        Assert.DoesNotThrow(() => store.Undo());
    }

    [Test]
    public void Undo_SingleRule()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule = new RegexRule();
        store.Add(rule);

        // Act

        store.Undo();

        // Assert

        Assert.AreEqual(0, store.RulesView.Count);
    }

    [Test]
    public void Undo_MultipleRules()
    {
        // Arrange

        var store = new MockRuleStore();
        var rule1 = new RegexRule() { IfColumn = "foo" };
        store.Add(rule1);
        var rule2 = new RegexRule() { IfColumn = "bar" };
        store.Add(rule2);

        // Act

        store.Undo();

        // Assert

        Assert.AreEqual(1, store.RulesView.Count);
        Assert.AreEqual(rule1, store.RulesView[0]);
    }
}

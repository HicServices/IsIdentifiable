using IsIdentifiable.Failures;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IsIdentifiable.Rules.Storage;

public abstract class RegexRuleStore : IRegexRuleStore
{
    /// <inheritdoc/>
    public int Count => _rules.Count;

    /// <inheritdoc/>
    public bool IsReadOnly { get; }

    /// <inheritdoc/>
    public IRegexRuleGenerator RuleGenerator { get; }

    private readonly List<IRegexRule> _rules = new();
    private readonly Stack<IRegexRule> _ruleHistory = new();

    protected RegexRuleStore(IRegexRuleGenerator ruleGenerator, bool isReadOnly)
    {
        RuleGenerator = ruleGenerator;
        IsReadOnly = isReadOnly;
    }

    /// <inheritdoc/>
    public IRegexRule DefaultRuleFor(Failure failure)
    {
        if (IsReadOnly)
            throw new NotSupportedException();

        return RuleGenerator.GenerateFor(failure);
    }

    /// <inheritdoc/>
    public void Add(IRegexRule rule)
    {
        if (IsReadOnly)
            throw new NotSupportedException();

        if (Contains(rule))
            return;

        _rules.Add(rule);
        _ruleHistory.Push(rule);

        AddImpl(rule);
    }

    /// <inheritdoc/>
    public bool Remove(IRegexRule rule)
    {
        if (IsReadOnly)
            throw new NotSupportedException();

        var removed = _rules.Remove(rule);

        if (!removed)
            return false;

        RemoveImpl(rule);

        return true;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (IsReadOnly)
            throw new NotSupportedException();

        if (_ruleHistory.Count == 0)
            return;

        var rule = _ruleHistory.Pop();
        _rules.Remove(rule);

        UndoImpl(rule);
    }

    /// <inheritdoc/>
    public bool HasRuleCovering(Failure failure, out IRegexRule rule)
    {
        rule = _rules.FirstOrDefault(r => r.Covers(failure));
        return rule != null;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (IsReadOnly)
            throw new NotSupportedException();

        ClearImpl();
        _rules.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(IRegexRule rule)
    {
        // TODO(rkm 2023-05-03) Following what OutBase does here, but we should investigate why we can't just use Equals
        return _rules.Any(r => r.AreIdentical(rule));
    }

    /// <inheritdoc/>
    public void CopyTo(IRegexRule[] array, int arrayIndex) => _rules.CopyTo(array);

    /// <inheritdoc/>
    public IEnumerator<IRegexRule> GetEnumerator() => _rules.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _rules.GetEnumerator();

    /// <summary>
    /// Allows implementations to set the initial rules list without causing AddImpl to be called
    /// </summary>
    /// <param name="rules"></param>
    protected void SetInitialRules(IEnumerable<IRegexRule> rules) => _rules.AddRange(rules);

    protected abstract void AddImpl(IRegexRule rule);
    protected abstract void RemoveImpl(IRegexRule rule);
    protected abstract void UndoImpl(IRegexRule rule);
    protected abstract void ClearImpl();
}

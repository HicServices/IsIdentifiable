using IsIdentifiable.Failures;
using System.Collections.Generic;
using System.Linq;

namespace IsIdentifiable.Rules;

public abstract class RuleStore : IRuleStore
{
    /// <summary>
    /// Existing rules which describe how to detect a <see cref="Failure"/> that should be handled by this class.
    /// </summary>
    protected List<IRegexRule> Rules { get; } = new List<IRegexRule>();

    /// <summary>
    /// A read-only view of the underlying Rules
    /// </summary>
    public IReadOnlyList<IRegexRule> RulesView => Rules.AsReadOnly();

    private readonly Stack<IRegexRule> _ruleHistory = new();

    protected RuleStore() { }

    /// <inheritdoc/>
    public void Add(IRegexRule rule)
    {
        // TODO(rkm 2023-05-03) Following what OutBase does here, but we should investigate why we can't just use Equals
        if (Rules.Any(r => r.AreIdentical(rule)))
            return;

        Rules.Add(rule);
        _ruleHistory.Push(rule);

        AddImpl(rule);
    }

    /// <inheritdoc/>
    public bool Delete(IRegexRule rule)
    {
        var removed = Rules.Remove(rule);

        if (!removed)
            return false;

        DeleteImpl(rule);

        return true;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_ruleHistory.Count == 0)
            return;

        var rule = _ruleHistory.Pop();
        Rules.Remove(rule);

        UndoImpl(rule);
    }

    protected abstract void AddImpl(IRegexRule rule);
    protected abstract void DeleteImpl(IRegexRule rule);
    protected abstract void UndoImpl(IRegexRule rule);
}

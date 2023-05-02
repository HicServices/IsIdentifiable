using IsIdentifiable.Rules;
using Terminal.Gui.Trees;

namespace ii.Views;

internal class DuplicateRulesNode : TreeNode
{
    private IRegexRule[] Rules { get; }

    public DuplicateRulesNode(string pattern, IRegexRule[] rules)
    {
        Rules = rules;

        base.Text = $"{pattern} ({Rules.Length})";
    }

}
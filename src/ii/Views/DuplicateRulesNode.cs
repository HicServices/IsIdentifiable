using IsIdentifiable.Rules;
using Terminal.Gui.Trees;

namespace ii.Views;

internal class DuplicateRulesNode : TreeNode
{
    private RegexRule[] Rules { get; }

    public DuplicateRulesNode(string pattern, RegexRule[] rules)
    {
        Rules = rules;

        base.Text = $"{pattern} ({Rules.Length})";
    }

}

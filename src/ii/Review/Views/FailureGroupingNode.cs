using System.Linq;
using Terminal.Gui.Trees;

namespace ii.Review.Views;

internal class FailureGroupingNode : TreeNodeWithCount
{
    public string Group { get; }
    public OutstandingFailureNode[] Failures { get; }

    public FailureGroupingNode(string group, OutstandingFailureNode[] failures) : base(group)
    {
        Group = group;
        Failures = failures;

        base.Children = failures.OrderByDescending(f => f.NumberOfTimesReported).Cast<ITreeNode>().ToList();
    }
}

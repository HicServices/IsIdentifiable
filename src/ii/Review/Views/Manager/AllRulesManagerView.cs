using IsIdentifiable.Options;
using IsIdentifiable.Redacting;
using IsIdentifiable.Rules;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace ii.Review.Views.Manager;

/// <summary>
/// View allowing editing and viewing of all rules for both IsIdentifiable and IsIdentifiableReviewer
/// </summary>
class AllRulesManagerView : View, ITreeBuilder<object>
{
    private const string Analyser = "Analyser Rules";
    private const string Reviewer = "Reviewer Rules";
    private readonly ResourceScannerBaseOptions? _analyserOpts;
    private readonly RuleDetailView _detailView;
    private readonly TreeView<object> _treeView;
    private readonly IFileSystem _fileSystem;

    public AllRulesManagerView(ResourceScannerBaseOptions? analyserOpts, IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;

        Width = Dim.Fill();
        Height = Dim.Fill();

        _analyserOpts = analyserOpts;

        _treeView = new TreeView<object>(this)
        {
            AspectGetter = NodeAspectGetter,
            Width = Dim.Percent(50),
            Height = Dim.Fill()
        };
        _treeView.AddObject(Analyser);
        _treeView.AddObject(Reviewer);
        base.Add(_treeView);

        _detailView = new RuleDetailView()
        {
            X = Pos.Right(_treeView),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        base.Add(_detailView);

        _treeView.SelectionChanged += Tv_SelectionChanged;
        _treeView.ObjectActivated += Tv_ObjectActivated;
        _treeView.KeyPress += Tv_KeyPress;
    }

    /// <summary>
    /// Rebuilds the tree and refreshes rules to match the current state of rules on disk
    /// </summary>
    /// <returns></returns>
    public void RebuildTree()
    {
        _treeView.RebuildTree();
    }

    private void Tv_KeyPress(KeyEventEventArgs obj)
    {
        try
        {
            if (obj.KeyEvent.Key != Key.DeleteChar) return;
            var allSelected = _treeView.GetAllSelectedObjects().ToArray();

            // Proceed only if all the things selected are rules
            if (!allSelected.All(s => s is IAppliableRule)) return;
            // and the unique parents among them
            var parents = allSelected.Select(r => _treeView.GetParent(r)).Distinct().ToArray();
            if (parents.Length != 1) return;

            //is only 1 and it is an OutBase (rules file)
            // then it is a Reviewer rule being deleted
            if (parents[0] is OutBase outBase)
            {
                if (MessageBox.Query("Delete Rules", $"Delete {allSelected.Length} rules?", "Yes", "No") != 0)
                    return;
                foreach (var r in allSelected.Cast<RegexRule>())
                {
                    // remove the rules
                    outBase.Rules.Remove(r);
                }

                // and save;
                outBase.Save();
                _treeView.RefreshObject(outBase);
            }

            //is only 1 and it is an Analyser rule under a RuleTypeNode
            if (parents[0] is RuleTypeNode ruleTypeNode)
            {
                if (ruleTypeNode.Rules == null)
                    throw new Exception("RuleTypeNode did not contain any rules, how are you deleting a node!?");

                foreach (var rule in allSelected.Cast<IAppliableRule>()) ruleTypeNode.Rules.Remove(rule);

                ruleTypeNode.Parent.Save();
                _treeView.RefreshObject(ruleTypeNode);
            }
        }
        catch (Exception ex)
        {
            Helpers.ShowException("Failed to delete", ex);
        }
    }

    private void Tv_ObjectActivated(ObjectActivatedEventArgs<object> obj)
    {
        if (obj.ActivatedObject is Exception ex)
        {
            Helpers.ShowException("Exception Details", ex);
        }
    }

    private void Tv_SelectionChanged(object? sender, SelectionChangedEventArgs<object> e)
    {
        if (e.NewValue is IAppliableRule r)
        {
            _detailView.SetupFor(r);
        }
        if (e.NewValue is OutBase rulesFile)
        {
            _detailView.SetupFor(rulesFile, rulesFile.RulesFile);
        }

        if (e.NewValue is RuleSetFileNode rsf)
        {
            _detailView.SetupFor(rsf, rsf.File);
        }
    }

    private string NodeAspectGetter(object toRender)
    {
        if (toRender is RegexRule basicrule)
        {
            return basicrule.IfPattern;
        }

        if (toRender is SocketRule socketRule)
        {
            return $"{socketRule.Host}:{socketRule.Port}";
        }
        if (toRender is AllowlistRule ignoreRule)
        {
            return ignoreRule.IfPattern ?? ignoreRule.IfPartPattern;
        }

        if (toRender is OutBase outBase)
        {
            return outBase.RulesFile.Name;
        }

        return toRender.ToString() ?? "";
    }

    public bool SupportsCanExpand => true;

    public bool CanExpand(object toExpand)
    {
        // These are the things that cannot be expanded upon
        return toExpand is not (Exception or IAppliableRule);
        //everything else can be expanded
    }

    public IEnumerable<object> GetChildren(object forObject)
    {
        try
        {
            return GetChildrenImpl(forObject).ToArray();
        }
        catch (Exception ex)
        {
            // if there is an error getting children e.g. file doesn't exist, put the
            // Exception object directly into the tree
            return new object[] { ex };
        }
    }

    private IEnumerable<object> GetChildrenImpl(object forObject)
    {
        if (ReferenceEquals(forObject, Analyser))
        {
            if (!string.IsNullOrWhiteSpace(_analyserOpts?.RulesDirectory))
            {
                foreach (var f in _fileSystem.Directory.GetFiles(_analyserOpts.RulesDirectory, "*.yaml"))
                {
                    yield return new RuleSetFileNode(_fileSystem.FileInfo.New(f));
                }
            }

            if (!string.IsNullOrWhiteSpace(_analyserOpts?.RulesFile))
            {
                var file = _fileSystem.FileInfo.New(_analyserOpts.RulesFile);

                if (file.Exists)
                {
                    yield return new RuleSetFileNode(file);
                }
            }
        }
        if (ReferenceEquals(forObject, Reviewer))
        {
            // TODO(rkm 2023-07-06) Replace this
            //if (!string.IsNullOrWhiteSpace(_reviewerOpts.Reportlist))
            //{
            //    yield return new RowUpdater(_fileSystem, _fileSystem.FileInfo.New(_reviewerOpts.Reportlist));
            //}
            //if (!string.IsNullOrWhiteSpace(_reviewerOpts.IgnoreList))
            //{
            //    yield return new IgnoreRuleGenerator(_fileSystem, _fileSystem.FileInfo.New(_reviewerOpts.IgnoreList));
            //}
        }

        if (forObject is RuleSetFileNode ruleSet)
        {

            yield return new RuleTypeNode(ruleSet, nameof(RuleSet.BasicRules));
            yield return new RuleTypeNode(ruleSet, nameof(RuleSet.SocketRules));
            yield return new RuleTypeNode(ruleSet, nameof(RuleSet.AllowlistRules));
            yield return new RuleTypeNode(ruleSet, nameof(RuleSet.ConsensusRules));
        }

        if (forObject is RuleTypeNode ruleType && ruleType.Rules != null)
        {
            foreach (var r in ruleType.Rules)
            {
                yield return r;
            }
        }

        if (forObject is OutBase outBase)
        {
            foreach (var r in outBase.Rules)
            {
                yield return r;
            }
        }
    }
}

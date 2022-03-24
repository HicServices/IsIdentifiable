﻿using IsIdentifiable.Rules;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace IsIdentifiable.Views;

internal class DuplicateRulesNode : TreeNode
{
    public IsIdentifiableRule[] Rules { get; }

    public DuplicateRulesNode(string pattern, IsIdentifiableRule[] rules)
    {
        Rules = rules;

        Text = $"{pattern} ({Rules.Length})";
    }

}
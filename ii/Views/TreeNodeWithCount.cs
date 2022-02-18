﻿using Terminal.Gui;
using Terminal.Gui.Trees;

namespace IsIdentifiable.Views
{
    internal class TreeNodeWithCount : TreeNode
    {
        public string Heading { get; }

        public TreeNodeWithCount(string heading)
        {
            Heading = heading;
        }

        public override string ToString()
        {
            return Heading + $" ({Children.Count:N0})";
        }
    }
}
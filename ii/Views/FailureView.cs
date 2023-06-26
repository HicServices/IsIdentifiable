using System;
using System.Collections.Generic;
using System.Linq;
using IsIdentifiable.Failures;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace ii.Views;

class FailureView : View
{
    Attribute _attNormal;
    Attribute _attHighlight;

    public Failure? CurrentFailure { get; set; }


    public FailureView()
    {
        _attNormal = Attribute.Make(Color.Gray, Color.Black);
        _attHighlight = Attribute.Make(Color.BrightRed, Color.Black);
    }

    public override void Redraw(Rect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        var toDisplay = CurrentFailure?.ProblemValue ?? " ";

        //if the original string validated 
        var originalNewlines = new HashSet<int>();

        for (var i = 0; i < toDisplay.Length; i++)
            if (toDisplay[i] == '\n')
                originalNewlines.Add(i);

        var lines = ViewHelpers.Wrap(toDisplay, bounds.Width).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var characterOffset = 0;
        Attribute? oldColor = null;

        for (var y = 0; y < h; y++)
        {
            var currentLine = lines.Length > y ? lines[y] : null;

            for (var x = 0; x < w; x++)
            {
                Attribute newColor;
                char symbol;

                if (currentLine == null || x + 1 > currentLine.Length)
                {
                    newColor = _attNormal;
                    symbol = ' ';
                }
                else
                {
                    newColor = CurrentFailure?.Parts.Any(p => p.Includes(characterOffset))==true
                        ? _attHighlight
                        : _attNormal;

                    symbol = currentLine[x];
                    characterOffset++;

                    //we dropped a \n in our split so have to compensate for that
                    if (originalNewlines.Contains(characterOffset))
                        characterOffset++;
                }

                if (newColor != oldColor)
                {
                    Driver.SetAttribute(newColor);
                    oldColor = newColor;
                }

                AddRune(x, y, symbol);
            }
        }

        if (CurrentFailure != null)
        {
            Driver.SetAttribute(_attNormal);
            Move(0, h);

            var classification =
                $"C:{string.Join(",", CurrentFailure.Parts.Select(p => p.Classification).Distinct().ToArray())}";

            var field = CurrentFailure.ProblemField;
            classification = classification.PadRight(w - field.Length);

            Driver.AddStr(classification + field);
        }

    }
}

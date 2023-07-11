using System;
using Terminal.Gui;

namespace ii.Review.Views;

internal class SpinnerView : View
{
    int _stage;

    public SpinnerView()
    {
        Width = 1;
        Height = 1;
        base.CanFocus = false;

        Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(0.25), Tick);
    }

    private bool Tick(MainLoop arg)
    {
        if (Visible)
        {
            _stage = (_stage + 1) % 4;
            SetNeedsDisplay();
        }

        return true;
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);

        Move(0, 0);

        var rune = _stage switch
        {
            0 => Driver.VLine,
            1 => '/',
            2 => Driver.HLine,
            3 => '\\',
            _ => throw new ArgumentOutOfRangeException(nameof(_stage))
        };

        AddRune(0, 0, rune);
    }
}

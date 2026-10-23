using Avalonia;
using Avalonia.Controls;

namespace Tempest.Desktop.Views;

/// <summary>
/// Two children: the main one (the inputs) and a fixed-width side panel (the
/// reference diagram). At or above <c>minimumBeside</c> the panel sits beside
/// the main child; below it (a laptop window) it stacks above, so it is seen
/// without scrolling and never squeezes the value boxes (`PO-2`, board finding
/// F1). The main child stays first, so the tab order runs through it whichever
/// way the panel is placed. Moved out of the retired legacy calculators view
/// for the worksheet form (ADR-0167, D-41).
/// </summary>
public sealed class BesideOrAbovePanel(double minimumBeside, double gap) : Panel
{
    /// <summary>Whether a width puts the side panel beside the main child.</summary>
    public bool Beside(double width) => double.IsInfinity(width) || width >= minimumBeside;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2)
            return base.MeasureOverride(availableSize);

        var (main, side) = (Children[0], Children[1]);

        if (!side.IsVisible)
        {
            main.Measure(availableSize);
            return main.DesiredSize;
        }

        if (Beside(availableSize.Width))
        {
            side.Measure(availableSize);
            var mainWidth = Math.Max(0, availableSize.Width - side.DesiredSize.Width - gap);
            main.Measure(availableSize.WithWidth(mainWidth));
            return new Size(
                double.IsInfinity(availableSize.Width) ? main.DesiredSize.Width + gap + side.DesiredSize.Width : availableSize.Width,
                Math.Max(main.DesiredSize.Height, side.DesiredSize.Height));
        }

        main.Measure(availableSize);
        side.Measure(availableSize);
        return new Size(Math.Max(main.DesiredSize.Width, side.DesiredSize.Width), main.DesiredSize.Height + gap + side.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 2)
            return base.ArrangeOverride(finalSize);

        var (main, side) = (Children[0], Children[1]);

        if (!side.IsVisible)
        {
            main.Arrange(new Rect(finalSize));
            return finalSize;
        }

        var sideWidth = Math.Min(side.DesiredSize.Width, finalSize.Width);

        if (Beside(finalSize.Width))
        {
            main.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width - sideWidth - gap), finalSize.Height));
            side.Arrange(new Rect(finalSize.Width - sideWidth, 0, sideWidth, side.DesiredSize.Height));
        }
        else
        {
            side.Arrange(new Rect(0, 0, sideWidth, side.DesiredSize.Height));
            main.Arrange(new Rect(0, side.DesiredSize.Height + gap, finalSize.Width, main.DesiredSize.Height));
        }

        return finalSize;
    }
}

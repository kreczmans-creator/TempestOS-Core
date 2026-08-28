using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Tempest.Companion.Branding;

/// <summary>
/// The TempestOS mark — the six-blade shutter/iris — drawn as vector
/// geometry in code, the first concrete realisation of the platform's
/// logo (`WP 14.0A`; the `WP 10.0A` Visual Design System deferred all
/// icon assets to an implementation phase, and the repository carries no
/// image assets). Pure geometry, no bitmap: the same
/// hand-authored-<see cref="StreamGeometry"/> approach
/// <c>Tempest.Desktop.Icons.IconGeometry</c> already established, so the
/// mark renders crisply at any size and inherits whatever
/// <see cref="Foreground"/> its host sets.
/// </summary>
/// <remarks>
/// Six identical trapezoidal blades rotated at 60° steps around the
/// centre, leaving a hexagonal aperture — a camera-shutter iris read. The
/// blade is authored once in a 100×100 design space and replayed under
/// six rotation transforms at render time.
/// </remarks>
public sealed class TempestLogoControl : Control
{
    /// <summary>Defines the <see cref="Foreground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<TempestLogoControl, IBrush?>(nameof(Foreground), Brushes.White);

    /// <summary>Gets or sets the brush the blades are filled with.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private static readonly Geometry Blade = BuildBlade();

    static TempestLogoControl()
    {
        AffectsRender<TempestLogoControl>(ForegroundProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Square, sized by whichever constraint binds; a sensible default
        // when unconstrained.
        var side = Math.Min(
            double.IsInfinity(availableSize.Width) ? 28 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 28 : availableSize.Height);

        return new Size(side, side);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 100.0;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);

        for (var blade = 0; blade < 6; blade++)
        {
            var transform =
                Matrix.CreateTranslation(-50, -50) *
                Matrix.CreateRotation(Math.PI / 3 * blade) *
                Matrix.CreateScale(scale, scale) *
                Matrix.CreateTranslation(centre.X, centre.Y);

            using (context.PushTransform(transform))
                context.DrawGeometry(brush, null, Blade);
        }
    }

    private static StreamGeometry BuildBlade()
    {
        // One shutter blade, in a 100×100 design space centred on (50,50):
        // a swept quadrilateral from the outer rim toward the hexagonal
        // aperture, offset so six rotated copies interleave like a camera
        // iris rather than a starburst.
        var geometry = new StreamGeometry();

        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(50, 2), isFilled: true);
        ctx.LineTo(new Point(91, 26));
        ctx.LineTo(new Point(66, 41));
        ctx.LineTo(new Point(50, 32));
        ctx.EndFigure(isClosed: true);

        return geometry;
    }
}

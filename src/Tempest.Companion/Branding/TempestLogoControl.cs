using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Branding;

/// <summary>
/// The Tempest mark — the brand pack's own three-layer, eighteen-stroke
/// rotor with its paper hexagonal core — rendered from
/// <see cref="TempestMarkGeometry"/>'s verbatim artwork coordinates
/// (`WP 14.1A`; supersedes `WP 14.0A`'s incorrect code-drawn iris).
/// Full-colour by default (indigo/cyan/violet, the mark's own layer
/// colours); set <see cref="Monochrome"/> to render every stroke in one
/// brush for a mono context, the pack's own mono-variant treatment.
/// </summary>
public sealed class TempestLogoControl : Control
{
    /// <summary>Defines the <see cref="Monochrome"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MonochromeProperty =
        AvaloniaProperty.Register<TempestLogoControl, IBrush?>(nameof(Monochrome));

    /// <summary>Defines the <see cref="CoreBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CoreBrushProperty =
        AvaloniaProperty.Register<TempestLogoControl, IBrush?>(nameof(CoreBrush), new SolidColorBrush(BrandPalette.Paper050));

    /// <summary>Gets or sets a single brush overriding every layer colour — <see langword="null"/> (the default) renders the mark's own full-colour layers.</summary>
    public IBrush? Monochrome
    {
        get => GetValue(MonochromeProperty);
        set => SetValue(MonochromeProperty, value);
    }

    /// <summary>Gets or sets the core's fill/stroke brush — paper, per the artwork, by default.</summary>
    public IBrush? CoreBrush
    {
        get => GetValue(CoreBrushProperty);
        set => SetValue(CoreBrushProperty, value);
    }

    private static readonly IBrush IndigoBrush = new SolidColorBrush(BrandPalette.Indigo600);
    private static readonly IBrush CyanBrush = new SolidColorBrush(BrandPalette.Cyan500);
    private static readonly IBrush VioletBrush = new SolidColorBrush(BrandPalette.Violet500);

    static TempestLogoControl()
    {
        AffectsRender<TempestLogoControl>(MonochromeProperty, CoreBrushProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Math.Min(
            double.IsInfinity(availableSize.Width) ? 28 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 28 : availableSize.Height);

        return new Size(side, side);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / TempestMarkGeometry.MarkDesignSize;
        var offset = new Point(
            (Bounds.Width - TempestMarkGeometry.MarkDesignSize * scale) / 2,
            (Bounds.Height - TempestMarkGeometry.MarkDesignSize * scale) / 2);

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
            RenderMark(context, this);
    }

    /// <summary>Draws the mark into <paramref name="context"/> in the artwork's own 1024×1024 space — shared with <see cref="TempestLockupControl"/> so the geometry is rendered by exactly one code path.</summary>
    internal static void RenderMark(DrawingContext context, TempestLogoControl? colourSource)
    {
        var mono = colourSource?.Monochrome;

        foreach (var stroke in TempestMarkGeometry.Strokes)
        {
            var brush = mono ?? stroke.Layer switch
            {
                MarkLayer.Indigo => IndigoBrush,
                MarkLayer.Cyan => CyanBrush,
                _ => VioletBrush,
            };

            var pen = new Pen(brush, TempestMarkGeometry.MarkStrokeWidth, lineCap: PenLineCap.Round);
            context.DrawLine(pen, stroke.Start, stroke.End);
        }

        var core = colourSource?.CoreBrush ?? new SolidColorBrush(BrandPalette.Paper050);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(TempestMarkGeometry.CorePoints[0], isFilled: true);
            for (var i = 1; i < TempestMarkGeometry.CorePoints.Length; i++)
                ctx.LineTo(TempestMarkGeometry.CorePoints[i]);
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(core, new Pen(core, TempestMarkGeometry.CoreStrokeWidth, lineJoin: PenLineJoin.Round), geometry);
    }
}

/// <summary>
/// The TEMPEST OS horizontal lockup — the brand pack's own artwork
/// (`logo-os-horizontal-transparent-*`), rendered from
/// <see cref="TempestMarkGeometry"/>'s verbatim coordinates: the mark at
/// the left, the TEMPEST logotype (paper on dark grounds, ink on paper
/// grounds — the pack's light/ink variants), and OS always in brand
/// cyan.
/// </summary>
public sealed class TempestLockupControl : Control
{
    private static readonly Geometry TempestGeometry = Geometry.Parse(TempestMarkGeometry.TempestPathData);
    private static readonly Geometry OsGeometry = Geometry.Parse(TempestMarkGeometry.OsPathData);
    private static readonly IBrush CyanBrush = new SolidColorBrush(BrandPalette.Cyan500);

    /// <summary>Defines the <see cref="WordmarkBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> WordmarkBrushProperty =
        AvaloniaProperty.Register<TempestLockupControl, IBrush?>(nameof(WordmarkBrush), new SolidColorBrush(BrandPalette.Paper050));

    /// <summary>Gets or sets the TEMPEST logotype brush — paper for dark grounds (the pack's light variant), ink for paper grounds (the ink variant).</summary>
    public IBrush? WordmarkBrush
    {
        get => GetValue(WordmarkBrushProperty);
        set => SetValue(WordmarkBrushProperty, value);
    }

    static TempestLockupControl()
    {
        AffectsRender<TempestLockupControl>(WordmarkBrushProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var design = TempestMarkGeometry.LockupDesignSize;
        var height = double.IsInfinity(availableSize.Height) ? 24 : availableSize.Height;

        if (!double.IsInfinity(availableSize.Width) && availableSize.Width / design.Width < height / design.Height)
            height = availableSize.Width / design.Width * design.Height;

        return new Size(height / design.Height * design.Width, height);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var design = TempestMarkGeometry.LockupDesignSize;
        var scale = Math.Min(Bounds.Width / design.Width, Bounds.Height / design.Height);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            // The mark, at the lockup's own transform: translate(0,10) scale(100/1024).
            var markScale = TempestMarkGeometry.LockupMarkScale;
            using (context.PushTransform(Matrix.CreateScale(markScale, markScale) * Matrix.CreateTranslation(0, TempestMarkGeometry.LockupMarkOffsetY)))
                TempestLogoControl.RenderMark(context, null);

            // The logotype, at translate(134, 83.8); OS at a further translate(332.6, 0).
            var text = TempestMarkGeometry.LockupTextOffset;
            using (context.PushTransform(Matrix.CreateTranslation(text.X, text.Y)))
            {
                context.DrawGeometry(WordmarkBrush ?? new SolidColorBrush(BrandPalette.Paper050), null, TempestGeometry);

                using (context.PushTransform(Matrix.CreateTranslation(TempestMarkGeometry.LockupOsOffsetX, 0)))
                    context.DrawGeometry(CyanBrush, null, OsGeometry);
            }
        }
    }
}

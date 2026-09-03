namespace Tempest.Desktop.Branding;

using Avalonia;

/// <summary>Which of the mark's three stroke layers a line belongs to — indigo (outer), cyan (middle), violet (inner), in the brand pack's own stroke order.</summary>
public enum MarkLayer
{
    /// <summary>The outer strokes — brand indigo.</summary>
    Indigo,

    /// <summary>The middle strokes — brand cyan.</summary>
    Cyan,

    /// <summary>The inner strokes — brand violet.</summary>
    Violet,
}

/// <summary>One stroke of the mark, in the artwork's own 1024×1024 space.</summary>
/// <param name="Start">The stroke's start point.</param>
/// <param name="End">The stroke's end point.</param>
/// <param name="Layer">The stroke's colour layer.</param>
public sealed record MarkStroke(Point Start, Point End, MarkLayer Layer);

/// <summary>
/// The Tempest mark and TEMPEST OS logotype geometry — transcribed
/// VERBATIM from the brand pack's own vector artwork
/// (`assets/logo/derived/logo-os-horizontal-transparent-*.svg`,
/// `WP 14.1A`), never redrawn: eighteen 24-unit round-capped strokes in
/// three colour layers around a paper hexagonal core, plus the exact
/// logotype outline paths. The pack's rule — "never rebuild the logo
/// mark in code" — is honoured by carrying the supplied coordinates
/// unchanged; this file is a transcription of the asset, not a redesign
/// of it.
/// </summary>
public static class TempestMarkGeometry
{
    /// <summary>The mark's design-space size (the artwork's own 1024×1024 viewBox).</summary>
    public const double MarkDesignSize = 1024;

    /// <summary>The mark's stroke width in design space (the artwork's own 24).</summary>
    public const double MarkStrokeWidth = 24;

    /// <summary>Every stroke of the mark, in the artwork's own order.</summary>
    public static readonly IReadOnlyList<MarkStroke> Strokes =
    [
        new(new Point(558.5, 569.5), new Point(885.5, 569.5), MarkLayer.Indigo),
        new(new Point(485.45, 581.02), new Point(648.95, 864.21), MarkLayer.Indigo),
        new(new Point(438.95, 523.52), new Point(275.45, 806.71), MarkLayer.Indigo),
        new(new Point(465.5, 454.5), new Point(138.5, 454.5), MarkLayer.Indigo),
        new(new Point(538.55, 442.98), new Point(375.05, 159.79), MarkLayer.Indigo),
        new(new Point(585.05, 500.48), new Point(748.55, 217.29), MarkLayer.Indigo),
        new(new Point(580.5, 634.5), new Point(830, 634.5), MarkLayer.Cyan),
        new(new Point(440.16, 632.57), new Point(564.91, 848.65), MarkLayer.Cyan),
        new(new Point(371.66, 510.07), new Point(246.91, 726.15), MarkLayer.Cyan),
        new(new Point(443.5, 389.5), new Point(194, 389.5), MarkLayer.Cyan),
        new(new Point(583.84, 391.43), new Point(459.09, 175.35), MarkLayer.Cyan),
        new(new Point(652.34, 513.93), new Point(777.09, 297.85), MarkLayer.Cyan),
        new(new Point(624, 698), new Point(781, 698), MarkLayer.Violet),
        new(new Point(406.92, 701.99), new Point(485.42, 837.96), MarkLayer.Violet),
        new(new Point(294.92, 515.99), new Point(216.42, 651.96), MarkLayer.Violet),
        new(new Point(400, 326), new Point(243, 326), MarkLayer.Violet),
        new(new Point(617.08, 322.01), new Point(538.58, 186.04), MarkLayer.Violet),
        new(new Point(729.08, 508.01), new Point(807.58, 372.04), MarkLayer.Violet),
    ];

    /// <summary>The hexagonal core, filled paper with a 20-unit round-joined stroke (the artwork's own values).</summary>
    public static readonly Point[] CorePoints = [new Point(546.5, 512), new Point(529.25, 541.88), new Point(494.75, 541.88), new Point(477.5, 512), new Point(494.75, 482.12), new Point(529.25, 482.12)];

    /// <summary>The core's stroke width in design space.</summary>
    public const double CoreStrokeWidth = 20;

    /// <summary>The lockup's design-space size (the artwork's own 559×120 viewBox).</summary>
    public static readonly Size LockupDesignSize = new(559, 120);

    /// <summary>The mark's transform inside the lockup: translate(0,10) scale(100/1024).</summary>
    public const double LockupMarkScale = 0.09765625;

    /// <summary>The mark's Y offset inside the lockup.</summary>
    public const double LockupMarkOffsetY = 10;

    /// <summary>The logotype group's offset inside the lockup: translate(134, 83.8).</summary>
    public static readonly Point LockupTextOffset = new(134, 83.8);

    /// <summary>The "OS" path's X offset within the logotype group: translate(332.6, 0).</summary>
    public const double LockupOsOffsetX = 332.6;

    /// <summary>The "TEMPEST" logotype outline — the artwork's own path data, verbatim. Rendered paper on dark grounds, ink on paper grounds.</summary>
    public const string TempestPathData = "M14.78 -38.61L0.99 -38.61L0.99 -46.20L37.55 -46.20L37.55 -38.61L23.76 -38.61L23.76 0.00L14.78 0.00L14.78 -38.61ZM44.83 -46.20L78.23 -46.20L78.23 -38.61L53.81 -38.61L53.81 -26.93L76.32 -26.93L76.32 -19.47L53.81 -19.47L53.81 -7.59L78.23 -7.59L78.23 0.00L44.83 0.00L44.83 -46.20ZM86.83 -46.20L95.15 -46.20L108.87 -16.37L109.01 -16.37L122.80 -46.20L131.12 -46.20L131.12 0.00L122.47 0.00L122.47 -28.12L122.34 -28.12L111.71 -6.47L106.17 -6.47L95.61 -28.12L95.48 -28.12L95.48 0.00L86.83 0.00L86.83 -46.20ZM141.70 -46.20L170.67 -46.20L177.73 -39.07L177.73 -22.90L170.60 -15.71L150.67 -15.71L150.67 0.00L141.70 0.00L141.70 -46.20ZM166.58 -23.23L168.89 -25.54L168.89 -36.37L166.58 -38.68L150.67 -38.68L150.67 -23.23L166.58 -23.23ZM186.33 -46.20L219.73 -46.20L219.73 -38.61L195.31 -38.61L195.31 -26.93L217.81 -26.93L217.81 -19.47L195.31 -19.47L195.31 -7.59L219.73 -7.59L219.73 0.00L186.33 0.00L186.33 -46.20ZM227.34 -7.13L227.34 -13.73L236.18 -13.73L236.18 -9.83L238.36 -7.66L252.02 -7.66L254.27 -9.90L254.27 -17.56L252.09 -19.73L234.60 -19.73L227.47 -26.86L227.47 -39.07L234.60 -46.20L255.45 -46.20L262.58 -39.07L262.58 -32.41L253.74 -32.41L253.74 -36.37L251.56 -38.54L238.49 -38.54L236.31 -36.37L236.31 -29.57L238.49 -27.39L255.98 -27.39L263.11 -20.26L263.11 -7.26L255.85 0.00L234.47 0.00L227.34 -7.13ZM282.86 -38.61L269.07 -38.61L269.07 -46.20L305.63 -46.20L305.63 -38.61L291.84 -38.61L291.84 0.00L282.86 0.00L282.86 -38.61Z";

    /// <summary>The "OS" logotype outline — the artwork's own path data, verbatim. Always brand cyan.</summary>
    public const string OsPathData = "M3.63 -7.59L3.63 -38.61L11.22 -46.20L34.98 -46.20L42.57 -38.61L42.57 -7.59L34.98 0.00L11.22 0.00L3.63 -7.59ZM30.23 -7.66L33.59 -11.02L33.59 -35.18L30.23 -38.54L15.97 -38.54L12.61 -35.18L12.61 -11.02L15.97 -7.66L30.23 -7.66ZM51.50 -7.13L51.50 -13.73L60.34 -13.73L60.34 -9.83L62.52 -7.66L76.18 -7.66L78.43 -9.90L78.43 -17.56L76.25 -19.73L58.76 -19.73L51.63 -26.86L51.63 -39.07L58.76 -46.20L79.62 -46.20L86.74 -39.07L86.74 -32.41L77.90 -32.41L77.90 -36.37L75.72 -38.54L62.65 -38.54L60.48 -36.37L60.48 -29.57L62.65 -27.39L80.14 -27.39L87.27 -20.26L87.27 -7.26L80.01 0.00L58.63 0.00L51.50 -7.13Z";
}

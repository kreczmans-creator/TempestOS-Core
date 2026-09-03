using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

using Avalonia.Media;

namespace Tempest.Desktop.Icons;

/// <summary>
/// The platform's own hand-authored vector icon set (`WP 10.5A`,
/// extended at the Desktop brand alignment from four chrome glyphs to
/// the full set the shell needs) — <see cref="StreamGeometry"/> path
/// data for every interactive chrome glyph: window/panel chrome, the
/// Ribbon's verb groups, the navigation rail's modules, the Cockpit's
/// regions and the Quick Access Toolbar's actions.
/// </summary>
/// <remarks>
/// <para>
/// Every geometry is stroke-only (no fill), authored in a 24×24 box,
/// rendered by <see cref="Build"/> via an ordinary <see cref="Path"/>
/// whose own <c>Stroke</c> is bound to the host control's inherited
/// <c>Foreground</c> — automatically theme-tinted, exactly like a text
/// glyph, never a fixed colour. This is what lets the shell drop the
/// mixed-style colour emoji the design system forbids (its own rule: no
/// emoji, ever; status is a coloured dot, a badge, or a label) without
/// giving up icons altogether: one weight, one style, one colour.
/// </para>
/// <para>
/// <see cref="IconRegistry"/>'s own Kind glyphs (the Geometric Shapes
/// text symbols) remain the object iconography — this set is the
/// <em>chrome</em>, and the two deliberately never overlap.
/// </para>
/// </remarks>
internal static class IconGeometry
{
    // ---- Chrome (`WP 10.5A`) -----------------------------------------

    /// <summary>A simple "X" — dismiss/close.</summary>
    public static readonly StreamGeometry Close = StreamGeometry.Parse("M5,5 L19,19 M19,5 L5,19");

    /// <summary>A simple checkmark — acknowledge/confirm/success.</summary>
    public static readonly StreamGeometry Check = StreamGeometry.Parse("M4,12 L10,18 L20,6");

    /// <summary>A right-pointing chevron — collapsed/expand affordance.</summary>
    public static readonly StreamGeometry ChevronRight = StreamGeometry.Parse("M9,5 L16,12 L9,19");

    /// <summary>A down-pointing chevron — expanded/collapse affordance.</summary>
    public static readonly StreamGeometry ChevronDown = StreamGeometry.Parse("M5,9 L12,16 L19,9");

    /// <summary>A left-pointing chevron — collapse a panel toward its edge.</summary>
    public static readonly StreamGeometry ChevronLeft = StreamGeometry.Parse("M15,5 L8,12 L15,19");

    /// <summary>An up-pointing chevron.</summary>
    public static readonly StreamGeometry ChevronUp = StreamGeometry.Parse("M5,15 L12,8 L19,15");

    /// <summary>A pin — a pinned/dockable panel.</summary>
    public static readonly StreamGeometry Pin = StreamGeometry.Parse("M9,3 L15,3 L14,10 L17,13 L17,15 L7,15 L7,13 L10,10 Z M12,15 L12,21");

    /// <summary>A pin turned sideways — an auto-hidden panel.</summary>
    public static readonly StreamGeometry PinOff = StreamGeometry.Parse("M3,9 L3,15 L10,14 L13,17 L15,17 L15,7 L13,7 L10,10 Z M15,12 L21,12");

    /// <summary>A collapse-to-strip glyph — a panel folded to its edge.</summary>
    public static readonly StreamGeometry Collapse = StreamGeometry.Parse("M4,4 L4,20 M20,4 L20,20 M15,8 L11,12 L15,16 M11,12 L20,12");

    // ---- Command verbs (the Ribbon) -----------------------------------

    public static readonly StreamGeometry Plus = StreamGeometry.Parse("M12,5 L12,19 M5,12 L19,12");
    public static readonly StreamGeometry Pencil = StreamGeometry.Parse("M4,20 L8,19 L19,8 L16,5 L5,16 Z M14,7 L17,10");
    public static readonly StreamGeometry Edit = StreamGeometry.Parse("M4,6 L14,6 M4,12 L20,12 M4,18 L12,18 M17,3 L21,7 L15,13 L11,13 L11,9 Z");
    public static readonly StreamGeometry Trash = StreamGeometry.Parse("M4,7 L20,7 M9,7 L9,4 L15,4 L15,7 M6,7 L7,20 L17,20 L18,7 M10,11 L10,16 M14,11 L14,16");
    public static readonly StreamGeometry Move = StreamGeometry.Parse("M12,3 L12,21 M3,12 L21,12 M9,6 L12,3 L15,6 M9,18 L12,21 L15,18 M6,9 L3,12 L6,15 M18,9 L21,12 L18,15");
    public static readonly StreamGeometry Copy = StreamGeometry.Parse("M9,9 L20,9 L20,20 L9,20 Z M4,15 L4,4 L15,4");
    public static readonly StreamGeometry Duplicate = StreamGeometry.Parse("M8,8 L20,8 L20,20 L8,20 Z M4,16 L4,4 L16,4 M14,11 L14,17 M11,14 L17,14");
    public static readonly StreamGeometry Play = StreamGeometry.Parse("M7,4 L19,12 L7,20 Z");
    public static readonly StreamGeometry Refresh = StreamGeometry.Parse("M20,12 A8,8 0 1 1 17.5,6.2 M17.5,3 L17.5,6.5 L14,6.5");
    public static readonly StreamGeometry Lock = StreamGeometry.Parse("M6,11 L18,11 L18,21 L6,21 Z M8,11 L8,7 A4,4 0 0 1 16,7 L16,11");
    public static readonly StreamGeometry Unlock = StreamGeometry.Parse("M6,11 L18,11 L18,21 L6,21 Z M8,11 L8,7 A4,4 0 0 1 15.5,5.5");
    public static readonly StreamGeometry Eye = StreamGeometry.Parse("M2,12 C5,6 19,6 22,12 C19,18 5,18 2,12 Z M12,12 m-3,0 a3,3 0 1 0 6,0 a3,3 0 1 0 -6,0");
    public static readonly StreamGeometry CheckCircle = StreamGeometry.Parse("M12,12 m-9,0 a9,9 0 1 0 18,0 a9,9 0 1 0 -18,0 M8,12 L11,15 L16,9");
    public static readonly StreamGeometry Archive = StreamGeometry.Parse("M3,4 L21,4 L21,8 L3,8 Z M5,8 L5,20 L19,20 L19,8 M10,12 L14,12");
    public static readonly StreamGeometry Upload = StreamGeometry.Parse("M12,16 L12,4 M8,8 L12,4 L16,8 M4,16 L4,20 L20,20 L20,16");
    public static readonly StreamGeometry Paperclip = StreamGeometry.Parse("M16,6 L8,14 A3,3 0 0 0 12.2,18.2 L20,10.4 A5,5 0 0 0 12.9,3.3 L5,11.2");
    public static readonly StreamGeometry Link = StreamGeometry.Parse("M10,14 A4,4 0 0 0 15.6,14 L18.6,11 A4,4 0 0 0 13,5.4 L11.5,6.9 M14,10 A4,4 0 0 0 8.4,10 L5.4,13 A4,4 0 0 0 11,18.6 L12.5,17.1");
    public static readonly StreamGeometry Chart = StreamGeometry.Parse("M4,20 L20,20 M7,17 L7,11 M12,17 L12,6 M17,17 L17,13");
    public static readonly StreamGeometry Scales = StreamGeometry.Parse("M12,4 L12,20 M7,20 L17,20 M4,8 L20,8 M4,8 L2,14 L6,14 Z M20,8 L18,14 L22,14 Z");
    public static readonly StreamGeometry Sliders = StreamGeometry.Parse("M4,7 L20,7 M4,12 L20,12 M4,17 L20,17 M9,5 L9,9 M15,10 L15,14 M7,15 L7,19");
    public static readonly StreamGeometry Inbox = StreamGeometry.Parse("M3,13 L3,19 L21,19 L21,13 M3,13 L6,5 L18,5 L21,13 M3,13 L9,13 L10,16 L14,16 L15,13 L21,13");
    public static readonly StreamGeometry Layers = StreamGeometry.Parse("M12,3 L21,8 L12,13 L3,8 Z M3,12 L12,17 L21,12 M3,16 L12,21 L21,16");
    public static readonly StreamGeometry Dot = StreamGeometry.Parse("M12,12 m-2.5,0 a2.5,2.5 0 1 0 5,0 a2.5,2.5 0 1 0 -5,0");

    // ---- Shell actions ------------------------------------------------

    public static readonly StreamGeometry Search = StreamGeometry.Parse("M10.5,10.5 m-6.5,0 a6.5,6.5 0 1 0 13,0 a6.5,6.5 0 1 0 -13,0 M15.5,15.5 L21,21");
    public static readonly StreamGeometry Command = StreamGeometry.Parse("M5,7 L10,12 L5,17 M12,17 L19,17");
    public static readonly StreamGeometry Theme = StreamGeometry.Parse("M12,12 m-8,0 a8,8 0 1 0 16,0 a8,8 0 1 0 -16,0 M12,4 L12,20 M12,7 L17,12 L12,17");
    public static readonly StreamGeometry LayoutReset = StreamGeometry.Parse("M4,4 L20,4 L20,20 L4,20 Z M9,4 L9,20 M9,14 L20,14");
    public static readonly StreamGeometry Graph = StreamGeometry.Parse("M6,6 m-2.5,0 a2.5,2.5 0 1 0 5,0 a2.5,2.5 0 1 0 -5,0 M18,6 m-2.5,0 a2.5,2.5 0 1 0 5,0 a2.5,2.5 0 1 0 -5,0 M12,18 m-2.5,0 a2.5,2.5 0 1 0 5,0 a2.5,2.5 0 1 0 -5,0 M7.2,8.2 L10.8,15.8 M16.8,8.2 L13.2,15.8 M8.5,6 L15.5,6");
    public static readonly StreamGeometry Macro = StreamGeometry.Parse("M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M17,14 L17,20 M14,17 L20,17");
    public static readonly StreamGeometry Undo = StreamGeometry.Parse("M4,10 L9,5 M4,10 L9,15 M4,10 L15,10 A4,4 0 0 1 15,18 L10,18");
    public static readonly StreamGeometry Redo = StreamGeometry.Parse("M20,10 L15,5 M20,10 L15,15 M20,10 L9,10 A4,4 0 0 0 9,18 L14,18");
    public static readonly StreamGeometry Bell = StreamGeometry.Parse("M6,17 L18,17 L16.5,15 L16.5,10 A4.5,4.5 0 0 0 7.5,10 L7.5,15 Z M10,20 L14,20");
    public static readonly StreamGeometry Filter = StreamGeometry.Parse("M3,5 L21,5 L14,13 L14,20 L10,18 L10,13 Z");
    public static readonly StreamGeometry Clock = StreamGeometry.Parse("M12,12 m-9,0 a9,9 0 1 0 18,0 a9,9 0 1 0 -18,0 M12,7 L12,12 L15.5,14");
    public static readonly StreamGeometry Star = StreamGeometry.Parse("M12,3 L14.7,9 L21,9.6 L16.2,13.9 L17.6,20.2 L12,17 L6.4,20.2 L7.8,13.9 L3,9.6 L9.3,9 Z");
    public static readonly StreamGeometry User = StreamGeometry.Parse("M12,8 m-4,0 a4,4 0 1 0 8,0 a4,4 0 1 0 -8,0 M4,21 C4,15 20,15 20,21");

    // ---- Modules (the navigation rail) --------------------------------

    public static readonly StreamGeometry Home = StreamGeometry.Parse("M4,11 L12,4 L20,11 L20,20 L14,20 L14,15 L10,15 L10,20 L4,20 Z");
    public static readonly StreamGeometry Folder = StreamGeometry.Parse("M3,6 L9,6 L11,8 L21,8 L21,19 L3,19 Z");
    public static readonly StreamGeometry Gear = StreamGeometry.Parse("M12,12 m-3,0 a3,3 0 1 0 6,0 a3,3 0 1 0 -6,0 M12,2.5 L12,5.5 M12,18.5 L12,21.5 M2.5,12 L5.5,12 M18.5,12 L21.5,12 M5.3,5.3 L7.4,7.4 M16.6,16.6 L18.7,18.7 M5.3,18.7 L7.4,16.6 M16.6,7.4 L18.7,5.3");
    public static readonly StreamGeometry CheckSquare = StreamGeometry.Parse("M4,4 L20,4 L20,20 L4,20 Z M8,12 L11,15 L16,9");
    public static readonly StreamGeometry Currency = StreamGeometry.Parse("M12,12 m-9,0 a9,9 0 1 0 18,0 a9,9 0 1 0 -18,0 M15,9 A3,3 0 0 0 9,9 L9,16 L15,16 M7,12.5 L13,12.5");
    public static readonly StreamGeometry People = StreamGeometry.Parse("M9,9 m-3.5,0 a3.5,3.5 0 1 0 7,0 a3.5,3.5 0 1 0 -7,0 M2,20 C2,15 16,15 16,20 M17,10 m-2.5,0 a2.5,2.5 0 1 0 5,0 a2.5,2.5 0 1 0 -5,0 M17,15 C20,15 22,17 22,20");
    public static readonly StreamGeometry Book = StreamGeometry.Parse("M4,4 L11,4 L12,6 L13,4 L20,4 L20,19 L13,19 L12,21 L11,19 L4,19 Z M12,6 L12,21");
    public static readonly StreamGeometry Shield = StreamGeometry.Parse("M12,3 L20,6 L20,12 C20,17 16,20 12,21 C8,20 4,17 4,12 L4,6 Z");

    // ---- Cockpit regions and states -----------------------------------

    public static readonly StreamGeometry Grid = StreamGeometry.Parse("M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z");
    public static readonly StreamGeometry Warning = StreamGeometry.Parse("M12,4 L21,20 L3,20 Z M12,10 L12,14.5 M12,17 L12,17.6");
    public static readonly StreamGeometry Info = StreamGeometry.Parse("M12,12 m-9,0 a9,9 0 1 0 18,0 a9,9 0 1 0 -18,0 M12,11 L12,16 M12,8 L12,8.6");
    public static readonly StreamGeometry Blocked = StreamGeometry.Parse("M12,12 m-9,0 a9,9 0 1 0 18,0 a9,9 0 1 0 -18,0 M5.6,5.6 L18.4,18.4");
    public static readonly StreamGeometry Flag = StreamGeometry.Parse("M5,21 L5,4 L18,4 L15,8.5 L18,13 L5,13");
    public static readonly StreamGeometry Activity = StreamGeometry.Parse("M3,12 L7,12 L10,5 L14,19 L17,12 L21,12");
    public static readonly StreamGeometry Document = StreamGeometry.Parse("M6,3 L14,3 L19,8 L19,21 L6,21 Z M14,3 L14,8 L19,8");
    public static readonly StreamGeometry Decision = StreamGeometry.Parse("M12,3 L21,12 L12,21 L3,12 Z M12,8 L12,13 M12,16 L12,16.6");
    public static readonly StreamGeometry Monitor = StreamGeometry.Parse("M3,5 L21,5 L21,16 L3,16 Z M9,20 L15,20 M12,16 L12,20");
    public static readonly StreamGeometry Bolt = StreamGeometry.Parse("M13,2 L5,13 L11,13 L10,22 L19,10 L13,10 Z");
    public static readonly StreamGeometry Compass = StreamGeometry.Parse("M12,12 m-9,0 a9,9 0 1 0 18,0 a9,9 0 1 0 -18,0 M15.5,8.5 L13.5,13.5 L8.5,15.5 L10.5,10.5 Z");
    public static readonly StreamGeometry Calculator = StreamGeometry.Parse("M5,3 L19,3 L19,21 L5,21 Z M8,6 L16,6 L16,9 L8,9 Z M8,13 L8,13.6 M12,13 L12,13.6 M16,13 L16,13.6 M8,17 L8,17.6 M12,17 L12,17.6 M16,17 L16,17.6");
    public static readonly StreamGeometry Factory = StreamGeometry.Parse("M3,21 L3,10 L8,13 L8,10 L13,13 L13,10 L18,13 L18,4 L21,4 L21,21 Z");
    public static readonly StreamGeometry Requirement = StreamGeometry.Parse("M5,4 L19,4 L19,20 L5,20 Z M8,9 L16,9 M8,13 L16,13 M8,17 L13,17");

    /// <summary>
    /// Renders <paramref name="geometry"/> as a theme-tinted vector icon
    /// at <paramref name="size"/> device pixels: a <see cref="Path"/>
    /// inside a <see cref="Viewbox"/> so every icon shares the identical
    /// 24×24 optical box regardless of its own extents, stroked in
    /// <paramref name="brush"/> or, when <see langword="null"/>, in the
    /// host's inherited <c>Foreground</c>.
    /// </summary>
    public static Control Build(StreamGeometry geometry, double size = 16, IBrush? brush = null, double strokeThickness = 1.7)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = geometry,
            Width = 24,
            Height = 24,
            StrokeThickness = strokeThickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Fill = null,
        };

        if (brush is not null)
            path.Stroke = brush;
        else
            path[!Avalonia.Controls.Shapes.Shape.StrokeProperty] = path[!TextElement.ForegroundProperty];

        return new Viewbox
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Child = path,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
    }
}

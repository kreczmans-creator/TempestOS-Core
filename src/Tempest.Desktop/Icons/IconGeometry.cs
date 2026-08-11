using Avalonia.Media;

namespace Tempest.Desktop.Icons;

/// <summary>
/// The platform's own first real, hand-authored vector icon set
/// (`WP 10.5A`) — a small, curated set of <see cref="StreamGeometry"/>
/// path data for the interactive "chrome" glyphs the new feedback
/// controls need (close, acknowledge, expand/collapse), deliberately
/// small rather than attempting to vectorise every one of
/// <see cref="IconRegistry"/>'s own ~30 Kind glyphs in one pass.
/// </summary>
/// <remarks>
/// Every geometry is stroke-only (no fill), 24×24 viewbox, rendered via
/// an ordinary <see cref="Avalonia.Controls.Shapes.Path"/> whose own
/// <c>Stroke</c> is bound to the host control's inherited
/// <c>Foreground</c> — automatically theme-tinted, exactly like a text
/// glyph, never a fixed colour. A full, hand-authored replacement for
/// every <see cref="IconRegistry"/> Kind glyph remains disclosed future
/// work (`FCR-0069`'s own sibling scope, extended by this Work Package —
/// see `WP10.5A Future Capability` disclosure).
/// </remarks>
internal static class IconGeometry
{
    /// <summary>A simple "X" — dismiss/close.</summary>
    public static readonly StreamGeometry Close = StreamGeometry.Parse("M4,4 L20,20 M20,4 L4,20");

    /// <summary>A simple checkmark — acknowledge/confirm/success.</summary>
    public static readonly StreamGeometry Check = StreamGeometry.Parse("M4,12 L10,18 L20,6");

    /// <summary>A right-pointing chevron — collapsed/expand affordance.</summary>
    public static readonly StreamGeometry ChevronRight = StreamGeometry.Parse("M8,4 L16,12 L8,20");

    /// <summary>A down-pointing chevron — expanded/collapse affordance.</summary>
    public static readonly StreamGeometry ChevronDown = StreamGeometry.Parse("M4,8 L12,16 L20,8");
}

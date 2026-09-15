using Avalonia.Media;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// The markup layer's own colour choices (`TD-98`) — five swatches drawn
/// from the Tempest Engineering Design System's own brand palette
/// (<see cref="BrandPalette"/>), the same tokens every other coloured
/// surface in this platform draws from, rather than a colour picker
/// inventing its own.
/// </summary>
public static class AnnotationPalette
{
    /// <summary>One selectable annotation colour: its own hex and a short, real name for its automation/tooltip text.</summary>
    public readonly record struct Swatch(string Name, string Hex);

    /// <summary>The five swatches offered, in the order the Annotations toolbar group shows them.</summary>
    public static readonly IReadOnlyList<Swatch> Swatches =
    [
        new("Red", ToHex(BrandPalette.Red500)),
        new("Amber", ToHex(BrandPalette.Amber500)),
        new("Green", ToHex(BrandPalette.Green500)),
        new("Cyan", ToHex(BrandPalette.Cyan500)),
        new("Violet", ToHex(BrandPalette.Violet500)),
    ];

    /// <summary>The colour a freshly opened viewer starts with — the palette's own first swatch.</summary>
    public static string Default => Swatches[0].Hex;

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

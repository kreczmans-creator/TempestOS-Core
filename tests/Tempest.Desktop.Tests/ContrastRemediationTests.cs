using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Review board finding #6 (`WP 16.5A-R1`) — three real contrast
/// failures the board measured directly against source: `FaintTextBrushKey`
/// (light) on both real surfaces it is drawn on, the dark theme's Primary
/// button text on its own pressed fill, and the light theme's Danger
/// button text on its own hovered fill. This file recomputes every one of
/// those four pairs, in both themes, against the real registered
/// <see cref="BrandPalette"/> resources — not a hard-coded table — so a
/// future value change that regresses any of them fails here first.
/// </summary>
/// <remarks>
/// Every value below is read via <see cref="BrandPalette.Brush(string, ThemeVariant?)"/>'s
/// own explicit-<see cref="ThemeVariant"/> overload — the same
/// no-live-toggle approach <see cref="HealthColorContrastTests"/> already
/// established for this suite, avoiding the toggle-and-reread flakiness
/// <see cref="VisualPolishTests"/> found under this project's own
/// parallel test execution.
/// </remarks>
public sealed class ContrastRemediationTests
{
    /// <summary>WCAG 2.1 AA's own floor for normal-weight body text.</summary>
    private const double MinimumTextContrastRatio = 4.5;

    /// <summary>
    /// `FaintTextBrushKey` is real text — captions, disabled labels,
    /// separators (see its own doc comment in <see cref="BrandPalette"/>)
    /// — not decoration, so it owes WCAG AA's 4.5:1 floor on every real
    /// surface it is drawn on. The light theme's original `Slate500`
    /// measured only 3.72:1 on <see cref="BrandPalette.SurfaceBackgroundBrushKey"/>
    /// and 3.27:1 on <see cref="BrandPalette.SunkenBackgroundBrushKey"/>
    /// (both real backdrops this key is bound against — `StatusBarView`,
    /// `GlobalNavigationRail`, `RibbonView`, `PageHeading`, `CockpitView`,
    /// `ProjectExplorerView`, `LayoutTabGroupView`, `CockpitCardControl`).
    /// The dark theme's own value is untouched and already clears 4.5:1
    /// on both.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void FaintText_MeetsWcagAaBodyTextContrast_OnEveryRealSurfaceItIsDrawnOn(string themeName)
    {
        var theme = ThemeOf(themeName);
        var text = Colour(BrandPalette.Brush(BrandPalette.FaintTextBrushKey, theme));

        AssertContrast("FaintText on Surface", text, Colour(BrandPalette.Brush(BrandPalette.SurfaceBackgroundBrushKey, theme)), MinimumTextContrastRatio);
        AssertContrast("FaintText on Sunken", text, Colour(BrandPalette.Brush(BrandPalette.SunkenBackgroundBrushKey, theme)), MinimumTextContrastRatio);
    }

    /// <summary>
    /// The `Primary` treatment's own text (<see cref="BrandPalette.OnAccentBrushKey"/>)
    /// on its own <c>:pressed</c> fill (<see cref="BrandPalette.AccentPressBrushKey"/>).
    /// The dark theme's original `Cyan600` (`#2b7fa5`) measured only
    /// 4.42:1 against `Navy900`, under WCAG AA's 4.5:1 floor. The light
    /// theme's own press fill is untouched and already clears 4.5:1.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void PrimaryPressedText_MeetsWcagAaBodyTextContrast(string themeName)
    {
        var theme = ThemeOf(themeName);
        var text = Colour(BrandPalette.Brush(BrandPalette.OnAccentBrushKey, theme));
        var fill = Colour(BrandPalette.Brush(BrandPalette.AccentPressBrushKey, theme));

        AssertContrast("Primary pressed text on AccentPress fill", text, fill, MinimumTextContrastRatio);
    }

    /// <summary>
    /// The `Danger` treatment's own text (<see cref="BrandPalette.OnAccentBrushKey"/>)
    /// on its own <c>:pointerover</c> fill (<see cref="BrandPalette.DangerBrushKey"/>).
    /// The light theme's original `#d03a3f` measured only 4.46:1 against
    /// `Paper050`, under WCAG AA's 4.5:1 floor. The dark theme's own
    /// `Red500` fill is untouched and already clears 4.5:1.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void DangerHoverText_MeetsWcagAaBodyTextContrast(string themeName)
    {
        var theme = ThemeOf(themeName);
        var text = Colour(BrandPalette.Brush(BrandPalette.OnAccentBrushKey, theme));
        var fill = Colour(BrandPalette.Brush(BrandPalette.DangerBrushKey, theme));

        AssertContrast("Danger hover text on Danger fill", text, fill, MinimumTextContrastRatio);
    }

    private static ThemeVariant ThemeOf(string name) => name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    private static void AssertContrast(string label, Color foreground, Color background, double minimum)
    {
        var ratio = ContrastRatio(foreground, background);
        Assert.True(ratio >= minimum, $"{label}: {foreground} on {background} measured {ratio:F2}:1, need >= {minimum}:1.");
    }

    private static Color Colour(IBrush brush) => ((ISolidColorBrush)brush).Color;

    /// <summary>The real WCAG 2.1 relative-luminance/contrast-ratio formula (§1.4.3), computed directly rather than trusted from a comment — the identical formula <see cref="HealthColorContrastTests"/> already established for this suite.</summary>
    private static double ContrastRatio(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color colour)
    {
        double Channel(byte c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);
    }
}

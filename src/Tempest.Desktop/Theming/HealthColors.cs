using Avalonia.Media;
using Tempest.App.Workspace;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The Engineering Colour Language's own first concrete instantiation
/// (`WP 10.1A`, realising `WP10.0A Visual Design System.md` §4) — one
/// colour per <see cref="EngineeringHealthStatus"/> value, applied
/// identically everywhere a status is shown on the Cockpit, exactly as
/// that document's own "one value, one colour, everywhere" rule requires.
/// Colour is never the only signal (that document's own explicit
/// accessibility constraint): every accent this resolves is paired with a
/// text label wherever it is used, never colour alone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Theme-reactive since `WP 16.5A` (`TD-65`).</b> Previously returned
/// four fixed <see cref="BrandPalette"/> brushes regardless of theme — a
/// real, disclosed accessibility gap: those brand hues read fine on the
/// dark Cockpit surface but fell as low as 2.04:1 on the light theme's
/// white one, well under WCAG AA's 4.5:1 body-text floor. Resolves through
/// <see cref="ApplicationPalette"/>'s own <c>HealthText*</c> keys instead
/// (the same per-theme <see cref="Avalonia.Controls.ResourceDictionary.ThemeDictionaries"/>
/// pattern <see cref="ApplicationPalette"/>/<see cref="BrandPalette"/>
/// already established), which resolve against whichever theme is active
/// at call time — the identical mechanism <see cref="BrandPalette.Brush"/>
/// already uses. A caller that also wants live re-paint on a theme toggle
/// (rather than a value resolved fresh at construction time) should bind
/// through <see cref="ThemeReactiveBrush"/> against the same keys, exactly
/// as every other theme-reactive control in this shell does.
/// </para>
/// </remarks>
internal static class HealthColors
{
    /// <summary>Resolves the accent brush for <paramref name="status"/>, against the application's current theme.</summary>
    public static IBrush Resolve(EngineeringHealthStatus status) => status switch
    {
        EngineeringHealthStatus.Healthy => BrandPalette.Brush(ApplicationPalette.HealthTextHealthyBrushKey),
        EngineeringHealthStatus.Attention => BrandPalette.Brush(ApplicationPalette.HealthTextAttentionBrushKey),
        EngineeringHealthStatus.Blocked => BrandPalette.Brush(ApplicationPalette.HealthTextBlockedBrushKey),
        EngineeringHealthStatus.Unknown => BrandPalette.Brush(ApplicationPalette.HealthTextUnknownBrushKey),
        _ => BrandPalette.Brush(ApplicationPalette.HealthTextUnknownBrushKey),
    };

    /// <summary>Resolves a short, human-readable label for <paramref name="status"/> — always paired with <see cref="Resolve"/>'s own colour, never colour alone.</summary>
    public static string Label(EngineeringHealthStatus status) => status switch
    {
        EngineeringHealthStatus.Healthy => "Healthy",
        EngineeringHealthStatus.Attention => "Attention",
        EngineeringHealthStatus.Blocked => "Blocked",
        EngineeringHealthStatus.Unknown => "Unknown",
        _ => "Unknown",
    };
}

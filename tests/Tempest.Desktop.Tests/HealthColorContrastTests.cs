using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.App.Workspace;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 16.5A` (`TD-65`) — Cockpit health text painted at as little as
/// 2.04:1 on the light theme's white surface, well under WCAG AA's
/// 4.5:1 body-text floor. <see cref="HealthColors"/> now resolves
/// theme-specific brushes (<see cref="ApplicationPalette"/>'s own
/// <c>HealthText*</c> keys); this file computes the real contrast ratio
/// for all four <see cref="EngineeringHealthStatus"/> values, in both
/// themes, against the actual resolved Cockpit surface brush — not a
/// hand-copied hex literal, and not a comment's own claimed number.
/// </summary>
/// <remarks>
/// Not parameterised via <c>[Theory]</c>/<c>[MemberData]</c>:
/// <see cref="EngineeringHealthStatus"/> is <see langword="internal"/>
/// (reached here only via this assembly's own
/// <c>InternalsVisibleTo</c>), and a <see langword="public"/> xUnit test
/// method — required for discovery — cannot declare an
/// <see langword="internal"/> parameter type (<c>CS0051</c>); every
/// status is instead enumerated inside one test, with every failure
/// collected and reported together rather than stopping at the first.
/// </remarks>
public sealed class HealthColorContrastTests
{
    /// <summary>WCAG 2.1 AA's own floor for normal-weight body text.</summary>
    private const double MinimumContrastRatio = 4.5;

    [AvaloniaFact]
    public void HealthTextBrushes_MeetWcagAaBodyTextContrast_InBothThemes_OnTheRealResolvedCockpitSurface()
    {
        var failures = new List<string>();
        var measured = new List<string>();

        foreach (EngineeringHealthStatus status in Enum.GetValues<EngineeringHealthStatus>())
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                // The real registered resource-dictionary entries
                // `ApplicationPalette.Register` adds — the identical keys
                // `HealthColors.Resolve` itself resolves through —
                // resolved for an explicit theme (rather than toggling the
                // shared, process-wide `Application.Current.RequestedThemeVariant`,
                // which would risk racing any other test class Avalonia's
                // headless test host runs concurrently against the same
                // single `Application` instance).
                var textBrush = BrandPalette.Brush(HealthTextKey(status), theme);
                var surfaceBrush = BrandPalette.Brush(BrandPalette.SurfaceBackgroundBrushKey, theme);

                var ratio = ContrastRatio(Colour(textBrush), Colour(surfaceBrush));
                measured.Add($"{status}/{theme}: {ratio:F2}:1");

                if (ratio < MinimumContrastRatio)
                    failures.Add($"{status} on {theme}: {ratio:F2}:1 measured against the real Cockpit surface brush, need >= {MinimumContrastRatio}:1.");
            }
        }

        Assert.True(failures.Count == 0, "Contrast failures:\n" + string.Join('\n', failures) + "\n\nAll measured:\n" + string.Join('\n', measured));
    }

    /// <summary><see cref="HealthColors.Resolve"/> itself, for the application's own current (ambient) theme, must be the exact same brush <see cref="BrandPalette.Brush(string, ThemeVariant?)"/> resolves for that same theme and key — proving the production entry point actually uses these same registered resources, not a second, independent value.</summary>
    [AvaloniaFact]
    public void HealthColorsResolve_UsesTheSameRegisteredBrush_AsTheDirectKeyLookup_ForEveryStatus()
    {
        foreach (EngineeringHealthStatus status in Enum.GetValues<EngineeringHealthStatus>())
        {
            var viaProductionEntryPoint = HealthColors.Resolve(status);
            var viaDirectKeyLookup = BrandPalette.Brush(HealthTextKey(status), Avalonia.Application.Current!.ActualThemeVariant);

            Assert.Equal(Colour(viaDirectKeyLookup), Colour(viaProductionEntryPoint));
        }
    }

    private static string HealthTextKey(EngineeringHealthStatus status) => status switch
    {
        EngineeringHealthStatus.Healthy => ApplicationPalette.HealthTextHealthyBrushKey,
        EngineeringHealthStatus.Attention => ApplicationPalette.HealthTextAttentionBrushKey,
        EngineeringHealthStatus.Blocked => ApplicationPalette.HealthTextBlockedBrushKey,
        EngineeringHealthStatus.Unknown => ApplicationPalette.HealthTextUnknownBrushKey,
        _ => ApplicationPalette.HealthTextUnknownBrushKey,
    };

    private static Color Colour(IBrush brush) => ((ISolidColorBrush)brush).Color;

    /// <summary>The real WCAG 2.1 relative-luminance/contrast-ratio formula (§1.4.3), computed directly rather than trusted from a comment.</summary>
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

using Avalonia.Media;
using Avalonia.Media.Immutable;
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
internal static class HealthColors
{
    // The design system's machine-state hues — green/amber/red are
    // reserved for state, never decoration; Unknown is the faint text tier.
    private static readonly IBrush Healthy = new ImmutableSolidColorBrush(BrandPalette.Green500);
    private static readonly IBrush Attention = new ImmutableSolidColorBrush(BrandPalette.Amber500);
    private static readonly IBrush Blocked = new ImmutableSolidColorBrush(BrandPalette.Red500);
    private static readonly IBrush Unknown = new ImmutableSolidColorBrush(BrandPalette.Slate500);

    /// <summary>Resolves the accent brush for <paramref name="status"/>.</summary>
    public static IBrush Resolve(EngineeringHealthStatus status) => status switch
    {
        EngineeringHealthStatus.Healthy => Healthy,
        EngineeringHealthStatus.Attention => Attention,
        EngineeringHealthStatus.Blocked => Blocked,
        EngineeringHealthStatus.Unknown => Unknown,
        _ => Unknown,
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

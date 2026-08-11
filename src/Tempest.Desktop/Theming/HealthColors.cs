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
internal static class HealthColors
{
    /// <summary>Resolves the accent brush for <paramref name="status"/>.</summary>
    public static IBrush Resolve(EngineeringHealthStatus status) => status switch
    {
        EngineeringHealthStatus.Healthy => Brushes.SeaGreen,
        EngineeringHealthStatus.Attention => Brushes.DarkOrange,
        EngineeringHealthStatus.Blocked => Brushes.Crimson,
        EngineeringHealthStatus.Unknown => Brushes.Gray,
        _ => Brushes.Gray,
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

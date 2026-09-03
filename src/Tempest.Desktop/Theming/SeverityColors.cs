using Avalonia.Media;
using Avalonia.Media.Immutable;
using Tempest.App.Workspace;

namespace Tempest.Desktop.Theming;

/// <summary>General-purpose UI feedback severities (`WP 10.5A`) — distinct from <see cref="EngineeringHealthStatus"/> (health) and <see cref="Tempest.Core.EngineeringDomain.LifecycleState"/> (lifecycle): this is the vocabulary a Toast, a validation summary, or a confirmation dialog uses, none of which are inherently about an object's own engineering state.</summary>
public enum FeedbackSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// A deterministic, one-colour-per-value mapping for <see cref="FeedbackSeverity"/>
/// (`WP 10.5A` scope: "success messages, warning messages, error
/// presentation") — the fourth colour-language mapping this platform now
/// carries (<see cref="HealthColors"/>, `CategoryColors`, `LifecycleColors`
/// — `WP 10.4A` — each keyed on a different, genuinely distinct
/// classification), mirroring <see cref="HealthColors"/>'s own "one
/// value, one colour, everywhere" rule a fourth time. Colour is never the
/// only signal here either — every consumer pairs this with
/// <see cref="Glyph"/> and <see cref="Label"/>, never colour alone.
/// </summary>
internal static class SeverityColors
{
    // The design system's machine-state hues; Info is the brand cyan.
    private static readonly IBrush Info = new ImmutableSolidColorBrush(BrandPalette.Cyan500);
    private static readonly IBrush Success = new ImmutableSolidColorBrush(BrandPalette.Green500);
    private static readonly IBrush Warning = new ImmutableSolidColorBrush(BrandPalette.Amber500);
    private static readonly IBrush Error = new ImmutableSolidColorBrush(BrandPalette.Red500);
    private static readonly IBrush Neutral = new ImmutableSolidColorBrush(BrandPalette.Slate500);

    /// <summary>Resolves the accent brush for <paramref name="severity"/>.</summary>
    public static IBrush Resolve(FeedbackSeverity severity) => severity switch
    {
        FeedbackSeverity.Info => Info,
        FeedbackSeverity.Success => Success,
        FeedbackSeverity.Warning => Warning,
        FeedbackSeverity.Error => Error,
        _ => Neutral,
    };

    /// <summary>A single-codepoint, text-default-presentation Unicode glyph for <paramref name="severity"/> — chosen specifically to avoid emoji-presentation codepoints (Unicode UTR#51), so it renders as a plain, monochrome, theme-tinted symbol via inherited <c>Foreground</c> on every platform, never a colour-forced pictograph.</summary>
    public static string Glyph(FeedbackSeverity severity) => severity switch
    {
        FeedbackSeverity.Info => "ⓘ",
        FeedbackSeverity.Success => "✓",
        FeedbackSeverity.Warning => "⚠",
        FeedbackSeverity.Error => "⊗",
        _ => "•",
    };

    /// <summary>A short, human-readable label for <paramref name="severity"/> — always paired with <see cref="Resolve"/>'s own colour, never colour alone.</summary>
    public static string Label(FeedbackSeverity severity) => severity switch
    {
        FeedbackSeverity.Info => "Info",
        FeedbackSeverity.Success => "Success",
        FeedbackSeverity.Warning => "Warning",
        FeedbackSeverity.Error => "Error",
        _ => "Unknown",
    };
}

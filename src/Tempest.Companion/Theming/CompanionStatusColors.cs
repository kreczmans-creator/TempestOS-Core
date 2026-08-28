using Avalonia.Media;
using Tempest.Companion.Offline;

namespace Tempest.Companion.Theming;

/// <summary>
/// Semantic status colours — the Tempest Engineering Design System's own
/// machine-state hues (`WP 14.1A`): green/amber/red are reserved for
/// state and never decoration; cyan marks the live/informational
/// channel. Status is never conveyed by colour alone — every consumer
/// pairs the colour with the status text itself.
/// </summary>
/// <remarks>
/// The health *vocabulary* (Healthy/Attention/Blocked/Unknown) is the
/// platform's own (`EngineeringHealthStatus`); the concrete hues here
/// are the brand pack's, superseding `WP 14.0A`'s provisional named
/// colours. The desktop's `HealthColors` still carries its pre-brand
/// values — realigning it is registered future work, not silently done
/// here.
/// </remarks>
public static class CompanionStatusColors
{
    /// <summary>Maps an <c>EngineeringHealthStatus</c> name (the wire form) to its machine-state colour.</summary>
    public static IBrush ForHealth(string status) => status switch
    {
        "Healthy" => new SolidColorBrush(BrandPalette.Green500),
        "Attention" => new SolidColorBrush(BrandPalette.Amber500),
        "Blocked" => new SolidColorBrush(BrandPalette.Red500),
        _ => new SolidColorBrush(BrandPalette.Slate500),
    };

    /// <summary>Maps a data-freshness state to its indicator colour — Live is the cyan live-value channel; Cached/Stale/Unavailable are machine state.</summary>
    public static IBrush ForFreshness(DataFreshness freshness) => freshness switch
    {
        DataFreshness.Live => new SolidColorBrush(BrandPalette.Cyan500),
        DataFreshness.Cached => new SolidColorBrush(BrandPalette.Green500),
        DataFreshness.Stale => new SolidColorBrush(BrandPalette.Amber500),
        _ => new SolidColorBrush(BrandPalette.Red500),
    };

    /// <summary>Maps a <c>NotificationSeverity</c> name to its colour.</summary>
    public static IBrush ForSeverity(string severity) => severity switch
    {
        "Success" => new SolidColorBrush(BrandPalette.Green500),
        "Warning" => new SolidColorBrush(BrandPalette.Amber500),
        "Error" => new SolidColorBrush(BrandPalette.Red500),
        _ => new SolidColorBrush(BrandPalette.Cyan500),
    };

    /// <summary>Maps a <c>NotificationSeverity</c> name to the pack's four-character log level (<c>INFO WARN ERR OK</c>).</summary>
    public static string LogLevelFor(string severity) => severity switch
    {
        "Success" => "OK",
        "Warning" => "WARN",
        "Error" => "ERR",
        _ => "INFO",
    };
}

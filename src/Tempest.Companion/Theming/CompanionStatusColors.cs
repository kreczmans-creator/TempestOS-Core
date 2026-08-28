using Avalonia.Media;
using Tempest.Companion.Offline;

namespace Tempest.Companion.Theming;

/// <summary>
/// The Companion's semantic status colours — the identical
/// Healthy/Attention/Blocked/Unknown colour language
/// <c>Tempest.Desktop.Theming.HealthColors</c> established platform-wide
/// (`WP 10.1A`), never a new, competing scheme: an engineer who knows
/// what SeaGreen means on the desktop Cockpit knows what it means here.
/// Status is never conveyed by colour alone — every consumer pairs the
/// colour with the status text itself (accessibility, `WP 10.5B`'s own
/// standing rule).
/// </summary>
public static class CompanionStatusColors
{
    /// <summary>Maps an <c>EngineeringHealthStatus</c> name (the wire form) to its platform colour.</summary>
    public static IBrush ForHealth(string status) => status switch
    {
        "Healthy" => Brushes.SeaGreen,
        "Attention" => Brushes.DarkOrange,
        "Blocked" => Brushes.Crimson,
        _ => Brushes.Gray,
    };

    /// <summary>Maps a data-freshness state to its indicator colour — Live borrows the brand's Electric Blue (a connectivity state, not an engineering health state).</summary>
    public static IBrush ForFreshness(DataFreshness freshness) => freshness switch
    {
        DataFreshness.Live => new SolidColorBrush(BrandPalette.ElectricBlue),
        DataFreshness.Cached => Brushes.SeaGreen,
        DataFreshness.Stale => Brushes.DarkOrange,
        _ => Brushes.Crimson,
    };

    /// <summary>Maps a <c>NotificationSeverity</c> name to its colour — the identical Info/Success/Warning/Error mapping <c>Tempest.Desktop.Theming.SeverityColors</c> uses.</summary>
    public static IBrush ForSeverity(string severity) => severity switch
    {
        "Success" => Brushes.SeaGreen,
        "Warning" => Brushes.DarkOrange,
        "Error" => Brushes.Crimson,
        _ => Brushes.SteelBlue,
    };
}

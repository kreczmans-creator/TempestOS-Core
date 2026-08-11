using Avalonia.Media;

namespace Tempest.Desktop.Theming;

/// <summary>
/// TempestOS's own "engineering colour language" (`WP 10.5C`, Ribbon
/// Modernisation/Visual Language scope) — a deterministic, one-colour-
/// per-discipline mapping, keyed by <c>CommandDescriptor.Category</c>'s
/// own real string value (the same six real discipline names
/// <see cref="Tempest.Desktop.Views.RibbonView"/> already groups its own
/// tabs by, `WP 10.3B`) — distinct from <see cref="HealthColors"/>
/// (health status), <see cref="SeverityColors"/> (feedback severity), and
/// <c>LifecycleColors</c>/<c>CategoryColors</c> (both `Tempest.Desktop.
/// DigitalThread`, object lifecycle/relationship category). No platform
/// concept before this Work Package answered "what colour is
/// Mechanical vs. Requirements vs. Calculations" — confirmed directly, a
/// real, previously-missing cross-cutting colour identity, now shared by
/// every surface that names a discipline (Ribbon tab accents, this Work
/// Package's own new Object Editor discipline strip).
/// </summary>
internal static class DisciplineColors
{
    /// <summary>
    /// Resolves <paramref name="category"/> (a real
    /// <c>CommandDescriptor.Category</c>/Navigation area title substring,
    /// case-insensitive) to its own fixed accent brush. An unrecognised
    /// category — a future discipline this Work Package did not
    /// anticipate — falls back to a neutral, disclosed default rather
    /// than throwing or guessing.
    /// </summary>
    public static IBrush Resolve(string? category)
    {
        if (category is null)
            return Brushes.Gray;

        if (category.Contains("Mechanical", StringComparison.OrdinalIgnoreCase))
            return Brushes.SteelBlue;
        if (category.Contains("Requirement", StringComparison.OrdinalIgnoreCase))
            return Brushes.MediumPurple;
        if (category.Contains("Calculation", StringComparison.OrdinalIgnoreCase))
            return Brushes.DarkOrange;
        if (category.Contains("Verification", StringComparison.OrdinalIgnoreCase))
            return Brushes.SeaGreen;
        if (category.Contains("Document", StringComparison.OrdinalIgnoreCase))
            return Brushes.Goldenrod;
        if (category.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase))
            return Brushes.Teal;

        return Brushes.Gray;
    }
}

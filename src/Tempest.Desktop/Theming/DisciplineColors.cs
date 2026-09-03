using Avalonia.Media;
using Avalonia.Media.Immutable;

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
            return Neutral;

        if (category.Contains("Mechanical", StringComparison.OrdinalIgnoreCase))
            return Mechanical;
        if (category.Contains("Requirement", StringComparison.OrdinalIgnoreCase))
            return Requirements;
        if (category.Contains("Calculation", StringComparison.OrdinalIgnoreCase))
            return Calculations;
        if (category.Contains("Verification", StringComparison.OrdinalIgnoreCase))
            return Verification;
        if (category.Contains("Document", StringComparison.OrdinalIgnoreCase))
            return Documents;
        if (category.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase))
            return Manufacturing;

        return Neutral;
    }

    // Six hues from the brand triad and its machine-state family, each
    // legible on both the navy and the paper ground. Cyan is the
    // Mechanical/product-structure discipline (the platform's primary
    // object graph carries the primary accent); violet, the brand's own
    // secondary, marks Requirements.
    private static readonly IBrush Mechanical = new ImmutableSolidColorBrush(BrandPalette.Cyan500);
    private static readonly IBrush Requirements = new ImmutableSolidColorBrush(Color.Parse("#9d6cf0"));
    private static readonly IBrush Calculations = new ImmutableSolidColorBrush(BrandPalette.Amber500);
    private static readonly IBrush Verification = new ImmutableSolidColorBrush(BrandPalette.Green500);
    private static readonly IBrush Documents = new ImmutableSolidColorBrush(Color.Parse("#5fb8b0"));
    private static readonly IBrush Manufacturing = new ImmutableSolidColorBrush(Color.Parse("#f27e5c"));
    private static readonly IBrush Neutral = new ImmutableSolidColorBrush(BrandPalette.Slate500);
}

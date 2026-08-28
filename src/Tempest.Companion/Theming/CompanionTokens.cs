using Avalonia;
using Avalonia.Media;

namespace Tempest.Companion.Theming;

/// <summary>
/// The Companion's own spacing/typography/touch-target constants — the
/// mobile counterpart of <c>Tempest.Desktop.Theming.DesignTokens</c>
/// (`WP 10.2A`): the identical 4px spacing rhythm, with sizes re-based
/// for touch-first interaction (44dp minimum touch targets, larger body
/// text) rather than pointer precision.
/// </summary>
/// <remarks>
/// Typography realises the TempestOS type system concretely for the first
/// time (`WP 14.0A`): <see cref="TitleFont"/> (Chakra Petch) for product
/// identity and card titles, <see cref="BodyFont"/> (Inter) for readable
/// UI text, <see cref="MonoFont"/> (Space Mono) for technical/status
/// values. Chakra Petch and Space Mono are embedded application
/// resources; Inter ships via the official <c>Avalonia.Fonts.Inter</c>
/// package. Every family carries a platform fallback stack, so a missing
/// face degrades to a system font rather than failing to render.
/// </remarks>
public static class CompanionTokens
{
    // ------------------------------------------------------------
    // Spacing — the same 4px base unit DesignTokens established.
    // ------------------------------------------------------------

    /// <summary>2dp.</summary>
    public const double SpaceXs = 2;

    /// <summary>4dp.</summary>
    public const double SpaceSm = 4;

    /// <summary>8dp.</summary>
    public const double SpaceMd = 8;

    /// <summary>12dp.</summary>
    public const double SpaceLg = 12;

    /// <summary>16dp — the page's own horizontal gutter.</summary>
    public const double SpaceXl = 16;

    /// <summary>24dp.</summary>
    public const double SpaceXxl = 24;

    /// <summary>A page's own content padding — gutter left/right, breathing room top/bottom.</summary>
    public static readonly Thickness PagePadding = new(SpaceXl, SpaceLg);

    /// <summary>A card's own internal padding.</summary>
    public static readonly Thickness CardPadding = new(SpaceXl, SpaceLg);

    /// <summary>The vertical rhythm between stacked cards.</summary>
    public const double CardSpacing = SpaceLg;

    // ------------------------------------------------------------
    // Typography — mobile-legible sizes over the desktop's compact ones.
    // ------------------------------------------------------------

    /// <summary>The product wordmark / page title size.</summary>
    public const double FontSizeTitle = 20;

    /// <summary>A card/section heading.</summary>
    public const double FontSizeHeading = 15;

    /// <summary>Body text.</summary>
    public const double FontSizeBody = 14;

    /// <summary>Secondary/caption text.</summary>
    public const double FontSizeCaption = 12;

    /// <summary>A hero status value (the Cockpit's own health readout).</summary>
    public const double FontSizeHero = 28;

    /// <summary>Heading weight.</summary>
    public static readonly FontWeight WeightHeading = FontWeight.SemiBold;

    /// <summary>Body weight.</summary>
    public static readonly FontWeight WeightBody = FontWeight.Normal;

    /// <summary>Chakra Petch — TempestOS product identity and headings. Embedded resource with system fallbacks.</summary>
    public static readonly FontFamily TitleFont = new("avares://Tempest.Companion/Assets/Fonts#Chakra Petch, Segoe UI, Roboto, sans-serif");

    /// <summary>Inter — readable UI/body text, via <c>Avalonia.Fonts.Inter</c>'s own embedded face.</summary>
    public static readonly FontFamily BodyFont = new("fonts:Inter#Inter, Segoe UI, Roboto, sans-serif");

    /// <summary>Space Mono — technical/system/status values. Embedded resource with monospace fallbacks.</summary>
    public static readonly FontFamily MonoFont = new("avares://Tempest.Companion/Assets/Fonts#Space Mono, Consolas, Courier New, monospace");

    // ------------------------------------------------------------
    // Structure — touch-first floors, not the desktop's pointer ones.
    // ------------------------------------------------------------

    /// <summary>The minimum touch target — 44dp, the accessibility floor mobile platforms converge on (the desktop's 28dp pointer floor is deliberately not reused).</summary>
    public const double MinTouchTarget = 44;

    /// <summary>The top app bar's own height.</summary>
    public const double AppBarHeight = 56;

    /// <summary>The bottom navigation bar's own height.</summary>
    public const double NavBarHeight = 64;

    /// <summary>A card/dialog corner radius — slightly softer than the desktop's 6, still restrained.</summary>
    public const double CornerRadius = 8;
}

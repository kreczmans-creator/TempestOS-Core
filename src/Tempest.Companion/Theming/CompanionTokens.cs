using Avalonia;
using Avalonia.Media;

namespace Tempest.Companion.Theming;

/// <summary>
/// The Companion's spacing/typography/shape constants — the Tempest
/// Engineering Design System's own values (`WP 14.1A`; the pack's
/// spacing/radius/typography tokens), re-based only where touch requires
/// it (the 44dp touch floor is the pack's own largest control height).
/// </summary>
/// <remarks>
/// Type roles, per the pack: Chakra Petch for anything structural —
/// headings, UI labels, numeric readouts (<see cref="TitleFont"/>);
/// Inter for running prose (<see cref="BodyFont"/>); Space Mono for
/// machine data — IDs, units, timestamps, log lines
/// (<see cref="MonoFont"/>). Labels are UPPERCASE with wide tracking;
/// prose is never set in Chakra Petch; headings never in Inter. Corners
/// are squared: 2px badges, 3px buttons/inputs, 5px cards/panels — only
/// radios and switch tracks are round.
/// </remarks>
public static class CompanionTokens
{
    // ------------------------------------------------------------
    // Spacing — the pack's 4px grid (2px exists only for hairline insets).
    // ------------------------------------------------------------

    /// <summary>2dp — hairline insets only.</summary>
    public const double SpaceXs = 2;

    /// <summary>4dp.</summary>
    public const double SpaceSm = 4;

    /// <summary>8dp.</summary>
    public const double SpaceMd = 8;

    /// <summary>12dp.</summary>
    public const double SpaceLg = 12;

    /// <summary>16dp — the page's own horizontal gutter (the pack's 32px page padding, halved for a phone).</summary>
    public const double SpaceXl = 16;

    /// <summary>24dp — the pack's gutter.</summary>
    public const double SpaceXxl = 24;

    /// <summary>A page's own content padding.</summary>
    public static readonly Thickness PagePadding = new(SpaceXl, SpaceLg);

    /// <summary>A card's own internal padding.</summary>
    public static readonly Thickness CardPadding = new(SpaceXl, SpaceLg);

    /// <summary>The vertical rhythm between stacked cards.</summary>
    public const double CardSpacing = SpaceLg;

    // ------------------------------------------------------------
    // Typography — pack roles and scale.
    // ------------------------------------------------------------

    /// <summary>A page/section title.</summary>
    public const double FontSizeTitle = 18;

    /// <summary>A card heading — set as an UPPERCASE label, not display type.</summary>
    public const double FontSizeHeading = 13;

    /// <summary>Body prose (the pack's 14–18px prose band, phone end).</summary>
    public const double FontSizeBody = 14;

    /// <summary>Secondary/caption text and mono metadata.</summary>
    public const double FontSizeCaption = 12;

    /// <summary>A micro label (the pack's 10–12px label band, lower end).</summary>
    public const double FontSizeLabel = 11;

    /// <summary>A hero numeric/status readout (the pack's 28–48px readout band, phone end).</summary>
    public const double FontSizeHero = 30;

    /// <summary>Heading weight.</summary>
    public static readonly FontWeight WeightHeading = FontWeight.SemiBold;

    /// <summary>Label weight — Chakra Petch Medium.</summary>
    public static readonly FontWeight WeightLabel = FontWeight.Medium;

    /// <summary>Body weight.</summary>
    public static readonly FontWeight WeightBody = FontWeight.Normal;

    /// <summary>
    /// Wide label tracking, in device pixels — the pack's <c>.14em</c> at
    /// the 11px label size. Applied to every UPPERCASE label.
    /// </summary>
    public const double LabelTracking = 1.6;

    /// <summary>The pack's widest tracking (<c>.28em</c> at 10px) — the COMPANION product-surface tag.</summary>
    public const double WideTracking = 2.8;

    /// <summary>Chakra Petch — structural: headings, labels, numeric readouts. Embedded pack asset with system fallbacks.</summary>
    public static readonly FontFamily TitleFont = new("avares://Tempest.Companion/Assets/Fonts#Chakra Petch, Segoe UI, Roboto, sans-serif");

    /// <summary>Inter — running prose, via the official embedded Avalonia package.</summary>
    public static readonly FontFamily BodyFont = new("fonts:Inter#Inter, Segoe UI, Roboto, sans-serif");

    /// <summary>Space Mono — machine data: IDs, units, timestamps, log levels. Embedded pack asset with monospace fallbacks.</summary>
    public static readonly FontFamily MonoFont = new("avares://Tempest.Companion/Assets/Fonts#Space Mono, Consolas, Courier New, monospace");

    // ------------------------------------------------------------
    // Shape — the pack's squared-corner system.
    // ------------------------------------------------------------

    /// <summary>A badge/pill corner (2px) — the pack's smallest square.</summary>
    public const double BadgeCornerRadius = 2;

    /// <summary>A button/input corner (3px).</summary>
    public const double ControlCornerRadius = 3;

    /// <summary>A card/panel/dialog corner (5px).</summary>
    public const double CornerRadius = 5;

    /// <summary>The status rule on a card's top edge, and the selection rule on a list item's left edge (2px).</summary>
    public const double RuleThickness = 2;

    // ------------------------------------------------------------
    // Structure — touch-first floors.
    // ------------------------------------------------------------

    /// <summary>The minimum touch target — the pack's own largest control height (44px), which is also the mobile accessibility floor.</summary>
    public const double MinTouchTarget = 44;

    /// <summary>The top app bar's own height (the pack's 56px OS top bar).</summary>
    public const double AppBarHeight = 56;

    /// <summary>The bottom navigation bar's own height.</summary>
    public const double NavBarHeight = 60;
}

using Avalonia;
using Avalonia.Media;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The Workspace Visual Design system's own spacing/typography/shape
/// constants (`WP 10.2A`; realigned to the Tempest Engineering Design
/// System at the Desktop brand alignment) — a single, shared source every
/// panel/control draws from, so "spacing system," "typography hierarchy,"
/// and "modern layout margins" are genuinely consistent platform-wide,
/// never independently eyeballed per control. Deliberately values-only:
/// no new theming *mechanism* is introduced (Avalonia's own
/// <see cref="Application.Current"/>/<see cref="Avalonia.Styling.ThemeVariant"/>
/// remain the only theme engine, `WP 10.0B`, `ADR-0094`).
/// </summary>
/// <remarks>
/// Type roles, per the design system: Chakra Petch for anything
/// structural — headings, UPPERCASE labels, numeric readouts
/// (<see cref="TitleFont"/>); Inter for running prose and control text
/// (<see cref="BodyFont"/>); Space Mono for machine data — identifiers,
/// units, timestamps, log lines (<see cref="MonoFont"/>). Labels are
/// UPPERCASE with wide tracking; prose is never set in Chakra Petch;
/// headings never in Inter. Corners are squared: 2px badges, 3px
/// buttons/inputs, 5px cards/panels.
/// </remarks>
internal static class DesignTokens
{
    // ------------------------------------------------------------
    // Spacing system — the pack's 4px grid (2px exists only for hairline
    // insets), mirroring the rhythm Avalonia's own FluentTheme follows.
    // ------------------------------------------------------------

    public const double SpaceXs = 2;
    public const double SpaceSm = 4;
    public const double SpaceMd = 8;
    public const double SpaceLg = 12;
    public const double SpaceXl = 16;
    public const double SpaceXxl = 24;
    public const double SpaceXxxl = 32;

    public static readonly Thickness PanelPadding = new(SpaceMd);
    public static readonly Thickness PanelHeaderPadding = new(SpaceLg, SpaceMd);
    public static readonly Thickness ControlMargin = new(0, SpaceXs);
    public static readonly Thickness SectionMargin = new(0, SpaceMd, 0, SpaceXs);

    /// <summary>A page/module surface's own content padding (the pack's 32px page padding, at desktop density).</summary>
    public static readonly Thickness PagePadding = new(SpaceXxl, SpaceXl);

    /// <summary>A card's own internal padding.</summary>
    public static readonly Thickness CardPadding = new(SpaceXl, SpaceLg);

    // ------------------------------------------------------------
    // Typography hierarchy — the pack's roles and scale, at desktop
    // density.
    // ------------------------------------------------------------

    /// <summary>A module/page display title — Chakra Petch.</summary>
    public const double FontSizeDisplay = 22;

    /// <summary>A panel/section title — Chakra Petch.</summary>
    public const double FontSizeTitle = 16;

    /// <summary>A card heading — set as an UPPERCASE label, not display type.</summary>
    public const double FontSizeHeading = 13;

    /// <summary>Body/control text.</summary>
    public const double FontSizeBody = 12;

    /// <summary>Secondary/caption text and mono metadata.</summary>
    public const double FontSizeCaption = 11;

    /// <summary>A micro UPPERCASE label (the pack's 10–12px label band).</summary>
    public const double FontSizeLabel = 10;

    /// <summary>A hero numeric/status readout (the pack's 28–48px readout band, desktop end).</summary>
    public const double FontSizeHero = 28;

    public static readonly FontWeight WeightHeading = FontWeight.SemiBold;
    public static readonly FontWeight WeightLabel = FontWeight.Medium;
    public static readonly FontWeight WeightBody = FontWeight.Normal;

    /// <summary>Wide label tracking, in device pixels — the pack's <c>.14em</c> at the label size. Applied to every UPPERCASE label.</summary>
    public const double LabelTracking = 1.4;

    /// <summary>The pack's widest tracking (<c>.28em</c>) — the product-surface tag beside the lockup.</summary>
    public const double WideTracking = 2.8;

    /// <summary>Chakra Petch — structural: headings, labels, numeric readouts. Embedded pack asset with system fallbacks.</summary>
    public static readonly FontFamily TitleFont = new("avares://Tempest.Desktop/Assets/Fonts#Chakra Petch, Segoe UI, Roboto, sans-serif");

    /// <summary>Inter — running prose and control text, via the official embedded Avalonia package.</summary>
    public static readonly FontFamily BodyFont = new("fonts:Inter#Inter, Segoe UI, Roboto, sans-serif");

    /// <summary>Space Mono — machine data: identifiers, units, timestamps, log levels. Embedded pack asset with monospace fallbacks.</summary>
    public static readonly FontFamily MonoFont = new("avares://Tempest.Desktop/Assets/Fonts#Space Mono, Consolas, Courier New, monospace");

    // ------------------------------------------------------------
    // Shape — the pack's squared-corner system.
    // ------------------------------------------------------------

    /// <summary>A badge/pill corner (2px) — the pack's smallest square.</summary>
    public const double BadgeCornerRadius = 2;

    /// <summary>A button/input corner (3px).</summary>
    public const double ControlCornerRadius = 3;

    /// <summary>A card/panel/dialog corner (5px).</summary>
    public const double PanelCornerRadius = 5;

    /// <summary>A dialog/overlay panel's own corner radius — Toast/ConfirmationDialog/EmptyStateView all share this exactly.</summary>
    public const double DialogCornerRadius = PanelCornerRadius;

    /// <summary>The status rule on a card's top edge, and the selection rule on a list item's or rail item's left edge (2px).</summary>
    public const double RuleThickness = 2;

    // ------------------------------------------------------------
    // Structural
    // ------------------------------------------------------------

    /// <summary>The minimum interactive control size (`WP10.2A`'s own Accessibility "minimum control sizes" requirement) — a real, applied floor.</summary>
    public const double MinControlSize = 28;

    /// <summary>A hairline separator brush key — the brand's 8% hairline.</summary>
    public const string SeparatorBrushKey = BrandPalette.HairlineBrushKey;

    /// <summary>The keyboard-focus-visible ring's own thickness — paired with <see cref="ApplicationPalette.FocusRingBrushKey"/>.</summary>
    public const double FocusRingThickness = 2;

    /// <summary>A small control's own height (a compact toolbar/chip button) — never below <see cref="MinControlSize"/>.</summary>
    public const double ControlSizeSmall = MinControlSize;

    /// <summary>A medium control's own height (the platform's prevailing default).</summary>
    public const double ControlSizeMedium = 32;

    /// <summary>A large control's own height (a primary dialog action button).</summary>
    public const double ControlSizeLarge = 40;

    /// <summary>A dialog/overlay panel's own internal padding.</summary>
    public static readonly Thickness DialogPadding = new(SpaceXl);

    /// <summary>A small inline glyph's own font size (a Toast/validation-row severity symbol).</summary>
    public const double IconSizeSmall = 14;

    /// <summary>A prominent standalone glyph's own font size (an EmptyStateView's own centred icon).</summary>
    public const double IconSizeLarge = 40;

    /// <summary>A vector chrome icon's own rendered size.</summary>
    public const double ChromeIconSize = 16;

    // ------------------------------------------------------------
    // Shell geometry — the fixed chrome every module shares.
    // ------------------------------------------------------------

    /// <summary>The brand header bar's own height (the pack's OS top bar, at desktop density).</summary>
    public const double HeaderHeight = 48;

    /// <summary>The global navigation rail's expanded width.</summary>
    public const double RailWidth = 200;

    /// <summary>The rail's compact (glyph-only) width, used below <see cref="CompactShellWidth"/>.</summary>
    public const double RailCompactWidth = 56;

    /// <summary>The window width below which the rail collapses to its compact form.</summary>
    public const double CompactShellWidth = 1240;

    /// <summary>The status bar's own height.</summary>
    public const double StatusBarHeight = 26;
}

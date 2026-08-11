using Avalonia;
using Avalonia.Media;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The Workspace Visual Design system's own spacing/typography/margin
/// constants (`WP 10.2A`) — a single, shared source every modernised
/// panel/control draws from, so "spacing system," "typography hierarchy,"
/// and "modern layout margins" are genuinely consistent platform-wide,
/// never independently eyeballed per control. Deliberately values-only:
/// no new theming *mechanism* is introduced (Avalonia's own
/// <see cref="Application.Current"/>/<see cref="Avalonia.Styling.ThemeVariant"/>
/// remain the only theme engine, `WP 10.0B`, `ADR-0094`) — full light/dark
/// compatibility is inherited automatically, not re-implemented here.
/// </summary>
internal static class DesignTokens
{
    // ------------------------------------------------------------
    // Spacing system — a 4px base unit, mirroring the Fluent design
    // language Avalonia's own FluentTheme already follows, so nothing
    // here fights the underlying theme's own rhythm.
    // ------------------------------------------------------------

    public const double SpaceXs = 2;
    public const double SpaceSm = 4;
    public const double SpaceMd = 8;
    public const double SpaceLg = 12;
    public const double SpaceXl = 16;
    public const double SpaceXxl = 24;

    public static readonly Thickness PanelPadding = new(SpaceMd);
    public static readonly Thickness PanelHeaderPadding = new(SpaceLg, SpaceMd);
    public static readonly Thickness ControlMargin = new(0, SpaceXs);
    public static readonly Thickness SectionMargin = new(0, SpaceMd, 0, SpaceXs);

    // ------------------------------------------------------------
    // Typography hierarchy — three sizes, three weights, covering every
    // text role this Work Package's own controls need: a panel/section
    // title, a body label, and a secondary/caption value.
    // ------------------------------------------------------------

    public const double FontSizeTitle = 16;
    public const double FontSizeHeading = 13;
    public const double FontSizeBody = 12;
    public const double FontSizeCaption = 11;

    public static readonly FontWeight WeightHeading = FontWeight.SemiBold;
    public static readonly FontWeight WeightBody = FontWeight.Normal;

    // ------------------------------------------------------------
    // Structural
    // ------------------------------------------------------------

    /// <summary>The minimum interactive control size (`WP10.2A`'s own Accessibility "minimum control sizes" requirement) — a real, applied floor, not merely a documented target.</summary>
    public const double MinControlSize = 28;

    /// <summary>A hairline separator brush key — resolved from the active theme's own resources so it never hardcodes a colour that would look wrong in the other theme variant.</summary>
    public const string SeparatorBrushKey = "SystemBaseLowColor";

    // ------------------------------------------------------------
    // Interaction/feedback tokens (`WP 10.5A`) — the Visual Polish
    // Work Package's own additions: keyboard-focus visibility, three
    // named control-size tiers (distinct from the single
    // `MinControlSize` floor above, which every tier still respects),
    // and shared dialog/overlay geometry, so every new feedback
    // control (Toast/BusyOverlay/ConfirmationDialog/EmptyStateView)
    // and every future one draws from the identical values.
    // ------------------------------------------------------------

    /// <summary>The keyboard-focus-visible ring's own thickness — paired with <see cref="ApplicationPalette.FocusRingBrushKey"/>.</summary>
    public const double FocusRingThickness = 2;

    /// <summary>A small control's own height (a compact toolbar/chip button) — never below <see cref="MinControlSize"/>.</summary>
    public const double ControlSizeSmall = MinControlSize;

    /// <summary>A medium control's own height (the platform's own prevailing default — most buttons/inputs already use this implicitly via <see cref="MinControlSize"/>).</summary>
    public const double ControlSizeMedium = 32;

    /// <summary>A large control's own height (a primary dialog action button).</summary>
    public const double ControlSizeLarge = 40;

    /// <summary>A dialog/overlay panel's own corner radius — Toast/ConfirmationDialog/EmptyStateView all share this exactly, never independently chosen per control.</summary>
    public const double DialogCornerRadius = 6;

    /// <summary>A dialog/overlay panel's own internal padding.</summary>
    public static readonly Thickness DialogPadding = new(SpaceLg);

    /// <summary>A small inline glyph's own font size (a Toast/validation-row severity symbol).</summary>
    public const double IconSizeSmall = 14;

    /// <summary>A prominent standalone glyph's own font size (an EmptyStateView's own centred icon).</summary>
    public const double IconSizeLarge = 40;
}

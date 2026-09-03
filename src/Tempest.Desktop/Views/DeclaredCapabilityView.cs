using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Shell;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The surface behind a navigation destination that is declared but not
/// yet implemented.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <b>not</b> a decorative placeholder. It names the module or
/// project area, states in the product's own words what the capability is
/// for, says exactly which parts of it already exist in the platform and
/// which do not, and cites the debt item that tracks the rest. Where a
/// project is open it names that project, so the surface is genuinely
/// project-aware rather than a context-free "coming soon" card.
/// </para>
/// <para>
/// Everything it renders comes from <see cref="ShellAreas"/>/
/// <see cref="ProjectAreas"/> — the same declarations a test asserts
/// against — so this view cannot claim something the application state
/// does not.
/// </para>
/// </remarks>
public sealed class DeclaredCapabilityView : UserControl
{
    /// <summary>The badge text shown on every declared-but-unimplemented surface.</summary>
    public const string NotImplementedBadge = "Not yet implemented";

    /// <summary>Builds the surface for a declared global module.</summary>
    public DeclaredCapabilityView(ShellAreaDescriptor descriptor, string? projectLabel = null)
        : this(
            IconFor((descriptor ?? throw new ArgumentNullException(nameof(descriptor))).Area),
            descriptor.Title,
            descriptor.Availability,
            descriptor.Note,
            descriptor.TrackedBy,
            projectLabel)
    {
    }

    /// <summary>Builds the surface for a declared project area.</summary>
    public DeclaredCapabilityView(ProjectAreaDescriptor descriptor, string? projectLabel = null)
        : this(
            IconFor((descriptor ?? throw new ArgumentNullException(nameof(descriptor))).Area),
            descriptor.Title,
            descriptor.Availability,
            descriptor.Note,
            descriptor.TrackedBy,
            projectLabel)
    {
    }

    private DeclaredCapabilityView(
        StreamGeometry icon, string title, NavigationAvailability availability, string note, string? trackedBy, string? projectLabel)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 520,
            Margin = DesignTokens.PagePadding,
            Spacing = DesignTokens.SpaceSm,
        };

        // The same crisp vector iconography the nav rail and project tab
        // strip use for this exact module — never the raw Unicode glyph
        // `ShellAreaDescriptor`/`ProjectAreaDescriptor` carry for those
        // text-only contexts. Rendering that glyph here (a single Unicode
        // character, system-font-dependent) previously gave a "not yet
        // implemented" surface a different, uncontrolled icon from the one
        // the user just clicked in the rail — the exact "belongs to a
        // different application" seam this view exists to avoid.
        var iconGlyph = IconGeometry.Build(icon, DesignTokens.IconSizeLarge - 8);
        iconGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        iconGlyph.VerticalAlignment = VerticalAlignment.Center;
        iconGlyph.Opacity = 0.55;
        ThemeReactiveBrush.Bind(iconGlyph, Avalonia.Controls.Documents.TextElement.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        var glyphFrame = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd),
            Child = iconGlyph,
        };
        ThemeReactiveBrush.Bind(glyphFrame, Border.BorderBrushProperty, BrandPalette.HairlineStrongBrushKey);
        ThemeReactiveBrush.Bind(glyphFrame, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        stack.Children.Add(glyphFrame);

        var titleText = new TextBlock
        {
            Text = title,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeDisplay,
            FontWeight = DesignTokens.WeightHeading,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        ThemeReactiveBrush.Bind(titleText, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        stack.Children.Add(titleText);

        if (availability == NavigationAvailability.Declared)
        {
            // The badge is the brand's violet — strictly secondary, the
            // colour the design system reserves for badges and category
            // rules — as an UPPERCASE label, never a filled call-to-action.
            var badgeText = new TextBlock
            {
                Text = NotImplementedBadge.ToUpperInvariant(),
                FontFamily = DesignTokens.TitleFont,
                FontSize = DesignTokens.FontSizeLabel,
                FontWeight = DesignTokens.WeightHeading,
                LetterSpacing = DesignTokens.LabelTracking,
            };
            ThemeReactiveBrush.Bind(badgeText, TextBlock.ForegroundProperty, BrandPalette.SecondaryAccentBrushKey);
            var badge = new Border
            {
                Padding = new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs + 1),
                CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm),
                Child = badgeText,
            };
            ThemeReactiveBrush.Bind(badge, Border.BorderBrushProperty, BrandPalette.SecondaryAccentBrushKey);
            AutomationProperties.SetName(badge, $"{title} — {NotImplementedBadge}");
            stack.Children.Add(badge);
        }

        if (projectLabel is { Length: > 0 })
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Project: {projectLabel}",
                FontSize = DesignTokens.FontSizeCaption,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.85,
            });
        }

        var noteText = new TextBlock
        {
            Text = note,
            FontSize = DesignTokens.FontSizeBody,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
        };
        ThemeReactiveBrush.Bind(noteText, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        stack.Children.Add(noteText);

        if (trackedBy is { Length: > 0 })
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Tracked as {trackedBy}.",
                FontSize = DesignTokens.FontSizeCaption,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.7,
            });
        }

        AutomationProperties.SetName(this, title);
        Content = stack;
    }

    /// <summary>
    /// The exact icon <see cref="GlobalNavigationRail"/> renders for
    /// <paramref name="area"/> — kept in lock-step by construction rather
    /// than by convention, so a declared module's own "not yet
    /// implemented" surface always shows the same glyph the user just
    /// clicked in the rail, never <see cref="ShellAreaDescriptor.Glyph"/>'s
    /// plain-text character (that field remains for the rail's own
    /// text-only contexts, e.g. automation names).
    /// </summary>
    private static StreamGeometry IconFor(ShellArea area) => area switch
    {
        ShellArea.Home => IconGeometry.Home,
        ShellArea.Projects or ShellArea.ProjectWorkspace => IconGeometry.Folder,
        ShellArea.Engineering => IconGeometry.Gear,
        ShellArea.Tasks => IconGeometry.CheckSquare,
        ShellArea.Commercial => IconGeometry.Currency,
        ShellArea.Resources => IconGeometry.People,
        ShellArea.Knowledge => IconGeometry.Book,
        ShellArea.Administration => IconGeometry.Shield,
        _ => IconGeometry.Dot,
    };

    /// <summary>The project-area counterpart of <see cref="IconFor(ShellArea)"/> — one vector icon per area, chosen for what the area actually is.</summary>
    private static StreamGeometry IconFor(ProjectArea area) => area switch
    {
        ProjectArea.Overview => IconGeometry.Compass,
        ProjectArea.Engineering => IconGeometry.Gear,
        ProjectArea.Documents => IconGeometry.Document,
        ProjectArea.Requirements => IconGeometry.Requirement,
        ProjectArea.Tasks => IconGeometry.CheckSquare,
        ProjectArea.Risks => IconGeometry.Warning,
        ProjectArea.Timeline => IconGeometry.Clock,
        ProjectArea.Reports => IconGeometry.Chart,
        ProjectArea.Settings => IconGeometry.Sliders,
        _ => IconGeometry.Dot,
    };
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Shell;
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
            (descriptor ?? throw new ArgumentNullException(nameof(descriptor))).Glyph,
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
            (descriptor ?? throw new ArgumentNullException(nameof(descriptor))).Glyph,
            descriptor.Title,
            descriptor.Availability,
            descriptor.Note,
            descriptor.TrackedBy,
            projectLabel)
    {
    }

    private DeclaredCapabilityView(
        string glyph, string title, NavigationAvailability availability, string note, string? trackedBy, string? projectLabel)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 520,
            Margin = DesignTokens.PagePadding,
            Spacing = DesignTokens.SpaceSm,
        };

        var glyphText = new TextBlock
        {
            Text = glyph,
            FontSize = DesignTokens.IconSizeLarge - 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.55,
        };
        ThemeReactiveBrush.Bind(glyphText, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        var glyphFrame = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd),
            Child = glyphText,
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
}

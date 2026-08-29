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
            MaxWidth = 460,
            Margin = DesignTokens.PanelPadding,
            Spacing = DesignTokens.SpaceSm,
        };

        stack.Children.Add(new TextBlock
        {
            Text = glyph,
            FontSize = DesignTokens.IconSizeLarge,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.6,
        });

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (availability == NavigationAvailability.Declared)
        {
            var badge = new Border
            {
                Padding = new Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs),
                CornerRadius = new CornerRadius(DesignTokens.SpaceXs),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = NotImplementedBadge, FontSize = DesignTokens.FontSizeCaption, FontWeight = FontWeight.Bold },
            };
            ThemeReactiveBrush.Bind(badge, Border.BackgroundProperty, ApplicationPalette.AccentPanelBackgroundBrushKey);
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

        stack.Children.Add(new TextBlock
        {
            Text = note,
            FontSize = DesignTokens.FontSizeBody,
            Opacity = 0.78,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });

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

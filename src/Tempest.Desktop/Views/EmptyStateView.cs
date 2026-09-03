using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Empty State Framework (`WP 10.5A` scope: "Replace placeholder
/// empty pages with genuine professional empty-state pages... guidance,
/// recommended actions, first-use assistance, engineering-specific
/// messaging") — a real, reusable "nothing here yet" panel: an icon, a
/// heading, guidance text, and an optional recommended action button.
/// </summary>
public sealed class EmptyStateView : UserControl
{
    private readonly TextBlock _icon = new() { FontSize = DesignTokens.IconSizeLarge - 8, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.55 };
    private readonly TextBlock _heading = new() { FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = Avalonia.Media.TextAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
    private readonly TextBlock _guidance = new() { FontSize = DesignTokens.FontSizeBody, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = Avalonia.Media.TextAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 380, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0), LineHeight = 18 };
    private readonly Button _action = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, DesignTokens.SpaceXl, 0, 0), MinHeight = DesignTokens.ControlSizeMedium, IsVisible = false };

    /// <summary>Initialises a new instance of the <see cref="EmptyStateView"/> class.</summary>
    /// <param name="icon">A single glyph (<see cref="Icons.IconRegistry"/>-style or plain Unicode) shown large and muted above the heading.</param>
    /// <param name="heading">A short, one-line summary of what is empty.</param>
    /// <param name="guidance">Engineering-specific guidance — why this is empty and what to do about it, never a generic "no data" placeholder.</param>
    public EmptyStateView(string icon, string heading, string guidance)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(guidance);

        _icon.Text = icon;
        _heading.Text = heading;
        _guidance.Text = guidance;

        // The design system's empty state: a muted glyph in a hairline
        // frame, a structural heading, prose guidance, one primary action.
        ThemeReactiveBrush.Bind(_icon, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        ThemeReactiveBrush.Bind(_heading, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        ThemeReactiveBrush.Bind(_guidance, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        _action.Classes.Add(ChromeStyles.Primary);

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = DesignTokens.PagePadding,
        };
        var iconFrame = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _icon,
        };
        _icon.VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(iconFrame, Border.BorderBrushProperty, BrandPalette.HairlineStrongBrushKey);
        ThemeReactiveBrush.Bind(iconFrame, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        stack.Children.Add(iconFrame);
        stack.Children.Add(_heading);
        stack.Children.Add(_guidance);
        stack.Children.Add(_action);

        Content = stack;
    }

    /// <summary>Updates the heading/guidance text in place — used where the same panel switches between distinct empty reasons (e.g. Project Explorer's own "genuinely empty" vs "filter matches nothing").</summary>
    public void SetMessage(string heading, string guidance)
    {
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(guidance);
        _heading.Text = heading;
        _guidance.Text = guidance;
    }

    /// <summary>Adds a recommended action button (`WP 10.5A` scope: "recommended actions") — e.g. "Create your first Requirement." Optional; the panel remains a valid, complete empty state without one.</summary>
    public void SetAction(string label, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(onClick);

        _action.Content = label;
        _action.IsVisible = true;
        _action.Click += (_, _) => onClick();
    }
}

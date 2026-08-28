using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// One card on a Companion page — the mobile counterpart of the desktop's
/// <c>CockpitCardControl</c> (`WP 10.1A`): the identical header-plus-
/// content anatomy and accent-left-border status treatment, re-based for
/// a phone (full-width, touch padding, Chakra Petch card titles). Cards
/// represent meaningful TempestOS concepts only — the regions the
/// Engineering Cockpit already names — never arbitrary dashboard tiles.
/// </summary>
public sealed class CompanionCard : Border
{
    private readonly StackPanel _content = new() { Spacing = CompanionTokens.SpaceMd };

    /// <summary>Initialises a new instance of the <see cref="CompanionCard"/> class.</summary>
    /// <param name="glyph">A short icon glyph — Unicode geometry, the <c>IconRegistry</c> approach, never an emoji.</param>
    /// <param name="title">The card's own title, rendered upper-case in the brand titling face.</param>
    /// <param name="accent">An optional semantic accent (a health/status colour) shown as a thicker left border — <see langword="null"/> for neutral.</param>
    public CompanionCard(string glyph, string title, IBrush? accent = null)
    {
        var app = Avalonia.Application.Current!;

        CornerRadius = new Avalonia.CornerRadius(CompanionTokens.CornerRadius);
        BorderThickness = accent is null ? new Avalonia.Thickness(1) : new Avalonia.Thickness(4, 1, 1, 1);
        BorderBrush = accent ?? BrandPalette.Brush(app, BrandPalette.CardBorderBrushKey);
        Background = BrandPalette.Brush(app, BrandPalette.CardBackgroundBrushKey);
        Padding = CompanionTokens.CardPadding;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var root = new StackPanel { Spacing = CompanionTokens.SpaceMd };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = CompanionTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            Text = glyph,
            FontSize = CompanionTokens.FontSizeHeading,
            Foreground = accent ?? BrandPalette.Brush(app, BrandPalette.AccentBrushKey),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeHeading,
            FontWeight = CompanionTokens.WeightHeading,
            LetterSpacing = 1.2,
            VerticalAlignment = VerticalAlignment.Center,
        });

        root.Children.Add(header);
        root.Children.Add(_content);

        Child = root;
    }

    /// <summary>Adds a body text line.</summary>
    public CompanionCard AddLine(string text, bool secondary = false)
    {
        var line = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = secondary ? CompanionTokens.FontSizeCaption : CompanionTokens.FontSizeBody,
        };

        // Only secondary text overrides Foreground - a primary line must
        // LEAVE the property unset so the theme's own text brush applies
        // (assigning null would be a local null brush, rendering nothing).
        if (secondary)
            line.Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.SecondaryTextBrushKey);

        _content.Children.Add(line);
        return this;
    }

    /// <summary>Adds a technical/status value line in the mono face (Space Mono — the system-information voice).</summary>
    public CompanionCard AddMonoLine(string text)
    {
        _content.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
        });
        return this;
    }

    /// <summary>Adds an arbitrary control to the content region.</summary>
    public CompanionCard AddContent(Control control)
    {
        _content.Children.Add(control);
        return this;
    }
}

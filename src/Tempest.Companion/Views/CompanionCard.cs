using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// One card on a Companion page — the Tempest Engineering Design
/// System's card anatomy (`WP 14.1A`): a flat surface fill, one
/// hairline, a 5px squared corner, no shadow, and optionally a 2px
/// status rule across the TOP edge (cyan default; amber/red for machine
/// state; violet for category — the pack's own rule vocabulary). Card
/// titles are UPPERCASE Chakra Petch labels with wide tracking; no icon
/// glyphs — the pack ships no glyph set and bans hand-drawn ones.
/// </summary>
public sealed class CompanionCard : Border
{
    private readonly StackPanel _content = new() { Spacing = CompanionTokens.SpaceMd };

    /// <summary>Initialises a new instance of the <see cref="CompanionCard"/> class.</summary>
    /// <param name="title">The card's own title, rendered as an uppercase tracked label.</param>
    /// <param name="rule">The 2px top-edge status rule brush, or <see langword="null"/> for the default hairline-only card.</param>
    public CompanionCard(string title, IBrush? rule = null)
    {
        var app = Avalonia.Application.Current!;

        CornerRadius = new Avalonia.CornerRadius(CompanionTokens.CornerRadius);
        BorderThickness = new Avalonia.Thickness(1);
        BorderBrush = BrandPalette.Brush(app, BrandPalette.CardBorderBrushKey);
        Background = BrandPalette.Brush(app, BrandPalette.CardBackgroundBrushKey);
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var outer = new StackPanel();

        if (rule is not null)
        {
            // The pack's status rule: a flat 2px band across the card's
            // whole top edge, inside the hairline.
            outer.Children.Add(new Border { Height = CompanionTokens.RuleThickness, Background = rule });
        }

        var inner = new StackPanel { Spacing = CompanionTokens.SpaceMd, Margin = CompanionTokens.CardPadding };

        inner.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeHeading,
            FontWeight = CompanionTokens.WeightLabel,
            LetterSpacing = CompanionTokens.LabelTracking,
            Foreground = BrandPalette.Brush(app, BrandPalette.HeadingTextBrushKey),
        });
        inner.Children.Add(_content);

        outer.Children.Add(inner);
        Child = outer;
    }

    /// <summary>Adds a body prose line (Inter).</summary>
    public CompanionCard AddLine(string text, bool secondary = false)
    {
        var app = Avalonia.Application.Current!;
        var line = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = secondary ? CompanionTokens.FontSizeCaption : CompanionTokens.FontSizeBody,
            Foreground = BrandPalette.Brush(app, secondary ? BrandPalette.SecondaryTextBrushKey : BrandPalette.BodyTextBrushKey),
        };

        _content.Children.Add(line);
        return this;
    }

    /// <summary>Adds a machine-data line (Space Mono — IDs, units, timestamps).</summary>
    public CompanionCard AddMonoLine(string text)
    {
        _content.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.BodyTextBrushKey),
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

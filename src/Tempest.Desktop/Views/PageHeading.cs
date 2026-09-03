using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The three text roles every module page opens with — an UPPERCASE
/// eyebrow label, a structural display title, and a prose lead — built
/// once here so the Projects module, the Project Workspace and every
/// future module page share one heading rhythm rather than each choosing
/// its own sizes.
/// </summary>
internal static class PageHeading
{
    /// <summary>The eyebrow: an UPPERCASE, wide-tracked micro label in the structural face.</summary>
    public static TextBlock Label(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeLabel,
            FontWeight = DesignTokens.WeightLabel,
            LetterSpacing = DesignTokens.LabelTracking,
        };
        ThemeReactiveBrush.Bind(label, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
        return label;
    }

    /// <summary>The page title, in the structural face at display size.</summary>
    public static TextBlock Title(string text)
    {
        var title = new TextBlock
        {
            Text = text,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeDisplay + 2,
            FontWeight = DesignTokens.WeightHeading,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeReactiveBrush.Bind(title, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        return title;
    }

    /// <summary>The lead: one sentence of prose beneath the title, muted.</summary>
    public static TextBlock Lead(string text)
    {
        var lead = new TextBlock
        {
            Text = text,
            FontSize = DesignTokens.FontSizeBody + 1,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 720,
            LineHeight = 19,
        };
        ThemeReactiveBrush.Bind(lead, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        return lead;
    }
}

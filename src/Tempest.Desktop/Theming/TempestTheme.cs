using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The Desktop shell's own theme composition — the one place the Tempest
/// Engineering Design System is applied to Avalonia's stock controls, so
/// every <see cref="Button"/>, <see cref="TabControl"/>, <see cref="TextBox"/>,
/// <see cref="TreeView"/>, <see cref="Expander"/> and <see cref="Menu"/>
/// in the product reads as one instrument rather than as Fluent defaults
/// with brand colours painted onto a few custom panels.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mechanism, not a second theme engine.</b> <see cref="FluentTheme"/>
/// remains the control theme (`ADR-0094`, unchanged). This class does
/// three sanctioned things to it: (1) supplies brand
/// <see cref="ColorPaletteResources"/> for both variants, which is
/// Fluent's own documented recolouring seam, so every stock control's own
/// state brushes derive from navy/paper/cyan rather than black/white/blue;
/// (2) overrides a small, named set of Fluent resource keys in
/// <see cref="Application.Resources"/> (squared corners, the type scale,
/// selection and focus treatment), which resource lookup consults before
/// the theme's own values; (3) adds a handful of global
/// <see cref="Style"/>s for density. <see cref="ThemeService"/> still
/// switches variants exactly as before.
/// </para>
/// <para>
/// The headless test application deliberately does <em>not</em> apply
/// this class — tests assert behaviour, not brand — so nothing here may
/// be load-bearing for a control to function.
/// </para>
/// </remarks>
internal static class TempestTheme
{
    /// <summary>Applies the brand theme to <paramref name="app"/> — call once, from <c>App.Initialize</c>, before any window exists.</summary>
    public static void Apply(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Dark] = InstrumentPalette();
        fluent.Palettes[ThemeVariant.Light] = PaperPalette();
        app.Styles.Add(fluent);

        // The platform's own theme-reactive resources (`WP 10.5A`) and the
        // brand's semantic keys — registered before any control exists.
        ApplicationPalette.Register(app);

        app.Resources.MergedDictionaries.Add(FluentOverrides());

        foreach (var style in GlobalStyles())
            app.Styles.Add(style);
    }

    // ----------------------------------------------------------------
    // Fluent colour palettes — the brand's tokens in Fluent's own slots.
    // ----------------------------------------------------------------

    /// <summary>The instrument (dark) palette — the brand's home ground.</summary>
    private static ColorPaletteResources InstrumentPalette() => new()
    {
        Accent = BrandPalette.Cyan500,
        RegionColor = BrandPalette.Navy800,

        AltHigh = BrandPalette.Navy800,
        AltMediumHigh = Alpha(BrandPalette.Navy800, 0.80),
        AltMedium = Alpha(BrandPalette.Navy800, 0.60),
        AltMediumLow = Alpha(BrandPalette.Navy800, 0.40),
        AltLow = Alpha(BrandPalette.Navy800, 0.20),

        BaseHigh = BrandPalette.Paper050,
        BaseMediumHigh = Alpha(BrandPalette.Paper050, 0.82),
        BaseMedium = BrandPalette.Slate400,
        BaseMediumLow = BrandPalette.Slate500,
        BaseLow = Alpha(BrandPalette.Paper050, 0.14),

        ChromeAltLow = BrandPalette.Paper050,
        ChromeBlackHigh = BrandPalette.Navy900,
        ChromeBlackMedium = Alpha(BrandPalette.Navy900, 0.80),
        ChromeBlackMediumLow = Alpha(BrandPalette.Navy900, 0.40),
        ChromeBlackLow = Alpha(BrandPalette.Navy900, 0.20),
        ChromeDisabledHigh = BrandPalette.Navy400,
        ChromeDisabledLow = BrandPalette.Slate500,
        ChromeGray = BrandPalette.Slate500,
        ChromeHigh = BrandPalette.Navy400,
        ChromeMedium = BrandPalette.Navy700,
        ChromeMediumLow = BrandPalette.Navy600,
        ChromeLow = BrandPalette.Navy900,
        ChromeWhite = BrandPalette.Paper050,

        ListLow = Alpha(BrandPalette.Paper050, 0.05),
        ListMedium = Alpha(BrandPalette.Paper050, 0.09),
        ErrorText = BrandPalette.Red500,
    };

    /// <summary>The paper (light) palette — documents and daylight reading.</summary>
    private static ColorPaletteResources PaperPalette() => new()
    {
        Accent = BrandPalette.Indigo600,
        RegionColor = BrandPalette.Paper050,

        AltHigh = BrandPalette.Paper000,
        AltMediumHigh = Alpha(BrandPalette.Paper000, 0.80),
        AltMedium = Alpha(BrandPalette.Paper000, 0.60),
        AltMediumLow = Alpha(BrandPalette.Paper000, 0.40),
        AltLow = Alpha(BrandPalette.Paper000, 0.20),

        BaseHigh = BrandPalette.Ink900,
        BaseMediumHigh = BrandPalette.Slate700,
        BaseMedium = BrandPalette.Slate600,
        BaseMediumLow = BrandPalette.Slate500,
        BaseLow = Alpha(BrandPalette.Ink900, 0.14),

        ChromeAltLow = BrandPalette.Slate700,
        ChromeBlackHigh = BrandPalette.Ink900,
        ChromeBlackMedium = Alpha(BrandPalette.Ink900, 0.80),
        ChromeBlackMediumLow = Alpha(BrandPalette.Ink900, 0.40),
        ChromeBlackLow = Alpha(BrandPalette.Ink900, 0.20),
        ChromeDisabledHigh = Color.Parse("#d0d2da"),
        ChromeDisabledLow = BrandPalette.Slate500,
        ChromeGray = BrandPalette.Slate500,
        ChromeHigh = Color.Parse("#d0d2da"),
        ChromeMedium = BrandPalette.Paper100,
        ChromeMediumLow = Color.Parse("#e6e8ef"),
        ChromeLow = BrandPalette.Paper100,
        ChromeWhite = BrandPalette.Paper000,

        ListLow = Alpha(BrandPalette.Ink900, 0.05),
        ListMedium = Alpha(BrandPalette.Ink900, 0.09),
        ErrorText = Color.Parse("#d03a3f"),
    };

    // ----------------------------------------------------------------
    // Fluent resource overrides — a small, named set.
    // ----------------------------------------------------------------

    private static ResourceDictionary FluentOverrides()
    {
        var shared = new ResourceDictionary
        {
            // The squared-corner system: 3px controls, 5px overlays.
            ["ControlCornerRadius"] = new CornerRadius(DesignTokens.ControlCornerRadius),
            ["OverlayCornerRadius"] = new CornerRadius(DesignTokens.PanelCornerRadius),

            // The prose face for every control's own text, and the
            // desktop density Fluent's 14px default is one step above.
            ["ContentControlThemeFontFamily"] = DesignTokens.BodyFont,
            ["ControlContentThemeFontSize"] = 12.5,

            // Tab strips read as one row of labels with a 2px accent rule,
            // never Fluent's 24px pivot headers.
            ["TabItemHeaderFontSize"] = 12.5,
            ["TabItemHeaderThemeFontWeight"] = FontWeight.Medium,
            ["TabItemHeaderMargin"] = new Thickness(DesignTokens.SpaceLg, 0),
            ["TabItemPipeThickness"] = DesignTokens.RuleThickness,

            ["MenuBarItemPadding"] = new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm),
            ["TreeViewItemMinHeight"] = 26.0,
            ["ExpanderMinHeight"] = 34.0,
            ["ProgressBarThemeMinHeight"] = 4.0,
        };

        var dark = ThemeSpecific(ThemeVariant.Dark);
        var light = ThemeSpecific(ThemeVariant.Light);
        shared.ThemeDictionaries[ThemeVariant.Dark] = dark;
        shared.ThemeDictionaries[ThemeVariant.Light] = light;
        return shared;
    }

    /// <summary>The variant-specific Fluent keys — selection, inputs, tab rules, tooltips, focus — each pointing at the brand's own semantic value for that variant.</summary>
    private static ResourceDictionary ThemeSpecific(ThemeVariant variant)
    {
        var isDark = variant == ThemeVariant.Dark;

        var accent = new ImmutableSolidColorBrush(isDark ? BrandPalette.Cyan500 : BrandPalette.Indigo600);
        var heading = new ImmutableSolidColorBrush(isDark ? BrandPalette.Paper050 : BrandPalette.Ink900);
        var muted = new ImmutableSolidColorBrush(isDark ? BrandPalette.Slate400 : BrandPalette.Slate600);
        var sunken = new ImmutableSolidColorBrush(isDark ? BrandPalette.Navy900 : BrandPalette.Paper100);
        var raised = new ImmutableSolidColorBrush(isDark ? BrandPalette.Navy600 : BrandPalette.Paper000);
        var hairline = new ImmutableSolidColorBrush(isDark ? BrandPalette.Paper050 : BrandPalette.Ink900, 0.08);
        var hairlineStrong = new ImmutableSolidColorBrush(isDark ? BrandPalette.Paper050 : BrandPalette.Ink900, 0.14);
        var hover = new ImmutableSolidColorBrush(isDark ? BrandPalette.Paper050 : BrandPalette.Ink900, 0.05);
        var selected = new ImmutableSolidColorBrush(BrandPalette.Cyan500, 0.12);
        var selectedHover = new ImmutableSolidColorBrush(BrandPalette.Cyan500, 0.18);

        return new ResourceDictionary
        {
            // Tabs: muted unselected label, heading-weight selected label,
            // the 2px accent rule beneath the selected one.
            ["TabItemHeaderForegroundUnselected"] = muted,
            ["TabItemHeaderForegroundUnselectedPointerOver"] = heading,
            ["TabItemHeaderForegroundUnselectedPressed"] = heading,
            ["TabItemHeaderForegroundSelected"] = heading,
            ["TabItemHeaderForegroundSelectedPointerOver"] = heading,
            ["TabItemHeaderForegroundSelectedPressed"] = heading,
            ["TabItemHeaderSelectedPipeFill"] = accent,

            // Inputs are sunken instrument surfaces with a hairline, and a
            // 2px-feel accent edge when focused.
            ["TextControlBackground"] = sunken,
            ["TextControlBackgroundPointerOver"] = sunken,
            ["TextControlBackgroundFocused"] = sunken,
            ["TextControlBorderBrush"] = hairlineStrong,
            ["TextControlBorderBrushPointerOver"] = new ImmutableSolidColorBrush(isDark ? BrandPalette.Paper050 : BrandPalette.Ink900, 0.24),
            ["TextControlBorderBrushFocused"] = accent,

            // Selection is the 12% cyan fill everywhere a row can be chosen.
            ["TreeViewItemBackgroundPointerOver"] = hover,
            ["TreeViewItemBackgroundSelected"] = selected,
            ["TreeViewItemBackgroundSelectedPointerOver"] = selectedHover,
            ["TreeViewItemBackgroundSelectedPressed"] = selectedHover,
            ["TreeViewItemForegroundSelected"] = heading,
            ["TreeViewItemForegroundSelectedPointerOver"] = heading,
            ["SystemControlHighlightListAccentLowBrush"] = selected,
            ["SystemControlHighlightListAccentMediumBrush"] = selectedHover,
            ["SystemControlHighlightListAccentHighBrush"] = selectedHover,
            ["SystemControlHighlightListLowBrush"] = hover,
            ["SystemControlHighlightListMediumBrush"] = hover,

            // Raised surfaces: menus, flyouts, tooltips.
            ["MenuFlyoutPresenterBackground"] = raised,
            ["MenuFlyoutPresenterBorderBrush"] = hairlineStrong,
            ["ToolTipBackground"] = raised,
            ["ToolTipBorderBrush"] = hairlineStrong,
            ["ToolTipForeground"] = heading,

            // Expander headers sit flat on their panel, not on a grey slab.
            ["ExpanderHeaderBackground"] = Brushes.Transparent,
            ["ExpanderHeaderBackgroundPointerOver"] = hover,
            ["ExpanderHeaderBackgroundPressed"] = hover,
            ["ExpanderHeaderBorderBrush"] = hairline,
            ["ExpanderContentBackground"] = Brushes.Transparent,
            ["ExpanderContentBorderBrush"] = hairline,

            // Buttons: hairline-edged raised surface; hover is a wash.
            ["ButtonBorderBrush"] = hairlineStrong,
            ["ButtonBorderBrushPointerOver"] = new ImmutableSolidColorBrush(isDark ? BrandPalette.Paper050 : BrandPalette.Ink900, 0.24),

            // Keyboard focus: the 2px accent ring.
            ["SystemControlFocusVisualPrimaryBrush"] = accent,
            ["SystemControlFocusVisualSecondaryBrush"] = Brushes.Transparent,

            // Splitters are hairlines until grabbed.
            ["GridSplitterBackground"] = hairline,
            ["GridSplitterBackgroundPointerOver"] = accent,
        };
    }

    // ----------------------------------------------------------------
    // Global density styles.
    // ----------------------------------------------------------------

    private static IEnumerable<Style> GlobalStyles()
    {
        yield return Style<TabItem>(
            (TabItem.MinHeightProperty, 34.0),
            (TabItem.PaddingProperty, new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceMd)),
            (TabItem.FontFamilyProperty, DesignTokens.BodyFont));

        yield return Style<Button>(
            (Button.PaddingProperty, new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceSm + 1)),
            (Button.CornerRadiusProperty, new CornerRadius(DesignTokens.ControlCornerRadius)));

        yield return Style<TextBox>(
            (TextBox.CornerRadiusProperty, new CornerRadius(DesignTokens.ControlCornerRadius)),
            (TextBox.PaddingProperty, new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm + 1)));

        yield return Style<ToolTip>(
            (ToolTip.FontSizeProperty, DesignTokens.FontSizeBody),
            (ToolTip.PaddingProperty, new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm + 2)),
            (ToolTip.CornerRadiusProperty, new CornerRadius(DesignTokens.ControlCornerRadius)));

        yield return Style<Expander>(
            (Expander.CornerRadiusProperty, new CornerRadius(DesignTokens.ControlCornerRadius)));

        // An Expander's header is a panel-section title: the structural
        // face, small, tracked, muted — one rule for every section in the
        // Property Inspector, the Object Editor and the dialogs.
        var expanderHeader = new Style(x => x.OfType<Expander>().Template().OfType<ToggleButton>());
        expanderHeader.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty, DesignTokens.TitleFont));
        expanderHeader.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, DesignTokens.FontSizeLabel + 1.5));
        expanderHeader.Setters.Add(new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold));
        expanderHeader.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm)));
        expanderHeader.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(BrandPalette.MutedTextBrushKey)));
        yield return expanderHeader;

        yield return Style<GridSplitter>(
            (GridSplitter.BackgroundProperty, new ImmutableSolidColorBrush(BrandPalette.Cyan500, 0.0)));

        yield return Style<Separator>(
            (Separator.MarginProperty, new Thickness(0)),
            (Separator.HeightProperty, 1.0));
    }

    private static Style Style<T>(params (AvaloniaProperty Property, object Value)[] setters) where T : StyledElement
    {
        var style = new Style(x => x.OfType<T>());
        foreach (var (property, value) in setters)
            style.Setters.Add(new Setter(property, value));
        return style;
    }

    private static Color Alpha(Color colour, double alpha) =>
        new((byte)Math.Round(alpha * 255), colour.R, colour.G, colour.B);
}

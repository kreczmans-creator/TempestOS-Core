using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tempest.Companion.Theming;

/// <summary>
/// The TempestOS brand palette — the platform's visual identity, realised
/// concretely for the first time by `WP 14.0A` (the `WP 10.0A` Visual
/// Design System deliberately deferred "concrete colour values" to an
/// implementation phase; this is that phase for the Companion). Three
/// brand colours, used semantically: Royal Blue is TempestOS identity
/// (chrome, wordmark, primary emphasis); Electric Blue is the interactive
/// accent (links, focus, live-status); Purple marks command/palette
/// surfaces. Never decoration — every surface not carrying meaning stays
/// neutral.
/// </summary>
/// <remarks>
/// Theme-reactive exactly as <c>Tempest.Desktop.Theming.ApplicationPalette</c>
/// established (`WP 10.5A`): every key is registered under both
/// <see cref="ThemeVariant.Light"/> and <see cref="ThemeVariant.Dark"/>
/// in a real <see cref="ResourceDictionary.ThemeDictionaries"/> entry, so
/// controls binding through <c>GetResourceObservable</c> re-resolve on
/// theme change with no manual repaint code.
/// </remarks>
public static class BrandPalette
{
    /// <summary>TempestOS Royal Blue — the primary brand colour.</summary>
    public static readonly Color RoyalBlue = Color.Parse("#1E2F97");

    /// <summary>TempestOS Electric Blue — the interactive/live accent.</summary>
    public static readonly Color ElectricBlue = Color.Parse("#00AEEF");

    /// <summary>TempestOS Purple — the command/palette accent.</summary>
    public static readonly Color Purple = Color.Parse("#6C2BD9");

    /// <summary>The app bar / brand chrome background.</summary>
    public const string ChromeBackgroundBrushKey = "Tempest.Companion.ChromeBackgroundBrush";

    /// <summary>Foreground rendered on brand chrome.</summary>
    public const string ChromeForegroundBrushKey = "Tempest.Companion.ChromeForegroundBrush";

    /// <summary>The page background behind cards.</summary>
    public const string PageBackgroundBrushKey = "Tempest.Companion.PageBackgroundBrush";

    /// <summary>A raised card's own background.</summary>
    public const string CardBackgroundBrushKey = "Tempest.Companion.CardBackgroundBrush";

    /// <summary>A raised card's own border.</summary>
    public const string CardBorderBrushKey = "Tempest.Companion.CardBorderBrush";

    /// <summary>Secondary/caption text.</summary>
    public const string SecondaryTextBrushKey = "Tempest.Companion.SecondaryTextBrush";

    /// <summary>The interactive accent (Electric Blue in both variants — the brand's own constant).</summary>
    public const string AccentBrushKey = "Tempest.Companion.AccentBrush";

    /// <summary>The command/palette accent (Purple in both variants).</summary>
    public const string CommandAccentBrushKey = "Tempest.Companion.CommandAccentBrush";

    /// <summary>The bottom navigation bar background.</summary>
    public const string NavBarBackgroundBrushKey = "Tempest.Companion.NavBarBackgroundBrush";

    /// <summary>A selected navigation item's own foreground.</summary>
    public const string NavSelectedBrushKey = "Tempest.Companion.NavSelectedBrush";

    /// <summary>An unselected navigation item's own foreground.</summary>
    public const string NavUnselectedBrushKey = "Tempest.Companion.NavUnselectedBrush";

    /// <summary>
    /// Registers every key above into <paramref name="app"/>'s own
    /// resources. Safe to call more than once — adds one more identical,
    /// harmless merged dictionary, the exact idempotence shape
    /// <c>ApplicationPalette.Register</c> documented for headless test
    /// hosts.
    /// </summary>
    public static void Register(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var dictionary = new ResourceDictionary();

        var light = new ResourceDictionary
        {
            [ChromeBackgroundBrushKey] = new SolidColorBrush(RoyalBlue),
            [ChromeForegroundBrushKey] = new SolidColorBrush(Colors.White),
            [PageBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#F2F4F8")),
            [CardBackgroundBrushKey] = new SolidColorBrush(Colors.White),
            [CardBorderBrushKey] = new SolidColorBrush(Color.Parse("#D5DAE4")),
            [SecondaryTextBrushKey] = new SolidColorBrush(Color.Parse("#5A6272")),
            [AccentBrushKey] = new SolidColorBrush(ElectricBlue),
            [CommandAccentBrushKey] = new SolidColorBrush(Purple),
            [NavBarBackgroundBrushKey] = new SolidColorBrush(Colors.White),
            [NavSelectedBrushKey] = new SolidColorBrush(RoyalBlue),
            [NavUnselectedBrushKey] = new SolidColorBrush(Color.Parse("#7A8194")),
        };

        var dark = new ResourceDictionary
        {
            [ChromeBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#141B4D")),
            [ChromeForegroundBrushKey] = new SolidColorBrush(Color.Parse("#EAECF5")),
            [PageBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#14161C")),
            [CardBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#1E2129")),
            [CardBorderBrushKey] = new SolidColorBrush(Color.Parse("#343946")),
            [SecondaryTextBrushKey] = new SolidColorBrush(Color.Parse("#9AA1B4")),
            [AccentBrushKey] = new SolidColorBrush(ElectricBlue),
            [CommandAccentBrushKey] = new SolidColorBrush(Color.Parse("#9A6CF0")),
            [NavBarBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#1A1D26")),
            [NavSelectedBrushKey] = new SolidColorBrush(ElectricBlue),
            [NavUnselectedBrushKey] = new SolidColorBrush(Color.Parse("#79808F")),
        };

        dictionary.ThemeDictionaries[ThemeVariant.Light] = light;
        dictionary.ThemeDictionaries[ThemeVariant.Dark] = dark;

        app.Resources.MergedDictionaries.Add(dictionary);
    }

    /// <summary>
    /// Resolves <paramref name="key"/> against the current theme variant,
    /// falling back to a transparent brush if unregistered — used by
    /// C#-constructed views at build time, with theme-reactive binding via
    /// <see cref="Avalonia.Controls.ResourceNodeExtensions.GetResourceObservable(IResourceHost, object)"/>
    /// where a control must repaint on toggle.
    /// </summary>
    public static IBrush Brush(Application app, string key) =>
        app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Transparent;
}

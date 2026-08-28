using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tempest.Companion.Theming;

/// <summary>
/// The Tempest Engineering brand palette — every value transcribed from
/// the authoritative Tempest Engineering Design System (`WP 14.1A`;
/// `tokens/colors.css`/`tokens/semantic.css` of the supplied brand pack),
/// which supersedes `WP 14.0A`'s provisional colours. The identity is
/// instrument-dark first: near-black navy ground, one cyan interactive
/// accent, violet strictly secondary, indigo the accent of the paper
/// (light) theme; green/amber/red are reserved for machine state, never
/// decoration.
/// </summary>
/// <remarks>
/// <see cref="ThemeVariant.Dark"/> is the brand's home ground ("Dark
/// first" — the design system's own words) and the Companion's default;
/// <see cref="ThemeVariant.Light"/> maps to the pack's paper theme
/// (<c>.t-light</c>). Registered as real theme dictionaries, the
/// `ApplicationPalette` pattern, so controls binding via
/// <c>GetResourceObservable</c> re-resolve on toggle.
/// </remarks>
public static class BrandPalette
{
    // ------------------------------------------------------------
    // Base palette — verbatim from tokens/colors.css.
    // ------------------------------------------------------------

    /// <summary>--navy-900 — sunken surfaces (inputs, rails, log surfaces).</summary>
    public static readonly Color Navy900 = Color.Parse("#070915");

    /// <summary>--navy-800 — the page ground.</summary>
    public static readonly Color Navy800 = Color.Parse("#0b0e1e");

    /// <summary>--navy-700 — a panel/card surface.</summary>
    public static readonly Color Navy700 = Color.Parse("#111527");

    /// <summary>--navy-600 — a raised surface.</summary>
    public static readonly Color Navy600 = Color.Parse("#181d33");

    /// <summary>--navy-400 — the strong border.</summary>
    public static readonly Color Navy400 = Color.Parse("#2e3552");

    /// <summary>--paper-050 — headings on dark; the paper theme's page ground.</summary>
    public static readonly Color Paper050 = Color.Parse("#f5f6fa");

    /// <summary>--paper-000 — the paper theme's surface.</summary>
    public static readonly Color Paper000 = Color.Parse("#ffffff");

    /// <summary>--paper-100 — the paper theme's sunken surface.</summary>
    public static readonly Color Paper100 = Color.Parse("#eff0f5");

    /// <summary>--slate-400 — muted text on dark.</summary>
    public static readonly Color Slate400 = Color.Parse("#a2a5af");

    /// <summary>--slate-500 — faint text.</summary>
    public static readonly Color Slate500 = Color.Parse("#82848e");

    /// <summary>--slate-600 — muted text on paper.</summary>
    public static readonly Color Slate600 = Color.Parse("#4b5160");

    /// <summary>--slate-700 — body text on paper.</summary>
    public static readonly Color Slate700 = Color.Parse("#31343f");

    /// <summary>--ink-900 — headings on paper; text on a cyan fill.</summary>
    public static readonly Color Ink900 = Color.Parse("#16181d");

    /// <summary>--indigo-600 — brand indigo, the paper theme's accent (read off the mark's outer strokes).</summary>
    public static readonly Color Indigo600 = Color.Parse("#1c2d97");

    /// <summary>--cyan-500 — brand cyan, THE interactive accent on dark (the mark's middle strokes).</summary>
    public static readonly Color Cyan500 = Color.Parse("#40a2ce");

    /// <summary>--cyan-400 — cyan's hover step.</summary>
    public static readonly Color Cyan400 = Color.Parse("#68bde2");

    /// <summary>--cyan-600 — cyan's press step.</summary>
    public static readonly Color Cyan600 = Color.Parse("#2b7fa5");

    /// <summary>--violet-500 — brand violet, strictly secondary (badges, category rules — never the primary CTA; the mark's inner strokes).</summary>
    public static readonly Color Violet500 = Color.Parse("#6c29d9");

    /// <summary>--green-500 — machine-state success. Reserved for state, never decoration.</summary>
    public static readonly Color Green500 = Color.Parse("#12b981");

    /// <summary>--amber-500 — machine-state warning.</summary>
    public static readonly Color Amber500 = Color.Parse("#f5a524");

    /// <summary>--red-500 — machine-state danger.</summary>
    public static readonly Color Red500 = Color.Parse("#e5484d");

    // ------------------------------------------------------------
    // Semantic keys — the pack's semantic.css aliases, as theme-reactive
    // Avalonia resources.
    // ------------------------------------------------------------

    /// <summary>The page ground (--bg-page).</summary>
    public const string PageBackgroundBrushKey = "Tempest.Companion.PageBackgroundBrush";

    /// <summary>A card/panel surface (--surface-card).</summary>
    public const string CardBackgroundBrushKey = "Tempest.Companion.CardBackgroundBrush";

    /// <summary>A card/panel hairline (--surface-card-border).</summary>
    public const string CardBorderBrushKey = "Tempest.Companion.CardBorderBrush";

    /// <summary>A sunken surface — the app bar, nav rail, inputs (--bg-surface-sunken/--bg-input).</summary>
    public const string SunkenBackgroundBrushKey = "Tempest.Companion.SunkenBackgroundBrush";

    /// <summary>Heading text (--text-heading).</summary>
    public const string HeadingTextBrushKey = "Tempest.Companion.HeadingTextBrush";

    /// <summary>Body text (--text-body).</summary>
    public const string BodyTextBrushKey = "Tempest.Companion.BodyTextBrush";

    /// <summary>Muted/secondary text (--text-muted).</summary>
    public const string SecondaryTextBrushKey = "Tempest.Companion.SecondaryTextBrush";

    /// <summary>The interactive accent (--accent-primary): cyan on dark, indigo on paper.</summary>
    public const string AccentBrushKey = "Tempest.Companion.AccentBrush";

    /// <summary>The secondary brand tint (--accent-secondary): violet in both themes.</summary>
    public const string CommandAccentBrushKey = "Tempest.Companion.CommandAccentBrush";

    /// <summary>Text set on an accent fill (--on-accent).</summary>
    public const string OnAccentBrushKey = "Tempest.Companion.OnAccentBrush";

    /// <summary>A selected item's fill (--bg-selected, 12% cyan).</summary>
    public const string SelectedBackgroundBrushKey = "Tempest.Companion.SelectedBackgroundBrush";

    /// <summary>An unselected navigation item's foreground.</summary>
    public const string NavUnselectedBrushKey = "Tempest.Companion.NavUnselectedBrush";

    /// <summary>Legacy aliases kept for the shell's chrome wiring — the app bar is a sunken instrument surface, not a filled brand banner.</summary>
    public const string ChromeBackgroundBrushKey = SunkenBackgroundBrushKey;

    /// <summary>Foreground on chrome — the heading brush.</summary>
    public const string ChromeForegroundBrushKey = HeadingTextBrushKey;

    /// <summary>The nav bar shares the sunken chrome surface.</summary>
    public const string NavBarBackgroundBrushKey = SunkenBackgroundBrushKey;

    /// <summary>A selected navigation item's foreground — the accent.</summary>
    public const string NavSelectedBrushKey = AccentBrushKey;

    /// <summary>
    /// Registers every key above into <paramref name="app"/>'s own
    /// resources. Safe to call more than once — adds one more identical,
    /// harmless merged dictionary (the headless-test idempotence shape
    /// `ApplicationPalette.Register` documented).
    /// </summary>
    public static void Register(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var dictionary = new ResourceDictionary();

        // The instrument (dark) theme — the brand's home ground.
        var dark = new ResourceDictionary
        {
            [PageBackgroundBrushKey] = new SolidColorBrush(Navy800),
            [CardBackgroundBrushKey] = new SolidColorBrush(Navy700),
            [CardBorderBrushKey] = new SolidColorBrush(Paper050, 0.08),
            [SunkenBackgroundBrushKey] = new SolidColorBrush(Navy900),
            [HeadingTextBrushKey] = new SolidColorBrush(Paper050),
            [BodyTextBrushKey] = new SolidColorBrush(Paper050, 0.82),
            [SecondaryTextBrushKey] = new SolidColorBrush(Slate400),
            [AccentBrushKey] = new SolidColorBrush(Cyan500),
            [CommandAccentBrushKey] = new SolidColorBrush(Violet500),
            [OnAccentBrushKey] = new SolidColorBrush(Navy900),
            [SelectedBackgroundBrushKey] = new SolidColorBrush(Cyan500, 0.12),
            [NavUnselectedBrushKey] = new SolidColorBrush(Slate500),
        };

        // The paper (light) theme — documents and daylight reading.
        var light = new ResourceDictionary
        {
            [PageBackgroundBrushKey] = new SolidColorBrush(Paper050),
            [CardBackgroundBrushKey] = new SolidColorBrush(Paper000),
            [CardBorderBrushKey] = new SolidColorBrush(Ink900, 0.08),
            [SunkenBackgroundBrushKey] = new SolidColorBrush(Paper100),
            [HeadingTextBrushKey] = new SolidColorBrush(Ink900),
            [BodyTextBrushKey] = new SolidColorBrush(Slate700),
            [SecondaryTextBrushKey] = new SolidColorBrush(Slate600),
            [AccentBrushKey] = new SolidColorBrush(Indigo600),
            [CommandAccentBrushKey] = new SolidColorBrush(Violet500),
            [OnAccentBrushKey] = new SolidColorBrush(Paper050),
            [SelectedBackgroundBrushKey] = new SolidColorBrush(Cyan500, 0.12),
            [NavUnselectedBrushKey] = new SolidColorBrush(Slate500),
        };

        dictionary.ThemeDictionaries[ThemeVariant.Dark] = dark;
        dictionary.ThemeDictionaries[ThemeVariant.Light] = light;

        app.Resources.MergedDictionaries.Add(dictionary);
    }

    /// <summary>
    /// Resolves <paramref name="key"/> against the current theme variant,
    /// falling back to a transparent brush if unregistered.
    /// </summary>
    public static IBrush Brush(Application app, string key) =>
        app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Transparent;
}

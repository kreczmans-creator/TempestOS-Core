using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The Tempest Engineering brand palette for the Desktop shell — every
/// base value transcribed verbatim from the authoritative Tempest
/// Engineering Design System (`docs/design/Tempest Engineering Design
/// System Reference.md`, supplied at `WP 14.1A` and first implemented by
/// the Companion; recovered here for the Desktop, closing `FCR-0092`).
/// The identity is instrument-dark first: near-black navy ground, one
/// cyan interactive accent, violet strictly secondary, indigo the accent
/// of the paper (light) theme; green/amber/red are reserved for machine
/// state, never decoration.
/// </summary>
/// <remarks>
/// <para>
/// Every semantic key below is registered as a real
/// <see cref="ResourceDictionary.ThemeDictionaries"/> entry (Dark and
/// Light both explicit), the identical mechanism <see cref="ApplicationPalette"/>
/// established (`WP 10.5A`), so anything bound through
/// <see cref="ThemeReactiveBrush"/> re-resolves the moment
/// <see cref="ThemeService"/> switches the variant — no repaint code
/// anywhere.
/// </para>
/// <para>
/// <see cref="ThemeVariant.Dark"/> is the brand's home ground ("Dark
/// first" — the design system's own words); <see cref="ThemeVariant.Light"/>
/// maps to the pack's paper theme, for documents and daylight reading.
/// </para>
/// </remarks>
public static class BrandPalette
{
    // ------------------------------------------------------------
    // Base palette — verbatim from tokens/colors.css.
    // ------------------------------------------------------------

    /// <summary>--navy-900 — sunken surfaces (chrome bars, rails, inputs).</summary>
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

    /// <summary>--indigo-600 — brand indigo, the paper theme's accent (the mark's outer strokes).</summary>
    public static readonly Color Indigo600 = Color.Parse("#1c2d97");

    /// <summary>--cyan-500 — brand cyan, THE interactive accent on dark (the mark's middle strokes).</summary>
    public static readonly Color Cyan500 = Color.Parse("#40a2ce");

    /// <summary>--cyan-400 — cyan's hover step.</summary>
    public static readonly Color Cyan400 = Color.Parse("#68bde2");

    /// <summary>
    /// --cyan-600 — cyan's press step. Lightened from the design system's
    /// original `#2b7fa5` (review board finding #6, `WP 16.5A-R1`):
    /// `AccentPressBrushKey`'s own text (`OnAccentBrushKey` =
    /// <see cref="Navy900"/> in the dark theme) measured only 4.42:1 on
    /// it, under WCAG AA's 4.5:1 body-text floor. Hue/saturation
    /// preserved, lightness nudged just enough to clear 4.5:1 (measured
    /// 4.60:1) — see `WP16.5A-R1 Accessibility Remediation.md` for the
    /// full before/after table.
    /// </summary>
    public static readonly Color Cyan600 = Color.Parse("#2c82a9");

    /// <summary>--violet-500 — brand violet, strictly secondary (badges, category rules; the mark's inner strokes).</summary>
    public static readonly Color Violet500 = Color.Parse("#6c29d9");

    /// <summary>--green-500 — machine-state success. Reserved for state, never decoration.</summary>
    public static readonly Color Green500 = Color.Parse("#12b981");

    /// <summary>--amber-500 — machine-state warning.</summary>
    public static readonly Color Amber500 = Color.Parse("#f5a524");

    /// <summary>--red-500 — machine-state danger.</summary>
    public static readonly Color Red500 = Color.Parse("#e5484d");

    // ------------------------------------------------------------
    // Semantic keys — the pack's semantic.css aliases, as theme-reactive
    // Avalonia resources. Every surface in the shell paints from these.
    // ------------------------------------------------------------

    /// <summary>The page ground (--bg-page).</summary>
    public const string PageBackgroundBrushKey = "Tempest.Brand.PageBackgroundBrush";

    /// <summary>A card/panel surface (--surface-card).</summary>
    public const string SurfaceBackgroundBrushKey = "Tempest.Brand.SurfaceBackgroundBrush";

    /// <summary>A raised surface — a menu, flyout, tooltip, hovered row.</summary>
    public const string RaisedBackgroundBrushKey = "Tempest.Brand.RaisedBackgroundBrush";

    /// <summary>A sunken surface — the header bar, the rail, the status bar, inputs (--bg-surface-sunken/--bg-input).</summary>
    public const string SunkenBackgroundBrushKey = "Tempest.Brand.SunkenBackgroundBrush";

    /// <summary>The 8% hairline every card, panel and bar edge uses.</summary>
    public const string HairlineBrushKey = "Tempest.Brand.HairlineBrush";

    /// <summary>The 14% strong hairline — a focused input, an active tab strip's own rule.</summary>
    public const string HairlineStrongBrushKey = "Tempest.Brand.HairlineStrongBrush";

    /// <summary>Heading text (--text-heading).</summary>
    public const string HeadingTextBrushKey = "Tempest.Brand.HeadingTextBrush";

    /// <summary>Body text (--text-body).</summary>
    public const string BodyTextBrushKey = "Tempest.Brand.BodyTextBrush";

    /// <summary>Muted/secondary text (--text-muted).</summary>
    public const string MutedTextBrushKey = "Tempest.Brand.MutedTextBrush";

    /// <summary>Faint text — captions, disabled labels, separators.</summary>
    public const string FaintTextBrushKey = "Tempest.Brand.FaintTextBrush";

    /// <summary>The interactive accent (--accent-primary): cyan on dark, indigo on paper.</summary>
    public const string AccentBrushKey = "Tempest.Brand.AccentBrush";

    /// <summary>The accent's hover step.</summary>
    public const string AccentHoverBrushKey = "Tempest.Brand.AccentHoverBrush";

    /// <summary>The accent's press step.</summary>
    public const string AccentPressBrushKey = "Tempest.Brand.AccentPressBrush";

    /// <summary>Text set on an accent fill (--on-accent).</summary>
    public const string OnAccentBrushKey = "Tempest.Brand.OnAccentBrush";

    /// <summary>The secondary brand tint (--accent-secondary): violet in both themes.</summary>
    public const string SecondaryAccentBrushKey = "Tempest.Brand.SecondaryAccentBrush";

    /// <summary>A selected item's fill (--bg-selected, 12% cyan).</summary>
    public const string SelectedBackgroundBrushKey = "Tempest.Brand.SelectedBackgroundBrush";

    /// <summary>A hovered item's wash (5% paper on dark, 5% ink on paper).</summary>
    public const string HoverBackgroundBrushKey = "Tempest.Brand.HoverBackgroundBrush";

    /// <summary>The faint blueprint grid line the page ground carries (5.5% cyan) — never behind body text.</summary>
    public const string GridLineBrushKey = "Tempest.Brand.GridLineBrush";

    /// <summary>Machine-state success.</summary>
    public const string SuccessBrushKey = "Tempest.Brand.SuccessBrush";

    /// <summary>Machine-state warning.</summary>
    public const string WarningBrushKey = "Tempest.Brand.WarningBrush";

    /// <summary>Machine-state danger.</summary>
    public const string DangerBrushKey = "Tempest.Brand.DangerBrush";

    /// <summary>
    /// Registers every key above into <paramref name="app"/>'s own
    /// resources. Safe to call more than once — adds one more identical,
    /// harmless merged dictionary (the headless-test idempotence shape
    /// <see cref="ApplicationPalette.Register"/> documented).
    /// </summary>
    public static void Register(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var dictionary = new ResourceDictionary();

        // The instrument (dark) theme — the brand's home ground.
        var dark = new ResourceDictionary
        {
            [PageBackgroundBrushKey] = Solid(Navy800),
            [SurfaceBackgroundBrushKey] = Solid(Navy700),
            [RaisedBackgroundBrushKey] = Solid(Navy600),
            [SunkenBackgroundBrushKey] = Solid(Navy900),
            [HairlineBrushKey] = Solid(Paper050, 0.08),
            [HairlineStrongBrushKey] = Solid(Paper050, 0.14),
            [HeadingTextBrushKey] = Solid(Paper050),
            [BodyTextBrushKey] = Solid(Paper050, 0.82),
            [MutedTextBrushKey] = Solid(Slate400),
            [FaintTextBrushKey] = Solid(Slate500),
            [AccentBrushKey] = Solid(Cyan500),
            [AccentHoverBrushKey] = Solid(Cyan400),
            [AccentPressBrushKey] = Solid(Cyan600),
            [OnAccentBrushKey] = Solid(Navy900),
            [SecondaryAccentBrushKey] = Solid(Violet500),
            [SelectedBackgroundBrushKey] = Solid(Cyan500, 0.12),
            [HoverBackgroundBrushKey] = Solid(Paper050, 0.05),
            [GridLineBrushKey] = Solid(Cyan500, 0.055),
            [SuccessBrushKey] = Solid(Green500),
            [WarningBrushKey] = Solid(Amber500),
            [DangerBrushKey] = Solid(Red500),
        };

        // The paper (light) theme — documents and daylight reading.
        var light = new ResourceDictionary
        {
            [PageBackgroundBrushKey] = Solid(Paper050),
            [SurfaceBackgroundBrushKey] = Solid(Paper000),
            [RaisedBackgroundBrushKey] = Solid(Paper000),
            [SunkenBackgroundBrushKey] = Solid(Paper100),
            [HairlineBrushKey] = Solid(Ink900, 0.08),
            [HairlineStrongBrushKey] = Solid(Ink900, 0.14),
            [HeadingTextBrushKey] = Solid(Ink900),
            [BodyTextBrushKey] = Solid(Slate700),
            [MutedTextBrushKey] = Solid(Slate600),
            // Darkened from the raw `Slate500` (review board finding #6,
            // `WP 16.5A-R1`) — `FaintTextBrushKey` is real text (captions,
            // disabled labels, separators), and `Slate500` measured only
            // 3.72:1 on `Surface`/`Paper000` and 3.27:1 on
            // `Sunken`/`Paper100`, both well under WCAG AA's 4.5:1 floor.
            // Hue/saturation preserved, lightness nudged just enough to
            // clear 4.5:1 against both real surfaces this key is drawn on
            // (measured 5.30:1 / 4.66:1) — the same darken-for-light-theme
            // treatment `ApplicationPalette`'s own `HealthText*` keys
            // already used for the same reason. The dark theme's own
            // value is untouched: `Slate500` already clears 4.5:1 on the
            // dark Cockpit surfaces this key is drawn on. See
            // `WP16.5A-R1 Accessibility Remediation.md` for the full
            // before/after table.
            [FaintTextBrushKey] = Solid(Color.Parse("#696b75")),
            [AccentBrushKey] = Solid(Indigo600),
            [AccentHoverBrushKey] = Solid(Color.Parse("#2a3db0")),
            [AccentPressBrushKey] = Solid(Color.Parse("#16247a")),
            [OnAccentBrushKey] = Solid(Paper050),
            [SecondaryAccentBrushKey] = Solid(Violet500),
            [SelectedBackgroundBrushKey] = Solid(Cyan500, 0.12),
            [HoverBackgroundBrushKey] = Solid(Ink900, 0.05),
            [GridLineBrushKey] = Solid(Indigo600, 0.055),
            [SuccessBrushKey] = Solid(Color.Parse("#0f9a6c")),
            [WarningBrushKey] = Solid(Color.Parse("#c97f0c")),
            // Darkened from `#d03a3f` (review board finding #6,
            // `WP 16.5A-R1`): the `Danger` treatment's own `:pointerover`
            // text (`OnAccentBrushKey` = `Paper050`) measured only 4.46:1
            // on it, under WCAG AA's 4.5:1 floor. Hue/saturation
            // preserved, lightness nudged just enough to clear 4.5:1
            // (measured 4.60:1); the dark theme's `Red500` already clears
            // 4.5:1 for the same pair (measured 5.07:1) and is untouched.
            // See `WP16.5A-R1 Accessibility Remediation.md` for the full
            // before/after table.
            [DangerBrushKey] = Solid(Color.Parse("#cf353b")),
        };

        dictionary.ThemeDictionaries[ThemeVariant.Dark] = dark;
        dictionary.ThemeDictionaries[ThemeVariant.Light] = light;

        app.Resources.MergedDictionaries.Add(dictionary);
    }

    /// <summary>
    /// Resolves <paramref name="key"/> against <paramref name="variant"/>
    /// (or the application's current variant), falling back to a
    /// transparent brush if unregistered — never an exception, so a
    /// control constructed before the palette is registered (a headless
    /// test) still renders.
    /// </summary>
    public static IBrush Brush(string key, ThemeVariant? variant = null)
    {
        var app = Application.Current;
        if (app is not null && app.TryGetResource(key, variant ?? app.ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    /// <summary>An immutable solid brush over <paramref name="colour"/> at <paramref name="opacity"/> — immutable so a shared resource can never be mutated by one consumer.</summary>
    private static IBrush Solid(Color colour, double opacity = 1.0) => new ImmutableSolidColorBrush(colour, opacity);
}

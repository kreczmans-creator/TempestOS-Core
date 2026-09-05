using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 16.5A` (`TD-65`) — the declared <c>FocusRing</c> tokens
/// (<see cref="DesignTokens.FocusRingThickness"/>,
/// <see cref="ApplicationPalette.FocusRingBrushKey"/>) previously had no
/// consumer; <see cref="ChromeStyles.Install"/> now draws a real ring on
/// Avalonia's own <c>:focus-visible</c> pseudo-class. Avalonia decides
/// <c>:focus-visible</c> itself, and only for real keyboard-device
/// navigation — two things confirmed directly before writing these
/// tests: setting the pseudo-class by hand via
/// <see cref="Classes.Set(string, bool)"/> throws
/// <see cref="ArgumentException"/> ("may only be added by the control
/// itself"), and calling
/// <see cref="InputElement.Focus(NavigationMethod, KeyModifiers)"/> with
/// <see cref="NavigationMethod.Tab"/> directly is <em>not</em> enough
/// either (it lands `:focus`/`:focus-within` but not `:focus-visible` —
/// Avalonia tracks the last real input device separately). A genuine
/// <c>Tab</c> keypress driven through the real headless input pipeline
/// (<see cref="HeadlessWindowExtensions.KeyPressQwerty"/>) is what
/// actually triggers it, so these tests use that — proving the style
/// resolves for a real keyboard interaction, not a hand-simulated
/// shortcut.
/// </summary>
public sealed class FocusVisibleStyleTests
{
    [AvaloniaTheory]
    [InlineData(ChromeStylesTreatment.Flat)]
    [InlineData(ChromeStylesTreatment.Subtle)]
    [InlineData(ChromeStylesTreatment.Primary)]
    [InlineData(ChromeStylesTreatment.Danger)]
    public void ButtonTreatments_RealTabKeypress_DrawsTheFocusRing(ChromeStylesTreatment treatment)
    {
        var button = new Button { Content = "Test" };
        button.Classes.Add(TreatmentClass(treatment));

        var window = new Window { Content = button };
        ChromeStyles.Install(window);
        window.Show();

        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();

        // Review board finding #1 (`WP 16.5A-R1`): `Primary` no longer
        // draws the generic ring — `AccentBrushKey` (its own rest fill)
        // resolves to the exact same colour as `FocusRingBrushKey` in
        // both themes (measured 1.00:1), so it draws its own foreground
        // colour (`OnAccentBrushKey`) instead, which is guaranteed to
        // contrast with its own fill. `Flat`/`Subtle`/`Danger`-at-rest
        // keep the original generic ring — see
        // <see cref="ButtonTreatments_FocusRing_DiffersFromAndContrastsWithEveryOpaqueFillItBorders"/>
        // for the hovered-and-focused case this test does not exercise.
        var expectedRing = BrandPalette.Brush(ExpectedRestFocusRingKey(treatment));

        // Not focused yet — the ring must not be there uninvited.
        Assert.NotEqual(expectedRing, presenter.BorderBrush);

        // A real Tab keypress, through the actual headless input pipeline
        // — the one thing that genuinely sets `:focus-visible` (a plain
        // `Focus(NavigationMethod.Tab)` call does not: confirmed directly,
        // see the class remarks).
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.True(button.IsFocused, "The button was not the window's own first tab stop.");
        Assert.Contains(":focus-visible", button.Classes);
        Assert.Equal(expectedRing, presenter.BorderBrush);
        Assert.Equal(new Avalonia.Thickness(DesignTokens.FocusRingThickness), presenter.BorderThickness);
    }

    /// <summary>
    /// Review board finding #1 (`WP 16.5A-R1`) — the fix. The pre-existing
    /// test above only ever asserted <c>presenter.BorderBrush == focusRing</c>,
    /// which cannot see a ring painted the exact same colour as its own
    /// background: <c>Primary</c>'s rest fill (<see cref="BrandPalette.AccentBrushKey"/>)
    /// resolved to the identical colour as the generic
    /// <see cref="ApplicationPalette.FocusRingBrushKey"/> in both themes
    /// (measured 1.00:1 — a keyboard user tabbing to Save/Continue/Enter
    /// Engineering saw no focus indicator at all), and <c>Danger</c>'s own
    /// <c>:pointerover</c> fill (<see cref="BrandPalette.DangerBrushKey"/>)
    /// failed the generic ring's own 3:1 floor too (measured 2.33:1 light,
    /// 1.36:1 dark) once also focused. This test resolves the presenter's
    /// real, currently-active border brush and background brush — through
    /// the same real keyboard-Tab (and, for the second half, a real
    /// pointer-hover) input pipeline the class's own remarks establish is
    /// the only genuine way to set <c>:focus-visible</c> — and computes
    /// the actual WCAG contrast ratio between them, for every treatment in
    /// both themes, rather than trusting a hard-coded table.
    /// </summary>
    /// <remarks>
    /// Each theme is exercised via <see cref="TopLevel.RequestedThemeVariant"/>
    /// set once on this test's own <see cref="Window"/> before
    /// <see cref="Window.Show"/> — a per-window, local override, not the
    /// shared, process-wide <see cref="Avalonia.Application.RequestedThemeVariant"/>
    /// <see cref="VisualPolishTests"/> already found genuinely flaky to
    /// toggle-and-reread live under this suite's own parallel execution.
    /// No live toggle happens here at all: each of the eight
    /// treatment/theme combinations below is a fresh window, set once.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ChromeStylesTreatment.Flat, "Light")]
    [InlineData(ChromeStylesTreatment.Flat, "Dark")]
    [InlineData(ChromeStylesTreatment.Subtle, "Light")]
    [InlineData(ChromeStylesTreatment.Subtle, "Dark")]
    [InlineData(ChromeStylesTreatment.Primary, "Light")]
    [InlineData(ChromeStylesTreatment.Primary, "Dark")]
    [InlineData(ChromeStylesTreatment.Danger, "Light")]
    [InlineData(ChromeStylesTreatment.Danger, "Dark")]
    public void ButtonTreatments_FocusRing_DiffersFromAndContrastsWithEveryOpaqueFillItBorders(ChromeStylesTreatment treatment, string themeName)
    {
        var theme = themeName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        var button = new Button { Content = "Test" };
        button.Classes.Add(TreatmentClass(treatment));

        var window = new Window { RequestedThemeVariant = theme, Content = button };
        ChromeStyles.Install(window);
        window.Show();

        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.True(button.IsFocused, "The button was not the window's own first tab stop.");
        Assert.Contains(":focus-visible", button.Classes);

        var failures = new List<string>();
        var measured = new List<string>();

        CheckState("focused, not hovered");

        // Also drive a real pointer hover while still keyboard-focused —
        // `Danger`'s own defect only shows up in exactly this combination
        // (its rest fill is transparent; only `:pointerover` fills it with
        // a colour the ring can collide with). Hovering does not clear
        // `:focus-visible` — only a pointer-driven focus *change* does
        // (see <see cref="ButtonTreatments_PointerFocus_NeverDrawsTheFocusRing"/>).
        window.MouseMove(button.Bounds.Center, RawInputModifiers.None);
        Assert.Contains(":pointerover", button.Classes);
        Assert.Contains(":focus-visible", button.Classes);
        CheckState("focused and hovered");

        Assert.True(failures.Count == 0,
            $"{treatment}/{theme} focus-ring contrast failures:\n" + string.Join('\n', failures) +
            "\n\nAll measured:\n" + string.Join('\n', measured));

        void CheckState(string label)
        {
            var ring = presenter.BorderBrush;
            var background = presenter.Background;

            // Only a real, fully-opaque solid fill is a genuine adjacency
            // the ring must clear 3:1 against. A fully transparent rest
            // fill (`Flat`/`Danger` at rest, `Brushes.Transparent` — a
            // zero-alpha `ISolidColorBrush`) or a translucent brand wash
            // (`Flat`'s own 5%/12% tints — a fully-opaque `Color` carried
            // at a fractional `IBrush.Opacity`, confirmed directly: the
            // `Color` itself never encodes the wash's own alpha) lets the
            // real page/parent background show through — a colour this
            // button's own resolved brushes cannot tell us, and (per
            // `ButtonTreatments_PointerFocus_NeverDrawsTheFocusRing`'s own
            // remarks) a coincidental match there would not be evidence
            // either way.
            if (ring is not ISolidColorBrush ringSolid || background is not ISolidColorBrush bgSolid || bgSolid.Color.A != 255 || bgSolid.Opacity < 1.0)
            {
                measured.Add($"{treatment}/{theme} {label}: ring={Describe(ring)} background={Describe(background)} — not a real opaque adjacency, skipped.");
                return;
            }

            var ratio = ContrastRatio(ringSolid.Color, bgSolid.Color);
            measured.Add($"{treatment}/{theme} {label}: ring={ringSolid.Color} background={bgSolid.Color} ratio={ratio:F2}:1");

            if (ringSolid.Color == bgSolid.Color)
                failures.Add($"{treatment}/{theme} {label}: the focus ring is painted the exact same colour ({ringSolid.Color}) as its own background — invisible to a keyboard user.");
            else if (ratio < MinimumNonTextContrastRatio)
                failures.Add($"{treatment}/{theme} {label}: ring {ringSolid.Color} on background {bgSolid.Color} measured {ratio:F2}:1, need >= {MinimumNonTextContrastRatio}:1 (WCAG 1.4.11).");
        }
    }

    [AvaloniaTheory]
    [InlineData(ChromeStylesTreatment.Flat)]
    [InlineData(ChromeStylesTreatment.Subtle)]
    [InlineData(ChromeStylesTreatment.Primary)]
    [InlineData(ChromeStylesTreatment.Danger)]
    public void ButtonTreatments_PointerFocus_NeverDrawsTheFocusRing(ChromeStylesTreatment treatment)
    {
        var button = new Button { Content = "Test" };
        button.Classes.Add(TreatmentClass(treatment));

        var window = new Window { Content = button };
        ChromeStyles.Install(window);
        window.Show();

        // A real pointer click, through the same real input pipeline —
        // never shows the ring, exactly the distinction the token pair
        // exists for.
        window.MouseDown(button.Bounds.Center, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(button.Bounds.Center, MouseButton.Left, RawInputModifiers.None);

        Assert.True(button.IsFocused, "The click did not focus the button.");
        // The authoritative check: the pseudo-class itself is never set by
        // a pointer click. (Not also asserting the resolved `BorderBrush`
        // here — the pointer is still hovering the button after the click,
        // so `:pointerover`'s own border colour can coincide with the
        // focus ring's for a treatment/theme pairing where both happen to
        // resolve to the same brand accent — a real, harmless coincidence,
        // not evidence either way.)
        Assert.DoesNotContain(":focus-visible", button.Classes);
    }

    /// <summary>
    /// The generic fallback's own declared intent — a real
    /// <see cref="Style"/>, matching any <see cref="TemplatedControl"/>
    /// carrying the real <c>:focus-visible</c> pseudo-class, setting the
    /// real <see cref="ApplicationPalette.FocusRingBrushKey"/>/
    /// <see cref="DesignTokens.FocusRingThickness"/> tokens — verified
    /// structurally rather than by empirical resolution.
    /// </summary>
    /// <remarks>
    /// <b>Why not the same empirical, real-keypress proof as the four
    /// button treatments above.</b> Tried directly, against a plain
    /// unclassed <see cref="Button"/>, a <see cref="CheckBox"/>, and a
    /// <see cref="TextBox"/>, before settling on this shape: none actually
    /// shows the ring. Fluent's own built-in <c>ControlTheme</c> for every
    /// one of those already declares its own <c>:focus-visible</c>
    /// treatment at a specificity an externally-added, generically-scoped
    /// <see cref="Style"/> does not outrank (the four button-treatment
    /// styles above only win because <c>.Class(styleClass)</c> makes their
    /// own selector more specific than Fluent's). A real, disclosed
    /// limitation: the generic fallback, as scoped by the plan brief
    /// ("a generic fallback for focusable controls"), reaches an
    /// as-yet-unstyled custom <see cref="TemplatedControl"/> this shell
    /// might add later with no competing theme of its own — not every
    /// existing Fluent-templated control, which already has one.
    /// </remarks>
    [AvaloniaFact]
    public void GenericFallback_IsARealStyle_MatchingAnyTemplatedControlsFocusVisiblePseudoclass_WithTheFocusRingTokens()
    {
        var window = new Window();
        ChromeStyles.Install(window);

        var genericFallback = window.Styles.OfType<Style>().SingleOrDefault(style =>
            style.Selector!.ToString() == "TemplatedControl:focus-visible");

        Assert.NotNull(genericFallback);

        var borderBrushSetter = genericFallback!.Setters.OfType<Setter>().Single(s => s.Property == TemplatedControl.BorderBrushProperty);
        var borderThicknessSetter = genericFallback.Setters.OfType<Setter>().Single(s => s.Property == TemplatedControl.BorderThicknessProperty);

        Assert.IsType<DynamicResourceExtension>(borderBrushSetter.Value);
        Assert.Equal(ApplicationPalette.FocusRingBrushKey, ((DynamicResourceExtension)borderBrushSetter.Value!).ResourceKey);
        Assert.Equal(new Avalonia.Thickness(DesignTokens.FocusRingThickness), borderThicknessSetter.Value);
    }

    private static string TreatmentClass(ChromeStylesTreatment treatment) => treatment switch
    {
        ChromeStylesTreatment.Flat => ChromeStyles.Flat,
        ChromeStylesTreatment.Subtle => ChromeStyles.Subtle,
        ChromeStylesTreatment.Primary => ChromeStyles.Primary,
        ChromeStylesTreatment.Danger => ChromeStyles.Danger,
        _ => throw new ArgumentOutOfRangeException(nameof(treatment)),
    };

    /// <summary>The ring key each treatment resolves at rest (keyboard-focused, not hovered) — `Primary` alone uses its own foreground instead of the generic ring (review board finding #1).</summary>
    private static string ExpectedRestFocusRingKey(ChromeStylesTreatment treatment) => treatment switch
    {
        ChromeStylesTreatment.Primary => BrandPalette.OnAccentBrushKey,
        _ => ApplicationPalette.FocusRingBrushKey,
    };

    /// <summary>WCAG 2.1 1.4.11's own floor for a UI component's own focus indicator.</summary>
    private const double MinimumNonTextContrastRatio = 3.0;

    private static string Describe(IBrush? brush) => brush switch
    {
        null => "(null)",
        ISolidColorBrush solid => solid.Color.ToString(),
        _ => brush.GetType().Name,
    };

    /// <summary>The real WCAG 2.1 relative-luminance/contrast-ratio formula (§1.4.3), computed directly rather than trusted from a comment — the identical formula <see cref="HealthColorContrastTests"/> already established for this suite.</summary>
    private static double ContrastRatio(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color colour)
    {
        double Channel(byte c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);
    }
}

/// <summary>The four <see cref="ChromeStyles"/> button treatments — test-only, so <see cref="Xunit.TheoryAttribute"/> can enumerate them without reaching for the internal string constants directly as inline data.</summary>
public enum ChromeStylesTreatment
{
    Flat,
    Subtle,
    Primary,
    Danger,
}

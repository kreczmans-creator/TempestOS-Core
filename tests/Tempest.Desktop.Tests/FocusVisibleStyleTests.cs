using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
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
        var focusRing = BrandPalette.Brush(ApplicationPalette.FocusRingBrushKey);

        // Not focused yet — the ring must not be there uninvited.
        Assert.NotEqual(focusRing, presenter.BorderBrush);

        // A real Tab keypress, through the actual headless input pipeline
        // — the one thing that genuinely sets `:focus-visible` (a plain
        // `Focus(NavigationMethod.Tab)` call does not: confirmed directly,
        // see the class remarks).
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.True(button.IsFocused, "The button was not the window's own first tab stop.");
        Assert.Contains(":focus-visible", button.Classes);
        Assert.Equal(focusRing, presenter.BorderBrush);
        Assert.Equal(new Avalonia.Thickness(DesignTokens.FocusRingThickness), presenter.BorderThickness);
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
}

/// <summary>The four <see cref="ChromeStyles"/> button treatments — test-only, so <see cref="Xunit.TheoryAttribute"/> can enumerate them without reaching for the internal string constants directly as inline data.</summary>
public enum ChromeStylesTreatment
{
    Flat,
    Subtle,
    Primary,
    Danger,
}

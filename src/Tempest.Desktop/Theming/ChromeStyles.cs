using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The shell's own three button treatments, as style classes — so a
/// navigation-rail item, a tab-strip tab, a panel chrome glyph and a
/// primary call-to-action each look like what they are, without any
/// view hand-painting a <see cref="Button"/>'s own background (which,
/// as a local value, would also silence the theme's own hover/press
/// feedback — the reason the pre-brand shell's flat buttons never
/// responded to the pointer).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="Flat"/> — transparent until hovered (a 5% wash), pressed (9%); rail items, tab-strip tabs, chrome glyphs, breadcrumbs.</description></item>
/// <item><description><see cref="Subtle"/> — a hairline-edged sunken surface; the header's search field, secondary actions.</description></item>
/// <item><description><see cref="Primary"/> — the accent fill with ink text; the one call-to-action a surface offers (Continue working, Enter Engineering, Save).</description></item>
/// </list>
/// Every brush is a <see cref="DynamicResourceExtension"/> over
/// <see cref="BrandPalette"/>'s own keys, so all three re-resolve on a
/// theme switch exactly like the rest of the shell.
/// </remarks>
internal static class ChromeStyles
{
    /// <summary>The flat treatment's own style class.</summary>
    public const string Flat = "tempest-flat";

    /// <summary>The subtle (hairline) treatment's own style class.</summary>
    public const string Subtle = "tempest-subtle";

    /// <summary>The primary (accent-filled) treatment's own style class.</summary>
    public const string Primary = "tempest-primary";

    /// <summary>A danger-tinted flat treatment — Delete/Discard confirmations.</summary>
    public const string Danger = "tempest-danger";

    /// <summary>Installs the three treatments into <paramref name="host"/>'s own styles — the main window, and any floating window, so a button classed anywhere beneath resolves them.</summary>
    public static void Install(StyledElement host)
    {
        ArgumentNullException.ThrowIfNull(host);

        // ---- Flat -------------------------------------------------------
        host.Styles.Add(Button(Flat,
            (Avalonia.Controls.Button.BackgroundProperty, Brushes.Transparent),
            (Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(0)),
            (Avalonia.Controls.Button.PaddingProperty, new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm + 1)),
            (Avalonia.Controls.Button.ForegroundProperty, Dyn(BrandPalette.BodyTextBrushKey))));
        host.Styles.Add(Presenter(Flat, ":pointerover",
            (ContentPresenter.BackgroundProperty, Dyn(BrandPalette.HoverBackgroundBrushKey)),
            (ContentPresenter.ForegroundProperty, Dyn(BrandPalette.HeadingTextBrushKey))));
        host.Styles.Add(Presenter(Flat, ":pressed",
            (ContentPresenter.BackgroundProperty, Dyn(BrandPalette.SelectedBackgroundBrushKey))));

        // ---- Subtle -----------------------------------------------------
        host.Styles.Add(Button(Subtle,
            (Avalonia.Controls.Button.BackgroundProperty, Dyn(BrandPalette.SunkenBackgroundBrushKey)),
            (Avalonia.Controls.Button.BorderBrushProperty, Dyn(BrandPalette.HairlineStrongBrushKey)),
            (Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(1)),
            (Avalonia.Controls.Button.ForegroundProperty, Dyn(BrandPalette.MutedTextBrushKey))));
        host.Styles.Add(Presenter(Subtle, ":pointerover",
            (ContentPresenter.BackgroundProperty, Dyn(BrandPalette.RaisedBackgroundBrushKey)),
            (ContentPresenter.BorderBrushProperty, Dyn(BrandPalette.AccentBrushKey)),
            (ContentPresenter.ForegroundProperty, Dyn(BrandPalette.HeadingTextBrushKey))));

        // ---- Primary ----------------------------------------------------
        host.Styles.Add(Button(Primary,
            (Avalonia.Controls.Button.BackgroundProperty, Dyn(BrandPalette.AccentBrushKey)),
            (Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(0)),
            (Avalonia.Controls.Button.ForegroundProperty, Dyn(BrandPalette.OnAccentBrushKey)),
            (Avalonia.Controls.Button.FontWeightProperty, FontWeight.SemiBold),
            (Avalonia.Controls.Button.PaddingProperty, new Thickness(DesignTokens.SpaceXl, DesignTokens.SpaceMd - 1))));
        host.Styles.Add(Presenter(Primary, ":pointerover",
            (ContentPresenter.BackgroundProperty, Dyn(BrandPalette.AccentHoverBrushKey)),
            (ContentPresenter.ForegroundProperty, Dyn(BrandPalette.OnAccentBrushKey))));
        host.Styles.Add(Presenter(Primary, ":pressed",
            (ContentPresenter.BackgroundProperty, Dyn(BrandPalette.AccentPressBrushKey)),
            (ContentPresenter.ForegroundProperty, Dyn(BrandPalette.OnAccentBrushKey))));

        // ---- Danger -----------------------------------------------------
        host.Styles.Add(Button(Danger,
            (Avalonia.Controls.Button.BackgroundProperty, Brushes.Transparent),
            (Avalonia.Controls.Button.BorderBrushProperty, Dyn(BrandPalette.DangerBrushKey)),
            (Avalonia.Controls.Button.BorderThicknessProperty, new Thickness(1)),
            (Avalonia.Controls.Button.ForegroundProperty, Dyn(BrandPalette.DangerBrushKey))));
        host.Styles.Add(Presenter(Danger, ":pointerover",
            (ContentPresenter.BackgroundProperty, Dyn(BrandPalette.DangerBrushKey)),
            (ContentPresenter.ForegroundProperty, Dyn(BrandPalette.OnAccentBrushKey))));

        // ---- Focus-visible (`WP 16.5A`, `TD-65`; review board finding #1,
        // `WP 16.5A-R1`) ----------------------------------------------------
        // The declared `FocusRing` tokens (`DesignTokens.FocusRingThickness`,
        // `ApplicationPalette.FocusRingBrushKey`) had no consumer anywhere
        // in the shell before this — a real, disclosed accessibility gap.
        // Avalonia sets the `:focus-visible` pseudo-class itself, and only
        // for keyboard-driven focus (never a pointer click), so targeting
        // it here draws the ring exactly for the users who need it, with
        // no click/tap ever showing one.
        //
        // `Primary` and `Danger` do NOT use the generic
        // `FocusRingBrushKey` ring below: both fill with a brand accent
        // wide enough to swallow it. `Primary.Button.Background` is
        // `AccentBrushKey`, which resolves to the exact same colour as
        // `FocusRingBrushKey` in both themes (measured 1.00:1 — the
        // review board's finding). `Danger`'s own `:pointerover` fills
        // with `DangerBrushKey`, against which the generic ring also
        // fails 3:1 (measured 2.33:1 light, 1.36:1 dark). Both are fixed
        // the same way: ring in `OnAccentBrushKey`, the treatment's own
        // foreground colour on that exact fill, so it is guaranteed to
        // contrast with whatever it borders (already ≥ 4.5:1 by the
        // contrast fixes above/below — see `WP16.5A-R1 Accessibility
        // Remediation.md` for every measured ratio). `Flat` and `Subtle`
        // keep the generic ring: neither ever fills with a colour the
        // ring itself uses.
        foreach (var styleClass in new[] { Flat, Subtle })
        {
            host.Styles.Add(Presenter(styleClass, ":focus-visible",
                (ContentPresenter.BorderBrushProperty, Dyn(ApplicationPalette.FocusRingBrushKey)),
                (ContentPresenter.BorderThicknessProperty, new Thickness(DesignTokens.FocusRingThickness))));
        }

        host.Styles.Add(Presenter(Primary, ":focus-visible",
            (ContentPresenter.BorderBrushProperty, Dyn(BrandPalette.OnAccentBrushKey)),
            (ContentPresenter.BorderThicknessProperty, new Thickness(DesignTokens.FocusRingThickness))));

        // `Danger` at rest (no pointer over it) fills transparent, so the
        // generic ring is fine there; only the hovered-and-focused
        // combination fills with `DangerBrushKey` and needs the
        // higher-contrast, own-foreground ring instead. Confirmed
        // directly against this codebase's real `Styles` resolution
        // (`FocusVisibleStyleTests`): two styles in the same `Styles`
        // collection that both match resolve by *addition order*, not by
        // selector complexity (the extra `:pointerover` class does not
        // itself win here) — so the plain rest-state rule is added
        // first, and the hover-specific override second, deliberately,
        // so the later one wins exactly when both are active.
        host.Styles.Add(Presenter(Danger, ":focus-visible",
            (ContentPresenter.BorderBrushProperty, Dyn(ApplicationPalette.FocusRingBrushKey)),
            (ContentPresenter.BorderThicknessProperty, new Thickness(DesignTokens.FocusRingThickness))));
        host.Styles.Add(PresenterStates(Danger, [":pointerover", ":focus-visible"],
            (ContentPresenter.BorderBrushProperty, Dyn(BrandPalette.OnAccentBrushKey)),
            (ContentPresenter.BorderThicknessProperty, new Thickness(DesignTokens.FocusRingThickness))));

        // A generic fallback for a focusable, templated control this shell
        // gives none of the four treatments above and that carries no
        // competing `:focus-visible` styling of its own. Disclosed limit,
        // found empirically (`FocusVisibleStyleTests`): Fluent's own
        // `ControlTheme` for `TextBox`/`CheckBox`/an untreated `Button`
        // already declares its own `:focus-visible` handling, at a
        // specificity this plain, generically-scoped `Style` does not
        // outrank (the four treatment styles above only win because
        // `.Class(styleClass)` makes their own selector more specific than
        // Fluent's) — so this reaches a future custom `TemplatedControl`
        // with no theme of its own, not every existing Fluent control.
        var genericFocusVisible = new Style(x => x.OfType<TemplatedControl>().Class(":focus-visible"));
        genericFocusVisible.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, Dyn(ApplicationPalette.FocusRingBrushKey)));
        genericFocusVisible.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(DesignTokens.FocusRingThickness)));
        host.Styles.Add(genericFocusVisible);

        // Every button's own disabled state is the pack's 40% — one rule,
        // not one per treatment.
        var disabled = new Style(x => x.OfType<Avalonia.Controls.Button>().Class(":disabled"));
        disabled.Setters.Add(new Setter(Visual.OpacityProperty, 0.4));
        host.Styles.Add(disabled);
        var disabledPresenter = new Style(x => x.OfType<Avalonia.Controls.Button>().Class(":disabled").Template().OfType<ContentPresenter>());
        disabledPresenter.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));
        host.Styles.Add(disabledPresenter);
    }

    /// <summary>A button-level style for <paramref name="styleClass"/>.</summary>
    private static Style Button(string styleClass, params (AvaloniaProperty Property, object Value)[] setters)
    {
        var style = new Style(x => x.OfType<Avalonia.Controls.Button>().Class(styleClass));
        foreach (var (property, value) in setters)
            style.Setters.Add(new Setter(property, value));
        return style;
    }

    /// <summary>A template-presenter style for <paramref name="styleClass"/> in <paramref name="pseudoClass"/> — Fluent's Button template paints its own state onto its <see cref="ContentPresenter"/>, so state styles must target that, not the Button.</summary>
    private static Style Presenter(string styleClass, string pseudoClass, params (AvaloniaProperty Property, object Value)[] setters)
    {
        var style = new Style(x => x.OfType<Avalonia.Controls.Button>().Class(styleClass).Class(pseudoClass).Template().OfType<ContentPresenter>());
        foreach (var (property, value) in setters)
            style.Setters.Add(new Setter(property, value));
        return style;
    }

    /// <summary>
    /// Like <see cref="Presenter"/>, but chaining every pseudo-class in
    /// <paramref name="pseudoClasses"/> onto the same selector (e.g.
    /// <c>:pointerover:focus-visible</c> together) — each extra class
    /// makes the selector more specific than a single-pseudo-class
    /// <see cref="Presenter"/> style for the same <paramref name="styleClass"/>,
    /// so it wins when every one of those states is active at once.
    /// </summary>
    private static Style PresenterStates(string styleClass, string[] pseudoClasses, params (AvaloniaProperty Property, object Value)[] setters)
    {
        var style = new Style(x =>
        {
            var selector = x.OfType<Avalonia.Controls.Button>().Class(styleClass);
            foreach (var pseudoClass in pseudoClasses)
                selector = selector.Class(pseudoClass);
            return selector.Template().OfType<ContentPresenter>();
        });
        foreach (var (property, value) in setters)
            style.Setters.Add(new Setter(property, value));
        return style;
    }

    private static DynamicResourceExtension Dyn(string key) => new(key);
}

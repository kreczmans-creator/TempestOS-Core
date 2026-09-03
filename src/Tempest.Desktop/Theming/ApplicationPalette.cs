using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The platform's own first genuinely theme-reactive custom brush
/// resources (`WP 10.5A`) — every key below is registered as a real
/// Avalonia <see cref="ResourceDictionary.ThemeDictionaries"/> entry
/// (Light and Dark variants both explicit), so any control binding to one
/// via <see cref="ThemeReactiveBrush"/> automatically re-resolves the
/// moment <see cref="ThemeService.ToggleAsync"/> changes
/// <see cref="Application.RequestedThemeVariant"/> — no manual re-paint
/// code anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Values are the brand's since the Desktop brand alignment.</b> The
/// five keys keep their names and their consumers (every `WP 10.5A`
/// overlay/panel/toast/dialog still binds to them unchanged), but each
/// now resolves to the Tempest Engineering Design System's own surface
/// tokens (<see cref="BrandPalette"/>) rather than the provisional
/// Fluent-adjacent greys — one palette, no second colour language.
/// </para>
/// <para>
/// <b>Deliberately not a wrapper around <see cref="Avalonia.Themes.Fluent.FluentTheme"/>'s
/// own internal system-colour resources</b> — this platform owns its own
/// keys with its own explicit values, rather than depending on
/// FluentTheme's own undocumented-to-this-codebase internal resource
/// names, which could change across an Avalonia version bump.
/// </para>
/// </remarks>
internal static class ApplicationPalette
{
    /// <summary>An overlay/scrim background — used by <see cref="Views.CommandPaletteOverlay"/> and the workspace layout's own Auto-Hide flyout (closes `TD-39`).</summary>
    public const string OverlayBackgroundBrushKey = "Tempest.OverlayBackgroundBrush";

    /// <summary>A raised panel's own background — used by the Toast/Dialog/Empty-State controls (`WP 10.5A`).</summary>
    public const string PanelBackgroundBrushKey = "Tempest.PanelBackgroundBrush";

    /// <summary>A raised panel's own border — pairs with <see cref="PanelBackgroundBrushKey"/>.</summary>
    public const string PanelBorderBrushKey = "Tempest.PanelBorderBrush";

    /// <summary>An accented panel background (the selected navigation item, the Digital Thread graph's own centre-node fill) — the brand's 12% cyan selection fill.</summary>
    public const string AccentPanelBackgroundBrushKey = "Tempest.AccentPanelBackgroundBrush";

    /// <summary>The keyboard-focus-visible ring colour — the brand's 2px cyan ring (`WP 10.5A`, "keyboard focus visibility").</summary>
    public const string FocusRingBrushKey = "Tempest.FocusRingBrush";

    /// <summary>
    /// Registers every key above into <paramref name="app"/>'s own
    /// <see cref="Application.Resources"/>, alongside <see cref="BrandPalette"/>'s
    /// own keys. Safe to call more than once — adds one more identical,
    /// harmless merged dictionary rather than throwing or silently
    /// skipping; deliberately not guarded by a process-wide static flag
    /// (a genuine, disclosed finding: Avalonia's own headless test host
    /// does not guarantee one <see cref="Application"/> instance per
    /// process the way the real, single-launch app does).
    /// </summary>
    public static void Register(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        BrandPalette.Register(app);

        var dictionary = new ResourceDictionary();

        var light = new ResourceDictionary
        {
            [OverlayBackgroundBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Paper000, 0.98),
            [PanelBackgroundBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Paper000),
            [PanelBorderBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Ink900, 0.10),
            [AccentPanelBackgroundBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Cyan500, 0.14),
            [FocusRingBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Indigo600),
        };
        var dark = new ResourceDictionary
        {
            [OverlayBackgroundBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Navy700, 0.98),
            [PanelBackgroundBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Navy700),
            [PanelBorderBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Paper050, 0.10),
            [AccentPanelBackgroundBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Cyan500, 0.14),
            [FocusRingBrushKey] = new ImmutableSolidColorBrush(BrandPalette.Cyan500),
        };

        dictionary.ThemeDictionaries[ThemeVariant.Light] = light;
        dictionary.ThemeDictionaries[ThemeVariant.Dark] = dark;

        app.Resources.MergedDictionaries.Add(dictionary);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The platform's own first genuinely theme-reactive custom brush
/// resources (`WP 10.5A`) — every one of the four keys below is
/// registered as a real Avalonia <see cref="ResourceDictionary.ThemeDictionaries"/>
/// entry (Light and Dark variants both explicit), so any control binding
/// to one via <see cref="StyledElementExtensions.GetResourceObservable"/>
/// automatically re-resolves the moment <see cref="ThemeService.ToggleAsync"/>
/// changes <see cref="Application.RequestedThemeVariant"/> — no manual
/// re-paint code anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closes a genuine, disclosed platform-wide gap.</b> Before this
/// class, zero controls anywhere in <c>Tempest.Desktop</c> used
/// <c>DynamicResource</c>/theme-reactive resource binding — every custom-
/// drawn control's own colour was either a hardcoded, non-reactive
/// <see cref="Brushes"/> constant (visually wrong in the opposite theme,
/// `TD-39`) or delegated entirely to <see cref="Avalonia.Themes.Fluent.FluentTheme"/>'s
/// own stock-control theming (automatic, but only for stock controls —
/// <see cref="Button"/>/<see cref="TextBox"/>/<see cref="TreeView"/>/
/// <see cref="TabControl"/>). This class establishes the pattern for
/// every future custom-drawn overlay/panel.
/// </para>
/// <para>
/// <b>Deliberately not a wrapper around <see cref="Avalonia.Themes.Fluent.FluentTheme"/>'s
/// own internal system-colour resources</b> (e.g. <c>SystemChromeMediumColor</c>)
/// — this platform owns its own four keys with its own explicit values,
/// rather than depending on FluentTheme's own undocumented-to-this-
/// codebase internal resource names, which could change across an
/// Avalonia version bump without this platform's own knowledge.
/// </para>
/// </remarks>
internal static class ApplicationPalette
{
    /// <summary>An overlay/scrim background — used by <see cref="Views.CommandPaletteOverlay"/> and the workspace layout's own Auto-Hide flyout (closes `TD-39`).</summary>
    public const string OverlayBackgroundBrushKey = "Tempest.OverlayBackgroundBrush";

    /// <summary>A raised panel's own background — used by the new Toast/Dialog/Empty-State controls (`WP 10.5A`).</summary>
    public const string PanelBackgroundBrushKey = "Tempest.PanelBackgroundBrush";

    /// <summary>A raised panel's own border — pairs with <see cref="PanelBackgroundBrushKey"/>.</summary>
    public const string PanelBorderBrushKey = "Tempest.PanelBorderBrush";

    /// <summary>An accented panel background (the Digital Thread graph's own centre-node fill, `WP 10.4A`) — replaces that class's own previously-hardcoded, non-theme-reactive hex colours (a genuine, disclosed finding of this Work Package's own theme audit).</summary>
    public const string AccentPanelBackgroundBrushKey = "Tempest.AccentPanelBackgroundBrush";

    /// <summary>The keyboard-focus-visible ring colour — used by the global focus style (`WP 10.5A`, "keyboard focus visibility").</summary>
    public const string FocusRingBrushKey = "Tempest.FocusRingBrush";

    /// <summary>
    /// Registers every key above into <paramref name="app"/>'s own
    /// <see cref="Application.Resources"/>. Safe to call more than once —
    /// adds one more identical, harmless merged dictionary rather than
    /// throwing or silently skipping; deliberately not guarded by a
    /// process-wide static flag (a genuine, disclosed finding: Avalonia's
    /// own headless test host does not guarantee one <see cref="Application"/>
    /// instance per process the way the real, single-launch app does —
    /// a static guard here intermittently left a later test's own fresh
    /// <see cref="Application"/> instance completely unregistered,
    /// found by a flaky test before this shape was reached).
    /// </summary>
    public static void Register(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var dictionary = new ResourceDictionary();

        var light = new ResourceDictionary
        {
            [OverlayBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#F5F5F5"), 0.98),
            [PanelBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#FFFFFF")),
            [PanelBorderBrushKey] = new SolidColorBrush(Color.Parse("#D0D0D0")),
            [AccentPanelBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#DCE8FB")),
            [FocusRingBrushKey] = new SolidColorBrush(Color.Parse("#0067C0")),
        };
        var dark = new ResourceDictionary
        {
            [OverlayBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#1E1E1E"), 0.98),
            [PanelBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#2A2A2E")),
            [PanelBorderBrushKey] = new SolidColorBrush(Color.Parse("#454549")),
            [AccentPanelBackgroundBrushKey] = new SolidColorBrush(Color.Parse("#2D4F7C")),
            [FocusRingBrushKey] = new SolidColorBrush(Color.Parse("#4CC2FF")),
        };

        dictionary.ThemeDictionaries[ThemeVariant.Light] = light;
        dictionary.ThemeDictionaries[ThemeVariant.Dark] = dark;

        app.Resources.MergedDictionaries.Add(dictionary);
    }
}

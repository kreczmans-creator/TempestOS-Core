using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using Tempest.Core.Settings;

namespace Tempest.Desktop.Theming;

/// <summary>
/// The Theme Framework — Light/Dark switching, persisted through the
/// existing <see cref="ISettingsProvider"/> Platform Service, exactly as
/// `WP10.0A Visual Design System.md` §1 specified ("theme data is a
/// Settings value, exactly like panel layout... no new 'Theming Service'
/// is introduced"). Introduces zero new persistence mechanism — the
/// identical <c>ADR-0064</c> pattern <see cref="Tempest.App.Workspace.WorkspaceState"/>
/// already established for layout, applied here to one further string
/// value.
/// </summary>
public sealed class ThemeService
{
    /// <summary>The <see cref="SettingDefinition.Key"/> the current theme choice is stored under.</summary>
    public const string SettingKey = "Desktop.Theme";

    private readonly ISettingsProvider _settingsProvider;

    /// <summary>Initialises a new instance of the <see cref="ThemeService"/> class.</summary>
    public ThemeService(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Desktop Theme (Light/Dark)", nameof(ThemeVariant.Light)));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Already registered by a prior ThemeService instance against
            // the same ISettingsProvider (a restart) — idempotent, not an
            // error, mirroring WorkspaceState's own identical precedent.
        }
    }

    /// <summary>Gets the theme variant currently applied to <see cref="Application.Current"/>.</summary>
    public ThemeVariant Current => Application.Current?.RequestedThemeVariant ?? ThemeVariant.Light;

    /// <summary>Reads the persisted theme choice (defaulting to Light on a missing/first-run value) and applies it.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var value = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);
        Apply(string.Equals(value, nameof(ThemeVariant.Dark), StringComparison.Ordinal) ? ThemeVariant.Dark : ThemeVariant.Light);
    }

    /// <summary>Toggles between Light and Dark, applies the new variant immediately, and persists the choice.</summary>
    public async Task ToggleAsync(CancellationToken cancellationToken = default)
    {
        var next = Current == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        Apply(next);
        await _settingsProvider.SetValueAsync(SettingKey, next == ThemeVariant.Dark ? nameof(ThemeVariant.Dark) : nameof(ThemeVariant.Light), cancellationToken).ConfigureAwait(false);
    }

    private static void Apply(ThemeVariant variant)
    {
        if (Application.Current is not { } app)
            return;

        // Tempest.Core's own async methods (SettingsProvider included)
        // ConfigureAwait(false) internally, so a caller awaiting
        // GetValueAsync/SetValueAsync before applying a theme resumes on a
        // thread-pool thread, not necessarily the Avalonia UI thread —
        // Application.RequestedThemeVariant's own setter requires the UI
        // thread. Marshalling explicitly here makes Apply correct
        // regardless of which thread calls it, rather than requiring every
        // caller to remember to do so itself.
        if (Dispatcher.UIThread.CheckAccess())
            app.RequestedThemeVariant = variant;
        else
            Dispatcher.UIThread.Invoke(() => app.RequestedThemeVariant = variant);
    }
}

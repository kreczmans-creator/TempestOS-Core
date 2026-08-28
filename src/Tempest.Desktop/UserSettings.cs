using System.Text.Json;
using Tempest.Core.Settings;

namespace Tempest.Desktop;

/// <summary>
/// The Desktop User Settings Framework (`WP 10.5B` scope: "User
/// Settings... appearance, theme, editor preferences, workspace
/// preferences, startup behaviour, notifications... User settings shall
/// remain completely separate from Engineering data") — real, working
/// preferences, persisted through the identical <see cref="ISettingsProvider"/>
/// substrate <see cref="Docking.DesktopPanelUiState"/>/<see cref="Theming.ThemeService"/>
/// already use, under a third, sibling key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Completely separate from Engineering data, by construction, not
/// merely by convention.</b> Every value here is a Desktop-local UI
/// preference (toast duration, delete-confirmation opt-out, recent-list
/// capacity) — none is, or could be, mistaken for Engineering Domain
/// content; none is readable by, or reachable from, any
/// <c>Tempest.Core.EngineeringDomain</c> type. <see cref="Theming.ThemeService"/>'s
/// own theme choice remains its own, separate key (unchanged, `WP 10.0B`)
/// — this class does not fold it in, avoiding a single "grab-bag" key
/// two independent concerns would then share.
/// </para>
/// <para>
/// Honestly scoped: this is a first, real, minimal preference set — not
/// every named scope item (editor/ribbon/toolbar/keyboard-shortcut
/// preferences) has its own persisted value yet, since several of those
/// name a capability (per-user keyboard shortcut remapping, a
/// customisable Ribbon) that does not exist anywhere in this platform
/// today, independent of settings persistence — disclosed directly,
/// not fabricated (`WP10.5B Implementation Report.md` §8).
/// </para>
/// </remarks>
public sealed class UserSettings
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Desktop.UserSettings";

    private readonly ISettingsProvider _settingsProvider;

    /// <summary>Initialises a new instance of the <see cref="UserSettings"/> class with every value at its own documented default.</summary>
    public UserSettings(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Desktop User Settings", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Already registered by a prior instance against the same
            // ISettingsProvider (a restart) — idempotent, not an error,
            // mirroring DesktopPanelUiState's/ThemeService's own identical
            // discipline.
        }
    }

    /// <summary>Gets or sets how long a Toast stays visible before auto-dismissing, in seconds.</summary>
    public double ToastDurationSeconds { get; set; } = 4.5;

    /// <summary>Gets or sets whether deleting an object asks for confirmation first (`WP 10.5B`'s own new Delete Confirmation dialog). <see langword="true"/> by default — a user may deliberately opt out once they trust their own workflow.</summary>
    public bool ConfirmBeforeDelete { get; set; } = true;

    /// <summary>Gets or sets how many entries the Project Explorer's own recent-search list keeps.</summary>
    public int RecentSearchCapacity { get; set; } = 5;

    /// <summary>Writes the current state via <see cref="ISettingsProvider.SetValueAsync"/>.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var dto = new UserSettingsDto(ToastDurationSeconds, ConfirmBeforeDelete, RecentSearchCapacity);
        var json = JsonSerializer.Serialize(dto);

        await _settingsProvider.SetValueAsync(SettingKey, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads persisted state via <see cref="ISettingsProvider.GetValueAsync"/>. A missing/first-run value leaves every property at its own documented default — never an exception.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);

        UserSettingsDto? dto;
        try
        {
            dto = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UserSettingsDto>(json);
        }
        catch (JsonException)
        {
            // A corrupted stored value (e.g. a torn write) degrades to
            // the documented first-run defaults — this method's own
            // "never an exception" contract (`TD-60`).
            dto = null;
        }

        if (dto is null)
            return;

        ToastDurationSeconds = dto.ToastDurationSeconds;
        ConfirmBeforeDelete = dto.ConfirmBeforeDelete;
        RecentSearchCapacity = dto.RecentSearchCapacity;
    }

    /// <summary>The plain, JSON-serializable shape this class persists.</summary>
    private sealed record UserSettingsDto(double ToastDurationSeconds, bool ConfirmBeforeDelete, int RecentSearchCapacity);
}

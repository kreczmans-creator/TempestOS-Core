using System.Text.Json;
using Tempest.Core.Settings;

namespace Tempest.Desktop;

/// <summary>
/// One entry in <see cref="RecentObjectsState"/> — a single previously
/// opened object.
/// </summary>
public sealed record RecentObjectEntry(Guid Id, string Kind, string DisplayName, DateTimeOffset OpenedAt);

/// <summary>
/// The "Recent objects" productivity feature (`WP 10.6A`) — a
/// Desktop-local, persisted, capacity-bounded most-recently-opened list,
/// mirroring <see cref="UserSettings"/>'s/<see cref="Docking.DesktopPanelUiState"/>'s
/// own established <see cref="ISettingsProvider"/>-JSON-DTO pattern under
/// its own, third sibling key.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately distinct from <c>NavigationService.RecentItems</c> — that
/// existing, frozen `WP8.0B` Workspace member is an in-memory,
/// session-only list (reset every restart); this Work Package needs a
/// persisted one to survive a restart, and <c>NavigationService</c>'s own
/// contract (one of the twelve frozen `WP8.0B` Workspace contracts) is
/// never modified to add persistence to it. This is a second, additional,
/// Desktop-local list, fed by the identical "an object was opened" events
/// <c>MainWindow</c> already raises — never a replacement.
/// </para>
/// </remarks>
public sealed class RecentObjectsState
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Desktop.RecentObjects";

    /// <summary>The maximum number of entries retained — the oldest is discarded once exceeded.</summary>
    public const int Capacity = 15;

    private readonly ISettingsProvider _settingsProvider;
    private readonly List<RecentObjectEntry> _entries = [];

    /// <summary>Initialises a new instance of the <see cref="RecentObjectsState"/> class, initially empty.</summary>
    public RecentObjectsState(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Recent Objects", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Idempotent — mirrors UserSettings'/DesktopPanelUiState's own identical discipline.
        }
    }

    /// <summary>Gets every recorded entry, most recently opened first.</summary>
    public IReadOnlyList<RecentObjectEntry> Entries => _entries;

    /// <summary>Records <paramref name="id"/>/<paramref name="kind"/>/<paramref name="displayName"/> as just opened — moves an existing entry for the same Id to the front rather than duplicating it, then trims to <see cref="Capacity"/>.</summary>
    public void Record(Guid id, string kind, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        _entries.RemoveAll(e => e.Id == id);
        _entries.Insert(0, new RecentObjectEntry(id, kind, displayName, DateTimeOffset.UtcNow));

        if (_entries.Count > Capacity)
            _entries.RemoveRange(Capacity, _entries.Count - Capacity);
    }

    /// <summary>Writes the current entries via <see cref="ISettingsProvider.SetValueAsync"/>.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(_entries);
        await _settingsProvider.SetValueAsync(SettingKey, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads persisted entries via <see cref="ISettingsProvider.GetValueAsync"/>. A missing/first-run value leaves the list empty — never an exception.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);

        var entries = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<RecentObjectEntry>>(json);
        if (entries is null)
            return;

        _entries.Clear();
        _entries.AddRange(entries);
    }
}

using System.Text.Json;
using Tempest.Core.Settings;

namespace Tempest.Desktop;

/// <summary>One entry in <see cref="FavouriteObjectsState"/> — a single favourited object.</summary>
public sealed record FavouriteObjectEntry(Guid Id, string Kind, string DisplayName);

/// <summary>
/// The "Favourite objects" productivity feature (`WP 10.6A`) — a
/// Desktop-local, persisted set of user-starred objects of any Kind,
/// mirroring <see cref="RecentObjectsState"/>'s own identical
/// <see cref="ISettingsProvider"/>-JSON-DTO shape.
/// </summary>
/// <remarks>
/// <b>Deliberately distinct from <c>EngineeringCockpit.FavouriteProjects</c>.</b>
/// That existing, disclosed-empty member is an Engineering-Domain-level
/// "favourite Project" concept — App-layer, still unbuilt (no
/// <c>IsFavourite</c> flag exists on <c>Project</c> or anywhere in the
/// Domain, and this Work Package does not add one there). This type is a
/// separate, Desktop-local "any open object" convenience list — a UI
/// preference, exactly like <see cref="UserSettings"/>, never Engineering
/// data, never conflated with <c>FavouriteProjects</c>.
/// </remarks>
public sealed class FavouriteObjectsState
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Desktop.FavouriteObjects";

    private readonly ISettingsProvider _settingsProvider;
    private readonly List<FavouriteObjectEntry> _entries = [];

    /// <summary>Initialises a new instance of the <see cref="FavouriteObjectsState"/> class, initially empty.</summary>
    public FavouriteObjectsState(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Favourite Objects", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Idempotent — mirrors UserSettings'/DesktopPanelUiState's own identical discipline.
        }
    }

    /// <summary>Gets every favourited entry.</summary>
    public IReadOnlyList<FavouriteObjectEntry> Entries => _entries;

    /// <summary>Gets whether <paramref name="id"/> is currently favourited.</summary>
    public bool IsFavourite(Guid id) => _entries.Any(e => e.Id == id);

    /// <summary>Adds <paramref name="id"/>/<paramref name="kind"/>/<paramref name="displayName"/> as a favourite — a no-op if already favourited.</summary>
    public void Add(Guid id, string kind, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (IsFavourite(id))
            return;

        _entries.Add(new FavouriteObjectEntry(id, kind, displayName));
    }

    /// <summary>Removes <paramref name="id"/> from the favourites, if present — a no-op otherwise.</summary>
    public void Remove(Guid id) => _entries.RemoveAll(e => e.Id == id);

    /// <summary>Toggles <paramref name="id"/>'s own favourite state — the real Undo/Redo target (`ADR-0099`): trivially self-inverting, calling this again exactly reverses the previous call.</summary>
    public void Toggle(Guid id, string kind, string displayName)
    {
        if (IsFavourite(id))
            Remove(id);
        else
            Add(id, kind, displayName);
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

        List<FavouriteObjectEntry>? entries;
        try
        {
            entries = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<FavouriteObjectEntry>>(json);
        }
        catch (JsonException)
        {
            // A corrupted stored value (e.g. a torn write) degrades to
            // the documented first-run empty list — this method's own
            // "never an exception" contract (`TD-60`).
            entries = null;
        }

        if (entries is null)
            return;

        _entries.Clear();
        _entries.AddRange(entries);
    }
}

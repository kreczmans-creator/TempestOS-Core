using System.Text.Json;

namespace Tempest.Companion.Client;

/// <summary>
/// Loads/saves <see cref="CompanionClientSettings"/> as one JSON file
/// under the Companion's own local application-data folder. Corrupt or
/// missing content loads as <see cref="CompanionClientSettings.Default"/>
/// rather than failing launch — a phone app must always be able to start
/// and show its own connection settings.
/// </summary>
public sealed class CompanionSettingsStore
{
    private readonly string _settingsPath;

    /// <summary>
    /// Initialises a new instance of the <see cref="CompanionSettingsStore"/> class.
    /// </summary>
    /// <param name="rootPath">
    /// The folder settings (and the snapshot cache) live under, or
    /// <see langword="null"/> for the conventional per-user default —
    /// overridable so tests isolate their own state, the identical
    /// pattern <c>WorkspaceHost</c>'s own persistence override follows
    /// (`TD-37`).
    /// </param>
    public CompanionSettingsStore(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TempestOS.Companion");
        _settingsPath = Path.Combine(RootPath, "settings.json");
    }

    /// <summary>Gets the folder settings and cache live under.</summary>
    public string RootPath { get; }

    /// <summary>Loads the persisted settings, or <see cref="CompanionClientSettings.Default"/> when absent or unreadable.</summary>
    public CompanionClientSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return CompanionClientSettings.Default;

            return JsonSerializer.Deserialize<CompanionClientSettings>(File.ReadAllText(_settingsPath))
                ?? CompanionClientSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CompanionClientSettings.Default;
        }
    }

    /// <summary>Persists <paramref name="settings"/>, creating the folder on first save.</summary>
    public void Save(CompanionClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(RootPath);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

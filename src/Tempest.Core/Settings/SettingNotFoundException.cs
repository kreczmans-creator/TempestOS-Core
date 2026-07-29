namespace Tempest.Core.Settings;

/// <summary>
/// Thrown by <see cref="ISettingsProvider.GetValueAsync"/> or
/// <see cref="ISettingsProvider.SetValueAsync"/> when no definition is
/// registered under the given key.
/// </summary>
public sealed class SettingNotFoundException : SettingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="SettingNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The setting key that has no registered definition.</param>
    public SettingNotFoundException(string key)
        : base($"No setting definition is registered under key '{key}'.")
    {
        Key = key;
    }

    /// <summary>
    /// Gets the setting key that has no registered definition.
    /// </summary>
    public string Key { get; }
}

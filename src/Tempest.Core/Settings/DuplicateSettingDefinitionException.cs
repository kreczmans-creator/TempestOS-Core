namespace Tempest.Core.Settings;

/// <summary>
/// Thrown when <see cref="ISettingsProvider.RegisterDefinition"/> is
/// called for a key that already has a registered definition.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override, mirroring
/// <see cref="Commands.DuplicateCommandIdException"/>'s own convention.
/// </remarks>
public sealed class DuplicateSettingDefinitionException : SettingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateSettingDefinitionException"/> class.
    /// </summary>
    /// <param name="key">The setting key that already has a registered definition.</param>
    public DuplicateSettingDefinitionException(string key)
        : base($"A setting definition is already registered under key '{key}'.")
    {
        Key = key;
    }

    /// <summary>
    /// Gets the setting key that already has a registered definition.
    /// </summary>
    public string Key { get; }
}

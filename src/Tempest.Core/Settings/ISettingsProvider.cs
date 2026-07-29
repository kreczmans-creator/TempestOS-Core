namespace Tempest.Core.Settings;

/// <summary>
/// Reads and writes runtime-mutable setting values — explicitly
/// distinct from <c>IConfigurationProvider</c>, which is read-only and
/// loaded once at startup (Case Study 05).
/// </summary>
public interface ISettingsProvider
{
    /// <summary>
    /// Registers <paramref name="definition"/>.
    /// </summary>
    /// <param name="definition">The definition to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateSettingDefinitionException">A definition is already registered under <see cref="ISettingDefinition.Key"/>.</exception>
    void RegisterDefinition(ISettingDefinition definition);

    /// <summary>
    /// Gets the current value for <paramref name="key"/> — the
    /// persisted value if one has been written, otherwise the
    /// registered definition's own default.
    /// </summary>
    /// <param name="key">The setting key to read.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="SettingNotFoundException">No definition is registered under <paramref name="key"/>.</exception>
    Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the value for <paramref name="key"/> and publishes an
    /// <see cref="ISettingsChangedEvent"/> through the existing Event
    /// Bus.
    /// </summary>
    /// <param name="key">The setting key to write.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="SettingNotFoundException">No definition is registered under <paramref name="key"/>.</exception>
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}

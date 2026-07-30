namespace Tempest.Core.Settings;

/// <summary>
/// The concrete, immutable <see cref="ISettingDefinition"/> implementation.
/// </summary>
public sealed class SettingDefinition : ISettingDefinition
{
    /// <summary>
    /// Initialises a new instance of the <see cref="SettingDefinition"/> class.
    /// </summary>
    /// <param name="key">The setting's stable, unique key.</param>
    /// <param name="displayName">A human-readable display name.</param>
    /// <param name="defaultValue">The value used when nothing has been persisted yet.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> or <paramref name="displayName"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="defaultValue"/> is <see langword="null"/>.</exception>
    public SettingDefinition(string key, string displayName, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key must not be null, empty, or whitespace.", nameof(key));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be null, empty, or whitespace.", nameof(displayName));

        ArgumentNullException.ThrowIfNull(defaultValue);

        Key = key;
        DisplayName = displayName;
        DefaultValue = defaultValue;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string DefaultValue { get; }
}

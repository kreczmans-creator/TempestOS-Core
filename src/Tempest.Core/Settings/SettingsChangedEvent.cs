namespace Tempest.Core.Settings;

/// <summary>
/// The concrete <see cref="ISettingsChangedEvent"/> implementation,
/// published by <see cref="SettingsProvider"/>.
/// </summary>
public sealed class SettingsChangedEvent : ISettingsChangedEvent
{
    /// <summary>
    /// Initialises a new instance of the <see cref="SettingsChangedEvent"/> class.
    /// </summary>
    /// <param name="key">The key of the setting that changed.</param>
    /// <param name="oldValue">The value before this change.</param>
    /// <param name="newValue">The value after this change.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <see langword="null"/>.</exception>
    public SettingsChangedEvent(string key, string oldValue, string newValue)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(oldValue);
        ArgumentNullException.ThrowIfNull(newValue);

        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public string OldValue { get; }

    /// <inheritdoc />
    public string NewValue { get; }
}

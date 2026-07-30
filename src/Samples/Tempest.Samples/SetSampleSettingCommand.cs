using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler writes a new value for
/// <see cref="SettingsSampleModule.SampleSettingKey"/> through
/// <see cref="Tempest.Core.Settings.ISettingsProvider"/>.
/// </summary>
/// <remarks>
/// Demonstrates the Command Framework and Settings interacting — see
/// <see cref="SetSampleSettingCommandHandler"/>.
/// </remarks>
public sealed class SetSampleSettingCommand : ICommand
{
    /// <summary>
    /// Initialises a new instance of the <see cref="SetSampleSettingCommand"/> class.
    /// </summary>
    /// <param name="newValue">The new value to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="newValue"/> is <see langword="null"/>.</exception>
    public SetSampleSettingCommand(string newValue)
    {
        ArgumentNullException.ThrowIfNull(newValue);

        NewValue = newValue;
    }

    /// <summary>Gets the new value to write.</summary>
    public string NewValue { get; }
}

namespace Tempest.Core.Settings;

/// <summary>
/// The base exception thrown when a Settings operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Identity.IdentityException"/>'s and
/// <see cref="Commands.CommandException"/>'s own base-plus-subtype
/// pattern.
/// </remarks>
public class SettingsException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="SettingsException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public SettingsException(string message)
        : base(message)
    {
    }
}

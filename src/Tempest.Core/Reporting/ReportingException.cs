namespace Tempest.Core.Reporting;

/// <summary>
/// The base exception thrown when a Reporting operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Settings.SettingsException"/>'s,
/// <see cref="Identity.IdentityException"/>'s, and
/// <see cref="Commands.CommandException"/>'s own base-plus-subtype
/// pattern.
/// </remarks>
public class ReportingException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReportingException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public ReportingException(string message)
        : base(message)
    {
    }
}

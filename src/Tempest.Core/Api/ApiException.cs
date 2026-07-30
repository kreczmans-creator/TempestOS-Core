namespace Tempest.Core.Api;

/// <summary>
/// The base exception thrown when a REST API registration operation
/// fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Reporting.ReportingException"/>'s,
/// <see cref="Settings.SettingsException"/>'s, and
/// <see cref="Commands.CommandException"/>'s own base-plus-subtype
/// pattern.
/// </remarks>
public class ApiException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public ApiException(string message)
        : base(message)
    {
    }
}

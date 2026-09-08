namespace Tempest.Core.Licensing;

/// <summary>
/// The base exception thrown when a Licensing operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Reporting.ReportingException"/>'s,
/// <see cref="Api.ApiException"/>'s, and <see cref="ExportImport.ExportImportException"/>'s
/// own base-plus-subtype pattern.
/// </remarks>
public class LicensingException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="LicensingException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public LicensingException(string message)
        : base(message)
    {
    }
}

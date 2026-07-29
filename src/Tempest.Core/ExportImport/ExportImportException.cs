namespace Tempest.Core.ExportImport;

/// <summary>
/// The base exception thrown when an Export/Import operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Reporting.ReportingException"/>'s,
/// <see cref="Api.ApiException"/>'s, and <see cref="Settings.SettingsException"/>'s
/// own base-plus-subtype pattern.
/// </remarks>
public class ExportImportException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ExportImportException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public ExportImportException(string message)
        : base(message)
    {
    }
}

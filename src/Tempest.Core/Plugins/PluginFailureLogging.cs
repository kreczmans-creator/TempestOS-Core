using Tempest.Core.Logging;

namespace Tempest.Core.Plugins;

/// <summary>
/// Maps a <see cref="PluginException"/> to the logging severity ADR-0025
/// assigns its category, and logs it accordingly.
/// </summary>
/// <remarks>
/// Shared by <see cref="PluginManifestDiscoveryService"/> and
/// <see cref="PluginAssemblyLoader"/> so both phases report isolated failures
/// identically — one place implements ADR-0025's severity table, rather than
/// each phase reimplementing the same <c>switch</c>.
/// </remarks>
internal static class PluginFailureLogging
{
    /// <summary>
    /// Logs an isolated plugin failure at the severity ADR-0025 assigns its category.
    /// </summary>
    /// <param name="logger">The logger to record the failure with, if any.</param>
    /// <param name="exception">The isolated failure.</param>
    /// <param name="candidateDescription">
    /// A short description of the candidate (its folder path, or its declared
    /// ID) to include in the log message.
    /// </param>
    public static void LogIsolatedFailure(ILogger? logger, PluginException exception, string candidateDescription)
    {
        var message = $"Plugin candidate '{candidateDescription}' isolated: {exception.Message}";

        switch (SeverityFor(exception))
        {
            case LogLevel.Information:
                logger?.Information(message, exception);
                break;
            case LogLevel.Warning:
                logger?.Warning(message, exception);
                break;
            default:
                logger?.Error(message, exception);
                break;
        }
    }

    private static LogLevel SeverityFor(PluginException exception) => exception switch
    {
        IncompatiblePluginVersionException => LogLevel.Information,
        InvalidPluginManifestException => LogLevel.Warning,
        DuplicatePluginIdException => LogLevel.Warning,
        PluginAssemblyNotFoundException => LogLevel.Error,
        PluginAssemblyLoadException => LogLevel.Error,
        _ => LogLevel.Error,
    };
}

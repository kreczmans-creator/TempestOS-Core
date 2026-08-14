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
        MissingPluginDependencyException => LogLevel.Warning,
        IncompatiblePluginDependencyVersionException => LogLevel.Warning,
        CircularPluginDependencyException => LogLevel.Warning,
        PluginSignatureVerificationFailedException => LogLevel.Error,
        PluginUnsignedLoadNotAllowedException => LogLevel.Warning,
        PluginTrustDeniedException => LogLevel.Warning,
        _ => LogLevel.Error,
    };

    /// <summary>
    /// Records an isolated plugin failure into the Plugin Registry, if a
    /// recorder is available.
    /// </summary>
    /// <param name="recorder">The registry's write side, or <see langword="null"/> if none is available.</param>
    /// <param name="exception">The isolated failure.</param>
    /// <param name="candidateFolderName">
    /// The candidate's folder name, used as the recorded <see cref="PluginRegistryEntry.Id"/>
    /// only when the exception carries no reliable plugin identifier of its own
    /// (<see cref="InvalidPluginManifestException"/>).
    /// </param>
    public static void RecordIsolatedFailure(IPluginRegistryRecorder? recorder, PluginException exception, string candidateFolderName)
    {
        if (recorder is null)
            return;

        var id = exception switch
        {
            DuplicatePluginIdException e => e.PluginId,
            IncompatiblePluginVersionException e => e.PluginId,
            MissingPluginDependencyException e => e.PluginId,
            IncompatiblePluginDependencyVersionException e => e.PluginId,
            CircularPluginDependencyException e => e.PluginId,
            PluginAssemblyNotFoundException e => e.PluginId,
            PluginAssemblyLoadException e => e.PluginId,
            PluginSignatureVerificationFailedException e => e.PluginId,
            PluginUnsignedLoadNotAllowedException e => e.PluginId,
            PluginTrustDeniedException e => e.PluginId,
            _ => candidateFolderName,
        };

        var state = exception switch
        {
            IncompatiblePluginVersionException => PluginRegistryState.Incompatible,
            MissingPluginDependencyException or IncompatiblePluginDependencyVersionException or CircularPluginDependencyException => PluginRegistryState.DependencyUnmet,
            PluginTrustDeniedException => PluginRegistryState.TrustDenied,
            _ => PluginRegistryState.Failed,
        };

        recorder.Record(new PluginRegistryEntry(id, null, null, state, exception.Message));
    }
}

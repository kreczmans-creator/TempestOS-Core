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
    /// whenever the exception carries no reliable plugin identifier of its own
    /// — every category's own exception type reliably carries one except
    /// <see cref="InvalidPluginManifestException"/>, whose own <see cref="InvalidPluginManifestException.PluginId"/>
    /// is deliberately <b>never</b> used as the recorded <see cref="PluginRegistryEntry.Id"/>,
    /// even when non-null (WP 13.3B architecture review finding: doing so
    /// would let a manifest that never fully validates — and so never
    /// reaches <see cref="DuplicatePluginIdException"/>'s own uniqueness
    /// check — inject a <see cref="PluginRegistryState.Failed"/> entry under
    /// the exact declared Id of a genuine, unrelated, already-<see cref="PluginRegistryState.Loaded"/>
    /// plugin, since <see cref="Plugins.PluginRegistry.Record"/> performs no
    /// deduplication of its own). <see cref="InvalidPluginManifestException.PluginId"/>
    /// is instead surfaced only as free text inside the recorded
    /// <see cref="PluginRegistryEntry.Detail"/>, which nothing treats as a
    /// unique key.
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

        // The candidate's own self-declared Id (if it got far enough to
        // parse one) is surfaced here, in free text only - never as the
        // structured Id above - so an operator can still correlate a
        // malformed-manifest failure with its intended plugin without that
        // unverified value ever being treated as a unique key by anything.
        var detail = exception is InvalidPluginManifestException { PluginId: { } declaredId }
            ? $"{exception.Message} (self-declared Id: '{declaredId}', not yet verified unique.)"
            : exception.Message;

        recorder.Record(new PluginRegistryEntry(id, null, null, state, detail));
    }
}

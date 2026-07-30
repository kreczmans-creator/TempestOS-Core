namespace Tempest.Core.ExportImport;

/// <summary>
/// Reads a previously exported artifact back into the owning service(s).
/// </summary>
/// <remarks>
/// A concrete <see cref="IImportable"/> must be registered ahead of time
/// (typically during Module Initialisation) before an artifact's own
/// section can be routed back to it — see <see cref="IImportable"/>'s own
/// remarks for how a module registers one.
/// </remarks>
public interface IImportService
{
    /// <summary>
    /// Imports every section of the artifact read from
    /// <paramref name="source"/>, dispatching each to its own registered
    /// <see cref="IImportable"/>.
    /// </summary>
    /// <param name="source">The stream the artifact is read from.</param>
    /// <param name="cancellationToken">A token observed for the duration of the import.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="IncompatibleExportSchemaException">
    /// The artifact's schema version is not supported by the current
    /// platform version, or the artifact names a section for which no
    /// <see cref="IImportable"/> is registered. No section of the artifact
    /// is imported when this is thrown — every section's compatibility is
    /// checked before any section is actually applied.
    /// </exception>
    /// <remarks>
    /// An <see cref="IImportable"/>'s own <see cref="IImportable.ImportAsync"/>
    /// exception, or an <see cref="IOException"/> from <paramref name="source"/>
    /// itself, propagates to the caller unmodified — this service does not
    /// wrap or reinterpret either failure.
    /// </remarks>
    Task ImportAsync(Stream source, CancellationToken cancellationToken = default);
}

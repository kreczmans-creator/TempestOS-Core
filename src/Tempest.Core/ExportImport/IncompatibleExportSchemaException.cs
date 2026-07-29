namespace Tempest.Core.ExportImport;

/// <summary>
/// Thrown by <see cref="IImportService.ImportAsync"/> when an artifact
/// section cannot be imported by the current platform version — either
/// because its <see cref="IExportable.SchemaVersion"/> does not match the
/// registered <see cref="IImportable.SchemaVersion"/> exactly, or because no
/// <see cref="IImportable"/> is registered under the section's own
/// <see cref="IImportable.Kind"/> at all.
/// </summary>
/// <remarks>
/// <see cref="IImportService"/> never attempts a best-effort partial
/// import — see <see cref="IImportService.ImportAsync"/>'s own remarks.
/// </remarks>
public sealed class IncompatibleExportSchemaException : ExportImportException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="IncompatibleExportSchemaException"/>
    /// class for a section whose schema version the current platform does
    /// not support.
    /// </summary>
    /// <param name="kind">The artifact section's own kind identifier.</param>
    /// <param name="artifactSchemaVersion">The schema version the artifact's own section was written with.</param>
    /// <param name="supportedSchemaVersion">The schema version the currently registered <see cref="IImportable"/> supports.</param>
    public IncompatibleExportSchemaException(string kind, int artifactSchemaVersion, int supportedSchemaVersion)
        : base($"Artifact section '{kind}' has schema version {artifactSchemaVersion}, which does not " +
               $"match the currently supported schema version {supportedSchemaVersion}.")
    {
        Kind = kind;
        ArtifactSchemaVersion = artifactSchemaVersion;
        SupportedSchemaVersion = supportedSchemaVersion;
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="IncompatibleExportSchemaException"/>
    /// class for a section that names a kind no <see cref="IImportable"/> is
    /// currently registered under.
    /// </summary>
    /// <param name="kind">The artifact section's own kind identifier.</param>
    public IncompatibleExportSchemaException(string kind)
        : base($"Artifact section '{kind}' cannot be imported: no {nameof(IImportable)} is registered under that kind.")
    {
        Kind = kind;
        ArtifactSchemaVersion = null;
        SupportedSchemaVersion = null;
    }

    /// <summary>Gets the artifact section's own kind identifier.</summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the schema version the artifact's own section was written with,
    /// or <see langword="null"/> when no <see cref="IImportable"/> is
    /// registered under <see cref="Kind"/> at all.
    /// </summary>
    public int? ArtifactSchemaVersion { get; }

    /// <summary>
    /// Gets the schema version the currently registered <see cref="IImportable"/>
    /// supports, or <see langword="null"/> when no <see cref="IImportable"/>
    /// is registered under <see cref="Kind"/> at all.
    /// </summary>
    public int? SupportedSchemaVersion { get; }
}

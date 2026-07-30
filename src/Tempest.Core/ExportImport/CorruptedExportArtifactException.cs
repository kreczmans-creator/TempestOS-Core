namespace Tempest.Core.ExportImport;

/// <summary>
/// Thrown by <see cref="IExportFormat.ReadAsync"/> when a stream does not
/// contain a well-formed artifact this format recognises — a malformed or
/// truncated file, not a schema-version mismatch.
/// </summary>
/// <remarks>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft, which named only <see cref="IncompatibleExportSchemaException"/>
/// as Export/Import's own exception surface. This Work Package's own
/// brief explicitly requires "Corrupted file tests" as a testing category —
/// a genuinely different failure mode from an incompatible-but-well-formed
/// artifact, so it is surfaced as its own concrete, sealed subtype rather
/// than overloading <see cref="IncompatibleExportSchemaException"/> for a
/// structurally invalid file.
/// </remarks>
public sealed class CorruptedExportArtifactException : ExportImportException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CorruptedExportArtifactException"/> class.
    /// </summary>
    /// <param name="reason">A description of why the artifact could not be read.</param>
    public CorruptedExportArtifactException(string reason)
        : base($"The artifact is corrupted or not well-formed: {reason}")
    {
        Reason = reason;
    }

    /// <summary>Gets the description of why the artifact could not be read.</summary>
    public string Reason { get; }
}

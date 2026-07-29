namespace Tempest.Core.ExportImport;

/// <summary>
/// Converts a simple key/value data set to and from raw bytes — the
/// "Serialization abstraction" named in this Work Package's own
/// implementation scope, for a specific <see cref="IExportable"/>'s own
/// internal data.
/// </summary>
/// <remarks>
/// <para>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft — an additive elaboration mirroring `WP 6.0`'s
/// own <c>IReportTemplate</c> precedent: entirely optional, and unknown to
/// <see cref="IExportService"/>/<see cref="IImportService"/>, exactly as
/// <c>IReportTemplate</c> is unknown to <see cref="Reporting.IReportingService"/>.
/// A concrete <see cref="IExportable"/>/<see cref="IImportable"/> pair may
/// use this internally as an ordinary constructor-injected collaborator, or
/// may serialize its own data directly.
/// </para>
/// <para>
/// Distinct from <see cref="IExportFormat"/>, which frames multiple
/// sections' own already-serialized bytes into one artifact — this
/// abstraction never sees more than one source's own data at a time, and
/// has no awareness of sections, kinds, or schema versions.
/// </para>
/// </remarks>
public interface IExportPayloadSerializer
{
    /// <summary>Serializes <paramref name="data"/> to its own byte representation.</summary>
    /// <param name="data">The data to serialize.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    byte[] Serialize(IReadOnlyDictionary<string, string> data);

    /// <summary>Deserializes <paramref name="payload"/> back into its own data.</summary>
    /// <param name="payload">The bytes previously produced by <see cref="Serialize"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <exception cref="CorruptedExportArtifactException"><paramref name="payload"/> is not well-formed.</exception>
    IReadOnlyDictionary<string, string> Deserialize(byte[] payload);
}

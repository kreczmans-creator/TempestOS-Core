namespace Tempest.Core.ExportImport;

/// <summary>
/// Frames one or more <see cref="ExportSection"/>s into a single artifact
/// stream, and reads that framing back — the "Format abstraction" named in
/// this Work Package's own implementation scope.
/// </summary>
/// <remarks>
/// <para>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft, which gave <see cref="IExportService"/> and
/// <see cref="IImportService"/> only a caller-supplied <see cref="Stream"/>
/// with no framing mechanism of their own. <see cref="ExportService"/> and
/// <see cref="ExportImport.ImportService"/> need some way to combine
/// multiple independently-written, opaque <see cref="IExportable.ExportAsync"/>
/// outputs into one artifact (and split them back apart on import) without
/// any single <see cref="IExportable"/> needing to know about any other —
/// this abstraction is that mechanism, kept entirely internal to
/// <see cref="ExportService"/>/<see cref="ExportImport.ImportService"/>'s
/// own orchestration. Distinct from <see cref="IExportPayloadSerializer"/>,
/// which is a separate, optional concern for a specific
/// <see cref="IExportable"/>'s own internal data — not used by
/// <see cref="ExportService"/>/<see cref="ExportImport.ImportService"/> at
/// all.
/// </para>
/// </remarks>
public interface IExportFormat
{
    /// <summary>
    /// Writes <paramref name="sections"/>, in order, as a single framed
    /// artifact to <paramref name="destination"/>.
    /// </summary>
    /// <param name="sections">The sections to frame together.</param>
    /// <param name="destination">The stream the framed artifact is written to.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    Task WriteAsync(IReadOnlyList<ExportSection> sections, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a previously framed artifact from <paramref name="source"/>
    /// back into its own original sections, in the order they were written.
    /// </summary>
    /// <param name="source">The stream the framed artifact is read from.</param>
    /// <param name="cancellationToken">A token observed while reading.</param>
    /// <exception cref="CorruptedExportArtifactException">
    /// <paramref name="source"/> does not contain a well-formed artifact
    /// this format recognises.
    /// </exception>
    Task<IReadOnlyList<ExportSection>> ReadAsync(Stream source, CancellationToken cancellationToken = default);
}

namespace Tempest.Samples;

/// <summary>
/// Holds the single most recently exported artifact, in memory, so
/// <see cref="ImportSampleDataCommandHandler"/> has something to read back
/// without this sample writing to disk. A demo-only convenience, not a
/// platform service — real callers supply their own destination/source
/// <see cref="Stream"/> directly to <see cref="Tempest.Core.ExportImport.IExportService"/>/
/// <see cref="Tempest.Core.ExportImport.IImportService"/>.
/// </summary>
public sealed class SampleExportArtifactStore
{
    /// <summary>
    /// Gets or sets the most recently exported artifact's own bytes, or
    /// <see langword="null"/> if nothing has been exported yet.
    /// </summary>
    public byte[]? Artifact { get; set; }
}

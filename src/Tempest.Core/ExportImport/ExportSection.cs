namespace Tempest.Core.ExportImport;

/// <summary>
/// One <see cref="IExportable"/>'s own already-written, opaque bytes, tagged
/// with the identifier and schema version <see cref="IImportService"/> uses
/// to route it back to a registered <see cref="IImportable"/> on import.
/// </summary>
/// <param name="Kind">The section's own stable identifier — see <see cref="IExportableKind"/>.</param>
/// <param name="SchemaVersion">The schema version the section was written with.</param>
/// <param name="Payload">The section's own already-serialized bytes, exactly as <see cref="IExportable.ExportAsync"/> wrote them.</param>
public sealed record ExportSection(string Kind, int SchemaVersion, byte[] Payload);

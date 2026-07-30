namespace Tempest.Core.ExportImport;

/// <summary>
/// An optional companion to <see cref="IExportable"/> that supplies the
/// stable identifier <see cref="IExportService"/> tags that source's own
/// artifact section with, so <see cref="IImportService"/> can later route
/// that section back to the matching registered <see cref="IImportable"/>.
/// </summary>
/// <remarks>
/// <para>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft, which gave <see cref="IExportable"/> only
/// <see cref="IExportable.SchemaVersion"/> and <see cref="IExportable.ExportAsync"/> —
/// an additive elaboration this Work Package's own implementation phase
/// introduces, mirroring `WP 6.0`'s own <c>IReportTemplate</c> and `WP
/// 6.1`'s own <c>IRole</c>/<c>IIdentityService</c> precedent: filling a gap
/// this Work Package's own brief named ("reads a previously exported
/// artifact back into the owning service(s)") without the approved
/// <see cref="IExportable"/>/<see cref="IImportService"/> shapes ever
/// carrying a section identifier of their own, and without modifying
/// either.
/// </para>
/// <para>
/// Entirely optional — a source that does not implement this interface is
/// still exportable; <see cref="ExportService"/> falls back to that
/// source's own runtime type name as its section's kind.
/// </para>
/// </remarks>
public interface IExportableKind
{
    /// <summary>
    /// The stable identifier this source's own artifact section is tagged
    /// with. Must match the <see cref="IImportable.Kind"/> of whatever
    /// <see cref="IImportable"/> is registered to read this section back.
    /// </summary>
    string Kind { get; }
}

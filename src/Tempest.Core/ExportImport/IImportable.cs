namespace Tempest.Core.ExportImport;

/// <summary>
/// The read-back counterpart to <see cref="IExportable"/> — registered
/// ahead of time so <see cref="IImportService.ImportAsync"/> can route an
/// artifact's own section back to its owning service by <see cref="Kind"/>.
/// </summary>
/// <remarks>
/// <para>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft — see <see cref="IExportableKind"/>'s own remarks
/// for why this gap needs an additive type rather than a change to
/// <see cref="IImportService"/>'s own approved, single-method shape.
/// </para>
/// <para>
/// Registered with the concrete <see cref="ExportImport.ImportService"/>
/// type (not <see cref="IImportService"/> itself) via
/// <see cref="ImportService.RegisterImportable"/>, typically during Module
/// Initialisation — mirroring <c>ADR-0044</c>'s own dual-registration
/// precedent for <c>CurrentPrincipalAccessor</c>: privileged registrants
/// resolve the concrete type, while every ordinary consumer resolves only
/// the read-only <see cref="IImportService"/> interface, both against the
/// exact same instance.
/// </para>
/// </remarks>
public interface IImportable
{
    /// <summary>
    /// The stable identifier this importable is registered under. An
    /// artifact section tagged with the same <see cref="IExportableKind.Kind"/>
    /// (or, absent that, the exporting source's own runtime type name) is
    /// routed to this importable on import.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// The schema version this importable currently supports. Compared for
    /// exact equality against the artifact section's own schema version —
    /// see <see cref="IncompatibleExportSchemaException"/>.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// Reads this section's own previously exported data from
    /// <paramref name="payload"/> back into the owning service.
    /// </summary>
    /// <param name="payload">The stream this section's own exported bytes are read from.</param>
    /// <param name="cancellationToken">A token observed while reading.</param>
    Task ImportAsync(Stream payload, CancellationToken cancellationToken = default);
}

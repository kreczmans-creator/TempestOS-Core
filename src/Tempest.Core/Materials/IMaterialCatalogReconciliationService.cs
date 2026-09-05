namespace Tempest.Core.Materials;

/// <summary>
/// The reconcile/repair path `TD-67`'s own register entry names as
/// missing: <see cref="MaterialCatalog.RegisterAsync"/> writes the
/// backing <see cref="EngineeringData.IEngineeringDocument"/> before
/// registering it in <see cref="MaterialCatalog.IndexCollectionName"/> —
/// a crash, or an index write failure, between the two leaves a material
/// document nothing can find through <see cref="MaterialCatalog.FindAsync"/>
/// or <see cref="MaterialCatalog.ListAsync"/>, yet it keeps consuming
/// storage forever. Mirrors <see cref="Requirements.IRequirementsReconciliationService"/>'s
/// own identical shape and repair discipline, for the sibling index this
/// namespace owns (`ADR-0055` Decision 3).
/// </summary>
/// <remarks>
/// An explicit, operator-invoked repair, never an automatic one — neither
/// method runs at startup or on any other schedule.
/// </remarks>
public interface IMaterialCatalogReconciliationService
{
    /// <summary>Scans every material specification without changing anything.</summary>
    Task<MaterialCatalogReconciliationReport> DetectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Repeats <see cref="DetectAsync"/>'s own scan and repairs every
    /// finding it can: a missing index entry is re-written from the
    /// orphan document's own recorded <c>materialId</c> — unless the
    /// index already names a <em>different</em> document under that same
    /// <c>materialId</c>, a genuine collision this sweep leaves alone
    /// rather than silently resolving; a stale index entry (naming a
    /// document that no longer exists or is no longer a
    /// <c>MaterialSpecification</c>) is removed.
    /// </summary>
    Task<MaterialCatalogReconciliationReport> SweepAsync(CancellationToken cancellationToken = default);
}

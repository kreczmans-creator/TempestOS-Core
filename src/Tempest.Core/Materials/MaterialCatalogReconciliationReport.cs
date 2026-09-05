namespace Tempest.Core.Materials;

/// <summary>One finding <see cref="IMaterialCatalogReconciliationService"/> reports (`TD-67`).</summary>
/// <param name="Category">One of the category constants declared on <see cref="MaterialCatalogReconciliationService"/>.</param>
/// <param name="DocumentId">The material specification document Id this finding is about.</param>
/// <param name="MaterialId">The <c>materialId</c> index key involved, when known.</param>
/// <param name="Description">A human-readable explanation, safe to surface directly to an operator.</param>
/// <param name="Repaired">Whether <see cref="IMaterialCatalogReconciliationService.SweepAsync"/> resolved this finding.</param>
public sealed record MaterialCatalogReconciliationFinding(string Category, Guid DocumentId, string? MaterialId, string Description, bool Repaired);

/// <summary>The complete result of one <see cref="IMaterialCatalogReconciliationService"/> pass.</summary>
/// <param name="Findings">Every orphan/stale-entry finding this pass reported. Empty if the catalog is fully consistent.</param>
public sealed record MaterialCatalogReconciliationReport(IReadOnlyList<MaterialCatalogReconciliationFinding> Findings);

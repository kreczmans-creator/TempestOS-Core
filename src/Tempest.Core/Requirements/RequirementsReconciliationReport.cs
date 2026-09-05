namespace Tempest.Core.Requirements;

/// <summary>
/// One finding <see cref="IRequirementsReconciliationService"/> reports —
/// a single Requirement/Requirement Collection/Requirement Group document
/// or index/registry entry whose forward or reverse pairing is missing
/// (`TD-67`).
/// </summary>
/// <param name="Category">
/// Which check found this — one of the <c>*MissingIndexEntry</c>/
/// <c>*MissingRegistryEntry</c> (a backing document exists with nothing
/// indexing it — the orphan `TD-67`'s own register entry names) or
/// <c>Stale*IndexEntry</c>/<c>Stale*RegistryEntry</c> (an index/registry
/// entry points at a document that no longer exists, or is no longer of
/// the expected Kind) constants declared on
/// <see cref="RequirementsReconciliationService"/>.
/// </param>
/// <param name="DocumentId">The document Id this finding is about.</param>
/// <param name="Key">The index/registry key involved, or <see langword="null"/> for a registry keyed by the document Id itself.</param>
/// <param name="Description">A human-readable explanation, safe to surface directly to an operator.</param>
/// <param name="Repaired">
/// Whether <see cref="IRequirementsReconciliationService.SweepAsync"/>
/// resolved this finding. Always <see langword="false"/> for a report
/// returned by <see cref="IRequirementsReconciliationService.DetectAsync"/>,
/// which never changes anything.
/// </param>
public sealed record RequirementsReconciliationFinding(string Category, Guid DocumentId, string? Key, string Description, bool Repaired);

/// <summary>The complete result of one <see cref="IRequirementsReconciliationService"/> pass.</summary>
/// <param name="Findings">Every orphan/stale-entry finding this pass reported. Empty if the store is fully consistent.</param>
public sealed record RequirementsReconciliationReport(IReadOnlyList<RequirementsReconciliationFinding> Findings);

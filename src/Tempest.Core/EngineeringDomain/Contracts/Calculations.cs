namespace Tempest.Core.EngineeringDomain;

public interface ICalculation : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata
{
}

public interface ICalculationSet : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    IReadOnlyList<Guid> MemberCalculationIds { get; }
}

/// <summary>
/// Retired (`TD-30`, closed `WP 21.3A`): zero implementations across five
/// releases (`WP 18.0A` through `WP 21.3A`), and none is planned.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why no implementation was given.</b> `WP 8.2C` declared this
/// interface deliberately unimplemented ("not given a competing concrete
/// realisation here"), reconciling the shape a future Domain-level
/// Calculation result might take against <c>Tempest.Core.Calculations</c>'s
/// own real, shipped <c>CalculationRecord&lt;TResult&gt;</c>. That record is
/// the platform's actual calculation evidence, and it is deliberately an
/// immutable, self-contained snapshot with no live service dependency of
/// its own (see its own remarks: "no hidden side channel a caller cannot
/// see"). Implementing <see cref="IHasRevisions"/> (a live
/// <c>ReviseAsync</c>/<c>GetRevisionHistoryAsync</c> pair) and
/// <see cref="IHasRelationships"/> (a live <c>LinkAsync</c>/<c>GetRelationshipsAsync</c>
/// pair) honestly would mean giving that record a document store and a
/// relationship repository of its own, and inventing what "revising" an
/// append-only calculation execution even means — a real Domain-model
/// question (`docs/governance/Future Capability Register.md`, `FCR-0051`:
/// "a real Domain design question, not a mechanical add"), not a thin
/// wrapper this package could add honestly within its own scope.
/// </para>
/// <para>
/// <see cref="EvidenceComposer"/> already discloses the practical
/// consequence: it pattern-matches every repository object against this
/// interface to build a Digital Thread evidence trail, and the match never
/// fires, so a Calculation's own evidence trail resolves empty (see
/// <c>Tempest.Workspace.Calculations.CalculationsPropertyFacetProvider</c>'s
/// own "honestly resolves empty" remark) — disclosed, non-blocking
/// (`docs/releases/v0.19.1/Technical Debt Rationalisation — Part 2.md`,
/// `TD-30`: "degrades silently... disclosed, non-blocking"). The interface
/// itself is left in place rather than deleted: <see cref="IEvidence"/> and
/// <see cref="ISimulation"/> already reference it structurally, and
/// removing it would be a second, unrelated change this package does not
/// own.
/// </para>
/// </remarks>
public interface ICalculationResult : IEngineeringObject, IHasRevisions, IHasRelationships, ITraceable
{
    Guid SubjectId { get; }
    IReadOnlyList<string> ReferencedMaterialIds { get; }
}

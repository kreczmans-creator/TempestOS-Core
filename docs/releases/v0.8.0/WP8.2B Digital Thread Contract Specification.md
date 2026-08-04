# WP 8.2B — Engineering Domain Contracts — Digital Thread Contract Specification

## Purpose

The contract shape realising `WP8.2A Digital Thread Specification.md`
— traceability, evidence, navigation, dependency traversal,
relationship discovery, and impact analysis. Proposed, uncompiled C#,
`Tempest.Core.EngineeringDomain` namespace throughout. Every contract
here is a **read-side composition** over `IHasRelationships`
(`Interface Catalogue.md` §1) — none proposes a new traversal
mechanism, index, or graph-storage technology, unchanged from
`WP8.2A Digital Thread Specification.md` §1's own founding claim.

## 1. Relationship Discovery

```csharp
/// <summary>Direct relationships only — one hop (mirrors GetReferencesAsync exactly).</summary>
public interface IRelationshipDiscovery
{
    Task<IReadOnlyList<IEngineeringRelationship>> GetOutgoingAsync(Guid objectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IEngineeringRelationship>> GetIncomingAsync(Guid objectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IEngineeringRelationship>> GetByCategoryAsync(Guid objectId, RelationshipCategory category, CancellationToken cancellationToken = default);
}
```

## 2. Dependency Traversal

```csharp
/// <summary>Multi-hop traversal — bounded by default (WP8.2A Digital Thread Specification.md §3's own disclosed depth limitation, restated as a contract default).</summary>
public interface IDependencyTraversal
{
    Task<IReadOnlyList<IEngineeringObject>> TraverseAsync(
        Guid startObjectId,
        RelationshipCategory category,
        int maxDepth = 1,
        CancellationToken cancellationToken = default);
}
```

`maxDepth` defaults to `1` deliberately — the same disclosed limitation
`ADR-0065` already names for the Workspace's own Digital Thread panel
("shows only one hop... tracing a longer chain requires repeated
manual navigation"), now given an explicit, overridable contract
default rather than left as an unstated implementation detail. A
caller needing a deeper traversal opts in explicitly.

## 3. Evidence Composition

```csharp
/// <summary>Composes IEvidence (Interface Catalogue.md §13) via the three-step recipe WP8.2A Digital Thread Specification.md §3 already defines.</summary>
public interface IEvidenceComposer
{
    Task<IEvidence> ComposeAsync(Guid subjectId, CancellationToken cancellationToken = default);
}
```

A conforming implementation performs exactly the three steps
`WP8.2A Digital Thread Specification.md` §3 already names: (1)
`IRelationshipDiscovery.GetOutgoingAsync`; (2) for each `Verification`
category result, resolve the linked `IVerificationResult`; (3) for
each `Calculation` category result, resolve the linked
`ICalculationResult`. No fourth step, no new data source.

## 4. Impact Analysis

```csharp
/// <summary>"What depends on this object, transitively" — the inverse of Dependency Traversal, over Dependency/Allocation/Verification categories.</summary>
public interface IImpactAnalysis
{
    Task<IReadOnlyList<IEngineeringObject>> GetImpactedObjectsAsync(
        Guid changedObjectId,
        int maxDepth = 1,
        CancellationToken cancellationToken = default);
}
```

Named for the first time at the contract stage — `WP 8.2A` did not
name Impact Analysis explicitly, but the controlling instruction for
this Work Package does. Realised entirely as `IDependencyTraversal`
run in the incoming direction (§1's own `GetIncomingAsync`), composed
over `Dependency`/`Allocation`/`Verification` categories specifically
(the three categories most likely to represent "X's own correctness
depends on Y") — no new traversal primitive, a specific, named
composition of §1/§2's own two contracts.

## 5. Navigation

No `INavigableThread`/`IThreadCursor` contract is proposed. Navigating
the Digital Thread, at the Engineering Domain layer, is simply calling
`IRelationshipDiscovery`/`IDependencyTraversal` repeatedly and
following the `Guid`s returned — exactly how `IRequirementsService.
GetEvidenceAsync` already works today, and exactly how the Workspace's
own `INavigationService.JumpToAsync` (`Tempest.App.Workspace`, `WP
8.1A`) already presents the result to a user. This layer supplies data;
presentation-layer navigation remains the Workspace's own concern,
unchanged.

## 6. Future Graph Traversal Compatibility

Every interface in this document returns `IReadOnlyList<T>` /
`Task<T>` — ordinary, synchronous-shaped async contracts with no
assumption about the traversal's own underlying implementation. A
future graph-database-backed implementation of `IRelationshipDiscovery`/
`IDependencyTraversal` (should one ever be built) satisfies these
contracts identically to today's `GetReferencesAsync`-based one — this
is the concrete meaning of "support future persistence mechanisms"
(`WP8.2B Engineering Domain Contracts.md` §1) applied to the Digital
Thread specifically. No contract in this document exposes a query
language, a cursor, or a pagination token that would presuppose one
storage technology over another.

## Related Documents

`WP8.2B Engineering Domain Contracts.md`; `WP8.2B Interface
Catalogue.md`; `WP8.2B Relationship Contract Specification.md`;
`WP8.2A Digital Thread Specification.md`; `ADR-0065`;
`IRequirementsService.GetEvidenceAsync`.

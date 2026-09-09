# WP 8.2B — Engineering Domain Contracts — Lifecycle Contract Specification

## Purpose

The contract shape realising `WP8.2A Lifecycle Specification.md`'s own
canonical eight-state vocabulary and per-family specialisation
(`ADR-0074`). Proposed, uncompiled C#, `Tempest.Core.EngineeringDomain`
namespace throughout.

## 1. `LifecycleState`

```csharp
/// <summary>The canonical eight-state vocabulary (WP8.2A Lifecycle Specification.md §1).</summary>
public enum LifecycleState
{
    Draft,
    InReview,
    Approved,
    Released,
    Superseded,
    Obsolete,
    Archived,
    Cancelled,
}
```

A family that specialises this vocabulary (`RequirementStatus`'s own
real, shipped seven values, for instance) is not proposed as a
different enum — `IHasLifecycle.Status` always returns a
`LifecycleState`; a family's own additional, inserted states
(`ADR-0074`'s own rule 1) are represented as an **extension**, not a
replacement:

```csharp
/// <summary>An inserted, family-specific state between Approved and a terminal state (ADR-0074 rule 1).</summary>
public interface IFamilySpecificState
{
    string Name { get; }
    LifecycleState CanonicalEquivalent { get; }
}
```

A future implementation Work Package realising `IRequirement` maps
`RequirementStatus.Allocated`/`.Verified`/`.Satisfied` to three
`IFamilySpecificState` values, each reporting `LifecycleState.Approved`
as its own `CanonicalEquivalent` until the object reaches its own
family-specific terminal state — this lets platform-wide code
(`Search`/`Governance`) always ask "is this in one of the two universal
terminal states" via `LifecycleState` alone, while family-aware code
uses the richer, family-specific value.

## 2. Transitions

```csharp
public interface ILifecycleTransitionRecord
{
    LifecycleState From { get; }
    LifecycleState To { get; }
    string ActorPrincipalId { get; }
    DateTimeOffset OccurredAt { get; }
    Guid? ApprovalId { get; }
}

/// <summary>The canonical transition table (WP8.2A Lifecycle Specification.md §2), queryable rather than hard-coded per family.</summary>
public interface ILifecycleTransitionTable
{
    bool IsPermitted(LifecycleState from, LifecycleState to);

    IReadOnlyList<LifecycleState> GetPermittedTargets(LifecycleState from);
}
```

`ILifecycleTransitionTable` is the contract-level generalisation of
`RequirementStatusTransitions`'s own shipped shape — every object
family gets an instance of this contract representing its own
effective table (canonical plus specialisation), rather than each
family hand-rolling its own static lookup independently.

## 3. Approval

```csharp
/// <summary>Gates InReview -> Approved (WP8.2A Lifecycle Specification.md §3).</summary>
public interface IApprovalGate
{
    Task<bool> IsSatisfiedAsync(Guid objectId, CancellationToken cancellationToken = default);

    /// <summary>The real, resolvable IApproval satisfying this gate, if one exists.</summary>
    Task<IApproval?> GetSatisfyingApprovalAsync(Guid objectId, CancellationToken cancellationToken = default);
}
```

Realised, at the object level, by checking for a resolvable
`Approved By` relationship (`WP8.2A Relationship Catalogue.md` §4) —
`IApprovalGate` names the check as its own contract so a future
implementation is not forced to inline the same relationship-traversal
logic once per object family.

## 4. Review

```csharp
/// <summary>Requests and tracks a Review prior to an Approval decision.</summary>
public interface IReviewGate
{
    Task<IReview> RequestReviewAsync(Guid objectId, IReadOnlyList<string> reviewerPrincipalIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReview>> GetReviewsAsync(Guid objectId, CancellationToken cancellationToken = default);
}
```

## 5. Release / Baseline

```csharp
/// <summary>Gates a Baseline's own transition to Released (WP8.2A Configuration Management Specification.md §4).</summary>
public interface IReleaseGate
{
    Task<bool> IsSatisfiedAsync(Guid baselineId, CancellationToken cancellationToken = default);
}
```

No separate `IBaselineGate` is proposed — a Baseline is itself an
`IConfiguration` (`Interface Catalogue.md` §3) with its own
`IHasLifecycle`; reaching `Released` uses the same `IApprovalGate`/
`ILifecycleTransitionTable` mechanism as every other object, not a
fourth gate type.

## 6. Obsolete / Archive

No dedicated contract — `LifecycleState.Obsolete`/`.Archived` are
ordinary values in the canonical table (§1). "Obsoleting" or
"archiving" an object is an ordinary `IHasLifecycle.TransitionAsync`
call, never a distinct operation or interface — directly satisfying
`WP8.2A Lifecycle Specification.md` §5's own "no Engineering Object is
ever physically deleted" rule at the contract level: there is no
`DeleteAsync` anywhere in this catalogue.

## 7. Validation

```csharp
/// <summary>Checked before any transition is permitted (WP8.2A Validation Specification.md §4).</summary>
public interface ILifecycleValidationRule
{
    Task<IValidationResult> ValidateTransitionAsync(Guid objectId, LifecycleState from, LifecycleState to, CancellationToken cancellationToken = default);
}
```

A concrete rule implementing this contract realises, for example, the
"a `Blocks` relationship prevents lifecycle advancement" rule
(`WP8.2A Validation Specification.md` §4.3) — named as architecture in
`WP 8.2A`, given a concrete contract shape here for the first time.

## 8. History

`IHasLifecycle.History` (`Interface Catalogue.md` §1) is the complete
history contract — an ordered, append-only
`IReadOnlyList<ILifecycleTransitionRecord>`, mirroring
`IDocumentRevision`'s own append-only precedent exactly. No separate
history query contract is proposed; the full history is always
available directly from the object itself.

## Related Documents

`WP8.2B Engineering Domain Contracts.md`; `WP8.2B Interface
Catalogue.md`; `WP8.2A Lifecycle Specification.md`; `WP8.2A Validation
Specification.md`; `ADR-0074`; `RequirementStatus`/
`RequirementStatusTransitions` (`src/Tempest.Core/Requirements/`).

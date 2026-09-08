# WP 7.2C — Requirements Platform Contracts

## Status

**Contract Review — documentation only. No implementation.** This
document is the pre-implementation review of the Requirements &
Verification Platform (`FCR-0027`), designed by `WP 7.2B`, Engineering
Review APPROVED — mirroring `WP7.0C Engineering Foundation
Contracts.md`'s own identical role for the original five Engineering
Foundation frameworks. Every interface below is a **proposed** design
artifact for review — matching this codebase's own XML-doc and naming
conventions exactly — without being compiled, tested, or committed as
source. No interface below is final; each remains subject to the owning
implementation Work Package's own confirmation, where `WP7.2C Required
ADR Catalogue.md`'s own reserved decisions are actually answered.

## How to Read This Document

Per this Work Package's own controlling instruction, each contract
answers the same seventeen questions: Purpose, Responsibilities, Public
Interface, Lifetime, Ownership, Consumers, Dependencies, Thread-Safety
Expectations, Failure Behaviour, Exception Model, Serialization
Expectations, Extension Points, Platform Service Consumption,
Engineering Core Consumption, Security Considerations, Testing
Strategy, Academy Requirements. Where a question's own answer is
identical to a concept already fully answered above it, the later
section states this explicitly and cross-references rather than
repeating verbatim — the same "does not apply, say so" discipline
`WP7.0C Engineering Foundation Contracts.md` itself established.

## Design Principles Applied Uniformly

Every signature below follows a rule already established somewhere in
this platform, mirroring `WP7.0C Engineering Foundation Contracts.md`'s
own identical discipline:

- A nullable-return lookup (`FindAsync`, `FindByIdentifierAsync`) paired
  with a throwing primary method where "not found" is exceptional —
  mirrors `IMaterialCatalog`/`IEngineeringDocumentStore`'s own precedent.
- An abstract base exception per namespace, concrete leaf exceptions
  beneath it — mirrors every existing Engineering Foundation namespace.
- Optional `ILogger?` constructor parameter on the one DI-registered
  service (`IRequirementsService`'s own concrete implementation); no
  logger on any pure data-contract type.
- Immutable data contracts for anything representing a past fact or a
  current, read-only snapshot — mirrors `IVerificationRecord`/
  `CalculationRecord<TResult>`.

---

## 1. Requirements Engine (`IRequirementsService`)

**Purpose.** The canonical entry point for the Systems Engineering
Foundation — creates, retrieves, revises, sets the lifecycle status of,
and relates requirements.

**Responsibilities.** Owns requirement creation, business-identifier
lookup, statement revisioning, lifecycle status transitions, and the
relationship-recording surface every other domain concept in this
document builds on. Does **not** own verification recording (delegated
to `IVerificationService`), report generation, export framing, or REST
exposure.

**Public Interface.**

```csharp
namespace Tempest.Core.Requirements;

/// <summary>
/// Registers, retrieves, revises, and relates requirements. Each
/// requirement is itself an
/// <see cref="Tempest.Core.EngineeringData.IEngineeringDocument"/> of
/// <c>Kind = "Requirement"</c> — this service is an indexed, typed view
/// over that shared store, never a second storage mechanism.
/// </summary>
public interface IRequirementsService
{
    /// <summary>Creates a new requirement with the given business identifier and statement.</summary>
    /// <exception cref="DuplicateRequirementIdentifierException"><paramref name="identifier"/> is already registered.</exception>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> or <paramref name="statement"/> is null, empty, or whitespace.</exception>
    Task<IRequirement> CreateAsync(string identifier, string statement, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the requirement, or <see langword="null"/> if none exists.</summary>
    Task<IRequirement?> FindAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>Returns the requirement registered under <paramref name="identifier"/>, or <see langword="null"/> if none is registered.</summary>
    Task<IRequirement?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>Records a new revision of the requirement's own statement.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task<IRequirement> ReviseAsync(Guid requirementId, string newStatement, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>Sets the requirement's own current lifecycle status.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    /// <exception cref="InvalidRequirementStatusTransitionException">The requested transition is not permitted from the requirement's own current status — see <c>WP7.2C Requirement Lifecycle Model.md</c>.</exception>
    Task SetStatusAsync(Guid requirementId, RequirementStatus status, CancellationToken cancellationToken = default);

    /// <summary>Records a typed, directed relationship from a requirement to another requirement, a group, a collection, or any other document.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="sourceRequirementId"/> does not exist.</exception>
    /// <exception cref="EngineeringData.EngineeringDocumentNotFoundException"><paramref name="targetDocumentId"/> does not exist.</exception>
    Task LinkAsync(Guid sourceRequirementId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default);

    /// <summary>Every relationship recorded with <paramref name="requirementId"/> as its own source. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<EngineeringData.DocumentReference>> GetRelationshipsAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>Every requirement currently registered. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IRequirement>> ListAsync(CancellationToken cancellationToken = default);
}
```

**Lifetime.** DI-public, container-constructed singleton — proposed for
the Phase 6 registration slot immediately after `IVerificationService`,
mirroring every Engineering Foundation framework's own registration
shape.

**Ownership.** `Tempest.Core.Requirements` owns the mapping from a
business identifier to a document Id and every relationship-kind
constant this Platform reserves (`WP7.2C Relationship Model.md`). It
does not own the underlying document/revision storage, delegated
entirely to `IEngineeringDocumentStore`.

**Consumers.** Any future module registering, revising, or relating
requirements; any future Engineering Discipline module allocating a
requirement to its own domain artefact; a future Requirements
Traceability Report (`Reporting Integration`, below).

**Dependencies.** `Tempest.Core.EngineeringData` (hard); `Tempest.Core.
Verification` (hard, for the separate verification-recording path, never
through this interface — see §8, Requirement Verification Link);
`Tempest.Core.Identity`/`Tempest.Core.Audit` (hard, calling-layer only).

**Thread-Safety Expectations.** Every method must be safe for concurrent
invocation, mirroring `IMaterialCatalog`/`IVerificationService`. `CreateAsync`'s
own identifier-uniqueness check and `ReviseAsync`'s own revision-number
increment must each be atomic per requirement, mirroring
`IEngineeringDocumentStore.ReviseAsync`'s own existing guarantee. **No
guarantee is proposed for cross-author edit-conflict detection** — see
Security Considerations, below, and `ADR-0060` (`WP7.2C Required ADR
Catalogue.md`, carried forward unresolved from `WP7.2B`).

**Failure Behaviour.** No method silently no-ops. `CreateAsync` against
a duplicate identifier fails, never overwrites. `SetStatusAsync` against
an invalid transition fails, never silently clamps to the nearest valid
state. Every failure from the underlying `IEngineeringDocumentStore`
propagates unmodified.

**Exception Model.** `RequirementsException` (abstract base) →
`DuplicateRequirementIdentifierException`, `RequirementNotFoundException`,
`InvalidRequirementStatusTransitionException` — one namespace, one base,
concrete leaves, mirroring every existing Engineering Foundation
exception hierarchy.

**Serialization Expectations.** `IRequirementsService` itself is never
serialized — it is a service, not a data contract. Every method's own
persisted representation (an internal DTO for `IRequirement`'s own
current state) is serialized via `System.Text.Json`, mirroring
`VerificationRecordDto`'s own precedent, and remains this framework's
own internal concern, never a public contract.

**Extension Points.** `relationshipKind` remains an open `string`, not a
closed enum — mirroring `IVerificationService`'s own identical decision
for `method` — so a future consumer is never blocked by a relationship
vocabulary this Platform does not yet have grounds to close (see
`WP7.2C Relationship Model.md`).

**Platform Service Consumption.** `Tempest.Core.Identity`
(`IPermissionEvaluator`/`ICurrentPrincipalAccessor`, calling-layer);
`Tempest.Core.Audit` (`IAuditRecorder`, calling-layer). Neither is
called internally by `IRequirementsService` itself — both are the
calling layer's own responsibility, mirroring `IReportingService`'s own
explicit precedent.

**Engineering Core Consumption.** `Tempest.Core.EngineeringData`
(`IEngineeringDocumentStore`, hard); `Tempest.Core.Verification`
(`IVerificationService`, hard, but never invoked *through* this
interface — see §8).

**Security Considerations.** Authorisation and auditability are both
calling-layer concerns, identical to every Engineering Core sibling.
Concurrent editing of the same requirement is a disclosed, unresolved
gap (`ADR-0060`) — see `WP7.2C Security Review.md`.

**Testing Strategy.** Create/find/revise round-trip; duplicate-identifier
rejection; status-transition validity (every permitted and forbidden
transition in `WP7.2C Requirement Lifecycle Model.md`); relationship
recording and retrieval round-trip; failure-path propagation for every
named exception. Full detail: `WP7.2C Testing Strategy.md`.

**Academy Requirements.** A new concept guide, per `WP7.2B Academy
Plan.md`'s own recommendation, written once real implementation exists.
Full detail: `WP7.2C Academy Plan.md`.

---

## 2. Requirement (`IRequirement`)

**Purpose.** The core domain entity — a single, stated engineering
requirement, identity and business key, current statement, category,
and lifecycle status.

**Responsibilities.** Owns its own business identifier, statement text,
category, status, and revision number. Does **not** own what satisfies
it, what verifies it, or what it is allocated to — each is a
relationship, resolved through `IRequirementsService.GetRelationshipsAsync`
or `IVerificationService.GetVerificationHistoryAsync`, never a field on
this contract itself.

**Public Interface.**

```csharp
namespace Tempest.Core.Requirements;

public interface IRequirement
{
    /// <summary>The underlying <see cref="Tempest.Core.EngineeringData.IEngineeringDocument"/>'s own stable identity.</summary>
    Guid Id { get; }

    /// <summary>The stable, human-facing business identifier (e.g., <c>"SYS-REQ-042"</c>).</summary>
    string Identifier { get; }

    /// <summary>The requirement's own current statement text — opaque to this framework, uninterpreted.</summary>
    string Statement { get; }

    /// <summary>An open, caller-defined classification (e.g., <c>"functional"</c>, <c>"safety"</c>). <see langword="null"/> if uncategorised.</summary>
    string? Category { get; }

    /// <summary>The requirement's own current lifecycle status — see <c>WP7.2C Requirement Lifecycle Model.md</c>.</summary>
    RequirementStatus Status { get; }

    /// <summary>The current revision number, mirroring <see cref="Tempest.Core.EngineeringData.IEngineeringDocument.CurrentRevisionNumber"/>.</summary>
    int RevisionNumber { get; }

    string CreatedByPrincipalId { get; }
    DateTimeOffset CreatedAt { get; }
}
```

**Lifetime.** Not a DI-registered type — an immutable, read-only
snapshot returned by `IRequirementsService`, mirroring `IMaterialSpecification`'s
own identical shape.

**Ownership.** `Tempest.Core.Requirements` owns this contract's own
shape; `Tempest.Core.EngineeringData` owns the underlying storage and
revisioning it is projected from.

**Consumers.** Any caller of `IRequirementsService`; a future
Requirements Traceability Report; a future Requirements Collection/
Group member enumeration.

**Dependencies.** `Tempest.Core.EngineeringData` (is-a
`IEngineeringDocument`, `Kind = "Requirement"`).

**Thread-Safety Expectations.** Trivially satisfied — an immutable
snapshot, safe to share across threads without synchronization.

**Failure Behaviour.** Not applicable — a pure data contract, no
behaviour to fail.

**Exception Model.** None — no method exists on this contract to throw.

**Serialization Expectations.** The concrete implementation's own
internal DTO is `System.Text.Json`-serializable, mirroring every
existing Engineering Foundation DTO (`MaterialSpecificationDto`,
`VerificationRecordDto`). `IRequirement` itself, as a public interface,
is never directly serialized — only its concrete backing type.

**Extension Points.** `Category` remains an open, nullable string — see
`WP7.2B Requirements Domain Model.md` §12; no closed taxonomy is
proposed.

**Platform Service Consumption.** None directly — `CreatedByPrincipalId`
is populated by `IRequirementsService`'s own implementation via
`ICurrentPrincipalAccessor`, mirroring `IEngineeringDocumentStore`'s own
existing pattern.

**Engineering Core Consumption.** `Tempest.Core.EngineeringData`
(direct, is-a relationship).

**Security Considerations.** Identity ownership (`CreatedByPrincipalId`)
is inherited, already-proven; see `WP7.2C Security Review.md`.

**Testing Strategy.** Confirmed via `IRequirementsService`'s own round-trip
tests — no separate test surface exists for a pure data contract. Full
detail: `WP7.2C Testing Strategy.md`.

**Academy Requirements.** Covered by `IRequirementsService`'s own
concept guide — no separate Academy content proposed for this contract
alone.

---

## 3. Requirement Collection (`IRequirementCollection`)

**Purpose.** A named, purpose-built set of requirements — a baseline, a
release scope, a review package.

**Responsibilities.** Owns membership (which requirements belong) and
its own name/identity. Does not own any data about a member requirement
itself.

**Public Interface.**

```csharp
namespace Tempest.Core.Requirements;

public interface IRequirementCollection
{
    Guid Id { get; }
    string Name { get; }

    /// <summary>Every requirement Id this collection currently contains. Never <see langword="null"/>.</summary>
    IReadOnlyList<Guid> MemberRequirementIds { get; }
}
```

**Lifetime.** Not DI-registered — an immutable snapshot, mirroring
`IRequirement`. Created and queried through `IRequirementsService`'s own
collection-management methods (proposed, not detailed further here —
deferred to the owning implementation Work Package's own signature
choice, since this Work Package's own controlling instruction asks for
architectural responsibilities, not a finalised method list, for every
concept beyond the Engine and the Requirement itself).

**Ownership.** `Tempest.Core.Requirements`; underlying storage delegated
to `IEngineeringDocumentStore` (`Kind = "RequirementCollection"`).

**Consumers.** A future baseline/release-scope management workflow; a
future Export/Import consumer (§ Reporting Integration, below).

**Dependencies.** `Tempest.Core.EngineeringData` (is-a
`IEngineeringDocument`); references member requirements by Id via
`LinkAsync`, never containing their own data.

**Thread-Safety Expectations.** Identical to `IRequirementsService`'s
own general requirement — safe for concurrent invocation; concurrent
membership changes to the *same* collection carry the same disclosed,
unresolved conflict-detection gap as `ReviseAsync` generally (`ADR-0060`).

**Failure Behaviour.** Adding a non-existent requirement Id as a member
fails (`EngineeringDocumentNotFoundException`), never silently ignored.

**Exception Model.** Reuses `RequirementsException`'s own hierarchy; no
new exception type proposed for this concept specifically.

**Serialization Expectations.** Mirrors `IRequirement`'s own answer —
internal DTO, `System.Text.Json`.

**Extension Points.** No constraint on collection size or membership
overlap — a requirement may belong to more than one collection
simultaneously, mirroring how a document may be the target of more than
one `LinkAsync` relationship.

**Platform Service Consumption.** None beyond the calling-layer pattern
`IRequirementsService` itself already establishes.

**Engineering Core Consumption.** `Tempest.Core.EngineeringData`,
directly.

**Security Considerations.** Identical profile to `IRequirement`
generally — see `WP7.2C Security Review.md`.

**Testing Strategy.** Membership add/remove/enumerate round-trip;
non-existent-member rejection. Full detail: `WP7.2C Testing
Strategy.md`.

**Academy Requirements.** Covered within the same concept guide as
`IRequirementsService` — collections are a worked example, not a
separate pattern.

---

## 4. Requirement Group (`IRequirementGroup`)

**Purpose.** A hierarchical categorisation node — the "requirement
hierarchy" `WP7.2B Requirements Platform Architecture.md` names,
distinct from Requirement Collection's own non-hierarchical, purpose-built
grouping.

**Responsibilities.** Owns its own position in a hierarchy (a parent
group reference) and its own name.

**Public Interface.**

```csharp
namespace Tempest.Core.Requirements;

public interface IRequirementGroup
{
    Guid Id { get; }
    string Name { get; }

    /// <summary><see langword="null"/> if this is a root group.</summary>
    Guid? ParentGroupId { get; }
}
```

**Every remaining question (Lifetime, Ownership, Consumers, Dependencies,
Thread-Safety, Failure Behaviour, Exception Model, Serialization,
Extension Points, Platform/Engineering Core Consumption, Security,
Testing, Academy) is answered identically to Requirement Collection,
§3, above** — the two concepts share an identical architectural shape
(an `IEngineeringDocument` linking to other documents via `LinkAsync`),
differing only in their own relationship-kind semantics
(`"groupedUnder"` forming a tree, vs. `"collects"` forming an
unordered set) — see `WP7.2C Relationship Model.md`.

---

## 5. Requirement Relationship, Requirement Allocation, and Requirement Trace Link

**These three concepts share one underlying mechanism and are
documented together** — `WP7.2C Relationship Model.md` gives the
complete design; this section states only what is common to all three
at the contract level.

**Purpose.** A general-purpose, typed, directed link between a
requirement and anything else — another requirement (Relationship), an
allocation target of any kind (Allocation), or a derivation/satisfaction
source (Trace Link).

**Responsibilities.** None of the three introduces a new stored type —
each is a `Tempest.Core.EngineeringData.DocumentReference`, created via
`IRequirementsService.LinkAsync` (itself a thin wrapper over
`IEngineeringDocumentStore.LinkAsync`), distinguished only by its own
`RelationshipKind` string value.

**Public Interface.** No new interface is proposed for any of the
three — each is represented entirely by an already-proposed
`DocumentReference` plus a reserved relationship-kind constant:

```csharp
namespace Tempest.Core.Requirements;

/// <summary>Reserved relationship-kind constants for the Requirements Platform.</summary>
public static class RequirementRelationshipKinds
{
    /// <summary>Requirement Group hierarchy (parent).</summary>
    public const string GroupedUnder = "groupedUnder";

    /// <summary>Requirement Collection membership.</summary>
    public const string CollectedIn = "collects";

    /// <summary>Requirement Relationship — general dependency.</summary>
    public const string DependsOn = "dependsOn";

    /// <summary>Requirement Trace Link — derivation.</summary>
    public const string DerivesFrom = "derivesFrom";

    /// <summary>Requirement Allocation — an allocation target of any kind.</summary>
    public const string AllocatedTo = "allocatedTo";

    /// <summary>Requirement Relationship — a non-owning cross-reference.</summary>
    public const string References = "references";

    /// <summary>Requirement Trace Link — satisfaction.</summary>
    public const string Satisfies = "satisfies";

    // "verifiedBy" is deliberately NOT reserved here — it is
    // Tempest.Core.Verification's own existing relationship kind,
    // created by VerificationService itself, never by this Platform.
}
```

**Lifetime, Ownership.** Not applicable in the DI sense — these are
`string` constants and data produced by `IEngineeringDocumentStore.
LinkAsync`, never their own service or stored entity.

**Consumers.** `WP7.2B Digital Thread Architecture.md`'s own traversal
design; any future Requirements Traceability Report.

**Dependencies.** `Tempest.Core.EngineeringData` (`LinkAsync`/
`GetReferencesAsync`), directly, no new mechanism.

**Thread-Safety Expectations.** Inherited entirely from
`IEngineeringDocumentStore.LinkAsync`'s own existing guarantee — each
call writes an independent, randomly-keyed reference entry (`TD-18`'s
own disclosed scope, unchanged by this Platform).

**Failure Behaviour.** A link to a non-existent target document fails
(`EngineeringDocumentNotFoundException`), inherited directly.

**Exception Model.** No new exception type — reuses
`EngineeringDocumentNotFoundException`/`RequirementNotFoundException`.

**Serialization Expectations.** `DocumentReference` is already a
serializable `sealed record` (`Tempest.Core.EngineeringData`); no change
proposed.

**Extension Points.** New relationship kinds may be added to
`RequirementRelationshipKinds` purely additively — no existing
relationship kind is renamed or removed by adding a new one, mirroring
`Capability Categories.md`'s own additive-only discipline.

**Platform Service Consumption.** None.

**Engineering Core Consumption.** `Tempest.Core.EngineeringData`,
directly, exclusively.

**Security Considerations.** Traceability integrity (append-only
references, `WP7.2B Security Architecture.md`'s own finding) applies
identically to all three — see `WP7.2C Security Review.md`.

**Testing Strategy.** One test per relationship kind, confirming it is
recorded and retrievable via `GetRelationshipsAsync`/`GetReferencesAsync`;
no relationship-kind-specific behaviour exists to test beyond the string
value itself. Full detail: `WP7.2C Testing Strategy.md` and `WP7.2C
Traceability Contract.md`.

**Academy Requirements.** Covered within the primary concept guide — the
relationship-kind vocabulary is presented as a worked table, not a
separate pattern requiring its own explanation.

**Confirmation, per this Work Package's own controlling instruction:**
whether each belongs in the initial implementation or is a future
extension point is answered in full in `WP7.2C Relationship Model.md`.

---

## 6. Requirement Verification Link

**This is not a new contract.** Per `WP7.2B Requirements Platform
Architecture.md` §4 and `WP7.2B Platform Integration Report.md` §1,
recording a requirement's own verification is a direct call to
`Tempest.Core.Verification.IVerificationService.RecordAsync(requirement.Id,
outcome, method, context)` — the existing, unmodified contract, reused,
never wrapped or duplicated. See `WP7.2C Verification Integration
Contract.md` for the complete confirmation of ownership, responsibility,
dependency direction, and the explicit absence of duplicated behaviour.

---

## 7. Requirement Evidence

**Purpose.** The complete evidentiary basis for a requirement's own
status — an aggregation, not a new stored entity.

**Responsibilities.** Presents, in one coherent read, every
`IVerificationRecord.Criteria`/`Evidence` recorded against a
requirement, every linked `CalculationRecord`'s own assumptions and
results, and every linked supporting document. Owns only the
*presentation shape* of this aggregation.

**Public Interface.**

```csharp
namespace Tempest.Core.Requirements;

/// <summary>
/// A read-side aggregation of every fact bearing on a requirement's own
/// evidentiary status. Owns no new stored data — every field is drawn
/// from an existing Engineering Core record.
/// </summary>
public interface IRequirementEvidence
{
    Guid RequirementId { get; }

    /// <summary>Every verification recorded against this requirement, oldest first — from <see cref="Tempest.Core.Verification.IVerificationService.GetVerificationHistoryAsync"/> directly.</summary>
    IReadOnlyList<Verification.IVerificationRecord> VerificationHistory { get; }

    /// <summary>Every document Id referenced by this requirement's own relationships (allocations, trace links, references) — from <see cref="IRequirementsService.GetRelationshipsAsync"/> directly.</summary>
    IReadOnlyList<EngineeringData.DocumentReference> LinkedReferences { get; }
}
```

**Lifetime.** Not DI-registered — a computed, on-demand projection,
never stored, produced by a proposed `IRequirementsService.
GetEvidenceAsync(Guid requirementId)` method (or equivalent; the exact
method placement is deferred to the owning implementation Work
Package).

**Ownership.** `Tempest.Core.Requirements` owns the aggregation
*shape*; `Tempest.Core.Verification` and `Tempest.Core.EngineeringData`
each own every underlying fact presented.

**Consumers.** A future Requirements Traceability Report; a future
compliance-support consumer (`WP7.2B Standards Mapping.md`).

**Dependencies.** `Tempest.Core.Verification` (`GetVerificationHistoryAsync`,
hard); `Tempest.Core.Requirements` (`GetRelationshipsAsync`, hard).

**Thread-Safety Expectations.** Trivially satisfied — a read-only,
point-in-time projection; no shared mutable state.

**Failure Behaviour.** Requesting evidence for a non-existent
requirement fails (`RequirementNotFoundException`), never returns an
empty, misleadingly-valid-looking result.

**Exception Model.** `RequirementNotFoundException`
(`Tempest.Core.Requirements`); `PermissionDeniedException`
(`Tempest.Core.Identity`, inherited from `GetVerificationHistoryAsync`'s
own existing permission gate).

**Serialization Expectations.** Composed entirely of already-serializable
types (`IVerificationRecord`, `DocumentReference`) — no new
serialization concern introduced.

**Extension Points.** Additional aggregated fact types (a future
Requirements Collection's own membership, for instance) may be added to
this projection purely additively, without changing any existing field.

**Platform Service Consumption.** None directly — inherits
`IVerificationService`'s own existing permission gate on
`GetVerificationHistoryAsync`.

**Engineering Core Consumption.** `Tempest.Core.Verification`,
`Tempest.Core.EngineeringData` — both directly, both read-only.

**Security Considerations.** Permission-gated by inheritance
(`Tempest.Core.Verification`'s own read-side gate) — no new
authorization surface introduced.

**Testing Strategy.** Aggregation correctness against a requirement with
a known set of verifications, calculation references, and linked
documents; permission-gating inherited-behaviour confirmation. Full
detail: `WP7.2C Testing Strategy.md`.

**Academy Requirements.** A worked example within the primary concept
guide, demonstrating the digital thread traversal
(`WP7.2B Digital Thread Architecture.md`) concretely.

---

## 8. Requirement Status

**Purpose.** The requirement's own lifecycle/workflow position —
deliberately distinct from `VerificationOutcome`. See `WP7.2C
Requirement Lifecycle Model.md` for the complete state model.

**Public Interface.**

```csharp
namespace Tempest.Core.Requirements;

/// <summary>A requirement's own lifecycle position — a workflow state, never derived automatically from a VerificationRecord's own Outcome.</summary>
public enum RequirementStatus
{
    Draft,
    Reviewed,
    Approved,
    Allocated,
    Verified,
    Satisfied,
    Obsolete
}
```

**Every remaining question is answered in full in `WP7.2C Requirement
Lifecycle Model.md`**, per this Work Package's own controlling
instruction naming the lifecycle as its own dedicated deliverable — this
section states only the proposed contract shape.

## 9. Requirement Category

**Purpose, Responsibilities, Public Interface.** `string?` — an open,
extensible, nullable classification, identical in shape to
`IMaterialSpecification.Category`. No new type, no closed enum.

**Every remaining question is answered identically to `IRequirement`
generally, §2, above** — Category is a field of `IRequirement`, not a
separate concept with its own lifetime, ownership, or dependency
profile.

## 10. Requirement Identifier

**Purpose.** A stable, human-facing business key, distinct from the
underlying `IEngineeringDocument.Id`.

**Responsibilities.** Maps a caller-chosen `string` to the underlying
document Guid — mirroring `IMaterialCatalog`'s own `materialId` index
precedent exactly (`ADR-0055` Decision 3).

**Public Interface.** No separate type — `IRequirement.Identifier`
(`string`) plus `IRequirementsService.FindByIdentifierAsync(string)`,
both already shown above.

**Lifetime, Ownership.** The identifier-to-Guid index is owned by
`Tempest.Core.Requirements`' own concrete implementation, requiring a
direct `IPersistenceStore` dependency for lookup-by-arbitrary-string —
`IEngineeringDocumentStore` itself has no such capability, identical to
`MaterialCatalog`'s own already-proven precedent.

**Consumers, Dependencies.** Any caller needing human-readable lookup
(a REST route parameter, a report filter, a search query).

**Thread-Safety Expectations.** The identifier index must reject a
duplicate identifier atomically — two concurrent `CreateAsync` calls
with the same identifier must not both succeed, mirroring
`MaterialCatalog.RegisterAsync`'s own existing `DuplicateMaterialException`
guarantee.

**Failure Behaviour, Exception Model.**
`DuplicateRequirementIdentifierException`, already shown above.

**Serialization Expectations.** A plain `string` — no serialization
concern beyond what `IRequirement`'s own DTO already covers.

**Extension Points.** No enforced format — a flat string, a
hierarchical dotted numbering, or a standard-mandated format are all
equally supported; `WP7.2C Required ADR Catalogue.md`'s own `ADR-0059`
reserves the exact representation decision.

**Platform/Engineering Core Consumption.** `Tempest.Core.Persistence`
(`IPersistenceStore`, direct, mirroring `MaterialCatalog`).

**Security Considerations.** None beyond `IRequirement`'s own general
profile.

**Testing Strategy.** Register/lookup round-trip by identifier;
duplicate-identifier rejection under concurrent registration attempts
(mirroring `MaterialCatalogTests.cs`'s own precedent). Full detail:
`WP7.2C Testing Strategy.md`.

**Academy Requirements.** Covered by the primary concept guide,
cross-referencing `WP7.1C-materials-framework-implementation.md`'s own
identical pattern.

## 11. Requirement Revision

**This is not a new contract — inherited directly from
`Tempest.Core.EngineeringData.IDocumentRevision`.** Every change to a
requirement's own statement produces a new revision through
`IRequirementsService.ReviseAsync`, itself a thin wrapper over
`IEngineeringDocumentStore.ReviseAsync`. No second revision model, no
Requirements-specific revision type, is proposed.

## Related Documents

`WP7.2B Requirements Platform Architecture.md`; `WP7.2B Requirements
Domain Model.md`; `WP7.2C Requirement Lifecycle Model.md`; `WP7.2C
Relationship Model.md`; `WP7.2C Traceability Contract.md`; `WP7.2C
Verification Integration Contract.md`; `WP7.2C Platform Integration
Matrix.md`; `WP7.2C Security Review.md`; `WP7.2C Testing Strategy.md`;
`WP7.2C Academy Plan.md`; `WP7.2C Required ADR Catalogue.md`.

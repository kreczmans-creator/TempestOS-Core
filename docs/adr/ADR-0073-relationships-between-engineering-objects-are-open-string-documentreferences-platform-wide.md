# ADR-0073: Relationships Between Engineering Objects Are Open-String `DocumentReference`s, Platform-Wide

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2A` (Engineering
Domain Architecture), 2026-08-04. Formalises, as binding platform-wide
architecture, the relationship model `Tempest.Core.Requirements` and
`Tempest.Core.Verification` each already independently adopted.

## Context

`WP 8.2A`'s own controlling instruction names twenty relationship
kinds — Parent, Child, Composition, Dependency, Allocation,
Verification, Derived From, Supersedes, Blocks, Manufactured By,
Verified By, and so on — that must connect fifty canonical object
families to each other. A combinatorial, per-pair-of-Kinds relationship
model (a distinct `PartToSupplierRelationship`, a distinct
`RequirementToRiskRelationship`, and so on) would require on the order
of hundreds of distinct relationship types across fifty object
families; no realistic closed enum can be designed in advance to cover
every future pairing, especially once module-defined custom objects
(`ADR-0072`'s own extensibility consequence) are considered.

This exact problem was already solved, twice, independently:
`RequirementRelationshipKinds` (six named `string` constants —
`groupedUnder`, `collects`, `dependsOn`, `derivesFrom`, `allocatedTo`,
`references`, `satisfies`) and `VerificationService`'s own three
relationship-kind constants (`verifiedBy`, `references`,
`basedOnCalculation`) are both realised as plain strings passed to the
identical `IEngineeringDocumentStore.LinkAsync(source, target,
relationshipKind)` method — no enum, no closed type, and, critically,
no validation that a `TargetDocumentId` is of any particular `Kind`
(`Engineering Principle 31`, "Kind-agnostic... never inspected").

## Decision

**Every relationship between Engineering Objects, of any Kind, is a
`DocumentReference(SourceDocumentId, TargetDocumentId,
RelationshipKind)`, where `RelationshipKind` is an open, unvalidated
string — never a closed enum, never a second relationship-storage
mechanism, and never validated against the target's own `Kind`.** This
extends `RequirementRelationshipKinds`'/`VerificationService`'s own
existing convention from "the way these two frameworks happen to do
it" to "the one way any relationship in this platform is expressed,"
binding on every canonical object family `WP8.2A Canonical Object
Catalogue.md` names and on every future module-defined object alike.
`WP8.2A Relationship Catalogue.md` is the resulting platform-wide
vocabulary — a shared, non-exhaustive convention, not a closed,
enforced registry.

## Consequences

**Positive:**

- Adding a new relationship kind is a documentation change (a new row
  in `Relationship Catalogue.md`, or a module's own locally-scoped
  string), never a platform code change — the identical "reuse what
  exists" conclusion `ADR-0067` already reached for Workspace
  extensibility, now reached for the Engineering Core's own
  relationship model.
- A relationship between two object families neither of which existed
  when the platform shipped (a future `Hazard` linking to a future
  `Supplier`, say) works today, with zero new code, the moment both
  Kinds exist — proven already by `RequirementRelationshipKinds.AllocatedTo`
  accepting any target Kind without modification since `WP 7.3A`.
- Directly satisfies this Work Package's own Definition of Done ("a new
  engineering team shall be capable of implementing the entire platform
  solely from this specification") — no relationship-type code
  generation or registry to build before any two canonical objects can
  be linked.

**Negative:**

- No structural guarantee that a `verifiedBy` link actually points at a
  Verification Result rather than, say, a Risk — mistakes are
  possible and only detectable by convention/review, not by the
  platform. This is the same trade-off `RequirementRelationshipKinds`
  already accepts today, at a smaller scale; this decision accepts it
  at the full fifty-object scale.
- `Relationship Catalogue.md`'s own vocabulary can drift or duplicate
  (two modules independently inventing `blockedBy` and `blocks` for the
  same concept) with no platform-level prevention — a disclosed,
  accepted cost of extensibility without central registration,
  mirroring `Kind`'s own identical, already-accepted risk (`WP8.2A
  Engineering Domain Architecture.md` §0).

## Alternatives Considered

**A closed `RelationshipKind` enum, extended by a Contract Review each
time a new kind is needed** — considered and rejected. This would
directly contradict `ADR-0072`'s own extensibility promise (a module
may mint a new canonical object without platform review) by requiring
platform review for every new relationship between such objects —
inconsistent scope discipline within the same architecture.

**Per-Kind-pair relationship validation** (the store checks that a
`verifiedBy` link's own target is actually `Kind = "VerificationRecord"`)
— considered and rejected; would require the `IEngineeringDocumentStore`
itself to gain Kind-awareness it deliberately does not have today
(`Engineering Principle 31`), and would need a closed Kind registry
this platform has explicitly never built.

## Related Documents

`ADR-0067`; `ADR-0072`; `WP8.2A Engineering Domain Architecture.md`;
`WP8.2A Relationship Catalogue.md`; `docs/engineering/Engineering
Principles.md` (Principle 31); `RequirementRelationshipKinds`
(`src/Tempest.Core/Requirements/`); `DocumentReference`/`LinkAsync`
(`src/Tempest.Core/EngineeringData/`).

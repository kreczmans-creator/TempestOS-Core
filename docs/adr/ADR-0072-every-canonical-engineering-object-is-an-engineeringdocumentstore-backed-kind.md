# ADR-0072: Every Canonical Engineering Object Is an `IEngineeringDocumentStore`-Backed `Kind`, Never a New Storage/Type Hierarchy

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2A` (Engineering
Domain Architecture), 2026-08-04. Formalises, as binding platform-wide
architecture, a pattern `Tempest.Core.Requirements`,
`Tempest.Core.Verification`, `Tempest.Core.Materials`, and
`Tempest.Core.Calculations` each already independently adopted.

## Context

`WP 8.2A`'s own controlling instruction names roughly fifty canonical
Engineering Objects — Project, Assembly, Part, Requirement, Risk,
Supplier, Milestone, and so on — and states "every current and future
module shall consume this model." Before deciding how these fifty
objects relate to each other, one question has to be answered first:
does each new object family get its own storage shape (its own table,
its own identity scheme, its own revision mechanism), or do they all
share one?

This question was already answered, independently, four times, without
platform-level coordination: `Requirement`, `IVerificationRecord`,
`IMaterialSpecification`, and `CalculationRecord<TResult>` are each,
underneath their own typed surface, an `IEngineeringDocument` — the
same `Guid Id`/`string Kind`/`int CurrentRevisionNumber` shape, backed
by the same `IEngineeringDocumentStore`, versioned through the same
`IDocumentRevision` mechanism. No framework built a second identity or
storage concept. `Engineering Foundation Contract Review` (`WP 7.0C`)
and its own four framework-specific contract reviews each independently
reached this conclusion; it has never been stated as a *platform*
architectural decision, binding on every future object, until now.

## Decision

**Every canonical Engineering Object — real or future — is realised as
a `Kind` string over the existing, single `IEngineeringDocumentStore`.
No canonical object gets its own storage mechanism, its own identity
scheme, or its own revision model.** A new object family is added to
the platform by declaring a new `Kind` value (and, where the canonical
lifecycle vocabulary needs specialising, a new closed transition table,
`ADR-0074`) — never by proposing a new persistence technology, a new
document store, or a parallel identity concept. This applies uniformly
whether the new object is a platform-reviewed canonical addition or a
module's own unreviewed custom extension (`WP8.2A Engineering Domain
Architecture.md` §7).

## Consequences

**Positive:**

- Roughly forty of the fifty named canonical objects have no
  implementation yet; this decision means every one of them, once
  built, automatically inherits identity stability (`Engineering
  Principle 1`), append-only revision history (`Engineering Principle
  2`/`4`), and the existing relationship mechanism — zero new
  persistence engineering required per object family.
- A new engineering team implementing this platform from the
  Engineering Domain Architecture alone (this Work Package's own
  Definition of Done) has exactly one storage concept to learn, not
  fifty.
- Directly extends `ADR-0053`'s own "Engineering Data Model reuse"
  decision (originally scoped to five Engineering Foundation
  frameworks) to the full canonical object set, rather than leaving
  each future framework to independently rediscover the same
  conclusion a fifth, sixth, and fiftieth time.

**Negative:**

- `IEngineeringDocument.Content` is opaque; any canonical object whose
  own content genuinely needs first-class, queryable structure (rather
  than an opaque blob interpreted only by its own owning framework)
  gains no help from this decision alone — exactly the same
  already-accepted trade-off every shipped framework already lives
  with (`Requirement.Statement` is opaque text; nothing queries inside
  it directly).
- A `Kind` value, once real, is binding — this decision makes it
  costlier to later decide a canonical object actually needs a
  genuinely different storage shape, since doing so would mean breaking
  this ADR's own platform-wide rule for one object family specifically.
  No such need has been demonstrated for any of the fifty named
  objects.

## Alternatives Considered

**A distinct storage/type hierarchy per object family** (an
`IPhysicalObject` base for Assembly/Part, an `IGovernanceObject` base
for Risk/Issue/Decision, and so on) — considered and rejected. This is
exactly the shape none of the four shipped frameworks chose, each
independently, and choosing it now for the remaining forty objects
would immediately fragment the platform into two incompatible
Engineering Core styles, contradicting the very principle ("everything
is an Engineering Object") this Work Package exists to state.

**Deferring this decision until each object family's own future
Contract Review** — considered and rejected. `WP 7.0C`'s own precedent
already proved the value of deciding this once, platform-wide, before
five separate Contract Reviews had to each independently re-litigate
it; the fifty-object canonical catalogue this Work Package produces
would be considerably weaker without a settled answer to "how is any of
this actually stored" underneath it.

## Related Documents

`ADR-0053`; `WP8.2A Engineering Domain Architecture.md`; `WP8.2A
Canonical Object Catalogue.md`; `docs/engineering/Engineering
Principles.md` (Principles 1, 2, 4); `IEngineeringDocumentStore`
(`src/Tempest.Core/EngineeringData/`).

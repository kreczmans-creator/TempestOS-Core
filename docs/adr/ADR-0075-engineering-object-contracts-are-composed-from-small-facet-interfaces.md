# ADR-0075: Engineering Object Contracts Are Composed From Small Facet Interfaces, Never One Monolithic Interface

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2B` (Engineering
Domain Contracts), 2026-08-04. Resolves the controlling instruction's
own "favour composition over inheritance" principle into a concrete,
checkable interface-design rule for the ~49 canonical Engineering
Object interfaces `WP 8.2A` named.

## Context

`WP 8.2A` named roughly fifty canonical Engineering Objects, every one
of which (`ADR-0072`) is backed by the same underlying
`IEngineeringDocumentStore` shape, and every one of which needs some
subset of about twenty common behaviours (identity, metadata,
lifecycle, revisions, relationships, traceability, validation,
attachments, searchability). Two shapes were available for the public
contract layer this Work Package defines: one large `IEngineeringObject`
interface carrying all twenty behaviours, which every one of the fifty
object interfaces would inherit from unconditionally; or a set of small,
single-purpose interfaces an object composes only as many of as it
actually needs.

A single large interface would force every object — including ones
with no natural attachments (`IAssumption`), no natural revisions
(`IApproval`, a point-in-time event, not a revised document), or no
natural relationships of their own (`IAttachment` itself, a leaf value)
— to implement members meaningless to it, or to throw
`NotSupportedException` from unused members, a shape this platform's
own `Engineering Principles` have never endorsed anywhere else.

## Decision

**Every canonical Engineering Object interface composes
`IEngineeringObject` (identity) plus whichever of ten small facet
interfaces — `IHasBusinessIdentifier`, `IHasMetadata`, `IHasLifecycle`,
`IHasRevisions`, `IHasRelationships`, `ITraceable`, `IValidatable`,
`IHasAttachments`, `ISearchable` — are actually relevant to it, via
ordinary C# multiple interface implementation. No canonical object
interface inherits from a monolithic interface carrying every
behaviour, and no canonical object interface inherits from more than
one *other* canonical object interface (`WP8.2B Dependency Rules.md`
§6 — at most one level of object-to-object specialisation, such as
`ISubAssembly : IAssembly`).** `WP8.2B Interface Catalogue.md` is the
resulting complete set, composed this way for every one of the ~49
named objects.

## Consequences

**Positive:**

- `IApproval` composes only `IEngineeringObject`/`IHasMetadata`/
  `IHasRelationships` — no `ReviseAsync`, `AttachAsync`, or
  `TransitionAsync` member it would never meaningfully support.
  Every object interface in the catalogue is honest about what it
  actually does, member by member.
- Adding a new canonical object (a module's own custom extension,
  `ADR-0072`'s own consequence) requires composing existing facets, not
  extending or overriding a shared base interface — no risk of a new
  object accidentally breaking an existing consumer of the shared base.
- A future implementing class can share one concrete facet
  implementation (a single `MetadataEnvelope` class realising
  `IHasMetadata`) across every object family that composes it, without
  that sharing being visible in, or constrained by, the public contract
  shape at all.

**Negative:**

- A consumer wanting to treat "any Engineering Object with a lifecycle"
  polymorphically must code against `IHasLifecycle` specifically, not
  `IEngineeringObject` — a small, disclosed ergonomic cost, accepted
  because the alternative (every object exposing a lifecycle whether or
  not it has one) is worse.
- Ten facet interfaces, composed in different combinations across
  forty-nine object interfaces, is more surface area to learn at once
  than one large interface would be — accepted because the ten facets
  are individually much smaller and map directly onto the twenty named
  "Common Behaviour" concerns (`WP8.2B Interface Catalogue.md` §1's own
  consolidation table), making the mapping easy to verify once, not
  fifty times.

## Alternatives Considered

**One monolithic `IEngineeringObject` carrying all twenty behaviours**
— considered and rejected for the reasons in Context, above; would also
have made `ADR-0076`'s own generic-relationship decision harder to
express cleanly, since `IHasRelationships` would no longer be a
separable concern.

**A class-inheritance-based facet mechanism** (an abstract
`EngineeringObjectBase` class implementing every facet, extended per
family) — considered and rejected; would directly contradict
`ADR-0072`'s own "never a new storage/type hierarchy" decision, and
would prevent a future implementation from composing, for instance, a
shared revision-handling base class alongside a shared metadata-handling
base class independently (C# single class inheritance would force a
choice between them).

## Related Documents

`ADR-0072`; `WP8.2A Engineering Domain Architecture.md` §3; `WP8.2B
Engineering Domain Contracts.md` §1, §3; `WP8.2B Interface
Catalogue.md`; `WP8.2B Dependency Rules.md` §6.

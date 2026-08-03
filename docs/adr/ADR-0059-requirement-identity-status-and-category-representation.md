# ADR-0059: Requirement Identity, Status, and Category Representation

## Status

Accepted — `WP 7.3A` (Requirements Engine), 2026-07-30.

## Context

`WP7.2C Required ADR Catalogue.md` reserved three representation
questions: whether `RequirementStatus` is a closed enum or an open
string; the exact shape of the business-identifier index; and whether
`Requirement Category` should remain a fully open string.
`WP7.2B Requirements Domain Model.md` explicitly recommended deciding each
independently rather than assuming uniformity is itself a virtue — this
Work Package confirms that recommendation and resolves all three.

## Decision

**1. `RequirementStatus` is a closed `enum`** (`Draft`, `Reviewed`,
`Approved`, `Allocated`, `Verified`, `Satisfied`, `Obsolete`) —
confirmed, mirroring `VerificationOutcome`'s own identical precedent.
Status transitions are enforced against a fixed table
(`RequirementStatusTransitions`, `WP7.2C Requirement Lifecycle Model.md`),
a real structural workflow this platform can usefully constrain, unlike
an open-ended classification.

**2. `Requirement Identifier` is a `string` business key, resolved
through a dedicated `IPersistenceStore` index** (`Requirements.Index`),
mirroring `MaterialCatalog`'s own `materialId` index exactly (`ADR-0055`
Decision 3). No format is enforced — a flat string, a hierarchical
dotted numbering, or a standard-mandated format are all equally
supported. `RequirementsService.CreateAsync`'s own per-identifier
`AsyncKeyedLock` guarantees atomic duplicate rejection, mirroring
`MaterialCatalog.RegisterAsync`'s own identical concurrency guarantee.

**3. `Requirement Category` remains a fully open, nullable `string`** —
confirmed, mirroring `IMaterialSpecification.Category`'s own identical
precedent. No closed taxonomy is introduced; inventing one now, absent a
real discipline module's own real category need, would repeat the
"cannot be sequenced from existing evidence" anti-pattern
`WP7.0B Engineering Discipline Assessment.md` already warned against,
one level down (inventing a taxonomy instead of a capability).

## Consequences

**Positive:**

- Each of the three representations was chosen independently, on its
  own merits, rather than forced into artificial uniformity — `Status`
  benefits from a closed set's own structural guarantee; `Category`
  benefits from an open string's own extensibility; `Identifier`
  benefits from a dedicated index exactly where `IEngineeringDocumentStore`
  itself has a genuine capability gap.
- Reusing `MaterialCatalog`'s own proven index pattern for `Identifier`
  means this decision required no new design — only a direct
  application of an already-validated one.

**Negative:**

- `RequirementStatus` being closed means any future lifecycle state this
  platform did not anticipate requires a breaking enum change, not an
  additive one — disclosed, not treated as unlikely, but judged an
  acceptable cost given the lifecycle model's own real, bounded
  structure (`WP7.2C Requirement Lifecycle Model.md`).

## Alternatives Considered

**A uniform representation style for all three** (all closed enums, or
all open strings) — considered, for internal consistency's own sake,
and rejected. Materials and Verification already demonstrate this
platform's own convention is "choose the representation the specific
concept's own maturity warrants," not "represent every classification-
like field identically" — this decision continues that convention.

**An open `string` for `RequirementStatus`**, mirroring `Category` —
considered and rejected. Unlike category (no real taxonomy exists yet to
close), the requirement lifecycle names a genuine, bounded workflow
structure (`WP7.2C Requirement Lifecycle Model.md`'s own seven states
and their transitions) that benefits from compiler-enforced closure,
the same reasoning `VerificationOutcome`'s own three-value closed enum
already established.

## Related Documents

`ADR-0055` (Materials' own identifier-index and open-category
precedent); `ADR-0057` (Verification's own closed-enum-outcome
precedent); `WP7.2B Requirements Domain Model.md` §9, §11, §12;
`WP7.2C Requirement Lifecycle Model.md`; `WP7.2C Required ADR
Catalogue.md`; `docs/releases/v0.7.0/WP7.3A Implementation Report.md`.

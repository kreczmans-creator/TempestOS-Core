# Engineering Principles

## Status

Established by `WP 7.1A` (Engineering Data Model) — the first Work
Package to implement a real Engineering Foundation framework rather than
plan or design one. This document is new: `docs/engineering/` did not
exist before this Work Package. Where `docs/academy/06 Engineering
Standards/Engineering Governance.md` governs how TempestOS is built,
and `VISION.md` states what TempestOS is for, this document states the
principles the engineering-domain content itself — not the platform
that hosts it — must uphold, derived from what `Tempest.Core.
EngineeringData` actually implements, not asserted in advance of it.

## Purpose

Every future Engineering Foundation framework (`FCR-0030`–`FCR-0033`)
and every future Engineering Module builds on the Engineering Data
Model. This document exists so each of them inherits a consistent set of
principles about what "engineering information" means on this platform,
rather than each independently re-deriving the same ground rules — the
Academy/governance equivalent of `Future Work Package Guidelines.md`,
scoped specifically to engineering-domain content.

**Only principles the implemented architecture actually demonstrates
are listed below.** A principle that sounded reasonable in the abstract
but that `EngineeringDocumentStore`'s own real implementation does not
enforce is not included — this document is derived from working code,
not from aspiration.

## The Principles

### 1. Engineering entities have stable identities

An `IEngineeringDocument`'s own `Id` is assigned once, at creation
(`CreateAsync`), and never changes for the rest of that document's
existence — every subsequent operation (`ReviseAsync`, `LinkAsync`,
`GetRevisionHistoryAsync`) addresses the document by that same, permanent
Id. A document's `Kind` likewise never changes after creation. Only
`CurrentRevisionNumber` advances. This is enforced structurally, not by
convention: no method on `IEngineeringDocumentStore` accepts a new Id
for an existing document, and `EngineeringDocumentStore`'s own
implementation never rewrites a document's identity record's `Kind`.

### 2. Revision history is explicit

Every change to a document's content produces a new, separately
retrievable `IDocumentRevision` — there is no "update in place" operation
anywhere in the approved contract or its implementation.
`GetRevisionHistoryAsync` returns every revision a document has ever
had, oldest first, not merely the current one. This was proven, not
merely designed: `EngineeringDocumentStoreTests.
GetRevisionHistoryAsync_ReturnsEveryRevision_OldestFirst` creates three
revisions and confirms all three, in order, are independently readable.

### 3. Engineering data is independent of calculations

`Tempest.Core.EngineeringData` contains no calculation logic, no
formula, and no numeric computation of any kind — `Content` is an
opaque `string`, uninterpreted by this namespace. This Work Package's
own controlling instruction required this separation explicitly ("shall
not implement engineering calculations"), and `WP7.0C Cross-Framework
Dependency Report.md` already confirmed, at contract level, that the
Engineering Calculation Framework (`FCR-0032`) depends on Units &
Quantities, never on the Data Model directly for its own core dispatch
mechanism — this Work Package's implementation introduces nothing that
would create such a dependency.

### 4. Engineering entities are immutable where practical

An `IDocumentRevision`, once written, is never modified or deleted —
confirmed directly by `EngineeringDocumentStore`'s own implementation,
which only ever writes a new revision key, never overwrites an existing
one. An `IEngineeringDocument`'s own identity record is the one
necessary exception (`CurrentRevisionNumber` must advance for the
document to have a "current" revision at all), and this exception is
itself narrow and structural, not a general licence to mutate — no
other field of a document's own identity record ever changes.

### 5. Engineering information is reproducible

Reading the same document Id and revision number always returns the
same, unchanged content — a direct consequence of Principle 2
(revisions are never modified) combined with Principle 4 (revisions are
immutable once written). This is a narrower claim than "a calculation
is reproducible" (which `Tempest.Core.EngineeringData` does not attempt,
per Principle 3) — it is specifically about the data layer: the record
of what engineering information existed at a point in time does not
drift or decay on repeated reads.

### 6. Engineering correctness takes precedence over convenience

`ReviseAsync`'s own atomicity guarantee (no two concurrent revisions of
the same document can ever claim the same revision number,
`EngineeringDocumentStoreTests.
ReviseAsync_CalledConcurrently_NeverProducesTwoRevisionsWithTheSameNumber`)
was implemented via a per-document lock, at the cost of serialising
concurrent revisions to the same document — a real, deliberate
performance trade-off, accepted because a document with an ambiguous or
colliding revision number would be a worse outcome than a slower write
path. Likewise, requiring a full new revision for any content change
(Principle 2), rather than offering a cheaper "patch" operation, is a
deliberate correctness-over-convenience choice, not an oversight.

## What This Document Does Not Cover

- **Units, calculations, materials, or verification** — each future
  Engineering Foundation framework (`FCR-0030`–`FCR-0033`) will assess
  which of these principles apply to it directly and which it extends
  with its own, once each is implemented; this document is not amended
  in advance of that work.
- **Discipline-specific engineering principles** (a structural
  engineering design principle, an electrical safety margin
  principle) — deliberately out of scope, per this Work Package's own
  controlling instruction not to introduce Mechanical, HVAC, Structural,
  or Electrical concepts.

## Related Documents

`docs/academy/06 Engineering Standards/Engineering Governance.md`;
`VISION.md`; `docs/releases/FOUNDATION.md`; `docs/governance/Future
Capability Register.md`; `ADR-0053`; `docs/academy/03 Work Packages/
WP7.1A-engineering-data-model-implementation.md`.

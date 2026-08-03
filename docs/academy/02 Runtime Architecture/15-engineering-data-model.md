# The Engineering Data Model

## 1. Introduction

`IEngineeringDocumentStore` (`Tempest.Core.EngineeringData`, `WP 7.1A`)
is this platform's first data-modelling abstraction beyond flat
key-value storage — every prior Platform Service (Settings, Audit,
Licensing) reads and writes `IPersistenceStore` directly, one key at a
time, with no concept of a document's own history or its relationships
to other documents. This guide explains the document/revision/reference
pattern the Engineering Data Model introduces, why it is a distinct
layer above `IPersistenceStore` rather than a replacement for it, and
why every other Engineering Foundation framework (Materials,
Calculation, Verification) builds on it rather than inventing its own
storage shape.

**Note on timing.** This guide should have accompanied `WP 7.1A`'s own
implementation — `WP7.0C Academy Plan.md` named it as "the
highest-priority new Academy content this entire programme produces,"
and it was not written until `WP 7.1F`'s own closing certification
review found the gap. It is written now from the same, real, shipped
code four further Work Packages (`WP 7.1B`–`WP 7.1E`) have since built
on, not from the original contract-review proposal — a stronger
foundation for a concept guide than would have been available at
`WP 7.1A`'s own completion.

## 2. Purpose

To explain what an `IEngineeringDocument` is, why it is not simply "a
persistence key with extra steps," and how its three capabilities
(stable identity, explicit revision history, typed references to other
documents) together give every future Engineering Foundation framework
and Engineering Module a shared way to represent engineering
information without each inventing its own.

## 3. Background — Why Not Just `IPersistenceStore`

`IPersistenceStore` (`WP 6.4`, `ADR-0041`) is a key-value store: a key
maps to one current value, overwritten on every write, with no native
concept of "what this value used to be" or "what this value relates
to." That shape is correct for Settings (a setting has one current
value) and sufficient for Audit's own append-only entries (each entry is
independent, never revised). Engineering information is different in
kind: a material specification, a requirement, or a calculation's own
record is expected to change over time while remaining traceable back
through every prior version, and is expected to reference other
engineering entities (a material referenced by a calculation; a
document verified by a verification record) in a way a bare string key
cannot express.

## 4. The Problem

1. **How does an engineering entity keep one stable identity across
   changes to its own content**, so a caller holding a `Guid` from six
   months ago can still find the same logical document today?
2. **How is a change to that content recorded** — overwritten silently,
   the way `IPersistenceStore.WriteAsync` behaves, or preserved as an
   independently retrievable fact about what the document used to say?
3. **How does one document reference another** — a calculation
   referencing the material it assumed, a verification referencing the
   requirement it demonstrates — without every consuming framework
   inventing its own ad hoc "this Id points at that Id" convention?

## 5. The Design

`IEngineeringDocumentStore.CreateAsync` assigns a `Guid` once, at
creation, and no method anywhere in the contract accepts a new Id for an
existing document — identity is permanent (Principle 1,
`docs/engineering/Engineering Principles.md`). `ReviseAsync` never
overwrites; it appends a new `IDocumentRevision`, and
`GetRevisionHistoryAsync` returns every revision a document has ever
had, oldest first (Principle 2) — there is no "update in place"
operation in the approved contract at all. `LinkAsync` records a typed,
directed relationship between two existing documents (a
`relationshipKind` string plus a target Id); `GetReferencesAsync`
answers "what does this document point to." Built directly on
`IPersistenceStore` (`ADR-0053`) — documents, revisions, and references
are each serialized into ordinary `IPersistenceStore` keys, introducing
no second storage mechanism alongside the one Settings and Audit already
use.

## 6. Alternatives Considered

**Extending `IPersistenceStore` itself with a native revision/reference
concept**, rather than building a separate layer above it — considered
and rejected in `ADR-0053`. `IPersistenceStore`'s own contract
(`ADR-0041`) is deliberately a plain key-value shape; every existing
consumer (Settings, Audit) relies on that simplicity, and neither needs
revisioning or typed references. Adding both directly to
`IPersistenceStore` would have forced every existing consumer to reason
about a richer contract it never asked for.

**A dedicated, purpose-built storage substrate for revisioned, linked
documents**, independent of `IPersistenceStore` entirely — considered
and rejected in the same decision. This would have introduced a second
storage mechanism this platform would then need to operate, back up, and
reason about failure modes for, with no concrete requirement (a real
scale or query-shape problem) yet demonstrating `IPersistenceStore`
itself is insufficient.

## 7. Why This Solution Was Chosen

It gives every Engineering Foundation framework a shared, proven
identity/revision/reference model without introducing a second storage
mechanism alongside `IPersistenceStore` — the same "one shared
abstraction, not reinvented per consumer" precedent `ADR-0041` itself
established for Settings and Audit, now applied one level up, to
engineering-domain data rather than platform configuration and action
records.

## 8. Architectural Principles

- **Single Responsibility Principle** — `IEngineeringDocumentStore`
  owns identity, revisioning, and typed references; it does not
  interpret a document's own content (`Content` is an opaque `string`)
  and does not perform any calculation (Principle 3).
- **Immutability Where Practical** — a written revision is never
  modified or deleted (Principle 4); only a document's own
  `CurrentRevisionNumber` pointer advances.
- **Reproducibility** — reading the same document Id and revision
  number always returns the same content (Principle 5), a direct
  consequence of the two principles above.

## 9. Benefits

- Every future Engineering Foundation framework or Engineering Module
  gets stable identity, full revision history, and typed
  cross-references for free, without designing or testing any of the
  three itself — proven directly: Materials, Calculation, and
  Verification each build on `IEngineeringDocumentStore` rather than
  inventing their own storage shape, exactly as this framework's own
  design intended.
- No second storage mechanism exists alongside `IPersistenceStore` —
  one substrate, one set of failure modes and operational
  characteristics to reason about across every Platform Service and
  every Engineering Foundation framework alike.
- A revision's own content never drifts on repeated reads, a narrower
  and more easily verified guarantee than "a calculation is
  reproducible" (which this framework deliberately does not attempt —
  Principle 3).

## 10. Trade-offs

- `IDocumentRevision.Content` is a plain, opaque `string` — no
  structured or typed payload support (`TD-17`); every consumer defines
  and enforces its own content schema outside this framework's own
  contract.
- `LinkAsync`'s own concurrency behaviour under many simultaneous calls
  against the *same* source document is not tested at the same depth as
  `ReviseAsync`'s own concurrent-revision regression test (`TD-18`) —
  believed safe (each call writes an independent, randomly-keyed
  entry), but not proven under real concurrent load the way
  `ReviseAsync`'s own atomicity has been.

## 11. Common Mistakes

The mistake most worth naming: treating `IEngineeringDocumentStore` as
a general-purpose document database and reaching for it to store
anything with more than one field. It is scoped specifically to what
Engineering Foundation and Engineering Module consumers need — stable
identity, explicit revision history, typed references between
documents. A Platform Service need with no revisioning or
cross-referencing requirement (an ordinary setting, a notification)
belongs on `IPersistenceStore` directly, exactly as Settings and Audit
already do; routing it through the Engineering Data Model instead would
add revisioning/reference machinery no such consumer needs.

A second mistake: assuming `ReviseAsync` supports a cheap, partial
"patch" of a document's own content. It does not — every content change
requires a full new revision (Principle 2), a deliberate
correctness-over-convenience choice (Principle 6), not an oversight to
work around with a manual read-modify-write cycle against the current
revision.

## 12. Future Evolution

- **Structured content support** (`TD-17`), once a real, demonstrated
  consumer needs framework-enforced content structure rather than an
  opaque string each consumer schemas for itself.
- **Transactional multi-document operations** (`FCR-0036`, `TD-23`),
  once a second, independent consumer beyond Verification demonstrates
  a genuine need for atomic multi-document writes (create, then link,
  then link again, as one unit).

## 13. Key Takeaways

1. A shared identity/revision/reference model, built as a layer above
   an existing key-value store rather than a replacement for it, lets
   every future consumer skip designing and testing its own version of
   the same three capabilities.
2. Immutability and explicit revision history are the same discipline —
   a document's own reproducibility guarantee falls directly out of
   never overwriting a written revision, not from a separate mechanism.
3. Scoping a data model narrowly (engineering documents specifically,
   not "anything with more than one field") keeps it from becoming an
   unintended second general-purpose store standing alongside
   `IPersistenceStore`.

## Related Documents

`ADR-0053`; `docs/engineering/Engineering Principles.md` (Principles
1-6); `docs/academy/03 Work Packages/
WP7.1A-engineering-data-model-implementation.md`;
`13-calculation-framework.md`, `14-verification-framework.md` (both
built directly on the pattern this guide explains); `docs/releases/
v0.7.0/WP7.1F Executive Summary.md` (discloses why this guide was
written four Work Packages later than planned).

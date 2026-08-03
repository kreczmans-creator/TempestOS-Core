# ADR-0053: The Engineering Data Model Is Built Directly on the Existing Persistence Abstraction — No New Storage Mechanism

## Status

Accepted — `WP 7.1A` (Engineering Data Model), 2026-07-30.

## Context

`WP7.0C Required ADR Catalogue.md` reserved this decision explicitly:
whether `IEngineeringDocumentStore` (`Tempest.Core.EngineeringData`,
`FCR-0029`) should be built directly on `IPersistenceStore` (`WP 6.4`),
serializing revision/reference structure into that store's own
key-value shape, or whether a new, dedicated storage abstraction
purpose-built for revisioned, linked documents should be introduced
instead. `IPersistenceStore`'s own shape has no native concept of a
revision sequence or a typed reference — the question was whether that
gap is significant enough to warrant new infrastructure, or whether it
can be closed entirely at the `Tempest.Core.EngineeringData` layer,
above `IPersistenceStore`, without `IPersistenceStore` itself needing to
change.

## Decision

**`Tempest.Core.EngineeringData.EngineeringDocumentStore` is built
directly on `IPersistenceStore`. No new storage abstraction is
introduced.** Three `IPersistenceStore` collections are used, each
owned exclusively by this store, mirroring `ISettingsProvider`'s and
`IAuditRecorder`'s own collection-ownership convention (`ADR-0041`):

- `EngineeringData.Documents` — one entry per document, keyed by the
  document's own Id, holding its identity record (kind, created-at,
  current revision number).
- `EngineeringData.Revisions` — one entry per revision, keyed by
  `"{documentId:N}_{revisionNumber:D10}"`. Because revision numbers are
  sequential and already known from the document's own current-revision
  pointer, `GetRevisionHistoryAsync` reads exactly the keys it needs
  directly, one per revision — it never enumerates the whole
  collection, unlike `IAuditQuery`'s own disclosed linear-scan
  limitation (`TD-12`).
- A reference collection per source document
  (`EngineeringData.References.{sourceDocumentId:N}`), so
  `GetReferencesAsync` enumerates only that one document's own outgoing
  references, never the whole reference set across every document.

Revision-number atomicity under concurrent `ReviseAsync` calls against
the same document is guaranteed by a per-document `AsyncKeyedLock`,
mirroring `SettingsProvider`'s own per-key locking rationale — the
read-current-then-write-next sequence is serialised per document,
never across two different documents.

## Consequences

**Positive:**

- No new storage infrastructure was introduced — `IPersistenceStore`'s
  own file-backed implementation, thread-safety guarantees, and failure
  model (`PersistenceStoreUnavailableException`) are all reused as-is.
- The revision-history read path is *more* efficient than Audit's own
  query pattern, not merely equally limited by it — a genuine
  improvement discovered during implementation, not assumed at contract
  time. `WP7.0C Engineering Foundation Contracts.md`'s own proposed
  design did not specify this optimisation; it was found once real key
  design was worked through.
- The reference-lookup design (one collection per source document)
  avoids inheriting `FCR-0007`'s disclosed query-capability gap for
  this specific access pattern, even though `IPersistenceStore` itself
  still has no native query capability generally.

**Negative:**

- Three `IPersistenceStore` collections are now used per document
  conceptually (documents, revisions, and one references collection per
  document that has ever called `LinkAsync`) — a larger number of
  distinct collections than any prior consumer of `IPersistenceStore`
  has used, though each remains a simple, narrow, single-purpose
  collection, not a design complexity in its own right.
- `IEngineeringDocumentStore.Properties`-style structured content
  remains string-only (`Content` is `string`, exactly as approved) —
  this ADR does not revisit that decision, which remains
  `WP7.0C Engineering Foundation Contracts.md`'s own disclosed,
  unchanged Extension Point.

## Alternatives Considered

**A new, dedicated storage abstraction purpose-built for revisioned,
linked documents** — considered and rejected. Building a second
storage primitive alongside `IPersistenceStore` would duplicate
`PersistenceStore`'s own file-backed implementation, concurrency
handling, and failure model for no functional gain: every capability
`IEngineeringDocumentStore` needs (durable key-value storage,
per-key concurrency safety, a loud failure mode) already exists.
Introducing a second primitive would repeat exactly the
avoidable-architectural-debt pattern `ADR-0041` already resolved once
for Settings and Audit.

**Coupling `IEngineeringDocumentStore`'s revision history directly to
`FCR-0007`'s own unscheduled query-capability extension** — considered
and rejected, exactly as `WP7.0C Required ADR Catalogue.md` itself
anticipated. The sequential-key-read design above closes the specific
gap this framework needed without waiting for, or depending on, a
general-purpose query extension to `IPersistenceStore` that has no
scheduled owner.

## Related Documents

`ADR-0041` (the shared-Persistence-abstraction precedent this decision
extends); `docs/releases/v0.7.0/WP7.0C Engineering Foundation
Contracts.md`; `docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md`;
`docs/governance/Quality/Technical Debt Register.md` (`TD-12`, `FCR-0007`).

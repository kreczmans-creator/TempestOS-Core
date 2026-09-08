# ADR-0145: One Store Is Authoritative; Object State, Revisions and Relationships Commit in One Transaction; In-Memory Repositories Are Caches

## Status

Accepted — `WP 17.1B` (Transactional engineering object store), 2026-09-08.

Supersedes `ADR-0113` in part (its four-writer decomposition of an
engineering object's durable state; the state record's own shape and its
migration rules stand unchanged) and `ADR-0116` in part (its write
ordering — memory first, then disk, with a compensating undo; everything
else `ADR-0116` decides about the boundary and the acting principal
stands).

Built on `ADR-0144`, which gave the platform a store with a real
transaction. This decision is what that transaction was for.

## Context

An engineering object's truth lived in four places at once.

1. The instance's own fields — `_displayName`, `_status`, `_history`,
   `_parentId`, `_attachments`, `_bomLine`, and each Kind's own.
2. The `EngineeringObjectState` record in
   `EngineeringObjectStateStore`.
3. The document, revision and reference records in
   `EngineeringDocumentStore`.
4. The `InMemoryEngineeringRelationshipRepository`, plus attachment bytes
   in `AttachmentContentStore`, which is arguably a fifth.

No transaction spanned them, because until `ADR-0144` the platform had no
transaction to span them with. Every mutator therefore wrote memory
first, then disk, and the platform grew an apparatus whose entire purpose
was to make the resulting disagreement smaller rather than impossible:

- `MutationRollbackPoint`, `CaptureRollbackPoint` and `RollBackTo`, to
  snapshot and restore the instance's fields;
- `RollBackOnFailureAsync`, to run that restore when the durable write
  threw;
- `DurableRecordAlreadyShowsThisStateAsync` and its seven-term
  `HoldsTheSameMutableState` comparison, to decide whether the write had
  in fact landed before it threw — because undoing a write that
  *had* landed creates the divergence it is trying to prevent;
- `AttachmentWriteIntentStore` and `IAttachmentWriteIntentStore`, a
  durable marker set before an attachment's bytes were written and
  cleared after the state naming them was;
- `AttachmentContentReconciliationService`, a sweep to find and collect
  the bytes that marker could not account for.

That is roughly four hundred lines of compensation, plus an unusually
large body of tests establishing precisely when a write has committed,
all of it correct, none of it removing the defect. The register carried
the residues it could not reach as disclosed facts: `TD-135`, `TD-136`,
`TD-140`–`TD-150`. Two are worth naming because they are the ones a user
could actually be harmed by.

- **`TD-143`.** A `MoveAsync` whose link write succeeded and whose state
  write failed left the `groupedUnder` edge behind for ever, because
  nothing in this platform removes a relationship. The caller was told
  the move had failed.
- **`TD-147`.** `EngineeringObjectFactory<T>.CreateAsync` registered the
  instance in the repository and only then wrote its initial state. When
  that write failed the caller received an exception and never received
  the object — but the object was in the repository, findable, live and
  mutable, and its next successful write of anything at all made a
  creation reported as failed into a durable one. It was Release
  Blocking.

Every one of these is the same defect wearing a different hat: **there is
more than one writer, and no boundary around them.**

## Decision

**One durable store is authoritative, and every change to an engineering
object is one transaction on it.**

### The write path

`EngineeringDomainContext.ExecuteWriteAsync` is the only way a durable
engineering change happens. It takes the domain write lock, opens one
transaction on the `IQueryablePersistenceStore` the context was built
with, and hands the body an `IPersistenceTransaction`. Everything a single
logical change touches is written through that one handle:

- the `EngineeringObjectState` record;
- the document record, its revision records and its outgoing reference
  records;
- attachment bytes, as a SQLite BLOB in the same database;
- one audit row.

`EngineeringDocumentStore`, `EngineeringObjectStateStore` and
`AttachmentContentStore` each grew a transactional write half —
`ITransactionalDocumentWriter`, `ITransactionalStateWriter`,
`ITransactionalAttachmentWriter`, all internal — expressed against that
handle. Their public methods remain, and remain the only way anything
outside the assembly *reads* a document, a state record or a payload.

Every mutator on `EngineeringObjectBase` — `TransitionAsync`,
`RenameAsync`, `ReviseAsync`, `LinkAsync`, `AttachAsync`,
`AttachContentAsync`, `MoveAsync`, `DeleteAsync`, `SetBomLineAsync` — and
every Kind-specific mutator, through `MutateTypeStateAndPersistAsync`,
runs inside one such transaction, and so does
`EngineeringObjectFactory<T>.CreateAsync`.

### Project, commit, apply

The shape of every mutator is now three steps in this order, and the
order is the decision:

1. **Project.** Compute the next state from the current state, inside the
   transaction. This step may throw to refuse — an impermissible
   lifecycle transition, a parent cycle, a delete with live children, a
   superseded instance. Nothing has been touched at that point, so a
   refusal simply is not an operation.
2. **Commit.** Write the projected state, everything that goes with it,
   and the audit row, through the transaction.
3. **Apply.** After the commit returns and **before the write lock is
   released**, write the change into the instance's own fields and into
   the in-memory repositories.

**Memory is never mutated before the commit.** That single ordering rule
is what makes a failed write leave nothing behind — in memory or on disk
— with no undo step at all.

**Memory is also never mutated after the lock is released**, and that
second half is not decoration. A mutator projects its next state from the
object's own in-memory fields. If apply happened after the lock were
released there would be a window in which a second writer had taken the
lock and projected from a field the first writer had committed but not
yet applied; the second writer would commit a state derived from a value
the store had already replaced, and the first writer's change would be
durably lost. That is precisely the lost update `WP 16.4B-R3` closed with
its per-object lock, and it would return the moment "apply after commit"
stopped also meaning "apply before releasing". Commit and apply are one
critical section: `ExecuteWriteAsync` takes an `afterCommit` callback and
runs it inside the lock hold, and no mutator applies its own change after
awaiting it.

The same rule is what makes supersession an ordering point rather than a
hint. `ReviseAsync` retires the predecessor and registers the successor
in that callback, so a mutator taking the lock next sees the retirement
and is refused, and one that took it first has already committed.

### Graph invariants are checked inside the transaction

`GuardAgainstCircularParentAsync`, the live-children check for
`DeleteAsync`, and the supersession and expected-revision checks all run
inside the transaction, reading through the transaction handle, so they
are checked against the state that will actually be committed. A check
that passes cannot be invalidated before the write it guards, because
there is no gap between them.

### The lock is domain-wide, and that is deliberate

`EngineeringDomainContext` holds **one** `SemaphoreSlim`, taken for the
whole of `ExecuteWriteAsync`. Not per project, not per object.

**TempestOS is a single-user desktop system of record.** One person, one
process, one database file, whose writes are user-initiated actions
seconds apart. A finer-grained lock would buy concurrency nobody is
waiting for, and would cost a lock-ordering discipline that the graph
checks make genuinely hard: the cycle check and the live-children check
each read an unbounded slice of the object graph, and would each have to
acquire an unbounded, order-sensitive set of locks to be correct. One
lock is the honest shape for the product this is, and it is recorded here
as a decision rather than left to look like an oversight.

The lock is taken outside the transaction, never inside it. SQLite's
`BEGIN IMMEDIATE` already holds the database's single write lock, so a
second writer waiting on the domain lock while a transaction holds the
database lock is one queue rather than two, and cannot invert.

### Repositories are caches

`InMemoryEngineeringObjectRepository` and
`InMemoryEngineeringRelationshipRepository` are populated from committed
state only and are never co-equal writers. `EngineeringObjectFactory<T>`
calls `Register` after the transaction commits, which is `TD-147` closed
at its cause: nothing before that line puts the instance anywhere a
second caller could find it.

`EngineeringObjectRehydrationService` reads every state record in one
`ReadAllAsync` rather than one key at a time. It remains **eager**: a
lazy, per-kind warm-up is deferred, because a lazy cache that can be
populated by two paths is a second consistency problem and this Work
Package exists to remove one. `WP 17.1B`'s own row anticipated the lazy
warm-up; it is not built here, and the deferral is stated rather than
silently dropped.

### Binary payloads are BLOBs in the same transaction

Attachment content is written through the same transaction as the state
record that references it. A committed attachment reference therefore
cannot name bytes that are not there, and a transaction that does not
commit leaves no bytes behind. Payloads above 256 MB are refused with a
clear message (`AttachmentContentTooLargeException`); a calc-sheet PDF is
under 5 MB and a scanned drawing under 50 MB. Content is still verified
by SHA-256 on read, exactly as before.

### Audit rows are written in the transaction

One audit row per committed mutation, into the existing `Audit`
collection, keyed `<objectId:N>_<utcTicks:D19>_<guid:N>`. The object id
leads so that "what happened to this object" is one prefix listing rather
than a scan of every audit record ever written (`TD-12`); the timestamp
is second so a prefix listing returns in chronological order without a
sort; the GUID is third so two rows in the same tick are still distinct
keys. `IAuditQuery`/`AuditQuery` were adapted to prefix listing and
`ReadAllAsync`. `IAuditRecorder` and the record DTO are unchanged, so
callers outside the engineering domain are unaffected and a reader cannot
tell which writer produced a row.

## Consequences

### Deleted

The compensation is removed, not kept alongside the fix:

| Deleted | Where |
|---|---|
| `RollBackOnFailureAsync`, `MutationRollbackPoint`, `CaptureRollbackPoint`, `RollBackTo`, `DurableRecordAlreadyShowsThisStateAsync`, `HoldsTheSameMutableState` | `EngineeringObjectBase` |
| `AttachmentWriteIntentStore` | `EngineeringDomain/Implementation` |
| `IAttachmentWriteIntentStore` | `EngineeringDomain/Contracts` |
| `AttachmentContentReconciliationService` | `EngineeringDomain/Implementation` |
| `IAttachmentContentReconciliationService` and its report types | `EngineeringDomain/Contracts` |
| The DI registrations for the write-intent store and the attachment sweep | `TempestHost` |
| `InMemoryEngineeringDocumentStore` | `EngineeringDomain/Implementation` |

`EngineeringObjectBase` falls from 1,819 lines to under 1,000 without any
change to the public facet interfaces (`ADR-0075`).

`InMemoryEngineeringDocumentStore` goes because it was a second
implementation of the document store, kept only so tests could avoid a
filesystem. Keeping it would have meant teaching a test double to take
part in the transaction — a fourth writer, reintroduced in the test
suite, to test a design whose point is that there are no longer four
writers. Tests now build the real stores over an in-memory
`IQueryablePersistenceStore` instead, so there is one production code
path and faults are injected at the store, where real faults occur.

### Closed

`TD-135`, `TD-136`, `TD-140`–`TD-150`, and `TD-147` in particular, which
was Release Blocking. `TD-142` and `TD-143`, whose disclosed residues no
longer exist, had characterisation facts carrying standing instructions
to *invert rather than delete* them when closed; those facts are inverted
in `R7RegressionProofTests` and `R7FalsificationTests`.

### Costs, stated plainly

- **All writes serialise on one lock.** Accepted, for the reason above.
  If TempestOS ever becomes multi-user or multi-process, this is the
  first decision to revisit, and it will be revisited as a whole rather
  than sharded incrementally.
- **A whole mutation's writes are held in memory until commit**,
  including up to 256 MB of attachment bytes. The limit exists because of
  this.
- **`WP 17.1B` is built on the SQLite backend alone.** The file-per-key
  store's `ExecuteInTransactionAsync` is a bare sequence of writes with
  no mechanism capable of being atomic, and `ADR-0144` retires it in
  `v0.18.0`. Running the engineering domain on `Persistence:Backend=files`
  gives up the guarantee this ADR makes, and the store's own
  implementation says so at the method.
- **The three verification models are not collapsed here.**
  `WP 17.1B`'s row scoped that alongside the transactional store; it is
  deferred to `WP 18.2B` with a backlog row, because it is an unrelated
  change to unrelated types and merging it into this one would have made
  a substrate change harder to review than it already is.

## Alternatives Considered

**Keep the compensation and make it correct.** This is what
`WP 16.4B-R7` did, and it worked: the undo is correct, the evidence test
is correct, and every term of it was pinned by a test. It still could not
make `TD-143`'s `groupedUnder` edge disappear, because that edge was
durably written by the time the failure occurred, and no amount of
in-memory rollback reaches disk. Compensation can restore the instance;
it cannot unwrite a committed record. Rejected because the defect is
structural.

**Per-object or per-project locks.** Rejected for the lock-ordering
reason given above. The cycle check reads an unbounded ancestor chain and
the delete check an unbounded child set; correct fine-grained locking
over those needs a global ordering over objects that the graph itself
does not supply.

**Two-phase commit across the existing stores.** Rejected: it is a
distributed-transaction protocol between four components inside one
process over one database file, which is strictly more machinery than
using the transaction the database already has.

**Attachment bytes on the filesystem, referenced by path.** Rejected in
the Work Package before migration began, and recorded there: it
reintroduces exactly the two-writer problem for the one payload most
expensive to reconcile, and the write-intent marker and sweep exist as
proof of what that costs.

**A lazy, per-kind repository warm-up, in this Work Package.** Deferred.
Eager rehydration in one `ReadAllAsync` is a single, obvious population
path; a lazy cache with two population paths is the shape this ADR is
removing.

## Related Documents

- `ADR-0144` — the SQLite backend and `IQueryablePersistenceStore`, which
  this decision is built on.
- `ADR-0113` — the engineering object state record. Superseded in part.
- `ADR-0116` — the write boundary and acting principal. Its write
  ordering is superseded.
- `ADR-0114`, `ADR-0120` — the state record's schema versioning and
  migration rules, unchanged.
- `ADR-0075` — the facet interfaces, unchanged.
- `docs/architecture/Engineering Object Rehydration Architecture.md`.
- `docs/releases/v1.0.0/WorkPackages.md`, `WP 17.1B`.

# Engineering Object Rehydration Architecture

**Realises `TD-85` and `ADR-0113`. Closes the durability gap `ADR-0077`
disclosed.**

## The question this answers

> A user creates engineering work, closes TempestOS, and opens it again.
> Is it still their work — the same objects, the same relationships, the
> same states — or has the platform quietly started a new object graph
> over their old documents?

Before `TD-85` the answer was the second one. Engineering **documents**
were durable (`ADR-0053`); the **objects** constructed over them were not
(`ADR-0077`). A relaunched TempestOS had an empty
`IEngineeringObjectRepository` and an empty
`IEngineeringRelationshipRepository`, so nothing read through the object
graph was findable in the next session.

## The three layers, and which of them is authoritative

| Layer | What it holds | Durable? | Authority |
|---|---|---|---|
| `IPersistenceStore` | Key/value collections, durable writes, and — since `ADR-0144` — prefix listing, whole-collection reads and multi-key transactions through `IQueryablePersistenceStore` | Yes | The substrate |
| `IEngineeringDocumentStore` | Document identity, Kind, created-at, every revision, every outgoing reference | Yes | **Authoritative** for identity, content and links |
| `IEngineeringObjectStateStore` | One `EngineeringObjectState` per object | Yes | **Authoritative** for object state |
| `IEngineeringObjectRepository` / `IEngineeringRelationshipRepository` | The live object graph and its relationship index | No — rebuilt at startup | Derived, never authoritative |

The two stores are **one authority split by concern**, not two competing
ones. The document owns what a document owns; the state record owns what a
document was never designed to carry. Everything in memory is derived from
them.

**Since `ADR-0145`, "split by concern" no longer means "written
separately".** The two stores were, until `WP 17.1B`, two writers with no
transaction between them, and the in-memory relationship repository was a
third: a change that touched a document record and a state record was two
durable writes, and a failure between them left them disagreeing. Both now
write through the same `IPersistenceTransaction`, opened once per logical
change by `EngineeringDomainContext.ExecuteWriteAsync` under one
domain-wide write lock. The concern split survives as a *reading* split —
which store answers which question — and the writing split is gone.

### The substrate, since `ADR-0144`

`IPersistenceStore` is one SQLite database, `tempest.db`, under the
persistence root (`Persistence:RootPath`, default `persistence-data`), in
WAL journal mode with `synchronous=FULL` — so a write that returns has
been fsynced, and a write that throws has not landed. It was a tree of
percent-encoded files per `collection`/`key` pair until `WP 17.1A`;
nothing above it changed, because `IPersistenceStore` and
`IBinaryPersistenceStore` are unchanged, but three things below it did and
they matter to this document:

- **Rehydration no longer scans a directory.** `EngineeringObjectStateStore`
  reading every state record was one `readdir` plus one file open per
  object; the same read is now one indexed query
  (`IQueryablePersistenceStore.ReadAllAsync`), which `WP 17.1B` moved it
  onto. `EngineeringObjectRehydrationService` reads every state record in
  that single call. It stays **eager**: a lazy, per-kind warm-up is
  deferred, because a cache that can be populated by two paths is a second
  consistency problem and `ADR-0145` exists to remove one.
- **Keys are exact.** A collection or key is stored verbatim and
  case-sensitively; it is no longer a file name, so it needs no encoding
  and can be any Unicode string of any length.
- **The store has a lifetime.** It holds `<root>/tempest.lock`
  exclusively, so a second TempestOS instance on one root is refused
  rather than allowed to interleave writes, and the Host disposes it at
  its Service Disposal phase.

The substrate now also has a **multi-key transaction**
(`ExecuteInTransactionAsync`), and since `WP 17.1B` the engineering domain
is built on it. State is still written per mutation, per object — one
transaction per logical change — but every record that change touches now
lands in that one transaction rather than in a sequence of independent
writes.

**The engineering domain therefore requires the SQLite backend.** The
file-per-key store implements `ExecuteInTransactionAsync` as a bare
sequence of writes, having no mechanism capable of being atomic, and its
own implementation says so at the method. Running the engineering domain
on `Persistence:Backend=files` gives up everything `ADR-0145` guarantees;
`ADR-0144` retires that backend in `v0.18.0`.

## What is persisted, and when

`EngineeringObjectState` (`Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectState.cs`):

```
Id, Kind, Identifier, DisplayName, Metadata,
Status, ParentId, IsDeleted,
BomLine   (Quantity, UnitOfMeasure, FindNumber, ItemNumber, ReferenceDesignator),
History   (From, To, ActorPrincipalId, OccurredAt, ApprovalId)*,
Attachments (Id, FileName, ContentType, SizeInBytes)*,
TypeState (the concrete type's own fields)
```

Written to the `EngineeringDomain.ObjectState` collection, keyed by the
object's own Id, at:

- **creation** — `EngineeringObjectFactory<T>.CreateAsync`;
- **every mutation that changes state** — `TransitionAsync`,
  `RenameAsync`, `MoveAsync`, `DeleteAsync`, `AttachAsync`,
  `SetBomLineAsync`.

There is deliberately **no save step**. A user cannot forget to save, and
a crash cannot lose the last edit.

Revision content is not duplicated here — it already lives durably in the
document store, which stays its single home.

## How a type persists and restores itself

Each concrete canonical type owns both directions, symmetrically, in its
own file:

```csharp
public sealed class Part : EngineeringObjectBase, IPart, IRehydratable<Part>
{
    public string? MaterialId { get; }

    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(MaterialId)] = MaterialId;

    static Part IRehydratable<Part>.Rehydrate(
        IEngineeringDocument document, IDocumentRevision currentRevision,
        EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(MaterialId)));
}
```

Keys are `nameof`, so renaming a property cannot silently orphan its
persisted value. A derived type calls `base.CaptureTypeState(state)` and
adds its own.

**There is no central switch over Kind.** Nothing outside a type knows that
type's fields, and a type that has not implemented `Rehydrate` cannot be
registered — a compile error, not a startup surprise.

## How a Kind string becomes a typed object

```
EngineeringObjectState.Kind  ─►  IEngineeringObjectRehydratorRegistry
                                        │
                                        ▼
                             EngineeringObjectRehydrator<T>
                                        │  T.Rehydrate(...)
                                        │  instance.AttachSelfFactory(...)
                                        │  instance.RestoreState(state)
                                        ▼
                                 a real IEngineeringObject
```

The registry is a **Domain-layer** contract, because the only thing
startup has is a Kind string read from disk and the map back to a type must
live where the types live. It declares no Kind string of its own
(`ADR-0105`) — each Kind's declaring class registers it:

| Declaring class | Kinds |
|---|---|
| `MechanicalObjectFactoryRegistry` | Project, Assembly, SubAssembly, Part, Component, Configuration, Baseline, Release |
| `DocumentObjectFactoryRegistry` | Document, Drawing, CadModel |
| `ManufacturingObjectFactoryRegistry` | ManufacturingOperation, WorkInstruction, Inspection |
| `CalculationObjectFactoryRegistry` | Calculation, CalculationSet |
| `VerificationActivityFactoryRegistry` | VerificationActivity |
| `SampleEngineeringObjectRehydrators` | Portfolio, Programme, Risk, Decision, Task, Milestone, Deliverable, ChangeRequest, EngineeringChange, Supplier, PurchaseItem, ExternalSystemLink |

**29 Kinds — every Kind with a live write path anywhere in `src/`.**

Registering a different type for an already-claimed Kind throws
(`DuplicateRehydratorRegistrationException`); registering the identical
type twice is a no-op, so a composition root that runs a discipline's
registration more than once is not punished for it.

## Startup

```
TempestHost.StartAsync
  └─ registers IEngineeringObjectStateStore, IEngineeringObjectRehydratorRegistry,
     EngineeringDomainContext, EngineeringObjectRehydrationService

EngineeringWorkspaceComposer.RegisterEngineeringDisciplines
  └─ each discipline registers its own Kinds' rehydrators

EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync   ◄── the recovery step
  └─ EngineeringObjectRehydrationService.RehydrateAsync
       1. read every EngineeringObjectState
       2. skip any object already live in this process
       3. resolve the rehydrator for its Kind
       4. find its document and current revision
       5. reconstruct, restore, register
       6. rebuild the relationship index from durable DocumentReferences
```

Both entry points run it — `Tempest.Desktop.WorkspaceHost.StartAsync` and
the console `Tempest.App/Program.cs` — so the two shells recover the
identical work from the identical store.

**An object already in the repository is never replaced by a disk
snapshot.** It is the same object, live, possibly with mutations not yet
written; overwriting it would discard them. Rehydration only fills in what
the process does not already have.

## Revision uses the same pair

`IHasRevisions.ReviseAsync` produces a new *instance* of the same object,
so it must carry the same object's whole state:

```csharp
var revised = _selfFactory(refreshedDocument, newRevision);
revised.AttachSelfFactory(_selfFactory);
revised.RestoreState(CaptureState());
```

It previously copied a hand-picked structural subset (`WP 9.0B`), which
reverted a revised object to `Draft` with no history — an in-memory-only
loss until object state became durable, at which point the revised
instance's next mutation wrote the reset to disk. `CaptureState`/
`RestoreState` is therefore the **single definition of an object's state**,
shared by persistence, rehydration and revision alike, so a field added to
it cannot be forgotten by one path and honoured by another.

## Relationships

Relationship *edges* were always durable. Rehydration rebuilds the
in-memory index from `GetReferencesAsync`, deriving `Category` through
`RelationshipKindCategoryMap.InferCategory`.

**`ADR-0145` changed how that edge is written.** `LinkAsync` used to
dual-write: the durable reference record through
`IEngineeringDocumentStore.LinkAsync`, and the in-memory index separately,
with nothing spanning the two. A failure between them left the index and
the store disagreeing, and a `MoveAsync` whose state write failed after
its link write left the `groupedUnder` edge behind for ever, because
nothing in this platform removes a relationship (`TD-143`). The reference
record, the object state that depends on it and the audit row are now one
transaction; **the in-memory index is updated only after that transaction
commits**, so it is a cache of committed state and never a co-equal
writer. A failed link leaves neither an edge nor an index entry, and
there is nothing to reconcile.

`DocumentReference` gained optional `CreatedByPrincipalId`/`CreatedAt` so
attribution survives too: without them, a rebuilt relationship would have
been credited to whoever happened to be signed in at startup. Both are
`null` for links written before `TD-85` — honestly absent rather than
fabricated.

## Partial failure

`EngineeringRehydrationResult` reports what came back and what did not:

| Field | Meaning |
|---|---|
| `ObjectCount` / `RelationshipCount` | What was restored |
| `UnknownKinds` | Kinds on disk that no discipline registered |
| `OrphanedStateIds` | State whose backing document is gone |
| `FailedObjectIds` | Objects whose reconstruction threw |
| `AlreadyLiveCount` | Objects this process had already loaded |
| `IsComplete` | Whether everything came back |

One unreadable record never costs a user every other object they own
(`TD-60`'s discipline for passive reads, applied to startup).

## What this removed

`ProjectDirectory` kept a second durable `Projects.Index` collection
purely because the object graph was not durable. It is **gone** —
collection, DTO, read/write paths, and its `IPersistenceStore` dependency.
`ProjectDirectory` now reads the one object graph. A recovered project is a
live `IProject` with its real lifecycle state, relationships, revisions and
contents, not a name-and-status snapshot.

## What is not attempted here

- **Write batching.** State is written per mutation, per object
  (`TD-86`).
- **State schema versioning/migration.** An unreadable or partial record
  degrades to skipped-or-defaulted rather than migrated (`TD-87`).
- **Lazy or paged rehydration.** Startup reads every persisted object
  (`TD-88`).
- **Attachment content.** Attachments remain metadata-only records
  (`TD-31`) — their metadata now survives restart; there were never any
  bytes to survive.
- **The `WP9.3A` link asymmetry (`TD-32`).** Rehydration rebuilds the
  relationship index from *all* durable references, including the
  Activity→Record `"verifiedBy"` links `VerificationService.RecordAsync`
  writes straight through the document store. The index is therefore
  slightly more complete after a relaunch than before one. That asymmetry
  is `TD-32`'s to close (`FCR-0057`), not this architecture's; it is
  recorded there, and consumers must not assume the index is
  session-invariant for such links.

## Proven by

- `tests/Tempest.Desktop.Tests/ObjectRehydrationAcceptanceTests.cs` — the
  18-step journey through the real `MainWindow` across three application
  lifetimes.
- `tests/Tempest.Core.Tests/EngineeringDomain/EngineeringObjectRehydrationTests.cs`
  — 24 tests over the real persistent stores.
- `tests/Tempest.Core.Tests/Shell/ProductSpineTests.cs` — the Product
  Spine's own restart tests, now driven through real rehydration rather
  than an index.

## Related Documents

`ADR-0113`; `ADR-0077`; `ADR-0072`; `ADR-0055`; `ADR-0053`; `ADR-0105`;
`docs/architecture/Product Spine Architecture.md`;
`docs/academy/02 Runtime Architecture/34-engineering-object-rehydration.md`.

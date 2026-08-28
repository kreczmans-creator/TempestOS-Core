# ADR-0113: Engineering Object State Is Durable, and Each Canonical Type Rehydrates Itself Through a Kind-Keyed Registry

## Status

Accepted — `TD-85` (Engineering Object Rehydration / Persistence Boundary), 2026-08-28. Closes the gap `ADR-0077` disclosed in its own Consequences; extends, and does not reopen, `ADR-0072`/`ADR-0077`.

## Context

`ADR-0077` established that the Engineering Domain's `IEngineeringObjectRepository`/`IEngineeringRelationshipRepository` are an **indexing layer**, not a document store, and disclosed the price:

> "The repository layer's own state (which objects exist, by Kind) is not itself durable — restarting the Host loses it, even though the underlying documents themselves survive in `IPersistenceStore`. A future Work Package wanting the repository to rebuild itself from the store on startup is a genuine, disclosed gap, not attempted here."

The Product Spine (`TD-84`) turned that disclosed gap into a product defect. The spine's own Definition of Done ends "close TempestOS, relaunch, keep working" — and it could not be met, because a relaunched TempestOS started a **new, empty object graph** over the user's still-persisted documents. `ProjectDirectory` worked around it for projects alone by keeping a second durable `Projects.Index` collection, which made a project *findable* by name and status while everything else a user had built silently disappeared, and while the project itself came back as an index snapshot rather than a live `IProject`.

**The obvious fix does not work.** A Kind-to-constructor map — which the five discipline factory registries look like they almost provide — cannot rehydrate anything, for two independent reasons:

1. **Wrong layer.** Those registries live in `Tempest.App` and are explicitly "never a Domain-layer registry contract" (`WP8.2B Dependency Rules.md` §8). They also construct a *fresh* `EngineeringObjectFactory<T>` per call, with the caller's arguments captured in a closure — they are creation helpers, not a durable type map.
2. **Wrong data, and this is the real problem.** `EngineeringObjectFactory<T>.CreateAsync` persisted only the document: `Kind`, created-at, and the first revision's prose. **Identifier, display name, metadata, lifecycle state and its history, structural parent, deletion, BOM line, attachments and every type-specific field lived only in the constructor closure and in memory.** A perfect Kind→constructor map would therefore have had nothing to pass to the constructor it resolved. State had to become durable *first*; the type map is the second, smaller half of the problem.

## Decision

**1. One object model. `EngineeringObjectState` is its serialisation, not a second model.**

A single `EngineeringObjectState` record captures everything that makes an object *that* object beyond its document: identity, Kind, identifier, display name, metadata, lifecycle state, transition history, structural parent, deletion, BOM line, attachments, and a `TypeState` dictionary the concrete type owns. It is to `IEngineeringObject` exactly what `EngineeringDocumentDto` already is to the backing document. No `ProjectModel`, no DTO hierarchy, no parallel repository: a rehydrated object is a real canonical object, of its own concrete type, holding its own real `EngineeringDomainContext`.

**2. One persistence authority, split by concern.**

`IEngineeringObjectStateStore`/`EngineeringObjectStateStore` writes one record per object, keyed by the object's own Id, in the `EngineeringDomain.ObjectState` collection of the **same** `IPersistenceStore` `EngineeringDocumentStore` already uses (`ADR-0053`). The document keeps owning identity, Kind and revision history; the state record owns what a document was never designed to carry. No new storage mechanism, and no competing authority.

State is written at creation and after every mutation that changes it (`TransitionAsync`, `RenameAsync`, `MoveAsync`, `DeleteAsync`, `AttachAsync`, `SetBomLineAsync`) — so there is no "save" step for a user to forget or for a crash to lose. The store is an **optional** collaborator on `EngineeringDomainContext`: a context composed without one behaves exactly as it did before, which is what keeps every hand-assembled test and sample pipeline working unchanged.

**3. Each type owns its own persistence, in both directions.**

- `EngineeringObjectBase.CaptureTypeState(IDictionary<string, string?>)` — a virtual hook each concrete type overrides to write its own fields.
- `IRehydratable<TSelf>.Rehydrate(...)` — a `static abstract` interface member each concrete type implements to read them back.

The two are symmetric and live in the same file as the type they belong to, keyed by `nameof` so a renamed property cannot silently orphan its own persisted value. **There is no central switch over Kind and no service that must be edited when a canonical type is added** — a type that has not implemented `Rehydrate` simply cannot be registered, which is a compile error rather than a startup surprise.

**4. A Domain-layer Kind→rehydrator registry, populated by each Kind's own declaring class.**

`IEngineeringObjectRehydratorRegistry` maps a Kind string read from disk back to `EngineeringObjectRehydrator<T>`. `WP8.2B` §8 proposed no Domain-layer registry, and creation genuinely never needed one: a caller who wants a Part already knows it wants a Part. Rehydration is the opposite problem — the only thing the platform has at startup is a Kind string — so the map must exist, and must live where the types live.

Kind *vocabulary* still belongs to its declaring class (`ADR-0105`): the registry declares no Kind string of its own. Each of the five discipline registries gained a `RegisterRehydrators` method passing the same named constants it already owns, and `Tempest.Samples` registers the twelve Kinds only its own modules declare.

**5. Rehydration is a startup step in the shared composition root, and it never overwrites live state.**

`EngineeringObjectRehydrationService.RehydrateAsync` reads every persisted state, resolves each Kind's rehydrator, reconstructs the object, restores its mutable facets, attaches its self-factory (so a rehydrated object can still revise itself into its own correct type), and registers it. It then rebuilds the relationship index from the durable per-document references the document store already held. `EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync` runs it after discipline registration and before anything reads the graph, in **both** the console and desktop entry points.

An object already present in the repository is **never** replaced by a disk snapshot — it is the same object, live, possibly with mutations not yet written. Rehydration only fills in what the process does not already have.

**6. Relationship provenance became durable.**

`DocumentReference`/`DocumentReferenceDto` gained optional `CreatedByPrincipalId`/`CreatedAt`, recorded by both `IEngineeringDocumentStore` implementations. Without them a rebuilt relationship would have been attributed to whoever happened to be signed in at startup. Both fields default to `null`, so a link written before this ADR reads back with honestly absent provenance rather than a fabricated attribution.

**7. `Projects.Index` was removed, not retained.**

With projects rehydrated as real `IProject` objects, `ProjectDirectory`'s second durable index had no remaining purpose, so it was deleted — collection, DTO, read/write paths and its `IPersistenceStore` dependency. Keeping it would have left two competing answers to "what projects exist?", which is the precise failure `TD-85` exists to end. A recovered project is now a live object with its real lifecycle state, relationships, revisions and contents — strictly more than the snapshot the index could ever return.

## Consequences

**Positive:**

- The product-level promise holds: create engineering work, close TempestOS, relaunch, and carry on working on the same objects — proven by an 18-step acceptance journey through the real `MainWindow` across three application lifetimes.
- One object model, one persistence authority, one project catalogue. A second persistence mechanism was removed rather than added.
- Adding a canonical Kind is two symmetric members on the type plus one registration line by its declaring class. No shared file grows a new `case`.
- Partial failure is survivable and honestly reported: `EngineeringRehydrationResult` names unknown Kinds, orphaned state, objects that threw, and objects already live. One unreadable record never costs a user every other object they own (`TD-60`'s discipline, applied to startup).
- Relationship attribution survives a restart truthfully.

**Negative:**

- Object state is now written on every mutation. Each write is a single small JSON value through the existing atomic-write store, but a bulk operation touching thousands of objects performs one write per object per mutation. No batching or write-behind is attempted here (`TD-86`).
- `EngineeringObjectState` is a schema. It is versionless today: an unreadable or partial record degrades to skipped-or-defaulted rather than migrated. Adequate while the shape is additive; a real migration story is disclosed debt (`TD-87`).
- Startup cost is linear in the number of persisted objects, and reads them all eagerly. There is no lazy or paged rehydration (`TD-88`).
- Rehydration rebuilds the relationship index from **all** durable references, including links written directly through `IEngineeringDocumentStore.LinkAsync` rather than through a Domain object (`WP9.3A`) — so the index is slightly *more* complete after a restart than before one. `DigitalThreadGraphModel` was made order-independent so this cannot change how a Verification record renders; the underlying asymmetry is `WP9.3A`'s, not this ADR's.
- A Kind whose declaring class does not register a rehydrator is not recoverable. This is now visible (`EngineeringRehydrationResult.UnknownKinds`) rather than silent, but it is a registration a new discipline must remember.

## Alternatives Considered

**A Kind→constructor map over the discipline factory registries alone** — considered first and rejected on investigation: wrong layer, and, decisively, the constructor arguments were never persisted. See Context.

**Reconstructing object state by parsing revision content** — rejected. `IHasRevisions.Content` is deliberately opaque prose (`ADR-0072`); inferring identifier, lifecycle and BOM data from it would invent a schema inside a field that has none, and would break the moment a user edited the text.

**Making `IEngineeringObjectRepository` itself durable** — rejected as reopening `ADR-0077`. The repository is an index; an index that persists is a second store. Persisting the objects' own state and rebuilding the index from it keeps `ADR-0077`'s decision intact and true.

**Keeping `Projects.Index` "for safety"** — rejected explicitly. Two durable answers to the same question is the defect, not the mitigation.

**A central `switch` over Kind in a rehydration service** — rejected. It would put every type's constructor arguments in one file owned by nobody, and would silently omit any Kind a contributor forgot, with no compile-time signal.

## Related Documents

- `ADR-0053` — the Engineering Data Model is built on the existing `IPersistenceStore`.
- `ADR-0055` — `MaterialCatalog`'s durable index over `IPersistenceStore` (the pattern `Projects.Index` copied, and which remains correct for its own case: a lookup by an arbitrary caller-chosen string, not a substitute for object durability).
- `ADR-0072` — every canonical object is realised as an `IEngineeringDocumentStore`-backed Kind.
- `ADR-0077` — the in-memory repository layer, and the durability gap this ADR closes.
- `ADR-0105` — vocabulary values are declared once, by their owning class.
- `docs/architecture/Engineering Object Rehydration Architecture.md`
- `docs/academy/02 Runtime Architecture/34-engineering-object-rehydration.md`
- `docs/governance/Quality/Technical Debt Register.md` — `TD-85` (resolved), `TD-86`/`TD-87`/`TD-88` (disclosed here).

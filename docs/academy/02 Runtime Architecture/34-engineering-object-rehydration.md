# Engineering Object Rehydration

**Programme:** Product Convergence & Recovery, 2026-08-28 ·
**Debt:** `TD-85` (resolved) · **Decision:** `ADR-0113` ·
**Code:** `Tempest.Core.EngineeringDomain`

## The bug that was not a bug

`ADR-0077` said it plainly, in 2026-08-04, in its own Consequences:

> The repository layer's own state is not itself durable — restarting the
> Host loses it, even though the underlying documents themselves survive.

That is an honest, deliberate, disclosed decision. Nobody hid anything.
And yet, four weeks later, the Product Spine's Definition of Done ended
with *"close TempestOS, relaunch it, and continue"* — and the platform
could not do it. Documents survived; **the engineering work did not.**

The lesson is not "someone should have caught this". It is that **a
disclosed gap is still a gap.** A trade-off recorded in an ADR buys you
honesty, not immunity. When the product later depends on the thing you
disclosed, the disclosure does not save you — and the register entry that
looked like architecture (`TD-85`) turns out to have been a P0 product
defect wearing a technical costume.

## The trap: the obvious fix

Five discipline registries in `Tempest.App` map a Kind string to a
constructor. So rehydration is just: read the Kind, look up the
constructor, done?

No — and it is worth understanding **why not**, because the wrong answer
is genuinely plausible:

1. **They are in the wrong layer.** Their own doc comment says it:
   *"Never a Domain-layer registry contract."* They also build a fresh
   factory per call, with the caller's arguments captured in a closure.
   They are creation helpers, not a durable type map.

2. **The constructor had nothing to be given.** This is the real one.
   `EngineeringObjectFactory<T>.CreateAsync` persisted a document —
   `Kind`, created-at, and one blob of prose. Identifier, display name,
   metadata, lifecycle state, transition history, parent, deletion, BOM
   line, attachments and every type-specific field lived **in the
   constructor closure and nowhere else.** A perfect Kind→constructor map
   would have resolved the right constructor and then had no arguments to
   pass it.

> **The transferable lesson.** When a lookup table looks like the fix,
> check whether the data the table would feed actually exists. Here the
> hard half was not "which type?" but "with what?". State had to become
> durable first; the type map was the smaller, second half.

## The shape of the answer

### One object model, serialised

`EngineeringObjectState` is not a second model. It is to
`IEngineeringObject` exactly what `EngineeringDocumentDto` already is to
its backing document: the serialisation of the one canonical thing. A
rehydrated object is a real `Part`, a real `Project` — same type, same
`EngineeringDomainContext`, same everything. There is no `ProjectModel`,
no DTO hierarchy, no parallel repository.

### One authority, split by concern

The state store writes into the **same** `IPersistenceStore` the document
store already uses. The document owns identity, Kind and revisions; the
state record owns what a document was never designed to carry. Two
collections, one authority. The in-memory repositories stay exactly what
`ADR-0077` said they were — a derived index — and are now rebuilt from
that authority at startup rather than starting empty.

### Each type persists and restores itself

The pattern worth stealing:

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

Two members, symmetric, in the same file as the type, keyed by `nameof`.

Three properties follow from that shape, and all three were requirements:

- **No central switch.** No file anywhere contains
  `switch (kind) { case "Part": ... }`. Nothing outside a type knows that
  type's fields.
- **Forgetting is a compile error.** `Rehydrate` is a `static abstract`
  interface member, so `registry.Register<Part>(...)` will not compile
  unless `Part` implements it. You cannot register a type that cannot
  rebuild itself.
- **Adding a Kind is local.** Two members on the type, one line by its
  declaring class. No shared file grows.

### The registry, and who fills it

`WP8.2B` §8 proposed no Domain-layer registry, and creation genuinely
never needed one: a caller who wants a `Part` already knows it wants a
`Part`. **Rehydration is the mirror problem** — at startup the only thing
you have is a Kind string read off a disk — so the map must exist, and it
must live where the types live.

But the *vocabulary* still belongs to its owner (`ADR-0105`). The registry
declares no Kind string of its own; each declaring class registers its
own, passing the constants it already owns. 29 Kinds, six declaring
classes, one line each.

> **The transferable lesson.** "We decided not to have a registry" is a
> decision about a problem, not a law. When a genuinely new problem
> arrives — here, *string on disk → type in memory* — reopen the decision
> explicitly and record why, rather than smuggling the map into a service
> that has no business owning it.

## Two things the tests found that design review did not

**Rehydration must not overwrite a live object.** The composition order is
host start (during which sample modules create objects) → discipline
registration → rehydrate. So rehydration meets objects that are already
live, holding mutations not yet written. Replacing them with a disk
snapshot would silently discard the newest edits. Rehydration now fills in
only what the process does not already have — and reports how many it
skipped.

**Rebuilding an index can make it *more* complete, and that can break
something.** The relationship index is rebuilt from durable references —
including links written straight through `IEngineeringDocumentStore.LinkAsync`
rather than through a Domain object (`WP9.3A`). Those had never been in
the index before. Suddenly a Verification record was reachable as an
ordinary neighbour, and the Digital Thread graph's first-wins node
insertion rendered it as an expandable node instead of a result leaf — in
the *second* session only.

The fix was not to make rehydration less complete. It was that the graph
had a latent bug: a node's identity depended on the order it was
discovered in. Verification records are now added first, so a record is a
record however it was reached.

> **The transferable lesson.** Restoring state exposes order-dependence
> you never see in a single session, because in a single session the order
> is always the same. "It only fails after a restart" usually means "it
> was always fragile."

## What we deleted

`ProjectDirectory` kept a durable `Projects.Index` collection — a second
persistence mechanism, added by the previous Work Package as an honest,
disclosed workaround for exactly this gap. With projects now rehydrated as
real `IProject` objects, it was **deleted**: collection, DTO, read/write
paths, its `IPersistenceStore` dependency, and its tests' reliance on it.

The temptation to leave it "for safety" is strong and should be resisted.
Two durable answers to "what projects exist?" is not a safety net; it is
the defect. And the object graph strictly dominates the index anyway — a
recovered project now has its real lifecycle state, relationships,
revisions and contents, where the index could only ever return a name and
a stale status.

> **The transferable lesson.** When you fix the cause, remove the
> workaround in the same change. A workaround kept past its cause becomes
> a second source of truth, and the next reader cannot tell which one to
> trust.

## The Definition of Done was a behaviour, not a round-trip

It would have been easy to declare victory at *"the object serialises and
deserialises"*. That is a unit test, and it is not the product promise.

The acceptance journey drives the **real** `MainWindow` over **three**
application lifetimes sharing one persistence root: launch → Projects →
create → open → Engineering → create an assembly and a part through the
real production command the ribbon dispatches → transition, rename, set a
BOM line, link, revise → close → **relaunch** → the project is recovered,
the objects are back as their own concrete types with lifecycle, history,
relationships, BOM data, parent and revisions intact → open the project by
clicking the real button in the real project browser → keep working:
another transition, another revision, another new part → **relaunch
again** → that work is there too.

Nothing in it inspects a file on disk. Every assertion is made against
what the running application hands back.

> **The transferable lesson.** For anything involving persistence, the
> third lifetime is the one that matters. Two lifetimes prove you can
> write and read. Three prove that what you did *after* recovery is real
> work and not a read-only echo.

## What we did not do, and said so

- Write batching — state is written per mutation, per object (`TD-86`).
- State schema versioning — unreadable records degrade rather than migrate
  (`TD-87`).
- Lazy rehydration — startup reads every persisted object (`TD-88`).

Each is a real cost, bounded, disclosed, and cheap to address when a real
workload demands it. None of them is a workaround.

## Related

`ADR-0113` · `ADR-0077` · `ADR-0072` · `ADR-0055` ·
`docs/architecture/Engineering Object Rehydration Architecture.md` ·
`docs/academy/02 Runtime Architecture/33-the-product-spine.md`

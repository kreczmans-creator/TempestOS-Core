# WP 2.2 — Runtime Registration

## 1. Introduction

WP 2.2 introduced the Runtime Module Manager: the single authoritative runtime
catalogue of every module known to TempestOS. Where WP 2.1 answered "what modules
exist," WP 2.2 answers "which of those modules has the runtime actually accepted,
and what do we know about each one right now." This is a narrower question than
it might first sound — WP 2.2 explicitly does *not* discover, instantiate, inject
dependencies into, or execute the lifecycle of a single module. It only
registers, tracks, and provides lookups.

This document explains why that narrowness was deliberate, what it cost, and what
it bought — for both WP 2.2 itself and for the two work packages built directly on
top of it.

## 2. Purpose

To give TempestOS one authoritative, in-memory catalogue of registered modules —
a `RuntimeModule` per module, keyed by ID, preserving registration order,
protected against duplicate registration, exposed only through genuinely
read-only collections — that later work (lifecycle orchestration, dependency
injection) could depend on without needing to reimplement registration,
deduplication, or lookup logic themselves.

## 3. Background

WP 2.1 had already established `ModuleDescriptor` as discovery's output: a
module's metadata plus its concrete `Type`. Nothing yet existed to actually *keep*
that information anywhere the rest of the runtime could query — discovery's
`DiscoverModules()` returns a list and forgets about it the moment the caller lets
go of the return value. WP 2.2 was the first place a *stateful*, in-memory record
of "which modules does this running instance of TempestOS actually know about"
would live.

The retired Python prototype had attempted something structurally similar — a
`WorkPackageRegistry` that simply appended items to a list, with no duplicate
protection, no lookup, and no ordering guarantee beyond insertion order by
accident rather than by design. WP 2.2 was not a continuation of that code (it was
retired along with the rest of the prototype), but the *shape* of the problem —
"keep track of a growing set of named things, safely" — was familiar territory.

## 4. The Problem

1. **How does a discovered module become "known" to the runtime**, and what
   happens if the same module is registered twice?
2. **What should the runtime representation of a registered module look like**,
   given that a *future* work package (already anticipated, not yet designed) would
   need to track that module's lifecycle state on top of whatever WP 2.2
   produced?
3. **How should lookups work** — by exact match (throwing if missing), by
   speculative check (a boolean-returning `TryGet`), or both?
4. **How can callers enumerate every registered module** without being able to
   corrupt the manager's internal bookkeeping by mutating whatever collection they
   receive?
5. **Must registration order be preserved**, and if so, why — as opposed to some
   other ordering (e.g., alphabetical, matching discovery's own convention)?
6. **How does registration activity get logged**, consistent with the rest of the
   runtime's logging conventions?
7. **How thread-safe does this need to be**, given a stated requirement to be
   "thread-safe where practical without introducing unnecessary complexity"?

## 5. The Design

Four public types:

- **`ModuleState`** — a lifecycle-state enum, introduced in WP 2.2 with only two
  values actually exercised (`Discovered`, `Registered`) but with the *full*,
  eventual lifecycle enumeration established up front — `Initialised`, `Running`,
  `Disabled`, `Failed` — explicitly so the type would have "a stable API for
  future releases" before those releases existed. WP 2.3 later extended this same
  enum additively (see WP 2.3's own retrospective) rather than replacing it.
- **`RuntimeModule`** — the immutable runtime record of a registered module:
  `Descriptor`, `State`, `RegisteredAt`, `FailureReason`, all get-only, with an
  `internal` constructor so only `RuntimeModuleManager` can create one. This is
  the subject of ADR-0001 and its accompanying case study, both of which go into
  the reasoning in full depth; this document summarises rather than repeats it.
- **`IRuntimeModuleManager`** — the contract: `Modules` (all registered modules,
  in registration order), `Register(descriptor)`, `Get(id)` (throws if missing),
  `TryGet(id, out module)` (returns `false` if missing), `IsRegistered(id)`,
  `GetAll()`.
- **`RuntimeModuleManager`** — the concrete, thread-safe implementation. A single
  `object` lock guards a `Dictionary<string, RuntimeModule>` (for O(1) lookup) and
  a parallel `List<RuntimeModule>` (preserving registration order, which the
  dictionary alone cannot do). `Register` validates the descriptor, checks for an
  existing entry under the same ID, creates the `RuntimeModule`, stores it in
  both structures, logs success, and returns it.

Two dedicated exceptions: `ModuleRegistrationException` (base) and
`DuplicateModuleRegistrationException` (thrown the instant a second registration
under an already-used ID is attempted) plus `ModuleNotRegisteredException`
(thrown by `Get` for a missing ID) — a deliberately *separate* hierarchy from
WP 2.1's `ModuleDiscoveryException` family, discussed further below.

## 6. Alternatives Considered

**Reusing `ModuleDiscoveryException` as the base for registration failures.**
Considered, and rejected. `ModuleDiscoveryException` represents failures *during
discovery* — bad metadata found via reflection, before a module is even a
candidate for registration. A registration failure (a duplicate ID; a lookup for
an unknown ID) can happen entirely independent of discovery — a descriptor could,
in principle, be constructed and registered without ever going through discovery
at all. Catching `ModuleDiscoveryException` to handle a registration problem, or
vice versa, would blur two genuinely different pipeline stages together. A new,
parallel hierarchy (`ModuleRegistrationException` → `DuplicateModuleRegistrationException`
/ `ModuleNotRegisteredException`) mirrors WP 2.1's shape without reusing its
base.

**A mutable `RuntimeModule` with a settable `State`.** Considered, in the sense
that it was the obvious, path-of-least-resistance option once WP 2.3's eventual
need to track lifecycle state was anticipated. Rejected for the reasons detailed
at length in ADR-0001 and its case study: it would have merged registration and
(future, not-yet-built) lifecycle concerns into one mutable object with no
enforced boundary between them.

**Returning arrays instead of `ReadOnlyCollection<T>` for `Modules`/`GetAll()`.**
Considered and rejected in favour of `ReadOnlyCollection<T>` wrapping a defensive
copy. An array satisfies the type signature `IReadOnlyCollection<T>`, but a
determined caller can cast it back to `RuntimeModule[]` and mutate elements
in-place via the indexer — arrays do not enforce immutability through the
collection interfaces the way `ReadOnlyCollection<T>` does (whose `IList<T>`
implementation explicitly throws `NotSupportedException` on any mutating member).
Given the brief's explicit requirement that "consumers must never receive mutable
collections," the stronger guarantee was chosen deliberately, and is directly
tested (`Modules_CannotBeMutatedByConsumers`, asserting `NotSupportedException` on
`Add`/`Clear` via the interface, not just checking the runtime type).

**Preserving registration order vs. sorting alphabetically like discovery does.**
Seriously considered, specifically because it would have been *consistent* with
WP 2.1's own convention. Rejected: registration order is itself meaningful,
observable information (the actual sequence modules were registered in), and
sorting would destroy it for no benefit — nothing about registration's *purpose*
(cataloguing) requires or benefits from alphabetical ordering the way discovery's
determinism requirement did. This turned out to be an important, deliberate point
of *divergence* from discovery's convention, not an oversight — WP 2.3
subsequently needed its *own*, separate ordering guarantee (ascending by `Id`,
for lifecycle purposes) built on top of `RuntimeModuleManager`'s registration-order
output, precisely because the two orderings serve different needs.

## 7. Why This Solution Was Chosen

Each design decision traces back to one governing question: what does the *next*
work package need to be able to depend on, safely, without needing to know
`RuntimeModuleManager`'s internals? An immutable `RuntimeModule` means later code
can hold, compare, and log registered modules without synchronisation concerns. A
genuinely-immutable collection type means later code can enumerate registered
modules without risking accidental mutation of the manager's own state. A
dedicated exception hierarchy means later code can distinguish "this ID is
already registered" from "this discovery-time metadata is invalid" without string
parsing or fragile `is`-checks against the wrong base type.

## 8. Architectural Principles

- **Single Responsibility** — registration, and nothing else; explicitly not
  discovery (already done), not lifecycle (not yet built), not construction (not
  yet built).
- **Immutability** — `RuntimeModule` itself, and the collections handed to
  callers, are both immutable, for different but related reasons (see the
  Immutability Engineering Principle document).
- **Fail Fast** — a duplicate registration is rejected the instant it's
  attempted, with a dedicated exception carrying the colliding ID, rather than
  silently overwriting or silently ignoring the second registration.
- **Defensive Programming / Thread Safety** — a single lock, chosen deliberately
  over more elaborate concurrency primitives, because the manager's actual
  workload (occasional registration, frequent read-only lookup) does not justify
  the complexity of finer-grained locking or lock-free structures.

## 9. Benefits

- `RuntimeModuleManager` became the one place WP 2.3 and WP 2.4 both needed to
  depend on to know "what modules exist right now" — neither needed to touch
  discovery directly, and neither needed to reinvent deduplication, lookup, or
  ordering.
- The `RuntimeModule`/`ModuleLifecycleStatus` split (WP 2.2 vs. WP 2.3) that
  followed from ADR-0001's immutability decision meant WP 2.3 could be built
  *entirely* as an addition — not a single line of `RuntimeModuleManager` or
  `RuntimeModule` needed to change to support it.
- The genuinely-immutable collection pattern established here
  (`ReadOnlyCollection<T>` over a defensive copy) was reused directly by
  `ModuleLifecycleManager.Modules` in WP 2.3, becoming a recognised TempestOS
  convention rather than a one-off decision.

## 10. Trade-offs

- A single coarse-grained lock means registration and lookup, while individually
  fast, cannot proceed in parallel with each other on different modules — a
  reasonable trade for the manager's actual workload, but one that would need
  revisiting if registration or lookup ever became a genuine throughput
  bottleneck (no evidence of this exists today).
- `RuntimeModule.State` is, in practice, always `ModuleState.Registered` — the
  full ten-or-so-value `ModuleState` enum this type references was established
  ahead of any code that would exercise most of its values, which is a deliberate
  bet that the lifecycle enumeration WP 2.3 would need was already roughly known
  — a bet that paid off (WP 2.3 extended the enum additively, not destructively)
  but was, at the time WP 2.2 shipped, still a bet rather than a certainty.

## 11. Common Mistakes

The most consequential decision in this work package was choosing a *separate*
exception hierarchy over reusing WP 2.1's. It would have been easy, and would
have compiled and worked correctly, to simply extend `ModuleDiscoveryException`
for registration's own duplicate-ID case — the two situations (duplicate ID at
discovery time; duplicate ID at registration time) even sound superficially
similar. The mistake this avoids is subtle: it would have meant a caller could
never `catch (ModuleDiscoveryException)` to handle "something went wrong during
discovery specifically" without *also* silently catching registration failures
that have nothing to do with discovery having gone wrong. Keeping the hierarchies
separate, even though it meant near-duplicating the base+subtype shape, keeps
`catch` blocks throughout the codebase honest about what category of failure
they're actually handling.

A related trap to watch for in any future registration-adjacent work: assuming
"registration order" and "discovery order" are the same thing, or that either is
interchangeable with "alphabetical order." They are three different, independently
meaningful orderings in this codebase, and conflating any two of them has already
been a source of confusion worth explicitly guarding against in code review.

## 12. Future Evolution

- **Deregistration.** No API exists to remove a registered module. If hot-reload
  or dynamic module unloading is ever required, this is a deliberate gap that
  needs a considered design (what happens to a module's lifecycle state, and any
  live instance, on deregistration?) rather than a quick addition.
- **Persistent registration.** Today's registry is purely in-memory and rebuilt
  from scratch on every process start (via a fresh discovery pass). A persistent
  catalogue (surviving process restarts) was never a WP 2.2 goal and would be a
  substantial, separate design exercise if ever needed.
- **Finer-grained concurrency.** If registration or lookup throughput ever
  becomes a genuine bottleneck, the single coarse lock is the first thing to
  revisit — but only in response to a demonstrated need, not speculatively.

## 13. Key Takeaways

1. Choosing a *separate* exception hierarchy over reusing an existing,
   superficially-similar one is often the right call when the two failure
   categories represent genuinely different pipeline stages — even at the cost
   of some structural duplication.
2. Two orderings that look interchangeable (registration order vs. discovery's
   alphabetical order) can both be correct, deliberate, and simultaneously
   necessary, for different consumers with different needs — the mistake is
   assuming there's only one "right" order for a system to impose.
3. Immutability decisions made in one work package (ADR-0001, this document's
   central design choice) directly determined how cleanly a *later*, not-yet-designed
   work package (WP 2.3) could be added — evidence that the cost of getting an
   early design decision right compounds forward through the entire pipeline.
4. "Thread-safe where practical without unnecessary complexity" is itself a
   design principle worth stating explicitly — a single lock, chosen
   deliberately over more elaborate primitives, is not a shortcut when the
   workload doesn't justify the alternative; it's the correct, considered choice.

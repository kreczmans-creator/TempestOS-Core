# WP 4.4D — Event Bus Implementation

## 1. Introduction

WP 4.4D implements `IEventBus`/`EventBus` exactly as ADR-0028 and `Event
Bus Architecture.md` designed them — imperative subscription, sequential
snapshot-based dispatch in subscription order, unconditional per-subscriber
failure isolation, and registration as an ordinary container-constructed
singleton. Unlike the architecture-only phase immediately before it
(`WP 4.4`'s own design phase), this work package produces real, tested
production code — the first `Tempest.Core.Events` types beyond the WP 4.0
contracts, and the first new line `TempestHost.cs` has needed since
`WP 4.2C`.

## 2. Purpose

To realise ADR-0028's decision precisely, prove it against dedicated unit
tests exercising `EventBus` directly — never `ClockModule`, never a new
sample or companion module — and demonstrate, not merely argue, that
`TempestHost`, `RuntimeModuleManager`, `ModuleLifecycleManager`, Discovery,
and Composition Root ownership are all completely unaffected beyond the
one approved registration line.

## 3. Background

`WP 4.4`'s own architecture phase (see `Event Bus Architecture.md`,
ADR-0028) answered every open dispatch, subscription, ordering, failure,
re-entrancy, and registration question in writing, before any code was
written. That phase was itself triggered by `WP 4.4C`, which discovered
mid-investigation that no `IEventBus` existed anywhere despite a task
brief assuming otherwise. With the design settled and approved, this work
package's own brief was explicit about its boundary: implement the bus
exactly as designed; do not modify `ClockModule`; do not implement event
publishing; do not begin sample module integration. Those three
exclusions are a single boundary, stated three ways — this work package
builds the platform service, not a consumer of it.

## 4. The Problem

1. **Implement exactly the shape ADR-0028 specifies** — `Subscribe`,
   `Unsubscribe`, `PublishAsync`, sequential dispatch over a per-call
   snapshot, unconditional isolation — without inventing any capability
   the design did not already call for.
2. **Prove the failure and re-entrancy model, not merely implement it** —
   a throwing subscriber, a re-entrant publish (same and different event
   type), and a subscriber added or removed mid-dispatch must each be
   demonstrated against the real implementation, not argued from the code
   alone.
3. **Register the service without touching anything else** — one new line
   in `TempestHost.cs`'s existing Platform Services Registered block; no
   new Host phase, no Composition Root change, no change to Discovery,
   `RuntimeModuleManager`, or `ModuleLifecycleManager`.
4. **Touch neither `ClockModule` nor any new sample/companion module** —
   the bus must stand on its own, fully validated, before any consumer is
   built against it.

## 5. The Design

See `src/Tempest.Core/Events/IEventBus.cs` and
`src/Tempest.Core/Events/EventBus.cs` in full — implemented without
deviation from ADR-0028's own code skeleton. `EventBus` holds subscribers
in a single `Dictionary<Type, List<object>>`, keyed by exact event type,
guarded by one `_gate` lock — mirroring `RuntimeModuleManager`'s own
pattern exactly. `Subscribe`/`Unsubscribe` mutate the relevant list under
the lock. `PublishAsync<TEvent>` takes an immutable `ToList()` snapshot of
the current subscriber list for `typeof(TEvent)` under the lock, then
dispatches outside it: sequentially, awaited one at a time, in
subscription order — the same shape as
`ModuleLifecycleManager.RunBatchAsync`. Cancellation is checked at the top
of each iteration (between subscribers, never mid-`HandleAsync`);
`OperationCanceledException` propagates uncaught. Any other subscriber
exception is caught, logged at `LogLevel.Error` with the event type, the
failing handler's own type, and the captured exception, and never
rethrown. `TempestHost.cs` gained exactly one new line,
`services.Singleton<IEventBus, EventBus>();`, in its existing Platform
Services Registered block, immediately after the existing
`platformVersionProvider` registration and before `AddDiscoveredModules`.

## 6. Alternatives Considered

None — this work package implements an already-decided ADR exactly, per
its own explicit brief. No new architectural alternative was evaluated
here; see ADR-0028 and its own retrospective for the alternatives the
design phase weighed and rejected (RD-0019 through RD-0022).

## 7. Why This Solution Was Chosen

Not applicable in the usual sense — the solution was chosen by ADR-0028.
This work package's own judgment calls were narrow: storing subscribers as
`List<object>` per event type (rather than a more elaborate generic
container) keeps the dictionary's value type uniform across every event
type while still type-safe at the public API boundary, since `Subscribe`/
`Unsubscribe`/`PublishAsync` are all themselves generic and only ever cast
back to the exact `IEventHandler<TEvent>` the caller already guaranteed by
the type parameter.

## 8. Architectural Principles

- **Reuse Before Invention** — dispatch ordering and the cancellation
  boundary reuse `ModuleLifecycleManager.RunBatchAsync`'s own established
  shape directly; the lock pattern reuses `RuntimeModuleManager`'s own
  `_gate` convention; registration reuses
  `ServiceCollection.Singleton<TService, TImplementation>()`, unchanged
  since `WP 2.4`.
- **Minimal Host Complexity** — confirmed, not merely claimed: `git diff`
  against `TempestHost.cs` is exactly one added line plus one added
  `using` directive; `RuntimeModuleManager.cs`, `ModuleLifecycleManager.cs`,
  `ReflectionFrameworkDiscoveryService.cs`, `TempestServiceProvider.cs`,
  and `ClockModule.cs` are byte-for-byte unchanged.
- **One Responsibility Per Service** — `IEventBus` carries messages only;
  it registers, initialises, starts, stops, and disposes nothing.
- **Constructor Injection Through Normal DI Patterns** — `EventBus`'s own
  constructor takes only an optional `ILogger?`, resolved exactly like
  every other platform service; no service locator, no static access
  anywhere in the implementation or its tests.

## 9. Benefits

- **Every dispatch, ordering, failure, and re-entrancy guarantee ADR-0028
  named is now proven, not merely designed** — 24 tests exercise
  `EventBus` directly, including an in-flight-concurrency counter proving
  sequential dispatch, and an explicit enter/exit ordering assertion
  proving nested re-entrant dispatch completes before the outer loop
  resumes.
- **Zero new Dependency Injection capability was needed, confirmed rather
  than merely predicted** — `services.Singleton<IEventBus, EventBus>()`
  resolves correctly through the real, unmodified
  `TempestServiceProvider`.
- **The bus is now ready for a consumer** — `ClockModule`'s own extension
  (`WP 4.4C`'s original objective) can proceed as its own, separate work
  package against a fully validated, real implementation, exactly as
  `WP 4.4B` unblocked constructor injection before this work package
  needed it.

## 10. Trade-offs

- No automatic unsubscription on module stop/dispose, and subscriber
  references held strongly for the bus's whole lifetime — both are
  ADR-0028's own named, accepted gaps, not new debt introduced by this
  implementation.
- Exact-event-type-only dispatch (no polymorphism) — unchanged from the
  design; no current event type needs it.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: testing "the bus
dispatches sequentially" only by asserting call order, which a
concurrent-but-coincidentally-ordered implementation could also satisfy.
Proving it instead with an in-flight counter that fails if more than one
handler is ever executing at once closes that gap — order alone does not
prove sequencing, concurrency measurement does.

## 12. Future Evolution

- **`ClockModule`'s own extension** (`WP 4.4C`'s original objective) is
  now fully unblocked and should proceed as its own, separate work
  package: declare `[ModuleMetadata]`, accept `IEventBus` via constructor
  injection, and publish from its lifecycle methods.
- **A companion module** subscribing to whatever `ClockModule` publishes
  remains `WP 4.4`'s own Deliverable to add, not this work package's
  concern.
- **Automatic unsubscription and a critical-subscriber opt-in** both
  remain available, purely additively, per RD-0022 and ADR-0028's own
  Future Considerations, if a real need for either ever emerges.

## 13. Key Takeaways

1. Implementing an already-fully-designed ADR closely is a narrow,
   low-risk exercise precisely because the hard questions were already
   answered — this work package's only real judgment call was an internal
   storage-type choice (`List<object>` per event type), not a new design
   decision.
2. Proving "dispatches sequentially" requires measuring concurrency
   directly (an in-flight counter), not merely asserting call order —
   order alone is consistent with either a sequential or a coincidentally-
   ordered concurrent implementation.
3. A work package whose own brief names three overlapping exclusions
   (no `ClockModule`, no event publishing, no sample module integration)
   is naming one boundary three times for emphasis, not three separate
   ones — recognising that kept this implementation's own test surface
   correctly scoped to `EventBus` alone.

---

## Architectural Debt Assessment

**No new debt introduced.** The two named gaps (no automatic
unsubscription; strong subscriber references) are ADR-0028's own accepted
trade-offs, disclosed at design time, not new debt discovered here. Every
other debt item on record from the Runtime Foundation, WP 4.0–4.4B, and
WP 4.2D remains exactly as previously described.

## Observations

- **Files added**: `src/Tempest.Core/Events/IEventBus.cs`;
  `src/Tempest.Core/Events/EventBus.cs`;
  `tests/Tempest.Core.Tests/Events/EventBusFixtures.cs`;
  `tests/Tempest.Core.Tests/Events/EventBusTests.cs`;
  `tests/Tempest.Core.Tests/Events/RecordingLevelLogger.cs`.
- **Files modified**: `src/Tempest.Core/Runtime/TempestHost.cs` only — one
  new `using Tempest.Core.Events;` directive and one new line,
  `services.Singleton<IEventBus, EventBus>();`, in the existing Platform
  Services Registered block. **Zero other production file was modified** —
  confirmed directly: `git diff --stat` against
  `RuntimeModuleManager.cs`, `ModuleLifecycleManager.cs`,
  `ReflectionFrameworkDiscoveryService.cs`, `TempestServiceProvider.cs`,
  and `ClockModule.cs` is empty.
- **Tests added**: 24 — subscribe then publish (1); unsubscribe stops
  delivery without affecting siblings (1); unsubscribe of a
  never-subscribed handler is a no-op (1); null-argument guards on
  `Subscribe`/`Unsubscribe`/`PublishAsync` (3); subscription-order
  dispatch (1); sequential dispatch proven via an in-flight-concurrency
  counter (1); deterministic ordering across five repeated publishes (1);
  a subscriber added during dispatch excluded from the in-flight publish
  (1) and included in the next (1); a subscriber removed during dispatch
  still receiving the in-flight publish (1) and excluded from the next
  (1); re-entrant publish of a different event type (1) and of the same
  event type with explicit nested enter/exit ordering (1); a throwing
  subscriber not preventing siblings (1); a subscriber exception never
  reaching the publisher (1); an isolated failure logged at `Error` (1);
  no `Error` log when nothing throws (1); cancellation propagating
  uncaught between subscribers without invoking the remainder (1);
  publish with zero subscribers is a no-op (1); dispatch limited to the
  exact event type (1); `services.Singleton<IEventBus, EventBus>()`
  resolving to `EventBus` (1) and to the same singleton instance on
  repeated resolution (1).
- **Test results**: 302 of 302 passing (278 pre-existing + 24 new), 0
  failures.
- **Build results**: 0 warnings, 0 errors.
- **Platform changes outside `Tempest.Core.Events` and the one
  registration line**: none. `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `ReflectionFrameworkDiscoveryService`,
  `TempestServiceProvider`, `Host Lifecycle.md`'s phase table, and
  `ClockModule` are byte-for-byte unchanged.
- **Readiness assessment**: WP 4.4D is complete. ADR-0028 is fully
  realised and proven against the real implementation. The Event Bus is
  ready for a consumer — `ClockModule`'s own extension (`WP 4.4C`'s
  original objective) may now begin as its own, separate work package.

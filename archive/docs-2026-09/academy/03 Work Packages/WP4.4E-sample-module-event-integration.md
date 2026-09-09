# WP 4.4E — Sample Module Event Integration

## 1. Introduction

WP 4.4E extends `ClockModule` to consume the now-complete Event Bus
(`WP 4.4D`) using ordinary constructor injection, and adds a new companion
module, `ClockLifecycleObserverModule`, that subscribes to what it
publishes. This is the original `WP 4.4C` objective — abandoned mid-
investigation when it discovered no Event Bus existed to consume — now
completed for real, against a real, tested implementation, with a real
second module proving the pipeline end-to-end for the first time.

## 2. Purpose

To demonstrate that a real module can consume a Platform Service exactly
as `ADR-0020`/`ADR-0027`/`ADR-0028` intended: constructor-injecting a
DI-public service into a discovered module carrying `[ModuleMetadata]`,
publishing real lifecycle events through it, and having a second, wholly
independent module receive them — with no direct reference between the
two modules anywhere in the proof, and with zero change to any Platform
Service.

## 3. Background

By the time WP 4.4E began, every prerequisite this chain ever named was
resolved: `ModuleMetadataAttribute` let a discovered module declare a
DI-resolvable constructor (`WP 4.4A`/`4.4B`, ADR-0027); `IEventBus` existed,
was implemented exactly per its own design, and was proven against 24
dedicated unit tests (`WP 4.4D`, ADR-0028); `ClockModule` itself remained
completely untouched through every one of those prior steps, exactly as
each of their own briefs promised. This work package's own brief was
explicit about the boundary: extend `ClockModule` and add its companion;
touch no Platform Service; if implementation appears to require modifying
one, stop and report rather than redesign the platform. It also required a
full Academy review before any code was written — see Section 5.

## 4. The Problem

1. **Extend `ClockModule` to declare `[ModuleMetadata]` and
   constructor-inject `IEventBus`**, publishing from `InitialiseAsync`/
   `StartAsync`/`StopAsync`, without touching `TempestHost`,
   `ReflectionFrameworkDiscoveryService`, `RuntimeModuleManager`,
   `ModuleLifecycleManager`, `TempestServiceProvider`, `EventBus`, or
   `IEventBus` itself.
2. **Add a companion module that subscribes**, holding no reference of any
   kind to `ClockModule` — only to a shared event data type — proving
   `ADR-0020`'s governing shape (`Module → IEventBus → Runtime`, never
   `Module A → Module B`) directly, not merely by design intent.
3. **Perform a complete Academy review before writing any production
   code** — verify every WP 4.x work package has appropriate Academy
   coverage, every Academy article reflects implemented rather than
   planned behaviour, and every cross-reference resolves — fixing anything
   found missing, stale, or broken as part of this work package's own
   Definition of Done.
4. **Prove it comprehensively**, including through the real, unmodified
   `TempestHost`, with the real `EventBus` — no mocks except where needed
   to observe logging.

## 5. The Design

**Academy review, performed first, per the brief's own explicit
requirement.** Every completed `WP 4.x` architecture and implementation
work package was checked against its own Academy retrospective, and every
retrospective's own claims were checked against the current, actual
repository state (not re-trusted from a prior reading). One genuine gap
was found: `WP 4.2D` (*Platform Services Architecture Review*) had no
Academy retrospective of its own, unlike its direct structural precedent,
`WP 2.7` (*Runtime Host Architecture Review*), which does. Every other
`WP 4.x` retrospective — `WP 4.0` through `WP 4.4D`, including the four
`WP 4.2` sub-work-packages and `WP 4.4A`/`4.4B` — was found accurate,
current, and correctly cross-referenced; no stale "not yet implemented"
language was found describing anything now implemented. **Fixed**: a new
`WP4.2D-platform-services-architecture-review.md` retrospective was
written, mirroring `WP 2.7`'s own structure, before any production code
for this work package was touched.

**Implementation.** `ClockModule` gained `[ModuleMetadata("tempest.
samples.clock", "System Clock", "1.0.0")]` and a constructor requiring
`IEventBus`, replacing its zero-argument constructor — the exact,
already-proven shape `WP 4.4B`'s own test fixtures established, now
exercised by the real sample module for the first time. Each lifecycle
method now publishes a new `ClockModuleLifecycleEvent`
(`Tempest.Samples`, implementing `WP 4.0`'s `IEvent`) carrying the
module's own `Id`/`Name`, the transition (`Initialised`/`Started`/
`Stopped`), a timestamp, and a per-instance correlation identifier shared
across all three events a given `ClockModule` instance publishes.
`ClockLifecycleObserverModule` — a new, second, SDK-conformant module,
also carrying `[ModuleMetadata]` and constructor-injecting `IEventBus` —
subscribes to `ClockModuleLifecycleEvent` during its own
`InitialiseAsync`, recording every one it observes and logging it via an
optional `ILogger?`. It holds no reference of any kind to `ClockModule`
itself — only to the shared `ClockModuleLifecycleEvent` type, exactly the
shape ADR-0020 requires.

**A genuine finding, not anticipated by any prior design document.**
`ModuleLifecycleManager` initialises modules in ascending-Id order and
stops them in descending order. `ClockModule`'s Id
("tempest.samples.clock") sorts before its companion's
("tempest.samples.clock.observer"), so `ClockModule` publishes its own
`Initialised` event and completes *before* the companion's own
`InitialiseAsync` — where it subscribes — even runs. The companion never
observes that one event. It reliably observes `Started` and `Stopped`,
because Module Initialisation completes for every module, including the
companion, before Module Start begins for any module, regardless of Id
order. This is a real, load-bearing consequence of the module pipeline's
own batch-per-phase shape (`ModuleLifecycleManager.RunBatchAsync`,
unchanged since `WP 2.3`) — not a defect in the Event Bus, `ClockModule`,
or the companion. It was found by writing the tests, not assumed, and is
proven directly (`ClockModuleEventIntegrationTests.
FullLifecycle_CompanionObserverModule_ReceivesStartedAndStopped_
ButNotInitialised`), including through the real, unmodified `TempestHost`
via captured console output.

## 6. Alternatives Considered

**Having the companion unsubscribe in its own `StopAsync`**, the
idiomatic-looking symmetric counterpart to subscribing in
`InitialiseAsync`. Considered, then rejected once the stop-order
consequence was traced: `ModuleLifecycleManager.StopAllAsync` stops
modules in descending order — the reverse of Initialise — so the
companion (initialised after `ClockModule`) would stop, and unsubscribe,
*before* `ClockModule` reaches its own `StopAsync`, missing the `Stopped`
event entirely. Not unsubscribing is simpler, avoids this exact hazard,
and is explicitly the trade-off ADR-0028 already named and accepted
(subscriber references held strongly for the bus's whole lifetime) — not
a new one introduced here.

**Choosing module Ids specifically to force the companion to subscribe
before `ClockModule` publishes `Initialised`.** Considered, and rejected
as dishonest engineering: gaming an identifier to paper over a real
platform behaviour would hide, rather than document, a genuine finding
about how the module pipeline's batch-per-phase ordering interacts with
Event Bus subscription timing — exactly the kind of quietly-patched
contradiction `FOUNDATION.md` names as something this project does not do.
The natural Ids were kept; the real behaviour was tested and documented
instead.

**A single correlation identifier per publish call, rather than one per
module instance shared across all three lifecycle events.** Considered.
Rejected: a fresh identifier per event would not let a subscriber
correlate the full Initialised → Started → Stopped sequence from one
`ClockModule` run, which is the entire reason to carry a correlation
identifier at all — a per-instance identifier, generated once in the
constructor, was the only choice that actually serves that purpose.

## 7. Why This Solution Was Chosen

Every decision here is a direct, unmodified application of an
already-resolved design: ADR-0027's attribute-based construction shape,
already proven against dedicated test fixtures by `WP 4.4B`; ADR-0028's
imperative subscribe/publish shape and its own accepted no-automatic-
unsubscribe trade-off; ADR-0020's prohibition on direct module-to-module
references. Nothing here required a new architectural judgment call
beyond the two named in Alternatives Considered, both of which were
resolved by tracing actual platform behaviour precisely rather than
assuming it.

## 8. Architectural Principles

- **Constructor Injection Through Normal DI Patterns** — the entire point
  of this work package: `ClockModule` and its companion each request
  `IEventBus` via an ordinary constructor, resolved by the real,
  unmodified `TempestServiceProvider`.
- **Reuse Before Invention** — the attribute-based construction shape
  reuses `WP 4.4B`'s own proven mechanism exactly; the companion's
  subscription reuses `IEventBus.Subscribe` exactly as ADR-0028 designed
  it; nothing new was invented at the platform level.
- **Downward Dependency Direction** (ADR-0023) — `ClockModule` and its
  companion each depend on `Tempest.Core` (`IEventBus`, `IEvent`,
  `ModuleLifecycleBase`); neither depends on the other, and nothing in
  `Tempest.Core` depends on either.
- **Document a Found Contradiction Honestly** (`FOUNDATION.md`) — the
  Initialise-phase ordering finding was tested, documented, and explained,
  not hidden behind a convenient identifier choice.

## 9. Benefits

- **The Event Bus's first real consumer now exists**, proving
  `ADR-0020`/`ADR-0027`/`ADR-0028` work together correctly in production
  code, not only in isolated unit tests against synthetic fixtures.
- **A genuine, previously undiscovered interaction between two already-
  shipped mechanisms** (module batch-per-phase ordering; Event Bus
  subscription timing) was found and documented precisely, before it could
  surprise a future module author relying on both.
- **Zero Platform Service was touched** — confirmed directly:
  `git diff --stat` against every file under `src/Tempest.Core/` is empty.
- **The Academy review this work package's own brief required surfaced and
  fixed a real, if narrow, gap** (`WP 4.2D`'s missing retrospective)
  entirely independent of the sample-module work itself.

## 10. Trade-offs

- The companion module does not unsubscribe, ever — an explicit,
  documented choice (Alternatives Considered), not an oversight, and
  exactly ADR-0028's own named, accepted gap.
- The companion does not observe every lifecycle transition `ClockModule`
  publishes — a real, disclosed limitation of this particular module
  pairing's own Id ordering, not a limitation of the Event Bus itself. A
  future module pairing that needs to observe every transition from
  construction onward would need to subscribe earlier in the pipeline than
  `InitialiseAsync` allows for a later-initialised module — not a gap this
  work package attempts to close, since no current consumer needs it.

## 11. Common Mistakes

The mistake most worth naming here is one avoided, not one that happened:
when the Initialise-phase ordering finding surfaced, the tempting fix was
either to rename the companion module so it would sort first, or to make
`ClockModule` publish `Initialised` from `StartAsync` instead — both would
have hidden a real, generally-applicable finding about how this pipeline's
existing ordering guarantees interact with the Event Bus, for the sake of
one test passing more conveniently. Testing and documenting the real
behaviour instead is what actually captures information module authors
need going forward — see the new *Building an Event-Driven Module* Academy
article's own "Lessons Learned" section.

## 12. Future Evolution

- **`WP 4.5` (Background Services)** and **`WP 4.7` (Command Framework)**
  can now find a real, working example of Event Bus consumption to model
  their own sample-module extensions against, rather than reasoning from
  the Event Bus's own design documents alone.
- **A future module pairing** that needs to observe every lifecycle event
  from construction onward, regardless of Id-ordering, would need its own
  explicit design — not built here, since no current consumer needs it.
- **Automatic unsubscription and a critical-subscriber opt-in** both
  remain available, purely additively, per ADR-0028's own Future
  Considerations, if a real need for either ever emerges from a future
  module.

## 13. Key Takeaways

1. A pairing of two already-correct, independently-tested mechanisms
   (batch-per-phase module ordering; imperative Event Bus subscription)
   can still produce a real, non-obvious interaction the first time they
   are actually used together — finding this required building a genuine
   second consumer, not merely re-reading either mechanism's own design
   document.
2. When a real platform behaviour is inconvenient for a test, the correct
   response is to test and document the real behaviour, not to choose
   identifiers or timing that quietly avoid exercising it — this project's
   own "document a contradiction honestly" discipline (`FOUNDATION.md`)
   applies exactly here.
3. A mandatory Academy review, run before implementation as this work
   package's own brief required, is not merely a compliance step —
   it found and closed a real, if narrow, gap (`WP 4.2D`'s missing
   retrospective) that a review scoped only to the sample-module work
   itself would never have surfaced.

---

## Architectural Debt Assessment

**No new debt introduced.** The Initialise-phase ordering behaviour this
work package found and documented is a pre-existing, correct property of
`ModuleLifecycleManager`'s own batch-per-phase shape (unchanged since
`WP 2.3`) and `IEventBus`'s own subscription timing (unchanged since
`WP 4.4D`) — newly discovered and disclosed, not newly created. Every
other debt item on record from the Runtime Foundation, `WP 4.0`–`WP 4.4D`,
and `WP 4.2D` remains exactly as previously described.

## Observations

- **Files added**: `src/Samples/Tempest.Samples/ClockModuleLifecycleEvent.cs`;
  `src/Samples/Tempest.Samples/ClockLifecycleObserverModule.cs`;
  `tests/Tempest.Core.Tests/Samples/ClockModuleEventIntegrationTests.cs`;
  `docs/academy/03 Work Packages/WP4.2D-platform-services-architecture-
  review.md` (Academy review remediation); `docs/academy/02 Runtime
  Architecture/04-building-an-event-driven-module.md`; this retrospective.
- **Files modified**: `src/Samples/Tempest.Samples/ClockModule.cs`
  (`[ModuleMetadata]`, constructor injection of `IEventBus`, publishing
  from all three lifecycle methods); `tests/Tempest.Core.Tests/Samples/
  ClockModuleTests.cs`, `ClockModuleDiscoveryTests.cs`,
  `ClockModulePipelineTests.cs` (updated for the new constructor and the
  companion module now sharing the same compiled assembly); `Sample Module
  Architecture.md`; `Building a Module.md`; `Event Bus Architecture.md`;
  `Platform Service Map.md`; `Engineering Glossary.md`; `WorkPackages.md`;
  `CHANGELOG.md`. **Zero files under `src/Tempest.Core/` were modified** —
  confirmed directly: `git diff --stat -- src/Tempest.Core/` is empty.
- **Tests added**: 8 new, dedicated integration tests
  (`ClockModuleEventIntegrationTests.cs`): constructor injection resolves
  a functioning `IEventBus`; full-lifecycle publish in order with correct
  payloads and a shared correlation identifier; multiple subscribers all
  receive every event; the companion observes `Started`/`Stopped` but not
  `Initialised` (the ordering finding, proven directly); discovery and
  registration of both modules with correct metadata; end-to-end delivery
  through the real, unmodified `TempestHost` (proven via captured console
  output); repeated execution across fresh pipeline instances and fresh
  `TempestHost` instances, both deterministic. A further 2 tests were
  added and 3 existing tests were adjusted (not newly written, but
  necessarily changed in scope) across `ClockModuleTests.cs`/
  `ClockModuleDiscoveryTests.cs` to account for the new constructor
  signature and the companion module now sharing `Tempest.Samples`'s
  compiled assembly.
- **Test results**: 313 of 313 passing (302 pre-existing + 11 new/changed
  net), 0 failures.
- **Build results**: 0 warnings, 0 errors.
- **Academy review findings**: one gap found and fixed (`WP 4.2D`'s
  missing retrospective); zero stale Academy articles found describing
  planned rather than implemented behaviour; zero broken cross-references
  found.
- **Platform Services changed**: none. `TempestHost`, `EventBus`,
  `IEventBus`, `ReflectionFrameworkDiscoveryService`, `RuntimeModuleManager`,
  `ModuleLifecycleManager`, and `TempestServiceProvider` are byte-for-byte
  unchanged — confirmed directly via `git diff --stat`.
- **Readiness assessment**: WP 4.4E is complete. The Event Bus now has a
  real, proven consumer and a real, proven subscriber, built with zero
  Platform Service change. `WP 4.4C`'s original objective is fully
  realised. `WP 4.5` may now proceed.

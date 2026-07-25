# TempestOS v0.4.0 — Changelog

## Status

**Unreleased.** Implementation is underway. Entries below are added as
each work package actually lands — not written in advance as predictions.
Compare against `WorkPackages.md` for what is planned but not yet
reflected here.

---

## [Unreleased]

### Added

- **Platform Contracts (WP 4.0)** — `IModule` (re-affirmed), `IHostedService`,
  `ICriticalBackgroundService`, `ICommand`, `IEvent`, `IEventHandler<T>`.
  `INavigationProvider`/`IDiagnosticsProvider` deliberately not defined yet.
- **Module SDK (WP 4.1)** — `ModuleBase`, `ModuleLifecycleBase`
  (`Tempest.Core.Modules`).
- **Plugin Manifest architecture (WP 4.2)** — design only; no code.
  `Plugin Manifest Architecture.md`.
- **Runtime Platform Version Infrastructure (WP 4.2A)** —
  `IPlatformVersionProvider`, `PlatformVersionProvider`, `PlatformVersion`
  (`Tempest.Core.Versioning`); `Directory.Build.props` now derives
  `<Version>` from the repository's own `VERSION` file.
- **Plugin Failure Classification (WP 4.2B)** — architecture only; no
  code. ADR-0025.
- **Plugin Discovery Lifecycle Placement (WP 4.2C)** — architecture only;
  no code. ADR-0026. `Host Lifecycle.md` gains two new phases (`3.1 Plugin
  Discovery`, `3.2 Plugin Loading`), inserted between Logging Built and
  Module Discovery without renumbering the existing thirteen phases. This
  was the last remaining prerequisite before `WP 4.2` implementation —
  none remain.
- **Plugin Manifest implementation (WP 4.2)** — `PluginManifest`,
  `PluginException` and five subtypes, `IPluginManifestDiscoveryService`/
  `PluginManifestDiscoveryService`, `IPluginAssemblyLoader`/
  `PluginAssemblyLoader` (`Tempest.Core.Plugins`). `TempestHost` now runs
  Plugin Discovery (Phase 3.1) and Plugin Loading (Phase 3.2) between
  Logging Built and Module Discovery, exactly per ADR-0026; Module
  Discovery, Registration, and Lifecycle are unchanged. 27 new tests.

- **Sample Module architecture (WP 4.3)** — design only; no code.
  `Sample Module Architecture.md`. Found and named a real tension between
  Discovery's zero-argument metadata probe and constructor-injecting a
  DI-public service (`IEventBus`) into a module — an ADR `WP 4.4` should
  resolve as its own first step. Recorded RD-0015 (deferring Plugin
  Manifest packaging of the sample module).
- **Sample Module implementation (WP 4.3)** — `ClockModule`
  (`Tempest.Samples`, new `src/Samples/` project), the living reference
  `WP 4.4`–`WP 4.7` extend and validate against. Proven, with 18 new
  tests, to travel through the complete, real, unmodified Platform
  Services pipeline (Discovery, Registration, Dependency Injection,
  Lifecycle) with no special-casing. No platform file was changed —
  `ReflectionFrameworkDiscoveryService`, `RuntimeModuleManager`,
  `ModuleLifecycleManager`, and `TempestHost` are byte-for-byte unchanged.
- **Dependency Injection for Discovered Modules (WP 4.4A)** — design only;
  no code. ADR-0027, `Module Dependency Injection Architecture.md`.
  Resolves the tension `WP 4.3` identified: an optional, additive
  `ModuleMetadataAttribute` lets a module declare its metadata without
  being instantiated by Discovery, freeing it to declare a DI-resolvable
  constructor — every module without the attribute keeps today's exact
  behaviour, unchanged.
- **ADR-0027 implementation (WP 4.4B)** — `ModuleMetadataAttribute`
  (`Tempest.Core.Modules`); `ReflectionFrameworkDiscoveryService` reads it
  when present, skipping instantiation entirely, and falls back to its
  original, unchanged behaviour when absent. Proven, with 18 new tests, at
  three levels: Discovery alone, the real composed Discovery → Registration
  → DI → Lifecycle pipeline, and the real, unmodified `TempestHost`
  constructor-injecting a genuine platform service (`ILogger`). No other
  production file was touched — `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `TempestHost`, `TempestServiceProvider`, and
  `ClockModule` are byte-for-byte unchanged. This was the last remaining
  prerequisite before `WP 4.4` — none remain.
- **Event Bus architecture (WP 4.4)** — design only; no code. ADR-0028,
  `Event Bus Architecture.md`. A task assuming `IEventBus` already existed
  (`WP 4.4C`, extending `ClockModule` to publish through it) was
  investigated against the actual repository first and found no
  `IEventBus` anywhere; this architecture phase was authorised instead of
  building one under time pressure. Designs `IEventBus`/`EventBus` in
  full: imperative `Subscribe`/`Unsubscribe`; sequential `PublishAsync`
  dispatch in subscription order over a per-call snapshot, making
  re-entrant publish safe without a deferred queue; unconditional
  per-subscriber failure isolation with no critical opt-in; registration
  as an ordinary container-constructed singleton, requiring no new DI
  capability. Recorded RD-0019 (DI-auto-discovered handlers), RD-0020
  (deferred/queued re-entrant publishing), RD-0021 (polymorphic event
  dispatch), RD-0022 (a per-subscriber critical opt-in). `ClockModule`
  remains completely untouched; its extension follows only after `WP 4.4`
  implementation.
- **Event Bus implementation (WP 4.4D)** — `IEventBus`, `EventBus`
  (`Tempest.Core.Events`), implemented exactly per ADR-0028. Imperative
  `Subscribe`/`Unsubscribe`; `PublishAsync` dispatches sequentially, in
  subscription order, over an immutable per-call snapshot; every
  subscriber exception is caught, logged at `Error`, and never rethrown;
  cancellation is checked between subscribers and propagates uncaught.
  `TempestHost` now registers `EventBus` as an ordinary singleton
  (`services.Singleton<IEventBus, EventBus>()`) in its existing Platform
  Services Registered block — one new line; no other production file
  changed. 24 new tests, exercising `EventBus` directly: subscribe/
  unsubscribe, subscription-ordered sequential dispatch (proven via an
  in-flight-concurrency counter), snapshot semantics under mid-dispatch
  addition/removal, re-entrant publishing (same and different event
  type), exception isolation and `Error`-level logging, cancellation
  propagation, and the DI registration itself. `ClockModule` remains
  completely untouched — no event publishing, no sample module
  integration.
- **Sample Module Event Integration (WP 4.4E)** — `ClockModule`
  (`Tempest.Samples`) now carries `[ModuleMetadata]` and
  constructor-injects `IEventBus`, publishing a `ClockModuleLifecycleEvent`
  from each of `InitialiseAsync`/`StartAsync`/`StopAsync` — `WP 4.4C`'s
  original objective, completed against a real, tested Event Bus. A new
  companion module, `ClockLifecycleObserverModule`, subscribes, holding no
  reference to `ClockModule` itself (ADR-0020). 8 new dedicated tests
  (`ClockModuleEventIntegrationTests.cs`) prove constructor injection,
  lifecycle event publication/ordering, multiple subscribers, event
  payload correctness, discovery of both modules, end-to-end delivery
  through the real, unmodified `TempestHost`, and deterministic repeated
  execution; 3 pre-existing sample-module test files updated for the new
  constructor signature and the companion module now sharing
  `Tempest.Samples`'s compiled assembly. A genuine finding — the
  companion does not observe `ClockModule`'s own `Initialised` event,
  because `ClockModule`'s Id sorts first in `ModuleLifecycleManager`'s
  ascending-order Initialise batch — was tested and documented, not
  engineered around. Zero Platform Service file was touched. A mandatory
  pre-implementation Academy review found and fixed one gap: a new
  `WP4.2D-platform-services-architecture-review.md` retrospective, and a
  new Academy article, *Building an Event-Driven Module*.
- **Academy & Documentation Baseline Audit (WP 4.4F)** — documentation
  only; no production code touched. A complete audit of every document
  produced across the Claude-developed history of TempestOS, determined
  directly from git history (first Claude-authored commit: `7514b9d`)
  rather than assumed. Found and fixed six genuine staleness findings
  (Engineering Standards, Engineering Governance, `ReleasePlan.md`,
  `Host Lifecycle.md`, `Ownership Matrix.md`, all understating how far the
  project had actually progressed) and wrote five new concept guides:
  *Working with the TempestOS Host*, *Platform Layering*, *Plugin
  Architecture*, *Failure Isolation Across TempestOS*, and
  *Reflection-Based Discovery*. `Academy Index.md`, `Academy Masterclass
  Roadmap.md`, and `Academy Audit Report.md` are all new. No missing
  retrospective, missing architecture document, or uncovered significant
  ADR was found.
- **Background Services Design (WP 4.5, architecture phase)** — design
  only; no code. ADR-0029, *Background Service Discovery, Ownership, and
  Orchestration Model*, and ADR-0030, *Background Service Host Lifecycle
  Placement*, together with `Background Services Architecture.md`, design
  the whole subsystem: a fourth, Host-owned runtime category (neither a
  Platform Service nor a Module); a new, dedicated
  `IHostedServiceDiscoveryService` that never instantiates a candidate,
  since `IHostedService` carries no metadata to read (no
  `ADR-0027`-style prerequisite ever arises); registration folded into the
  existing Platform Services Registered phase, requiring no new DI
  capability; a new, Host-owned `IHostedServiceManager` starting and
  stopping every discovered service sequentially, in deterministic order
  (reversed for stop), realising ADR-0021's isolated/critical failure
  model exactly; two new, decimal-numbered Host Lifecycle phases (`8.1`
  Hosted Services Started, `10.1` Hosted Services Stopped), following
  ADR-0026's own precedent. Recorded RD-0023 (DI multi-registration
  resolution), RD-0024 (a dedicated descriptor type), RD-0025 (extending
  Module Discovery itself), RD-0026 (active Host-level monitoring),
  RD-0027 (a new discovery/registration phase), RD-0028 (concurrent
  service start), RD-0029 (automatic restart/backoff). No implementation
  exists yet; `WP 4.5`'s own implementation may now begin.

_Still planned, per `WorkPackages.md`:_

- Background Services implementation (WP 4.5)
- Navigation Architecture (WP 4.6A), then Navigation Implementation
  (WP 4.6B)
- Command Framework (WP 4.7)
- Diagnostics Improvements (WP 4.8)
- Developer Experience Improvements (WP 4.9)

### Changed

- **Platform Services Architecture Review (WP 4.2D)** — a formal review
  and hardening pass over the whole `WP 4.0`–`WP 4.2` milestone before
  `WP 4.3` began. No functionality changed; nine stale documentation
  cross-references were found and corrected (`FOUNDATION.md`'s ADR count,
  two stale "nothing has begun" status lines, the Engineering Glossary's
  Plugin entry and its missing Plugin Manifest entry, and structural gaps
  in the Platform Service Map). See `Platform Services Architecture
  Review.md`.

### Fixed

- A flaky test in WP 4.1's own test suite (`SdkLifecycleLog` shared static
  state across two xUnit classes that could run concurrently) — found and
  fixed during routine validation before WP 4.2 began.
- Two test-only regressions found during WP 4.2's own implementation, both
  fixed without touching production code: a cross-test dynamic assembly
  identity collision (two test methods' dynamically-built plugin
  assemblies shared a simple name, so `Assembly.LoadFrom` silently
  resolved to whichever loaded first — fixed via GUID-suffixed assembly
  identities); and the same `Console.Out`-redirection race pattern
  already fixed once for `SdkLifecycleLog`, recurring between
  `TempestHostTests` and the new `TempestHostPluginLifecycleTests` — fixed
  via a shared `[Collection("Console output capture")]`.

### Architecture Decision Records

- **ADR-0020** — The Event Bus Is a DI-Public Platform Service. Decided
  during planning, before implementation (WP 4.0/4.4).
- **ADR-0021** — Background Service Failures Are Isolated by Default;
  Criticality Is Opt-In. Decided during planning, before implementation
  (WP 4.0/4.5).
- **ADR-0022** — Navigation and Commands Are Orthogonal Platform Services.
  Decided during planning, before implementation (WP 4.0/4.6A/4.7).
- **ADR-0023** — Platform Layering: Dependencies Flow Downward Only.
  Decided during planning; applies platform-wide, not only to this
  release (see `docs/releases/FOUNDATION.md`).
- **ADR-0024** — Platform Contracts Are Packaged by Capability, Not a
  Shared Contracts Namespace. Decided during WP 4.0.
- **ADR-0025** — Plugin Failure Classification. Decided during WP 4.2B —
  isolated for every plugin-loading failure category except a genuine
  Host-level defect in the loading orchestration itself.
- **ADR-0026** — Plugin Discovery Lifecycle Placement. Decided during
  WP 4.2C — two new decimal-numbered phases (`3.1 Plugin Discovery`,
  `3.2 Plugin Loading`) inserted between Logging Built and Module
  Discovery; `PlatformVersionProvider` construction moves earlier (its DI
  registration does not); candidate folders sorted ordinally by name for
  deterministic duplicate-identity resolution. This was the last
  architectural blocker before `WP 4.2` implementation.
- **ADR-0027** — A Declarative `ModuleMetadataAttribute` Decouples
  Discovery From Construction. Decided during WP 4.4A — an optional,
  class-level attribute lets Discovery read a module's `Id`/`Name`/
  `Version` without instantiating it, so such a module may declare a
  DI-resolvable constructor; every module without the attribute is
  completely unaffected. This was the last architectural blocker before
  `WP 4.4` implementation.
- **ADR-0028** — Event Bus Dispatch, Subscription, and Failure Model.
  Decided during `WP 4.4`'s own architecture phase — imperative
  subscription, sequential snapshot-based dispatch in subscription order,
  unconditional per-subscriber failure isolation with no critical opt-in,
  and registration as an ordinary container-constructed singleton needing
  no new DI capability. Fully realised by `WP 4.4D` — this was the last
  architectural blocker before `WP 4.4` implementation.
- **ADR-0029** — Background Service Discovery, Ownership, and
  Orchestration Model. Decided during `WP 4.5`'s own architecture phase —
  a fourth, Host-owned runtime category; discovery via a new, dedicated
  service that never instantiates a candidate; registration folded into
  the existing Platform Services Registered phase; a new, Host-owned
  manager starting/stopping every discovered service sequentially,
  realising ADR-0021's failure model exactly. Not yet implemented.
- **ADR-0030** — Background Service Host Lifecycle Placement. Decided
  during `WP 4.5`'s own architecture phase — two new decimal-numbered
  phases (`8.1 Hosted Services Started`, `10.1 Hosted Services Stopped`)
  inserted between Module Initialisation/Runtime Running and Shutdown
  Requested/Module Disposal respectively, following ADR-0026's own
  precedent. This was the last architectural blocker before `WP 4.5`
  implementation.
- Expected, not yet written: Navigation's `Tempest.Core` placement
  (`WP 4.6A`) — see `Architecture.md`. No further ADR is expected before
  `WP 4.5` implementation.

---

## How This File Is Maintained

Each work package adds its own entries here as part of its own Definition
of Done (`ReleaseChecklist.md`), under the correct `Added`/`Changed`/
`Fixed` heading, referencing its work package number (e.g. "WP 4.4 — Event
Bus: added `IEventBus` with per-subscriber failure isolation."). This file
is not written retroactively at release time from memory — it is a running
record, exactly like `Risks.md`.

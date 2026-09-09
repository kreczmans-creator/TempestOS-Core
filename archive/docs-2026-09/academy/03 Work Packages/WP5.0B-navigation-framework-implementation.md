# WP 5.0B — Navigation Framework Implementation

## 1. Introduction

WP 5.0B implements `NavigationItem`, `INavigationProvider`/`NavigationService`,
`NavigationRequestedEvent`, and the `NavigationException` hierarchy exactly
as `ADR-0031`, `ADR-0032`, and `Navigation Framework Architecture.md`
designed them — with zero deviation from the approved Public Surface.
Unlike the architecture-only phase immediately before it (`WP 5.0A`), this
work package produces real, tested production code — the first
`Tempest.Core.Navigation` types, and the second new registration line
`TempestHost.cs` has needed since the Event Bus's own (`WP 4.4D`).

## 2. Purpose

To realise ADR-0031/ADR-0032's decisions precisely, prove them against
real modules constructor-injecting the real `INavigationProvider` and
registering real `NavigationItem`s through the real, unmodified module
pipeline, and demonstrate — not merely argue — that the Runtime Host, the
existing module lifecycle, and `Tempest.App` are all unaffected beyond the
one approved registration line.

## 3. Background

`WP 5.0A`'s own architecture phase answered every open representation,
ownership, registration, notification, and rendering-boundary question in
writing, before any code was written. With the design settled and
accepted, this work package's own brief was explicit about its boundary:
implement the approved architecture exactly; introduce no Host ownership,
no new Host lifecycle phase, no reflection discovery, no metadata
attributes, and no rendering logic; stop and report on any conflict with
the accepted design rather than redesign during implementation. No
conflict arose — the design as approved required no revision to
implement.

## 4. The Problem

1. **Implement exactly the shape ADR-0031/ADR-0032 specify** —
   `NavigationItem` as pure data; `INavigationProvider`/`NavigationService`
   with imperative `Register`/`Unregister`, a deterministically-ordered
   `Items` snapshot, and `Navigate` publishing through the real
   `IEventBus` — without inventing any capability the design did not
   already call for.
2. **Prove the failure and lifecycle model, not merely implement it** — a
   duplicate registration, an unknown-id `Unregister`/`Navigate`, and a
   full Initialise → Running → Dispose cycle leaving no orphaned entry
   must each be demonstrated against real modules and the real
   `ModuleLifecycleManager`, not argued from the code alone.
3. **Register the service without touching anything else** — one new line
   in `TempestHost.cs`'s existing Platform Services Registered block; no
   new Host phase, no Composition Root change, no change to Discovery,
   `RuntimeModuleManager`, or `ModuleLifecycleManager`.
4. **Prove module and plugin parity** — a module contributed by a
   plugin-loaded assembly must register navigation through the identical
   path an ordinarily-discovered module uses, with no plugin-specific
   mechanism anywhere in the implementation.
5. **Touch `Tempest.App` only where compilation absolutely requires it** —
   in fact, not at all: `Tempest.App` references nothing under
   `Tempest.Core.Navigation`, and remains the pre-module-pipeline console
   loop `WP 5.0A`'s own Repository Investigation found it to be.

## 5. The Design

See `src/Tempest.Core/Navigation/` in full — implemented without
deviation from `ADR-0031`/`ADR-0032`'s own code skeleton. `NavigationItem`
is an immutable, caller-constructed data type with a validated `Id`/
`Title` and optional `Order`/`Icon`/`Group`/`ParentId`/`IsVisible`.
`NavigationService` holds registered items in a single
`Dictionary<string, NavigationItem>`, keyed by `Id`, guarded by one
`_gate` lock — mirroring `EventBus`'s own pattern exactly. `Register`
throws `DuplicateNavigationItemException` under the lock if the `Id` is
already taken; `Unregister` is a lock-guarded, silent no-op for an
unknown `Id`. `Items` returns a fresh, lock-guarded snapshot ordered
ascending by `Group` (nulls first, via `StringComparer.Ordinal`'s own
null-sorts-first behaviour), then ascending by `Order`, then ascending
ordinal by `Id` — every registered item regardless of `IsVisible`, which
is never evaluated by the service itself. `Navigate` validates the
requested `Id` under the lock, throwing `NavigationItemNotFoundException`
if absent, then publishes a `NavigationRequestedEvent` through the
constructor-injected `IEventBus` outside the lock. `TempestHost.cs` gained
exactly one new line, `services.Singleton<INavigationProvider,
NavigationService>();`, immediately after the existing
`IEventBus`/`EventBus` registration in the Platform Services Registered
block.

Three new reference modules in `Tempest.Samples` — `NavigationSampleModule`,
`SecondaryNavigationSampleModule`, and `DuplicateNavigationSampleModule` —
constructor-inject `INavigationProvider` and register (and, for the first
two, unregister on disposal) real items, mirroring `ClockModule`'s own
role for the Event Bus.

## 6. Alternatives Considered

None — this work package implements already-decided ADRs exactly, per its
own explicit brief. No new architectural alternative was evaluated here;
see `ADR-0031`/`ADR-0032` and their own retrospective (`WP 5.0A`) for the
alternatives the design phase weighed and rejected (`RD-0030` through
`RD-0033`).

## 7. Why This Solution Was Chosen

Not applicable in the usual sense — the solution was chosen by
`ADR-0031`/`ADR-0032`. This work package's own judgment calls were narrow:
constructor argument validation on `NavigationItem`'s `Id`/`Title`
(`ArgumentException` on null/empty/whitespace), matching `ModuleBase`'s
own established convention, and mirroring `EventBus`'s exact lock-then-
snapshot-then-dispatch-outside-the-lock shape for `NavigationService`
rather than inventing a different concurrency pattern for a structurally
identical problem.

## 8. Architectural Principles

- **Reuse Before Invention** — `NavigationService`'s lock/snapshot pattern
  reuses `EventBus`'s own shape directly; registration reuses
  `ServiceCollection.Singleton<TService, TImplementation>()`, unchanged
  since `WP 2.4`; the duplicate-ID exception hierarchy mirrors
  `ModuleRegistrationException`/`DuplicateModuleRegistrationException`'s
  own shape.
- **Minimal Host Complexity** — confirmed, not merely claimed: `TempestHost.cs`
  gained one `using` directive and one registration line; `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `ReflectionFrameworkDiscoveryService`,
  `TempestServiceProvider`, and `Host Lifecycle.md`'s phase table are
  byte-for-byte/content unchanged.
- **One Responsibility Per Service** — `INavigationProvider` carries a
  navigation catalogue and one notification; it registers, initialises,
  starts, stops, and disposes nothing in the module pipeline.
- **Fail Fast** — `NavigationItem`'s constructor validates `Id`/`Title`
  immediately; `NavigationService.Register` rejects a duplicate `Id`
  immediately, rather than silently overwriting the original registration.

## 9. Benefits

- **Every registration, ordering, failure, lifecycle, and notification
  guarantee `ADR-0031`/`ADR-0032` named is now proven, not merely
  designed** — 45 new tests exercise `NavigationItem`, `NavigationService`,
  and three real sample modules directly, including a real, on-disk
  plugin assembly built and loaded through the unmodified
  `PluginAssemblyLoader`.
- **Zero new Dependency Injection capability was needed, confirmed rather
  than merely predicted** — `services.Singleton<INavigationProvider,
  NavigationService>()` resolves correctly through the real, unmodified
  `TempestServiceProvider`, including its own dependency on the
  container's `IEventBus`.
- **A duplicate navigation registration needs no new Host-level failure
  policy, confirmed against a real failing module** —
  `DuplicateNavigationSampleModule`'s own `InitialiseAsync` throws
  `DuplicateNavigationItemException`, is isolated by the existing,
  unmodified `ModuleLifecycleManager`, and does not prevent any other
  module — including one initialising after it — from succeeding.
- **A plugin-loaded module contributes navigation with zero
  plugin-specific code**, confirmed by loading a real, dynamically-built
  assembly through the real `PluginAssemblyLoader` and discovering it
  through the real, unmodified `ReflectionFrameworkDiscoveryService`.
- **`Tempest.App` required zero changes** — confirmed directly: it
  references nothing under `Tempest.Core.Navigation`, exactly as this
  work package's own brief anticipated.

## 10. Trade-offs

- No automatic unregistration on module stop/dispose is provided *by the
  service itself* — each sample module unregisters explicitly in its own
  `DisposeAsync`, mirroring `ClockLifecycleObserverModule`'s own pattern
  of a module managing its own cleanup rather than the platform managing
  it on the module's behalf. This is `ADR-0032`'s own accepted shape, not
  new debt.
- `NavigationService` now carries a mandatory dependency on `IEventBus` —
  a real, precedented (`LoggerFactory` → `IConfigurationProvider`),
  platform-service-to-platform-service coupling, disclosed at design time.
- The plugin-compatibility test proves a plugin-loaded module contributes
  navigation through the identical DI/lifecycle path an ordinary module
  uses; it does not re-prove Plugin Loading's own assembly-isolation
  guarantees, which `PluginAssemblyLoaderTests` already covers completely.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: asserting `Items`'
ordering only with inputs already added in sorted order, which a
non-deterministic or insertion-order implementation could also satisfy
by coincidence. Registering items deliberately out of order — and, in a
separate test, proving registration order itself has no bearing on the
returned order — closes that gap the same way `EventBusTests`'
subscription-order tests already do for the Event Bus.

## 12. Future Evolution

- **`WP 5.1` (Command Framework)**, once implemented, can call
  `NavigationService.Navigate(...)` directly from application logic,
  exactly as `ADR-0022` already illustrates — no change to either service
  is anticipated.
- **`Tempest.App`'s own rendering** — resolving a `NavigationItem.Id` to
  an actual console menu case or future UI — remains entirely out of
  scope, per this work package's own explicit brief, and is not
  anticipated to require any change to `Tempest.Core.Navigation` when it
  is eventually built.
- **A future permission system**, whenever a real one is designed, plugs
  into `NavigationItem.IsVisible` without `NavigationService` itself
  needing to change — the seam already exists, deliberately, per
  `RD-0033`'s own deferral.

## 13. Key Takeaways

1. Implementing an already-fully-designed pair of ADRs closely is a
   narrow, low-risk exercise precisely because the hard questions were
   already answered — this work package's only real judgment calls were
   constructor validation and reusing `EventBus`'s own concurrency shape,
   not new design decisions.
2. Proving deterministic ordering requires registering inputs
   deliberately out of order and asserting the output is sorted anyway —
   registering them already-sorted would prove nothing a coincidental
   implementation couldn't also satisfy.
3. A plugin-compatibility proof does not need to reinvent Plugin
   Loading's own isolation tests — it only needs to show that, once an
   assembly is loaded and discovered by the existing, unmodified
   pipeline, nothing about Navigation registration treats it any
   differently from an ordinarily-discovered module.

---

## Architectural Debt Assessment

**No new debt introduced.** The trade-offs named above (no automatic
unregistration by the service itself; a mandatory `IEventBus` dependency)
are `ADR-0032`'s own accepted trade-offs, disclosed at design time, not
new debt discovered here. Every other debt item on record from the
Foundation phase and `WP 5.0A` remains exactly as previously described.

## Observations

- **Files added**: `src/Tempest.Core/Navigation/NavigationItem.cs`;
  `INavigationProvider.cs`; `NavigationService.cs`;
  `NavigationRequestedEvent.cs`; `NavigationException.cs`;
  `DuplicateNavigationItemException.cs`; `NavigationItemNotFoundException.cs`;
  `src/Samples/Tempest.Samples/NavigationSampleModule.cs`;
  `SecondaryNavigationSampleModule.cs`; `DuplicateNavigationSampleModule.cs`;
  `tests/Tempest.Core.Tests/Navigation/NavigationItemTests.cs`;
  `NavigationServiceTests.cs`;
  `tests/Tempest.Core.Tests/Samples/NavigationSampleModuleIntegrationTests.cs`;
  this retrospective.
- **Files modified**: `src/Tempest.Core/Runtime/TempestHost.cs` (one new
  `using Tempest.Core.Navigation;` directive and one new line,
  `services.Singleton<INavigationProvider, NavigationService>();`, in the
  existing Platform Services Registered block);
  `tests/Tempest.Core.Tests/Plugins/DynamicPluginAssemblyBuilder.cs`
  (extended with a method that emits a real, on-disk plugin assembly
  containing a Navigation-registering module, for the plugin-compatibility
  test); `tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
  (one pre-existing test's own assembly-wide module count updated from 2
  to 5, since `Tempest.Samples` now legitimately compiles three more real
  modules). **Zero change to `Tempest.App`** — confirmed directly: it
  references nothing under `Tempest.Core.Navigation`.
- **Tests added**: 45 — `NavigationItem` construction and validation (8);
  `NavigationService` registration, duplicate handling, and unregistration
  including repeated cycles (9); hierarchy via `ParentId` (2);
  deterministic `Group`/`Order`/`Id` ordering (4); visibility predicate
  storage without evaluation (2); `Navigate` publishing
  `NavigationRequestedEvent` with correct payload, unknown-id handling,
  multiple subscribers, and cancellation (5); logging (2); DI/Platform
  Service registration (3); constructor injection through the real
  pipeline (1); full Initialise→Running→Dispose lifecycle with no
  orphaned entry, including three repeated fresh-instance runs (3);
  multiple independent modules contributing without collision (2);
  duplicate-ID isolation via a real failing module (2); end-to-end
  execution through the real, unmodified Host (1); plugin-loaded module
  parity via a real, dynamically-built, on-disk plugin assembly (1).
- **Test results**: 400 of 400 passing (355 pre-existing + 45 new), 0
  failures.
- **Build results**: 0 warnings, 0 errors.
- **Platform changes outside `Tempest.Core.Navigation` and the one
  registration line**: none. `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `ReflectionFrameworkDiscoveryService`,
  `TempestServiceProvider`, `Host Lifecycle.md`'s phase table, and
  `Tempest.App` are unchanged.
- **Readiness assessment**: WP 5.0B is complete. `ADR-0031`/`ADR-0032` are
  fully realised and proven against real modules, a real plugin assembly,
  and the real Host. The Navigation Framework is ready for a consumer —
  `Tempest.App`'s own rendering, and `WP 5.1`'s Command Framework, may now
  each proceed as their own, separate work packages against a fully
  validated, real implementation.

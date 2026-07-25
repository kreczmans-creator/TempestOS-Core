# ADR-0029: Background Service Discovery, Ownership, and Orchestration Model

## Status

Accepted — v0.4.0, WP 4.5 (design phase), 2026-07-25. Resolves the
mechanics ADR-0021 deliberately left open when it decided background
service *failure classification* (isolated by default, critical opt-in)
without deciding how a background service is found, constructed, started,
stopped, or ordered relative to its siblings. Architecture only; no
production code accompanies this decision — see `Background Services
Architecture.md` for the full design this ADR resolves.

## Context

ADR-0021 decided *what happens when a background service fails* — isolated
by default, Host-fatal only if the service declares itself
`ICriticalBackgroundService`. It deliberately did not decide: where a
background service comes from; who constructs it; who decides when it
starts and stops relative to the module pipeline and to other background
services; how it participates in dependency injection; or where, precisely,
it sits in `Host Lifecycle.md`'s phase table. `WP 4.0` already defined the
two contracts this ADR builds on:

- **`IHostedService`** (`Tempest.Core.BackgroundServices`) — `StartAsync`/
  `StopAsync`, each taking a `CancellationToken`. No `Id`, `Name`, or
  `Version` — unlike `IModule`, a hosted service carries no identity
  metadata at all.
- **`ICriticalBackgroundService`** — an empty marker interface extending
  `IHostedService`, declaring that this specific service's failure should
  be Host-fatal rather than isolated (ADR-0021).

Neither contract has changed, or needs to change, as a result of this ADR.

### What the repository investigation confirmed

Before proposing a design, this ADR's own investigation traced the existing
mechanisms a background service could plausibly reuse, and found:

- **`TempestServiceProvider` has no multi-registration resolution
  capability** (`IEnumerable<TService>` resolving every implementation of a
  given interface) — confirmed directly, and already the deciding factor
  behind RD-0019 (rejecting DI-auto-discovered Event Bus handlers for
  exactly this reason). Any design assuming the container can enumerate
  "every registered `IHostedService`" would require inventing this
  capability from scratch.
- **Reflection-based discovery is an established, proven pattern**, applied
  twice already for two different candidate shapes: `IModule` (WP 2.1) and
  plugin manifests (WP 4.2). Both share four disciplines — filter before
  instantiating, impose deterministic ordering, isolate per-candidate load
  failures, expose an `internal` test seam — documented as a reusable
  pattern in `docs/academy/04 Design Patterns/04-reflection-based-discovery.md`.
- **`IHostedService` carries no metadata to read**, unlike `IModule`
  (`Id`/`Name`/`Version`, requiring `ReflectionFrameworkDiscoveryService`'s
  own transient-instantiation metadata probe, and — for modules needing
  constructor injection — `ModuleMetadataAttribute`, ADR-0027, to avoid
  that probe). A hosted service candidate needs no metadata probe of any
  kind: discovery only needs to identify *which types* implement
  `IHostedService`, never construct one. This means a hosted service can be
  constructor-injected from its very first implementation — the
  parameterless-constructor constraint ADR-0027 solved for modules never
  applies to background services in the first place.
- **`Runtime Host Architecture.md`'s own Future Extensibility section**
  already named the intended phase placement, in prose, since WP 2.7A:
  hosted services "would slot in between Module Initialisation and Runtime
  Running at startup, and at the front of Shutdown — started after modules
  are initialised, stopped before modules are." This ADR's own placement
  decision (see ADR-0030) realises exactly this sentence.
- **The Event Bus (`IEventBus`, ADR-0020/ADR-0028) already exists** as the
  platform's one, general-purpose cross-component communication mechanism.
  Nothing about background services requires, or should introduce, a second
  one.

## Decision

### Classification: a fourth, Host-owned category — neither a Platform Service nor a Module

Applying the same test `docs/academy/02 Runtime Architecture/
06-platform-layering.md` names for classifying any new capability — *does
this orchestrate the module pipeline, or does it merely carry
data/messages?* — a background service is neither:

- **Not a Platform Service.** A module does not resolve a specific
  `IHostedService` implementation via constructor injection the way it
  resolves `IEventBus` or `IConfigurationProvider` — a module has no
  legitimate reason to hold a direct reference to a specific background
  service, exactly as it has no legitimate reason to hold a direct
  reference to another module (ADR-0020's governing shape, restated here:
  `Module → IEventBus → Runtime`, never `Module A → Module B`, and by the
  identical reasoning, never `Module → SpecificHostedService`).
- **Not a Module.** A background service does not implement `IModule`/
  `IModuleLifecycle`, is not driven by `ModuleLifecycleManager`, is not
  registered with `RuntimeModuleManager`, and has no `Id`. It is discovered,
  constructed, and orchestrated by an entirely separate, new Host-owned
  mechanism.

**A background service is a Host-owned runtime component — structurally
parallel to a Module (discovered via reflection, constructed via the DI
container, orchestrated through a batch lifecycle with per-item failure
isolation) but a distinct kind of thing, with its own, simpler contract and
its own manager.** This mirrors ADR-0017's own reasoning applied to a new
component: the *mechanism that orchestrates* background services
(`IHostedServiceManager`, below) is Host-owned and never DI-public, for the
same reason `ModuleLifecycleManager` is — granting a module a path to it
would let that module start, stop, or enumerate background services
directly, exactly the kind of reach-back-into-the-orchestrator ADR-0017
already forecloses for the module pipeline itself. The *individual hosted
service instances* are, however, ordinary DI-constructed objects, free to
constructor-inject any DI-public Platform Service — including `IEventBus`
— exactly as a module carrying `[ModuleMetadata]` can (ADR-0027).

### Discovery: a new, dedicated reflection-based discovery service — not an extension of `ReflectionFrameworkDiscoveryService`

A new type, `IHostedServiceDiscoveryService`
(`Tempest.Core.BackgroundServices`), mirrors `IFrameworkDiscoveryService`'s
own shape exactly:

```csharp
public interface IHostedServiceDiscoveryService
{
    IReadOnlyList<Type> DiscoverHostedServiceTypes();
}
```

Its implementation, `ReflectionHostedServiceDiscoveryService`, scans loaded
assemblies for concrete types implementing `IHostedService`, applying the
same four disciplines Module Discovery and Plugin Discovery both already
apply: filter interfaces/abstract classes/open generic definitions before
any further work; **never instantiate a candidate** (there is no metadata
to read, so — unlike `ReflectionFrameworkDiscoveryService`'s own
`Activator.CreateInstance` metadata probe — this discovery step performs no
construction of any kind); impose a deterministic order (ascending,
ordinal, by the type's own `FullName`, since a hosted service has no `Id`
to sort by); and expose an `internal`, explicit-candidate-list overload for
deterministic testing, exactly mirroring
`ReflectionFrameworkDiscoveryService.DiscoverModules(IEnumerable<Type>)`'s
own established seam.

**Not an extension of `ReflectionFrameworkDiscoveryService` itself.**
Rejected — see RD-0025 — for the same reason Plugin Discovery received its
own dedicated service rather than extending Module Discovery: one
discovery service scanning for two, unrelated candidate shapes (`IModule`
and `IHostedService`) would blur Module Discovery's own single,
already-frozen responsibility, and would require modifying a component
every other work package has left untouched since WP 2.1.

**No dedicated descriptor type.** `ModuleDescriptor` and `PluginManifest`
both exist because their subjects carry real metadata a later stage needs.
`IHostedService` carries none — the discovered `Type` itself is the
complete answer to "what did discovery find." Introducing a
`HostedServiceDescriptor` wrapping a bare `Type` would be ceremony with no
information to carry. Rejected — see RD-0024.

### Registration: folded into the existing Platform Services Registered phase — no new DI capability

Each discovered type is registered as an ordinary, self-referential
singleton — `services.Singleton(hostedServiceType, hostedServiceType)`,
the exact Type-based overload `AddDiscoveredModules` already uses for
modules — during the *existing* Platform Services Registered phase
(`Host Lifecycle.md`, Phase 6), alongside the existing
`AddDiscoveredModules` call. This requires **zero** new
`ServiceCollection`/`TempestServiceProvider` capability: `GetService(Type)`
already resolves a self-registered singleton's constructor recursively,
exactly as it already does for a module's own concrete type. No new phase
is introduced for discovery/registration — see RD-0027 for why a dedicated
new phase was considered and rejected in favour of folding this into
Phase 6, exactly as WP 4.4D folded the Event Bus's own registration into
the same, existing phase without redefining its meaning.

### Ownership and orchestration: a new, Host-owned `IHostedServiceManager`

A new type, `IHostedServiceManager`/`HostedServiceManager`
(`Tempest.Core.BackgroundServices`), constructed directly by `TempestHost`
— never registered into the `ServiceCollection`, mirroring
`RuntimeModuleManager`/`ModuleLifecycleManager`'s own ADR-0017 status
exactly:

```csharp
public interface IHostedServiceManager
{
    IReadOnlyCollection<HostedServiceStatus> Services { get; }
    Task StartAllAsync(CancellationToken cancellationToken);
    Task StopAllAsync(CancellationToken cancellationToken);
}
```

Constructed from the discovered type list, the already-built
`ITempestServiceProvider`, and an optional `ILogger?` — the same
constructor shape `ModuleLifecycleManager` already has, adapted for a
component with no separate "registration" stage to depend on
(`RuntimeModuleManager`'s role is unnecessary here; there is no duplicate-
identity concept to guard against, since ordering is by type, not by a
caller-supplied string `Id`).

`HostedServiceStatus` — a small, immutable snapshot (`ServiceType`,
`State`, `FailureReason`), mirroring `ModuleLifecycleStatus`'s own shape —
lets a future diagnostics capability (`WP 4.8`) query which background
services are running or failed, exactly as `ModuleLifecycleStatus` already
does for modules.

### Ordering and concurrency: sequential, deterministic start and stop; independent, concurrent execution once started

**Starting and stopping are sequential batch operations, not concurrent
ones** — `StartAllAsync`/`StopAllAsync` process the (ascending-by-`FullName`
sorted) list one service at a time, awaited, exactly mirroring
`ModuleLifecycleManager.RunBatchAsync`'s own established shape. `StopAllAsync`
processes the list in **reverse** order, mirroring modules' own
`StopAllAsync`/`DisposeAllAsync` LIFO teardown discipline. Cancellation is
checked between services, never mid-`StartAsync`/`StopAsync` call — the
Atomic Phase Principle, applied identically to how `RunBatchAsync` already
applies it.

**A background service's own ongoing work, once started, is independent
and concurrent** — with the Host's own `Running` state, with every other
background service, and with modules. This is the entire meaning of
"background": `StartAsync` is expected to arrange for its own long-running
work (a timer, an internally-managed loop, a listener) and **return
promptly**, exactly as `Microsoft.Extensions.Hosting`'s own well-known
`IHostedService` convention already establishes elsewhere in the .NET
ecosystem (unrelated in origin — ADR-0024 — but not unrelated in shape,
since both solve the identical problem the same way). A `StartAsync` that
blocks for the service's entire working life would prevent the Host from
ever reaching `Running` at all — the Host's own orchestration depends on
every `StartAsync` call being bounded, exactly as every existing phase
already is.

Considered and rejected: starting all background services concurrently
(`Task.WhenAll`-style), reasoning that "independent" services need no
ordering relative to each other. Rejected in favour of sequential,
deterministic starting — see RD-0028 — for the same reasons
`ModuleLifecycleManager` already chose sequential batch processing over
concurrent: a fixed, reproducible order is easier to reason about, test,
and diagnose, and nothing about a background service's own *start-up*
requires concurrency — the actual concurrency background services need
(running independently of each other and of the Host) is achieved once
each service's own `StartAsync` has returned, not by starting many services
at once.

### Failure model: exactly ADR-0021, realised precisely

An isolated (non-critical) service's `StartAsync`/`StopAsync` exception is
caught, logged at `Error`, recorded on that service's own
`HostedServiceStatus` (`State = Failed`, `FailureReason` set), and the
batch continues with the next service — identical in shape to
`ModuleLifecycleManager`'s own per-module isolation.

A **critical** service's (`ICriticalBackgroundService`) exception is
**not** caught by the batch — it propagates immediately, aborting the
remainder of that batch call, exactly as a platform-service failure aborts
the phase it occurred in (ADR-0013). During `StartAllAsync`, this reaches
`TempestHost.ExecuteStartupPhasesAsync`'s own existing exception handling
and results in `Starting → Faulted` — no new Host-level catch/transition
logic is required; the existing platform-service-failure path already
does exactly this for any exception escaping a startup phase. During
`StopAllAsync`, the identical exception propagates and results in
`Stopping → Faulted` — a transition `Runtime State Machine.md` already
defines for "a genuine Host-level defect during shutdown orchestration
itself." **Cleanup guarantees are unaffected either way**: per ADR-0004/
ADR-0019, `Faulted → Disposed` remains always legal and disposal is always
attempted, regardless of which phase produced the fault — a critical
service's own failure never prevents the rest of the platform's disposal
from being attempted, it only prevents the *remaining, not-yet-attempted*
part of that specific batch from proceeding, exactly as a Host-fatal
platform-service failure already behaves today.

`OperationCanceledException` is never isolated, from either method — it
propagates uncaught, exactly mirroring `RunBatchAsync`'s own established
cancellation boundary.

### Dependency direction and interaction with existing services

- **Dependency Injection.** A hosted service's constructor is resolved by
  `TempestServiceProvider` exactly like any other registered service —
  free to request `IConfigurationProvider`, `ILogger`, `IPlatformVersionProvider`,
  or `IEventBus`, via ordinary constructor injection. No new DI capability
  is required.
- **Event Bus.** A hosted service may publish or subscribe through
  `IEventBus` exactly as a module can (WP 4.4E's own `ClockModule`/
  `ClockLifecycleObserverModule` precedent). This is also the platform's
  answer to monitoring a hosted service's own *later* failures (after
  `StartAsync` has already returned) — see the Future Considerations
  section, below, and RD-0026.
- **Plugins.** A hosted service type may be shipped inside a plugin
  assembly, exactly as a module type can. `ReflectionHostedServiceDiscoveryService`
  requires no plugin-awareness whatsoever: by the time it runs (after
  Module Initialisation, see ADR-0030), every plugin assembly Plugin
  Loading (Phase 3.2) loaded is already visible to any reflection scan,
  including this one — the identical, already-proven guarantee Module
  Discovery itself already relies on.
- **Runtime Host.** `IHostedServiceManager` is Host-owned, constructed
  directly by `TempestHost`, never DI-public — see Classification, above.
- **Cancellation.** Reuses the Host's existing linked startup token for
  `StartAllAsync`, and the existing shutdown/escalation token for
  `StopAllAsync` — the identical tokens `ModuleLifecycleManager` already
  receives for the equivalent calls. No new cancellation signal is
  introduced.
- **Logging.** `HostedServiceManager` takes an optional `ILogger?`,
  logging discovery counts, per-service start/stop, and isolated failures
  at `Error` — the same universal convention every other platform
  component already follows.
- **Platform Versioning.** No special interaction. `IPlatformVersionProvider`
  is simply another DI-public service a hosted service may consume, no
  differently from any module.

## Consequences

**Positive:**

- Every dispatch/ordering/failure question this work package's own brief
  named now has a decided, written answer, before any implementation.
- Zero new Dependency Injection capability required — confirmed, not
  merely hoped for: multi-registration resolution (rejected, RD-0023) was
  never actually necessary, exactly as RD-0019 already found for the Event
  Bus.
- A hosted service can be constructor-injected from its very first
  implementation, with no ADR-0027-style prerequisite — because
  `IHostedService` carries no metadata, the parameterless-constructor
  constraint modules once had never applies here at all.
- Reuses three already-proven patterns directly: reflection-based
  discovery (Module/Plugin Discovery), sequential per-item batch
  orchestration with failure isolation (`ModuleLifecycleManager.RunBatchAsync`),
  and the Event Bus as the platform's one cross-component communication
  channel — no new mechanism is introduced anywhere in this design.
- Fully consistent with ADR-0013 (isolated/Host-fatal boundary, extended
  by ADR-0021, realised precisely here), ADR-0017 (the orchestrator is
  Host-owned, never DI-public), ADR-0020/ADR-0023 (dependency direction:
  a hosted service depends downward on Platform APIs/Services; nothing
  depends upward on a specific hosted service), and ADR-0021 (failure
  classification, unchanged, only mechanised).

**Negative:**

- A third failure-classification default (alongside platform-service and
  module) now has a fourth *realised* instance — a background service
  reasoning about "what happens if I throw" must know whether it is
  critical, exactly the cost ADR-0021 already named and accepted.
- No monitoring exists for a hosted service's own work *after* `StartAsync`
  returns — a service that starts successfully and later faults internally
  (on its own timer, its own loop) is invisible to the Host's own
  orchestration unless the service itself surfaces the failure (typically
  via `IEventBus`). This is a deliberate, disclosed boundary — see Future
  Considerations — not an oversight.
- Two new Host Lifecycle phases (see ADR-0030) mean `Host Lifecycle.md`'s
  table grows again, following the decimal-numbering precedent ADR-0026
  already established — a real, if now well-precedented, cost each new
  capability of this kind adds.

## Alternatives Considered

Recorded in full, with reasoning, above and permanently indexed as
RD-0023 (DI multi-registration resolution), RD-0024 (a dedicated
`HostedServiceDescriptor` type), RD-0025 (extending
`ReflectionFrameworkDiscoveryService` itself), RD-0026 (active Host-level
monitoring of ongoing background work), RD-0027 (a new, dedicated
discovery/registration phase), and RD-0028 (concurrent start of
independent services).

## Future Considerations

If a genuine need arises for a hosted service's own *later* failure
(after `StartAsync` has returned) to be visible to the Host or to other
components, the correct mechanism is for that service to publish an event
through the already-existing `IEventBus` from within its own defensive
exception handling — not a new, Host-level monitoring or health-check
capability invented speculatively now. If automatic restart or backoff for
an isolated, failed background service is ever needed, that is a separate,
additive capability for a future work package to design deliberately
against a real, demonstrated need — see RD-0029 and ADR-0021's own,
already-standing Future Considerations, both of which this ADR does not
revisit or resolve.

# Ownership Matrix

**Status: implemented — WP 2.7B (`Tempest.Core.Runtime`).** Every ownership
relationship below is now enforced by working code (private fields, `internal`
constructors, and ADR-0017's DI-exclusion), not only documented intent.

**Update, WP 4.2A/WP 4.4D:** two rows added below for platform services
introduced since WP 2.7B — `IPlatformVersionProvider` (Host-owned,
`AddInstance`, like Configuration and Logging) and `IEventBus` (the first
row in this table that is *not* Host-owned: an ordinary,
container-constructed singleton, owned by `TempestServiceProvider` itself,
consistent with ADR-0020's decision that the Event Bus is DI-public rather
than a Host-owned collaborator like Discovery, Registration, or Lifecycle).

**Update, WP 4.5:** two further rows added below, now implemented
(ADR-0029/ADR-0030, `Tempest.Core.BackgroundServices`) —
`IHostedServiceDiscoveryService` and `IHostedServiceManager`, both
Host-owned, mirroring Discovery and Lifecycle's own ADR-0017 status
exactly, for a new, fourth kind of runtime component (neither a Platform
Service nor a Module — see *Background Services Architecture.md*).

**Update, WP 13.12.2 — v0.13.0 plugin platform ownership recorded.**
This matrix had not been touched since `WP 5.2` and carried no row for any
of the seven Host-owned components `v0.13.0` introduced, a gap
`WP 13.12.1`'s readiness re-execution raised as a Disclosed,
Non-Blocking finding (`DNB-4`). Ownership was never ambiguous — each is
constructed exactly once, in `TempestHost`, and the boundary is stated in
`Plugin Platform Architecture.md`, `Plugin Trust & Isolation Architecture.md`
and `ADR-0111` — but it was not recorded here. Seven rows added below,
each citing its single construction site. One deliberate asymmetry worth
naming: `ICurrentComponentAccessor` is the only one of the seven that is
DI-registered at all (the `ADR-0044` precedent) — and it is registered
under **both** its own concrete type and the read-only interface
(`TempestHost.cs:520-521`), the concrete registration being what
`ModuleLifecycleManager`, `HostedServiceManager`, `EventBus`, and
`CommandHandlerTable` resolve in order to call `BeginScope`. That is safe
because the concrete type is denylisted in
`PluginAssemblyLoader.NeverEligibleServiceResolveTypes`, so no plugin may
obtain it by constructor injection or by any `plugin.services.resolve:*`
grant, at any trust tier including First-Party.

**Update, WP 5.1A — drift found and corrected.** A Navigation
(`INavigationProvider`/`NavigationService`) row was never added to this
table at either `WP 5.0A` (design) or `WP 5.0B` (implementation) —
confirmed by direct inspection: no reference to either type existed
anywhere in this file before this Work Package. Added below now,
disclosed here as pre-existing documentation drift found incidentally
while adding this Work Package's own Command Framework row, not caused
by this Work Package. A Command Framework (`ICommandDispatcher`/
`ICommandRegistry`) row is also added, architected — not yet
implemented — by `WP 5.1A` (ADR-0036–ADR-0038); implementation is
`WP 5.1B`.

## Purpose

One table, answering "who is responsible for this" for every significant
object in the platform's runtime. Where the rest of `docs/architecture/`
explains *why* each ownership boundary exists, this document exists to make
each individual answer findable in one place, without needing to read six
documents to find out who constructs, holds, or destroys a given object.

## What "Owner" Means

The **owner** of an object is whichever component:

1. **Constructs it** (or is the only thing that ever calls whatever
   constructs it),
2. **Holds the authoritative reference to it** for as long as it exists, and
3. **Decides when it is destroyed** (disposed, discarded, or allowed to be
   garbage-collected).

Everything else that touches an object is a **consumer**, not an owner — a
consumer may hold a reference (typically resolved via DI, or passed in as a
constructor parameter) and may call methods on it, but does not decide the
object's lifetime and must not attempt to destroy it. This distinction is not
new to this document — it is the same distinction the Platform Service Map
already draws between a service's "dependencies" and "consumers," applied
here at the level of individual objects rather than whole services.

## The Matrix

| Platform Object | Owner | Notes |
|---|---|---|
| Configuration (`IConfigurationProvider`) | `TempestHost` | Built once via `ConfigurationBuilder`, before anything else exists. Never rebuilt for the life of the Host. |
| `ILogSink` (`ConsoleLogSink`) | `TempestHost` | Constructed directly, alongside `LoggerFactory`. |
| `LoggerFactory` (`ILoggerFactory`) | `TempestHost` | Constructed from Configuration; decides every subsequent `Logger`'s minimum level and sink. |
| `Logger` (`ILogger`) | **Factory** | Each `Logger` instance is produced by `LoggerFactory.CreateLogger(category)`. The Factory owns the *policy* that shapes every `Logger` it produces (minimum level, sink); the resulting instance is then held by whichever collaborator requested it (the Host itself, for its default logger; each of Discovery/Registration/Lifecycle, for their own). |
| `ITempestServiceProvider` (`TempestServiceProvider`) | `TempestHost` | Built once, after Configuration, Logging, Discovery, and Registration have all completed (ADR-0011). Never rebuilt. **Update, `WP 5.0D`:** also exposed externally, read-only, via `ITempestHost.Services` (ADR-0034) — `TempestHost` remains the sole owner; an external consumer (the Shell) only ever resolves through it, never replaces or rebuilds it. |
| Discovery (`IFrameworkDiscoveryService`) | `TempestHost` | Constructed directly; never registered in DI (ADR-0017). Used once, during Module Discovery, then no longer needed. |
| Registration (`IRuntimeModuleManager` / `RuntimeModuleManager`) | `TempestHost` | Constructed directly; never registered in DI (ADR-0017). Held for the Host's entire life, since Lifecycle depends on it throughout. |
| Lifecycle (`IModuleLifecycleManager` / `ModuleLifecycleManager`) | `TempestHost` | Constructed directly, after the service provider exists; never registered in DI (ADR-0017). Held for the Host's entire life. |
| Modules (the registered catalogue — `RuntimeModule` records) | **Runtime Manager** (`RuntimeModuleManager`) | The Host owns *driving* modules (via Lifecycle); it does not itself hold the canonical list. `RuntimeModuleManager` remains, unchanged since WP 2.2, "the single authoritative runtime catalogue of every module known to TempestOS." |
| Module lifecycle state (`ModuleLifecycleStatus`) | `ModuleLifecycleManager` | Tracked internally (WP 2.3's `TrackedModule`), exposed only as read-only snapshots. Independent of the Host's own state (ADR-0012). |
| `CancellationToken`(s) | `TempestHost` | The Host owns both signals described in ADR-0014 — the startup token and the shutdown-request signal. Every collaborator only ever *receives* a token; none creates its own. |
| Shutdown (initiation and sequencing) | `TempestHost` | The Host alone decides when `Stopping` begins and drives `StopAllAsync`/`DisposeAllAsync` in order — see *Shutdown Sequence.md*. |
| Disposal (ordering and completion) | `TempestHost` | The Host alone decides the order Service Disposal happens in, and is the only thing that can declare the platform fully `Disposed` — see ADR-0004's Host-level reuse. |
| Platform Version (`IPlatformVersionProvider` / `PlatformVersionProvider`) | `TempestHost` | Constructed directly, immediately after Logging Built (moved earlier by ADR-0026 so Plugin Discovery can use it), and registered via `AddInstance` — the same Composition Root pattern as Configuration and Logging (WP 4.2A). |
| Event Bus (`IEventBus` / `EventBus`) | **`TempestServiceProvider`** | The one platform service in this table the Host does not construct directly — registered as an ordinary `services.Singleton<IEventBus, EventBus>()` (WP 4.4D) and constructed by the container like any other resolved service, the moment something first requests it. DI-public by design (ADR-0020): unlike every `TempestHost`-owned row above, a module may hold and resolve it directly. |
| Hosted Service Discovery (`IHostedServiceDiscoveryService`) *(implemented — WP 4.5, ADR-0029)* | `TempestHost` | Constructed directly; never registered in DI (ADR-0017, applied to a new component). Used once, during Platform Services Registered, then no longer needed — mirroring Discovery's own role exactly. |
| Hosted Service orchestration (`IHostedServiceManager`) *(implemented — WP 4.5, ADR-0029)* | `TempestHost` | Constructed directly, after the service provider exists; never registered in DI (ADR-0017, applied to a new component). Held for the Host's entire life, starting hosted services after Module Initialisation and stopping them before Module Disposal. |
| Navigation (`INavigationProvider` / `NavigationService`) *(implemented — WP 5.0A design, WP 5.0B implementation, ADR-0031/ADR-0032)* | **`TempestServiceProvider`** | Registered as an ordinary `services.Singleton<INavigationProvider, NavigationService>()`, constructed by the container the first time something resolves it — the same non-Host-owned shape the Event Bus row above already established. DI-public by design (ADR-0032): a module or plugin-loaded module may hold and resolve it directly, and registers its own `NavigationItem`s imperatively. This row was missing from this table until `WP 5.1A`; see the Update note above. |
| Command Framework (`ICommandDispatcher` / `ICommandRegistry`) *(implemented — WP 5.1A design, WP 5.1B implementation, ADR-0036–ADR-0038)* | **`TempestServiceProvider`** | Registered as an ordinary singleton, mirroring the Event Bus and Navigation rows exactly (ADR-0036). A module or plugin-loaded module registers its own command handler(s)/descriptor(s) imperatively, during its own lifecycle, exactly as it already does for `IEventBus`/`INavigationProvider`. Both share a `CommandHandlerTable` collaborator (also container-constructed, its own singleton row not separately listed here — see `Dependency Injection Register.md`) so dispatch and Id-based invocation operate against the identical handler set. |
| Diagnostics (`IDiagnosticsProvider` / `DiagnosticsProvider`) *(implemented — WP 5.2, ADR-0039)* | `TempestHost` | Constructed directly, alongside Platform Version — the Composition Root pattern (ADR-0009) — and registered via `AddInstance`, **not** container-constructed like the Event Bus/Navigation/Command Framework rows above. DI-public (a module may resolve it directly), yet Host-constructed: a novel combination for this table, made possible because `DiagnosticsProvider` itself carries no orchestration authority (it only *reads* `Modules`/`HostedServices` via `Func<T>` accessors) even though `TempestHost` is the one that builds it. `IModuleLifecycleManager`/`IHostedServiceManager` — the rows immediately above — remain exactly as Host-owned and non-DI-public as ever; Diagnostics reads their data, never reaches the managers themselves. |
| Plugin Registry (`IPluginRegistry` / `PluginRegistry`) *(implemented — WP 13.1A, ADR-0107/ADR-0017)* | `TempestHost` | Constructed directly (`TempestHost.cs:293`); never registered in DI (ADR-0017's Host-owned-collaborator boundary, applied to a fourth collaborator). Reaches DI only as a read-only projection through `IDiagnosticsProvider.Plugins` — the ADR-0039 precedent, not a second registration. |
| Plugin Trust Store (`IPluginTrustStore` / `PluginTrustStore`) *(implemented — WP 13.2A, ADR-0112)* | `TempestHost` | Constructed directly (`TempestHost.cs:299`), Host-owned alongside `PluginRegistry` for the identical ADR-0017 reason. Consulted during Plugin Discovery (3.1) for trust-tier assignment and detached-signature verification; never DI-resolvable by a module or plugin. |
| Plugin component principal registry (`IPluginComponentPrincipalRegistry` / `IPluginComponentPrincipalRecorder` / `PluginComponentPrincipalRegistry`) *(implemented — WP 13.2A, ADR-0111)* | `TempestHost` | Constructed directly (`TempestHost.cs:308`); never registered in DI (ADR-0017). Read/write split so nothing outside `Tempest.Core.Plugins` is handed a mutating reference: `PluginAssemblyLoader` records through the recorder interface, `TempestHost`'s own `componentScopeProvider` closure observes through the registry interface. |
| Plugin denied-type registry (`IPluginDeniedTypeRegistry` / `IPluginDeniedTypeRecorder` / `PluginDeniedTypeRegistry`) *(implemented — WP 13.9.4, ADR-0111)* | `TempestHost` | Constructed directly (`TempestHost.cs:325`); never registered in DI (ADR-0017), mirroring `PluginComponentPrincipalRegistry`'s own read/write split exactly. Written during Plugin Loading (3.2); read by Module Discovery's `isTypeExcluded` predicate (WP 13.9.6) and by both Registration filters — every write completes before the first read. |
| Ambient component accessor (`ICurrentComponentAccessor` / `CurrentComponentAccessor`) *(implemented — WP 13.2A, ADR-0111)* | `TempestHost` | Constructed directly (`TempestHost.cs:334`). The one plugin-trust collaborator that **is** DI-registered, mirroring `ICurrentPrincipalAccessor`'s own ADR-0044 precedent. Registered under **both** its own concrete type and the read-only `ICurrentComponentAccessor` interface (`TempestHost.cs:520-521`) — the concrete registration is what `ModuleLifecycleManager`/`HostedServiceManager`/`EventBus`/`CommandHandlerTable` resolve to call `BeginScope`. The concrete type is named in `PluginAssemblyLoader.NeverEligibleServiceResolveTypes`, so no plugin may obtain it by constructor injection or by any `plugin.services.resolve:*` grant, at any trust tier including First-Party. |
| Plugin Discovery (`PluginManifestDiscoveryService`) *(implemented — WP 13.1A, ADR-0107)* | `TempestHost` | Constructed directly (`TempestHost.cs:336`); never registered in DI (ADR-0017, applied as for Module Discovery). Used once, during Plugin Discovery (3.1), then no longer needed — mirroring Discovery's own role exactly. |
| Plugin Loading (`IPluginAssemblyLoader` / `PluginAssemblyLoader`) *(implemented — WP 13.1A, ADR-0107/ADR-0111)* | `TempestHost` | Constructed directly (`TempestHost.cs:356`); never registered in DI (ADR-0017). Used once, during Plugin Loading (3.2). Holds the write side of both the component-principal and denied-type registries for the duration of that phase, and nothing beyond it. |

## Reading the Matrix Alongside Other Documents

- **"Owner: `TempestHost`" rows** are the direct, itemised consequence of
  *Runtime Host Architecture.md*'s Responsibilities section (orchestration,
  startup, shutdown, cancellation, disposal ordering) — this table is that
  section broken down object by object, not a new decision.
- **The "Modules" row** is a deliberate exception worth re-reading carefully:
  it is the one place in this table where the Host constructs and holds the
  *manager* but explicitly does not own the *thing the manager manages* — a
  distinction already established by WP 2.2's own design (ADR-0001,
  `RuntimeModule` immutability) and unaffected by anything WP 2.7 introduces.
- **The "Logger: Factory" row** is the one entry that isn't simply
  "`TempestHost`," and is worth re-reading if the rest of the table's pattern
  makes it look like an oversight — it isn't; see the Notes column.
- **The "Event Bus," "Navigation," and "Command Framework" rows** are the
  entries that aren't `TempestHost`, and for a genuinely different reason
  than the Logger row: none of the three is Host-owned at all. `IEventBus`
  (ADR-0020), `INavigationProvider` (ADR-0032), and the Command Framework
  (ADR-0036) are each DI-public precisely because none carries any
  orchestration authority — a module resolving any of the three is not
  reaching back into anything the Host would need to keep private, unlike
  Discovery, Registration, or Lifecycle above them in this table. All
  three were decided independently, at three different Work Packages, and
  reached the identical conclusion — see `Failure Isolation Across
  TempestOS`'s own "asked fresh each time" discipline, applied here to an
  ownership question rather than a failure-isolation one.
- **The two Hosted Service rows** are `TempestHost`-owned, like Discovery,
  Registration, and Lifecycle — deliberately the *opposite* pattern from
  the Event Bus/Navigation/Command Framework rows immediately above them.
  A hosted service *instance* may consume `IEventBus` and any other
  DI-public service exactly like a module can; the *manager that starts
  and stops it* is kept Host-owned for the identical reason
  Discovery/Registration/Lifecycle are — see ADR-0029.
- **The "Diagnostics" row** is a genuinely new combination in this table:
  DI-public, like the Event Bus/Navigation/Command Framework rows, *and*
  constructed directly by `TempestHost`, like Configuration/Logging/
  Platform Version. This is possible only because `DiagnosticsProvider`
  itself carries no orchestration authority of its own — it is a
  read-only projection over data two Host-owned managers already produce,
  never a path back to either manager (ADR-0039).
- Every row implicitly cites ADR-0017 (Discovery/Registration/Lifecycle are
  Host-owned, never DI-public) and ADR-0011 (the order in which the
  `TempestHost`-owned objects come into existence).

## What This Table Deliberately Does Not Cover

Object *consumers* — for example, which future modules might resolve
`IConfigurationProvider` or `ILogger` via constructor injection — are not this
table's concern; that is the Platform Service Map's "Consumers" column,
already maintained separately. This table answers exactly one question per
row — *who owns this* — and is not a substitute for the fuller dependency
picture the Platform Service Map and *The Module Pipeline* already provide.

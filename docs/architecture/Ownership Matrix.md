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
| `ITempestServiceProvider` (`TempestServiceProvider`) | `TempestHost` | Built once, after Configuration, Logging, Discovery, and Registration have all completed (ADR-0011). Never rebuilt. |
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
- **The "Event Bus" row** is the second entry that isn't `TempestHost`, and
  for a genuinely different reason than the Logger row: it isn't Host-owned
  at all. `IEventBus` is DI-public (ADR-0020) precisely because it carries
  no orchestration authority — a module resolving it is not reaching back
  into anything the Host would need to keep private, unlike Discovery,
  Registration, or Lifecycle immediately above it in this table.
- **The two Hosted Service rows** are `TempestHost`-owned, like Discovery,
  Registration, and Lifecycle — deliberately the *opposite* pattern from
  the Event Bus row immediately above them. A hosted service *instance*
  may consume `IEventBus` and any other DI-public service exactly like a
  module can; the *manager that starts and stops it* is kept Host-owned for
  the identical reason Discovery/Registration/Lifecycle are — see
  ADR-0029.
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

# TempestOS v0.6.0 — Service Lifecycle

## Purpose

For each proposed `v0.6.0` service: whether it is DI-public or
Host-owned, exactly how it comes to exist (ordinary container
construction vs. Composition-Root `AddInstance` vs. `IHostedService`),
and exactly which `Host Lifecycle.md` phase it is constructed/registered/
started/stopped in — confirming, service by service, that **no new Host
Lifecycle phase is required for any of the eight proposed services**,
including the REST API's own placement at the existing `IHostedService`
phases.

## Classification Key

- **DI-public, container-constructed** — an ordinary `services.
  Singleton<TInterface, TImplementation>()` registration, resolved via
  reflection-based constructor injection like the overwhelming majority
  of platform services (Event Bus, Navigation, Command Framework).
- **Composition-Root-constructed** — built directly by `TempestHost`
  (or, for Licensing, before `TempestHost` itself exists) and registered
  via `AddInstance`, per `ADR-0009` — used only when a service's own
  construction requires something the container cannot yet provide
  (Diagnostics' `Func<T>` accessors; Licensing's pre-container timing).
- **Hosted Service** — discovered and orchestrated by
  `IHostedServiceManager` exactly like any other `IHostedService`
  (`ADR-0029`/`ADR-0030`) — used only by the REST API.

## Per-Service Placement

| Service | Classification | Host Lifecycle Phase | New Phase Required? |
|---|---|---|---|
| Persistence | DI-public, container-constructed | 6 (Platform Services Registered) | No |
| Reporting | DI-public, container-constructed | 6 | No |
| Identity & Permissions | DI-public, container-constructed | 6 | No |
| Notifications | DI-public, container-constructed | 6 | No |
| Settings | DI-public, container-constructed | 6 | No |
| Audit | DI-public, container-constructed | 6 | No |
| Licensing (validator) | Composition-Root-constructed, pre-container | Before Phase 1 completes (mirrors Configuration) | No |
| Licensing (provider) | Composition-Root-constructed, `AddInstance` | 6 | No |
| Export/Import | DI-public, container-constructed | 6 | No |
| REST API | Hosted Service | Start 8.1, Stop 10.1 | No |

Every row resolves to an already-existing phase — this release adds no
row to `Host Lifecycle.md`'s own Phase Table, and no change to `Runtime
State Machine.md`'s seven-state model.

## Persistence, Reporting, Identity & Permissions, Notifications,
## Settings, Audit, Export/Import

Each of these seven follows the identical, already-dominant pattern this
platform uses for a service with no orchestration authority of its own —
the Event Bus, Navigation, Command Framework's three registrations, and
Diagnostics all reached this same shape independently (`Release
Architecture.md`'s own observation that four independent decisions
converging on one shape is evidence of a correct default, not
coincidence):

1. Registered via `services.Singleton<TInterface, TImplementation>()`
   during `TempestHost`'s existing Platform Services Registered block
   (Phase 6) — added as additional lines within the method that already
   registers `IEventBus`, `INavigationProvider`, the three Command
   Framework types, and `IDiagnosticsProvider`. No new method, no new
   phase.
2. Resolved by any consumer (module or another platform service) via
   ordinary constructor injection — reachable identically whether the
   consumer is a first-party module or a plugin-loaded one (`ADR-0032`'s
   "no special-casing" precedent, applied to seven more services).
3. Disposed, if `IDisposable`, at the ordinary Host-teardown point every
   container-constructed singleton already goes through — no bespoke
   disposal step.

Settings' dependency on Persistence, and Audit's dependency on both
Persistence and Identity & Permissions, are ordinary constructor
dependencies between two container-constructed singletons — no
different in kind from Navigation's own existing dependency on the
Event Bus. Registration order within Phase 6 must place Persistence and
Identity & Permissions ahead of Settings/Audit in the method body (the
container resolves a graph, not a list, so registration order does not
itself need to match dependency order — but human-readability of the
Phase 6 registration block should still group a dependency above its
dependents, matching the existing block's own top-to-bottom convention).

## Licensing

The one proposed service that does **not** fit the pattern above,
deliberately: `ILicenseValidator.Validate()` must run *before* the DI
container is built, so an invalid license can abort startup at the
earliest possible point — the same reasoning `ADR-0013` already applies
to every other platform-service failure, extended to a check that
cannot itself be a resolved, constructed service yet. Concretely:

1. `TempestHostBuilder` (or `TempestHost`'s own startup method)
   constructs `ILicenseValidator` directly — a plain `new`, reading its
   own license-file source, with no constructor dependencies at all
   (mirroring Platform Version's "deliberately a leaf" position) —
   immediately after Configuration is built and before Logging Built,
   i.e., at the same point in the sequence Configuration itself
   occupies today.
2. If `Validate()` reports `IsValid: false`, startup aborts immediately
   — Host-fatal, per `ADR-0013` — before Logging Built even runs, since
   there is nothing yet to log to in a way any later phase depends on.
3. If valid, the resulting `ILicense` is wrapped in a simple
   `ILicenseProvider` implementation and registered via `AddInstance`
   during Phase 6, exactly like `IPlatformVersionProvider` and
   `IDiagnosticsProvider` are today.

This two-step split (a pre-container validation gate, then a
post-validation, DI-public read-only view) is the same shape Platform
Version and Diagnostics each independently arrived at for their own,
different timing constraints — not a new pattern, but this release's
third application of it.

## REST API

The REST API is a Hosted Service, full stop — no adaptation of
`ADR-0029`/`ADR-0030` is required:

1. Discovered by the existing `IHostedServiceDiscoveryService`
   (reflection-based, exactly like Module/Plugin Discovery) — no change
   to the discovery mechanism itself.
2. Registered as an ordinary self-referential singleton during Phase 6,
   exactly like any other hosted service.
3. Started by `IHostedServiceManager` at Phase 8.1 (after Module
   Initialisation, alongside every other hosted service, in
   deterministic order) — begins listening for HTTP requests only once
   started, never before.
4. Every inbound request, once started, resolves
   `ICurrentPrincipalAccessor`/`IPermissionEvaluator` and
   `ICommandRegistry` through the *already-built* container (the
   container is complete well before Phase 8.1) — no request-time
   container mutation of any kind.
5. Stopped by `IHostedServiceManager` at Phase 10.1 (before Module
   Disposal, in reverse order) — stops accepting new connections and
   drains in-flight requests within whatever timeout
   `ICriticalBackgroundService`'s existing shutdown contract already
   provides, introducing no new shutdown signal beyond `ADR-0014`'s
   existing cancellation/shutdown-request distinction.

This placement directly retires `AT-07` ("Zero real hosted services
exist beyond the infrastructure... Revisit trigger: The first Work
Package that ships a real hosted service") — see `Technical Debt
Assessment.md`.

## Confirmation: No New Phase, No New State

- `Host Lifecycle.md`'s Phase Table gains registrations *within*
  existing phases (6, 8.1, 10.1) — no new decimal-numbered phase, no
  renumbering of any existing phase.
- `Runtime State Machine.md`'s seven states (`Registered` →
  `Initialising` → `Initialised` → `Starting` → `Running` → `Stopping` →
  `Stopped`, plus `Failed`) are unaffected — every proposed service is
  either a Platform Service (no lifecycle state of its own, exactly like
  the Event Bus or Navigation) or, for the REST API alone, an
  `IHostedService` reusing `HostedServiceState`/`HostedServiceStatus`
  exactly as they already exist.

## Related Documents

`docs/architecture/Host Lifecycle.md`; `docs/architecture/Runtime State
Machine.md`; `docs/architecture/Ownership Matrix.md`; `Release
Architecture.md`; `Platform Services Overview.md`; `Public Interface
Catalogue.md`; `ADR-0009`, `ADR-0013`, `ADR-0014`, `ADR-0017`,
`ADR-0029`, `ADR-0030`, `ADR-0039`.

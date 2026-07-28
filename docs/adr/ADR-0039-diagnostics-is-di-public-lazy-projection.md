# ADR-0039: Diagnostics Is a DI-Public Platform Service Exposing Host-Owned Lifecycle State via Lazy, Read-Only Projection

## Status

Accepted — `WP 5.2` (Diagnostics Improvements), 2026-07-28.

## Context

`WP 4.0` deliberately left `IDiagnosticsProvider` undefined. `WP 5.2`'s
own brief requires it: "a consumer can query every module's state
without gaining write access to `IRuntimeModuleManager`/
`IModuleLifecycleManager` themselves." The data a diagnostics consumer
needs — `ModuleLifecycleStatus`, `HostedServiceStatus`, `HostState` —
already exists, but its two richest sources,
`IModuleLifecycleManager`/`IHostedServiceManager`, are both Host-owned
and deliberately never added to the dependency injection container
(ADR-0017): a module must never reach the machinery orchestrating it.

A second, load-bearing constraint surfaced during design: neither
`IModuleLifecycleManager` nor `IHostedServiceManager` exists yet at the
point in `Host Lifecycle.md`'s phase table where Platform Services are
registered (Phase 6) — both are constructed only afterwards
(`IModuleLifecycleManager` after Dependency Injection Built;
`IHostedServiceManager` only after Module Initialisation completes,
per ADR-0029/ADR-0030's own phase ordering, which this Work Package does
not revisit). A DI-public service needing either reference cannot simply
take it as an ordinary constructor parameter — no such reference exists
yet when the container is built.

## Decision

**`IDiagnosticsProvider` is a DI-public Platform Service** (the fourth,
after the Event Bus, Navigation, and the Command Framework, to reach this
conclusion independently), registered as an already-constructed instance
via `AddInstance` — the Composition Root pattern (ADR-0009), the same
mechanism already used for Configuration, Logging, and
`PlatformVersionProvider` — rather than an ordinary container-constructed
singleton, because its concrete implementation's own dependencies are not
themselves container-resolvable.

**Every dependency is supplied as a `Func<T>` accessor, not a direct
reference**, closing over `TempestHost`'s own private
`_lifecycleManager`/`_hostedServiceManager` fields. This lets
`DiagnosticsProvider` be constructed and registered early (Phase 6,
before either manager exists) while still reporting live, current data
once they do. `HostState` is supplied the same way, via `() => State`,
so it always reflects the Host's own current state, not a value frozen
at construction time.

**Before a collaborator has been constructed, its corresponding
projection reports an honest "not yet available" — an empty collection,
never an exception.** `HostedServices` is expected to be legitimately
empty for any diagnostics consumer that queries it before Host Lifecycle
Phase 8.1 completes (a module's own constructor, in particular, always
runs before this). This mirrors `ITempestHost.Services`'s own
`null`-before-Dependency-Injection-Built convention (ADR-0034) exactly:
"not yet available" is a normal, honestly-reported temporal state, not
an error condition to guard against or throw over.

## Consequences

**Positive:**

- No new Dependency Injection capability is required — `AddInstance`
  already exists, already used identically for three other services.
- `IModuleLifecycleManager`/`IHostedServiceManager` remain exactly as
  unreachable to a module as ADR-0017 already requires — `Diagnostics`
  exposes only their own already-public, read-only snapshot types
  (`ModuleLifecycleStatus`, `HostedServiceStatus`), never the managers
  themselves, and never their `InitialiseAllAsync`/`StartAllAsync`/etc.
  write surface.
- Establishes a reusable pattern — "a DI-public projection over a
  Host-owned collaborator not yet constructed at registration time,"
  via `Func<T>` accessors — for any future Platform Service facing the
  identical timing constraint.

**Negative:**

- A consumer resolving `IDiagnosticsProvider` very early (from its own
  constructor, during Module Initialisation) sees `HostedServices` as
  empty, even though hosted services will exist moments later. This is
  disclosed, not hidden, but is a genuine, observable gap between "the
  service is resolvable" and "every projection it offers is populated" —
  a caller wanting a guaranteed-complete view should query after the Host
  reaches `HostState.Running`, not from within a module's own lifecycle
  methods.
- `DiagnosticsProvider`'s three `Func<T>` constructor parameters are less
  immediately self-explanatory at a call site than ordinary, direct
  references would be — mitigated by this ADR's own explanation and the
  type's own doc comments.

## Alternatives Considered

**Resolve `IModuleLifecycleManager`/`IHostedServiceManager` directly as
ordinary constructor parameters.** Rejected outright — neither is ever
registered in the container (ADR-0017); this would not compile against
the real `TempestServiceProvider`, and registering either to make it
work would reopen ADR-0017 itself, which this Work Package does not have
standing to revisit.

**Defer `DiagnosticsProvider`'s own registration until after both
managers exist** (register it later in `ExecuteStartupPhasesAsync`,
after Module Initialisation, instead of during Platform Services
Registered). Rejected — `IServiceCollection.AddInstance`/`Singleton` have
no effect once `TempestServiceProvider` has already been constructed
from the collection (confirmed by direct inspection); registration must
happen during Phase 6, before the container is built, or not at all.

**Move `IHostedServiceManager`'s own construction earlier**, ahead of
Module Initialisation, so both managers exist by Phase 6. Rejected —
`Host Lifecycle.md`'s phase table (Module Initialisation, Phase 8, before
Hosted Services Started, Phase 10.1) is frozen, approved architecture
(ADR-0029/ADR-0030); reordering it to suit Diagnostics' own convenience
would be exactly the "redesign the framework" this Work Package's own
brief prohibits.

## Related Documents

`ADR-0009` (Composition Root pattern); `ADR-0017` (Discovery/
Registration/Lifecycle Host-owned); `ADR-0020`, `ADR-0032`, `ADR-0036`
(the three prior, independent "is this a DI-public Platform Service"
decisions this one reaches a fourth time); `ADR-0034` (`ITempestHost.
Services`'s own null-before-ready precedent); `Diagnostics
Architecture.md`.

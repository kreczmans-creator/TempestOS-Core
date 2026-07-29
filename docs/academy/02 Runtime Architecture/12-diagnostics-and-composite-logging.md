# Diagnostics & Composite Logging

**Status: designed and implemented in a single Work Package — `WP 5.2`
(`ADR-0039`), mirroring the `WP 4.5` (Background Services) precedent of
combining architecture and implementation where the scope is small enough
not to need a separate design phase.**

## 1. Introduction

TempestOS closes two long-standing, named pieces of technical debt in
`WP 5.2`: `TD-02` (a log entry can only ever go to one sink at a time) and
a genuine gap `WP 4.0` deliberately left open — there was no way for
anything, anywhere, to ask the platform "what is actually happening right
now?" without either reading raw console output or reaching directly into
Host-owned orchestration machinery that `ADR-0017` forbids a module from
ever touching. `CompositeLogSink` and `IDiagnosticsProvider` answer these
two questions together, because both are, at heart, the same underlying
need: making information the platform already produces genuinely visible,
without opening a door that was deliberately kept shut.

## 2. Purpose

To explain, from first principles, why a platform that already has
logging and already has module/hosted-service lifecycle tracking still
needs a dedicated Diagnostics concept layered on top of both — and why
that concept had to be built as a *read-only projection*, not a new way
to reach the objects underneath.

## 3. Background — Two Debts, One Underlying Shape

`TD-02` existed since `WP 2.6`: `Logger` writes to exactly one `ILogSink`.
This was never a defect — a single `ConsoleLogSink` was always sufficient
for a locally-run process with one output stream — but it is a named,
disclosed limitation the moment a second sink (a file, a future remote
log aggregator) becomes a real need.

Separately, `WP 4.0` scoped `IDiagnosticsProvider` deliberately out of its
own Platform Contracts phase (`RD-0002`), because at that point neither
`IModuleLifecycleManager` nor `IHostedServiceManager` existed yet to have
anything to project from. By `WP 5.2`, both exist, both already expose
their own read-only snapshot collections (`Modules`, `Services`), and the
only thing missing is a single, DI-public front door onto both at once.

## 4. The Problem

Two genuinely different-shaped problems, solved by two genuinely
different, small mechanisms:

**Composite logging.** `Logger` holds exactly one `ILogSink` field. Fan-out
to more than one destination requires either changing `Logger` itself (a
change to an already-stable, `WP 2.6`-era type, for a need with no real
consumer yet) or introducing a sink that *is itself* multiple sinks,
requiring no change to `Logger`, `ILoggerFactory`, or any existing
consumer of `ILogger` at all.

**Diagnostics.** A consumer needs to read the state of every registered
module and every hosted service — but `IModuleLifecycleManager` and
`IHostedServiceManager` are Host-owned collaborators, never registered in
the DI container (`ADR-0017`), precisely so that the machinery
orchestrating a module can never be reached *by* a module. Any solution
has to expose the read-only *data* those two managers already produce,
without exposing the managers — or the write-capable operations they
carry (`StartAllAsync`, `DisposeAllAsync`, and so on) — themselves.

## 5. The Design

**`CompositeLogSink`** is an ordinary `ILogSink` implementation that holds
a fixed list of child sinks and fans every `Write` call out to each of
them, isolating one child's own failure from every other — the same
sink-failure-isolation convention `Logger` itself already established one
level up. No interface changed; a consumer of `ILogger` cannot tell,
without inspecting the registered sink's own type, whether it is writing
to one destination or five.

**`IDiagnosticsProvider`** is a small, three-property, read-only interface
(`HostState`, `Modules`, `HostedServices`) — no methods, no mutation
surface of any kind. It is registered exactly like `IConfigurationProvider`
or `IPlatformVersionProvider` (the Composition Root pattern, `ADR-0009`):
`TempestHost` constructs one `DiagnosticsProvider` instance directly and
registers it via `AddInstance`, rather than letting the container
construct it.

The one genuine wrinkle `WP 5.2`'s own investigation found: neither
`IModuleLifecycleManager` nor `IHostedServiceManager` exists yet at the
point in the Host Lifecycle where DI registrations happen (Platform
Services Registered, Phase 6) — both are constructed later (Phase 8 and
Phase 10.1 respectively, per `ADR-0029`/`ADR-0030`'s frozen phase table).
`DiagnosticsProvider` is therefore given `Func<T>` accessors — closures
over `TempestHost`'s own private fields — rather than direct constructor
references, so it can be built and registered early while still reporting
live, current data once those fields are actually assigned. This directly
mirrors `ITempestHost.Services`'s own established convention
(`ADR-0034`): "not yet available" is reported honestly (`null`, or here,
an empty collection), never thrown as an error.

## 6. Alternatives Considered

See `Diagnostics Architecture.md` and `ADR-0039`'s own Alternatives
Considered section for the complete reasoning. In summary: resolving the
two managers as ordinary constructor parameters does not compile (they are
never registered, by design); deferring `DiagnosticsProvider`'s own
registration until after both managers exist doesn't work, because
`AddInstance`/`Singleton` calls have no effect once the container is
already built; and reordering the Host Lifecycle's own frozen phase table
to construct the managers earlier would mean redesigning already-settled,
multiply-cross-referenced architecture for the convenience of one new,
much smaller feature.

## 7. Why This Solution Was Chosen

`Func<T>` accessors let `DiagnosticsProvider` be constructed once, early,
in the ordinary Composition Root position every other externally-created
service already occupies — no special-cased second registration phase, no
change to the Host Lifecycle's own phase table, and no new capability
added to the DI container. The cost — a caller that queries
`HostedServices` during Module Initialisation legitimately sees an empty
collection, since `IHostedServiceManager` genuinely does not exist yet at
that point — is disclosed plainly rather than hidden, and costs nothing
to a caller that queries later, once the Host has actually finished
starting.

## 8. Architectural Principles

- **Least Privilege** — `IDiagnosticsProvider` exposes exactly the
  read-only projection a consumer needs (state snapshots) and nothing a
  consumer must never have (the managers' own write-capable methods),
  directly satisfying this Work Package's own Acceptance Criteria.
- **Reuse Before Invention** — `ModuleLifecycleStatus` and
  `HostedServiceStatus` are reused exactly as-is; neither is duplicated or
  wrapped in a new type.
- **Fail Honestly, Not Silently, Not by Throwing** — "not yet available"
  is reported as an empty collection, mirroring `ITempestHost.Services`'s
  own `null`-before-ready convention, rather than a thrown exception for
  what is, at that point in the lifecycle, entirely normal.
- **Composition Root Pattern** — `DiagnosticsProvider`, like
  `IConfigurationProvider`, `ILogger`, and `IPlatformVersionProvider`
  before it, is built directly by `TempestHost` and registered as an
  already-constructed instance (`ADR-0009`), not left to the container to
  construct.

## 9. Benefits

- Closes `TD-02` with zero change to `Logger`, `ILoggerFactory`, or any
  existing `ILogger` consumer.
- Gives every future consumer — a future Shell status page, a future
  health-check command, this Work Package's own `GetDiagnosticsSummaryCommand`
  — one, DI-public place to ask "what is the platform doing right now,"
  without any of them needing a reference to Host-owned machinery.
- Costs nothing architecturally: no new Host Lifecycle phase, no new DI
  container capability, no new failure mode of its own.

## 10. Trade-offs

- A caller resolving `IDiagnosticsProvider` very early (during a module's
  own `InitialiseAsync`) sees `HostedServices` reported as empty — not a
  bug, but a real, timing-dependent gap a reader must understand rather
  than assume away.
- `TD-01` (the legacy `LoggingService`/bootstrap-era logging mechanism)
  is deliberately **not** resolved by this Work Package — see this Work
  Package's own retrospective for the reasoning behind re-scoping it
  forward rather than migrating genuinely dead code.

## 11. Common Mistakes

- **Assuming `IDiagnosticsProvider` should expose `IModuleLifecycleManager`
  or `IHostedServiceManager` directly**, "for convenience." This is
  exactly the boundary `ADR-0017` exists to prevent — the moment a module
  can reach the manager, it can call the manager's own write-capable
  methods, and the isolation the platform relies on disappears.
- **Assuming an empty `HostedServices` collection means something is
  broken.** During Module Initialisation, it is the honest, expected
  answer — check the Host Lifecycle phase before treating "empty" as a
  failure.
- **Reaching for a constructor parameter of type `IModuleLifecycleManager`
  directly**, by analogy with how ordinary DI-public services are
  constructed. It will not resolve — the type is never registered, by
  design — and the fix is a `Func<T>` accessor, not a new DI registration.

## 12. Future Evolution

A second, real `ILogSink` implementation (a file sink, a remote log
aggregator) is the natural next consumer of `CompositeLogSink` — none
exists yet, so none was built speculatively. A future Shell diagnostics
page, a periodic Host-orchestrated health check, and eventually resolving
`TD-01` in full (migrating or deleting the legacy bootstrap-era logging
code) are all named, explicitly deferred possibilities in `Diagnostics
Architecture.md` — not designed now, because no real consumer needs any
of them yet.

## 13. Key Takeaways

1. Two named debts (`TD-01`, `TD-02`) turned out to share almost nothing
   in common except being logging-adjacent — `TD-02` was closed outright;
   `TD-01` was re-scoped forward again, deliberately, because the code it
   concerns is already dead.
2. A read-only projection over data two Host-owned managers already
   produce is a fundamentally different, much safer thing to make
   DI-public than the managers themselves — the distinction this Work
   Package's entire design rests on.
3. `Func<T>` accessors are the established pattern, in this codebase, for
   a Composition-Root-constructed service that needs to report on
   something not yet built at its own construction time — the same shape
   `ITempestHost.Services` already uses for the DI container itself.

## Related Documents

`Diagnostics Architecture.md` (the complete design); `ADR-0009` (Composition
Root); `ADR-0017` (Host-owned collaborators never DI-public); `ADR-0034`
(the `null`/empty-before-ready convention this design reuses); `ADR-0039`
(this Work Package's own decision); `docs/academy/03 Work Packages/WP5.2-diagnostics-improvements.md`.

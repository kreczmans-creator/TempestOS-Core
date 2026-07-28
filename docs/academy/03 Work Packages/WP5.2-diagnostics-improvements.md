# WP 5.2 — Diagnostics Improvements

## 1. Introduction

WP 5.2 closes two named pieces of technical debt (`TD-01`, `TD-02`) and
delivers the one platform contract `WP 4.0` deliberately scoped out of its
own Platform Contracts phase (`RD-0002`): `IDiagnosticsProvider`. Like
`WP 4.5` before it, this is a single, combined Work Package — design and
implementation together — because the scope proved small enough not to
need a separate architecture phase.

## 2. Purpose

To build `CompositeLogSink` (closing `TD-02`), decide the `TD-01` legacy
`LoggingService` migration question, and build `IDiagnosticsProvider`/
`DiagnosticsProvider` — a read-only projection over `IModuleLifecycleManager`
and `IHostedServiceManager`'s own existing snapshot data — without
granting any consumer write access to either manager, and without
altering the Runtime Host, Event Bus, Navigation, or Command Framework in
any way.

## 3. Background

This Work Package's own brief, as originally written, asked for an "Event
Framework Implementation" against an "Event Framework Architecture.md."
Investigation before any code was written found neither existed: no such
architecture document is anywhere in the repository, and the Event Bus
(`IEventBus`/`EventBus`) has been fully implemented since `WP 4.4D`,
registered in `TempestHost`, and covered by 27 existing tests. The actual,
current `WP 5.2` entry in `docs/releases/v0.5.0/WorkPackages.md` is, and
has always been, **Diagnostics Improvements** — a different scope
entirely. This mismatch was surfaced directly rather than guessed past,
mirroring `WP 4.4C`'s own precedent (`D-009`): stop before implementing
against a false premise, present the finding, and let the redirection be
an explicit decision, not an inference. See `D-019`.

## 4. The Problem

Three separate, small problems, named by `WorkPackages.md`'s own `WP 5.2`
entry:

1. **`TD-02`** — `Logger` writes to exactly one `ILogSink`; there is no
   way to fan a log entry out to more than one destination.
2. **`TD-01`** — two logging mechanisms have coexisted since `WP 2.6`:
   the platform's own `ILogger`/`ILoggerFactory`, and a legacy,
   bootstrap-era `LoggingService` that predates the module pipeline
   entirely. `WP 5.2` was named as the Work Package that must either
   migrate `BootstrapService`/`HostingService`/`Program.cs` off the
   legacy mechanism, or explicitly re-scope the debt forward again.
3. **No health/status visibility** — nothing anywhere can ask "what is
   the platform actually doing right now" (is the Host running; which
   modules are up; which hosted services are up) without either reading
   raw console log output or reaching directly into
   `IModuleLifecycleManager`/`IHostedServiceManager`, both Host-owned and
   never DI-public (`ADR-0017`).

## 5. The Design

**`CompositeLogSink : ILogSink`** fans a log entry out to a fixed,
construction-time list of child sinks, isolating one child's own write
failure from every other (caught, reported to `Console.Error`, never
propagated) — the same sink-failure-isolation convention `Logger` itself
already established one level up. No existing type changed.

**The `TD-01` decision:** `Program.cs` has not called
`BootstrapService`/`HostingService`/the legacy logging path since
`WP 5.0D` — confirmed again directly during this Work Package's own
Repository Investigation. Migrating code with zero live callers carries
no behavioural benefit and only risk (introducing a regression in code
nothing currently exercises). This Work Package's decision:
**do not migrate — re-scope `TD-01` forward again**, exactly as
`WorkPackages.md`'s own brief allowed. See `D-020`.

**`IDiagnosticsProvider`** exposes exactly three read-only properties —
`HostState`, `Modules` (`IReadOnlyCollection<ModuleLifecycleStatus>`),
`HostedServices` (`IReadOnlyCollection<HostedServiceStatus>`) — reusing
both snapshot types exactly as they already exist, no duplication. It is
registered via the Composition Root pattern (`ADR-0009`): `TempestHost`
constructs one `DiagnosticsProvider` directly and registers it with
`AddInstance`, exactly like `IConfigurationProvider`/`IPlatformVersionProvider`.

The one genuine implementation finding: neither `IModuleLifecycleManager`
nor `IHostedServiceManager` exists yet at Phase 6 (Platform Services
Registered), where DI registrations happen — both are constructed later
(Phase 8, Phase 10.1). `DiagnosticsProvider` is given `Func<T>` accessors
closing over `TempestHost`'s own private fields, exactly mirroring
`ITempestHost.Services`'s own established "not yet available reports
empty/null, never throws" convention (`ADR-0034`). See `ADR-0039`.

`DiagnosticsSampleModule` (`Tempest.Samples`) constructor-injects
`IDiagnosticsProvider`, `ICommandDispatcher`, and `ICommandRegistry`;
observes `HostState`/`Modules`/`HostedServices` during its own
`InitialiseAsync` (disclosing, rather than hiding, that
`HostedServices` is legitimately empty at that point); and registers
`GetDiagnosticsSummaryCommand`, whose handler reads
`IDiagnosticsProvider` to report a one-line platform-status summary —
demonstrating the Command Framework and Diagnostics interacting exactly
as a future Shell "show platform status" command realistically would.

## 6. Alternatives Considered

See `ADR-0039` and `Diagnostics Architecture.md` for the complete
reasoning. In summary: resolving the two managers as ordinary constructor
parameters does not compile, since neither is registered by design;
deferring `DiagnosticsProvider`'s own registration until after both
managers exist does not work, since `AddInstance` has no effect once the
container is already built; and reordering the Host Lifecycle's own
frozen phase table to construct the managers earlier would mean
redesigning already-settled architecture (`ADR-0029`/`ADR-0030`) for this
one feature's convenience — exactly the "redesign the framework" this
Work Package's own brief prohibited absent a genuine defect.

## 7. Why This Solution Was Chosen

`Func<T>` accessors let `DiagnosticsProvider` occupy the same, ordinary
Composition Root position every other externally-created service already
occupies, at no cost to the Host Lifecycle, the DI container, or any
existing platform service. Re-scoping `TD-01` forward, rather than
migrating dead code, follows this project's own standing rule against
manufacturing unnecessary changes — a migration with no live caller to
validate against is pure risk with no corresponding benefit.

## 8. Architectural Principles

- **Least Privilege** — `IDiagnosticsProvider` exposes read-only state and
  nothing else; a consumer can query every module's state without ever
  gaining a path to `StartAllAsync`/`DisposeAllAsync` or any other
  write-capable manager method.
- **Reuse Before Invention** — `ModuleLifecycleStatus`, `HostedServiceStatus`,
  and the Composition Root pattern are reused exactly as they already
  exist; nothing was duplicated.
- **Fail Honestly, Not by Throwing** — "not yet available" is an empty
  collection, not an exception, mirroring `ITempestHost.Services`'s own
  convention.
- **Premise Verification Before Implementation** — this Work Package's own
  opening finding (Section 3) is itself an application of the same
  discipline `D-009` established: investigate before writing code against
  an unverified brief.

## 9. Files Added

`src/Tempest.Core/Logging/CompositeLogSink.cs`;
`src/Tempest.Core/Diagnostics/IDiagnosticsProvider.cs`;
`src/Tempest.Core/Diagnostics/DiagnosticsProvider.cs`;
`src/Samples/Tempest.Samples/DiagnosticsSampleModule.cs`;
`src/Samples/Tempest.Samples/GetDiagnosticsSummaryCommand.cs`;
`src/Samples/Tempest.Samples/GetDiagnosticsSummaryCommandHandler.cs`;
`tests/Tempest.Core.Tests/Logging/CompositeLogSinkTests.cs`;
`tests/Tempest.Core.Tests/Diagnostics/DiagnosticsProviderTests.cs`;
`tests/Tempest.Core.Tests/Samples/DiagnosticsSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0039-diagnostics-is-di-public-lazy-projection.md`;
`docs/architecture/Diagnostics Architecture.md`;
`docs/academy/02 Runtime Architecture/12-diagnostics-and-composite-logging.md`;
this retrospective.

## 10. Trade-offs

- A caller resolving `IDiagnosticsProvider` during a module's own
  `InitialiseAsync` sees `HostedServices` reported as empty — a real,
  disclosed timing gap (proven directly by
  `DiagnosticsSampleModuleIntegrationTests`), not a defect.
- `TD-01` remains open — re-scoped forward again, not resolved. A future
  Work Package that actually needs the legacy bootstrap code (or decides
  to delete it outright) will need to revisit this.

## 11. Common Mistakes

- **Assuming `IDiagnosticsProvider` should expose the managers
  themselves**, "for convenience" — this is exactly the boundary
  `ADR-0017` exists to prevent.
- **Assuming an empty `HostedServices` collection during Module
  Initialisation means something is broken** — it is the honest, expected
  answer at that point in the Host Lifecycle.
- **Assuming this Work Package's own name ("Diagnostics Improvements")
  must mean something adjacent to logging severity levels or telemetry**
  — the actual scope, per `WorkPackages.md`, is composite logging plus
  read-only lifecycle-state visibility; nothing about metrics,
  telemetry, or external monitoring integration was in scope.

## 12. Future Evolution

A second, real `ILogSink` implementation; a future Shell diagnostics page
consuming `IDiagnosticsProvider` directly; periodic Host-orchestrated
health checks; and eventually resolving `TD-01` in full (migrating or
deleting the legacy bootstrap-era code) are all named, explicitly
deferred possibilities in `Diagnostics Architecture.md` — not designed
now, because no real consumer needs any of them yet.

## 13. Key Takeaways

1. A Work Package's own brief can be wrong about what it names — verifying
   the premise before writing code is a standing discipline in this
   project (`D-009`, now `D-019`), not a one-time exception.
2. `TD-01` and `TD-02`, despite sharing a debt-register category, required
   opposite treatment: one was closed with new code, the other was
   deliberately left alone because migrating dead code has no benefit.
3. `Func<T>` accessors are this codebase's established answer to "a
   Composition-Root-constructed service needs to report on something not
   yet built at its own construction time" — the same shape
   `ITempestHost.Services` already uses.

## Architectural Debt Assessment

`TD-02` — **Resolved** by this Work Package. `TD-01` — **Open, reassessed
and re-scoped forward again** (no new owning Work Package named; revisit
trigger: the legacy bootstrap code is either genuinely revived or
deliberately deleted). No new debt item was found to be genuinely
required: the plugin-visibility implication of `IDiagnosticsProvider`
(a plugin-loaded module can now also read every other module's lifecycle
state) is fully subsumed by the already-tracked `TD-09` (no isolation
boundary between a loaded plugin and a first-party module) — the same
root cause, not a new, distinct gap.

## Observations

The Host Lifecycle phase-ordering constraint this Work Package found
(`IModuleLifecycleManager`/`IHostedServiceManager` not yet constructed at
Phase 6) was not assumed — it was confirmed directly against
`TempestHost.cs`'s own field-assignment order before `ADR-0039` was
written. `Architecture Document Register.md` and `Feature Register.md`
were both found to have drifted stale independently of this Work
Package's own scope (the Command Framework's own "implementation
pending" marker, not updated when `WP 5.1B` actually completed it) —
corrected here as part of this Work Package's own repository review,
consistent with the standing practice of fixing pre-existing governance
drift found along the way, not only the drift a Work Package's own brief
names. A second, older instance of the same pattern was also found:
`docs/releases/v0.5.0/WorkPackages.md`'s own `WP 5.0D` entry had read
"Not started" since it was first written, even though that Work Package
completed long ago — no Work Package since (`WP 5.0S`, `WP 5.1A`,
`WP 5.1B`) had touched that specific line during its own repository
review. Both corrections are a reminder that a repository review's own
scope should be "everything encountered," not merely "everything the
current brief names."

## Related Documents

`Diagnostics Architecture.md`; `ADR-0039`; `docs/academy/02 Runtime
Architecture/12-diagnostics-and-composite-logging.md`; `Technical Debt
Register.md` (`TD-01`, `TD-02`); `Decision Register.md` (`D-019`,
`D-020`); `docs/releases/v0.5.0/WorkPackages.md` (`WP 5.2`'s own entry).

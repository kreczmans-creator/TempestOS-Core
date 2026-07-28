# Diagnostics Architecture

**Status: designed and implemented — `WP 5.2` (ADR-0039). `WP 4.0`
deliberately left `IDiagnosticsProvider` undefined, naming this Work
Package as its own revisit trigger.**

## Objective

Close two disclosed diagnostics debt items (`TD-01`, `TD-02`, both since
`WP 2.6`) and define `IDiagnosticsProvider`: a read-only, DI-public
projection over the Runtime Host's own lifecycle state, letting a
consumer observe module and hosted-service health without gaining any
authority over either.

## Repository Investigation

**No `IDiagnosticsProvider`, health/status service, or composite log
sink exists anywhere in the repository** — confirmed directly (`grep -rl
"IDiagnosticsProvider\|CompositeLogSink" src/` returns nothing before
this Work Package). `WP 4.0`'s own Platform Contracts phase left this
gap explicitly, rather than guessing at a shape ahead of a real need.

**What already exists and must be reused, not duplicated:**

- **`ModuleLifecycleStatus`** (`Tempest.Core.Modules`, `WP 2.3`) — an
  already-public, already-immutable snapshot of one module's own
  lifecycle state, exposed via `IModuleLifecycleManager.Modules`.
- **`HostedServiceStatus`** (`Tempest.Core.BackgroundServices`, `WP 4.5`)
  — the identical shape, one layer down, for a hosted service, exposed
  via `IHostedServiceManager.Services`.
- **`HostState`** (`Tempest.Core.Runtime`, `WP 2.7B`) — already exposed
  publicly via `ITempestHost.State`.
- **`ILogSink`/`ILogger`/`ILoggerFactory`** (`Tempest.Core.Logging`,
  `WP 2.6`) — the existing logging abstraction. `ILogger`'s own contract
  already depends on nothing about *where* a message ends up; only the
  sink `LoggerFactory` is constructed with decides that. Adding a second
  sink implementation requires no change to either.
- **`ITempestHost.Services`** (ADR-0034, `WP 5.0D`) — the direct
  precedent for "expose Host-internal state safely to an external
  consumer, `null`/empty before it exists yet, not an error."
- **`IModuleLifecycleManager`, `IHostedServiceManager`** — both
  Host-owned, never DI-public (ADR-0017), and — a genuine, load-bearing
  finding from this Work Package's own investigation, not assumed —
  **neither exists yet at the point in `Host Lifecycle.md`'s phase table
  where Platform Services are registered** (Phase 6). `IModuleLifecycleManager`
  is constructed only after Dependency Injection Built;
  `IHostedServiceManager` only after Module Initialisation completes.
  This is the central constraint `IDiagnosticsProvider`'s own design
  answers — see ADR-0039.

**No duplication found.** Nothing under `Tempest.Core` currently reports
aggregated platform health; nothing under `Tempest.Core.Logging` fans
output out to more than one destination. The surface this Work Package
adds does not overlap or re-decide anything an existing platform service
already owns.

## Architecture

### Composite Logging (`TD-02`)

**`CompositeLogSink : ILogSink`** wraps two or more child sinks,
writing every log entry to each in order. A child sink's own exception
is caught and reported directly to `Console.Error` — mirroring
`Logger`'s own established sink-failure-isolation convention exactly,
applied one level down so a single failing child cannot prevent a
sibling sink from receiving the same entry.

**No change to `ILogger`, `ILoggerFactory`, or any consumer of either.**
`LoggingServiceCollectionExtensions.AddLogging` and `TempestHost`'s own
composition continue to construct a single `ConsoleLogSink` today — this
Work Package's own deliverable is the *capability* to fan out to more
than one sink, proven directly by test, not a change to what
`Tempest.App` actually runs with. No second, real sink implementation
exists in this codebase to fan out to yet; introducing one (a file sink,
a network sink) is explicitly out of this Work Package's own scope — see
Required for v0.5 vs. Deferred, below.

### The Legacy `LoggingService` Question (`TD-01`)

`WP 5.2`'s own brief required a decision, not a default: migrate
`BootstrapService`/`HostingService`/`Program.cs` off the legacy
`LoggingService`, or explicitly re-scope the debt forward again.

**Decision: re-scope forward. No migration is performed.**
`Program.cs` already stopped calling `BootstrapService`/`HostingService`/
`ProjectService` entirely as of `WP 5.0D` — confirmed directly, this
code path is already unreachable from the running application. The only
remaining question is whether to rewrite, or delete, code that nothing
calls. Migrating genuinely dead code changes no observable behaviour and
introduces pure risk (of a mistake in code no test currently exercises
via any real entry point) for zero benefit — exactly the "do not create
unnecessary changes" instruction this Work Package's own brief states
directly. `TD-01` remains **Open**, annotated with this Work Package's
own reasoning, not resolved — a real migration (or deletion) is deferred
until a real, demonstrated need for this bootstrap-era code either
returns it to service or removes it outright. See `Technical Debt
Register.md`.

### Diagnostics (`IDiagnosticsProvider`)

**Ownership and registration**: see ADR-0039 in full. In summary:
DI-public, constructed directly by `TempestHost` and registered via
`AddInstance` (the Composition Root pattern, ADR-0009) rather than an
ordinary container-constructed singleton, because two of its three
data sources are themselves Host-owned, never-DI-public collaborators
that do not exist yet at the point Platform Services are registered.

```csharp
namespace Tempest.Core.Diagnostics;

public interface IDiagnosticsProvider
{
    HostState HostState { get; }
    IReadOnlyCollection<ModuleLifecycleStatus> Modules { get; }
    IReadOnlyCollection<HostedServiceStatus> HostedServices { get; }
}

public sealed class DiagnosticsProvider : IDiagnosticsProvider
{
    public DiagnosticsProvider(
        Func<HostState> hostStateAccessor,
        Func<IModuleLifecycleManager?> lifecycleManagerAccessor,
        Func<IHostedServiceManager?> hostedServiceManagerAccessor);

    public HostState HostState { get; }
    public IReadOnlyCollection<ModuleLifecycleStatus> Modules { get; }
    public IReadOnlyCollection<HostedServiceStatus> HostedServices { get; }
}
```

**No imperative registration surface.** Unlike the Event Bus, Navigation,
and the Command Framework, nothing *registers with* Diagnostics — it is
a pure, read-only reporter over data two other services already
maintain. A module cannot contribute to, or influence, what Diagnostics
reports; it can only observe it.

**No write access to the underlying managers.** `Modules`/`HostedServices`
expose only the already-public, already-immutable `ModuleLifecycleStatus`/
`HostedServiceStatus` snapshot types — never `IModuleLifecycleManager`/
`IHostedServiceManager` themselves, and never any method that could
initialise, start, stop, or dispose anything. This is the direct,
concrete realisation of this Work Package's own Acceptance Criteria: "a
consumer can query every module's state without gaining write access to
`IRuntimeModuleManager`/`IModuleLifecycleManager` themselves."

### Dependency Direction

```
Module / Plugin-loaded Module
        │  (constructor-injects)
        ▼
IDiagnosticsProvider
        │  (reads live, via Func<T> accessors)
        ├──▶ HostState               (TempestHost.State)
        ├──▶ IModuleLifecycleManager?.Modules
        └──▶ IHostedServiceManager?.HostedServices
```

- A module or plugin-loaded module depends downward on
  `IDiagnosticsProvider`, exactly as it already depends on `IEventBus`,
  `INavigationProvider`, or `ICommandDispatcher`/`ICommandRegistry`
  (ADR-0023: dependencies flow downward only).
- `DiagnosticsProvider` itself depends on nothing module-specific — it
  holds three `Func<T>` closures over `TempestHost`'s own private state,
  supplied once at construction, never reaching back into anything a
  module would need to stay unaware of.
- **Never depends on `IEventBus`, `INavigationProvider`, or the Command
  Framework**, and none of those three depends on it. Diagnostics is a
  fifth, independent Platform Service, orthogonal to all of them.

## Lifecycle Interaction

**No new Host Lifecycle phase, no new `HostState`, no new transition.**
`DiagnosticsProvider` is constructed and registered during the existing
Platform Services Registered phase (Phase 6) — one new block of code,
alongside the existing `AddInstance`/`Singleton` calls, closing over
`TempestHost`'s own already-existing `_lifecycleManager`/
`_hostedServiceManager` private fields. Neither field's own assignment
changes; Diagnostics only adds a new way to read them, guarded by the
same `_gate` lock `TempestHost` already uses for `State`/`Services`.

## Failure Model

`IDiagnosticsProvider` itself has no failure mode of its own — it never
throws (beyond ordinary `ArgumentNullException` for a `null` constructor
argument, at construction time only) and never blocks. Reading it before
one of its two lazily-attached collaborators exists simply returns an
empty collection — an honest, expected temporal state (see ADR-0039),
not a failure requiring a Host-level policy of any kind.

## Public Surface

| Type | Kind | New? |
|---|---|---|
| `Tempest.Core.Logging.CompositeLogSink` | Sealed class | Yes |
| `Tempest.Core.Diagnostics.IDiagnosticsProvider` | Interface | Yes |
| `Tempest.Core.Diagnostics.DiagnosticsProvider` | Sealed class | Yes |

No change to `ILogger`, `ILoggerFactory`, `ILogSink`, `ModuleLifecycleStatus`,
`HostedServiceStatus`, `HostState`, `IModuleLifecycleManager`, or
`IHostedServiceManager` — every existing type this design reads from is
reused exactly as it already stood.

## Testing Implications

Following this project's own established "prefer real implementations
over mocks" convention:

- **`CompositeLogSink`**: writes to every child sink in order; a
  throwing child does not prevent a sibling from receiving the same
  entry; a throwing child's own exception never propagates to the
  caller; an empty or `null`-containing sink list is rejected at
  construction.
- **`DiagnosticsProvider`**: `HostState` reflects the live, current
  value from its accessor, not a value frozen at construction;
  `Modules`/`HostedServices` return empty before their respective
  accessor returns a non-null manager, and the real data once it does;
  constructor-null-argument validation.
- **Real Host integration**: a real, discovered module (the Sample
  Module's own new diagnostics-observing addition) constructor-injects
  `IDiagnosticsProvider` and is proven, through the real, unmodified
  `TempestHost`, to observe its own module's state transition from
  `Initialising` through `Running`, and — once the Host reaches
  `Running` — a non-empty `HostedServices` collection.
- **Plugin parity**: a plugin-loaded module resolves `IDiagnosticsProvider`
  through the identical path an ordinarily-discovered module uses.

## Risks

- **`DiagnosticsProvider`'s `Func<T>`-based construction is a genuinely
  new pattern in this codebase** — mitigated by ADR-0039's own complete
  reasoning and this document's own worked example, so a future
  contributor facing the identical "DI-public service needs a
  not-yet-constructed Host-owned collaborator" problem has a precedent
  to follow rather than reinventing one.
- **A consumer misreading an early-queried, empty `HostedServices` as
  "no hosted services exist" rather than "not yet observed"** —
  mitigated by this document's and `IDiagnosticsProvider`'s own doc
  comments stating the distinction explicitly.

## Alternatives Considered

Recorded in full in ADR-0039's own "Alternatives Considered" section:
resolving the Host-owned managers directly as constructor parameters
(rejected — neither is ever registered); deferring `DiagnosticsProvider`'s
own registration until both managers exist (rejected — `AddInstance`/
`Singleton` have no effect once the container is already built);
reordering `Host Lifecycle.md`'s own phase table so both managers exist
earlier (rejected — frozen, approved architecture; reordering it to suit
Diagnostics' own convenience is exactly the redesign this Work Package's
brief prohibits).

## Documentation Impact

**New**: this document; ADR-0039; a `WP 5.2` Academy retrospective; a new
Academy concept guide.

**Updated**: `Platform Service Map.md` (new Diagnostics entry; Logging
entry notes the composite-sink capability); `Ownership Matrix.md` (new
Diagnostics row); `Engineering Glossary.md` (new Diagnostics entry;
Logging entry notes `CompositeLogSink`); `Technical Debt Register.md`
(`TD-01` annotated, not resolved; `TD-02` resolved).

**Not required**: no `Host Lifecycle.md`/`Runtime State Machine.md`/
`Failure Behaviour.md` change — no new phase, state, or Host-level
failure category is introduced.

## Validation Against Governing Documents

- **`FOUNDATION.md`.** One responsibility per component, unchanged (②):
  `DiagnosticsProvider` reports; it does not orchestrate, register, or
  construct anything. No new externally-mutable state (③): every type
  Diagnostics exposes was already immutable before this Work Package.
  Dependencies flow downward only (⑨): a module depends on Diagnostics;
  Diagnostics depends on nothing module-specific.
- **ADR-0009.** Directly applied — the Composition Root pattern,
  reasoned about explicitly, not merely cited.
- **ADR-0017.** Unaffected, reaffirmed: `IModuleLifecycleManager`/
  `IHostedServiceManager` remain exactly as unreachable as before;
  Diagnostics exposes only their own already-public read-only snapshots.
- **ADR-0023.** Preserved; every dependency drawn in this document's own
  diagram points downward.
- **ADR-0034.** Directly extended: the same "not yet available is a
  normal state, not an error" discipline `ITempestHost.Services` already
  established, applied a second time.

## Required for v0.5 vs. Deferred Beyond v0.5

**Required for v0.5 (this Work Package):**

- `CompositeLogSink`, proven by direct test.
- `IDiagnosticsProvider`/`DiagnosticsProvider`, registered during the
  existing Platform Services Registered phase, proven against the real
  Runtime Host.
- A documented decision on the legacy `LoggingService` question
  (re-scoped forward, not migrated).

**Explicitly deferred beyond v0.5 (named here so they are not silently
forgotten, not because any of them is currently planned):**

- **A second, real `ILogSink` implementation** (a file sink, a network
  sink) to actually exercise `CompositeLogSink` in the running
  application. No real consumer needs one yet; `Tempest.App` continues
  to run with a single `ConsoleLogSink`.
- **Migrating or deleting the legacy `LoggingService`/`BootstrapService`/
  `HostingService`/bootstrap-era code.** Re-scoped forward again, per
  this document's own "The Legacy `LoggingService` Question" section —
  revisit trigger: a real, demonstrated need either to revive or to
  remove this code.
- **Periodic, Host-orchestrated health checks** (a hosted service
  polling Diagnostics and acting on what it finds). `IDiagnosticsProvider`
  is a passive reporter only; nothing currently consumes it
  automatically. `WP 4.5`'s own Background Services infrastructure could
  host such a check in future, without any change to `IDiagnosticsProvider`
  itself.
- **A Diagnostics `NavigationItem` or `CommandDescriptor`** (a
  health/status page or command a future Shell could present). No UI
  exists to present one yet; the Shell's own Status Bar region
  (`Shell & Composition Framework Architecture.md`) was reserved for
  exactly this, unpopulated by design until a real consumer exists.

## Future Extensibility

- **A future GUI, tablet, mobile, or web shell** consumes the identical
  `IDiagnosticsProvider` surface this document designs to populate a
  health/status view — the same platform/application boundary already
  proven for Navigation and Commands.
- **A future permission model**, whenever one is designed, could gate
  who may resolve `IDiagnosticsProvider` at all, without this document's
  own shape needing to change — deliberately not designed now, per
  `Security Principles.md` Principle 7.

## Related Documents

`ADR-0009` (Composition Root pattern); `ADR-0017` (Discovery/
Registration/Lifecycle Host-owned); `ADR-0023` (four-layer model);
`ADR-0034` (`ITempestHost.Services`'s own precedent); `ADR-0039` (this
Work Package); `Technical Debt Register.md` (`TD-01`, `TD-02`);
`docs/security/Platform Security Review v0.5.0.md`; `Runtime Host
Architecture.md`; `Host Lifecycle.md`; `Background Services
Architecture.md`; `docs/releases/v0.5.0/WorkPackages.md` (`WP 5.2`).

# Runtime State Machine

**Status: implemented — WP 2.7B (`Tempest.Core.Runtime`).** `HostState` and
`TempestHost` implement every state, transition, and illegal-transition guard
described below exactly.

**Update, WP 4.2:** ADR-0026's two new lifecycle *phases* (Plugin
Discovery, Plugin Loading — see `Host Lifecycle.md`) are now implemented,
both occurring entirely within the existing `Starting` state, exactly as
designed. **No new state and no new transition were introduced.** A phase
is a finer-grained unit than a state (this document's own Overview already
says so); this remains true with fifteen phases mapping onto seven states
just as it was true with thirteen.

**Update, WP 4.5:** ADR-0029/ADR-0030's two new lifecycle *phases* (Hosted
Services Started, Hosted Services Stopped — see `Host Lifecycle.md`) are
now implemented, occurring entirely within the existing `Starting`/
`Stopping` states respectively. **No new state and no new transition were
introduced.** A critical hosted service's failure uses exactly the
`Starting → Faulted`/`Stopping → Faulted` transitions this document
already defines for a platform-service failure and a genuine shutdown-time
Host-level defect, respectively — not a new transition of its own.

## Overview

The Runtime Host has its own state machine, independent of `ModuleState` (see
ADR-0012 for why these are deliberately two separate things). Seven states:
`Created`, `Starting`, `Running`, `Stopping`, `Stopped`, `Faulted`, `Disposed`.

## State Diagram

```mermaid
stateDiagram-v2
    [*] --> Created

    Created --> Starting: Run() called

    Starting --> Running: All platform-service phases,\nModule Initialisation, and Hosted\nServices Started complete
    Starting --> Faulted: Platform-service failure\n(Configuration, Logging, DI,\nDiscovery, Registration - ADR-0013)\nor a critical hosted service's\nStartAsync failure (ADR-0021/0029,\nimplemented - WP 4.5)
    Starting --> Stopping: Startup cancellation OR an early\nshutdown request fires - not a fault\n(ADR-0018)

    Running --> Stopping: Shutdown requested (ADR-0014)
    Running --> Faulted: Unhandled runtime exception\n(no code path today - WP 4.5\nintroduces no ongoing supervision\nof a running hosted service)

    Stopping --> Stopped: Hosted Services Stopped + Module\nDisposal + Service Disposal complete\n(individual failures already isolated,\nWP 2.3/WP 4.5) - the same procedure\nregardless of whether Stopping was\nentered from Running or Starting
    Stopping --> Faulted: Genuine Host-level defect during\nshutdown orchestration itself, or a\ncritical hosted service's StopAsync\nfailure (ADR-0021/0029, implemented -\nWP 4.5)\n(not an ordinary module failure)

    Stopped --> Disposed: Dispose()
    Faulted --> Disposed: Dispose() - always attempted,\nper ADR-0004's Host-level reuse

    Disposed --> [*]
```

## States

| State | Meaning |
|---|---|
| `Created` | The Host object exists; nothing has been built. |
| `Starting` | Any of Configuration Built through Module Initialisation is in progress (see *Host Lifecycle.md*). |
| `Running` | Module Initialisation completed; the platform is up. Does not imply every module succeeded — see ADR-0013. |
| `Stopping` | Controlled shutdown in progress: Module Disposal, then Service Disposal. Entered from `Running` (graceful shutdown) or from `Starting` (cancellation or an early shutdown request — ADR-0018); the procedure is identical either way. |
| `Stopped` | Controlled shutdown completed — whether it was a graceful, post-`Running` shutdown or an interrupted startup torn down via `Stopping`. |
| `Faulted` | A platform-service failure aborted startup, or a genuine Host-level defect occurred during Running or Stopping. |
| `Disposed` | Terminal. Every resource that could be released has had release attempted. |

## Transitions

| From | To | Trigger |
|---|---|---|
| `Created` | `Starting` | The Host's run method is called. |
| `Starting` | `Running` | Every platform-service phase, Module Initialisation, and Hosted Services Started (ADR-0029/0030, implemented — WP 4.5) completed (regardless of individual module/hosted-service outcomes). |
| `Starting` | `Faulted` | A platform-service failure (Configuration/Logging/DI/Discovery/Registration) — ADR-0013 — or a critical hosted service's `StartAsync` failure (ADR-0021/0029, implemented — WP 4.5). |
| `Starting` | `Stopping` | The startup `CancellationToken` fires, **or** a shutdown request (ADR-0014) arrives early, before `Running` is reached; not treated as a fault — ADR-0018. |
| `Running` | `Stopping` | A shutdown request (ADR-0014) is observed. |
| `Running` | `Faulted` | An unhandled runtime exception (no producing code path exists today — `WP 4.5` introduces no ongoing supervision of a running hosted service). |
| `Stopping` | `Stopped` | Hosted Services Stopped, Module Disposal, and Service Disposal all completed (individual module Stop/Dispose failures already isolated by WP 2.3, and individual hosted-service Stop failures per ADR-0021/0029, implemented — WP 4.5, do not prevent this). |
| `Stopping` | `Faulted` | A genuine Host-level defect in shutdown orchestration itself, or a critical hosted service's `StopAsync` failure (ADR-0021/0029, implemented — WP 4.5) — not an ordinary module or isolated hosted-service failure. |
| `Stopped` | `Disposed` | Disposal is invoked (the normal path). |
| `Faulted` | `Disposed` | Disposal is invoked — always legal, per ADR-0004's Host-level reuse (WP 2.7 update); disposal must be attempted even after a fault. |

## Terminal States

**`Disposed`** is the only true terminal state — no outgoing transition exists
from it. Both `Stopped` and `Faulted` converge on it, exactly as ADR-0004
established for individual modules (permissive disposal, restricted only
against an already-`Disposed` state) and ADR-0012/ADR-0013 apply that same
philosophy one level up, at the Host.

## Illegal Transitions

The following are explicitly **not** legal, and a future implementation should
reject them (with a dedicated exception, following the established
`InvalidModuleLifecycleTransitionException` pattern from WP 2.3 — see the WP
2.7 Academy review's Recommendations):

- `Created → Running` (must pass through `Starting`).
- `Created → Stopping` / `Created → Stopped` (nothing has started; there is
  nothing to stop).
- `Starting → Stopped` **directly** (no longer legal since ADR-0018: any
  cancellation or early shutdown request during `Starting` must pass through
  `Stopping`, exactly as a graceful, post-`Running` shutdown does — there is
  now exactly one path to `Stopped`, not two).
- `Running → Starting` (no re-entrant or repeated startup — a Host instance
  runs once).
- `Stopped → Starting` / `Stopped → Running` (no restart — **decided**, not
  open: see ADR-0015, *Runtime Hosts Are Not Restartable*. A `TempestHost`
  instance is single-use; a second run means a new `TempestHostBuilder`
  producing a new `TempestHost`, not a transition back to `Starting`).
- `Faulted → Starting` / `Faulted → Running` (a faulted Host cannot resume;
  only `Faulted → Disposed` is legal, mirroring exactly how `Failed` modules
  can still be disposed but never re-initialised).
- Anything `→ Disposed →` anything else (terminal; no exceptions).

## Relationship to `ModuleState`

This state machine and `ModuleState` (WP 2.2/2.3) are deliberately independent
— see ADR-0012. A Host in `Running` can coexist with individual modules in
`Failed`; querying "is the platform up" (Host state) and "is this module
healthy" (`ModuleState`, via `IModuleLifecycleManager`) are two different
questions with two different answers, and this state machine does not attempt
to derive one from the other.

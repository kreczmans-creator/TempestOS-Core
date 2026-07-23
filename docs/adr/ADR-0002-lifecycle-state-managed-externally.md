# ADR-0002: Lifecycle State Is Managed Externally, Not On the Module

## Status

Accepted — WP 2.3 (Runtime Lifecycle), 2026-07-22.

## Context

WP 2.3 needed to track each module's progress through a ten-value state machine
(`Discovered`, `Registered`, `Initialising`, `Initialised`, `Starting`, `Running`,
`Stopping`, `Stopped`, `Disabled`, `Disposed`, `Failed`). Two obvious places existed
to store that state: on `RuntimeModule` itself (rejected — see ADR-0001), or on the
module instance the developer writes (e.g., an `IModule` implementation exposing its
own `CurrentState` property).

The second option was seriously considered. It has real appeal: the module "knows
what it's doing," and a module author debugging their own code can simply inspect
`this.State` from inside their own methods.

## Decision

State is owned entirely by `ModuleLifecycleManager`, in a private, per-module
`TrackedModule` record that never leaves the class. Neither `IModule` nor
`IModuleLifecycle` exposes any state property at all. A module implementation has
no way to know, ask, or claim what lifecycle state it is in — it only implements
`InitialiseAsync`/`StartAsync`/`StopAsync`/`DisposeAsync`, and the manager decides,
externally, when to call each one and what to record about the outcome.

Callers observe state exclusively through `IModuleLifecycleManager.GetState(id)` or
`.Modules` (a snapshot collection of `ModuleLifecycleStatus`).

## Consequences

**Positive:**

- **A single source of truth.** There's exactly one place a bug in state tracking
  could live. If a module implementation could also expose its own state, two
  representations could disagree — module says `Running`, manager says
  `Initialised` — and nothing would prevent it, since two independent
  implementations own two independent pieces of memory.
- **Modules can't lie, corrupt, or race the state machine.** Because a module has
  no reference to its own tracked state, it cannot set itself to `Running` before
  `StartAsync` has actually completed, and a badly-behaved module can't corrupt
  the manager's bookkeeping even by accident.
- **The state machine's invariants are enforced in one place.** Validity of a
  transition (`InitialiseModuleAsync` requires `Registered`; `StartModuleAsync`
  requires `Initialised`; etc.) is a property of `ModuleLifecycleManager`'s code,
  not a convention module authors have to remember to respect.
- **Testability.** A test can assert on `lifecycleManager.GetState(id)` without
  needing the module fixture itself to expose or track anything beyond its actual
  behaviour.

**Negative:**

- A module cannot easily answer "am I currently running?" from inside its own
  `StopAsync`, for instance, without the manager telling it. In practice this has
  not mattered: a module always knows what phase it's in because that phase is the
  method currently executing.
- If two independent `ModuleLifecycleManager` instances were ever constructed over
  the same `IRuntimeModuleManager` (not currently possible via any supported code
  path, but not structurally prevented either), each would maintain its own,
  independent state for the same modules — see ADR-0002's "Future Considerations."

## Future Considerations

If genuine cross-cutting visibility into lifecycle state is needed outside
`ModuleLifecycleManager` (for a health-check subsystem, for example), the correct
extension is a read-only observer interface the health subsystem depends on — not
giving modules write access to their own state. Multiple-manager scenarios, if ever
required, would need an explicit design decision about which manager is
authoritative; today, exactly one `ModuleLifecycleManager` per `IRuntimeModuleManager`
is the only supported and tested configuration.

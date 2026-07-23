# ADR-0001: RuntimeModule Is Immutable

## Status

Accepted — WP 2.2 (Runtime Module Manager), 2026-07-22.

## Context

WP 2.2 introduced `RuntimeModule`, the runtime representation of a module once it
has been registered with the `RuntimeModuleManager`. At the point this type was
designed, the eventual WP 2.3 (Runtime Lifecycle) was already anticipated: a module
would need to move through a sequence of states (`Registered` → `Initialising` →
`Initialised` → `Starting` → `Running` → ...), and something would need to hold and
update that state over the module's life.

The obvious, minimum-friction design was to give `RuntimeModule` a settable `State`
property (or an internal setter) and let whichever component drives the lifecycle —
not yet designed at the time — mutate it directly, in place, as the module
progresses.

This was rejected before it was written.

## Decision

`RuntimeModule` is a sealed, immutable class. Every property (`Descriptor`, `State`,
`RegisteredAt`, `FailureReason`) is get-only, and the constructor is `internal` —
only `RuntimeModuleManager` may create instances. There is no way, from outside
`Tempest.Core`, to construct a `RuntimeModule` with arbitrary field values, and no
way, from anywhere, to change one after construction.

`RuntimeModule.State` therefore reflects the module's state *at the moment it was
registered*, permanently. It does not track ongoing lifecycle progress. That
responsibility was later given to WP 2.3's `ModuleLifecycleManager`, which
maintains its own internal, mutable tracking (a private `TrackedModule` type) and
exposes point-in-time snapshots through a separate, different public type,
`ModuleLifecycleStatus` — see ADR-0002.

## Consequences

**Positive:**

- `RuntimeModuleManager` — the WP 2.2 component responsible for *registering*
  modules — never has to reason about concurrent lifecycle mutation happening to
  objects it owns. A `RuntimeModule` handed out via `Get`/`TryGet`/`GetAll` is safe
  to hold, compare, and read from any thread, forever; its value can never change
  underneath the caller.
- The two concerns — "what modules are registered" (WP 2.2) and "what state is
  each module in right now" (WP 2.3) — are structurally prevented from merging
  back together, even under time pressure or a future contributor's convenience
  urge. You cannot accidentally start mutating `RuntimeModule.State` from lifecycle
  code, because there is no setter to call.
- Testing is simpler: a `RuntimeModule` in a test is a plain, comparable value.
  There is no need to worry about a background operation mutating it mid-assertion.

**Negative:**

- A second type, `ModuleLifecycleStatus`, was required to represent "current state"
  once WP 2.3 arrived — some structural duplication (both types carry a
  `Descriptor`, a `ModuleState`, and a `FailureReason`) was accepted as the price of
  keeping registration and lifecycle genuinely decoupled.
- `RuntimeModule.State`'s name is arguably slightly misleading in hindsight: a
  reader could reasonably expect it to reflect "current" state given the property
  is literally named `State`. The XML documentation is explicit that it does not,
  but this is a real, acknowledged sharp edge rather than a non-issue.

## Future Considerations

If a future work package needs to attach genuinely mutable, frequently-updated
runtime data to a module (health status, live metrics, active connection counts),
that data should live in its own manager/type — following the same pattern
`ModuleLifecycleManager`/`ModuleLifecycleStatus` established — rather than adding
mutability back onto `RuntimeModule`. See the Health/Diagnostics stages of the
intended pipeline (Discovery → Registration → Lifecycle → DI → Health →
Diagnostics).

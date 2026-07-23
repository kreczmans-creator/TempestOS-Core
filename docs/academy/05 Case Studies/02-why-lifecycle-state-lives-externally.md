# Case Study: Why Lifecycle State Lives Externally

*Companion to ADR-0002.*

## Original Problem

WP 2.3 needed to track, for every registered module, which of ten lifecycle
states it currently occupied, and needed to prevent illegal transitions between
them (starting a module that was never initialised; disposing something twice).
Two natural places existed to put that tracking: on the module itself (something
an `IModule`/`IModuleLifecycle` implementation exposes about its own state), or
externally, in the component orchestrating the lifecycle.

## Alternative Designs

**Option A — Self-reporting modules.** Extend `IModuleLifecycle` (or `IModule`)
with a `ModuleState CurrentState { get; }` property. Each module implementation
tracks its own state internally (perhaps via a protected base class helper) and
reports it when asked. `ModuleLifecycleManager` reads `module.CurrentState`
whenever it needs to know where a module currently is, and updates it (or asks
the module to update it) as operations complete.

This has an appealing symmetry: the module *is* the thing doing the work
(`InitialiseAsync`, `StartAsync`), so it seems natural for it to also know and
report what phase of that work it's in — much like an object that "knows its own
mind."

**Option B — Externally-owned state.** `IModule`/`IModuleLifecycle` expose no
state at all. `ModuleLifecycleManager` alone decides what state each module is
in, tracks it privately, and is the sole authority that transitions it — modules
only ever implement the four behavioural methods and never see, touch, or report
their own state.

## Reasoning

Option A fails a basic trust test the moment you ask: what stops a badly written,
or simply buggy, module from reporting a state that isn't actually true? If
`CurrentState` is a property the module implementation controls, nothing prevents
a module from reporting `Running` before its own `StartAsync` has actually
finished — or from never updating it at all, leaving it permanently `Discovered`
regardless of what has actually happened. The moment state-reporting is delegated
to arbitrary third-party code (any module author, on any team, at any point in
the future), the manager loses the one thing a state machine exists to guarantee:
that "the state" and "reality" cannot diverge.

There's a second, subtler issue: Option A doesn't actually remove any work from
`ModuleLifecycleManager` — it *duplicates* it. The manager still has to decide
when to call `InitialiseAsync`, `StartAsync`, and so on, in the right order, with
the right preconditions checked. All Option A adds is a *second*, module-owned
copy of "what state are we in," which now has to somehow stay synchronised with
the manager's own understanding, with no mechanism enforcing that synchronisation
beyond hoping every module author gets it right, every time.

Option B has an immediate, obvious cost: a module cannot ask "am I currently
running?" from inside its own code. In practice, this turned out not to matter —
a module always knows what phase it's in, because that phase *is* the method
currently executing. Code inside `StopAsync` doesn't need to ask "is my state
`Running`?" — the fact that `StopAsync` is executing at all already answers that
question, because `ModuleLifecycleManager` guarantees it only calls `StopAsync`
when the state was `Running` a moment ago.

## Decision

State lives entirely inside `ModuleLifecycleManager`, in a private
`TrackedModule` record that never leaves the class. Neither `IModule` nor
`IModuleLifecycle` exposes any state-related member at all. The only way to
observe a module's lifecycle state, from anywhere, is
`IModuleLifecycleManager.GetState(id)` or `.Modules` — both read-only, both
sourced from the one place state is actually tracked.

## Outcome

Every state transition in the system has exactly one implementation to audit:
`ModuleLifecycleManager.TransitionAsync`'s guard (`if (tracked.State !=
requiredState) throw ...`) and `DisposeModuleAsync`'s equivalent guard. There is
no second copy of "current state" anywhere that could disagree with it. The
dedicated "Invalid transitions" test category (three separate tests: re-
initialising an already-initialised module, starting a never-initialised one,
disposing an already-disposed one) exercises exactly this guarantee, and none of
those tests need to reason about, or set up, any state on the *module* itself —
because the module has no state to set up. The fixture modules
(`RecordingLifecycleModuleAlpha` and its siblings) are, deliberately, as simple as
possible: they know how to do their job, and nothing about what phase of doing it
they're currently in.

The one cost predicted at design time — a module can't introspect its own current
lifecycle phase — was accepted and, in practice, never missed: nothing in four
work packages' worth of implementation has ever needed a module to ask the
question "what state am I in?" of itself.

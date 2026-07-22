# ADR-0004: Dispose Is Permitted From Every State Except Disposed

## Status

Accepted — WP 2.3 (Runtime Lifecycle), reviewed and confirmed under architectural
review, 2026-07-22.

**Update, WP 2.7 (Runtime Host Architecture):** this ADR's reasoning was
reused, at the Host level, for the Host's own `Faulted → Disposed` transition
in *Runtime State Machine.md* — a Host that faults during startup still
attempts to dispose whatever platform services were already brought up,
following exactly this ADR's logic (permissive disposal, restricted only
against an already-`Disposed` terminal state) applied one level up from
individual modules to the Host itself. No new ADR was created for this reuse;
see ADR-0009's own WP 2.6 update for the precedent of citing rather than
duplicating a reused principle.

## Context

`Initialise`, `Start`, and `Stop` each have exactly one valid precondition state
(`Registered`, `Initialised`, and `Running`, respectively) — attempting them from
any other state throws `InvalidModuleLifecycleTransitionException`. `Dispose` was
initially designed the same way, but this was revisited during implementation:
what precondition state should `Dispose` require?

The strict, symmetrical answer would be to require the module to be `Stopped`
(having completed the full happy path) before allowing disposal. This was
rejected during architectural review, which specifically asked: does disposing a
module that was `Registered` but never `Initialised` make sense?

## Decision

`DisposeModuleAsync` is valid from any `ModuleState` other than `Disposed` itself —
including `Registered` (never initialised), `Failed`, and every transient state.
Only calling it a second time, once a module is already `Disposed`, throws.

This is sound specifically *because of* ADR-0003: since a module is never
instantiated until `InitialiseAsync` runs, a `Registered` module holds no
constructed instance and therefore no resources — disposing it is a pure
bookkeeping transition to `Disposed`, invoking no user code at all.

## Consequences

**Positive:**

- **Unconditional shutdown sweeps work.** A caller can call `DisposeAllAsync` at
  any point — mid-startup, after a partial failure, before `InitialiseAllAsync` was
  ever called at all — and every tracked module ends up `Disposed`, without the
  caller needing to special-case "modules that never got far enough to dispose."
  This is the single largest practical benefit: shutdown code does not need to
  reason about how far startup progressed.
- **Terminal-state discipline.** Once `Disposed`, a module cannot be re-initialised
  (`Initialise` requires `Registered`), so `Dispose` doubles as "decommission this
  module, permanently" — consistent with how `IDisposable`/`IAsyncDisposable`
  behave throughout the wider .NET ecosystem: disposing something you never used
  is always legal, and the caller is trusted not to dispose something they still
  need.
- **No special-casing inside `ModuleLifecycleManager` itself.** A single guard
  (`if (tracked.State == ModuleState.Disposed) throw ...`) handles every case; there
  is no branching logic distinguishing "disposing a never-started module" from
  "disposing a stopped one."

**Negative:**

- A caller could accidentally dispose a module they intended to initialise later,
  permanently foreclosing it. This is judged to be an acceptable, standard
  `IDisposable` risk — the same responsibility every consumer of any disposable
  .NET type already carries — rather than a defect specific to this design.
- The asymmetry (`Dispose` permissive, the other three strict) is a genuine
  departure from a simpler, fully-symmetrical rule, and needs to be understood
  by anyone reading the state machine for the first time. This is why it is
  documented in three places: this ADR, an inline comment at the guard clause
  itself, and the accompanying case study.

## Future Considerations

If a future work package introduces a `Reset()`/`Recover()` operation (see the
roadmap note under the Failed state), it should be designed with this same
question in mind: from which states should recovery be legal, and does allowing it
from `Failed` (as opposed to only from terminal-but-not-yet-disposed states)
introduce new invariants that need protecting? That work does not need to
reproduce this ADR's reasoning from scratch, but should reference it.

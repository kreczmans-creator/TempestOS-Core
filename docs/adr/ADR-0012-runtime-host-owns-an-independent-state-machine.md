# ADR-0012: The Runtime Host Owns Its Own, Independent State Machine

## Status

Accepted — WP 2.7 (Runtime Host Architecture), 2026-07-22. Architecture only;
no code changes accompany this decision.

## Context

TempestOS already has a module-level state machine (`ModuleState`, WP 2.2/2.3):
`Discovered → Registered → Initialising → Initialised → Starting → Running →
Stopping → Stopped → Disposed`, with `Failed` reachable from most states. It
would be tempting to let the Host's own "state" simply be a derived summary of
all tracked modules' states — for example, "the Host is Running once every
module is Running," with no separate state machine of its own.

This does not hold up. A Host can be legitimately mid-startup while modules
are in a mix of states (some `Initialised`, some still `Initialising`); a Host
can be `Running` — meaning the module pipeline completed its startup sequence
— while individual modules sit in `Failed`, by design, per WP 2.3's per-module
failure isolation (see ADR-0013). "All modules are in the same state" is
neither a true precondition for, nor a reliable derivation of, any single Host
state.

## Decision

The Runtime Host has its own state machine — `Created`, `Starting`, `Running`,
`Stopping`, `Stopped`, `Faulted`, `Disposed` (see *Runtime State Machine.md*) —
tracked independently of, and never derived by aggregating, individual
`ModuleState` values. The Host's state answers "what phase of its own
lifecycle is the Host in"; `IModuleLifecycleManager.GetState(id)` continues to
answer "what phase is this specific module in." These are two different
questions, asked at two different levels, and this ADR keeps them two
different pieces of state.

## Consequences

**Positive:**

- The Host can meaningfully be `Running` with some modules `Failed` — exactly
  the behaviour ADR-0013 requires — without that being a contradiction, because
  "Running" is a statement about the Host's own orchestration having reached
  its steady state, not a claim about every module's individual health.
- Diagnosing "is the platform up" and "is this specific module healthy" remain
  two independently answerable questions, each backed by its own, purpose-built
  state representation — consistent with how `RuntimeModule` (registration)
  and `ModuleLifecycleStatus` (lifecycle) were deliberately kept as two
  separate types rather than one (ADR-0001, ADR-0002).
- The Host's state machine can evolve independently of `ModuleState` — for
  example, if the Host ever gains hosted-service support, a new Host-level
  state or sub-phase can be added without touching `ModuleState` at all, and
  vice versa.

**Negative:**

- Two state machines exist in the platform now, at two different levels, and a
  reader needs to know which one a given piece of code or documentation is
  talking about. This is judged to be a necessary consequence of the two
  states genuinely meaning different things, not avoidable complexity.
- The Host's state does not automatically reflect module health at a glance —
  a consumer wanting to know "are all modules actually healthy" must query
  `IModuleLifecycleManager` directly (for example, via its `Modules` snapshot),
  not infer it from the Host's own state alone.

## Future Considerations

If a future diagnostics or health-check service (see the Platform Service
Map's planned entries) needs a single, combined view of "Host state plus
module health," that combined view should be a new, additive projection over
both state machines — it should not collapse them back into one, which would
reintroduce the exact ambiguity this ADR avoids.

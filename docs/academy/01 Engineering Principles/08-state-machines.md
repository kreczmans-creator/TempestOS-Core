# State Machines

## What

A state machine models a system as a finite set of named states, together with a
defined set of legal transitions between them. At any moment, the system is in
exactly one state; it can only move to another state via an explicitly permitted
transition, and any attempted transition not on that list is, by definition,
invalid.

## Why

Systems that have distinct phases of life (not yet started, starting, running,
stopping, stopped) are frequently modelled *implicitly*, through a scatter of
boolean flags (`isInitialised`, `isRunning`, `isDisposed`) checked ad hoc,
wherever a developer happened to think a check was needed. This implicit
approach has a specific, recurring failure mode: it is easy to reach an
impossible combination of flags (`isRunning = true` and `isDisposed = true`
simultaneously) that the code never explicitly considered, because nothing
enumerates the legal *combinations* — only individual flags are ever checked in
isolation. An explicit state machine makes the legal states, and only the legal
states, representable at all, and makes every transition a deliberate, checked
decision rather than an implicit side effect of setting one flag among several.

## Benefits

- Impossible states become genuinely unrepresentable, not merely avoided by
  convention — if `ModuleState` only has one value at a time, there is no way to
  accidentally have "running" and "disposed" both be true simultaneously, because
  there is no such combination to accidentally construct.
- Every transition is a single, auditable decision point: "is the current state
  the one this operation requires?" — one question, one place, rather than a
  scattered set of flag checks that each need to independently get the logic
  right.
- The state machine's diagram *is* the documentation of the system's lifecycle —
  a reader can see the whole set of legal phases and moves at a glance, rather
  than reconstructing them by reading every method that touches every flag.

## Disadvantages

- A state machine formalism can be overkill for something with only two states
  (on/off) where a single boolean genuinely is sufficient and clearer.
- Modelling every nuance of a complex process as states can produce a
  combinatorially large number of states and transitions if not kept disciplined
  — the value comes from the *states being genuinely distinct and meaningful*,
  not from maximising how many there are.

## When to Use

Any time a system or object has more than two meaningfully distinct phases of
life, where certain operations are only valid during certain phases, and where
getting the phase wrong would cause a real bug (calling `Start` before
`Initialise` has completed; disposing something twice).

## When Not to Use

For simple, two-state, symmetrical on/off conditions with no meaningful
intermediate phases and no operations that are only valid in one state versus the
other — a plain boolean is clearer and the state-machine formalism would add
ceremony without adding safety.

## How TempestOS Applies It

`ModuleState` (WP 2.2, expanded WP 2.3) is an explicit, ten-value state machine:
`Discovered → Registered → Initialising → Initialised → Starting → Running →
Stopping → Stopped → Disposed`, with `Failed` reachable from any non-terminal
state and `Disabled` reserved for future work.

`ModuleLifecycleManager` enforces this as a genuine state machine, not just an
enum used loosely:

- Every transition method (`InitialiseModuleAsync`, `StartModuleAsync`,
  `StopModuleAsync`) checks the module's *current* state against the *one*
  precondition state that operation requires, and throws
  `InvalidModuleLifecycleTransitionException` — carrying the module's actual
  state and the attempted operation — for anything else.
- `DisposeModuleAsync` is the sole, deliberate exception to "exactly one
  precondition state": it is legal from *any* state except `Disposed` itself —
  see ADR-0004 and its accompanying case study for the full reasoning behind that
  asymmetry.
- The full lifecycle enumeration (including states like `Stopping`, `Stopped`,
  and `Disabled` that WP 2.2 didn't yet exercise) was established *before* WP 2.3
  needed all of it, specifically so `RuntimeModule`/`ModuleState` would have "a
  stable API for future releases" — an explicit, deliberate application of
  designing the state machine's shape ahead of the code that would exercise every
  state, rather than growing the enum piecemeal as each new state became
  necessary.

The dedicated unit test category "Invalid transitions" (WP 2.3's requirement #7,
tested directly via the `internal` per-module methods rather than only through
the batch orchestration) exists specifically to prove the state machine actually
rejects illegal moves, not merely that it happens to behave correctly along the
one happy path every other test exercises.

## Key Takeaway

A state machine's value is in making illegal states and illegal transitions
*impossible to reach silently* — `ModuleLifecycleManager` doesn't merely avoid
calling `StartAsync` on a module that hasn't been initialised; it makes doing so
throw a specific, descriptive exception, every time, by construction.

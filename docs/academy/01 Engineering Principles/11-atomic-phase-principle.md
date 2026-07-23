# Atomic Phase Principle

## What

**Runtime lifecycle phases shall be atomic. Once a phase begins, it either
completes successfully or fails. External cancellation shall only be observed
*between* phases, never in the middle of one — ensuring the system never
occupies an indeterminate intermediate state.**

This principle draws a deliberate distinction between two terms that are easy
to conflate, and conflating them is exactly what causes the false alarm this
document originally raised (and ADR-0018 later resolved — see below):

- A **lifecycle phase** is a named, ordered step in a sequence — a startup
  step, a shutdown step, a stage of a longer pipeline. A phase answers "where
  are we in the sequence"; it exists for scheduling and observability, and its
  boundaries are drawn by whoever designs the sequence. A phase may be coarse
  or fine, and — critically — a phase may itself be a *batch* of many smaller
  units of work, not a single one.
- An **atomic operation** is the actual indivisible unit of work this
  principle governs: it either completes in full or fails in full, with no
  observable intermediate state. It is the only granularity at which
  "cancellation is observed only between, never during" is a meaningful,
  checkable claim.

A phase and an atomic operation coincide exactly when a phase's work is a
single, indivisible call — in that case there is nothing smaller inside the
phase for cancellation to land in the middle of. They do not coincide when a
phase's work is a batch over multiple items: the phase, taken as a whole,
need not be uninterruptible — what must be uninterruptible is each
constituent atomic operation within it, with cancellation observed only at
the boundary between them. The principle governs atomic operations. "Phase"
is a label for how they're grouped and sequenced, not a synonym for them.

## Why

A system built from atomic phases has a small, enumerable set of states it
can ever actually be in — one state per phase boundary. A system that allows
cancellation (or any other interruption) to land *inside* a phase has, in the
worst case, as many possible states as there are points within that phase
where the interruption could have landed. Every one of those extra states is
a state someone has to reason about, test, and recover from — usually without
having designed for it explicitly, because "cancelled 40% of the way through
phase X" is rarely a state anyone sat down and thought through on purpose.

## Benefits

- **A small, closed set of states to reason about.** "Which phase completed
  last" is always a well-defined, answerable question — never "phase X was
  running, but I don't know how far it got."
- **Cleanup logic doesn't need to handle a combinatorial explosion of partial
  states.** If a phase is atomic, recovering from an interruption only ever
  means "phase X never happened" or "phase X fully happened" — not "phase X
  happened to items 1 through 7 of 12."
- **Testing is simpler.** A test for "what happens if this phase is
  cancelled" only needs to assert one of two outcomes, not a spectrum of
  partial-completion states depending on timing.
- **Diagnosability improves.** An operator or a log reader can always answer
  "what was the system doing when it stopped" with a single phase name, not a
  phase name plus an uncertain fraction of it.

## Disadvantages

- **Coarser cancellation responsiveness.** A long-running phase cannot be
  interrupted quickly — cancellation has to wait for the phase to finish (or
  fail) before it's honoured, even if the request to stop arrived near the
  very start of a slow phase. This is a real, deliberate trade of
  responsiveness for simplicity, not a free win.
- **Phase boundaries have to be drawn deliberately, and mistaking a phase for
  an atomic operation is an easy way to draw them wrong.** A phase that is
  actually a batch of many atomic operations does not, itself, need to be
  uninterruptible — but each atomic operation within it does, and it is easy
  to misjudge which of the two is being interrupted. See "How TempestOS
  Applies It," below, for a worked example (`ModuleLifecycleManager`) where
  getting this distinction right, rather than wrong, is what makes an
  apparently coarse phase still fully compliant.
- **Not free for genuinely long or genuinely resumable work.** An operation
  that is naturally idempotent, checkpoint-able, or expected to run for a very
  long time may be a poor fit for strict atomicity — forcing it to be atomic
  either makes it uninterruptible for an unacceptably long time, or forces an
  artificially small phase size that reintroduces overhead without adding real
  safety.

## When to Use

Startup and shutdown sequences (the case this principle was first written
for); any batch or pipeline where partial completion would be strictly worse
than either full completion or no completion at all; anywhere the system's
own diagnosability depends on always being describable as "at phase N," not
"somewhere inside phase N."

## When Not to Use

Long-running, naturally interruptible work where responsiveness matters more
than atomicity, and where partial progress is genuinely fine, expected, and
recoverable (a large file transfer with resumable checkpoints, a long
calculation that can save intermediate results). Forcing atomicity onto
this class of work trades away its main advantage — the ability to stop
promptly and resume from where it left off — for a guarantee it doesn't
actually need.

## How TempestOS Applies It

**The Host's own six coarse startup phases are each a single atomic
operation.** Configuration Built, Logging Built, Module Discovery, Module
Registration, Platform Services Registered, and Dependency Injection Built
(*Host Lifecycle.md*) are each a single, indivisible call from the Host's own
point of view — `ConfigurationBuilder.Build()`, `LoggerFactory`'s
construction, `DiscoverModules()`, and so on. Phase and atomic operation
coincide exactly here: none of them is a batch over many items that
cancellation could land in the middle of; each either returns or throws, in
full, with nothing in between. The Host's own 7-state machine (ADR-0012) is,
as a direct consequence, always in one of its seven well-defined states, and
ADR-0018 gives cancellation arriving during `Starting` one single,
deterministic reaction (transition to `Stopping`).

**Module Initialisation is a phase that is a batch of atomic operations, not
a single one — and that distinction is what resolves an apparent tension
this document originally flagged.** `ModuleLifecycleManager.RunBatchAsync` —
the shared loop `InitialiseAllAsync`/`StartAllAsync`/`StopAllAsync`/
`DisposeAllAsync` all funnel through (WP 2.3, already shipped) — checks the
cancellation token at the top of *each iteration* of its loop over modules:

```
foreach (var tracked in modules)
{
    cancellationToken.ThrowIfCancellationRequested();
    ...
}
```

Read naively, treating "Module Initialisation" the phase as if it were itself
one atomic operation, this looks like a violation: if cancellation fires
after three of ten modules, the remaining seven are never attempted, leaving
the phase "half-done." But that reading conflates phase with atomic
operation. Read correctly — per ADR-0018's Terminology section — the atomic
operation here is one module's own initialise call, not the whole batch. The
loop's cancellation check sits *between* iterations, before the next atomic
operation begins, never inside one. That is precisely what this principle
requires: cancellation observed only at the boundary between atomic
operations. `ModuleLifecycleManager` was already correct at the grain that
matters; the earlier apparent conflict was a terminology gap, not a code
defect, and `ModuleLifecycleManager` remains unchanged — see ADR-0018's
2026-07-22 update for the full resolution.

## Key Takeaway

This principle is valuable well beyond the Host — batch document processing,
long-running calculations, and plugin execution are all named, up front, as
places it should guide future design. Applying it correctly depends on never
conflating a *lifecycle phase* (a named step in a sequence, which may be a
batch) with an *atomic operation* (the actual indivisible unit of work the
principle governs). A phase built from many atomic operations is still
principle-compliant so long as cancellation is observed only between those
operations, never inside one — exactly what `ModuleLifecycleManager` already
does.

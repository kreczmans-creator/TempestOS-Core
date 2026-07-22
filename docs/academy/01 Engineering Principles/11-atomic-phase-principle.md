# Atomic Phase Principle

## What

**Runtime lifecycle phases shall be atomic. Once a phase begins, it either
completes successfully or fails. External cancellation shall only be observed
*between* phases, never in the middle of one — ensuring the system never
occupies an indeterminate intermediate state.**

A "phase" here is any named, discrete step in a sequenced lifecycle — a
startup step, a shutdown step, a stage of a longer pipeline — that is small
enough to reason about as a single unit, and whose *partial* completion would
leave the system in a state nobody explicitly designed for. The principle
says: don't let that partial state be reachable. A phase is either fully done
or it never started; cancellation is a decision made at the boundary between
phases, not an interruption injected into the middle of one.

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
- **Phase boundaries have to be drawn deliberately, and can be drawn wrong.**
  Too coarse, and a "phase" quietly contains a lot of internal structure
  (multiple sub-operations over multiple items) that can *itself* be
  interrupted partway through, silently reintroducing the exact problem the
  principle exists to prevent — see this document's own honest account of
  where that has already happened, below.
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

## How TempestOS Applies It — Including an Honest Account of Where It Doesn't Yet, Fully

**The Host's own six coarse startup phases already satisfy this principle
cleanly.** Configuration Built, Logging Built, Module Discovery, Module
Registration, Platform Services Registered, and Dependency Injection Built
(*Host Lifecycle.md*) are each a single, indivisible operation from the
Host's own point of view — `ConfigurationBuilder.Build()`,
`LoggerFactory`'s construction, `DiscoverModules()`, and so on. None of them
is a loop over many items that cancellation could land in the middle of; each
either returns or throws, in full, with nothing in between. The Host's own
7-state machine (ADR-0012) is, as a direct consequence, always in one of its
seven well-defined states, and ADR-0018 gives cancellation arriving during
`Starting` one single, deterministic reaction (transition to `Stopping`) —
by every measure at the Host's own level of reasoning, this principle already
holds.

**Module Initialisation (and Stop/Dispose) is where this needs an honest
flag, not a quiet assumption.** `ModuleLifecycleManager.RunBatchAsync` — the
shared loop `InitialiseAllAsync`/`StartAllAsync`/`StopAllAsync`/`DisposeAllAsync`
all funnel through (WP 2.3, already shipped) — checks the cancellation token
at the top of *each iteration* of its loop over modules:

```
foreach (var tracked in modules)
{
    cancellationToken.ThrowIfCancellationRequested();
    ...
}
```

If cancellation fires after three of ten modules have been processed, the
remaining seven are never even attempted — they stay `Registered`, untouched.
Read strictly, against this principle's own wording ("once a phase begins, it
either completes successfully or fails"), "Module Initialisation" — a single
row in *Host Lifecycle.md*'s phase table — can be left exactly half-done. This
is a genuine tension between an already-shipped design and a principle being
adopted after the fact, not a hypothetical.

There are two honest ways to read this, and this document takes a position on
neither, deliberately:

1. **The Host-level reading**, under which the principle is already
   satisfied: the *Host's own* observable state is always one of its seven
   well-defined values, and its reaction to cancellation is always
   deterministic (ADR-0018) — `ModuleState` is explicitly independent of Host
   state (ADR-0012), so a mix of `Initialised` and `Registered` modules is not
   an "indeterminate Host state," it is simply module-level detail the Host
   was never claiming to make uniform in the first place.
2. **The strict, per-batch reading**, under which "Module Initialisation" as
   named in *Host Lifecycle.md* is not atomic, and either the phase needs
   redefining at a finer grain (each module's own turn *is* the atomic unit;
   cancellation is observed *between* modules, which is arguably already what
   `RunBatchAsync` does — cancellation is checked between iterations, not
   mid-module), or `RunBatchAsync`'s cancellation check needs to move so that
   an already-started batch always finishes every module's turn before
   cancellation is honoured.

**This is flagged here, deliberately, rather than resolved.** Reconciling it
means either reinterpreting what "phase" means for a batch operation (a
documentation-only decision) or changing already-shipped, tested
`ModuleLifecycleManager` behaviour (a code decision, outside a documentation
update's scope). It is recommended that this be settled by a dedicated ADR
before any future work package (Project Engine, Requirements Engine, document
processing, plugin execution) adopts this principle as settled fact for its
own batch operations — adopting a principle without first resolving a known
counterexample to it would undermine the very discipline the principle is
meant to enforce.

## Key Takeaway

This principle is valuable well beyond the Host — batch document processing,
long-running calculations, and plugin execution are all named, up front, as
places it should guide future design. But its value depends entirely on
"phase" being defined at the right granularity every time it's applied, and
on cancellation being checked *only* at those boundaries — get the boundary
wrong even once, as `ModuleLifecycleManager`'s existing per-module loop
arguably does, and the principle quietly stops holding without anyone having
decided that it should.

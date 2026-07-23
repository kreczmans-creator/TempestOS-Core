# WP 2.3 — Runtime Lifecycle

## 1. Introduction

WP 2.3 introduced deterministic lifecycle control: initialisation, startup,
shutdown, and disposal, for every module `RuntimeModuleManager` (WP 2.2) knows
about. It is the point in the pipeline where TempestOS's modules stop being
inert catalogue entries and start actually *doing something* — a module's
`InitialiseAsync`, `StartAsync`, `StopAsync`, and `DisposeAsync` methods are, for
the first time, genuinely invoked by the runtime, in a genuinely enforced order,
with genuinely enforced preconditions.

This work package produced the single largest concentration of deliberate,
defensible asymmetry in TempestOS's design — the permissive `Dispose` precondition
— and is the work package whose architectural review exchange (preserved in
Case Study 03) most directly demonstrates how this Academy is meant to be used:
not as a rubber stamp, but as a place where a specific, falsifiable question gets
asked and answered before a design is accepted.

## 2. Purpose

To give TempestOS "the single orchestration point for module execution" (the
brief's own words): one component responsible for deciding when each registered
module's lifecycle methods run, in what order, with what happens on failure, and
with cancellation respected throughout.

## 3. Background

By the time WP 2.3 began, WP 2.1 (discovery) and WP 2.2 (registration) had
established a firm foundation: `ModuleDescriptor` as the record of what a module
is, `RuntimeModule` as the record of what's registered, and — critically —
`ModuleState` already existing as a WP 2.2 enum with only `Discovered` and
`Registered` exercised, but with `Initialised`, `Running`, `Disabled`, and
`Failed` already present, specifically anticipating this work package's arrival.
WP 2.3 was, in a real sense, the work package WP 2.2's design had been *waiting
for* — the moment its unexercised enum values would finally matter.

## 4. The Problem

1. **What is the actual contract a module implements to participate in
   lifecycle?** Nothing in WP 2.1/2.2 gave modules any behavioural methods at
   all — `IModule` is pure metadata.
2. **Where does mutable, changing lifecycle state live**, given WP 2.2's
   `RuntimeModule` was deliberately made immutable specifically to keep this
   question open for a later work package to answer properly? (Answered in depth
   in ADR-0002 and its case study.)
3. **What order should modules initialise and start in, and does shutdown really
   need to be the exact reverse?**
4. **What happens when a module throws** during a lifecycle operation — does it
   take down the whole batch, or only itself?
5. **How does cancellation actually propagate** — does a mid-batch cancellation
   still let already-started work finish, or does it stop dead?
6. **What is a valid transition, and what isn't** — and specifically, does
   `Dispose` need the same "exactly one precondition state" rule the other three
   operations have, or does it need something different?
7. **Where does a module instance actually come from**, given nothing before this
   work package had ever kept a *persistent* module instance alive anywhere — WP
   2.1's discovery only ever instantiated modules transiently, to read metadata,
   then discarded them.

## 5. The Design

**`IModuleLifecycle`** — the behavioural contract: `InitialiseAsync`,
`StartAsync`, `StopAsync`, `DisposeAsync`, every one accepting a
`CancellationToken`. This was deliberately kept separate from `IModule` — a
module implementing only `IModule` has no lifecycle behaviour and is still
tracked through the full state progression, just with no-op transitions (nothing
is ever constructed or invoked for it). The brief's own illustrative example
showed `DisposeAsync()` without a token; the explicit, stronger requirement that
"every lifecycle operation shall accept CancellationToken" was followed instead,
as a deliberate, documented deviation from the softer, illustrative example.

**`ModuleState`**, extended additively from WP 2.2's baseline: `Initialising`,
`Starting`, `Stopping`, `Stopped`, and `Disposed` were added; `Discovered`,
`Registered`, `Disabled`, and `Failed` were left untouched, including
`Discovered`'s explicit `= 0` (protecting the existing
`ModuleState_DefaultValueIsDiscovered` test from WP 2.2).

**`ModuleLifecycleStatus`** — the answer to where lifecycle state lives: not on
`RuntimeModule` (see ADR-0002), but as its own, structurally similar but
independent public snapshot type, populated from `ModuleLifecycleManager`'s
private, mutable `TrackedModule` records.

**`IModuleLifecycleManager`** / **`ModuleLifecycleManager`** — the orchestrator.
On construction, takes an ordered snapshot (ascending, ordinal, by
`ModuleDescriptor.Id`) of every module currently registered with the supplied
`IRuntimeModuleManager`. Exposes `Modules`, `GetState(id)`, and four batch
operations — `InitialiseAllAsync`, `StartAllAsync`, `StopAllAsync`,
`DisposeAllAsync` — each iterating in the snapshot's order (`StopAllAsync` and
`DisposeAllAsync` iterate in reverse). Internally, each per-module operation
(`InitialiseModuleAsync`, and so on — `internal`, not part of the public
interface, mirroring WP 2.1's test-seam pattern) checks the module's current
state against the one precondition state that operation requires, transitions
through an intermediate "-ing" state, invokes the module's own method if one
exists, and lands on a completed state — or, on any exception other than
`OperationCanceledException`, marks the module `Failed` and rethrows.

Two exception types: `ModuleLifecycleException` (base) and
`InvalidModuleLifecycleTransitionException` (a dedicated subtype carrying the
module ID, its actual current state, and the operation that was attempted) — a
third, separate hierarchy, following the same base-plus-subtype pattern WP 2.1
and WP 2.2 each established independently for their own concerns.

## 6. Alternatives Considered

**Requiring `Dispose`'s precondition to be `Stopped`, symmetrically with the
other three operations.** This was the initial design, revisited specifically
under architectural review (preserved in full in Case Study 03). Ultimately
rejected in favour of permitting `Dispose` from any non-`Disposed` state, on the
reasoning that a `Registered`-but-never-`Initialised` module holds no constructed
instance (per ADR-0003) and therefore no resources to protect by refusing early
disposal — while permissive disposal enables unconditional shutdown sweeps that a
strict precondition would have made every caller reason about manually.

**Letting one module's failure abort an entire batch operation.** Considered and
rejected: "handle failures gracefully" was read as meaning per-module failure
isolation, not batch-wide abortion. A `RunBatchAsync` helper (later folded
directly into each `*AllAsync` method) catches every exception except
`OperationCanceledException` around each per-module call and continues to the
next module — so one broken module cannot prevent every other, healthy module
from initialising, starting, stopping, or disposing.

**Treating a "not yet eligible" module (e.g., asking to `Start` something still
`Registered`) as a batch-level error.** Rejected in favour of silent skipping at
the batch level, while still making the *per-module* method throw
`InvalidModuleLifecycleTransitionException` when called directly. This gives two
different, deliberately different behaviours for two different callers: a batch
operation naturally skips modules not yet at the right phase (there's nothing
wrong with a module still being `Registered` when `StartAllAsync` runs, if
`InitialiseAllAsync` hasn't reached it yet for some reason); a direct,
programmatic call to a specific module's transition method is a much stronger
statement of intent, and getting the precondition wrong there is treated as an
error worth surfacing loudly.

**A public, generic `Discover(IEnumerable<Type>)`-style testing overload for
lifecycle**, mirroring WP 2.1's pattern exactly. Instead, WP 2.3 used a
different testing strategy: real, small `RuntimeModuleManager` instances built
per test, with real fixture modules recording their own invocations into a
shared, resettable static log (`LifecycleTestLog`) — necessary specifically
because lifecycle fixtures are instantiated via reflection with no constructor
injection available (this is documented explicitly as a deliberate, pragmatic
choice, not an accidental one, in the WP 2.3 completion report).

## 7. Why This Solution Was Chosen

The governing design question throughout WP 2.3 was: what does a *reviewer*, not
just a compiler, need to be able to verify about this state machine? Every major
decision — the permissive `Dispose` precondition, the per-module-not-per-batch
failure isolation, the internal seam for testing invalid transitions directly —
was chosen specifically because it made the system's actual behaviour something
a human could read, reason about, and verify against a specific test, rather than
something that merely happened to work in the cases exercised so far.

## 8. Architectural Principles

- **State Machines** — the entire work package is a state machine made explicit,
  with illegal transitions structurally rejected rather than merely avoided by
  convention (see the State Machines Engineering Principle document).
- **Fail Fast** — invalid transitions throw immediately, with the actual state
  and attempted operation captured in the exception.
- **Deterministic Systems** — ascending order for startup, reverse order for
  shutdown, fixed at construction time, independent of any incidental reflection
  or dictionary-iteration ordering.
- **Separation of Concerns** — lifecycle orchestration depends on
  `IRuntimeModuleManager` through its interface only, and (from WP 2.4 onward)
  on `ITempestServiceProvider` through its interface only; it has no knowledge of
  how either one does its job internally.

## 9. Benefits

- Modules gained real, invokable behaviour for the first time in TempestOS's
  pipeline — `IModuleLifecycle` is the first interface in the system whose
  methods are actually *called* by the runtime, rather than just describing
  metadata.
- The permissive `Dispose` design directly enabled a genuinely simple shutdown
  story: `DisposeAllAsync()` is safe to call unconditionally, from any point in a
  module's life, without the caller needing to reason about how far startup got.
- The `ModuleState`/`ModuleLifecycleStatus` split (per ADR-0002) meant this
  entire work package could be built without modifying a single line of WP 2.2's
  `RuntimeModuleManager` or `RuntimeModule`.

## 10. Trade-offs

- The asymmetry between `Dispose`'s permissive precondition and the other three
  operations' strict ones is a genuine, ongoing cognitive cost for anyone reading
  the state machine for the first time — mitigated, but not eliminated, by
  documenting it in three separate places (an inline comment, this Academy, and
  ADR-0004).
- No `Reset()`/`Recover()` operation exists — once `Failed`, a module stays
  `Failed` for the lifetime of the `ModuleLifecycleManager` instance that
  tracked it, with no supported way to retry. This is a deliberate, acknowledged
  gap (see Future Evolution), not an oversight.
- `LoggingService` (the existing infrastructure this work package was required to
  integrate with) only ever supports `Information`-level logging — a duplicate
  registration or transition failure is logged at the same severity as routine
  progress, which understates the significance of a genuine failure.

## 11. Common Mistakes

The single most important mistake to understand from WP 2.3 is one that was
*caught and fixed during WP 2.4*, not during WP 2.3 itself — proof that a design
decision can look completely correct in isolation and only reveal a flaw once a
later work package changes what's actually possible at a specific call site. WP
2.3's `TransitionAsync` originally called instance construction (at the time,
`Activator.CreateInstance`) *inside* the state-transition lock, but *outside* the
`try`/`catch` block that marks a module `Failed` on exception. Under WP 2.3 alone,
this was essentially harmless — a bare `Activator.CreateInstance` call on a
parameterless-constructor type essentially never throws. Once WP 2.4 replaced
that call with dependency-injection resolution — which *can* legitimately throw
for a missing dependency, a circular dependency, or an ambiguous constructor —
the same code path could leave a module permanently stuck in a transient state
like `Initialising`, with no recorded failure reason, instead of correctly
transitioning to `Failed`. See WP 2.4's own retrospective and its completion
report for the full account of the fix. The lesson: a correctness property that
depends on "this call basically never throws" is fragile precisely because
nothing prevents a *later*, unrelated change from making it throw far more
easily — and the place to guard against that is structural (put construction
inside the failure-handling `try` block, regardless of how unlikely failure looks
today), not a mental note to "remember this is safe because X doesn't usually
fail."

## 12. Future Evolution

- **`Reset()`/`Recover()` for `Failed` modules.** Explicitly noted, during
  architectural review, as roadmap material rather than in-scope for WP 2.3.
  Any future design here needs to reconcile with ADR-0004's reasoning about which
  states permit which transitions.
- **Logging severity levels.** `LoggingService`'s `Information`-only design was
  flagged directly as something WP 2.4 (or a dedicated logging work package)
  should address before lifecycle failures need to be operationally
  distinguishable from routine progress in production logs.
- **Dedicated ordering metadata.** As with discovery's alphabetical convention,
  lifecycle's ascending-by-`Id` ordering is documented explicitly, in the
  interface's own XML remarks, as an implementation convenience rather than
  permanent design intent — a future `Priority`/`StartupOrder` property on
  `ModuleDescriptor` would change only the sort key this class is built from, not
  its public contract or behaviour.

## 13. Key Takeaways

1. A state machine's asymmetries (permissive `Dispose`) can be entirely
   deliberate and correct, but need explicit, repeated documentation precisely
   because they violate the symmetrical intuition a reader would otherwise bring
   to the code.
2. Per-module failure isolation in a batch operation is a design decision, not a
   default — it has to be built deliberately (catch, mark `Failed`, continue) and
   distinguished carefully from cancellation, which is deliberately *not*
   isolated the same way.
3. Architectural review that asks a specific, falsifiable question ("does
   Registered → Dispose make sense?") and accepts a well-reasoned "yes, and
   here's why" is at least as valuable as review that demands a change — the
   Academy exists to capture the *reasoning*, whichever way a review concludes.
4. A design's correctness can be contingent on a fact that is true *today* but
   not guaranteed to stay true as later work packages change what's possible at
   the same call site — the WP 2.4-discovered lock/try-catch bug is the clearest
   demonstration of this in TempestOS's history so far, and is exactly the kind
   of lesson this Academy's Common Mistakes sections exist to preserve.

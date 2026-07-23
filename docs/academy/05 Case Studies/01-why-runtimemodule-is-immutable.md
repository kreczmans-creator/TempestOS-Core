# Case Study: Why RuntimeModule Is Immutable

*Companion to ADR-0001.*

## Original Problem

WP 2.2 needed a runtime representation of a module once it had been registered —
something more than the bare `ModuleDescriptor` discovery produces, capable of
carrying operational metadata: what state is this module in, when was it
registered, did it fail and why.

At the time WP 2.2 was being designed, WP 2.3 (Runtime Lifecycle) did not exist
yet, but its shape was already visible on the horizon — the pipeline diagram
(Discovery → Registration → Lifecycle → Dependency Injection) had already been
discussed, and it was obvious that *something*, eventually, would need to track a
module moving through states like `Initialising`, `Running`, `Stopping`.

The question WP 2.2 actually had to answer was narrower: should `RuntimeModule` —
the type being designed *right now*, for registration — also be the type that
*later* tracks lifecycle state? Or should it just be a snapshot, with lifecycle
tracking left to whatever component eventually needs it?

## Alternative Designs

**Option A — Mutable `RuntimeModule`, settable `State`.** Give `RuntimeModule` a
`State` property with a public or internal setter. When lifecycle management
arrives (WP 2.3), it calls `runtimeModule.State = ModuleState.Initialising` and so
on, directly, on the same object `RuntimeModuleManager` already hands out to every
caller.

This is the path of least resistance. It requires no second type, no snapshot
mechanism, and lets a caller holding a `RuntimeModule` reference simply watch its
`.State` change over time, which at first glance looks like a *feature* — "live"
state, always current, no need to re-query anything.

**Option B — Immutable `RuntimeModule`, external state tracking.** Keep
`RuntimeModule` exactly as WP 2.2's minimum public API specified: `Descriptor`,
`State`, `RegisteredAt`, `FailureReason`, all get-only. Whatever WP 2.3's
eventual lifecycle manager turns out to be, it tracks state *itself*, internally,
and exposes it through its own mechanism.

## Reasoning

Option A's apparent advantage — "any caller holding a `RuntimeModule` can watch
its state change live" — is also, on inspection, its central problem. If any
caller holding a reference can *observe* live mutation, then that mutation has to
be safe to observe from anywhere, at any time, on any thread, with no
synchronisation contract specified anywhere. `RuntimeModuleManager.GetAll()`
already promised callers "a read-only snapshot" for its *collection* — but a
collection of *mutable* objects is not actually a snapshot of anything; it's a
collection of live windows into an object that something else, elsewhere, is
changing underneath the holder.

There was also a deeper problem with *ownership*. `RuntimeModuleManager`'s entire
job, per WP 2.2's brief, was registration — "the runtime module manager only owns
runtime metadata... it does not manage lifecycle execution." If `RuntimeModule`
were mutable and lifecycle code (a different class, a different work package,
written by different reasoning weeks later) reached in and mutated its `State`
directly, then registration and lifecycle would no longer be genuinely separate
concerns — they'd be two pieces of code sharing one mutable object, with no
enforcement preventing either one from stepping on the other's job. The
separation WP 2.2's brief insisted on ("runtime metadata only... does not manage
lifecycle execution") would exist only as a comment, not as something the type
system actually protected.

There was a third, more practical consideration: testability. A `RuntimeModule`
that cannot change is trivially safe to hold in a test, compare with
`Assert.Same`, and reason about — nothing else in the test process can quietly
change it mid-assertion. A mutable one would require every test to consider
"could this have changed since I captured it?"

## Decision

`RuntimeModule` was made sealed and immutable, with an `internal` constructor —
only `RuntimeModuleManager` can create one, and once created, it is fixed forever.
When WP 2.3 arrived and genuinely needed to track lifecycle state, it did not
reach into `RuntimeModule` at all. It introduced its own private tracking
(`ModuleLifecycleManager`'s internal `TrackedModule` type) and its own public
snapshot type, `ModuleLifecycleStatus`, structurally similar to `RuntimeModule`
(it also carries a `Descriptor`, a `State`, a `FailureReason`) but entirely
independent of it.

## Outcome

The separation held exactly as intended. WP 2.3 never had to touch
`RuntimeModule` or `RuntimeModuleManager` at all — not because of a rule someone
remembered to follow, but because there was no mutable hook to reach for even if
someone had been tempted to take a shortcut. `RuntimeModule.State` today still
means exactly what it meant on the day WP 2.2 shipped: the module's state *at
registration time* — which, since nothing produces a `RuntimeModule` in any state
other than `Registered`, is in practice always `ModuleState.Registered`.

The cost predicted at design time — a second, structurally similar type
(`ModuleLifecycleStatus`) — did materialise, and is an accepted, deliberate
duplication rather than a discovered problem. The alternative (mutable
`RuntimeModule`, shared and reached into by two unrelated subsystems) was judged,
correctly in hindsight, to be the more expensive path — it would have made WP 2.3
a *modification* to WP 2.2's class rather than a wholly independent addition, and
would have left the registration/lifecycle boundary enforced by nothing but
convention and code review vigilance.

# Case Study: Why Dispose Is Always Legal

*Companion to ADR-0004. This case study documents an actual architectural review
exchange from WP 2.3, preserved here because the back-and-forth itself is
instructive.*

## Original Problem

`ModuleLifecycleManager`'s three other lifecycle operations
(`InitialiseModuleAsync`, `StartModuleAsync`, `StopModuleAsync`) each have exactly
one valid precondition state: you can only initialise a `Registered` module, only
start an `Initialised` one, only stop a `Running` one. When `DisposeModuleAsync`
was being designed, the obvious, symmetrical rule would have been: only dispose a
`Stopped` module — completing the full, tidy happy path before allowing teardown.

## Alternative Designs

**Option A — Symmetrical precondition.** `Dispose` requires `Stopped`, exactly
like the other three operations require their own single precondition state.
Disposing anything else — a module that's still `Registered`, or one that
`Failed` partway through startup — throws `InvalidModuleLifecycleTransitionException`,
the same as attempting any other out-of-order transition.

**Option B — Permissive precondition.** `Dispose` is legal from any state except
`Disposed` itself. A `Registered` module that was never touched, a `Failed`
module, a `Running` module that was never explicitly stopped first — all of them
can be disposed directly.

## Reasoning

This design was implemented as Option B during WP 2.3, and then specifically
challenged during architectural review, which asked directly: *"Does disposing a
module that never Initialised make sense? For example, Registered → Dispose.
Should that happen?"* — and requested justification rather than an immediate
code change.

The justification, worked through directly: in this implementation, a module is
never actually *instantiated* until `InitialiseAsync` runs — this rests on
ADR-0003, the convention that module constructors are cheap and side-effect-free,
which in turn is what lets discovery transiently instantiate every candidate
module purely to read its metadata, without consequence. Given that convention, a
module still in the `Registered` state has, provably, no constructed instance and
therefore nothing that could hold a resource needing release. Disposing it is not
"skipping cleanup" — there is no cleanup to skip. It is a pure bookkeeping
transition to `Disposed`, and no user code runs at all (`ResolveInstance` was
never called, so `tracked.Instance` is `null`, and the `if (tracked.Instance is
not null)` guard means `DisposeAsync` is never invoked on anything).

That answered *whether it was safe*. The second half of the review question was
sharper: *"Is Dispose intended to represent (a) release runtime resources only,
or (b) transition the module into a permanent terminal state regardless of
previous lifecycle progression?"*

The honest answer, on reflection, was: both, and that hadn't been fully separated
out loud until the question forced it. (a) is trivially true for a
never-initialised module — there is nothing to release, so "release resources"
degenerates to a no-op. But (b) is the actual, deliberate reason permissiveness
was chosen: it lets `DisposeAllAsync` function as an *unconditional shutdown
sweep*. Call it at any point — mid-startup, after a partial failure, before
`InitialiseAllAsync` was ever called at all — and every tracked module ends up
`Disposed`, with no caller anywhere needing to special-case "what if this module
never got far enough to dispose normally." Once `Disposed`, a module also cannot
be re-initialised (`Initialise` requires `Registered`), so `Dispose` additionally
functions as "decommission this module, permanently" — which mirrors how
`IDisposable`/`IAsyncDisposable` behave throughout the wider .NET ecosystem:
disposing something you never used is always legal, and the caller is trusted not
to dispose something they still need.

Option A (symmetrical, `Stopped`-only) was reconsidered explicitly at this point
and rejected: it would force every caller performing a shutdown sweep to first
determine how far each module actually got, and skip — or worse, attempt to
force through the intervening states of — anything that didn't reach `Stopped`
normally. That complexity would exist purely to enforce a symmetry that, on
inspection, wasn't actually protecting anything real.

## Decision

Option B was retained, unchanged. The review's outcome was documentation, not a
code change: an inline comment was added directly above the state guard in
`DisposeModuleAsync` explaining the reasoning in the moment a future reader
encounters it, plus a note in `ModuleLifecycleManager`'s and
`IModuleLifecycleManager`'s own XML remarks, plus this case study and ADR-0004.

## Outcome

This is, deliberately, an example of the Academy's purpose working as intended:
the review didn't ask "is this right?" in the abstract — it asked a specific,
falsifiable question ("does Registered → Dispose make sense?") and requested
the *reasoning*, with an explicit statement that if the reasoning held up, no
change was needed. The reasoning did hold up, on the strength of a dependency the
original implementation hadn't stated out loud: permissive `Dispose` is only safe
*because* of ADR-0003's constructor convention. That dependency is now written
down in three places (the inline comment, this case study, and ADR-0004) instead
of living only in the head of whoever implemented it — which is precisely the gap
a future engineer, encountering this asymmetry for the first time with no
context, would otherwise have had to rediscover from scratch.

# Fail Fast

## What

Fail Fast is the principle that a system should detect and report errors as
close as possible — in time and in code location — to where they actually
originate, rather than allowing invalid state to propagate silently until it
causes a confusing failure somewhere else, possibly much later and in a
completely unrelated part of the system.

## Why

The cost of a bug is roughly proportional to the distance between where it
originates and where it's discovered. A `null` that should never have been
`null`, allowed to propagate, eventually causes a `NullReferenceException` at some
unrelated call site three layers away, with a stack trace that points at the
*symptom*, not the *cause*. Fail Fast trades a small amount of upfront defensive
checking for a large reduction in debugging time later.

## Benefits

- Errors are reported with maximum context, at the point where the actual
  invariant was violated, not at some downstream point where the original cause
  has been lost.
- Invalid states cannot silently propagate and compound — a bug is caught before
  it can cause a second, unrelated bug.
- Tests that exercise failure paths become simpler to write and more precise:
  asserting "this specific, descriptive exception was thrown, here" is far more
  useful than asserting "something eventually went wrong, somewhere."

## Disadvantages

- Overzealous fail-fast checking (validating the same invariant redundantly at
  every layer) adds noise and maintenance burden without adding real safety, if
  the invariant is already guaranteed by an earlier check.
- A system that fails fast on every minor inconsistency can be less resilient in
  production than one that degrades gracefully — fail fast is a debugging and
  correctness tool, not automatically the right choice for user-facing runtime
  behaviour, where recoverable degradation is sometimes preferable to a hard stop.

## When to Use

At the boundary where an invariant is first knowable to be violated — argument
validation at a public method's entry, a registry rejecting a duplicate the
moment it's detected, a state machine rejecting an invalid transition the moment
it's attempted. Anywhere a defect, left unchecked, would otherwise surface as a
confusing symptom far from its cause.

## When Not to Use

For expected, recoverable conditions that are a normal part of a system's
operation — a missing optional configuration value with a sensible default, a
network request that should be retried rather than immediately abandoned. Fail
Fast is for genuine invariant violations and programmer errors, not for every
condition that merely *could* be treated as an error.

## How TempestOS Applies It

Fail Fast is arguably the most pervasive principle in TempestOS's runtime code,
expressed through its consistent use of dedicated, descriptive exceptions rather
than `null` returns, silent no-ops, or generic exceptions:

- `RuntimeModuleManager.Register` throws `DuplicateModuleRegistrationException`
  the instant a duplicate ID is detected — not later, when two modules with the
  same ID cause some other confusing conflict downstream.
- `ModuleLifecycleManager`'s internal per-module methods
  (`InitialiseModuleAsync`, `StartModuleAsync`, ...) throw
  `InvalidModuleLifecycleTransitionException` the moment an operation is
  attempted from the wrong state, rather than allowing, say, `StartAsync` to run
  on a module that was never initialised and fail in some unrelated,
  module-specific way inside `StartAsync` itself.
- `TempestServiceProvider` was explicitly required (WP 2.4, requirement #7) to
  make resolution failures maximally informative — identifying the requested
  service, the specific missing/circular/ambiguous type, and the full
  construction chain — at the exact point resolution fails, rather than letting a
  vague `NullReferenceException` or `MissingMethodException` surface somewhere
  inside a module's own code, far from the actual cause (a missing registration).
- Argument validation (`ArgumentNullException.ThrowIfNull`, explicit
  `ArgumentException` for a blank descriptor ID) happens at the entry to public
  methods across every work package, consistently, so a caller passing invalid
  input finds out immediately, at the call site, rather than via some later,
  unrelated failure.

The consistent rule across the whole codebase is: a **dedicated exception type for
each genuinely distinct failure**, thrown at the earliest point the failure is
knowable — never a generic `Exception`, `InvalidOperationException`, or a
swallowed `null`.

## Key Takeaway

Fail Fast is why TempestOS has so many small, specific exception types rather
than a handful of generic ones — each one exists to report a *specific* invariant
violation at the *exact* point it's first detectable, so the exception message
alone tells you what actually went wrong, without needing to trace a symptom back
through unrelated code.

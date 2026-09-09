# Defensive Programming

## What

Defensive programming is the practice of writing code that actively checks its
own assumptions and preconditions, rather than trusting that callers will always
supply valid input, that invariants will always hold, or that a dependency will
always behave as expected. It's closely related to Fail Fast (this section),
which describes *when* to raise the alarm; defensive programming describes the
discipline of consistently checking *at all*.

## Why

Code that assumes its preconditions are always met, without checking, will behave
unpredictably — or silently incorrectly — the moment a precondition is violated,
whether through caller error, an edge case nobody anticipated, or a future change
elsewhere in the system that the original author never saw coming. Defensive
checks convert "silent, unpredictable misbehaviour, discovered eventually and
expensively" into "a clear, immediate, descriptive failure, discovered as soon as
the violation happens."

## Benefits

- Bugs surface as clear failures at the point of violation rather than as subtle,
  hard-to-trace misbehaviour elsewhere.
- Public APIs become self-documenting about their actual requirements: a
  `null`-check with a clear message tells a caller exactly what was expected,
  where a lack of any check leaves them guessing after the fact, from a symptom.
- A codebase that checks its own invariants consistently is safer to change,
  because a future contributor violating an assumption they didn't know existed
  will be told immediately, rather than only discovering it through a
  hard-to-reproduce bug report much later.

## Disadvantages

- Excessive, redundant checking — validating the same invariant at every layer
  it passes through, when an earlier layer has already guaranteed it — adds
  noise and maintenance cost without adding real safety.
- Defensive checks are not a substitute for actually reasoning about a design;
  scattering `null` checks everywhere doesn't fix a design that shouldn't be
  producing `null` in the first place.

## When to Use

At the boundary of any public API, where you cannot control or verify what a
caller will pass; whenever an internal invariant, once violated, would be
expensive or confusing to debug later; whenever accepting bad input silently
would be worse than failing immediately (see Fail Fast).

## When Not to Use

For private, internal call paths where the caller is the same code that already
guarantees the invariant, and re-checking would be pure redundancy — trust your
own code's local invariants once you've established them, rather than
re-verifying them at every internal step.

## How TempestOS Applies It

Argument validation is consistent and disciplined across every work package:

- `ArgumentNullException.ThrowIfNull(...)` guards every public constructor and
  method that accepts a reference-type parameter it depends on
  (`RuntimeModuleManager.Register`, `ModuleLifecycleManager`'s constructor,
  `TempestServiceProvider.GetService`, `ServiceCollection.Add`).
- `ArgumentException` (not a generic exception) is thrown for arguments that are
  present but semantically invalid — a blank `ModuleDescriptor.Id`, an
  implementation type that doesn't actually satisfy the service type it's being
  registered against.
- Discovery (WP 2.1) defensively handles `ReflectionTypeLoadException` when
  scanning assemblies — a real-world condition where some types in an assembly
  fail to load — rather than letting the entire discovery pass crash because one
  problematic type existed somewhere in a scanned assembly.
- `ModuleLifecycleManager`'s `TransitionAsync`/`DisposeModuleAsync` methods check
  the module's current state *before* attempting a transition, rather than
  attempting the transition and hoping for the best — a direct application of
  defensive programming to the state-machine principle described above.

TempestOS deliberately draws a line between two categories of defensive check,
using different exception families for each (documented explicitly in the WP 2.2
and WP 2.4 completion reports): `ArgumentException`/`ArgumentNullException` for
*caller-contract violations* (a genuine programmer error — passing `null`, or a
blank ID), versus dedicated, domain-specific exceptions
(`DuplicateModuleRegistrationException`, `InvalidModuleLifecycleTransitionException`,
`ServiceNotRegisteredException`) for *business-rule violations* — states or
conditions that are valid inputs but represent an invalid *situation*. This
distinction is itself a piece of defensive-programming discipline: it keeps a
caller's `catch` blocks meaningful, since catching `ArgumentException` means "I
passed something wrong," while catching a domain exception means "the operation
itself couldn't legally proceed."

## Key Takeaway

Defensive programming in TempestOS is not indiscriminate `null`-checking
everywhere — it's a consistent, deliberate policy of checking preconditions at
public boundaries, with the *type* of exception thrown itself communicating
*what kind* of precondition was violated.

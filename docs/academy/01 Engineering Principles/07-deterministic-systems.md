# Deterministic Systems

## What

A deterministic system is one that, given the same inputs and the same starting
state, always produces the same outputs and the same observable sequence of
effects — no dependence on timing, thread scheduling, hash-table iteration order,
or any other incidental, non-guaranteed detail of the runtime environment.

## Why

Non-deterministic behaviour is one of the hardest categories of bug to reproduce
and fix, because "it works when I run it locally" and "it fails in CI" can both
be true of the exact same code, on the exact same inputs, if the system's
behaviour secretly depends on something incidental — the iteration order of a
`Dictionary<TKey,TValue>` (which is not, and has never been, a guaranteed
ordering in .NET, despite frequently *appearing* stable), the order in which a
reflection API happens to enumerate types, or the scheduling of concurrent tasks.
Determinism converts "sometimes this happens, sometimes that happens, and we
don't know why" into "this happens, every time, and we can write a test that
proves it."

## Benefits

- Tests are trustworthy: a passing test means the behaviour is correct, not
  "correct on this run, on this machine, in this order."
- Debugging is tractable: a deterministic system's bugs are reproducible, and a
  reproducible bug is one you can actually fix with confidence, rather than one
  you can only guess at.
- Operational behaviour (startup order, shutdown order) is predictable and
  auditable — an engineer can read the code and know, with certainty, what order
  things will happen in, rather than needing to run it repeatedly to build
  statistical confidence.

## Disadvantages

- Enforcing determinism sometimes costs a small amount of extra work — sorting
  a collection that would otherwise iterate in whatever order it happens to be
  stored in, for instance — that a non-deterministic version wouldn't need.
- Genuine concurrency (multiple independent operations legitimately racing, by
  design, for performance) is inherently non-deterministic in its *interleaving*,
  even if each individual operation is deterministic. Determinism is a property
  you can guarantee for *sequential* orchestration; true parallelism requires a
  different set of tools (idempotency, careful synchronisation) to reason about
  safely.

## When to Use

Anywhere the *order* of operations is observable and matters — startup sequences,
shutdown sequences, anything a test needs to assert a specific sequence for, and
anywhere reflection or a hash-based collection could otherwise introduce
incidental, unguaranteed ordering into behaviour that looks stable in testing but
isn't actually guaranteed by anything.

## When Not to Use

Genuine concurrent execution, where multiple operations are deliberately allowed
to interleave for performance or responsiveness, doesn't need — and often can't
have — a single deterministic ordering of *all* effects. What it needs instead is
correctness under any legal interleaving, which is a different (and harder)
property than determinism.

## How TempestOS Applies It

- `ReflectionFrameworkDiscoveryService.DiscoverModules()` explicitly sorts its
  results — ascending, ordinal, by `ModuleDescriptor.Id` — specifically so that
  "which order did `Assembly.GetTypes()` happen to return things in" (an
  unspecified, implementation-detail ordering) never leaks into observable
  behaviour. This is documented directly in the type's own XML remarks.
- `ModuleLifecycleManager` takes a snapshot of registered modules at
  construction and sorts it the same way, so that initialisation and startup
  order is fixed and predictable — see the ordering note added to
  `IModuleLifecycleManager`'s documentation during architectural review, which
  explicitly flags this as an *implementation convenience* (using `Id` because no
  dedicated ordering metadata exists yet) rather than a permanent design
  commitment — determinism was the actual requirement; alphabetical-by-ID was
  simply the available, correct-today mechanism for achieving it.
- `RuntimeModuleManager`, by contrast, deliberately preserves *registration*
  order rather than sorting — a different, but equally deterministic, ordering
  guarantee, chosen because registration order is itself meaningful information
  (the order modules were actually registered in) that alphabetising would
  destroy.
- Test design throughout the Academy's source work packages depends on this: the
  WP 2.3 ordering tests (`InitialiseAllAsync_RunsModulesInAscendingIdOrder`, and
  its Start/Stop/Dispose equivalents) register modules in *deliberately shuffled*
  order and assert the *sorted* order comes out — a test that would be flaky, not
  just wrong, if the underlying system weren't genuinely deterministic.

## Key Takeaway

Determinism is not free — someone has to actively impose an ordering (a sort, a
fixed iteration structure) on top of whatever the underlying platform happens to
provide, since the platform itself very often makes no ordering guarantee at all.
TempestOS does this explicitly, in exactly the two places order is observable
(discovery's output, lifecycle's execution order), and documents *why* each
chosen ordering was picked, not just that one exists.

# Case Study: Why Isn't Configuration Mutable?

*Companion to WP 2.5's retrospective and *The Startup Sequence*.*

## Original Problem

WP 2.5's brief stated its architectural principles plainly: configuration is
data, not business logic; it is immutable once the runtime has started; it is
loaded once; consumers read it and never modify it. Taken together, these rule
out a mutable `IConfigurationProvider` outright — but "the brief said so" is not
an engineering justification, and a future engineer revisiting this decision
deserves the actual reasoning, not just the rule. Mutable configuration is not
an obviously bad idea on its face: plenty of real systems support changing a
configuration value at runtime and having that change take effect immediately,
and it can look, from a distance, like a convenience worth having.

## Alternative Designs

**Option A — A mutable `IConfigurationProvider`**, with a `Set(key, value)`
method alongside `Get`, allowing any consumer to change a value at any point
during the runtime's operation, with every other consumer's next `Get` call
observing the new value immediately.

**Option B — Immutable snapshots**, exactly as implemented:
`ConfigurationProvider` is built once, from a merged, validated set of sources,
and never changes for the rest of its life. A genuinely new configuration
requires genuinely rebuilding the provider from scratch, not mutating the
existing one.

## Reasoning

**Deterministic startup.** If configuration could change at any point,
including *during* startup, then two runs of the exact same TempestOS instance,
started at slightly different wall-clock moments relative to whatever mutated
a value, could genuinely start up differently — a module reading a
configuration value during its own `InitialiseAsync` might see one value on one
run and a different value on another, for no reason connected to anything the
operator actually intended to change. Immutable configuration guarantees every
module sees exactly the same values throughout the entire startup sequence
described in *The Startup Sequence*, because there is nothing left to observe
changing.

**Reproducible behaviour.** A bug report that says "TempestOS behaved
incorrectly, here are the configuration values" is only actionable if those
values are guaranteed to have been stable for the whole run being reported on.
Mutable configuration means "here are the configuration values" is only ever
true at the instant it was captured — the values that actually mattered to the
misbehaviour might have been different five seconds earlier, and there would
be no way to know.

**Easier testing.** Every test in this work package's own suite
(`ConfigurationBuilderTests`, `ConfigurationProviderTests`, and the rest)
constructs a provider once and asserts against it repeatedly, with total
confidence that no other part of the test, and no concurrently-running test,
could have changed a value out from under an assertion. A mutable provider
would require every test to consider whether *something else* — another
thread, another part of the same test, a shared fixture — might have mutated a
value between the arrange and assert phases, exactly the class of problem
immutability eliminates by construction (see the Immutability Engineering
Principle document).

**No hidden state changes.** A `Set(key, value)` method, however carefully
scoped, is a place where configuration can change *without* the component
reading it being anywhere near the code path that changed it — action at a
distance, the specific failure mode immutability throughout this codebase
exists to prevent (see ADR-0001 and ADR-0002's identical reasoning for
`RuntimeModule` and lifecycle state, applied here to a third, independent
subsystem). A component that read a configuration value and cached a decision
based on it would have no way of knowing that decision was stale, because
nothing forces it to re-check — and nothing in a mutable design would make that
staleness visible.

**Simpler threading model.** `ConfigurationProvider`'s internal storage is a
`ReadOnlyDictionary` wrapping a defensively-copied `Dictionary`, requiring zero
synchronisation for concurrent reads, because there are no writes to
synchronise against, ever, after construction. A mutable design would need a
lock (or a concurrent collection) guarding every read *and* every write, for
the entire lifetime of the provider — a permanent, ongoing synchronisation cost
paid by every consumer, forever, to support a capability (runtime mutation)
that Options A and B disagree about needing at all.

## Decision

Option B. `ConfigurationProvider` exposes no mutation method of any kind — not
a public one, not an internal one a determined caller could reach via casting.
`IConfigurationProvider` itself, as an interface, has no such method to
implement in the first place. The only way to obtain a *different* set of
configuration values is to build an entirely new provider from a
`ConfigurationBuilder`.

## Outcome

Every one of the five reasons above is independently sufficient justification;
together, they make Option A very difficult to argue for on technical merits
alone. But the case for mutability is not always merely mistaken — sometimes
the actual, underlying need is real, and the honest answer is that WP 2.5's
provider is the wrong layer to put it in, not that the need itself is
illegitimate.

**Dynamic configuration may be implemented later as a higher-level service that
replaces immutable snapshots, rather than weakening the guarantees of the core
provider.** Concretely: a future `IReloadableConfiguration` (or similarly named)
service could hold a *reference* to the current `IConfigurationProvider`
snapshot, expose a way to atomically swap that reference for an entirely new
snapshot built from fresh sources, and notify subscribers when a swap occurs —
while every individual `IConfigurationProvider` snapshot it ever hands out
remains exactly as immutable as it is today. Consumers that want "always
current" values would depend on the higher-level service; consumers that want
"stable for as long as I hold this reference" — which is what every consumer
depends on today, and should continue to be able to depend on — would continue
to depend on `IConfigurationProvider` directly, unaffected. The core provider's
guarantees would never need to be weakened to deliver reload-on-change; reload-
on-change would be a new capability layered *on top of* those guarantees, not a
replacement for them.

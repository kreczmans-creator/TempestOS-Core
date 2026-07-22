# Case Study: Why Discovery Is Isolated

*Companion to ADR-0008.*

## Original Problem

WP 2.4's stated objective was blunt: "The runtime shall no longer construct
module instances directly using `Activator.CreateInstance()`. Instance creation
becomes the responsibility of a service provider." Read at face value, and
applied mechanically everywhere that phrase appeared in the codebase, this could
have meant *every* `Activator.CreateInstance` call — and there were, at that
point, exactly two: one inside `ModuleLifecycleManager` (constructing the "real,"
persistent module instance used to drive lifecycle methods), and one inside
`ReflectionFrameworkDiscoveryService` (constructing a throwaway instance purely to
read `Id`/`Name`/`Version` metadata during discovery, before discarding it
immediately).

## Alternative Designs

**Option A — Route both call sites through the service provider.** Apply
WP 2.4's objective literally and completely: discovery, too, resolves candidate
module instances through `ITempestServiceProvider` instead of calling
`Activator.CreateInstance` directly, for consistency — "no runtime service should
manually instantiate modules after this work package," taken at its broadest
reading.

**Option B — Route only the lifecycle manager's call site; leave discovery
untouched.** Treat WP 2.4's objective as being about *module instantiation for
actual use* — the one, real, persistent instance whose lifecycle methods get
invoked — and recognise discovery's transient metadata probe as a categorically
different thing that happens to also call `Activator.CreateInstance`, but for an
unrelated reason, at an unrelated point in the pipeline.

## Reasoning

Option A runs immediately into a sequencing problem that has no clean resolution.
The intended pipeline is Discovery → Registration → Lifecycle → Dependency
Injection, in that order, for a reason: you cannot register a module you haven't
discovered yet, you cannot orchestrate the lifecycle of a module you haven't
registered yet, and — critically for this decision — you cannot *resolve a type
through a container* whose registrations are built from modules discovery has
already found. If discovery itself needed the container to run, the container
would need to already contain registrations for the very modules discovery's job
is to go and find in the first place. There is no non-circular ordering that
makes this work: either discovery runs first and the container doesn't exist yet
to route through, or the container is built first from some *other*, unspecified
source of module information, which defeats the entire purpose of having a
discovery stage at all.

Beyond the sequencing problem, the two `Activator.CreateInstance` calls are not
actually doing the same job, despite superficially looking identical in code.
Discovery's instantiation is transient — the instance exists for the duration of
reading three string properties, then is discarded and garbage-collected; nothing
about it is kept, reused, or exposed to any caller. `ModuleLifecycleManager`'s
instantiation (via `ResolveInstance`, post-WP 2.4) produces the *one*, persistent
instance a module's actual behavioural methods are invoked on for its entire
runtime life. Conflating "instantiate something to peek at three properties and
throw it away" with "construct the real, living instance the system will run" —
just because both happen to call the same BCL method — would have been a
category error: forcing two different concerns to share one mechanism, for the
sake of literal consistency with a sentence, rather than because they were
actually the same problem.

There was a third consideration, closer to WP 2.1's own explicit constraints:
"Do not redesign module discovery" appeared, verbatim, in every one of WP 2.2,
WP 2.3, and WP 2.4's briefs. Routing discovery through DI would have been exactly
that — a redesign of how discovery obtains its metadata probes, motivated by a
different work package's objective, not by any actual need discovered within
discovery's own scope.

## Decision

Option B. `ReflectionFrameworkDiscoveryService`'s `Activator.CreateInstance` call
was left completely untouched by WP 2.4 — not a single line of that class was
modified. WP 2.4's entire production-code change to prior work packages was one
edit, inside `ModuleLifecycleManager`, replacing its own, separate
`Activator.CreateInstance` call with `_serviceProvider.GetService(...)`.

## Outcome

The distinction turned out to matter for a reason not fully anticipated at
design time: ADR-0003 (side-effect-free constructors) is *why* this distinction
is safe at all. Because a module's constructor must be cheap and free of
observable side effects, it does not matter, in practice, that a module's
constructor now runs at least twice over its life if the module is ever
discovered and later initialised — once by discovery (thrown away), once by the
lifecycle manager via DI (kept, and used for real). If that convention were ever
violated by a module author — a constructor that opens a file, say — the effect
would happen twice, silently, and discovery would be the culprit even though
discovery was doing exactly what it was always designed to do. This is
documented explicitly in ADR-0003 and ADR-0008 as a genuine, acknowledged sharp
edge: an engineer reading only `ModuleLifecycleManager`'s code, without knowing
about discovery's separate metadata probe, could reasonably be surprised that
"construction" isn't a once-per-module-per-run event.

The isolation held up under direct scrutiny during WP 2.4's own completion
report, which flagged the decision explicitly as an assumption worth stating
rather than a silent omission — precisely the kind of documentation this Academy
exists to preserve: not just *that* discovery was left alone, but the specific
reasoning (sequencing, category difference, scope discipline) for why leaving it
alone was the correct call rather than an oversight.

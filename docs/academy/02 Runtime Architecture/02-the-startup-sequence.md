# The Startup Sequence

## 1. Introduction

WP 2.5 introduced the first service in TempestOS that must exist *before*
dependency injection can begin — configuration. That single fact makes explicit
something the module pipeline document (*The Module Pipeline*) did not need to
address: TempestOS's startup is not just "discover, register, orchestrate,
inject" — it is an ordered sequence, and configuration's place in that sequence
is load-bearing, not incidental. This document names that sequence explicitly,
because every future service with the same "must exist before DI" property
(logging, most plugin mechanisms, and hosted services are all likely
candidates) will need to slot into it correctly, not rediscover the ordering
from scratch.

## 2. Purpose

To state, once, in one place, the order TempestOS's runtime is intended to come
up in — from process start to the module pipeline actually running — so that
every future work package adding a new startup-time concern can answer "where
does this go" by consulting this document, rather than by guessing or by
copying whatever the previous work package happened to do.

## 3. Background

WP 2.1 through WP 2.4 never needed to state a startup sequence explicitly,
because nothing in those four work packages existed *before* the container:
discovery, registration, and lifecycle orchestration are all things that happen
*through* the DI container, or immediately alongside it, once it exists.
Configuration breaks that pattern — by design, per its own architectural
principles ("configuration is loaded once," "immutable once the runtime has
started") — and in doing so, forces the ordering question that this document
now answers.

## 4. The Problem

Given that configuration must exist before the container does, and that the
container must exist before modules can be resolved and orchestrated, what is
the complete, correct order of operations from "the process starts" to "the
module pipeline is running" — and where, specifically, does configuration
become permanently fixed, such that nothing after that point can still change
it?

## 5. The Design

```
Startup
   │
   ▼
Build configuration              (ConfigurationBuilder.AddSource(...) × N,
   │                               then ConfigurationBuilder.Build())
   ▼
Freeze configuration              (Build() already returns an immutable
   │                               ConfigurationProvider — this step is not a
   │                               separate action, it is the guarantee that
   │                               Build()'s output is not revisited)
   ▼
Register configuration            (services.AddInstance<IConfigurationProvider>(
   │                               provider) — see ADR-0009)
   ▼
Build service provider            (new TempestServiceProvider(services, ...))
   │
   ▼
Runtime starts                    (discovery → registration → lifecycle,
                                    all now able to resolve IConfigurationProvider,
                                    and anything else registered alongside it,
                                    through the container)
```

**Build configuration** happens first, and happens exactly once: every
`IConfigurationSource` the composition root intends to use is added, in the
order that determines override precedence, and `Build()` is called. This is
also where every configuration-time failure (a missing key's *source* problem,
a duplicate key, an invalid entry) surfaces — before anything else has started,
while a failure is still cheap and unambiguous to diagnose.

**Freeze configuration** is not a distinct method call — it is the
*consequence* of `Build()`'s return value being an immutable
`ConfigurationProvider` (see ADR and Case Study material on why configuration
is immutable). It is named as its own step in this sequence deliberately,
because it is the conceptual moment after which no code path in TempestOS is
permitted to treat configuration as something still in flux, even though no
single line of code performs a "freeze" operation.

**Register configuration** hands the already-built, already-frozen provider to
the DI container via `AddInstance` — the mechanism ADR-0009 names as one
expression of a broader principle: some services must exist before dependency
injection begins, and registering them is a distinct step from constructing
the container itself.

**Build service provider** constructs the `TempestServiceProvider` from the
now-populated `ServiceCollection` — at this point, and not before, anything
registered (configuration included) becomes resolvable via constructor
injection.

**Runtime starts** — only now does the module pipeline (discovery →
registration → lifecycle) begin, with every module able to declare a
constructor dependency on `IConfigurationProvider`, or on any other service
registered before this point, and have it resolved automatically.

## 6. Alternatives Considered

**Registering configuration sources into the container, and letting the
container build configuration lazily on first request.** This was the
implicit alternative that would result from treating configuration like any
other service from the start. Rejected: it would mean the *first* thing to
request `IConfigurationProvider` triggers `ConfigurationBuilder.Build()`
(and, with it, every possible build-time validation failure) at some
arbitrary, unpredictable point during the runtime's operation, rather than
deterministically at startup, before anything depends on it — directly
contradicting "configuration is loaded once" and turning a startup-time
failure into a runtime-time one.

**Treating "freeze" as an explicit method** (`ConfigurationProvider.Freeze()`
or similar) rather than an implicit consequence of immutability. Considered and
rejected as unnecessary ceremony: `ConfigurationProvider`'s constructor already
produces a fully immutable value (see WP 2.5's own retrospective and the
accompanying case study) — there is no meaningful "unfrozen" state for an
explicit method to transition out of.

## 7. Why This Solution Was Chosen

The sequence was chosen by working backward from a single hard requirement:
every module, and every future runtime-time service, must be able to assume
configuration already exists, is complete, and will not change, the moment its
own constructor runs. The only way to guarantee that assumption universally is
to make configuration's construction happen strictly before the container that
resolves those constructors even exists — which is exactly what "build, then
freeze, then register, then build the provider" achieves, in that order, with
no step reorderable without breaking the guarantee.

## 8. Architectural Principles

- **Deterministic Systems** — the sequence is fixed and total; there is no
  ambiguity about what has and hasn't happened at any given point during
  startup.
- **Immutability** — "freeze configuration" is this document's name for the
  moment `ConfigurationProvider`'s own immutability guarantee takes effect for
  the whole running system, not just for one object.
- **Fail Fast** — every configuration validation failure is structurally
  forced to occur during "build configuration," before the runtime has done
  anything else, rather than being deferred to whenever a value happens to be
  first requested.
- **Dependency Injection** (this document's central concern) — a service that
  cannot be constructed by the container has to be built and registered
  *before* the container is built, not resolved into existence lazily.

## 9. Benefits

- Every future startup-time concern (logging configuration, plugin loading,
  hosted service startup) has an explicit place to ask "where do I fit" — this
  document, updated as each is actually introduced, rather than an implicit,
  undocumented convention a new contributor has to reverse-engineer from
  whichever composition root code happens to exist at the time.
- Configuration-dependent failures (a missing required key, a malformed
  source) are guaranteed to surface at the earliest possible point — before
  discovery, before registration, before lifecycle orchestration, before any
  module has run any code at all.

## 10. Trade-offs

- This sequence is currently *documented intent*, not yet enforced by a single
  piece of code that literally performs all six steps in order — WP 2.1 through
  WP 2.5's tests each construct only the pieces they need, directly, rather
  than exercising a real, end-to-end composition root. A dedicated composition
  root implementing this exact sequence (already flagged as a gap in the WP 2.4
  retrospective's Future Evolution section) remains outstanding.

## 11. Common Mistakes

The mistake this document exists to prevent, pre-emptively, is a future work
package registering its own service *before* configuration, if that service
happens to need a configuration value during its own construction or
initialisation. Any future service depending on `IConfigurationProvider` via
constructor injection is safe automatically (the container resolves
dependencies recursively, regardless of registration order) — but a service
that reads configuration *outside* of DI-mediated construction, for instance
during some earlier bootstrap step that runs before the service provider
exists, would not be safe, since configuration would not yet have been built.
The rule this sequence protects is simple to state and easy to violate by
accident: nothing may read configuration before "build configuration" has
completed, regardless of which mechanism it uses to read it.

## 12. Future Evolution

This document should be updated, not left as a four-work-package-old
prediction, the moment any of the following are actually introduced:

- **Logging** — if TempestOS's logging infrastructure is ever reworked to be
  configuration-driven (a minimum log level read from configuration, for
  example), its initialisation slots in immediately after "build service
  provider," since it would need to resolve `IConfigurationProvider` through
  the container.
- **Plugins** — a plugin-loading mechanism (populating `src/Plugins/`,
  currently empty — see WP 2.1's own noted gap around external assembly
  loading) would need its own explicit slot in this sequence, most likely
  before "runtime starts," since discovered plugin assemblies may themselves
  need to be scanned by the Framework Discovery service.
- **Hosted services** — if TempestOS introduces a hosted-service concept
  (explicitly out of scope for WP 2.4's dependency injection work), its startup
  and shutdown ordering relative to the module pipeline's own lifecycle needs
  to be decided and added to this sequence, not left implicit.

## 13. Key Takeaways

1. A startup sequence is worth documenting explicitly the moment *any* service
   has a "must exist before DI" property — waiting until several such services
   exist means reconstructing the ordering rules retroactively, under more
   constraints, instead of setting them once while there is still only one
   example to reason from.
2. "Freeze" can be a conceptual step in a sequence without being a literal
   method call — what matters is that every reader agrees on the *moment* after
   which a guarantee holds, not that the code contains a function named to
   match.
3. Every future service that will need to exist before dependency injection
   begins (logging, plugins, hosted services) should be evaluated against this
   sequence, and against ADR-0009's broader principle, before its own startup
   ordering is decided independently.

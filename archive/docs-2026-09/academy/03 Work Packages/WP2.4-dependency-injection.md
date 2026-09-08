# WP 2.4 — Dependency Injection

## 1. Introduction

WP 2.4 completed the four-stage pipeline that WP 2.1 through WP 2.3 had been
building toward: Discovery → Registration → Lifecycle → Dependency Injection.
It introduced TempestOS's own dependency injection container and, with it,
answered the one question the previous three work packages had all deliberately
left open: where does a module's actual, runnable instance come from?

This work package is unusual among the four in that its brief contained an
explicit, hard constraint most projects would never impose on themselves: build a
DI container from scratch, and do not reach for
`Microsoft.Extensions.DependencyInjection` or any other existing library. This
document explains the reasoning behind that constraint, what it cost, what it
bought, and — distinctively for this work package — a real bug that this
Academy's own review process caught and fixed as a direct consequence of the
change, preserved here in full because it is one of the more valuable lessons in
TempestOS's engineering history.

## 2. Purpose

To give TempestOS a mechanism for constructing objects — modules, specifically,
but designed generally enough to construct anything — with their dependencies
resolved automatically, recursively, through constructor injection, with
descriptive failures instead of cryptic ones, and to retire the one remaining
`Activator.CreateInstance` call that had been quietly standing in for "real"
dependency injection since WP 2.3.

## 3. Background

By the time WP 2.4 began, `ModuleLifecycleManager` (WP 2.3) already constructed
module instances — once, during `InitialiseModuleAsync`, via
`Activator.CreateInstance(descriptor.ModuleType)`, caching the result for reuse
across `Start`/`Stop`/`Dispose`. This worked, but only for modules with
parameterless constructors — which was every module that existed, since nothing
before WP 2.4 gave a module any way to declare a dependency at all. The
architecture's own stated, fixed responsibility list (repeated verbatim across
WP 2.2, WP 2.3, and this work package's own brief) had always described a
"Service Provider" as owning construction — WP 2.4 was the point at which that
line in the architecture diagram finally got a real implementation behind it.

## 4. The Problem

1. **What lifetimes does the container need to support?** The brief specified
   exactly two — Singleton and Transient — and explicitly no Scoped, since
   TempestOS has no request/unit-of-work concept to scope anything to.
2. **How are services registered?** Four forms were specified:
   `Singleton<T>()`, `Singleton<TInterface,TImplementation>()`, `Transient<T>()`,
   `Transient<TInterface,TImplementation>()`.
3. **How is a type's constructor chosen**, given a type might have zero, one, or
   several public constructors?
4. **How are a constructor's own parameters resolved** — recursively, through the
   same container, so a service's dependencies (and their dependencies) are
   constructed automatically?
5. **What happens when something goes wrong** — a missing registration, a
   circular dependency, an ambiguous constructor — and how descriptive can the
   resulting exception be made?
6. **How do discovered modules get into the container automatically**, without
   every caller hand-writing a registration line per module type?
7. **How does `ModuleLifecycleManager` change**, given its `Activator.CreateInstance`
   call was the literal, named target of this work package's objective?
8. **What must explicitly not change** — discovery's own, separate
   `Activator.CreateInstance` call, module registration, and lifecycle
   orchestration's actual algorithm, none of which this work package was
   permitted to redesign?

## 5. The Design

**`ServiceLifetime`** — `Singleton` / `Transient`, nothing more.

**`ServiceDescriptor`** — a registration record: `ServiceType`,
`ImplementationType`, `Lifetime`.

**`IServiceCollection`** / **`ServiceCollection`** — the registration side. The
interface itself carries exactly one real method, `Add(Type, Type,
ServiceLifetime)`; the four forms the brief specified (`Singleton<T>()`, and so
on) are extension methods built on top of it, giving both the required
convenience API and a Type-based entry point with no compile-time generic
argument required — the latter turning out to be exactly what was needed for
automatic module registration (below).

**`ITempestServiceProvider`** / **`TempestServiceProvider`** — the resolution
side. Named `ITempestServiceProvider`, deliberately not `IServiceProvider` — see
the naming decision below. `GetService(Type)` resolves an instance, recursively
resolving constructor parameters, choosing the single public constructor
(throwing a descriptive exception for zero or more than one), caching singleton
instances in a dictionary guarded by a single lock, and constructing transient
instances fresh on every call.

**Exception hierarchy**: `ServiceResolutionException` (base),
`ServiceNotRegisteredException`, `CircularServiceDependencyException`,
`AmbiguousConstructorException` — each carrying the originally-requested
top-level service type, the specific failing type, and the full construction
chain as structured properties, not just embedded in a message string, satisfying
the brief's explicit requirement that failures identify "missing dependency,
requested service, construction chain."

**`ModuleServiceCollectionExtensions.AddDiscoveredModules`** — bridges discovery's
output into the container: registers every `ModuleDescriptor.ModuleType` as a
singleton, keyed by itself.

**`ModuleLifecycleManager`'s one change**: its constructor now requires an
`ITempestServiceProvider`; its private `ResolveInstance` method calls
`_serviceProvider.GetService(descriptor.ModuleType)` instead of
`Activator.CreateInstance`. This was the *only* production-code change to any
prior work package's class.

## 6. Alternatives Considered

**Adopting `Microsoft.Extensions.DependencyInjection`.** The industry-default
choice, explicitly forbidden by the brief. See ADR-0005 for the full reasoning —
in short, a deliberate trade of development speed and ecosystem familiarity for
zero third-party dependency surface and a container with exactly the complexity
TempestOS needs, no more.

**Naming the resolution interface `IServiceProvider`**, matching
`Microsoft.Extensions.DependencyInjection`'s own convention and the BCL's own
`System.IServiceProvider`. Rejected immediately upon recognising that `System` is
part of this project's implicit usings (`Directory.Build.props`) — any file also
referencing `Tempest.Core.DependencyInjection` would hit an unavoidable
ambiguous-reference compiler error (CS0104) on every unqualified use. Named
`ITempestServiceProvider` instead.

**Property or method injection, in addition to or instead of constructor
injection.** Rejected in favour of constructor injection exclusively — see
ADR-0006 and the Dependency Injection Engineering Principle document for the
full reasoning: constructor injection makes a type's dependencies fully visible
in its signature and guarantees no partially-constructed object ever exists.

**Picking "the constructor with the most resolvable parameters"** when multiple
public constructors exist, as some containers (including
`Microsoft.Extensions.DependencyInjection`, optionally) support. Rejected in
favour of requiring exactly one public constructor, unconditionally — this
removes an entire category of non-determinism (the same type resolved
differently depending on what else happens to be registered) at the cost of
requiring module authors to keep any alternative constructors non-public.

**Routing discovery's own `Activator.CreateInstance` call through the new
container**, for literal consistency with the brief's "no runtime service should
manually instantiate modules" phrasing. Rejected — see ADR-0008 and its case
study for the full reasoning: this would have been circular (the container's
registrations are themselves built from discovery's output) and would have
conflated two genuinely different instantiation purposes (a transient metadata
probe versus the one, real, persistent instance) under a single mechanism for the
sake of a literal reading rather than the objective's actual intent.

**A live, thread-local, or otherwise ambient resolution-chain tracker** for
circular-dependency detection. Rejected in favour of threading the chain through
as an explicit, immutable list parameter on the private `Resolve` method — no
shared mutable state exists anywhere in the resolution path, so concurrent,
unrelated `GetService` calls cannot interfere with each other's cycle detection.

## 7. Why This Solution Was Chosen

Every non-obvious decision in this work package traces back to one of two
governing constraints, both explicit in the brief: build the whole thing from
scratch, and touch only what the objective actually named. The naming decision
(`ITempestServiceProvider`), the minimal-interface-plus-extension-methods
pattern, and the decision to leave discovery untouched are all different
expressions of the same discipline: implement exactly what was asked, and
nothing adjacent to it, however tempting the adjacent change might look.

## 8. Architectural Principles

- **Dependency Injection** and **Composition Over Inheritance** — the subject of
  the whole work package; see both Engineering Principle documents.
- **Fail Fast** — every resolution failure is a specific, structured exception
  identifying exactly what went wrong and where, at the exact point of failure.
- **Single Responsibility** — the container constructs; it does not discover,
  register, or orchestrate. `ModuleLifecycleManager`'s one-line change is the
  clearest possible demonstration of this: construction was always its stated
  responsibility, DI is simply a different *mechanism* for fulfilling the same,
  unchanged responsibility.
- **Defensive Programming** — argument validation (`ArgumentNullException.ThrowIfNull`)
  at every public entry point, exactly as established in every prior work
  package.

## 9. Benefits

- Modules can, for the first time, declare real constructor dependencies —
  loggers, configuration, other services — rather than being limited to
  parameterless construction.
- Resolution failures are dramatically more informative than anything
  `Activator.CreateInstance` alone could produce — a missing dependency now
  names the requested service, the missing type, and the full chain, rather than
  surfacing as an unrelated `NullReferenceException` deep inside a module's own
  code.
- `ModuleLifecycleManager`'s change being exactly one call site is direct,
  concrete proof that WP 2.1 through WP 2.3's separation-of-concerns discipline
  paid off: introducing an entire new subsystem required touching only the one
  place that subsystem's responsibility actually lived.

## 10. Trade-offs

- No disposal tracking for singleton instances — if a singleton implements
  `IDisposable`/`IAsyncDisposable`, nothing in `TempestServiceProvider` will ever
  call it. Explicitly documented as an observation, not fixed, since fixing it
  would edge toward hosted-service lifecycle management, out of this work
  package's stated scope.
- Duplicate registration of the same service type silently replaces the previous
  one ("last wins") rather than throwing — a deliberate, but not universally
  obvious, choice; a reader expecting the stricter, fail-fast-everywhere
  convention this codebase otherwise follows might reasonably expect a
  registration conflict to throw, and should know that it doesn't.
- Every caller constructing a `ModuleLifecycleManager` must now also assemble a
  `ServiceCollection`/`TempestServiceProvider` and keep it in sync with
  discovery's output via `AddDiscoveredModules` — more setup than the
  single-argument constructor WP 2.3 shipped with, and not enforced by the type
  system (nothing stops constructing a `ModuleLifecycleManager` with an empty
  provider, which will simply fail at `Initialise` time instead of at
  construction time).

## 11. Common Mistakes

This work package's Common Mistakes section is dominated by a single, genuinely
important discovery, made not before implementation but *during* it, through
careful re-reading of the interaction between old and new code: a bug that would
otherwise have shipped. `ModuleLifecycleManager.TransitionAsync`'s original
structure called instance resolution *inside* the state-transition lock, but
*outside* the `try`/`catch` block responsible for marking a module `Failed`. This
was invisible and harmless under WP 2.3's `Activator.CreateInstance`, which
essentially cannot fail for a well-formed, parameterless-constructor module. It
became a live, realistic failure mode the instant construction started going
through DI resolution, which can throw for entirely legitimate reasons (a missing
dependency, a circular one, an ambiguous constructor) that have nothing to do
with the module's own code being broken. Left unfixed, a module hitting exactly
this failure mode would have been left stuck permanently in a transient state
(`Initialising`) with `FailureReason` never set — directly undermining this same
work package's own requirements #7 ("improve error messages") and #8 ("log
construction failures"). The fix — moving resolution inside the `try` block — was
made as part of this work package specifically because it was judged to *block
completion*, per the brief's own instruction to document unrelated issues but fix
only what blocks completion; a regression test
(`InitialiseModuleAsync_MarksModuleFailed_WhenServiceProviderResolutionFails`)
now exists specifically to prove this scenario is handled correctly.

The broader lesson, worth internalising beyond this specific bug: a piece of
code can be entirely correct under the assumptions true when it was written, and
become subtly wrong the moment a *later*, seemingly unrelated change alters what
can happen at the exact call site it depends on. The defence is not "remember
this is fragile" — memory fades and engineers rotate — the defence is structural:
put failure-prone operations inside their failure-handling blocks regardless of
how unlikely failure looks today, because "unlikely today" is not a property that
survives architectural change.

## 12. Future Evolution

- **Singleton disposal tracking.** The most likely near-term gap to need
  addressing: if TempestOS's singleton services start needing to hold real,
  disposable resources, `TempestServiceProvider` will need its own
  `IDisposable`/`IAsyncDisposable` implementation, tracking and disposing every
  singleton instance it created, in reverse creation order — mirroring, at the
  container level, the same reverse-order shutdown discipline
  `ModuleLifecycleManager` already applies at the module level.
- **A composition-root helper.** The discovery → registration →
  `AddDiscoveredModules` → `TempestServiceProvider` → `ModuleLifecycleManager`
  wiring sequence is currently assembled by hand at every call site (including
  every test). If this sequence grows more elaborate, a small, explicit helper
  encoding the correct order once would be worth introducing.
- **Logging severity.** As with WP 2.3, `LoggingService`'s `Information`-only
  design means a circular-dependency exception and a routine "service resolved"
  message are logged at the same severity — worth revisiting alongside WP 2.3's
  identical, already-noted gap.

## 13. Key Takeaways

1. A hard constraint ("build it yourself, no third-party libraries") can be a
   legitimate, deliberate engineering trade-off rather than an arbitrary
   restriction — but it should be documented as explicitly as any other
   architectural decision, with its costs stated as honestly as its benefits
   (ADR-0005 exists for exactly this reason).
2. Naming collisions with framework types (`System.IServiceProvider`) are a real,
   easily-overlooked hazard the moment implicit usings are involved — check for
   them before committing to a name that mirrors an existing, well-known
   interface.
3. The cleanest possible evidence that earlier separation-of-concerns work paid
   off is a later work package needing to change exactly one call site to
   introduce an entirely new subsystem — `ModuleLifecycleManager`'s single-line
   change from `Activator.CreateInstance` to `_serviceProvider.GetService` is
   that evidence, concretely, in this codebase's own history.
4. Integrating a new subsystem into existing code can reveal latent bugs in that
   existing code that were never wrong on their own terms — only newly
   exercisable because the integration changed what could happen at a shared
   call site. Finding and fixing this class of bug is not scope creep; it is
   precisely the kind of issue an engineering discipline that reviews
   interactions, not just new code in isolation, is supposed to catch.

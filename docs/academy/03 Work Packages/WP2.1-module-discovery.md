# WP 2.1 — Module Discovery

## 1. Introduction

WP 2.1 is the first stage of TempestOS's module pipeline: Framework Discovery.
It answers exactly one question — "what modules exist?" — using reflection over
loaded .NET assemblies, and answers it in a way that is safe, deterministic, and
testable. Everything else in the pipeline (registration in WP 2.2, lifecycle
orchestration in WP 2.3, dependency injection in WP 2.4) depends on discovery
having answered this question correctly, but none of them depend on *how*
discovery answers it — a boundary that turned out to matter more than it might
first appear.

This document is not a description of `ReflectionFrameworkDiscoveryService`'s
code — that is what the source file and its XML documentation are for. This
document is about *why* the code looks the way it does: the design questions that
had to be answered, the alternatives that were considered and rejected, and the
reasoning that connects this work package to everything built on top of it since.

## 2. Purpose

To give TempestOS a mechanism for finding every implementation of `IModule`
across whatever assemblies happen to be loaded, without requiring any of those
implementations to be known about, referenced, or registered anywhere in advance.
This is the foundational capability that makes TempestOS's later
"module" concept possible at all — without discovery, "modules" would just be a
label applied to plain classes with no runtime mechanism connecting them to
anything.

WP 2.1 explicitly did *not* aim to build a plugin system, a module registry, a
lifecycle manager, or a dependency injection container — each of those was a
deliberate non-goal, reserved for later work packages. WP 2.1's purpose was
narrow and specific: find modules, validate their metadata, return them in a
predictable order.

## 3. Background

Before WP 2.1, TempestOS's C# implementation (the canonical replacement for the
retired Python prototype — see the repository's stabilisation history) consisted
of a small `Tempest.Core` project with configuration, hosting, logging, and
project-management services, and a `Tempest.App` console front end. None of it
had any notion of "modules" as a first-class concept. The Python prototype it
replaced *had* attempted something in this direction — a `FrameworkRegistry` and
`WorkPackageRegistry` — but that code was retired specifically because the
prototype was being abandoned wholesale in favour of the C# rewrite, not because
its module concept was reused or carried forward. WP 2.1 started from a genuinely
blank slate for this concern.

The wider architectural intent — a four-stage pipeline of Discovery →
Registration → Lifecycle → Dependency Injection, eventually followed by Health and
Diagnostics — was already understood in outline before WP 2.1's implementation
began, which is why WP 2.1's brief was careful to state explicit non-goals
("do not implement any concrete application modules yet") rather than let
discovery's scope creep into territory later work packages would need.

## 4. The Problem

Concretely, WP 2.1 had to solve:

1. **How does a module declare itself, at minimum?** Some common contract needed
   to exist that any module type could implement, carrying enough metadata
   (a unique identifier, a name, a version) for the runtime to talk about it
   meaningfully.
2. **How does the runtime find implementations of that contract**, given that new
   modules should be addable without modifying any existing discovery code — the
   Open/Closed Principle applied concretely?
3. **What should be excluded**, and how? Reflection over "everything implementing
   an interface" naturally surfaces the interface itself, abstract base classes
   partially implementing it, and open generic type definitions that can't be
   instantiated without a type argument — none of which represent real,
   usable modules.
4. **What happens when two modules claim the same identifier?** A collision is a
   configuration error, not a runtime condition to silently tolerate.
5. **What order should discovered modules come back in?** Reflection APIs make no
   ordering guarantee; something deterministic had to be imposed on top.
6. **How does discovery interact with the rest of the (not-yet-built) runtime**,
   particularly the existing logging infrastructure?
7. **How can any of this be tested**, given that "scan whatever happens to be
   loaded into the process" is about as non-deterministic and untestable a
   starting point as an API can have?

## 5. The Design

Three types anchor WP 2.1:

- **`IModule`** — the minimal contract: `Id`, `Name`, `Version`, all read-only
  string properties. Deliberately nothing else. No lifecycle methods (those came
  in WP 2.3, as a *separate* interface, `IModuleLifecycle` — see the Single
  Responsibility and Interface Segregation discussions in the Engineering
  Principles section), no configuration, no dependencies.
- **`ModuleDescriptor`** — an immutable snapshot of what discovery found for one
  module: its `Id`, `Name`, `Version`, and the concrete `Type` that implements
  `IModule`. This is the *output* of discovery — the thing every later work
  package actually consumes.
- **`IFrameworkDiscoveryService`** / **`ReflectionFrameworkDiscoveryService`** —
  the contract and its one implementation. `DiscoverModules()` scans assemblies
  (defaulting to everything loaded in the current `AppDomain`, or an explicit set
  supplied to the constructor), filters candidate types, instantiates survivors
  via their public parameterless constructor to read metadata, validates that
  metadata, detects duplicates, and returns results in ascending, ordinal
  alphabetical order by `Id`.

Two more types support the failure modes discovery can hit: `ModuleDiscoveryException`
(the base, for general metadata validation failures — a blank `Id`, `Name`, or
`Version`) and `DuplicateModuleIdException` (a dedicated subtype, carrying the
colliding ID, for the one failure mode explicitly called out as needing its own
exception type).

The filtering logic excludes anything that is an interface, an abstract class, an
open generic type definition, or that simply doesn't implement `IModule` at all —
checked via `Type.IsInterface`, `Type.IsAbstract`, `Type.IsGenericTypeDefinition`,
and `typeof(IModule).IsAssignableFrom(type)`, in that order, before any attempt is
made to instantiate a candidate.

Logging is threaded through as an optional collaborator: `ReflectionFrameworkDiscoveryService`
accepts a `LoggingService?` (nullable, defaulting to `null`), and logs discovery
start, each module found, duplicate detection, and a completion summary when one
is supplied.

## 6. Alternatives Considered

**Attribute-based discovery instead of interface-based.** Modules could have been
identified by a custom attribute (`[Module("my.module", "My Module", "1.0")]`)
rather than an interface with properties. This was not pursued for WP 2.1: an
interface gives compile-time enforcement (a type either implements `IModule`
correctly or it doesn't compile) that an attribute, whose values are just data
with no structural guarantee, cannot provide. Attribute-based discovery remains a
plausible future addition (see Future Evolution) but was not the WP 2.1 baseline.

**A public, testable `DiscoverModules(IEnumerable<Type>)` overload** — considered
and *adopted*, but as an `internal` seam rather than a public API surface. Making
it fully public would have exposed the algorithm's "operate over an explicit type
list" mechanics as a permanent part of the public contract, when the *actual*
public contract WP 2.1's brief asked for was "scans loaded assemblies." Keeping
it `internal`, exposed to the test assembly via `InternalsVisibleTo`, satisfied
both: a genuinely testable core algorithm, and a public surface that says exactly
what it's meant to say.

**Testing via real assembly scanning of a single, shared test assembly.** This was
tried conceptually and rejected once the actual test fixtures were designed:
having deliberately duplicate-ID fixtures, invalid-metadata fixtures, and
ignored-shape fixtures (interfaces, abstract classes, open generics) *all*
compiled into the same test assembly meant that any test attempting a genuine,
whole-assembly `Assembly.GetTypes()` scan of that assembly would immediately trip
over the duplicate/invalid fixtures that *other* tests needed to exist, and throw
before completing. This is documented at length in the WP 2.1 completion
report's "Architectural decisions" section and is one of the more instructive
lessons of this work package — see Common Mistakes below.

**Throwing a generic exception for duplicate IDs.** Explicitly rejected by the
brief itself, which asked for "a dedicated exception," and reinforced by the
project's established Fail Fast convention (see the Engineering Principles
section): a generic `InvalidOperationException` or `ArgumentException` would tell
a caller *that* something went wrong, but not *what* — `DuplicateModuleIdException`
carries the actual colliding ID as a structured property, not just embedded in a
message string.

## 7. Why This Solution Was Chosen

The interface-based, reflection-driven design was chosen because it required no
new infrastructure beyond what .NET already provides (`System.Reflection`), gave
compile-time safety over module metadata, and — critically — imposed no
constraints on module authors beyond "implement three read-only properties and
have a public parameterless constructor." This kept WP 2.1 genuinely minimal, in
line with its explicit non-goals, while still being extensible: the *next*
module anyone writes requires zero changes to discovery's own code, satisfying
the Open/Closed Principle concretely rather than as an aspiration.

The internal/public split for the two `DiscoverModules` overloads was chosen
specifically because it let the *real* algorithm — filtering, validation,
deduplication, ordering — be tested deterministically, independent of whatever
happens to be loaded into the test process's `AppDomain` at the moment a test
runs, which is inherently unsuitable for a reliable unit test in the first place.

## 8. Architectural Principles

- **Single Responsibility** — discovery finds modules; it does not register,
  orchestrate, or construct them for use. See the Single Responsibility
  Engineering Principle document.
- **Interface Segregation** — `IModule` carries only what every module,
  regardless of behaviour, must expose; behavioural capability (lifecycle) was
  deliberately deferred to a separate interface in a later work package rather
  than bolted on here.
- **Fail Fast** — a duplicate ID, or invalid metadata, is reported immediately,
  with full context, rather than silently tolerated or discovered later as some
  unrelated symptom.
- **Deterministic Systems** — reflection APIs make no ordering promises;
  discovery imposes one explicitly (ascending, ordinal, by `Id`) so behaviour is
  reproducible regardless of assembly-load order or `Assembly.GetTypes()`'s
  internal enumeration order.
- **Defensive Programming** — `ReflectionTypeLoadException` (a real-world
  condition where some types in a scanned assembly fail to load) is handled
  explicitly, so one problematic type in one assembly cannot crash an entire
  discovery pass.

## 9. Benefits

- New modules require zero changes to discovery, registration, lifecycle, or
  dependency injection code — they are found automatically the next time
  discovery runs, simply by existing and implementing `IModule`.
- Discovery's output (`ModuleDescriptor`) is immutable and safe to share freely
  with every downstream consumer (WP 2.2 onward) without synchronisation concerns.
- The deterministic ordering guarantee means discovery's output is directly,
  reliably testable — a property that turned out to matter significantly once
  WP 2.3 needed to build its own ordering guarantee on top of discovery's output.
- The `internal` test seam pattern established here (a public, spec-compliant
  entry point plus an internal, deterministic core for testing) was reused
  directly in WP 2.2 and WP 2.3, becoming a recognisable TempestOS convention
  rather than a one-off trick.

## 10. Trade-offs

- Modules must have a public parameterless constructor to be discoverable at all
  — no constructor-parameter-based configuration is possible at the module type
  level (this limitation was later addressed, for the *lifecycle-time* instance,
  by WP 2.4's dependency injection container; discovery's own transient metadata
  probe still requires parameterless construction, and always will, since it
  happens before any container exists — see ADR-0008).
- Every discoverable type in every loaded assembly is transiently instantiated on
  every discovery pass, which depends entirely on the convention that module
  constructors are cheap and side-effect-free (ADR-0003) — a convention enforced
  by nothing but documentation and discipline, not by the compiler.
- `AppDomain.CurrentDomain.GetAssemblies()` only sees assemblies already loaded
  into the process — discovery cannot find modules in plugin DLLs sitting
  unloaded on disk (the `src/Plugins/` directory that already exists in the
  repository) unless something else loads them first. This gap was noted
  explicitly in WP 2.1's own completion report and remains open.

## 11. Common Mistakes

The most instructive mistake avoided during WP 2.1's design, rather than made and
fixed, was the temptation to test discovery by scanning the actual, shared test
assembly end-to-end. The test assembly necessarily contains fixtures representing
*every* category discovery needs to reject or detect — duplicate IDs, invalid
metadata, ignored type shapes — because those are exactly what the required test
categories ("discovery, duplicate detection, invalid types, and ordering") need
to exercise. Scanning that whole assembly for a "happy path" test would
immediately encounter the deliberately-broken fixtures meant for *other* tests
and throw. The fix — an `internal` overload operating over an explicit type list,
reserving the one truly whole-assembly-scan test for `Tempest.Core`'s own
assembly (which, at this point in the pipeline, contains no modules at all, and
correctly returns an empty result) — is the kind of design decision that looks
obvious in hindsight but is easy to get wrong the first time, by reaching
instinctively for "just scan the real thing" as the most realistic-looking test.

A related mistake to watch for in any future work touching discovery: assuming
`Activator.CreateInstance`'s exceptions are self-explanatory. A module with no
public parameterless constructor throws a generic, unhelpful exception from deep
inside reflection machinery if this isn't guarded against explicitly — worth
remembering if discovery's filtering logic is ever extended.

## 12. Future Evolution

- **Plugin/external assembly loading.** If TempestOS ever needs to discover
  modules from assemblies not already loaded into the process (the empty
  `src/Plugins/` directory suggests this is anticipated), a loading step needs
  designing before discovery's `AppDomain.CurrentDomain.GetAssemblies()` default
  becomes useful for that scenario — discovery itself would not need to change,
  only what feeds it assemblies.
- **Attribute-based or hybrid discovery.** If interface-only discovery ever
  proves too restrictive (for modules that can't cleanly implement a shared
  interface, for instance), an attribute-based alternative could be introduced
  as an additional discovery strategy alongside the reflection-based one, behind
  the same `IFrameworkDiscoveryService` contract.
- **Ordering metadata.** Discovery's alphabetical ordering is explicitly
  documented, in its own code, as an implementation convenience rather than a
  permanent design commitment — see ADR-0004's sibling discussion in WP 2.3 for
  the equivalent note on lifecycle ordering. A dedicated priority/order property
  on `IModule`, if ever introduced, would change discovery's sort key without
  requiring any change to its filtering or validation logic.

## 13. Key Takeaways

1. A discovery mechanism's *output* (`ModuleDescriptor`) mattered more to the rest
   of the system than its *mechanism* (reflection) — every later work package
   depends on the shape of `ModuleDescriptor`, none of them depend on how it's
   produced.
2. Determinism has to be imposed deliberately on top of reflection APis that make
   no ordering guarantees — this is not a detail, it's a prerequisite for the
   entire system being testable at all.
3. Testing code that fundamentally operates over reflection and ambient process
   state requires a deliberate seam (the internal type-list overload) — reaching
   for "just test the real thing" produces tests that interfere with each other
   by design, not by accident.
4. A convention with no compiler enforcement (constructors are side-effect-free)
   can still be load-bearing for the rest of the system's correctness — it just
   has to be documented loudly enough that nobody violates it by accident. That
   is precisely what this Academy, and ADR-0003 specifically, exist to do.

# ADR-0027: A Declarative `ModuleMetadataAttribute` Decouples Discovery From Construction

## Status

Accepted — v0.4.0, WP 4.4A, 2026-07-24. Resolves the architectural
limitation `WP 4.3`'s own design and implementation phases identified and
deliberately did not solve: a discovered module cannot receive any
constructor-injected, DI-public platform service, and `WP 4.4` needs
exactly that (`IEventBus`, ADR-0020) for its own already-approved
Deliverable extending the `WP 4.3` sample module. Architecture only; no
production code accompanies this decision — see `Module Dependency
Injection Architecture.md` for the full design this ADR resolves, and the
WP 4.4A retrospective for the complete investigation.

## Context

### The current construction pipeline, traced exactly

```
Assembly
  │
  ▼
ReflectionFrameworkDiscoveryService.DiscoverModules()
  │   for each candidate Type:
  │     Activator.CreateInstance(type)   <-- (1) throwaway instance, zero-arg
  │     read Id/Name/Version from the instance
  │     validate, check for duplicate Id
  │     discard the instance
  ▼
ModuleDescriptor (Id, Name, Version, ModuleType)
  │
  ▼
RuntimeModuleManager.Register(descriptor)   <-- stores the descriptor only;
  │                                             never constructs anything
  ▼
ServiceCollection.AddDiscoveredModules(descriptors)
  │   registers each descriptor.ModuleType as a DI singleton,
  │   keyed by its own concrete type
  ▼
TempestServiceProvider (built from the now-complete ServiceCollection)
  │
  ▼
ModuleLifecycleManager.InitialiseModuleAsync
  │   tracked.Instance = ResolveInstance(descriptor)
  │     => _serviceProvider.GetService(descriptor.ModuleType)   <-- (2) the
  │        real instance, fully DI-resolved, every constructor
  │        parameter recursively resolved (verified directly in
  │        TempestServiceProvider.Construct)
  │   invoke InitialiseAsync, then StartAsync, on that one instance
  ▼
Running Module
```

**Every module is constructed twice, at two different points, for two
different reasons — a fact ADR-0008 already named explicitly** ("a module
class gets constructed at least twice over its life if it's ever
discovered and later initialised — once by discovery (thrown away), once
by the lifecycle manager via DI (kept)"). Instantiation (1), Discovery's
own metadata probe, is the one this ADR is about. Instantiation (2),
`TempestServiceProvider.Construct`, already fully supports constructor
dependencies today — verified directly: every constructor parameter is
resolved recursively through the same container, exactly like any other
registered service. **The real, lifecycle-driving instance could already
receive `IEventBus` via ordinary constructor injection, today, with zero
change — if it could ever get *discovered* in the first place.**

### Where, and why, `Activator.CreateInstance` is required

Exactly one call site: `ReflectionFrameworkDiscoveryService`'s type-scanning
loop, non-public overload `DiscoverModules(IEnumerable<Type>
candidateTypes)` (both the public, `AppDomain`-scanning overload and
`TempestHostBuilder`'s `discoveryCandidateTypesOverride` test seam funnel
into this one method). For every candidate type that passes
`IsValidModuleType` (assignable to `IModule`, concrete, non-generic), it
calls `(IModule)Activator.CreateInstance(type)!` — the zero-argument
overload, which requires a public **parameterless** constructor — reads
`Id`/`Name`/`Version` from the resulting instance, validates them, and
discards the instance immediately. **This call is unconditional and
uncaught.** If the type's sole public constructor takes any parameter, the
zero-argument `Activator.CreateInstance(Type)` overload throws
(`MissingMethodException`, no matching constructor) — inside Discovery's
own loop, with no per-candidate isolation around it, propagating all the
way to `TempestHost.RunAsync`'s outer catch: **a Host-fatal crash (ADR-0013),
not an isolated module failure**, for what is conceptually a single
module's own, unremarkable design choice.

### What depends on parameterless construction, and what does not

- **Depends on it:** only this one metadata probe. Nothing about
  `ModuleDescriptor` (already just `Id`/`Name`/`Version`/`ModuleType`, an
  immutable value with no construction logic of its own),
  `RuntimeModuleManager` (never constructs anything — registers
  descriptors only), or `ModuleLifecycleManager`'s own resolution path (2)
  requires a parameterless constructor; (2) already resolves whatever
  constructor shape `TempestServiceProvider.Construct`'s own existing rule
  requires — exactly one public constructor, of any arity.
- **Does not depend on it:** `TempestHost`'s phase ordering (`Host
  Lifecycle.md`) is unaffected either way — Module Discovery (Phase 4)
  still precedes Module Registration (Phase 5), which still precedes
  Platform Services Registered (Phase 6) and Dependency Injection Built
  (Phase 7), per ADR-0011, for reasons entirely independent of this
  question (Registration needs every `Id` known before the container is
  built, to detect duplicates and populate `AddDiscoveredModules` — this
  ADR does not change *when* Discovery or Registration run, only *how*
  Discovery reads three strings).

## Decision

**A new, optional attribute, `Tempest.Core.Modules.ModuleMetadataAttribute`,
lets a module declare its `Id`/`Name`/`Version` on the type itself, so
Discovery can read them without constructing anything.** When present,
`ReflectionFrameworkDiscoveryService` reads `Id`/`Name`/`Version` directly
from the attribute and does **not** call `Activator.CreateInstance` for
that candidate at all. When absent, Discovery's behaviour is **completely,
byte-for-byte unchanged**: instantiate via the zero-argument overload, read
the three instance properties, discard the instance — exactly what every
module already does today.

```csharp
[ModuleMetadata("tempest.events.publisher", "Event Publisher", "1.0.0")]
public sealed class EventPublisherModule : ModuleLifecycleBase
{
    private readonly IEventBus _eventBus;

    public EventPublisherModule(IEventBus eventBus)
        : base("tempest.events.publisher", "Event Publisher", "1.0.0")
    {
        _eventBus = eventBus;
    }

    // IModule.Id/Name/Version are still ordinary instance properties,
    // inherited from ModuleBase exactly as today - the attribute exists
    // for Discovery's benefit only, and its values must match.
}
```

A module carrying the attribute is free to declare any single public
constructor `TempestServiceProvider.Construct` can already resolve —
including one requiring `IEventBus`, or any other DI-public platform
service — because Discovery never attempts to construct it with zero
arguments. The real, lifecycle-driving instance is still constructed
exactly once, exactly where it already is: `ModuleLifecycleManager`'s
existing `ResolveInstance` call, during Module Initialisation (Phase 8),
via `TempestServiceProvider` — unchanged.

### Answering the brief's own architecture questions directly

| Question | Answer |
|---|---|
| Should Discovery instantiate modules at all? | Only when it must — for modules without the attribute, exactly as today. For attribute-carrying modules, no. |
| Can metadata be obtained without constructing an instance? | Yes — a class-level attribute, read via ordinary reflection (`Type.GetCustomAttribute`), needs no instance. |
| Should construction move entirely into the DI container? | It already has, for the one construction that matters (the real, lifecycle-driving instance) — since WP 2.4 (ADR-0007). This decision removes the *second*, throwaway construction for modules that opt in; it does not move anything that wasn't already there. |
| Should module metadata become declarative? | Optionally, yes — for modules that need it. Mandatorily, no — every existing module's instance-property metadata remains fully supported, unchanged, forever. |
| Should `ModuleDescriptor` evolve? | No. It already holds exactly `Id`, `Name`, `Version`, `ModuleType` — sufficient regardless of which mechanism produced those three strings. |
| Should `RuntimeModuleManager` own construction? | No — unchanged; it has never constructed anything and does not start now. |
| Should `TempestHost` own construction? | No — unchanged; it has never constructed a module instance directly (ADR-0007) and does not start now. |
| Should module activation become a separate lifecycle stage? | No. Construction already happens at exactly the right point — Module Initialisation (Phase 8), via DI — for every module, attribute-based or not. Introducing a new phase for something that already happens correctly, at the correct point, would be exactly the speculative complexity this release's own principles argue against. |

## Consequences

**Positive:**

- **Exactly one real construction path, for every module, unchanged.**
  `TempestServiceProvider.Construct`, called from
  `ModuleLifecycleManager.ResolveInstance`, remains the only place any
  module's real, lifecycle-driving instance is ever created — this
  decision removes a *second, throwaway* instantiation for modules that opt
  in; it does not introduce a second *real* one. There is no duplicate
  construction path and no parallel activation mechanism: attribute or
  not, every module is activated by the same call, at the same phase.
- **Every existing module is completely unaffected.** `ModuleBase`,
  `ModuleLifecycleBase`, `ClockModule`, and every test fixture across the
  codebase carry no attribute and take the exact code path they take
  today — verified directly against `ReflectionFrameworkDiscoveryService`'s
  own existing, unmodified fallback behaviour. Nothing is deprecated,
  nothing is migrated, nothing breaks.
- **A latent bad failure mode is fixed as a side effect, for modules that
  opt in.** Today, a module with an accidentally non-parameterless
  constructor crashes the Host outright (an uncaught
  `MissingMethodException` inside Discovery). A `[ModuleMetadata]`-carrying
  module's constructor is instead resolved by `TempestServiceProvider` at
  Module Initialisation time, inside `ModuleLifecycleManager`'s own
  existing try/catch — so a genuine construction problem (missing
  dependency, ambiguous constructor) becomes an ordinary **isolated**
  module failure (ADR-0013), not a Host-fatal one. Not this decision's
  purpose, but a direct, welcome consequence of where real construction
  already happens.
- **ADR-0004's reasoning is untouched.** A `Registered`-but-never-
  `Initialised` module still holds no constructed instance, attribute or
  not — construction still happens only at Module Initialisation, exactly
  as before. Permissive disposal from `Registered` remains sound for
  exactly the same reason it always was.
- **No layering violation.** The attribute lives in `Tempest.Core.Modules`
  (the same Platform API layer `IModule` already occupies); Discovery
  (Host-owned, ADR-0017) reads it; nothing about ADR-0023's downward-only
  direction changes — a module still depends only on `Tempest.Core`, never
  the reverse.
- **Fully consistent with ADR-0020.** `IEventBus` remains exactly as
  DI-public as that ADR decided; this ADR does not touch where `IEventBus`
  lives or how it is registered — it only removes the one obstacle
  preventing a *module* from constructor-injecting it.

**Negative:**

- **A module author who opts in must keep the attribute and the instance
  properties in agreement by hand** — Discovery reads the attribute for
  `[ModuleMetadata]`-carrying modules and never cross-checks it against the
  eventually-constructed instance's own `Id`/`Name`/`Version`. This is a
  real, named risk, structurally identical to the one already accepted for
  `PluginManifest`'s own declared `Version` versus a loaded module's real
  `IModule.Version` (`Plugin Manifest Architecture.md`, Versioning
  Strategy) — not solved here, for the same reason it is not solved there:
  no current consumer needs the cross-check enforced, and inventing one
  now would be exactly the speculative validation this release's
  principles argue against.
- **Two metadata-reading code paths now exist inside one method**
  (attribute-present vs. attribute-absent), where one existed before. This
  is judged a small, well-contained, clearly-branched cost — not a
  duplicate *construction* path (there remains exactly one of those) — in
  exchange for zero breaking change to any existing module.
- **The Module SDK does not (yet) offer a convenience for the
  attribute-based path.** A module wanting both `ModuleLifecycleBase`'s
  convenience methods and a DI-injected constructor writes the attribute
  and the constructor by hand, today. See Future Considerations.

## Alternatives Considered

**Defer all metadata reading until after the DI container is built**,
resolving the real instance first and reading `Id`/`Name`/`Version` from
it for both registration and lifecycle purposes, eliminating the throwaway
instance entirely. Rejected: `RuntimeModuleManager.Register` (duplicate-Id
detection) and `ServiceCollection.AddDiscoveredModules` (which needs every
module's concrete type to register it) both need every module's `Id`
*before* the container is built — inverting this would require Discovery
and Registration to follow, not precede, Dependency Injection Built,
directly contradicting ADR-0011's already-decided ordering (itself a
consequence of ADR-0008's independent Discovery). Recorded as RD-0016.

**A second, always-parameterless "descriptor" type per module**, distinct
from the module's own real implementation type — Discovery instantiates
only the lightweight descriptor; the descriptor names the real
implementation type for DI to construct later (mirroring the Manifest/
Plugin split `WP 4.2` already established). Rejected: this would require
every module wanting DI access to author two classes instead of one,
reintroducing exactly the per-module boilerplate `WP 4.1`'s SDK exists to
eliminate — a materially heavier cost than one attribute, for the same
result. Recorded as RD-0017.

**Static abstract interface members on `IModule`** (`static abstract
string Id { get; }`, C# 11+), read via reflection on the `Type` itself,
avoiding attributes entirely. Rejected: `IModule`'s existing,
instance-property contract would need to change for every module ever
written — `ModuleBase`, `ClockModule`, every test fixture — a breaking
change of a scale this ADR's own problem does not justify, when an
additive, opt-in attribute solves the identical problem for the one
category of module that actually needs it. Recorded as RD-0018.

**A service-locator or ambient static accessor** (a module reaching
`IEventBus` some way other than its own constructor). Not seriously
re-evaluated here — already rejected, for this exact class of problem, as
RD-0007 during `WP 4.1`, which named precisely this ADR's own arrival as
the correct resolution path ("if the underlying constraint is ever
lifted, it should be lifted at the Discovery/`TempestServiceProvider`
level… with its own ADR — not worked around at the SDK level a second
time"). This ADR is that ADR; RD-0007's own rejection of the workaround
stands, unreversed. **Property injection**, for the same reason, was never
seriously considered — ADR-0006 already forbids it categorically, for
reasons unrelated to and unaffected by this decision.

## Future Considerations

**Module SDK convenience.** If attribute-based, constructor-injected
modules become common, `ModuleBase`/`ModuleLifecycleBase` could gain a
documented pattern or minor convenience for declaring the attribute
alongside the base constructor call — not decided here, since no second
consumer exists yet beyond `WP 4.4`'s own anticipated need. Revisit once
`WP 4.4` (or a later work package) actually builds a second such module.

**Attribute/property agreement.** If divergence between a module's
`[ModuleMetadata]` attribute and its own instance properties ever proves a
real, recurring problem (not merely a theoretical risk), a future,
narrowly-scoped validation (for example, `ModuleLifecycleManager`
cross-checking the resolved instance's own properties against the
descriptor it was given, once, after first resolution) would be the
correct, additive fix — not a reason to remove the attribute path.

**This ADR does not implement anything.** `WP 4.4`'s own first
implementation step should be exactly this design — the
`ModuleMetadataAttribute` type and `ReflectionFrameworkDiscoveryService`'s
small, additive branch — before attempting to extend the `WP 4.3` sample
module with event publishing, mirroring exactly how `ADR-0025`/`ADR-0026`
were implemented as part of `WP 4.2`'s own implementation after being
designed separately.

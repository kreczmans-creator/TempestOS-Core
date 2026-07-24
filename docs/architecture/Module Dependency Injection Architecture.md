# Module Dependency Injection Architecture

**Status: architecture only — WP 4.4A. No production code exists yet.**
This document is the design behind ADR-0027, produced the same way
`WP 4.2A`–`4.2C` preceded `WP 4.2`'s own implementation: a prerequisite,
architecture-only work package, resolved before the work package that
actually needs it (`WP 4.4`) begins.

## Objective

Determine how a discovered module may obtain DI-managed Platform Services
— specifically, the DI-public `IEventBus` (ADR-0020) `WP 4.4` must let the
`WP 4.3` sample module consume — while preserving every architectural
principle established throughout `WP 2.x`/`WP 4.x`. This document is the
fuller design narrative behind ADR-0027; the ADR itself is the authoritative
decision record — read it first for the decision, this document for the
complete picture around it.

## Repository Investigation

### The current construction pipeline, traced exactly

See ADR-0027's own Context section for the complete, annotated pipeline
diagram (`Assembly → ReflectionFrameworkDiscoveryService → ModuleDescriptor
→ RuntimeModuleManager → ServiceCollection/TempestServiceProvider →
ModuleLifecycleManager → Running Module`), traced directly against the
current source of `ReflectionFrameworkDiscoveryService.cs`,
`RuntimeModuleManager.cs`, `ModuleLifecycleManager.cs`, and
`TempestServiceProvider.cs` — not inferred from documentation.

**The one finding this investigation turned up beyond what `WP 4.3`
already knew**: every module is constructed *twice*, and the second
construction — `TempestServiceProvider.Construct`, called from
`ModuleLifecycleManager.ResolveInstance` during Module Initialisation —
**already fully supports constructor-injected dependencies today.** Every
constructor parameter is resolved recursively through the same container,
exactly like any other registered service; nothing about
`TempestServiceProvider` needs to change at all. The entire limitation is
confined to the *first* construction — `ReflectionFrameworkDiscoveryService`'s
own metadata probe — which exists for exactly one reason: reading three
string properties (`Id`, `Name`, `Version`) before a `ModuleDescriptor` can
be built, before Registration, before the container even exists.

### What already depends on parameterless construction, precisely

Exactly one thing: `ReflectionFrameworkDiscoveryService`'s
`Activator.CreateInstance(type)` call, in its internal
`DiscoverModules(IEnumerable<Type>)` method. Nothing else in the pipeline —
not `ModuleDescriptor`, not `RuntimeModuleManager`, not
`ModuleLifecycleManager`'s own resolution call — requires it. This was
verified by reading each type's own source directly, not assumed from
`WP 4.1`'s original documentation of the constraint.

### What does not need to change, and why that matters

`Host Lifecycle.md`'s phase table, `Runtime State Machine.md`, `Failure
Behaviour.md`, ADR-0011's ordering (Discovery/Registration precede DI
Container construction), and `TempestHost.cs`'s own phase sequence are
**every one of them unaffected** by this design. The problem is small and
precisely located; the solution should be too.

## Architecture

### The proposed solution

A new, optional, class-level attribute — `Tempest.Core.Modules.
ModuleMetadataAttribute` — carrying the same three values `IModule`
already requires (`Id`, `Name`, `Version`). `ReflectionFrameworkDiscoveryService`
reads it via `Type.GetCustomAttribute<ModuleMetadataAttribute>()`, entirely
without instantiating the candidate type, when present; when absent, it
falls back to exactly today's behaviour, unchanged. See ADR-0027's own
Decision section for the complete design and worked example.

### Ownership

| Concern | Owner | Changed by this design? |
|---|---|---|
| Declaring a module's metadata | The module author, via `IModule`'s instance properties (unchanged) or, optionally, `ModuleMetadataAttribute` (new) | Additive only |
| Reading metadata during discovery | `ReflectionFrameworkDiscoveryService` | Gains one new, optional branch; existing branch unchanged |
| Registering descriptors | `RuntimeModuleManager` | Unaffected |
| Populating the DI container | `ServiceCollection.AddDiscoveredModules`, `TempestHost` | Unaffected |
| Constructing the real, lifecycle-driving instance | `TempestServiceProvider`, via `ModuleLifecycleManager.ResolveInstance` | Unaffected — already supports this |
| Driving the module's lifecycle | `ModuleLifecycleManager` | Unaffected |

No new owner is introduced. Every existing owner keeps exactly the
responsibility it already had.

### Lifecycle Impact

No new `Host Lifecycle.md` phase, no new `HostState`, no new transition.
Module Discovery (Phase 4) still precedes Module Registration (Phase 5),
Platform Services Registered (Phase 6), and Dependency Injection Built
(Phase 7), exactly per ADR-0011 — this design changes *what happens inside*
Phase 4's own existing loop for one candidate at a time, not the ordering
of phases around it. Module Initialisation (Phase 8) is exactly where
construction already happens, for every module, attribute-based or not —
see ADR-0027's own answer to "should module activation become a separate
lifecycle stage?" (no).

A module carrying `[ModuleMetadata]` and a DI-dependent constructor flows
through the exact same ten-value `ModuleState` machine as any other module
— `Registered → Initialising → Initialised → Starting → Running →
Stopping → Stopped → Disposed`, with `Failed` reachable from any
non-terminal state, unchanged. A construction failure (a missing
dependency, an ambiguous constructor) now surfaces as an ordinary,
isolated `ModuleState.Failed` (ADR-0013) rather than a Host-fatal crash —
see ADR-0027's own Consequences for why this is a welcome, if incidental,
correction.

## Public Surface

| Type | Kind | New? |
|---|---|---|
| `Tempest.Core.Modules.ModuleMetadataAttribute` | Sealed attribute class (`AttributeTargets.Class`) | Yes — the only new type this design introduces |

No new interface, no new exception type (existing `ModuleDiscoveryException`
already covers invalid attribute values, exactly as it covers invalid
instance-property values today), no change to `IModule`, `IModuleLifecycle`,
`ModuleBase`, `ModuleLifecycleBase`, `ModuleDescriptor`, or any existing
public method signature anywhere in the pipeline.

## Migration Strategy

**Nothing migrates.** This is the central property of an additive, opt-in
design: every module that exists today — `ModuleBase`, `ModuleLifecycleBase`,
`ClockModule`, every test fixture across the codebase — takes the exact
code path it takes today, forever, whether or not this design is ever
implemented. There is no deprecation, no required rewrite, and no
migration window.

For a **new** module needing a DI-public service:

1. Add `[ModuleMetadata(id, name, version)]` to the class, with the same
   literal values already passed to `ModuleBase`'s constructor.
2. Give the class a constructor accepting whatever DI-public service(s) it
   needs, alongside calling `base(id, name, version)` if it derives from
   `ModuleBase`/`ModuleLifecycleBase` — no SDK change required, since
   `ModuleBase`'s own constructor is already `protected`, called via
   `base(...)`, entirely independent of whatever additional parameters the
   derived class's own public constructor declares.
3. Nothing else changes — the module is discovered, registered, and driven
   through its lifecycle exactly like any other module.

**Recommended implementation order, when `WP 4.4` begins:** build
`ModuleMetadataAttribute` and `ReflectionFrameworkDiscoveryService`'s new
branch *first*, prove it against a small, dedicated test module (not
`ClockModule` — the sample module is explicitly not to be modified by this
work package, and should remain the "legacy path" living reference it
already is), *then* extend the sample module's own future companion or
extension with the attribute once `IEventBus` itself exists.

## Testing Implications

Prospective — no test is written by this work package. When implemented:

- **Discovery, attribute-present.** A dedicated test module carrying
  `[ModuleMetadata]` and a constructor whose parameterless invocation would
  itself throw if ever attempted (proving, positively, that Discovery
  never constructs it) is discovered correctly, with metadata read from
  the attribute.
- **Discovery, attribute-absent (regression).** Every existing Discovery
  test continues to pass completely unmodified — the single strongest
  proof that this design is genuinely additive, not merely designed to be.
- **Mixed batch.** Attribute-based and legacy modules discovered together,
  in the same pass, with no interference — mirroring `WP 4.3`'s own
  `DiscoverModules_AlongsideAnUnrelatedModule_FindsBothWithoutInterference`
  pattern.
- **Full pipeline, DI success.** An attribute-based module requiring a
  registered dependency resolves correctly and reaches `Running` through
  the real, composed pipeline — mirroring `WP 4.3`'s own
  `ClockModulePipelineTests` composition pattern exactly.
- **Full pipeline, isolated failure.** An attribute-based module requiring
  an *unregistered* dependency fails in isolation (`ModuleState.Failed`,
  the Host still reaching `Running`) rather than faulting the Host —
  proving the "welcome side effect" ADR-0027 names is real, not merely
  argued.
- **Invalid attribute values.** Null/empty/whitespace `Id`/`Name`/`Version`
  on the attribute produce the same `ModuleDiscoveryException` an invalid
  instance property already does today — no new exception type, no new
  failure category.

## Validation Against Governing Documents

- **`FOUNDATION.md`.** Every non-negotiable principle holds: exactly one
  responsibility per component, unchanged (②); no new mutable or
  externally-writable state (③) — `ModuleMetadataAttribute` is itself
  immutable, read-only after construction, exactly like `ModuleDescriptor`;
  the platform-service/module failure boundary is not touched, and is in
  one respect made *more* correct (④); nothing about disposal-order
  guarantees changes (⑤); no new batch or interruption boundary is
  introduced (⑥); this ADR is the seventh instance of principle ⑦ in this
  release; dependencies still flow downward only (⑨).
- **`Platform Services Architecture Review.md`.** Consistent with every
  strength that review confirmed. Responds directly to Recommendation 3
  (documentation structural completeness) by stating explicitly, in the
  ADR itself, exactly which existing documents do and do not need updating
  — see Documentation Impact, below.
- **ADR-0009.** Not engaged directly — `ModuleMetadataAttribute` is read by
  Discovery, not constructed by the composition root, and does not touch
  `AddInstance` or any composition-root registration concern.
- **ADR-0013.** Reinforced, not reopened — a module's construction failure
  remains an isolated module failure; this design corrects one path that
  had accidentally escaped that classification (Discovery's own uncaught
  crash) back into it.
- **ADR-0017.** Untouched — Discovery, Registration, and Lifecycle remain
  exactly as Host-owned and non-DI-public as before; nothing about this
  design gives a module any new path back into them.
- **ADR-0020.** Directly enabled, not altered — `IEventBus` remains
  DI-public, registered exactly where that ADR already places it; this
  design only removes the one obstacle preventing a module from
  constructor-injecting it.
- **ADR-0023.** Preserved — the new attribute lives in `Tempest.Core.Modules`
  (Platform API layer, alongside `IModule` itself); no dependency points
  upward or sideways as a result of this design.

## Documentation Impact

- **New**: ADR-0027; this document; a WP 4.4A Academy retrospective; three
  new Rejected Designs entries (RD-0016, RD-0017, RD-0018); a forward
  cross-reference added to RD-0007 (not superseded — its own rejection of
  a service-locator workaround stands; only its named revisit path is now
  fulfilled).
- **Updated, preemptively, marked architected-not-implemented** (mirroring
  exactly how `ADR-0026`'s own architecture phase updated `Host
  Lifecycle.md` before `WP 4.2` implemented it): `Platform Service Map.md`'s
  Discovery entry; `Building a Module.md`'s "One Constraint You Still Need
  to Know About" section; `Sample Module Architecture.md`'s "Required
  ADRs" section (now resolved, not merely identified).
- **Not required**: no `Host Lifecycle.md`/`Runtime State Machine.md`/
  `Failure Behaviour.md` change — nothing about the Host's own phases,
  states, or failure model changes, as established above.

## Implementation Recommendation

**Design is sound; `WP 4.4` may now begin, starting with exactly this
design's own implementation** (`ModuleMetadataAttribute` and
`ReflectionFrameworkDiscoveryService`'s new, additive branch) **before**
attempting to extend the `WP 4.3` sample module with event publishing —
mirroring precisely how `ADR-0025`/`ADR-0026` were implemented as part of
`WP 4.2`'s own implementation, immediately after being designed
separately. No further ADR is anticipated before `WP 4.4` can proceed.

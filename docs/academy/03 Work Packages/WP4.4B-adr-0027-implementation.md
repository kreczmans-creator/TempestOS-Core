# WP 4.4B — ADR-0027 Implementation

## 1. Introduction

WP 4.4B implements `ModuleMetadataAttribute` exactly as ADR-0027 and
`Module Dependency Injection Architecture.md` designed it — removing
Discovery's own dependency on parameterless construction, for modules
that opt in, while every module that does not remains completely
unaffected. Unlike the two architecture-only phases immediately before it
(`WP 4.4A`, and `WP 4.3`'s own design phase), this work package produces
real, tested production code — the first change to
`ReflectionFrameworkDiscoveryService` since `WP 2.1`.

## 2. Purpose

To realise ADR-0027's decision precisely, prove it against dedicated test
modules rather than the sample module, and demonstrate — not merely
argue — that `RuntimeModuleManager`, `ModuleLifecycleManager`,
`TempestHost`, `TempestServiceProvider`, and `ClockModule` all continue
operating exactly as before.

## 3. Background

`WP 4.4A` traced the construction pipeline precisely and found that
`TempestServiceProvider.Construct` already supports constructor-injected
dependencies; the entire limitation was confined to Discovery's own,
separate, throwaway metadata probe. ADR-0027 decided the fix: an optional,
class-level `ModuleMetadataAttribute`, read without instantiating the
candidate type, falling back to today's exact behaviour when absent. This
work package's own brief was explicit that it implements ADR-0027 only —
no event publishing, no change to `ClockModule`, no other component
touched.

## 4. The Problem

1. **Implement exactly the algorithm ADR-0027 specifies** — inspect type;
   attribute present, read from it; attribute absent, execute the
   existing construction path unchanged — without introducing a second
   construction path or any speculative extension.
2. **Prove backward compatibility**, not merely preserve it by not
   touching anything — every existing Discovery test must continue to
   pass completely unmodified, and `ClockModule` must remain untouched.
3. **Prove constructor injection is now genuinely possible**, end-to-end,
   not just at the Discovery level — through the real, composed pipeline,
   and through the real, unmodified `TempestHost`.
4. **Touch nothing else** — `RuntimeModuleManager`, `ModuleLifecycleManager`,
   `TempestHost`, `TempestServiceProvider`, and the Host's own lifecycle
   phases are explicitly out of this work package's own scope.

## 5. The Design

See `src/Tempest.Core/Modules/ModuleMetadataAttribute.cs` and the modified
`ReflectionFrameworkDiscoveryService.cs` in full — implemented without
deviation from ADR-0027's own worked example and algorithm. A new,
private `CreateDescriptor(Type)` method replaces the discovery loop's
previously-inline instantiate-and-read logic: it checks
`Type.GetCustomAttribute<ModuleMetadataAttribute>()` first; if present, it
builds a `ModuleDescriptor` directly from the attribute's own values,
never calling `Activator.CreateInstance`; if absent, it performs exactly
the same instantiate-then-read sequence Discovery has always performed.
`ValidateMetadata` was refactored from accepting an `IModule` instance to
accepting three plain strings, so the same null/empty/whitespace checks —
and the same `ModuleDiscoveryException` — govern both mechanisms
identically, without duplicating the validation logic.

## 6. Alternatives Considered

None — this work package implements an already-decided ADR exactly, per
its own explicit brief. No new architectural alternative was evaluated;
see `WP 4.4A`'s own retrospective for the alternatives ADR-0027 itself
weighed and rejected (RD-0016, RD-0017, RD-0018).

## 7. Why This Solution Was Chosen

Not applicable in the usual sense — the solution was chosen by `WP 4.4A`.
This work package's own judgment calls were narrower: refactoring
`ValidateMetadata` to accept plain strings rather than duplicating its
three checks for the attribute path was chosen because it keeps validation
behaviour for both mechanisms provably identical (one method, one set of
rules) rather than two copies that could quietly drift apart over time —
a direct application of the same "one responsibility" discipline ADR-0027
itself argued for.

## 8. Architectural Principles

- **Reuse Before Invention** — `ValidateMetadata`'s existing logic is
  reused for both mechanisms via a small signature change, not duplicated.
- **Minimal Host Complexity** — confirmed, not merely claimed: `git diff`
  against `TempestHost.cs`, `RuntimeModuleManager.cs`,
  `ModuleLifecycleManager.cs`, `TempestServiceProvider.cs`, and
  `ClockModule.cs` is empty.
- **Backward Compatibility as a Provable Property, Not an Intention** —
  every pre-existing Discovery test (`ReflectionFrameworkDiscoveryServiceTests`,
  `PluginManifestDiscoveryServiceTests`, `ClockModuleDiscoveryTests`, and
  every Host-level test) passes completely unmodified, verified by running
  the full, pre-existing suite before adding a single new test.
- **Constructor Injection Through Normal DI Patterns** — every new test
  module uses an ordinary public constructor; no service locator, no
  property injection, no static access anywhere in the implementation or
  its tests.

## 9. Benefits

- **`WP 4.4` is now unblocked** — a discovered module may declare a
  constructor requiring any DI-public platform service, proven end-to-end
  against a real, unmodified `TempestHost` injecting a real `ILogger`,
  with zero test-only wiring.
- **Every existing module, including `ClockModule`, is proven — not
  merely assumed — unaffected**: the full, pre-existing 260-test suite
  passes unmodified, and `ClockModule.cs` itself has an empty diff.
- **The incidental correction ADR-0027 predicted is now demonstrated**:
  an attribute-based module with an unregistered dependency fails in
  isolation (`ModuleState.Failed`), not as a Host-fatal crash — proven
  by a dedicated test, not merely argued in the ADR's own prose.

## 10. Trade-offs

- Discovery's internal `CreateDescriptor` method now branches on whether
  the attribute is present — a small, deliberate increase in that one
  method's own internal complexity, in exchange for zero change to every
  other component in the pipeline.
- The attribute/instance-property agreement risk ADR-0027 named remains
  exactly as accepted, unaddressed by this implementation — no validation
  was added to detect divergence, consistent with that decision.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: testing "constructor
injection now works" only at the Discovery level (a descriptor was
produced) without also proving it through the real, composed pipeline and
the real Host. A module could be discovered correctly and still fail to
resolve if `TempestServiceProvider`'s own behaviour had been
misunderstood — proving it three ways (Discovery alone, the manually
composed pipeline, and the real `TempestHost`) closes that gap rather
than assuming the middle layer works because the two ends do.

## 12. Future Evolution

- **`WP 4.4`'s own event-publishing work** may now proceed, using
  `[ModuleMetadata]` and an ordinary constructor requiring `IEventBus`,
  exactly as this implementation's own `HostInjectedModule` test fixture
  demonstrates for `ILogger`.
- **A Module SDK convenience** for the attribute-based path remains
  un-added, per `WP 4.4A`'s own Future Considerations — revisit once a
  second real consumer exists beyond `WP 4.4`.
- **Attribute/instance-property agreement validation** remains un-added,
  per the same reasoning — revisit only if divergence proves a real,
  recurring problem.

## 13. Key Takeaways

1. Implementing an already-fully-designed ADR closely is a narrow,
   low-risk exercise precisely because the hard questions were already
   answered — this work package's only real judgment call was a small,
   internal refactor (`ValidateMetadata`'s new signature) to avoid
   duplicating validation logic, not a new design decision.
2. "Backward compatible" is a claim worth proving by running the exact
   pre-existing test suite before writing a single new test, not an
   assumption to carry forward from the design phase.
3. Proving a capability end-to-end (Discovery, the composed pipeline, and
   the real Host) catches gaps a single-layer proof would miss, at a
   modest, worthwhile cost in test count.

---

## Architectural Debt Assessment

**No new debt introduced.** The attribute/instance-property agreement
risk is unchanged from what ADR-0027 already named and accepted — not new
debt created by implementing it. Every other debt item on record from the
Runtime Foundation, WP 4.0–4.3, WP 4.2D, and WP 4.4A remains exactly as
previously described.

## Observations

- **Files added**: `src/Tempest.Core/Modules/ModuleMetadataAttribute.cs`;
  `tests/Tempest.Core.Tests/Modules/ModuleMetadataAttributeFixtures.cs`;
  `tests/Tempest.Core.Tests/Modules/ModuleMetadataAttributeDiscoveryTests.cs`;
  `tests/Tempest.Core.Tests/Modules/ModuleMetadataAttributePipelineTests.cs`.
- **Files modified**: `src/Tempest.Core/Modules/ReflectionFrameworkDiscoveryService.cs`
  only — the class-level XML remarks, and the discovery loop's metadata-
  reading step, refactored into `CreateDescriptor`/a re-signatured
  `ValidateMetadata`. **Zero other production file was modified** —
  confirmed directly: `git diff --stat` against `RuntimeModuleManager.cs`,
  `ModuleLifecycleManager.cs`, `TempestHost.cs`, `TempestServiceProvider.cs`,
  and `ClockModule.cs` is empty.
- **Tests added**: 18 — metadata discovered from the attribute without
  instantiation (1); a parameterised-constructor module discovered
  successfully (1); a zero-argument module with the attribute still
  discovered (1); the legacy fallback path unchanged, both success and
  invalid-metadata regression (2); attribute precedence over a working,
  differently-valued constructor (1); malformed attribute values — empty
  Id, whitespace Name, empty Version (3); duplicate identity, both within
  the attribute mechanism and across it and the legacy mechanism (2);
  mixed-assembly discovery alongside the real `ClockModule` (1);
  deterministic ordering across a mixed batch (1); repeatable discovery
  (1); the full, really-composed pipeline resolving a registered
  dependency and completing initialisation (1); the same, with an
  unregistered dependency, isolated rather than Host-fatal (1); the real,
  unmodified `TempestHost` constructor-injecting a real `ILogger` and
  reaching `Running`/`Stopped` (1); `ClockModule` alongside a
  constructor-injected module, both reaching `Running` with no
  special-casing (1).
- **Test results**: 278 of 278 passing (260 pre-existing + 18 new), 0
  failures, verified stable across 5 consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors.
- **Platform changes outside Discovery**: none. `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `TempestHost`, `TempestServiceProvider`,
  `Host Lifecycle.md`'s phase table, and `ClockModule` are byte-for-byte
  unchanged.
- **Readiness assessment**: WP 4.4B is complete. ADR-0027 is implemented
  exactly as designed. Constructor injection into a discovered module is
  now real, proven at three levels (Discovery, the composed pipeline, and
  the real Host). `WP 4.4`'s own event-publishing implementation may now
  begin.

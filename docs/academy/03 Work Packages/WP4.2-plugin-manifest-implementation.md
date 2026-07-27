# WP 4.2 — Plugin Manifest Implementation

## 1. Introduction

WP 4.2 implements the Plugin Manifest system `Plugin Manifest
Architecture.md` designed, ADR-0025 (*Plugin Failure Classification*)
classified, and ADR-0026 (*Plugin Discovery Lifecycle Placement*)
sequenced. Unlike WP 4.2/4.2A/4.2B/4.2C before it, this work package
produces real, tested production code: `Tempest.Core.Plugins`, wired into
`TempestHost` exactly where ADR-0026 places it.

## 2. Purpose

To build `PluginManifest`, its exception hierarchy, `PluginManifestDiscoveryService`
(Plugin Discovery, Phase 3.1), and `PluginAssemblyLoader` (Plugin Loading,
Phase 3.2), and wire both into `TempestHost.RunAsync` between Logging
Built and Module Discovery — closing the gap `Runtime Host
Architecture.md` named since WP 2.7A, without altering Module Discovery,
Module Registration, or Module Lifecycle in any way.

## 3. Background

By the time this work package began, every architectural question had
already been settled: what a manifest contains and excludes, how failures
are classified (ADR-0025), and exactly where the two new phases sit in
`Host Lifecycle.md`'s table (ADR-0026). This work package's own brief was
explicit that it must not revisit any of those decisions unless a genuine
defect was discovered — none was. Every design choice below traces
directly back to one of the four prior WP 4.2x documents.

## 4. The Problem

1. **Realise the design exactly**, without smuggling in new architectural
   decisions under the guise of "implementation detail."
2. **Prove Module Discovery truly needs zero code changes** — not merely
   assert it, as the architecture document did, but demonstrate it with a
   real, dynamically-built plugin assembly.
3. **Implement ADR-0025's eleven-category failure table faithfully**,
   including the one Host-fatal carve-out, without either over-catching
   (silently swallowing a genuine Host-level bug) or under-catching
   (letting a plugin-scoped failure fault the whole platform).
4. **Test comprehensively without mocks**, per the brief's own explicit
   instruction to prefer temporary directories and real, dynamically-built
   assemblies — proving the pipeline against real `Assembly.LoadFrom`
   calls and real JSON files, not simulated stand-ins.

## 5. The Design

See `Tempest.Core.Plugins` in full, and `Plugin Manifest Architecture.md`'s
own "Public API — As Implemented" section for the complete type-by-type
mapping from design to code. In summary:

- **`PluginManifest`**: sealed, immutable, exactly as designed, plus one
  addition — `AssemblyPath`, the fully-resolved absolute path (folder +
  declared `AssemblyFileName`), computed once at discovery time so Plugin
  Loading never needs the manifest's own folder passed around separately.
- **`PluginManifestDiscoveryService`**: scans a plugins root (default:
  `Plugins`, relative to `AppContext.BaseDirectory`; an internal
  constructor overload accepts an explicit root for tests, mirroring
  `ReflectionFrameworkDiscoveryService`'s own test-seam pattern). Each
  immediate subdirectory is a candidate; a candidate is expected to
  contain `plugin.manifest.json`. Candidates are sorted ordinally by
  folder name before processing (ADR-0026's determinism requirement).
  Every plugin-scoped failure is caught via a single `catch (PluginException)`
  around per-candidate processing (exactly as the architecture document
  anticipated) and logged at the severity ADR-0025's table assigns; only
  an exception that is *not* a `PluginException` propagates, faulting the
  Host — proven directly, not merely reasoned about (see Section 9).
- **`PluginAssemblyLoader`**: loads each eligible manifest's
  `AssemblyPath` via `Assembly.LoadFrom`, isolating a missing file
  (`PluginAssemblyNotFoundException`) or a load failure
  (`PluginAssemblyLoadException`, catching `BadImageFormatException`/
  `FileLoadException`/`IOException` specifically around the `LoadFrom`
  call itself, not the whole method). Returns only the assemblies that
  loaded successfully, in the same order as its input.
- **`TempestHost.ExecuteStartupPhasesAsync`**: `PlatformVersionProvider`'s
  construction moved to immediately follow Logging Built (its DI
  registration, `services.AddInstance(platformVersionProvider)`, stayed
  exactly where WP 4.2A placed it, inside Platform Services Registered —
  construction and registration are separable, and ADR-0026 only required
  moving the former). Plugin Discovery and Plugin Loading run
  immediately after, before `ReflectionFrameworkDiscoveryService` is even
  constructed. Both `TempestHost` and `TempestHostBuilder` gained an
  internal `pluginsRootPathOverride` test seam, mirroring the existing
  `discoveryCandidateTypesOverride` seam exactly.

## 6. Alternatives Considered

**A single `IPluginAssemblyLoader`/`PluginAssemblyLoader` interface pair,
not originally named in `Plugin Manifest Architecture.md`.** Added during
implementation, not treated as a new architectural decision: it keeps
Plugin Loading a separate, independently-testable service from Plugin
Discovery, mirroring `IFrameworkDiscoveryService`/`RuntimeModuleManager`'s
own existing two-service split. It does not change where either phase
sits, what it depends on, or its failure behaviour — purely an
implementation-shape choice within a design that already called for "two
phases, not one" (ADR-0026).

**Testing "Module Discovery is unaffected" against a full, real
`AppDomain` scan inside `TempestHost`.** Investigated and rejected: the
test assembly (`Tempest.Core.Tests`) contains many `internal`-visibility
`IModule` fixtures across its own test files (`HostTestFixtures.cs`,
`ModuleFixtures.cs`, and others), which `ReflectionFrameworkDiscoveryService`
— running from `Tempest.Core`, with no `InternalsVisibleTo` back into the
test assembly — cannot construct via `Activator.CreateInstance`. A full
scan during any test would risk faulting on those, entirely unrelated to
plugins. "Assembly visibility to Module Discovery" is instead proven
precisely, and without that hazard, by scoping a fresh
`ReflectionFrameworkDiscoveryService` to just the one newly-loaded plugin
assembly directly — the exact same, completely unchanged discovery
mechanism the Host itself uses, minus the unrelated noise.

## 7. Why This Solution Was Chosen

Every implementation decision traces back to one of the four prior WP 4.2x
documents; none required a new architectural judgment call. Where the
brief's own required test scenarios (comprehensive coverage, "prefer real
assemblies over mocks") met a genuine engineering obstacle — proving a
Host-fatal, unattributable failure without fabricating an artificial fault
hook, or building a real loadable plugin assembly without a second csproj
— the solution chosen was always the one that stayed closest to
production code's real behaviour: `System.Reflection.Emit.PersistedAssemblyBuilder`
(added in .NET 9, no new package dependency — ADR-0005) builds and saves a
genuinely valid, loadable PE assembly at test time, and a real,
badly-behaved `IPlatformVersionProvider` test double (one whose `Version`
getter throws) proves the Host-fatal contract via an actually-realistic
scenario — a broken supporting service, not a contrived filesystem
edge case.

## 8. Architectural Principles

- **The Manifest describes; the Runtime decides** — realised exactly:
  `PluginManifest` carries no behaviour, only data; every decision (accept,
  isolate, load) is the Host's own services' responsibility.
- **Fail one plugin, not the platform** (ADR-0025) — implemented via a
  single `catch (PluginException)` boundary in both new services, never a
  blanket `catch (Exception)`, so the isolation guarantee and the
  Host-fatal carve-out are the same code path, not two.
- **Deterministic Startup** — candidate folders sorted ordinally by name
  before any processing, proven by a dedicated ordering test using
  deliberately out-of-alphabetical creation order.
- **Reuse Before Invention** — Module Discovery, Module Registration, and
  Module Lifecycle are untouched; "assembly visibility" rests entirely on
  `AppDomain.CurrentDomain.GetAssemblies()`'s pre-existing behaviour, used,
  not extended.

## 9. Benefits

- **Zero code changes to Module Discovery, Registration, or Lifecycle** —
  not merely claimed, verified: `ReflectionFrameworkDiscoveryServiceTests`
  and every other existing test in the 215-test suite passed unmodified,
  and a new test
  (`PluginAssemblyLoaderTests.LoadPlugins_LoadedAssembly_IsVisibleToUnchangedModuleDiscovery`)
  proves a plugin's module is found by the exact same, untouched service.
- **The Host-fatal contract is proven, not just asserted by code
  inspection**: `DiscoverManifests_UnexpectedNonPluginException_PropagatesUncaught`
  demonstrates that a defect outside ADR-0025's classification (a
  malfunctioning `IPlatformVersionProvider`) is not swallowed by the
  per-candidate isolation boundary.
- **A real, non-mocked dynamic assembly builder**
  (`DynamicPluginAssemblyBuilder`, using `PersistedAssemblyBuilder`) now
  exists in the test suite, available to any future work package
  (Sample Module, `WP 4.3`, in particular) that needs a genuinely loadable
  test assembly rather than a hand-maintained fixture project.
- **27 new tests**, 0 regressions across 5 consecutive full-suite runs.

## 10. Trade-offs

- **A test-only assembly-identity collision was found and fixed during
  this work package**: two test methods building two different dynamic
  assemblies that happened to share a file name ("Valid.dll") collided,
  because `Assembly.LoadFrom` resolves by assembly *identity*
  (name+version), not by file path, in the default `AssemblyLoadContext` —
  the second test's "load" silently returned the first test's
  already-loaded assembly instance. Fixed by giving each dynamically-built
  assembly a GUID-suffixed internal identity, independent of its on-disk
  file name.
- **A pre-existing xUnit test-isolation hazard was found and fixed**: a
  new test class (`TempestHostPluginLifecycleTests`) that redirects the
  process-global `Console.Out` to capture log output could run
  concurrently with the existing `TempestHostTests` (which does the same),
  corrupting each other's captured output — the identical hazard already
  found and fixed once before, between `ModuleLifecycleBaseTests` and
  `ModuleSdkIntegrationTests` (see the WP 4.1→4.2 transition). Fixed the
  same way: `[Collection("Console output capture")]` on both classes.
- **The plugins root directory and manifest file name are fixed
  conventions**, not configurable in this release — consistent with
  ADR-0026's own note that Plugin Discovery has no hard dependency on
  Configuration. A future work package may make either configurable
  without needing to revisit anything decided here.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: treating "Module
Discovery requires no code change" as a claim that a *unit test asserting
so* would satisfy on its own. A test that never actually loads a real
assembly and checks Discovery finds it would only prove the claim
*conditionally* — dynamically building and loading a genuine PE assembly,
then handing it to the real, unmodified `ReflectionFrameworkDiscoveryService`,
is what actually proves the architecture's central load-bearing claim
rather than merely restating it in test form.

## 12. Future Evolution

- **`WP 4.3` (Sample Module)** can now package itself as a real plugin
  (a `plugin.manifest.json` alongside its compiled assembly) to validate
  the full pipeline end-to-end with a genuine, non-synthetic consumer, or
  continue as an ordinary discovered module — both paths now work
  identically once Module Discovery runs.
- **`WP 4.5` (Background Services)**, per ADR-0026's own Future
  Considerations, should follow this work package's precedent (and
  ADR-0026's) for inserting its own new phase — decimal sub-numbering, no
  renumbering, explicit entry/exit criteria — rather than re-deriving the
  question from scratch.
- **A future diagnostics capability (`WP 4.8`)** can build the
  queryable "which plugins failed and why" structure ADR-0025's Future
  Considerations named — not designed or built here, since no current
  consumer needs it yet.
- **Plugins root/manifest file name configurability** remains available
  as a purely additive future change, if a real need for it emerges.

## 13. Key Takeaways

1. An implementation work package that follows a fully-resolved
   architecture faithfully should surface *zero* new architectural
   decisions — this one surfaced exactly one implementation-shape choice
   (`IPluginAssemblyLoader`) and confirmed it was shape, not substance, by
   checking it against every dimension ADR-0026 already fixed (phase
   placement, dependencies, failure behaviour) and finding none disturbed.
2. Testing a "the existing system needs no change" claim is only as
   strong as what the test actually exercises — a real, dynamically-built
   assembly loaded through the real loader into the real, unmodified
   discovery service proves the claim; a test that stops short of loading
   anything real would not have.
3. Cross-test hazards (shared global state such as `Console.Out`, or
   process-wide assembly-identity resolution) are easy to reintroduce
   even when the underlying lesson was already learned once — the fix in
   both cases here was recognizing the *same* pattern from an earlier
   work package, not discovering a new one.

---

## Architectural Debt Assessment

**No new debt introduced.** Every debt item on record from the Runtime
Foundation, WP 4.0/4.1, and WP 4.2/4.2A/4.2B/4.2C remains exactly as
previously described. The one gap `Plugin Manifest Architecture.md`'s own
Risks section still names — no assembly-unloading support — is
unchanged and unaffected by this work package, consistent with ADR-0015.

## Observations

- **Files added** (`src/Tempest.Core/Plugins/`): `PluginManifest.cs`,
  `PluginException.cs`, `InvalidPluginManifestException.cs`,
  `IncompatiblePluginVersionException.cs`, `DuplicatePluginIdException.cs`,
  `PluginAssemblyNotFoundException.cs`, `PluginAssemblyLoadException.cs`,
  `PluginManifestDto.cs`, `PluginFailureLogging.cs`,
  `IPluginManifestDiscoveryService.cs`, `PluginManifestDiscoveryService.cs`,
  `IPluginAssemblyLoader.cs`, `PluginAssemblyLoader.cs` (13 new production
  files).
- **Files modified**: `TempestHost.cs` (Plugin Discovery/Loading wired in;
  `PlatformVersionProvider` construction moved per ADR-0026);
  `TempestHostBuilder.cs` (new internal `pluginsRootPathOverride` test
  seam, mirroring the existing `discoveryCandidateTypesOverride` seam).
- **Test files added**: `Plugins/TempDirectory.cs`,
  `Plugins/DynamicPluginAssemblyBuilder.cs`,
  `Plugins/FakePlatformVersionProvider.cs`,
  `Plugins/RecordingLevelLogger.cs`,
  `Plugins/PluginManifestDiscoveryServiceTests.cs`,
  `Plugins/PluginAssemblyLoaderTests.cs`,
  `Runtime/TempestHostPluginLifecycleTests.cs`.
- **Tests added**: 27 — successful manifest parsing; malformed JSON;
  missing required field (each of the five fields, via `Theory`);
  unparseable `MinimumPlatformVersion`; incompatible
  `MinimumPlatformVersion` (Information severity) vs. compatible
  (accepted); duplicate plugin identity (first-folder-wins); deterministic
  ordinal ordering regardless of creation order; absent/empty plugins
  directory; a candidate folder missing its manifest file; one candidate's
  failure not affecting siblings; a genuine, unattributable exception
  propagating uncaught (Host-fatal contract); the internal seam trusting
  given order; successful assembly load; missing assembly; corrupt
  assembly load failure; isolated failures not affecting sibling loads;
  a loaded assembly's visibility to real, unmodified Module Discovery;
  Host-level phase-ordering (Plugin Discovery/Loading before Module
  Discovery, via log inspection); a missing plugins directory still
  reaching `Running`; an isolated manifest failure still reaching
  `Running`; multiple simultaneous isolated failures still reaching
  `Running`.
- **Test results**: 242 of 242 passing (215 pre-existing + 27 new), 0
  failures, verified stable across 5 consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors.
- **Regressions found and fixed during this work package** (both
  test-only, no production behaviour affected): a cross-test dynamic
  assembly identity collision (fixed via GUID-suffixed assembly names);
  an xUnit `Console.Out` redirection race between two test classes (fixed
  via a shared `[Collection]`, mirroring the established
  `SdkLifecycleLog` precedent from WP 4.1→4.2).
- **Readiness assessment**: WP 4.2 is complete. Every prerequisite named by
  `Plugin Manifest Architecture.md` (WP 4.2A, ADR-0025, ADR-0026) is
  resolved and now realised in working code. No architectural blocker,
  and no known implementation gap, remains for this feature.

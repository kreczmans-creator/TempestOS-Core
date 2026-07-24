# Plugin Manifest Architecture

**Status: implemented — WP 4.2 (`Tempest.Core.Plugins`).** Every type and
behaviour this document describes is now backed by working, tested code,
not only design intent. All three prerequisites this document originally
named were resolved before implementation began (platform version —
WP 4.2A; failure classification — ADR-0025, WP 4.2B; lifecycle placement —
ADR-0026, WP 4.2C). See this document's own Recommendation section, and
"Implementation Notes (WP 4.2)" below, for exactly what was built and where
it differs in small, non-architectural ways from what was originally
proposed.

## Overview

The Plugin Manifest describes a module before it is loaded. It answers
"who is this, what does it need, is it safe to load" using data sitting on
disk, readable without executing or reflecting over a single line of the
plugin's own assembly. It does not load the module, does not construct it,
does not inject its dependencies, and does not drive its lifecycle — those
remain exactly where WP 2.1 through WP 2.3 already put them, untouched.

**The Manifest describes. The Runtime decides.**

## Where This Sits in the Existing Architecture

`Runtime Host Architecture.md`'s Future Extensibility section already
named this gap during WP 2.7A: "Plugins — loading assemblies from disk…
would need to happen *before* Module Discovery… so that Discovery's
`AppDomain.CurrentDomain.GetAssemblies()` default actually sees them." That
sentence is the entire architectural anchor for everything below. A plugin
manifest is a **pre-discovery** artifact — it describes something not yet
loaded into the process — as distinct from `ModuleDescriptor`, which is a
**post-discovery** artifact describing something already loaded and
already reflectable. This distinction is the single most important fact
this design rests on.

## Responsibilities Matrix

| Component | Responsibility | Change from today |
|---|---|---|
| **Plugin Discovery** *(Host-owned — Phase 3.1, ADR-0026 — `PluginManifestDiscoveryService`)* | Scans a known plugins directory for manifest files, parses and validates each one, produces a list of `PluginManifest` values. Loads no assembly. | New — implemented |
| **Plugin Loading** *(Host-owned — Phase 3.2, ADR-0026 — `PluginAssemblyLoader`)* | For each manifest that passes validation and the platform-version compatibility check, loads its declared assembly file into the process. | New — implemented |
| **Module Discovery** *(existing, `IFrameworkDiscoveryService`)* | Scans **all** loaded assemblies — including plugin assemblies Plugin Loading just loaded — for `IModule` types, exactly as today. | **Unchanged** |
| **Module Registration** *(existing, `RuntimeModuleManager`)* | Registers whatever descriptors Module Discovery finds. Has no plugin-specific logic at all. | **Unchanged** |
| **Module Lifecycle** *(existing, `ModuleLifecycleManager`)* | Drives registered modules through initialisation, startup, shutdown, disposal — a module that arrived via a plugin is indistinguishable from one that didn't. | **Unchanged** |
| **Module SDK** *(existing, WP 4.1)* | `ModuleBase`/`ModuleLifecycleBase` are available to, and require nothing different from, a module shipped inside a plugin. | **Unchanged** |

The load-bearing claim in this table is the "Unchanged" column. If this
design requires changing Discovery, Registration, or Lifecycle, it has
failed its own brief. It does not: Module Discovery's existing
`AppDomain.CurrentDomain.GetAssemblies()` default already sees any
assembly loaded into the process by any means, including one this design's
new Plugin Loading step loads via `Assembly.LoadFrom`. Nothing about
*how* an assembly arrived in the AppDomain is Discovery's concern today,
and this design does not ask it to become one.

## Manifest Content — Required, Optional, and Explicitly Excluded

| Field | Required? | Justification |
|---|---|---|
| `Id` | Required | Mirrors `IModule.Id`. Lets the Runtime decide "have I already got this plugin" *before* loading its assembly — loading is not free and, per this release's non-goals, cannot be undone without a full process restart (ADR-0015). |
| `Name` | Required | Mirrors `IModule.Name`. |
| `Version` | Required | The plugin's own version — a plain string, matching `IModule.Version`'s existing (unvalidated-format) convention exactly. Not redesigned into a stricter type; reuse-first. |
| `MinimumPlatformVersion` | Required | The only genuinely new *comparable* value this manifest introduces — see Versioning Strategy. Required, not optional, so every plugin author makes an explicit compatibility claim rather than one being silently assumed. |
| `AssemblyFileName` | Required | A relative path (relative to the manifest's own location) to the assembly Plugin Loading should load. |

**Deliberately excluded from v0.4.0**, each challenged against "does this
have a real consumer today" and failing that test:

- **Publisher / Author.** No tooling, UI, or runtime logic consumes this
  today. Purely additive if a future work package needs it.
- **Description.** Same reasoning — no consumer exists (Navigation and
  Developer Experience, the two places a human-readable description might
  eventually surface, are both later, undesigned work).
- **An explicit "entry point type" field.** Considered directly — and
  rejected — see Alternative Designs Considered, below.
- **A maximum / "tested up to" platform version.** Considered — see
  Versioning Strategy.
- **A separate SDK version, distinct from platform version.** The Module
  SDK (WP 4.1) is not a separately-versioned package (see Rejected Design
  RD-0006) — while that remains true, "SDK version" and "platform version"
  are the same number, and a separate field would have no meaning to check
  against.

Every excluded field is cheap to add later (purely additive to an
immutable data type) — none was excluded because it would be expensive to
introduce, all were excluded because nothing consumes them yet.

## Public API — As Implemented (WP 4.2)

Every type below is implemented, in `Tempest.Core.Plugins`, exactly as
originally proposed — with one deliberate, non-architectural addition
(`IPluginAssemblyLoader`/`PluginAssemblyLoader`, and `PluginManifest`
carrying a resolved `AssemblyPath` alongside the declared
`AssemblyFileName`) called out explicitly below.

| Type | Kind | Shape | Mirrors |
|---|---|---|---|
| `PluginManifest` | Sealed, immutable class | `Id`, `Name`, `Version`, `MinimumPlatformVersion`, `AssemblyFileName`, `AssemblyPath` — all get-only, set once at construction | `ModuleDescriptor` exactly |
| `PluginException` | Base exception | Message + optional inner exception | `ModuleDiscoveryException`'s existing base/subtype pattern |
| `InvalidPluginManifestException : PluginException` | Sealed exception | Malformed JSON, or a required field missing/empty/whitespace | `ModuleDiscoveryException`'s subtypes |
| `IncompatiblePluginVersionException : PluginException` | Sealed exception | A well-formed manifest whose `MinimumPlatformVersion` exceeds the running platform's own version | New shape, same base-plus-subtype pattern |
| `DuplicatePluginIdException : PluginException` | Sealed exception | Two manifests declare the same `Id` (ADR-0025, category 3) | `DuplicateModuleRegistrationException`'s naming convention |
| `PluginAssemblyNotFoundException : PluginException` | Sealed exception | The manifest's declared `AssemblyFileName` does not exist (ADR-0025, category 5) | Same base-plus-subtype pattern |
| `PluginAssemblyLoadException : PluginException` | Sealed exception | `Assembly.LoadFrom` itself throws (ADR-0025, category 6) | Same base-plus-subtype pattern |
| `IPluginManifestDiscoveryService` / `PluginManifestDiscoveryService` | Interface / concrete service | One method, shaped like `IFrameworkDiscoveryService.DiscoverModules()`: scan, parse, validate, return `IReadOnlyList<PluginManifest>` | `IFrameworkDiscoveryService` directly — the same kind of service, one phase earlier |
| `IPluginAssemblyLoader` / `PluginAssemblyLoader` | Interface / concrete service | One method: load each manifest's declared assembly, return the ones that loaded successfully | **Not originally named in this document** — added during implementation to keep Plugin Loading (Phase 3.2) a separate, independently-testable service from Plugin Discovery (Phase 3.1), mirroring `IFrameworkDiscoveryService`/`RuntimeModuleManager`'s own two-service split. An implementation detail, not an architectural decision — it does not change where either phase sits, what it depends on, or its failure behaviour. |

**`AssemblyPath` was added to `PluginManifest`, beyond the fields originally
proposed above.** It is not a manifest *content* field — it is the fully
resolved, absolute form of the declared, manifest-relative
`AssemblyFileName`, computed once at discovery time (folder + declared file
name), exactly as `ModuleDescriptor.ModuleType` captures something derived
at discovery time rather than declared directly in `IModule`. Plugin
Loading needs an absolute path to call `Assembly.LoadFrom` against;
resolving it once during Discovery (when the manifest's own folder is
still in scope) rather than re-deriving it during Loading avoids passing
the folder path around separately.

**Two implementation-level conventions this document did not previously
fix, decided during WP 4.2:**

- **Manifest file name**: each plugin candidate folder must contain a file
  named `plugin.manifest.json`. A data-file-naming convention, not an
  architectural decision — no genuine alternative was contested.
- **Plugins root directory**: `Plugins`, relative to the application's own
  base directory (`AppContext.BaseDirectory`) — a fixed convention, per
  ADR-0026's own note that Plugin Discovery has no hard dependency on
  Configuration for this. A future work package may make this
  configurable; nothing here forecloses it.

All six exception types share one base (`PluginException`), consistent
with every other stage of the pipeline's own base-plus-subtype
convention (`ConfigurationException`, `ModuleDiscoveryException`,
`ModuleLifecycleException`) — a single `catch (PluginException)` at the
Plugin Discovery/Loading call site is sufficient to implement ADR-0025's
uniform "isolate, log, continue" handling for every category that ADR
classifies as isolated, without needing to catch each subtype separately.

**Not proposed**: an `IPluginManifestSource` abstraction generalising
*where* a manifest comes from (filesystem today, something else
hypothetically later). `IConfigurationSource` earned that abstraction
because multi-source configuration (files, environment variables, CLI
arguments) is a near-universal pattern in real software; "where do plugin
manifests live" has no comparable multi-source expectation for a v0.4.0
local platform. See Rejected Design RD-0008, below.

## Architectural Questions — Evaluated

The brief asked six concrete shapes to be evaluated explicitly, not
assumed.

| Shape | Verdict | Why |
|---|---|---|
| Immutable object | **Recommended** | Matches every existing descriptor/snapshot type in the platform (`ModuleDescriptor`, `RuntimeModule`, `ModuleLifecycleStatus`) without exception. |
| An interface (`IPluginManifest`) | Rejected | No second implementation is anticipated, ever — `ModuleDescriptor` itself is a concrete sealed class for the same reason. An interface here would be unjustified abstraction. |
| An attribute | Rejected | An attribute lives *inside* the compiled assembly. Reading it would require loading the assembly first — directly contradicting the entire reason a manifest exists (describing something *before* it is loaded). |
| Loaded from code | Rejected | Same defect as an attribute: "code" must be compiled and loaded to run, which is the exact step this design exists to defer. |
| **Loaded from JSON** | **Recommended** | A plain data file, parseable via `System.Text.Json` — already part of .NET, zero new dependency (ADR-0005) — without loading anything executable. |
| Generated | Rejected | The brief explicitly forbids source generators and code generation; a generated manifest would also still need *something* loadable to generate it from, reintroducing the same defect as the two rejections above. |
| Discoverable separately | **Recommended** | This is the design: a distinct "Plugin Discovery" step, scanning for manifest files independently of, and prior to, Module Discovery's own reflection-based type scan. |

## Versioning Strategy

- **Module Version** (the plugin's own): a plain string, unchanged in kind
  from `IModule.Version` today. Cross-checked once the plugin's assembly
  is actually loaded and Module Discovery reads the real `IModule.Version`
  from it — a mismatch between the manifest's claim and the loaded
  module's own value is a validation failure (the manifest lied, or is
  stale relative to the assembly sitting next to it).
- **Minimum Platform Version**: the one field that must be genuinely
  *comparable*, not just descriptive — the Runtime needs to answer "is
  this plugin compatible with me" before loading it. Recommended
  representation: .NET's built-in `System.Version` (`Major.Minor.Build`),
  parsed from a plain string field (e.g. `"0.4.0"`), compared with
  `>=` against the running platform's own version. No new dependency, no
  custom SemVer parser.
- **Maximum / "tested up to" platform version**: considered and rejected
  for v0.4.0 — see Rejected Design RD-0009. Recommend revisiting once real
  plugins and real version history exist to design a ceiling policy
  against, rather than guessing at one now.
- **Future SDK Version**: not distinct from Platform Version while the
  Module SDK remains part of `Tempest.Core` itself (RD-0006) — no separate
  field proposed.
- **Forward/backward compatibility**: a plugin declaring a minimum version
  lower than the current platform is forward-compatible by construction
  (it already declared itself willing to run on anything newer, up to
  whatever ceiling — if any — a future release decides to add). No
  backward-compatibility guarantee is proposed *from* the platform *to*
  old plugins beyond "the platform is not required to run a plugin whose
  minimum version exceeds its own" — this is the only guarantee v0.4.0
  needs.

**A real, blocking gap this design surfaced, not owned by the Manifest
itself — now resolved (WP 4.2A).** At the time this document was first
written, TempestOS did not expose its own running version as anything
queryable at runtime: no `<Version>` element existed in
`Directory.Build.props` or either project's `.csproj`, so compiled
assemblies carried the SDK's own default version (`1.0.0.0`), completely
disconnected from the real `VERSION` file (`0.3.0` at the time). A
`MinimumPlatformVersion` check would have been meaningless without
something authoritative to compare it against. **WP 4.2A** (*Runtime
Platform Version Infrastructure* — see `Platform Version.md`) closed this
gap directly: `IPlatformVersionProvider.Version.AssemblyVersion` is now
exactly what a future `MinimumPlatformVersion` check compares a manifest's
declared minimum against. See Risks and ADRs Required, below, for what
still remains open.

## Validation Strategy

| Rule | Detail |
|---|---|
| **Mandatory fields** | `Id`, `Name`, `Version`, `MinimumPlatformVersion`, `AssemblyFileName` — all required, non-null, non-empty, non-whitespace, mirroring `RuntimeModuleManager.Register`'s existing check style exactly. |
| **Optional fields** | None proposed for v0.4.0 — see the excluded-fields table above. |
| **Invalid values** | Malformed JSON; a missing or blank required field; an unparseable `MinimumPlatformVersion` string. All are **hard, per-manifest validation failures** — `InvalidPluginManifestException`. |
| **Incompatible-but-well-formed values** | A `MinimumPlatformVersion` that parses correctly but exceeds the running platform's own version — `IncompatiblePluginVersionException`. Deliberately a *different* exception type from a malformed manifest: one is a defect in the manifest, the other is a true, expected "this plugin simply doesn't run here" outcome. |
| **When** | At Plugin Discovery time — before any assembly is loaded, consistent with Fail Fast. |
| **Who owns it** | The new Plugin Discovery component, Host-owned — not Module Discovery, not Registration, neither of which is touched by this design at all. |
| **What happens on any of these failures** | **Fully classified — ADR-0025.** Every validation failure above is isolated to the one candidate plugin; the Host continues, every other plugin is still attempted. See that ADR for the complete, eleven-category table, including the failures that occur after a manifest is valid (missing/corrupt assembly, reflection failures, and so on). |

## Discovery Interaction — Direct Answers

- **Should Discovery read the Manifest?** No. `IFrameworkDiscoveryService`
  is untouched; it continues to scan whatever is loaded into the AppDomain,
  exactly as today.
- **Should Registration?** No. `RuntimeModuleManager` has no plugin-aware
  logic at all, before or after this design.
- **Should Runtime (the Host)?** Yes — Plugin Discovery (Phase 3.1) and
  Plugin Loading (Phase 3.2), both decided by ADR-0026, are new,
  Host-owned steps between Logging Built and Module Discovery, mirroring
  how Configuration and Logging are already Host-constructed steps
  preceding Discovery today.
- **Should nobody, until a later work package actually implements it?**
  This was true through WP 4.2A/B/C; **implemented — WP 4.2**:
  `PluginManifestDiscoveryService` and `PluginAssemblyLoader`
  (`Tempest.Core.Plugins`) now do exactly this, called from `TempestHost`
  in the order this document and ADR-0026 specify.

## Non-Goals (Restated From the Brief, Explicitly)

Not designed here, in any form: module *dependencies* (a plugin depending
on another plugin), package management, online repositories, downloads,
updates, security policy, permissions, a marketplace, sandboxing, remote
loading, dynamic unloading, or cross-process communication. Every one of
these is a real, plausible future need — none has enough real understanding
behind it yet to design responsibly, per this release's own governing
philosophy (WP 4.0).

## Alternative Designs Considered

**An explicit "entry point type" field in the manifest**, naming which CLR
type inside the loaded assembly is the module. Considered directly.
Rejected: it would duplicate logic Module Discovery already owns (finding
`IModule` types by scanning). Once Plugin Loading has loaded the assembly
into the AppDomain, Module Discovery's existing, unchanged scan finds the
module the same way it finds any other — reusing that logic instead of
inventing a second way to locate a module type is a direct application of
this release's reuse-first mandate.

**Treating an invalid or incompatible plugin as Host-fatal**, mirroring
platform-service failures (ADR-0013). Considered, and rejected — see
ADR-0025, *Plugin Failure Classification*, which settles this decision in
full (isolated, not Host-fatal, for every category except a genuine defect
in the Host's own plugin-loading orchestration itself). Recorded
permanently as Rejected Design RD-0010.

## Risks

- ~~The platform-version-at-runtime gap~~ **Resolved — WP 4.2A.** See
  Versioning Strategy, above.
- ~~**Loading an untrusted or malformed assembly file**~~ **Resolved —
  WP 4.2.** `PluginAssemblyLoader` catches `BadImageFormatException`,
  `FileLoadException`, and `IOException` around the `Assembly.LoadFrom`
  call specifically and translates them to `PluginAssemblyLoadException`
  (ADR-0025, category 6, Error severity) — proven directly by
  `PluginAssemblyLoaderTests.LoadPlugins_CorruptAssembly_IsIsolated_ExcludedAndLoggedAsError`,
  which loads a genuinely corrupt file (not a mock) and confirms isolation.
- **No assembly unloading support** (explicitly a non-goal) means a loaded
  plugin — bad or good — stays loaded for the process's entire life,
  consistent with, and no worse than, ADR-0015's existing "no restart"
  decision. A plugin fix requires a full process restart, exactly as a
  platform-service fix already does today.
- **Security is an accepted, named gap**, not a solved problem. Loading
  arbitrary local assemblies is inherently a trust decision this design
  does not attempt to mitigate — sandboxing, signing, and permissions are
  all explicit non-goals, not omissions.

## ADRs Required Before Implementation

Two decisions this design originally deliberately did not settle, because
both met Engineering Governance §5's ADR criteria (a genuine alternative
exists; the decision establishes a convention future plugin-related work
depends on). **Both are now resolved:**

1. ~~**Plugin failure classification**~~ — **Resolved — ADR-0025**, *Plugin
   Failure Classification*. Isolated for every category except a genuine
   defect in the Host's own plugin-loading orchestration itself, which
   remains Host-fatal — a full eleven-category classification table, not
   merely the headline principle.
2. ~~**Where Plugin Discovery and Plugin Loading sit in `Host Lifecycle.md`'s
   phase table.**~~ — **Resolved — ADR-0026**, *Plugin Discovery Lifecycle
   Placement*. Two new decimal-numbered phases (`3.1` Plugin Discovery,
   `3.2` Plugin Loading), inserted between Logging Built and Module
   Discovery, with no renumbering of the existing thirteen phases and no
   change to `Runtime State Machine.md`. (This was the risk already
   flagged, at the release level, in `docs/releases/v0.4.0/Risks.md`, R4 —
   now retired; see that register.)

~~A third matter needs resolving but is not, itself, a Plugin-Manifest
architectural decision: how the running platform's own version becomes
queryable at runtime.~~ **Resolved — WP 4.2A**, `IPlatformVersionProvider`
(`Tempest.Core.Versioning`), ahead of and independent of Plugin Manifest
implementation itself. See `Platform Version.md`.

## Recommendation

**Implemented — WP 4.2.** This closed the same two-phase pattern WP 2.7A/2.7B
and the release's own Navigation split (`WP 4.6A`/`4.6B`) both
established — architecture first, implementation only once every open
decision is actually settled, never implied:

1. ~~Write and ratify the two ADRs named above~~ — **both done**: failure
   classification (ADR-0025, WP 4.2B) and lifecycle placement (ADR-0026,
   WP 4.2C).
2. ~~Resolve the platform-version-at-runtime gap~~ — **done, WP 4.2A.**
3. ~~A future work package may now implement `PluginManifest`,
   `IPluginManifestDiscoveryService`, and the corresponding
   `Host Lifecycle.md`/`Runtime State Machine.md`/`Failure Behaviour.md`
   updates this design anticipated~~ — **done, WP 4.2**: `Tempest.Core.Plugins`
   implements every type this document named, `TempestHost` wires Plugin
   Discovery and Plugin Loading exactly where ADR-0026 places them, and
   every lifecycle document's own "architected, not yet implemented"
   status is now "implemented" — see each document's own status banner.

Implementation code, and a comprehensive test suite (27 tests — unit-level
coverage of `PluginManifestDiscoveryService`/`PluginAssemblyLoader`, plus
Host-level integration tests), now accompany this document. **`WP 4.2`
is complete.**

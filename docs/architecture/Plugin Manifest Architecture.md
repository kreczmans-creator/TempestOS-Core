# Plugin Manifest Architecture

**Status: architecture only. No production code, and no interfaces intended
for implementation, exist yet. Everything below is a design proposal — see
this document's own Recommendation section for whether implementation
should proceed as proposed.**

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
| **Plugin Discovery** *(new, Host-owned)* | Scans a known plugins directory for manifest files, parses and validates each one, produces a list of `PluginManifest` values. Loads no assembly. | New |
| **Plugin Loading** *(new, Host-owned)* | For each manifest that passes validation and the platform-version compatibility check, loads its declared assembly file into the process. | New |
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

## Candidate Public API

No code, no interfaces — the shapes below describe what a later
implementation work package would build, in the same spirit
`Runtime Host Architecture.md` named `TempestHost`/`TempestHostBuilder`
before WP 2.7B implemented either.

| Candidate Type | Kind | Shape | Mirrors |
|---|---|---|---|
| `PluginManifest` | Sealed, immutable class | `Id`, `Name`, `Version`, `MinimumPlatformVersion`, `AssemblyFileName` — all get-only, set once at construction | `ModuleDescriptor` exactly |
| `PluginManifestException` | Base exception | Message + optional inner exception | `ModuleDiscoveryException`'s existing base/subtype pattern |
| `InvalidPluginManifestException : PluginManifestException` | Sealed exception | Malformed JSON, or a required field missing/empty/whitespace | `ModuleDiscoveryException`'s subtypes |
| `IncompatiblePluginVersionException : PluginManifestException` | Sealed exception | A well-formed manifest whose `MinimumPlatformVersion` exceeds the running platform's own version | New shape, same base-plus-subtype pattern |
| `IPluginManifestDiscoveryService` | Interface | One method, shaped like `IFrameworkDiscoveryService.DiscoverModules()`: scan, parse, validate, return `IReadOnlyList<PluginManifest>` | `IFrameworkDiscoveryService` directly — the same kind of service, one phase earlier |

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

## Discovery Interaction — Direct Answers

- **Should Discovery read the Manifest?** No. `IFrameworkDiscoveryService`
  is untouched; it continues to scan whatever is loaded into the AppDomain,
  exactly as today.
- **Should Registration?** No. `RuntimeModuleManager` has no plugin-aware
  logic at all, before or after this design.
- **Should Runtime (the Host)?** Yes — Plugin Discovery and Plugin Loading
  are new, Host-owned steps, preceding Module Discovery in the startup
  sequence, mirroring how Configuration and Logging are already
  Host-constructed steps preceding Discovery today.
- **Should nobody, until a later work package actually implements it?**
  Correct, and explicit: this document is a design proposal only. No
  component reads a manifest today. Implementation is a distinct, later
  work package — see Recommendation, below.

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
platform-service failures (ADR-0013). Considered, and this document
recommends against it — see ADRs Required, below, for why this is flagged
as a decision rather than settled here.

## Risks

- ~~The platform-version-at-runtime gap~~ **Resolved — WP 4.2A.** See
  Versioning Strategy, above.
- **Loading an untrusted or malformed assembly file** (`Assembly.LoadFrom`)
  can throw for reasons having nothing to do with the manifest itself
  (a corrupt DLL, a missing native dependency). This must be caught and
  treated consistently with whatever the Plugin Discovery/Loading failure
  classification ADR decides — not left to propagate as an unhandled
  exception.
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

Two decisions this design deliberately does not settle, because both meet
Engineering Governance §5's ADR criteria (a genuine alternative exists; the
decision establishes a convention future plugin-related work depends on):

1. **Plugin failure classification** — is an invalid or incompatible
   plugin's failure Host-fatal (like a platform service, ADR-0013) or
   isolated (like an individual module, ADR-0013's other half)? This
   document's own reasoning leans toward isolated — a bad plugin should no
   more take down the Host than a bad module does — but this is exactly
   the kind of consequential, precedent-setting call ADR-0013 and ADR-0021
   both received their own ADRs for, not a detail to settle informally.
2. **Where Plugin Discovery and Plugin Loading sit in `Host Lifecycle.md`'s
   phase table.** This table was treated as complete and frozen after
   WP 2.7A/B; inserting new phases before Module Discovery needs the same
   rigour those phases originally received, not a quiet insertion. (Already
   flagged, at the release level, in `docs/releases/v0.4.0/Risks.md`, R4 —
   this document is the detailed design that risk anticipated.)

~~A third matter needs resolving but is not, itself, a Plugin-Manifest
architectural decision: how the running platform's own version becomes
queryable at runtime.~~ **Resolved — WP 4.2A**, `IPlatformVersionProvider`
(`Tempest.Core.Versioning`), ahead of and independent of Plugin Manifest
implementation itself. See `Platform Version.md`.

## Recommendation

**Design is sound; implementation should not begin yet.** This mirrors
WP 2.7A preceding WP 2.7B, and the release's own Navigation split
(`WP 4.6A`/`4.6B`) — architecture first, implementation only once the
open decisions above are actually settled, not implied. Specifically,
before an implementation work package begins:

1. Write and ratify the two ADRs named above (failure classification;
   phase-table placement) — still outstanding.
2. ~~Resolve the platform-version-at-runtime gap~~ — **done, WP 4.2A.**
3. Only then should a future work package implement `PluginManifest`,
   `IPluginManifestDiscoveryService`, and the corresponding
   `Host Lifecycle.md`/`Runtime State Machine.md`/`Failure Behaviour.md`
   updates this design anticipates.

No code, interfaces, or tests accompany this document, per this work
package's own scope.

# Plugin Platform Architecture

**Status: Implemented — `v0.13.0`.** Designed `WP 13.0A`; every design
below extends `Plugin Manifest Architecture.md`'s implemented (`WP 4.2`)
baseline. Implemented in full by `WP 13.1A` (`ADR-0107`–`ADR-0109`: the
fixed-point dependency-graph resolution and topological sort, the
Host-owned `PluginRegistry`/`IPluginRegistry`, the configurable plugins
root/manifest convention, `IDiagnosticsProvider.Plugins`) — not `WP 13.0B`,
which this document's own original Recommendation section (below) named
as the anticipated implementer; `WP 13.0B` was in fact commissioned as an
independent architecture review of this document instead, a disclosed
divergence recorded in `docs/releases/v0.13.0/WorkPackages.md`'s own
`WP 13.0B` row. `WP 13.3A`/`WP 13.3B` subsequently independently
re-verified every decision in this document against real, current source
end to end (no remaining gap) and, during that pass, found and fixed one
genuine registry-Id-spoofing defect adjacent to this document's own
`PluginRegistryEntry.Id` design (`WP 13.3B`) — this document's own
technical content was confirmed accurate throughout and required no
correction of its own. Corrected `WP 13.9.1` (`WP13.9.0 Engineering
Release Report.md`'s own Governance-readiness Finding 3) — only this
Status header and the stale `WP 13.0B` implementer citation in
Recommendation, below, were out of date; the technical content
throughout the rest of this document was independently confirmed still
accurate and is unchanged.

## Overview

`Plugin Manifest Architecture.md` answered "who is this, what does it
need, is it safe to load" for a single plugin, once, at process startup —
and, deliberately, left "does this plugin depend on another plugin,"
"where is the plugins directory configured from," "what is the plugin
catalogue once loading finishes," "how does a plugin get upgraded or
removed," and "can a plugin register a service" all as named non-goals,
each with its own cited reason. This document is where each of those
reasons is revisited, now that this release's own trigger — the Product
Owner's confirmed commitment to third-party plugin support (`FCR-0001`) —
makes every one of them a real, near-term question rather than a
speculative one.

**This document extends `Plugin Manifest Architecture.md`; it does not
replace it.** Every field, type, phase, and failure category that
document already settled remains exactly as it stood — `Id`, `Name`,
`Version`, `MinimumPlatformVersion`, `AssemblyFileName`, `AssemblyPath`,
`PluginManifestDiscoveryService`, `PluginAssemblyLoader`, all eleven of
ADR-0025's failure categories, and both of ADR-0026's phases (3.1, 3.2)
are unchanged. Everything below is additive.

**Explicit boundary with the sibling Security & Trust workstream.** This
document designs the plugin *platform's* shape — dependencies, discovery
extension, registry, lifecycle, version compatibility, DI boundaries,
upgrade/uninstall, and where commercial licensing could someday hook in.
It does **not** design trust levels, permission/capability semantics,
code-signing verification, the isolation mechanism (`AssemblyLoadContext`
or otherwise), or sandboxing — those belong to the Trust & Isolation
Architecture the sibling Security & Trust workstream owns, referenced
throughout this document by name, never duplicated. Two manifest fields
this document defines (`RequestedCapabilities`, `Publisher`/`Signature`)
are explicitly **shape only** — see Manifest v2, below — with their
semantics deferred entirely to that document. See "The Boundary With
Trust & Isolation," below, for the precise shape this document assumes
that architecture will fill.

## Where This Sits in the Existing Architecture

`Plugin Manifest Architecture.md`'s own anchor still holds unmodified: a
plugin manifest is a pre-Discovery artifact, Plugin Discovery/Loading are
Host-owned phases 3.1/3.2, and Module Discovery/Registration/Lifecycle
remain completely unaware of plugins. This document adds one more
Host-owned collaborator to that picture — the **Plugin Registry** — and
one more DI-public read-only surface — an extension to the already-shipped
`IDiagnosticsProvider` (`WP 5.2`, ADR-0039) — without moving, renaming, or
reordering anything `Plugin Manifest Architecture.md` or `Host
Lifecycle.md` already established.

```
Configuration Built (2)
Logging Built (3)
Plugin Discovery (3.1)         ── extended: per-manifest validation (unchanged,
   │                                ADR-0025) + dependency graph resolution
   │                                (new, ADR-0107) — still side-effect-free,
   │                                still one phase, no new phase number
   ▼
Plugin Loading (3.2)           ── extended: loads in dependency-topological
   │                                order (ADR-0107), not folder-name-only
   │                                order; populates the new Plugin Registry
   ▼
Module Discovery (4) ──────────── plugin-unaware (see note below)
   ⋮
Platform Services Registered (6) ── IDiagnosticsProvider gains a new
                                     read-only `Plugins` property (this
                                     document; no new Host-owned service
                                     is exposed, mirroring ADR-0017/
                                     ADR-0039's existing precedent exactly)
```

## Responsibilities Matrix

Extends `Plugin Manifest Architecture.md`'s own matrix; every row that
document already listed is reproduced only where this document changes
it.

| Component | Responsibility | Change from `WP 4.2` |
|---|---|---|
| **Plugin Discovery** *(Host-owned — Phase 3.1)* | Unchanged per-manifest validation (ADR-0025), **plus** dependency graph construction and fixed-point resolution (ADR-0107) over the surviving, individually-valid candidate set. Still loads no assembly. | Extended |
| **Plugin Loading** *(Host-owned — Phase 3.2)* | Loads each eligible plugin's assembly, now in **dependency-topological order** (ADR-0107), folder name remaining the deterministic tie-break. Populates the new **Plugin Registry** with a `PluginRegistryEntry` for every candidate, loaded or not. | Extended |
| **Plugin Registry** *(Host-owned, new — `IPluginRegistry`)* | The queryable catalogue of every plugin candidate this run attempted, and its outcome. Never DI-public — mirrors `IRuntimeModuleManager`'s own ADR-0017 boundary exactly. | **New** |
| **Diagnostics** *(DI-public, existing — `IDiagnosticsProvider`, ADR-0039)* | Gains one new read-only property, `Plugins`, projecting the Plugin Registry's own entries — the identical pattern already used for `Modules`/`HostedServices`. | Extended |
| **Module Discovery / Registration / Lifecycle** | ~~**Unchanged.** As `Plugin Manifest Architecture.md` insisted, and this document insists again: nothing here changes because plugins gained dependencies, a registry, or a lifecycle document of their own.~~ **Corrected, `WP 13.12.2`: "Unchanged" is false and has been since `WP 13.9.6`.** `ReflectionFrameworkDiscoveryService` gained an optional `Func<Type, bool>? isTypeExcluded` predicate (`WP 13.9.6`); `ModuleLifecycleManager` and `HostedServiceManager` each gained an optional `componentScopeProvider` hook (`WP 13.2A`, extended `WP 13.10B`); `TempestHost` gained trust-denial filters at Module Registration and Hosted Service Registration (`WP 13.9.4`). The accurate claim is **plugin-unaware**, not unchanged: every hook is a generic `Func<>`, and `Tempest.Core.Modules`/`Tempest.Core.BackgroundServices` hold no code reference to `Tempest.Core.Plugins`. The `WP 13.9.1` Status-header assertion that "the technical content throughout the rest of this document was independently confirmed still accurate" was itself invalidated by `WP 13.9.6` landing in the same commit (`d7d19d4`). | **Plugin-unaware, not unchanged** |

The load-bearing claim is identical to the one `Plugin Manifest
Architecture.md` made: if this design required changing Module Discovery,
Registration, or Lifecycle, it would have failed its own brief. It does
not.

## The Boundary With Trust & Isolation

Stated explicitly so the two documents compose without collision or
silent contradiction. This document assumes the sibling Trust & Isolation
Architecture will:

1. Define the **semantics** of `PluginManifest.RequestedCapabilities` (a
   plugin declares what it wants to do) — this document defines only that
   the field exists, is a list of opaque strings, and is read (not
   interpreted) at Plugin Discovery time.
2. Define the **semantics** of `PluginManifest.Publisher`/`Signature` —
   this document defines only that the fields exist, are optional
   strings, and are read (not verified) at Plugin Discovery time.
3. Decide the **isolation mechanism** (`AssemblyLoadContext` per plugin,
   or an alternative) — this document's Plugin Lifecycle (ADR-0108)
   explicitly reserves, but does not build, the state-machine seam
   (`Loaded → Unloading → Unloaded`) a per-plugin isolation boundary would
   make usable for the first time.
4. Decide **where and how enforcement happens** for the DI/registration
   surfaces this document names as a plugin's only mechanism for
   contributing capability to other modules (`IEventBus`,
   `INavigationProvider`, `ICommandRegistry` — ADR-0109) — this document
   defines the *mechanical* surface; whether a trust level gates a
   specific call through it is that document's decision, referenced, not
   designed, here (directly continuing `TD-09`/`TD-10`/`TD-11`'s own
   already-disclosed scope boundary).
5. Decide whether a **denied capability check** produces a new
   `PluginRegistryState` value (for example, a `TrustDenied` state
   distinct from this document's own `Failed`/`Incompatible`/
   `DependencyUnmet`/`Disabled`) or is expressed through the existing
   states — this document's `PluginRegistryState` enum (below) is
   designed to be **extended, not restructured**, by whatever the sibling
   document decides, mirroring how ADR-0025's own eleven categories were
   themselves extended, not restructured, by this document's own
   ADR-0107.

**If the sibling document's isolation boundary does not, in fact, use a
per-plugin `AssemblyLoadContext` or equivalent collectible mechanism**,
ADR-0108's own reserved `Unloading`/`Unloaded` seam simply stays reserved,
unused, until whatever mechanism is chosen either enables it or a further
ADR revisits the question — this document's lifecycle design does not
assume a specific outcome, only that *some* per-plugin boundary decision
will eventually be made by that document, not this one.

## Manifest v2 — Field Shape

`PluginManifest` v1's five required fields (`Id`, `Name`, `Version`,
`MinimumPlatformVersion`, `AssemblyFileName`) and its one
discovery-computed field (`AssemblyPath`) are **unchanged, still
required, still exactly as `Plugin Manifest Architecture.md` specified.**
Every field below is new, and every one is **optional** — a v1-shaped
manifest, with none of these fields present, remains a fully valid
manifest under this document's own extended parsing, defaulting every new
field to empty/absent. See Architectural Questions Evaluated for why no
explicit schema-version discriminator field accompanies this document,
despite calling it "v2."

| Field | Required? | Shape | Justification / Owner |
|---|---|---|---|
| `Dependencies` | Optional (default: empty list) | `IReadOnlyList<PluginDependency>` — each entry an `Id` (required), `MinimumVersion` (required), `MaximumVersion` (optional, `null` = unbounded above) | This document (ADR-0107). A real, demonstrated need — inter-plugin dependency — now exists per this release's own trigger. |
| `RequestedCapabilities` | Optional (default: empty list) | `IReadOnlyList<string>` — opaque identifiers, unvalidated and uninterpreted by this document or by Plugin Discovery | **Shape only — semantics defined by the Trust & Isolation Architecture.** Reserved so that document does not also need to propose a manifest-format change; Plugin Discovery reads the field into memory and does nothing else with it. |
| `Publisher` | Optional (default: `null`) | `string?` — free text, unverified | **Shape only — semantics (verification, display, trust weighting) defined by the Trust & Isolation Architecture.** Directly the field `Plugin Manifest Architecture.md` originally excluded ("no tooling, UI, or runtime logic consumes this today") — now reserved because the sibling document is that consumer. |
| `Signature` | Optional (default: `null`) | `string?` — an opaque, encoded blob (algorithm and encoding undecided here) | **Shape only — semantics (algorithm, verification, failure handling) defined by the Trust & Isolation Architecture.** Plugin Discovery reads the field's presence and raw value; it performs no cryptographic operation of any kind. |

```csharp
namespace Tempest.Core.Plugins;

public sealed class PluginManifest
{
    // v1 — unchanged
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string MinimumPlatformVersion { get; }
    public string AssemblyFileName { get; }
    public string AssemblyPath { get; }

    // v2 — new, all optional/additive
    public IReadOnlyList<PluginDependency> Dependencies { get; }      // never null
    public IReadOnlyList<string> RequestedCapabilities { get; }       // never null; reserved, see Trust & Isolation Architecture
    public string? Publisher { get; }                                  // reserved, see Trust & Isolation Architecture
    public string? Signature { get; }                                  // reserved, see Trust & Isolation Architecture
}

public sealed class PluginDependency
{
    public string Id { get; }
    public string MinimumVersion { get; }
    public string? MaximumVersion { get; }   // null = unbounded above
}
```

## Configurable Plugins Root and Manifest Convention (closes `FCR-0010`/`TD-06`)

`Plugin Manifest Architecture.md` fixed two conventions (`Plugins/`,
relative to `AppContext.BaseDirectory`; `plugin.manifest.json` per
candidate folder) while explicitly noting "a future work package may make
this configurable; nothing here forecloses it." That future work package
is this one.

| Configuration key | Required? | Default if absent |
|---|---|---|
| `Runtime:Plugins:RootDirectory` | Optional | `Plugins` (unchanged from `WP 4.2`) |
| `Runtime:Plugins:ManifestFileName` | Optional | `plugin.manifest.json` (unchanged from `WP 4.2`) |
| `Runtime:Plugins:Disabled` | Optional | Empty array — no plugin disabled by default |

**No Host Lifecycle ordering change is required.** ADR-0026 already
placed Configuration Built (Phase 2) before Plugin Discovery (Phase 3.1)
and already named this exact seam: "[Plugin Discovery] has no hard
dependency on Configuration for its core function... but *may* optionally
consult it in a future implementation (for example, to override the
plugins directory path, or disable plugin loading entirely)." This
document exercises exactly that already-anticipated seam; it does not
open a new one.

`Runtime:Plugins:Disabled` is checked immediately after a candidate's
manifest has been parsed far enough to know its own `Id` — after category
2's malformed-manifest check (ADR-0025), before any further validation —
so a disabled entry never needs to pass compatibility, dependency, or
assembly-existence checks at all. A disabled plugin is recorded in the
Plugin Registry as `PluginRegistryState.Disabled`; it is not a failure of
any kind, and is logged at Information severity, mirroring category 1's
own "not a failure" treatment for an absent plugins directory.

## Discovery Extension — Dependency Graph Resolution

Fully specified in **ADR-0107**; summarised here for this document's own
completeness. Dependency declarations are resolved entirely within the
existing Phase 3.1 (Plugin Discovery) — no new Host Lifecycle phase — via
a fixed-point reduction over the individually-valid candidate set,
followed by a topological sort (folder name as tie-break) that determines
Plugin Loading's own load order. A missing or version-incompatible
dependency, or membership in a dependency cycle, isolates exactly the
affected plugin(s) — never Host-fatal, never the whole batch — extending
ADR-0025's table with three new categories (12, 13, 14; see ADR-0107 for
the full table).

## Plugin Registry — A Host-Owned Catalogue With a Read-Only Projection

### Ownership

`IPluginRegistry`/`PluginRegistry` is a **Host-owned collaborator** —
constructed and held directly by `TempestHost`, populated during Plugin
Discovery/Loading (3.1/3.2), **never added to the `ServiceCollection`,
never resolvable by a module or plugin.** This is `ADR-0017`'s own
principle ("Discovery, Registration, and Lifecycle remain Host-owned
collaborators, not public DI services") applied to a fourth Host-owned
collaborator, for the identical reason: a module able to reach
`IPluginRegistry` directly could, in principle, be given write access to
it later by a careless future change, or could be mistaken for a
legitimate place to *drive* plugin loading rather than merely observe its
outcome. Keeping it structurally unreachable from any module removes that
risk at the API surface, not merely by convention.

```csharp
namespace Tempest.Core.Plugins;

public enum PluginRegistryState
{
    Loaded,
    Failed,
    Incompatible,
    DependencyUnmet,
    Disabled,
    TrustDenied,   // added — WP 13.0A integration; semantics owned by
                    // Plugin Trust & Isolation Architecture.md, ADR-0112
                    // categories 15–17 (signature/eligibility/conformance
                    // failure). This document's own boundary text, above,
                    // reserved this exact sixth value without naming it;
                    // the sibling document names it, this integration
                    // pass records it here so the enum is complete in its
                    // one authoritative source rather than only implied.
}

public sealed class PluginRegistryEntry
{
    public string Id { get; }              // the manifest's own Id if parseable, else the candidate folder name
    public string? Name { get; }
    public string? Version { get; }
    public PluginRegistryState State { get; }
    public string? Detail { get; }          // human-readable reason, mirroring ADR-0025's own logged detail
}

public interface IPluginRegistry   // Host-owned — never DI-public (ADR-0017)
{
    IReadOnlyCollection<PluginRegistryEntry> Entries { get; }
}
```

`PluginRegistryState` deliberately does not distinguish *which* of
ADR-0025's or ADR-0107's failure categories produced `Failed`/
`DependencyUnmet` — that finer detail lives in the log, and in `Detail`,
exactly as ADR-0025's own "What Is Explicitly Not Introduced" section
already declined to build a richer taxonomy than the running log already
provides. Five states were the floor this document's own brief named
(Loaded/Failed/Disabled/Incompatible/DependencyUnmet); a sixth,
`TrustDenied`, is added by the sibling Trust & Isolation Architecture
(`ADR-0112` categories 15–17) and recorded directly in this enum by this
work package's own integration pass, per "The Boundary With Trust &
Isolation," above — not invented speculatively, but adopted from that
document's own, already-cited decision. No seventh state exists or is
anticipated.

### The read-only projection — extending `IDiagnosticsProvider`, not inventing a new service

`IDiagnosticsProvider` (`WP 5.2`, ADR-0039) already exists for exactly
this purpose — "a read-only, DI-public projection over the Runtime Host's
own lifecycle state, letting a consumer observe module and hosted-service
health without gaining any authority over either." A plugin candidate is
the same *kind* of observable thing, one pipeline stage earlier. This
document extends it by one property, reusing the identical `Func<T>`
accessor pattern ADR-0039 already established for reading a
not-yet-constructed Host-owned collaborator safely:

```csharp
namespace Tempest.Core.Diagnostics;

public interface IDiagnosticsProvider
{
    HostState HostState { get; }
    IReadOnlyCollection<ModuleLifecycleStatus> Modules { get; }
    IReadOnlyCollection<HostedServiceStatus> HostedServices { get; }
    IReadOnlyCollection<PluginRegistryEntry> Plugins { get; }   // new — WP 13.0A
}
```

`DiagnosticsProvider`'s constructor gains one further `Func<IPluginRegistry?>`
accessor, closing over `TempestHost`'s own private field, exactly as its
two existing accessors already do for `IModuleLifecycleManager`/
`IHostedServiceManager` — reading `Plugins` before Plugin Discovery has
run returns an empty collection (an honest "not yet observed" state, per
ADR-0039's own established discipline), never an error. **No new Host
Lifecycle phase, no new `HostState`, no new transition** — the identical
guarantee ADR-0039 already gave for `Modules`/`HostedServices`, extended
to a third data source without needing to be re-argued.

This directly closes the gap ADR-0025's own Future Considerations named
and explicitly declined to design: "A future implementation... should
record, in some queryable form, which plugin candidates failed during a
given run and why... only requires that whatever `WP 4.2` builds remains
readable by a future diagnostics capability (`WP 4.8`) without this ADR
needing to be revisited." `IDiagnosticsProvider.Plugins` is that queryable
form, arriving later than `WP 4.8` but built exactly to the shape ADR-0025
anticipated, without that ADR needing revision.

**Why this does not consume one of this work package's three reserved ADR
numbers.** A genuine alternative was considered (a new, dedicated
`IPluginDiagnosticsProvider`, parallel to `IDiagnosticsProvider` — see
Alternative Designs Considered, RD-0051) — but the decision itself is a
direct, cited, mechanical application of two already-Accepted precedents
(`ADR-0017`'s Host-owned boundary; `ADR-0039`'s `Func<T>`-accessor
extension pattern) to one additional, structurally identical data source,
not a new tension between two live alternatives with no established
precedent to draw on. `Plugin Manifest Architecture.md` itself drew the
same line once already (the `Plugins/`/`plugin.manifest.json` conventions
were real decisions, explicitly not ADR'd, "a data-file-naming
convention... no genuine alternative was contested") — this document
draws it in the same place, for the same kind of reason, and records the
alternative in the Rejected Designs Log regardless, exactly as that
document's own convention requires when a real alternative existed even
though no ADR was warranted.

## Plugin Lifecycle — Load, Upgrade, Uninstall

Fully specified in **ADR-0108**; summarised here. Live, in-process plugin
unload remains a named, defended non-goal for `v0.13.0` — `Plugin Manifest
Architecture.md`'s own Risk, formalised into a decision record rather than
silently carried forward. Load is unchanged from `WP 4.2`/ADR-0026.
Upgrade and Uninstall are both file-system operations, performed while the
process is not running, taking effect on the *next* process start —
Plugin Discovery carries no memory across runs, a direct consequence of
ADR-0015. An operator wanting to stop a plugin before the next restart
uses `Runtime:Plugins:Disabled`, not a live-unload operation, which does
not exist. See ADR-0108 for the full state machine, the reserved
`Unloading`/`Unloaded` seam, and its own Alternatives Considered.

## Version Compatibility — Revisiting `RD-0009`

`RD-0009` (a maximum/"tested up to" platform version in the manifest) was
rejected for `v0.4.0` with an explicit revisit trigger: "once real plugins
and real version history exist to design a ceiling policy against — not
before." This document's own commissioning event — the Product Owner's
confirmed third-party plugin commitment (`FCR-0001`) — is a real trigger
for *building toward* third-party plugins; it is not, itself, the trigger
`RD-0009` named. **`RD-0009`'s rejection is reaffirmed, not reversed, by
this document.** `Plugin Register.md` remains empty at the time of this
decision — verified directly, not assumed (`src/Plugins/` contains zero
real plugin packages, `WP 4.5A`'s own last review, unchanged since). A
commitment to eventually ship third-party plugins is not the same fact as
"real plugins and real version history exist" — the latter requires at
least one real plugin to have shipped and undergone at least one real
version increment, so a ceiling *policy* (warn, block, or
allow-with-warning) can be designed against actual observed upgrade
behaviour rather than guessed a second time. The concrete, now-nameable
future trigger: **the first real plugin (first- or third-party) ships,
and is upgraded at least once** — see Proposed Edit to `Rejected
Designs.md`, below, for the exact addendum text recommended for `RD-0009`
itself.

**This document's own `PluginDependency.MaximumVersion` field (ADR-0107)
is not a re-litigation of `RD-0009`.** It bounds one plugin's declared
compatibility with *another plugin* — a real, present need this release's
own brief named directly (item 5, "version-range compatibility between
plugins") — not the platform's own version, which remains exactly as
unbounded-above as `Plugin Manifest Architecture.md` left it.

## Service Registration and DI Boundaries

Fully specified in **ADR-0109**; summarised here. No new DI container
capability is introduced. A plugin's `IModule` is constructor-injected
exactly as any discovered module's is (`ADR-0027`); a plugin makes its own
capability available to other modules through the same, already
DI-public, imperative surfaces every module already has — `IEventBus`,
`INavigationProvider`, `ICommandRegistry` — called during its own Module
Initialisation step, never through a plugin-specific registration API and
never through raw `IServiceCollection` access, which no module of any
kind has ever had.

## Future Commercial Plugin Support

**Not designed here beyond this section — a named future capability with
its own trigger, not a current decision.** `ILicenseValidator` (`WP 6.6`,
ADR-0050) already validates and constructs a fully-resolved `ILicense`
**before** Configuration Built, ahead of Plugin Discovery (3.1) in the
existing phase table — meaning a validated license is already, mechanically,
available earlier than Plugin Discovery runs, with no ordering tension of
the kind ADR-0026 had to resolve once already for
`IPlatformVersionProvider`. This is a structural fact worth recording now,
even though nothing consumes it yet: **should licensing-gated plugin
loading ever become a real, scheduled capability (`FCR-0025`), Plugin
Discovery could, in principle, receive the same already-validated
`ILicense` instance `TempestHost` already constructs, exactly as it
already receives `IPlatformVersionProvider`'s instance today — without
requiring the DI-registered `ILicenseProvider` wrapper, which is not
constructed until Phase 6.** This is disclosed as a structural
observation, not a decision: no capability-gated plugin loading is
designed, built, or scheduled by this document. A marketplace or
third-party distribution channel remains a named non-goal, unchanged from
`Plugin Manifest Architecture.md`'s own — revisit trigger: a real,
concrete third-party plugin author or distribution scenario, per
`FCR-0002`'s own still-unmet condition, not this document's own
architecture-only scope.

## Architectural Questions — Evaluated

| Question | Verdict | Why |
|---|---|---|
| Should `PluginManifest` v2 carry an explicit schema-version discriminator field? | Rejected | Every new field is optional and purely additive; a v1 manifest remains valid without modification. Mirrors `RD-0009`'s and the original document's own "purely additive, cheap to add later" reasoning — a discriminator earns its place only once a genuinely *breaking* manifest change is proposed, which this document does not propose. Revisit trigger: a future, non-additive manifest change. |
| Should dependency resolution get its own new Host Lifecycle phase? | Rejected | Pure computation over already-validated manifests, no side effect of its own — belongs inside the existing side-effect-free half of the Discovery/Loading split (ADR-0026, RD-0012), not a new phase. |
| Should a missing/incompatible plugin dependency be Host-fatal? | Rejected | Direct extension of ADR-0025's own governing principle; see ADR-0107, RD-0046. |
| Should the Plugin Registry be DI-public? | Rejected | Direct extension of ADR-0017's own governing principle to a fourth Host-owned collaborator; see Plugin Registry, above, RD-0052. |
| Should a new, dedicated `IPluginDiagnosticsProvider` service be built instead of extending `IDiagnosticsProvider`? | Rejected | `IDiagnosticsProvider` already exists for exactly this purpose (ADR-0039); extending it reuses an established, proven `Func<T>`-accessor pattern rather than proliferating near-identical read-only reporters. See RD-0051. |
| Should live, per-plugin unload be designed now? | Rejected for `v0.13.0` | Depends on an isolation mechanism decision this document does not own (the sibling Trust & Isolation Architecture's), and no real, demonstrated hot-upgrade need exists yet (`src/Plugins/` empty). See ADR-0108, RD-0049. |
| Should plugins get `IServiceCollection` access, or a restricted plugin-scoped registration API? | Rejected, both | Plugin code does not run until Module Initialisation (Phase 8), after the container is already built and frozen (`RD-0043`) — no point in the phase table exists where either would take effect without either running plugin code implausibly early or silently doing nothing. See ADR-0109. |
| Is the `RD-0009` platform-version-ceiling revisit trigger now met? | **No** | A commitment to build toward third-party plugins is not the same fact as "real plugins and real version history exist" — `src/Plugins/` remains empty. See Version Compatibility, above. |
| Should `PluginDependency.MaximumVersion` be considered a re-opening of `RD-0009`? | No | Different axis entirely — inter-plugin compatibility, not platform compatibility — a real, present need this release's own brief named directly. |

## Validation Strategy

Extends `Plugin Manifest Architecture.md`'s own table; every existing rule
(mandatory v1 fields, malformed JSON, unparseable `MinimumPlatformVersion`,
incompatible platform version) is unchanged.

| Rule | Detail |
|---|---|
| **New optional fields** | `Dependencies`, `RequestedCapabilities`, `Publisher`, `Signature` — absence is valid; presence requires each `PluginDependency.Id`/`MinimumVersion` to be non-null, non-empty, non-whitespace (mirroring every existing required-field check), `MaximumVersion` when present must parse and be `>= MinimumVersion`. |
| **Dependency resolution failures** | Missing dependency, incompatible dependency version, circular dependency — all isolated per ADR-0107, categories 12–14. Never hard manifest-validation failures (`InvalidPluginManifestException`) — a dependency problem is a relationship between otherwise-valid manifests, not a defect in any single one. |
| **Disabled-list matching** | `Runtime:Plugins:Disabled` is matched against a candidate's parsed `Id` only, after category-2 (malformed manifest) validation, before any further check — a disabled plugin's own `MinimumPlatformVersion`/`Dependencies` are never evaluated at all. |
| **When** | Still entirely at Plugin Discovery time (dependency resolution) or immediately after (disabled-list check) — no validation of any kind newly introduced at Plugin Loading time. |
| **Who owns it** | Plugin Discovery, extended — still Host-owned, still not Module Discovery/Registration's concern. |

## Non-Goals (Restated and Extended)

Everything `Plugin Manifest Architecture.md` already named as a non-goal
(package management, online repositories, downloads, remote loading,
cross-process communication) remains one. This document additionally
declines, explicitly:

- **Live, in-process plugin unload** — ADR-0108, cited, defended,
  reserved-but-not-built.
- **A version-ceiling policy against the platform's own version** —
  `RD-0009`, reaffirmed, not reversed.
- **Trust levels, permission/capability semantics, code-signing
  verification, the isolation mechanism, sandboxing** — the sibling Trust
  & Isolation Architecture's own scope, referenced, not designed, here.
- **A sixth `PluginRegistryState` for a trust/capability denial** — left
  for the sibling document to add, if its own design needs one; this
  document's enum is designed to be extended, not restructured.
- **A marketplace or third-party distribution channel** — unchanged
  non-goal, revisit trigger unchanged (`FCR-0002`).
- **Commercial licensing policy for plugin loading** — a structural
  observation only (Future Commercial Plugin Support, above), not a
  design; revisit trigger: `FCR-0025` becoming a real, scheduled need.
- **Soft/optional plugin dependencies** — RD-0048, no demonstrated need.
- **Automatic restart/backoff for a plugin that fails after loading** —
  RD-0050, mirrors RD-0029's identical reasoning for hosted services.
- **A restricted, plugin-scoped `IServiceCollection`-shaped registration
  API** — ADR-0109, rejected on ordering grounds, not merely on trust
  grounds.

## Alternative Designs Considered

Recorded in full in each ADR's own Alternatives Considered section;
indexed here for this document's own completeness, each cross-referencing
the Rejected Designs Log entry the orchestrator is asked to add (see this
work package's final report):

- Host-fatal circular plugin dependency — **RD-0046** (ADR-0107).
- A dedicated cascade-notification step for transitive dependency failure
  — **RD-0047** (ADR-0107).
- Soft/optional plugin dependencies — **RD-0048** (ADR-0107).
- Real per-plugin unload for `v0.13.0` — **RD-0049** (ADR-0108).
- Automatic restart/backoff for a failed-after-loading plugin —
  **RD-0050** (ADR-0108).
- A new, dedicated `IPluginDiagnosticsProvider` service — **RD-0051**
  (this document, Plugin Registry).
- Making `IPluginRegistry` itself DI-public — **RD-0052** (this document,
  Plugin Registry, mirroring ADR-0017 directly).
- A restricted, plugin-scoped registration API; full `IServiceCollection`
  access; extending the DI container with open-generic/keyed registration
  now — all three, ADR-0109's own Alternatives Considered.

## Risks

- **The sibling Trust & Isolation Architecture's own isolation-mechanism
  decision could, in principle, make ADR-0108's reserved `Unloading`/
  `Unloaded` seam unusable as shaped** (for example, if it chooses a
  mechanism that is not a collectible `AssemblyLoadContext` and offers no
  analogous unload capability at all). Mitigated by ADR-0108's own framing
  — the seam is reserved, not built, and its own trigger already names
  "the sibling document adopts a per-plugin isolation boundary that
  supports it" as a precondition, not an assumption.
- **`PluginManifest.RequestedCapabilities`/`Publisher`/`Signature` are
  shape-only, unenforced fields until the sibling document's semantics
  land.** A plugin author populating them today gets no actual
  enforcement — disclosed explicitly here, not silently implied to be
  "already secure" by virtue of the fields merely existing.
- **Dependency graph resolution's own runtime cost is unmeasured** —
  disclosed as an assumption (small, local candidate counts) in
  ADR-0107's own Consequences, not proven.
- **The "not yet observed, not a failure" empty-collection convention
  `ADR-0039` established for `Modules`/`HostedServices` is reused for
  `Plugins`, but the parallel is not exact, disclosed here rather than
  overclaimed.** `IDiagnosticsProvider` itself is not constructed until
  Platform Services Registered (Phase 6, `ADR-0039`), which always
  occurs *after* Plugin Discovery/Loading (Phases 3.1/3.2, `ADR-0026`'s
  frozen phase table) has already completed — unlike `Modules`, which has
  a genuine, reachable early-read window (a module's own constructor, at
  Phase 8, can observe `IDiagnosticsProvider` before Module
  Initialisation's own later modules have run). By the time any caller
  can reach `IDiagnosticsProvider.Plugins` at all, the Plugin Registry is
  therefore always already fully populated — the empty-collection case
  this convention guards against is consistent, defensive doc-comment
  discipline for a state this design does not believe is actually
  reachable, not a genuine, exercised ambiguity the way it is for
  `Modules`/`HostedServices`.

## ADRs Required

Three decisions this document's own brief named as needing the same
rigour `ADR-0025`/`ADR-0026` already brought to Plugin Manifest — **all
three are resolved directly by this work package**, using exactly the
reserved `ADR-0107`–`ADR-0109` range, no more:

1. **ADR-0107** — *Plugin Dependency Graph Resolution and Extended
   Failure Classification.* Ordering, cycle detection, and three new
   ADR-0025-extending failure categories.
2. **ADR-0108** — *Plugin Lifecycle Covers Load, Upgrade, and Uninstall —
   Live, In-Process Unload Remains a Named, Cited Non-Goal.* The
   lifecycle state machine, and the explicit, defended decision not to
   build live unload for `v0.13.0`.
3. **ADR-0109** — *A Plugin Registers Services Exactly Like Any Module —
   No New DI Container Capability, No `IServiceCollection` Access.* The
   mechanical service-registration boundary.

No fourth ADR was needed for the Plugin Registry's own DI-public
projection design (extending `IDiagnosticsProvider`) — see Architectural
Questions Evaluated, above, for why that decision, though real, is a
direct, cited application of two already-Accepted precedents rather than
a fresh tension between live alternatives.

## Recommendation

1. Ratify `ADR-0107`, `ADR-0108`, `ADR-0109` — all three drafted and
   ready for review alongside this document.
2. ~~A future implementation work package (`WP 13.0B`, already named in
   `docs/releases/v0.13.0/WorkPackages.md`'s own roadmap-predicted table)
   may implement~~ **Corrected, `WP 13.9.1`: `WP 13.0B` was in fact
   commissioned as an independent architecture review of this document,
   not its implementation — a disclosed divergence, see
   `docs/releases/v0.13.0/WorkPackages.md`'s own `WP 13.0B` row. The real
   implementation work this item anticipated —
   `PluginDependency`, the extended `PluginManifest`,
   `IPluginRegistry`/`PluginRegistry`, the extended `IDiagnosticsProvider`,
   and the corresponding `Host Lifecycle.md`/`Failure Behaviour.md`/
   `Rejected Designs.md` updates — was in fact performed by `WP 13.1A`,
   independently reviewed by `WP 13.1B`, and independently re-verified
   end to end by `WP 13.3A`/`WP 13.3B`.** Not performed by this document
   itself, which remains architecture only, exactly as `Plugin Manifest
   Architecture.md` itself was once architecture-only ahead of `WP 4.2`.
3. This document's own Manifest v2 field *shapes* for capability/trust
   metadata (`RequestedCapabilities`, `Publisher`, `Signature`) are ready
   to compose with the sibling Trust & Isolation Architecture the moment
   that document lands — no coordination beyond both documents citing
   each other by name is required, per "The Boundary With Trust &
   Isolation," above.

## Related Documents

`Plugin Manifest Architecture.md` (the `v0.4.0` baseline this document
extends); `ADR-0025` (*Plugin Failure Classification*); `ADR-0026`
(*Plugin Discovery Lifecycle Placement*); `ADR-0107`/`ADR-0108`/`ADR-0109`
(this document's own three new ADRs); `ADR-0015` (*Runtime Hosts Are Not
Restartable*); `ADR-0017` (*Discovery, Registration, and Lifecycle Remain
Host-Owned*); `ADR-0009` (*Composition Root Owns Externally-Created
Services*); `ADR-0039`/`Diagnostics Architecture.md`
(`IDiagnosticsProvider`'s own existing design, extended here); `ADR-0050`
(*License Validation*, whose already-early construction this document's
Future Commercial Plugin Support section observes); `Host Lifecycle.md`
(Phases 3.1/3.2, unchanged in number, extended in content — see this work
package's own final report for exact proposed text); `Rejected Designs.md`
(RD-0009, RD-0010, RD-0011, RD-0012, RD-0022, RD-0029, RD-0040, RD-0043,
and this document's own RD-0046–RD-0052); `docs/governance/Future
Capability Register.md` (`FCR-0001`, `FCR-0002`, `FCR-0010`, `FCR-0025`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-06`, `TD-09`,
`TD-10`, `TD-11`); `docs/security/Security Roadmap.md` (items 1, 2, 10);
`docs/governance/Engineering/Plugin Register.md`.

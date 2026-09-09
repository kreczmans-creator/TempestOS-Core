# TempestOS v0.4.0 — Platform Services Architecture Review

**WP 4.2D.** A formal engineering review of the entire Platform Services
milestone (`WP 4.0` → `WP 4.2`), performed before approving `WP 4.3` and
everything that follows it — the same gate a mature engineering programme
would run before committing to the next tranche of work. This is a review
and hardening exercise: no new functionality was added, no completed
architecture was redesigned, and no ADR was reopened. Every corrective
action taken here is a documentation fix; the codebase itself was not
touched.

## Executive Summary

The Platform Services milestone is architecturally sound. Every service
reviewed — Configuration, Logging, Platform Version, Plugin Discovery,
Plugin Loading, Module Discovery, Module Registration, Module Lifecycle,
and Dependency Injection — has a single, clear owner; a dependency graph
that points downward only, with no exceptions found; and a failure model
that is uniformly and correctly applied. The exception hierarchy, the
service-registration discipline, and the Host-owned-collaborator pattern
are consistent across every service that has ever been added to the
platform, including the two (Plugin Discovery, Plugin Loading) added most
recently. No architectural inconsistency, layering violation, duplicated
responsibility, or accidental abstraction was found anywhere in the
reviewed surface.

What this review did find is smaller and more mundane: a handful of
documentation cross-references that fell out of date as `WP 4.0` through
`WP 4.2` landed — a stale ADR count, two stale "nothing has been built yet"
status lines, a glossary entry describing plugins as not-yet-implemented
after they were implemented, and two structural gaps in the Platform
Service Map (a missing "Key types" field, a summary-table row). All nine
are fixed below. None required a design decision; every one was a
mechanical correction once found.

**Recommendation: proceed to `WP 4.3`.** Nothing found in this review
should delay it.

## Scope

Reviewed for architectural consistency, dependency direction, layering,
ownership, lifecycle correctness, naming consistency, service-registration
consistency, documentation consistency, ADR consistency, Rejected Design
consistency, Academy consistency, and cross-reference correctness:

- All 26 ADRs (`ADR-0001` through `ADR-0026`).
- `docs/releases/FOUNDATION.md`.
- `docs/architecture/`: Platform Service Map, Runtime Host Architecture,
  Host Lifecycle, Startup Sequence, Shutdown Sequence, Runtime State
  Machine, Failure Behaviour, Ownership Matrix, Engineering Glossary,
  Plugin Manifest Architecture, Platform Version, Rejected Designs.
- `docs/releases/v0.4.0/`: Architecture, CHANGELOG, WorkPackages, Risks,
  Testing, ReleasePlan, ReleaseChecklist.
- Every `WP 4.x` Academy retrospective (`WP4.0` through the `WP4.2`
  implementation retrospective, including the `WP4.2A`/`4.2B`/`4.2C`
  sub-work-packages).
- Production source for all nine services named in the brief:
  Configuration, Logging, Versioning (Platform Version), Plugin Discovery,
  Plugin Loading, Module Discovery, Module Registration, Module Lifecycle,
  Dependency Injection — read directly, not inferred from documentation.

## Architectural Strengths

Findings this review confirms as already correct — stated explicitly, per
the brief's own instruction not to manufacture improvements where none are
needed.

**Dependency direction is clean, with no exceptions found.** Tracing every
cross-namespace `using` statement in `Tempest.Core` confirms: `Versioning`
depends on nothing but `Logging` (optional, diagnostic-only); `Configuration`
depends on nothing but `Logging` (same); `DependencyInjection` depends on
nothing but `Logging`; `Logging` depends on `Configuration` (required, for
`MinimumLevel`) and `DependencyInjection` (its own extension-method
registration surface only); `Modules` depends on `Logging` and
`DependencyInjection`; `Plugins` depends on `Logging`, `Versioning`, and
`Modules`; `Runtime` depends on all of the above. Every one of these edges
points downward through ADR-0023's four-layer stack, exactly as
`FOUNDATION.md`'s ninth non-negotiable principle requires. `Plugins`'s only
touch of `Modules` is a documentation-only `<see cref="IModule.Version"/>`
cross-reference inside an XML comment — not a functional dependency — which
is exactly right: the architecture's own claim that "Module Discovery
remains completely unaware of plugins" holds in both directions, not only
the one the architecture documents emphasise.

**The exception hierarchy is fully consistent.** Every capability that can
fail defines exactly one non-sealed base type (`ConfigurationException`,
`ModuleDiscoveryException`, `ModuleRegistrationException`,
`ModuleLifecycleException`, `ServiceResolutionException`, `PluginException`,
`HostException`) and one or more sealed leaf subtypes beneath it — no
capability has two competing base types, and no leaf type skips the base to
inherit directly from `Exception`. `Tempest.Core.Plugins`, the newest
capability, follows this pattern exactly: `PluginException` base, five sealed
subtypes. `Logging` and `Versioning` deliberately define no exception type
at all — correctly, since both are designed to degrade rather than throw
(a sink failure is caught internally; missing version metadata falls back to
a default) — this is a designed absence, not a gap.

**Host-owned collaborators are constructed exactly once, in exactly one
place, and never registered into DI.** `ReflectionFrameworkDiscoveryService`,
`RuntimeModuleManager`, `ModuleLifecycleManager`,
`PluginManifestDiscoveryService`, and `PluginAssemblyLoader` are each
constructed directly by `TempestHost`, confirmed by grepping every
production call site — one match each. None appears in any
`ServiceCollection` registration. This is ADR-0017's rule, and Plugin
Discovery/Loading (added by `WP 4.2`, after ADR-0017 was written) extend it
correctly rather than quietly becoming the exception — a module still has
no path back into the machinery orchestrating it, including the newest two
phases of that machinery.

**Service-registration discipline is consistent.** Every genuinely
DI-public service (`IConfigurationProvider`, `ILogSink`, `ILoggerFactory`,
`ILogger`, `IPlatformVersionProvider`) is registered via `AddInstance` at
the composition root, per ADR-0009 — confirmed directly in
`TempestHost.ExecuteStartupPhasesAsync`. Nothing is registered by any other
mechanism, and no service that should remain Host-owned (Discovery,
Registration, Lifecycle, Plugin Discovery, Plugin Loading) leaks into the
container by accident.

**The internal test-seam pattern is applied uniformly.** Every service that
has an environment-dependent default (`ReflectionFrameworkDiscoveryService`'s
`AppDomain` scan, `PlatformVersionProvider`'s executing-assembly lookup,
`PluginManifestDiscoveryService`'s plugins-root path, `TempestHostBuilder`'s
both of the above) exposes a public constructor for production use and a
second, `internal` constructor accepting an explicit override, visible only
to `Tempest.Core.Tests` via `InternalsVisibleTo`. `WP 4.2` follows this
exact, pre-existing shape rather than inventing a new one.

**Naming precision, not inconsistency, where it might look otherwise.**
`IFrameworkDiscoveryService.DiscoverModules()` and
`IPluginManifestDiscoveryService.DiscoverManifests()` are named after
different nouns — "modules" versus "manifests" — which could look like
careless inconsistency at a glance. It is not: a `PluginManifest` describes
a *candidate* that has not yet been proven loadable, while a
`ModuleDescriptor` describes something already loaded and reflectable.
Naming the plugin method "DiscoverPlugins" would overclaim exactly what
"the Manifest describes; the Runtime decides" exists to prevent.

**The Rejected Designs Log and ADR series are both internally consistent.**
All 14 Rejected Design entries are correctly indexed, never renumbered, and
every "Superseded" or "Retired" marker (none currently marked Superseded)
would be honoured if one existed. Every ADR that revises or extends an
earlier one says so explicitly in its own Status section (ADR-0004's WP 2.7
and WP 2.7B updates, ADR-0009's WP 2.6 update, ADR-0014's note pointing to
ADR-0018, ADR-0019 resolving the ADR-0004/`Shutdown Sequence.md` tension) —
no ADR was found to silently contradict another.

**`FOUNDATION.md`'s nine non-negotiable principles hold, unviolated, across
`WP 4.0`–`WP 4.2`.** Checked individually: layering (①, ⑨) — confirmed above;
single ownership (②) — Plugin Discovery/Loading own exactly their own
phase, nothing else; state ownership (③) — `PluginManifest` is immutable,
matching `RuntimeModule`/`ModuleDescriptor`'s own shape; the platform/module
failure boundary (④) — extended, not blurred, by ADR-0025's plugin
classification; permissive disposal (⑤) — untouched, since `WP 4.2`
introduces no new disposable resource; atomic interruption boundaries (⑥) —
Plugin Discovery/Loading are each a single phase, not a batch, so nothing
new was required; ADR discipline (⑦) — 7 new ADRs (`ADR-0020`–`ADR-0026`)
in this release alone, every one recorded before its implementing code;
authority tiers (⑧) — unaffected, no process change proposed.

## Per-Service Review

| Service | Verdict |
|---|---|
| **Configuration** | Correct, unchanged since `WP 2.5`. One documentation gap found and fixed (see Corrective Actions). |
| **Logging** | Correct, unchanged since `WP 2.7B`'s sink-isolation fix. Pre-existing debt (single-sink, dual-mechanism) is already named and owned by `WP 4.8` — not re-litigated here. |
| **Platform Version (Versioning)** | Correct. The one service in the platform with a genuinely zero-dependency constructor — the clearest example of ADR-0023's layering in practice. `WP 4.2` moved its *construction* earlier without touching its DI *registration*, exactly as ADR-0026 specifies; verified directly in `TempestHost.cs`. |
| **Plugin Discovery** | Correct. `PluginManifestDiscoveryService` isolates every plugin-scoped failure via a single `catch (PluginException)`, never a blanket `catch (Exception)` — confirmed by reading the implementation, not merely the documentation. Deterministic ordering (sort by folder name) is real, not aspirational. |
| **Plugin Loading** | Correct. `PluginAssemblyLoader` follows the identical isolation shape as Plugin Discovery. Requires, and receives, zero cooperation from Module Discovery. |
| **Module Discovery** | Correct, unchanged since `WP 2.1`, and — this review confirms directly — genuinely unchanged by `WP 4.2`: no line of `ReflectionFrameworkDiscoveryService.cs` differs from before Plugin Loading existed. |
| **Module Registration** | Correct, unchanged since `WP 2.2`. No plugin-awareness of any kind, as designed. |
| **Module Lifecycle** | Correct, unchanged since `WP 2.3`. |
| **Dependency Injection** | Correct, unchanged since `WP 2.4`. `AddInstance`'s reuse for `IPlatformVersionProvider` (`WP 4.2A`) is its third application (after Configuration, Logging), exactly matching ADR-0009's own anticipated pattern — not a new mechanism. |

## Review Findings and Corrective Actions Taken

Nine findings, all documentation drift accumulated across `WP 4.0`–`WP 4.2C`
as work landed faster than every cross-reference could be re-checked. Every
one is fixed in this same commit; none required a design decision.

1. **`FOUNDATION.md`'s ADR count was stale.** "Nineteen exist at the time of
   this writing" was accurate when written but is a brittle claim in a
   document explicitly meant to outlive any single work package — 26 ADRs
   exist as of this review. *Fixed*: rephrased to record both the original
   count and the current one, without committing the sentence to needing a
   further edit at ADR 27.
2. **`ReleasePlan.md`'s status line was stale.** "Planning. No
   implementation has begun." — written before `WP 4.0` started, never
   revisited. *Fixed*: updated to "In progress," pointing to `WorkPackages.md`/
   `CHANGELOG.md` as the living record, consistent with how this document
   already describes itself relative to those two.
3. **`WorkPackages.md`'s closing line was stale.** "None of these work
   packages has begun implementation," directly under the document's own
   `WP 4.0`–`WP 4.2` entries, each of which is separately marked complete
   two sections later. *Fixed*: replaced with a dated update note.
4. **`Engineering Glossary.md`'s "Plugin" entry was stale**, marked
   `*(planned)*` and reading "Not yet implemented" after `WP 4.2` implemented
   exactly that. *Fixed*: corrected, and cross-referenced to a new **Plugin
   Manifest** entry.
5. **A "Plugin Manifest" glossary entry did not exist at all.** This is not
   merely an omission this review noticed — `ReleaseChecklist.md`'s own
   Release-Level Checklist explicitly names "Plugin Manifest" as a term the
   Glossary must gain, and it had not. *Fixed*: added, at the same density
   as the existing "Event Bus"/"Hosted Service" entries, covering both
   Plugin Discovery and Plugin Loading.
6. **`Engineering Glossary.md`'s own "Related Documents" ADR range was
   stale** ("ADR-0001 through ADR-0018", despite the glossary's own entries
   already citing `ADR-0020`–`ADR-0025` by name). *Fixed*: extended to
   `ADR-0026`.
7. **`Platform Service Map.md`'s Plugin Manifest entry was missing a "Key
   types" field** — present on every one of the document's other twelve
   entries. *Fixed*: added, listing `PluginManifest`, `PluginException` and
   its five subtypes, and both Discovery/Loading interface-plus-
   implementation pairs.
8. **`Platform Service Map.md`'s "At a Glance" summary table omitted Module
   SDK**, which has its own full prose section further down the same
   document. A table whose own document's Purpose states it exists "so a
   reader can answer 'what is X' in one place" should not require already
   knowing to scroll past the table to find something. *Fixed*: added a row,
   explicitly annotated as "not Host-orchestrated" so its inclusion doesn't
   imply it became a Host-driven service.
9. **`Platform Service Map.md`'s Configuration entry silently omitted its
   own optional `ILogger?` constructor parameter**, while Discovery,
   Registration, Lifecycle, and Platform Version all explicitly document the
   identical convention. *Fixed*: added, with an honest, specific note that
   — unlike the other four — `TempestHost`'s own real call site never
   actually has a logger to pass at that point in startup, so the parameter
   exists on the type without ever being exercised by the Host's own
   production code path.

Also corrected in passing, found while verifying finding 9's context:
`Runtime Host Architecture.md`'s Overview still called itself "the 'Host
(planned)' entry in the Platform Service Map," unchanged since before
`WP 2.7B` implemented the Host. *Fixed*: reworded to past/present tense
correctly.

## Remaining Technical Debt

Everything below was already known, already named, and already has an
owning work package or an explicit revisit trigger — repeated here as a
consolidated list, not as new findings, and none of it blocks `WP 4.3`.

- **Two logging mechanisms coexist** (`ILogger` vs. legacy `LoggingService`)
  — debt since `WP 2.6`, owned by `WP 4.8`.
- **Single-sink logging** — debt since `WP 2.6`, owned by `WP 4.8`.
- **No disposal tracking** for `AddInstance`-registered or
  reflection-constructed singletons implementing `IDisposable` — debt since
  `WP 2.4`/ADR-0009, no owning work package named yet; not urgent, since no
  current platform service is disposable.
- **`IHostedService` naming proximity** to
  `Microsoft.Extensions.Hosting.IHostedService` — open per ADR-0024,
  explicit revisit trigger is real usage evidence once `WP 4.5` lands.
- **The parameterless-constructor-only constraint** on discovered modules
  (found during `WP 4.1`) — documented, not fixed; fixing it would be a
  Discovery-level architectural decision, correctly out of scope for every
  work package so far.
- **`src/Plugins/` remains empty** — by design; building a first real
  plugin is `WP 4.3`'s scope, not a gap in `WP 4.2`.
- **Plugins root directory (`Plugins/`) and manifest file name
  (`plugin.manifest.json`) are fixed conventions**, not configurable —
  explicitly deferred in the `WP 4.2` retrospective as a purely additive
  future enhancement, not a current limitation with a known cost.
- **Navigation's `Tempest.Core` placement** — the one genuinely open
  architectural question left in the whole release, owned by `WP 4.6A`.
- **Background Services (`WP 4.5`) will need to extend `Host Lifecycle.md`'s
  phase table a second time** — no longer a design risk (ADR-0026 is now a
  worked precedent to follow), but still future work, tracked as `Risks.md`
  R1.

## Recommendations Before WP 4.3

1. **Proceed.** No finding in this review rises to the level of blocking
   `WP 4.3`, and every corrective action taken was documentation-only —
   nothing about the reviewed code changed.
2. **`WP 4.5`, when it arrives, should explicitly follow ADR-0026's decimal
   sub-numbering precedent** for its own `Host Lifecycle.md` extension,
   rather than re-deriving how to insert a phase from scratch — already
   noted in `Risks.md` R1, restated here as this review's own agreement with
   that plan.
3. **Treat structural completeness of the Platform Service Map and
   Engineering Glossary as a checked item in each future work package's own
   Definition of Done** — not a new rule, but this review's own findings
   (7, 8, 9 above) show that a missing field or a missing entry can survive
   an entire work package's own review cycle unnoticed. `ReleaseChecklist.md`
   already requires both documents be updated; the gap was in verifying the
   update was structurally complete, not merely present.
4. **No new ADR is required as a result of this review.** Every finding was
   documentation drift, not an architectural inconsistency in the code
   itself — there was nothing here to decide, only something to correct.

## Sign-Off

The Platform Services milestone (`WP 4.0` → `WP 4.2`) is formally reviewed
and signed off. Configuration, Logging, Platform Version, Plugin Discovery,
Plugin Loading, Module Discovery, Module Registration, Module Lifecycle, and
Dependency Injection are each architecturally sound, correctly layered,
consistently named, and correctly documented as of this review. `WP 4.3`
may proceed.

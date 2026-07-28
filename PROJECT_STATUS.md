# TempestOS — Project Status

**Last Updated:** 2026-07-28 (WP 5.3 — Developer Experience Improvements)

This is the primary status dashboard for TempestOS. Read this first for
"where does the project stand right now" — for "why is it built this
way," read `docs/releases/FOUNDATION.md`; for "how do I get productive,"
read `docs/academy/Contributor Learning Path.md`.

---

## Current Repository Phase

**Developer Experience — complete.** The Foundation phase is complete and
closed — Platform Formation, Academy Formation, and Governance Formation
are all done (see `docs/releases/Platform Foundation Completion Report.md`),
and `v0.4.0` ("Platform Foundation") shipped exactly that scope. TempestOS
then built *on* the foundation — Navigation, a Command Framework,
Diagnostics, and Developer Experience tooling itself — through the
**Developer Experience** phase, whose every named Work Package is now
complete. `WP 5.0A` through `WP 5.0D` were this phase's first four
completed Work Packages — Navigation and the Shell are both fully
implemented, and `Tempest.App` now runs the real platform for the first
time in this project's history. `WP 5.0S` (Platform Security Baseline
Audit) followed as a dedicated, formal engineering audit — not a feature
Work Package — establishing the v0.5.0 Security Baseline every future
Work Package's Definition of Done is checked against. `WP 5.1A`
(Command Framework Architecture) and `WP 5.1B` (Command Framework
Implementation) followed — `ICommand`'s own contract (`WP 4.0`) now has
a real handler contract and dispatcher, proven against a real sample
module and the real Runtime Host. `WP 5.2` (Diagnostics Improvements)
followed — a composite `ILogSink` closes a long-named debt (`TD-02`), and
a new `IDiagnosticsProvider` gives any DI-resolving consumer a read-only
view of the Host's own current lifecycle state, without granting write
access to the Host-owned machinery that state comes from. `WP 5.3`
(Developer Experience Improvements) closes the phase out — a `dotnet new`
module template, and a previously-only-documented Discovery pitfall now
closed with a clear, actionable error message. See Current Priorities,
below, for what this means for the release itself.

## Current Development Branch

**`feature/v0.5.0-developer-experience`**, cut from `main` after the
`v0.4.0` tag. Carries `WP 5.0A` through `WP 5.0D`, plus `WP 5.0S`,
`WP 5.1A`, `WP 5.1B`, `WP 5.2`, and `WP 5.3`. Unmerged into `main`; every
Work Package in `docs/releases/v0.5.0/WorkPackages.md` is now complete,
but the merge/tag sequence for `v0.5.0` itself is a separate, explicit
Product Approval decision (Engineering Governance §7), not assumed from
Work Package completion alone — see Current Priorities, below.

## Current Release

**v0.4.0** ("Platform Foundation") — released 2026-07-27, still the most
recent tag. Root `VERSION` reads `0.4.0`; `v0.5.0` is in progress but not
yet cut. `v0.3.0` ("Runtime Foundation Complete") is the release before
that.

## Current Work Package

**`WP 5.3` — Developer Experience Improvements — complete** (this Work
Package). Unlike every implementation Work Package before it this
release, `WP 5.3` has no preceding architecture phase — confirmed
directly before any code was written (no design document or ADR was ever
expected for a scoped tooling/polish pass; see this Work Package's own
retrospective, Section 3). Delivers: `dotnet new tempest-module`
(`src/Templates/Tempest.Templates.Module/`), generating a module shaped
exactly as `Building a Module.md` describes, installed locally rather
than as a NuGet package (`RD-0045`); and a clearer
`ReflectionFrameworkDiscoveryService` failure — a module with no
`[ModuleMetadata]` and no public parameterless constructor now raises a
`ModuleDiscoveryException` naming the actual fix, instead of a raw
`MissingMethodException`, closing a gap `Building a Module.md` has
documented in prose since `WP 4.1` but the code itself never enforced.
The template was verified twice: once manually, with the real `dotnet
new` CLI (installed, generated, built with 0 warnings/errors, then
uninstalled and removed, leaving no trace), and once by an automated
test that substitutes, builds, and proves the result discoverable by the
real, unmodified `ReflectionFrameworkDiscoveryService` on every future
test run. A repository review found and corrected three genuine,
pre-existing governance/documentation drifts, none caused by this Work
Package's own changes — see Documentation Status and Governance Status,
below. 10 new tests (552 total). See this Work Package's own
retrospective:
`docs/academy/03 Work Packages/WP5.3-developer-experience-improvements.md`.

## Next Planned Work Package

**None named.** Every Work Package in `docs/releases/v0.5.0/
WorkPackages.md` (`WP 5.0A` through `WP 5.3`) is now complete — the
Developer Experience phase itself is done. Whether and when to cut the
`v0.5.0` release (merge to `main`, tag, release notes) is a Product
Approval decision (Engineering Governance §7), not one this Work Package
makes for itself — see Current Priorities, below.

## Foundation Status

**Complete.**

| Milestone | Status |
|---|---|
| Platform Formation (Runtime Host, six platform services, Plugins, Event Bus, Background Services) | Complete |
| Academy Formation (63 articles across 7 categories, formal maintenance obligation) | Complete |
| Governance Formation (27 registers, full traceability, zero outstanding debt) | Complete |

See `docs/releases/Platform Foundation Completion Report.md` for the full
closeout narrative and `docs/releases/v0.4.0/Release Notes.md` for the
release this milestone shipped as.

## Platform Summary

TempestOS is a modular runtime platform. The Runtime Host
(`TempestHost`/`TempestHostBuilder`) assembles and orchestrates: Discovery,
Registration, Lifecycle, Dependency Injection, Configuration, and Logging
(the original Runtime Foundation, v0.3.0); Plugin Manifest discovery and
loading; the Event Bus (publish/subscribe between modules); and
Background Services (Host-orchestrated hosted work with isolated/critical
failure classification); and Navigation (a DI-public registry of
navigable destinations, notified via the Event Bus). Five real modules
(`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`,
`SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`)
exercise the complete pipeline end to end. `Tempest.App` now runs the
real platform for the first time in this project's history: its entry
point builds a `TempestHostBuilder`, constructs the Shell
(`TempestShell`, `Tempest.App.Shell`), and runs it — the Shell resolves
`INavigationProvider`/`IEventBus` through `ITempestHost.Services`
(`ADR-0033`–`ADR-0035`, implemented `WP 5.0D`) and presents a real,
interactive Navigation/Content region. The bootstrap-era
`BootstrapService`/`HostingService`/`ProjectService` code remains in the
repository, untouched and unmigrated, simply no longer referenced by
`Program.cs`. The Command Framework (`ICommand`, `v0.4.0`; `ICommandDispatcher`/
`ICommandRegistry`, `ADR-0036`–`ADR-0038`, `WP 5.1A`/`WP 5.1B`) is now
fully implemented — a type-keyed dispatcher and an Id-keyed registry,
both DI-public, mirroring the Event Bus and Navigation — proven by a
real reference module (`CommandSampleModule`) that also realises
ADR-0022's own Navigation-integration illustration for the first time.
Wiring the Shell's own input handling (keyboard shortcuts, a menu) to
the Command Framework is deferred to a later Work Package. Diagnostics
(`IDiagnosticsProvider`/`DiagnosticsProvider`, `Tempest.Core.Diagnostics`)
is now implemented — a read-only projection over the Host's own current
lifecycle state, constructed directly by `TempestHost` and registered via
`AddInstance` with `Func<T>` accessors (`ADR-0039`), proven by
`DiagnosticsSampleModule` and its `GetDiagnosticsSummaryCommand`. Logging
also gained `CompositeLogSink`, closing `TD-02`. A `dotnet new
tempest-module` template (`src/Templates/`) now lets a new contributor
scaffold a correctly-shaped module without hand-copying an existing one,
and Discovery's own long-documented parameterless-constructor pitfall now
fails with a clear, actionable message instead of a raw runtime
exception. Every Work Package originally scoped for the Developer
Experience phase is now complete.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 552 (0 failures) — 10 new (`WP 5.3`: `Modules/` and `Templates/` test suites) |
| ADRs | 39 (`ADR-0001`–`ADR-0039`), all Accepted — unchanged by `WP 5.3` (no new ADR met §5's criteria) |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) |
| Academy articles | 76 (see `docs/governance/Documentation/Academy Register.md`) |
| Governance registers | 27 (32 governance documents total), plus 4 standing security documents under `docs/security/` (not governance registers themselves, indexed from `Governance Index.md`'s Security section) |
| Architecture documents | 20 under `docs/architecture/` (22 including the two release-scoped documents) |
| Platform services | 17 catalogued — 14 Implemented (unchanged by `WP 5.3` — a template and a Discovery message, not a new platform service), 2 not implemented as platform services, 1 developer-convenience layer |
| Modules (production) | 7 (`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`, `CommandSampleModule`, `DiagnosticsSampleModule`) |
| Hosted services (production) | 0 — infrastructure fully implemented and tested; zero shipped consumers by deliberate scope decision |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Commits (total / since `v0.4.0` tag) | 61 total (56 Claude-authored) / 9 since `v0.4.0` (`WP 5.0A`, `WP 5.0B`, `WP 5.0C`, `WP 5.0D`, `WP 5.0S`, `WP 5.1A`, `WP 5.1B`, `WP 5.2`, `WP 5.3`) |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build src/TempestOS.slnx`).
- **Tests:** 552/552 passing, verified stable across multiple consecutive
  full-suite runs at every major Work Package boundary, including a
  manual, direct execution of the real `dotnet new` CLI confirming the
  module template installs, generates, and builds cleanly (`WP 5.3`).
- **Known regressions:** None.
- **Working tree:** Clean at every Work Package boundary — see
  `docs/governance/Quality/Validation Register.md`.

## Documentation Status

Mature and cross-referenced. Every architecture document, ADR, and
Academy article is indexed in `docs/governance/` and cross-checked
against its own source. `WP 5.0A` added `Navigation Framework
Architecture.md`, `ADR-0031`/`ADR-0032`, and a new Academy concept guide;
`WP 5.0B` updated each of them in place to reflect implementation. `WP
5.0C` added `Shell & Composition Framework Architecture.md` and
`ADR-0033`–`ADR-0035`; `WP 5.0D` updated each of them in place to reflect
implementation, following the identical documentation shape Navigation's
own implementation phase established. Two small, pre-existing drifts were
also found and fixed along the way, unrelated to this Work Package's own
changes: `Documentation Register.md` had gone stale since `WP 4.5B`, and
`Technical Debt Register.md`'s own TD-07 still described Navigation's
`Tempest.Core` placement as an open question, under its old `WP 4.6A`
number, three Work Packages after `ADR-0031` had already resolved it.
`docs/releases/v0.5.0/ReleasePlan.md` and `WorkPackages.md` carry the
renumbered Developer Experience scope (`WP 4.6A`–`WP 4.9` → `WP 5.0A`–
`WP 5.3`) forward, plus the new `WP 5.0C`/`WP 5.0D` pair inserted without
renumbering anything else (`D-016`); the old `v0.4.0` entries were
annotated with redirect notes rather than deleted, per this project's
"never delete, mark superseded" convention. `WP 5.0S` added a new
top-level documentation tree, `docs/security/` (four documents: `Threat
Model.md`, `Security Principles.md`, `Platform Security Review
v0.5.0.md`, `Security Roadmap.md`), indexed from `Governance Index.md`'s
new Security section and `Documentation Register.md`'s Directory Map —
the first new top-level `docs/` tree since `docs/governance/` itself
(`WP 4.5A`). `WP 5.1A` added `Command Framework Architecture.md` and
`ADR-0036`–`ADR-0038`; the existing "Command" entries in `Platform
Service Map.md` and `Engineering Glossary.md` were updated in place,
following the identical documentation shape Navigation's and the
Shell's own design phases established. A genuine, pre-existing drift was
found and corrected along the way, unrelated to `WP 5.1A`'s own design
work: `Ownership Matrix.md` had never received a row for Navigation, at
either `WP 5.0A` or `WP 5.0B` — added then, alongside `WP 5.1A`'s own new
Command Framework row. `WP 5.1B` updated every "Command Framework" status
line from "architected" to "implemented" (`Platform Service Map.md`,
`Ownership Matrix.md`, `Engineering Glossary.md`, `Architecture Document
Register.md`) and added an Implementation Note and Security Review
Update to `Command Framework Architecture.md` itself. Further
pre-existing drift was found and corrected during `WP 5.1B`'s own
repository review: `docs/releases/v0.5.0/WorkPackages.md` had never
gained an entry for `WP 5.0S`, and several Engineering registers
(`Platform Services Register.md`, `Interface Register.md`, `Namespace
Register.md`, `Dependency Injection Register.md`, `Exception Register.md`,
`Module Register.md`) and Delivery registers (`Feature Register.md`,
`Traceability Matrix.md`) had gone stale since `WP 5.0D` — all corrected
in the same commit as the change that prompted noticing them. `WP 5.2`
added `Diagnostics Architecture.md` and `ADR-0039`; `Platform Service
Map.md`, `Ownership Matrix.md`, and `Engineering Glossary.md` each gained
a new Diagnostics entry, following the identical documentation shape
Navigation's and the Command Framework's own design phases established.
A genuine, pre-existing drift was found and corrected along the way,
unrelated to `WP 5.2`'s own scope: `Architecture Document Register.md`
still read `Command Framework Architecture.md` as "implementation
pending... not yet started," two Work Packages after `WP 5.1B` had
actually completed it — corrected here. `WP 5.3` updated `Building a
Module.md` and `Sample Module Architecture.md` to reference the new
scaffolding template, and amended `Engineering Governance.md` §11 to
document `src/Templates/`. Three further, genuine, pre-existing drifts
were found and corrected during this Work Package's own repository
review, none related to its own scope: `Rejected Designs Register.md`
had added `RD-0042`–`RD-0044` (`WP 5.2`) without the corresponding full
entries ever being written into `docs/architecture/Rejected Designs.md`
itself; `Engineering Governance.md` §11 had not been updated when
`WP 5.2` added the `Tempest.Core.Diagnostics` namespace; and
`Governance Register.md`'s own Compliance Matrix had not been updated
since `WP 5.0D`, missing four completed Work Packages entirely (see
Governance Status, below, for the full account).

## Academy Status

76 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md`. Every completed Work Package has a matching
retrospective, including `WP 5.0A` through `WP 5.3`. Maintenance
obligation (Engineering Governance §6) verified honoured by two
independent audits (`WP 4.4F`, and the Academy Register built during
`WP 4.5A`); `WP 5.0A` updated two existing articles' (`06-platform-
layering.md`, `08-failure-isolation.md`) "Future Evolution" sections,
`WP 5.0B` confirmed those predictions against the real implementation
with no correction needed, `WP 5.0D` added a genuine, non-obvious
implementation finding (`const` fields not forcing assembly load) to the
Shell's own concept guide, `WP 5.0S` added a new "Security" category
teaching threat modelling, secure plugin architecture, trust boundaries,
and least privilege from first principles, `WP 5.1A` added a new
"Command Framework" category (a new concept guide,
`11-command-framework.md`) and updated `08-failure-isolation.md` with a
genuinely new, fifth failure-isolation case (Case 5 — Command Dispatch:
propagate, don't isolate) that document's own "Future Evolution" section
had explicitly anticipated testing, `WP 5.1B` added the matching
implementation retrospective and confirmed the concept guide's own
design against the real, working implementation, with one genuine,
non-obvious implementation finding (`CommandHandlerTable`) added to both,
`WP 5.2` added a new "Diagnostics" category (a new concept guide,
`12-diagnostics-and-composite-logging.md`) plus its own retrospective,
teaching the `Func<T>` lazy-accessor pattern and the two-named-debts
decision from first principles, and `WP 5.3` updated `Building a
Module.md` in place (no new concept guide — this Work Package extends
already-covered material, not a new platform capability) plus its own
retrospective, teaching why a scoped tooling Work Package legitimately
has no preceding architecture phase.

## Governance Status

27 registers (32 governance documents total, including the Index,
Philosophy, Audit Report, Maturity Report, and Future Work Package
Guidelines), fully cross-referenced, re-verified during `v0.4.0` Release
Engineering and every Work Package since. Traceability for both
Navigation and the Shell is complete end to end — no Pending cells remain
(`docs/governance/Delivery/Traceability Matrix.md`). One stale, pre-
existing entry unrelated to `WP 5.0D`'s own scope was found and corrected
along the way: `Technical Debt Register.md`'s TD-07. `WP 5.0S` added two
new, disclosed debt items following its own security audit — `TD-09`
(plugin trust boundary) and `TD-10` (Navigation ownership gap) — both
Open, both requiring a future Architecture Work Package, neither a
regression of anything previously Resolved. `Decision Register.md`
gained `D-017` (conducting `WP 5.0S` as a dedicated security audit Work
Package). `WP 5.1A` added `TD-11` (command/navigation registration-order
squatting, `CMD-1`) and widened `TD-09`'s own scope to name the Command
Framework as a second affected surface; `Decision Register.md` gained
`D-018` (splitting `WP 5.1` into `WP 5.1A`/`WP 5.1B`); `Risks.md`'s R3 is
now Retired (the Event Bus/Command Framework cross-reference it required
now exists). `WP 5.1B` confirmed `TD-09`/`TD-11` present in the real
implementation exactly as designed (neither worsened, neither newly
introduced) and disclosed no new debt. Several Engineering and Delivery
registers (`Platform Services Register.md`, `Interface Register.md`,
`Namespace Register.md`, `Dependency Injection Register.md`, `Exception
Register.md`, `Module Register.md`, `Feature Register.md`, `Traceability
Matrix.md`) had drifted stale since `WP 5.0D` without any Work Package
since having touched them — found and corrected during `WP 5.1B`'s own
mandatory repository review. `WP 5.2` resolved `TD-02` (`CompositeLogSink`)
and reassessed `TD-01` (re-scoped forward again, not migrated —
`D-020`); `Decision Register.md` also gained `D-019` (the Event
Framework/Diagnostics premise redirect). ADR Register, Rejected Designs
Register, and every Engineering/Delivery register touched by this Work
Package's own new types were updated in the same commit; `Architecture
Document Register.md`'s stale Command Framework marker (see Documentation
Status, above) was corrected during this Work Package's own repository
review, unrelated to its own scope. `WP 5.3` added `RD-0045` (local-folder
vs. NuGet-packaged template distribution) and touched no Technical Debt
item (its own scope does not concern `TD-01`–`TD-11`). This Work
Package's own repository review found three further, genuine,
pre-existing drifts: (1) `RD-0042`–`RD-0044` had been added to
`Rejected Designs Register.md` during `WP 5.2` but never actually written
into `docs/architecture/Rejected Designs.md` itself — the register's own
declared Source of Truth — backfilled here, unchanged in content; (2)
`Engineering Governance.md` §11 had not been updated when `WP 5.2` added
the `Tempest.Core.Diagnostics` namespace; (3) most significantly,
`Governance Register.md`'s own Compliance Matrix had not been updated
since `WP 5.0D` — four completed Work Packages (`WP 5.0S`, `WP 5.1A`,
`WP 5.1B`, `WP 5.2`) were missing entirely, and `WP 5.0D`'s own row still
carried a `*(this commit)*` placeholder never backfilled with its real
hash. All five rows backfilled, verified directly against `git log`. This
is the third Work Package in a row to find a real, previously-unnoticed
governance drift during its own repository review — see this Work
Package's own retrospective, Observations, for the pattern this now
represents.

## Known Unknowns

Recorded honestly, not guessed at — full detail in `docs/governance/
Governance Audit Report.md`:

1. `docs/releases/v0.2.0/` — an empty directory; whether v0.2.0 was ever
   released, skipped, or reserved is unknown.
2. `docs/roadmap/`, `docs/diagrams/` — both empty, both unreferenced by
   any document reviewed; intended purpose unknown.
3. Exact original authorship of four pre-Claude namespaces
   (`Tempest.Core.Hosting`, `Bootstrap`, `Projects`, `Repositories`) and
   seven unnamespaced bootstrap-era files.
4. A five-day gap in earliest git history (2026-07-15 to 2026-07-21).
5. v0.1.0's full scope beyond its own commit message.
6. Intermediate historical test-count totals for `WP 4.1` and `WP 4.3`
   (each retrospective states only the tests it added, not a running
   total).

## Current Priorities

1. **Every Work Package in `docs/releases/v0.5.0/WorkPackages.md` is now
   complete.** The next decision is Product Approval's, not an
   engineering one: whether and when to cut the `v0.5.0` release
   (Engineering Governance §7) — merge `feature/v0.5.0-developer-
   experience` into `main`, tag `v0.5.0`, and write its release notes —
   or whether to open a new Work Package first. Neither is assumed here;
   both require explicit, per-occasion approval.
2. No merge to `main` has occurred yet.

## Near-Term Roadmap

Per `docs/releases/v0.5.0/WorkPackages.md`, the Developer Experience
phase is now complete, `WP 5.0A` through `WP 5.3`:

- `WP 5.0A` — Navigation Framework Architecture (design only). **Complete.**
- `WP 5.0B` — Navigation Framework Implementation. **Complete.**
- `WP 5.0C` — Shell & Composition Framework Architecture (design only). **Complete.**
- `WP 5.0D` — Shell & Composition Framework Implementation. **Complete.**
- `WP 5.0S` — Platform Security Baseline Audit (dedicated engineering
  audit, not a feature Work Package). **Complete.**
- `WP 5.1A` — Command Framework Architecture (design only). **Complete.**
- `WP 5.1B` — Command Framework Implementation. **Complete.**
- `WP 5.2` — Diagnostics Improvements (composite logging, `TD-01`
  reassessment, `IDiagnosticsProvider`). **Complete.**
- `WP 5.3` — Developer Experience Improvements (module template,
  clearer Discovery message). **Complete.**

No further Work Package is named in the current release plan. A future
release's own scope (Project Engine, Requirements Engine, or anything
else) is a Product Approval decision, not one this document anticipates.

## Long-Term Vision

TempestOS aims to be an extensible platform other people build on, not
merely a runtime that hosts a fixed set of built-in capabilities — see
`docs/releases/v0.4.0/ReleasePlan.md`'s own "From Runtime to Platform"
theme. Two named, not-yet-designed platform services (Project Engine,
Requirements Engine) remain aspirational, each requiring its own
classification under ADR-0013 before design begins. The governing
constraint on all of it is `docs/releases/FOUNDATION.md`: every future
capability is a module or platform service running inside the one Runtime
Host this foundation established, never a second, parallel execution
model — and every future Work Package is expected to build capability
against that stable foundation rather than revisit it, absent evidence
that requires otherwise (see `docs/governance/Future Work Package
Guidelines.md`).

---

## Maintaining This Document

Update this file as part of the Definition of Done for any Work Package
that changes: the current branch, release, or Work Package; Foundation
status; a Repository Metrics figure; Repository Health; or a Known
Unknown being resolved. Keep it short — this is a dashboard, not a
narrative; link to the fuller document (Governance suite, Academy,
`WorkPackages.md`) rather than inlining detail that belongs there.

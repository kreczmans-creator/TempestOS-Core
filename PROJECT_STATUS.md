# TempestOS — Project Status

**Last Updated:** 2026-07-28 (WP 5.1A — Command Framework Architecture)

This is the primary status dashboard for TempestOS. Read this first for
"where does the project stand right now" — for "why is it built this
way," read `docs/releases/FOUNDATION.md`; for "how do I get productive,"
read `docs/academy/Contributor Learning Path.md`.

---

## Current Repository Phase

**Developer Experience.** The Foundation phase is complete and closed —
Platform Formation, Academy Formation, and Governance Formation are all
done (see `docs/releases/Platform Foundation Completion Report.md`), and
`v0.4.0` ("Platform Foundation") shipped exactly that scope. TempestOS is
now inside the **Developer Experience** phase: building *on* the
foundation — Navigation, a Command Framework, Diagnostics, and Developer
Experience tooling itself — not revisiting it, absent evidence that
requires otherwise (see `docs/governance/Future Work Package
Guidelines.md`). `WP 5.0A` through `WP 5.0D` are this phase's first four
completed Work Packages — Navigation and the Shell are both fully
implemented, and `Tempest.App` now runs the real platform for the first
time in this project's history. `WP 5.0S` (Platform Security Baseline
Audit) followed as a dedicated, formal engineering audit — not a feature
Work Package — establishing the v0.5.0 Security Baseline every future
Work Package's Definition of Done is now checked against. `WP 5.1A`
(Command Framework Architecture) is the phase's next completed Work
Package — `ICommand`'s own contract (`WP 4.0`) finally has a handler
contract and a dispatcher design; implementation (`WP 5.1B`) has not yet
begun.

## Current Development Branch

**`feature/v0.5.0-developer-experience`**, cut from `main` after the
`v0.4.0` tag. Carries `WP 5.0A` through `WP 5.0D`, plus `WP 5.0S` and
`WP 5.1A`. Unmerged into `main`;
the merge/tag sequence for `v0.5.0` itself is not yet due, since the
Developer Experience phase has only just begun (see `docs/releases/
v0.5.0/WorkPackages.md`).

## Current Release

**v0.4.0** ("Platform Foundation") — released 2026-07-27, still the most
recent tag. Root `VERSION` reads `0.4.0`; `v0.5.0` is in progress but not
yet cut. `v0.3.0` ("Runtime Foundation Complete") is the release before
that.

## Current Work Package

**`WP 5.1A` — Command Framework Architecture — complete** (this Work
Package). Architecture only — no production code changed, no tests
added. Designs `ICommandDispatcher`/`ICommandRegistry`/`CommandDescriptor`/
`CommandResult`: a type-keyed dispatcher for callers with a concrete
command instance, and an Id-keyed registry for callers with only a
string (menus, toolbars, keyboard shortcuts, future automation/AI
invocation) — both DI-public, both registered imperatively, mirroring
the Event Bus and Navigation exactly (`ADR-0036`–`ADR-0038`). A
mandatory security review against the `WP 5.0S` baseline surfaced one
new, genuine finding — `CMD-1`/`TD-11`, "registration-order squatting,"
which turns out to affect the already-implemented Navigation Framework
too, not only the new Command Framework — disclosed, not fixed
(architectural; deferred to a future Work Package). Risk R3 (`Risks.md`)
is retired: the Event Bus/Command Framework cross-reference it required
now exists. See this Work Package's own retrospective:
`docs/academy/03 Work Packages/WP5.1A-command-framework-architecture.md`.

## Next Planned Work Package

`WP 5.1B` — Command Framework Implementation (see `docs/releases/v0.5.0/
WorkPackages.md`). Implements exactly what `WP 5.1A` designed.

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
`Program.cs`. The Command Framework's contract (`ICommand`, `v0.4.0`) now
has a complete design (`ICommandDispatcher`/`ICommandRegistry`,
`ADR-0036`–`ADR-0038`, `WP 5.1A`) — a type-keyed dispatcher and an
Id-keyed registry, both DI-public, mirroring the Event Bus and
Navigation; implementation (`WP 5.1B`) is the only remaining unbuilt
piece of the Developer Experience phase's original scope besides
Diagnostics and DevEx tooling.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 448 (0 failures) — unchanged by `WP 5.1A` (architecture only, no tests added) |
| ADRs | 38 (`ADR-0001`–`ADR-0038`), all Accepted — adds `ADR-0036`–`ADR-0038` (`WP 5.1A`) |
| Rejected Designs | 41 (`RD-0001`–`RD-0041`) — adds `RD-0038`–`RD-0041` (`WP 5.1A`) |
| Academy articles | 72 (see `docs/governance/Documentation/Academy Register.md`) |
| Governance registers | 27 (32 governance documents total), plus 4 standing security documents under `docs/security/` (not governance registers themselves, indexed from `Governance Index.md`'s Security section) |
| Architecture documents | 19 under `docs/architecture/` (21 including the two release-scoped documents) — adds `Command Framework Architecture.md` (`WP 5.1A`) |
| Platform services | 16 catalogued — 12 Implemented, 1 **Architected** (Command Framework, `WP 5.1A`; implementation pending `WP 5.1B`, moved from "contract-only"), 2 not implemented as platform services, 1 developer-convenience layer |
| Modules (production) | 5 (`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`) |
| Hosted services (production) | 0 — infrastructure fully implemented and tested; zero shipped consumers by deliberate scope decision |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Commits (total / since `v0.4.0` tag) | 58 total (53 Claude-authored) / 6 since `v0.4.0` (`WP 5.0A`, `WP 5.0B`, `WP 5.0C`, `WP 5.0D`, `WP 5.0S`, `WP 5.1A`) |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build src/TempestOS.slnx`).
- **Tests:** 448/448 passing, verified stable across multiple consecutive
  full-suite runs at every major Work Package boundary, including
  `WP 5.1A` (architecture only — re-run to confirm no regression, none
  expected or found).
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
found and corrected along the way, unrelated to this Work Package's own
design work: `Ownership Matrix.md` had never received a row for
Navigation, at either `WP 5.0A` or `WP 5.0B` — added now, alongside this
Work Package's own new Command Framework row.

## Academy Status

72 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md`. Every completed Work Package has a matching
retrospective, including `WP 5.0A` through `WP 5.1A`. Maintenance
obligation (Engineering Governance §6) verified honoured by two
independent audits (`WP 4.4F`, and the Academy Register built during
`WP 4.5A`); `WP 5.0A` updated two existing articles' (`06-platform-
layering.md`, `08-failure-isolation.md`) "Future Evolution" sections,
`WP 5.0B` confirmed those predictions against the real implementation
with no correction needed, `WP 5.0D` added a genuine, non-obvious
implementation finding (`const` fields not forcing assembly load) to the
Shell's own concept guide, `WP 5.0S` added a new "Security" category
teaching threat modelling, secure plugin architecture, trust boundaries,
and least privilege from first principles, and `WP 5.1A` added a new
"Command Framework" category (a new concept guide,
`11-command-framework.md`) and updated `08-failure-isolation.md` with a
genuinely new, fifth failure-isolation case (Case 5 — Command Dispatch:
propagate, don't isolate) that document's own "Future Evolution" section
had explicitly anticipated testing.

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
now exists).

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

1. Begin `WP 5.1B` (Command Framework Implementation) on
   `feature/v0.5.0-developer-experience`, implementing exactly what
   `WP 5.1A` designed — `ICommandDispatcher`/`ICommandRegistry`, both
   DI-public, both registered imperatively — and now able to wire into
   the real Shell's own input handling once built.
2. No merge to `main` is due yet — `v0.5.0` is not cut until the
   Developer Experience phase's Work Packages are complete (see
   `docs/releases/v0.5.0/WorkPackages.md`).

## Near-Term Roadmap

Per `docs/releases/v0.5.0/WorkPackages.md`, the Developer Experience
phase, in sequence — `WP 5.0A` through `WP 5.1A` are complete so far:

- `WP 5.0A` — Navigation Framework Architecture (design only). **Complete.**
- `WP 5.0B` — Navigation Framework Implementation. **Complete.**
- `WP 5.0C` — Shell & Composition Framework Architecture (design only). **Complete.**
- `WP 5.0D` — Shell & Composition Framework Implementation. **Complete.**
- `WP 5.0S` — Platform Security Baseline Audit (dedicated engineering
  audit, not a feature Work Package). **Complete.**
- `WP 5.1A` — Command Framework Architecture (design only). **Complete.**
- `WP 5.1B` — Command Framework Implementation (dispatcher). Next planned.
- `WP 5.2` — Diagnostics Improvements (composite logging, health/status
  reporting).
- `WP 5.3` — Developer Experience Improvements (templates, scaffolding).

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

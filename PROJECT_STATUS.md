# TempestOS — Project Status

**Last Updated:** 2026-07-27 (WP 5.0A — Navigation Framework Architecture)

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
Guidelines.md`). `WP 5.0A` (Navigation Framework Architecture) is this
phase's first completed Work Package.

## Current Development Branch

**`feature/v0.5.0-developer-experience`**, cut from `main` after the
`v0.4.0` tag. Carries `WP 5.0A` (architecture only — no production code).
Unmerged into `main`; the merge/tag sequence for `v0.5.0` itself is not
yet due, since the Developer Experience phase has only just begun (see
`docs/releases/v0.5.0/WorkPackages.md`).

## Current Release

**v0.4.0** ("Platform Foundation") — released 2026-07-27, still the most
recent tag. Root `VERSION` reads `0.4.0`; `v0.5.0` is in progress but not
yet cut. `v0.3.0` ("Runtime Foundation Complete") is the release before
that.

## Current Work Package

**`WP 5.0A` — Navigation Framework Architecture — complete** (this Work
Package; architecture only, no production code). Produced
`ADR-0031`, `ADR-0032`, `docs/architecture/Navigation Framework
Architecture.md`, `RD-0030`–`RD-0033`, and the corresponding Academy and
governance updates. See this Work Package's own retrospective:
`docs/academy/03 Work Packages/WP5.0A-navigation-framework-architecture.md`.

## Next Planned Work Package

`WP 5.0B` — Navigation Framework Implementation (see
`docs/releases/v0.5.0/WorkPackages.md`). Builds `Tempest.Core.Navigation`
(`NavigationItem`, `INavigationProvider`, `NavigationRequestedEvent`, and
its exception types) exactly as designed in `WP 5.0A`, with no open
architectural questions remaining.

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
failure classification). Two real modules (`ClockModule`,
`ClockLifecycleObserverModule`) exercise the complete pipeline end to end.
Navigation and a Command Framework dispatcher remain unbuilt; Navigation
is now fully *designed* (`WP 5.0A`: `ADR-0031`, `ADR-0032`, `Tempest.Core.
Navigation`'s public surface), and the Command Framework's contract
(`ICommand`) still exists from `v0.4.0` with its dispatcher pending
`WP 5.1` — both belong to the Developer Experience phase now underway.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 355 (0 failures) — unchanged since `v0.4.0`; `WP 5.0A` is architecture-only |
| ADRs | 32 (`ADR-0001`–`ADR-0032`), all Accepted |
| Rejected Designs | 33 (`RD-0001`–`RD-0033`) |
| Academy articles | 65 (see `docs/governance/Documentation/Academy Register.md`) |
| Governance registers | 27 (32 governance documents total) |
| Architecture documents | 17 under `docs/architecture/` (19 including the two release-scoped documents) |
| Platform services | 16 catalogued — 11 Implemented, 1 Designed (Navigation), 1 contract-only, 2 not implemented as platform services, 1 developer-convenience layer |
| Modules (production) | 2 (`ClockModule`, `ClockLifecycleObserverModule`) |
| Hosted services (production) | 0 — infrastructure fully implemented and tested; zero shipped consumers by deliberate scope decision |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Commits (total / since `v0.4.0` tag) | 52 total (47 Claude-authored) / 0 since `v0.4.0` (this Work Package not yet committed) |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build src/TempestOS.slnx`).
- **Tests:** 355/355 passing, verified stable across multiple consecutive
  full-suite runs at every major Work Package boundary.
- **Known regressions:** None.
- **Working tree:** Clean at every Work Package boundary — see
  `docs/governance/Quality/Validation Register.md`.

## Documentation Status

Mature and cross-referenced. Every architecture document, ADR, and
Academy article is indexed in `docs/governance/` and cross-checked
against its own source. `WP 5.0A` added `Navigation Framework
Architecture.md`, `ADR-0031`/`ADR-0032`, and a new Academy concept guide,
each cross-referenced into the governance suite in the same commit that
introduced it — no documentation drift was introduced. `docs/releases/
v0.5.0/ReleasePlan.md` and `WorkPackages.md` were created to carry the
renumbered Developer Experience scope (`WP 4.6A`–`WP 4.9` → `WP 5.0A`–
`WP 5.3`) forward; the old `v0.4.0` entries were annotated with redirect
notes rather than deleted, per this project's "never delete, mark
superseded" convention.

## Academy Status

65 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md`. Every completed Work Package has a matching
retrospective, including `WP 5.0A`. Maintenance obligation (Engineering
Governance §6) verified honoured by two independent audits (`WP 4.4F`,
and the Academy Register built during `WP 4.5A`); `WP 5.0A` additionally
updated two existing articles' (`06-platform-layering.md`,
`08-failure-isolation.md`) "Future Evolution" sections to reflect
Navigation's actual classification rather than leaving a stale
prediction in place.

## Governance Status

27 registers (32 governance documents total, including the Index,
Philosophy, Audit Report, Maturity Report, and Future Work Package
Guidelines), fully cross-referenced, zero outstanding governance debt as
of the `WP 4.5A` baseline (see `docs/governance/Governance Audit
Report.md`), re-verified during `v0.4.0` Release Engineering and again
during `WP 5.0A` (every register touched by Navigation's design was
updated in the same commit, including the four registers whose own
"Source of Truth" is direct source inspection — those add an explicit
"Note — Navigation (Designed, Not Yet Implemented)" section rather than a
row, to avoid misrepresenting their own stated scope). Traceability
reflects Navigation's chain as begun but Pending (`WP 5.0B`) for
Implementation/Tests — a disclosed partial, not silence
(`docs/governance/Delivery/Traceability Matrix.md`).

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

1. Begin `WP 5.0B` (Navigation Framework Implementation) on
   `feature/v0.5.0-developer-experience`, building `Tempest.Core.
   Navigation` exactly to `WP 5.0A`'s design — no architectural
   questions remain open.
2. No merge to `main` is due yet — `v0.5.0` is not cut until the
   Developer Experience phase's Work Packages are complete (see
   `docs/releases/v0.5.0/WorkPackages.md`).

## Near-Term Roadmap

Per `docs/releases/v0.5.0/WorkPackages.md`, the Developer Experience
phase, in sequence — `WP 5.0A` (below) is the only one complete so far:

- `WP 5.0A` — Navigation Framework Architecture (design only). **Complete.**
- `WP 5.0B` — Navigation Framework Implementation. Next planned.
- `WP 5.1` — Command Framework (dispatcher).
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

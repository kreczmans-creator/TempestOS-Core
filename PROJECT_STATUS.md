# TempestOS — Project Status

**Last Updated:** 2026-07-27 (v0.4.0 Release Engineering — "Platform Foundation")

This is the primary status dashboard for TempestOS. Read this first for
"where does the project stand right now" — for "why is it built this
way," read `docs/releases/FOUNDATION.md`; for "how do I get productive,"
read `docs/academy/Contributor Learning Path.md`.

---

## Current Repository Phase

**Developer Experience.** The Foundation phase is complete and closed —
Platform Formation, Academy Formation, and Governance Formation are all
done (see `docs/releases/Platform Foundation Completion Report.md`), and
`v0.4.0` ("Platform Foundation") ships exactly that scope. TempestOS now
enters the **Developer Experience** phase: building *on* the foundation —
Navigation, a Command Framework, Diagnostics, and Developer Experience
tooling itself — not revisiting it, absent evidence that requires
otherwise (see `docs/governance/Future Work Package Guidelines.md`).

## Current Development Branch

**`main` (post-release).** `v0.4.0` merges `feature/v0.4.0-platform-
services` into `main` via an explicit `--no-ff` merge, tagged `v0.4.0`
(Engineering Governance §7). The next feature branch is cut from `main`
for whatever Work Package begins the Developer Experience phase,
matching the same discipline used for every prior release (§1.3). See
`docs/releases/v0.4.0/ReleaseChecklist.md`'s "Merge and Tag Sequence" for
the exact, prepared command sequence this release used.

## Current Release

**v0.4.0** ("Platform Foundation") — released 2026-07-27. Root `VERSION`
reads `0.4.0`. `v0.3.0` ("Runtime Foundation Complete") is the prior
tagged release.

## Current Work Package

None in progress — `v0.4.0` Release Engineering is complete. The branch
is ready for merge into `main` (see "Current Development Branch," above,
and `docs/releases/v0.4.0/ReleaseChecklist.md`).

## Next Planned Work Package

`WP 4.6A` — Navigation Architecture (architecture-only phase; see
`docs/releases/v0.4.0/WorkPackages.md`). Rescoped out of `v0.4.0` itself
— see `docs/releases/v0.4.0/ReleasePlan.md`'s "Scope" section — it is the
first Work Package of the Developer Experience phase, not part of the
release just shipped.

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
Navigation and a Command Framework dispatcher remain unbuilt; their
contracts exist, their implementations do not — both are rescoped out of
`v0.4.0` and belong to the Developer Experience phase now beginning.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 355 (0 failures) |
| ADRs | 30 (`ADR-0001`–`ADR-0030`), all Accepted |
| Rejected Designs | 29 (`RD-0001`–`RD-0029`) |
| Academy articles | 63 (see `docs/governance/Documentation/Academy Register.md`) |
| Governance registers | 27 |
| Architecture documents | 16 under `docs/architecture/` (18 including the two release-scoped documents) |
| Platform services | 15 catalogued — 11 Implemented, 1 contract-only, 2 not implemented as platform services, 1 developer-convenience layer |
| Modules (production) | 2 (`ClockModule`, `ClockLifecycleObserverModule`) |
| Hosted services (production) | 0 — infrastructure fully implemented and tested; zero shipped consumers by deliberate scope decision |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Commits (total / this release) | 50 total (45 Claude-authored) / 23 since `v0.3.0` |
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
against its own source. Release Engineering for `v0.4.0` performed a
full Pre-Release Review across architecture, documentation, Academy,
governance, traceability, release documentation, repository standards,
engineering standards, root documentation, cross references, and
repository structure — no new documentation drift was found beyond what
`WP 4.5B` had already corrected. `ReleasePlan.md` and `WorkPackages.md`
were updated to reflect this release's actual, rescoped scope (Platform
Foundation only), and every "planned"/"not yet begun" reference to
`WP 4.0`–`WP 4.5B` was removed now that those work packages are shipped.

## Academy Status

63 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md`. Every completed Work Package has a matching
retrospective. Maintenance obligation (Engineering Governance §6)
verified honoured by two independent audits (`WP 4.4F`, and the Academy
Register built during `WP 4.5A`), re-checked during `v0.4.0` Release
Engineering with no new drift found.

## Governance Status

27 registers (32 governance documents total, including the Index,
Philosophy, Audit Report, Maturity Report, and Future Work Package
Guidelines), fully cross-referenced, zero outstanding governance debt as
of the `WP 4.5A` baseline (see `docs/governance/Governance Audit
Report.md`), re-verified during `v0.4.0` Release Engineering. Traceability
is complete for all 13 Implemented capabilities
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

1. Merge `feature/v0.4.0-platform-services` into `main` and push the
   `v0.4.0` tag, following the prepared command sequence in
   `docs/releases/v0.4.0/ReleaseChecklist.md` — pending explicit,
   separate approval before any push, per that checklist's own gate.
2. Begin `WP 4.6A` (Navigation Architecture) on a new feature branch cut
   from `main`, once the above is complete.

## Near-Term Roadmap

Per `docs/releases/v0.4.0/WorkPackages.md`, the Developer Experience
phase, in sequence — none of this is part of `v0.4.0`, which has already
shipped:

- `WP 4.6A` — Navigation Architecture (design only).
- `WP 4.6B` — Navigation Implementation.
- `WP 4.7` — Command Framework (dispatcher).
- `WP 4.8` — Diagnostics Improvements (composite logging, health/status
  reporting).
- `WP 4.9` — Developer Experience Improvements (templates, scaffolding).

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

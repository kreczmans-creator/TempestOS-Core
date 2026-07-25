# TempestOS — Project Status

**Last Updated:** 2026-07-25 (WP 4.5B — Platform Foundation Closeout)

This is the primary status dashboard for TempestOS. Read this first for
"where does the project stand right now" — for "why is it built this
way," read `docs/releases/FOUNDATION.md`; for "how do I get productive,"
read `docs/academy/Contributor Learning Path.md`.

---

## Current Repository Phase

**Foundation phase — closing.** Platform Formation, Academy Formation, and
Governance Formation are all complete (see `docs/releases/Platform
Foundation Completion Report.md`). This Work Package (`WP 4.5B`) is the
formal closeout. From `WP 4.6A` onward, TempestOS enters a **Capability
Expansion** phase: building *on* the foundation (Navigation, Command
Framework, Diagnostics, Developer Experience), not revisiting it, absent
evidence that requires otherwise.

## Current Development Branch

`feature/v0.4.0-platform-services` — cut from `main` at the `v0.3.0` tag.
No work occurs directly on `main` (Engineering Governance §1.3).

## Current Release

**v0.3.0** is the most recent tagged release ("Runtime Foundation
Complete") — the root `VERSION` file still reads `0.3.0`. **v0.4.0**
("Platform Services") is in progress, unreleased, on the branch above.

## Current Work Package

`WP 4.5B` — Platform Foundation Closeout (documentation and governance
only; formally closes the Foundation phase).

## Next Planned Work Package

`WP 4.6A` — Navigation Architecture (architecture-only phase; see
`docs/releases/v0.4.0/WorkPackages.md`).

## Foundation Status

| Milestone | Status |
|---|---|
| Platform Formation (Runtime Host, six platform services, Plugins, Event Bus, Background Services) | Complete |
| Academy Formation (61 articles across 7 categories, formal maintenance obligation) | Complete |
| Governance Formation (27 registers, full traceability, zero outstanding debt) | Complete |

See `docs/releases/Platform Foundation Completion Report.md` for the full
closeout narrative.

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
contracts exist, their implementations do not.

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

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` — update both together.)*

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
against its own source. Two genuinely stale status lines were found and
corrected during this Work Package (`WorkPackages.md`'s and
`ReleasePlan.md`'s own top-level status notes, both still describing
`WP 4.3`/`WP 4.5` as "not begun") — see `docs/releases/Platform
Foundation Completion Report.md` for the full account. No other
documentation drift is currently known.

## Academy Status

63 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and (new this Work
Package) `Contributor Learning Path.md`. Every completed Work Package has
a matching retrospective. Maintenance obligation (Engineering Governance
§6) verified honoured by two independent audits (`WP 4.4F`, and the
Academy Register built during `WP 4.5A`).

## Governance Status

27 registers, fully cross-referenced, zero outstanding governance debt as
of the `WP 4.5A` baseline (see `docs/governance/Governance Audit
Report.md`). Traceability is complete for all 13 Implemented capabilities
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

1. Complete `WP 4.5B` (this Work Package) — close the Foundation phase
   formally, with a coherent, contributor-facing repository identity.
2. Begin `WP 4.6A` (Navigation Architecture) once this Work Package is
   merged and reviewed.

## Near-Term Roadmap

Per `docs/releases/v0.4.0/WorkPackages.md`, in sequence:

- `WP 4.6A` — Navigation Architecture (design only).
- `WP 4.6B` — Navigation Implementation.
- `WP 4.7` — Command Framework (dispatcher).
- `WP 4.8` — Diagnostics Improvements (composite logging, health/status
  reporting).
- `WP 4.9` — Developer Experience Improvements (templates, scaffolding).
- v0.4.0 release tag, once all of the above meet their own Acceptance
  Criteria (`docs/releases/v0.4.0/ReleaseChecklist.md`).

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

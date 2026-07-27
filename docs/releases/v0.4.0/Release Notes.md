# TempestOS v0.4.0 — "Platform Foundation"

**Release date:** 2026-07-27
**Tag:** `v0.4.0`
**Branch:** `feature/v0.4.0-platform-services` → `main`

---

## Overview

TempestOS v0.4.0 closes the **Platform Foundation** phase: everything the
Runtime Foundation (v0.3.0) needed to become an actual, extensible
platform — a module SDK, plugin loading, cross-module communication, and
Host-orchestrated background work — plus the Academy and Governance
disciplines that keep all of it explainable and checkable, not just
working.

This release is a **deliberate rescoping**. The original v0.4.0 plan also
included Navigation, a Command Framework dispatcher, Diagnostics
improvements, and Developer Experience tooling. Those four work packages
have not started, and are formally deferred to the next milestone rather
than held back as an unfinished commitment — see "What's Next," below.
Everything reported here has actually shipped, been tested, and been
documented; nothing is aspirational.

## Highlights

- **A real Runtime Host, now genuinely extensible.** Plugin Manifest
  discovery/loading, an Event Bus, and Host-orchestrated Background
  Services all slot into `TempestHost`'s existing lifecycle without a
  single change to its state machine, failure model, or disposal
  contract.
- **A living reference module set.** `ClockModule` and
  `ClockLifecycleObserverModule` exercise the complete pipeline —
  discovery, registration, lifecycle, dependency injection, and
  publish/subscribe — as real, non-synthetic proof, not a hypothetical
  example.
- **A fourth-time-proven discovery pattern.** The same four disciplines
  that made Module Discovery safe (filter before instantiating, impose
  deterministic order, isolate per-candidate failures, expose an internal
  test seam) were reused, unmodified, for Plugin Discovery and Hosted
  Service Discovery alike.
- **The first complete Governance baseline.** 27 registers, full
  traceability from requirement to release for every shipped capability,
  and zero outstanding governance debt.
- **355 tests, up from 164 at v0.3.0** — zero regressions at any Work
  Package boundary across the entire release.

## Major Features

| Feature | What It Does |
|---|---|
| **Module SDK** | `ModuleBase`/`ModuleLifecycleBase` reduce module boilerplate to identity plus the lifecycle hooks a module actually overrides. |
| **Plugin Manifest** | `PluginManifestDiscoveryService`/`PluginAssemblyLoader` discover and load plugins from disk, before Module Discovery ever runs, with zero code change to Discovery itself. |
| **Sample Module Set** | `ClockModule`/`ClockLifecycleObserverModule` — a real, extended, SDK-conformant module pair every later Work Package validated against. |
| **Event Bus** | `IEventBus`/`EventBus` — imperative subscribe/publish, sequential snapshot-based dispatch, unconditional per-subscriber failure isolation, DI-public by design (ADR-0020). |
| **Background Services** | `HostedServiceDiscoveryService`/`HostedServiceManager` — Host-orchestrated background work, isolated-by-default failure with an opt-in critical escalation (ADR-0021), started/stopped at new decimal-numbered Host Lifecycle phases (`8.1`/`10.1`). |
| **Dependency Injection for Discovered Modules** | `ModuleMetadataAttribute` lets a module declare metadata without being instantiated by Discovery, freeing it to declare a DI-resolvable constructor. |

Two infrastructure pieces ship complete and tested with **zero real
consumers by deliberate choice** — Plugin loading (`src/Plugins/` is
empty) and Background Services (no real hosted service ships yet). Both
are ready for a first real consumer whenever a future Work Package
decides to build one.

## Engineering Improvements

- The internal-test-seam pattern (a public, ambient-scanning entry point
  paired with an `internal`, explicit-input one) was applied consistently
  a third time, for Hosted Service Discovery, with no new capability
  needed.
- Two genuine cross-test isolation hazards were found and fixed as they
  recurred: a dynamic-assembly identity collision (`Assembly.LoadFrom`
  resolves by identity, not file path) and a `Console.Out`-redirection
  race between test classes sharing process-wide state — both fixed by
  extending an already-proven pattern, not inventing a new one.
- A genuine defect was found and fixed during Background Services'
  implementation: adding hosted service discovery had silently given
  several pre-existing `TempestHostBuilder` test-seam constructors a
  full-`AppDomain` scan by default, discovering test fixtures never meant
  to run — caught by running the *complete*, unfiltered test suite, not
  only the new tests in isolation.

## Architecture

- **11 new ADRs** (`ADR-0020`–`ADR-0030`), all Accepted, none superseded.
- **29 Rejected Designs entries**, recording every genuine alternative
  seriously considered and declined — DI multi-registration resolution,
  a dedicated descriptor type, active Host-level monitoring, automatic
  restart/backoff, and more.
- **Decimal sub-numbered Host Lifecycle phases** (`3.1`/`3.2` for
  Plugins, `8.1`/`10.1` for Background Services) proved, a second time,
  that the phase-table extension pattern established by `ADR-0026`
  composes cleanly without renumbering anything.
- **The four-layer platform model** (`ADR-0023`: Modules → Platform APIs
  → Platform Services → Runtime Host) absorbed six genuinely new
  capabilities without needing to change.
- **No breaking changes.** Every Runtime Foundation contract —
  Configuration, Logging, Discovery, Registration, Dependency Injection,
  Lifecycle, the Host's own state machine and failure model — is
  unchanged.

## Documentation

- 16 standing architecture documents under `docs/architecture/` (18
  including the two release-scoped documents), all cross-referenced, all
  current.
- `docs/releases/Platform Foundation Completion Report.md` — the full
  Foundation-phase closeout narrative.
- `PROJECT_STATUS.md` (repository root) — the new primary status
  dashboard.
- `docs/academy/Contributor Learning Path.md` — a repository-wide
  onboarding sequence for a new contributor.

## Academy

63 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards) — every completed Work Package has a
matching retrospective, verified by two independent audits (`WP 4.4F`,
and the Governance Register Baseline's own Academy Register). New this
release: `docs/academy/06 Engineering Standards/Engineering Lifecycle.md`
(the canonical Idea-to-Maintenance engineering pipeline) and
`03-governance-registers.md` (why and how to maintain the governance
suite).

## Governance

The first complete governance register suite TempestOS has produced: 27
registers across Architecture, Engineering, Quality, Documentation, and
Delivery, plus a Governance Index, Governance Philosophy, Governance
Audit Report, and Repository Maturity Report. Every register entry is
marked **Verified**, **Inferred**, or **Unknown** against direct
repository evidence — no history is invented. **Outstanding Governance
Debt: NONE.** `docs/governance/Future Work Package Guidelines.md`
establishes 10 mandatory expectations standing for every future Work
Package.

## Testing

| Metric | v0.3.0 | v0.4.0 | Change |
|---|---|---|---|
| Automated tests | 164 | 355 | +191 |
| Test failures | 0 | 0 | — |
| Build warnings | 0 | 0 | — |
| Build errors | 0 | 0 | — |

Verified stable across multiple consecutive full-suite runs at every
major Work Package boundary throughout the release. Testing philosophy
unchanged since v0.3.0: prefer real implementations over mocks; the one
recurring exception is a level-recording `ILogger`, used only to observe
log output.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 355 |
| ADRs | 30 (`ADR-0001`–`ADR-0030`), all Accepted |
| Rejected Designs | 29 (`RD-0001`–`RD-0029`) |
| Academy articles | 63 |
| Governance registers | 27 (32 governance documents total, including the Index, Philosophy, Audit Report, Maturity Report, and Future Work Package Guidelines) |
| Architecture documents | 16 (18 including the two release-scoped documents) |
| Platform services | 15 catalogued — 11 Implemented, 1 contract-only, 2 not implemented, 1 developer-convenience layer |
| Modules (production) | 2 (`ClockModule`, `ClockLifecycleObserverModule`) |
| Hosted services (production) | 0 (infrastructure complete; zero shipped consumers by design) |
| Plugins (production) | 0 (infrastructure complete; `src/Plugins/` empty by design) |
| Commits (this release, `v0.3.0` → `v0.4.0`) | 23 |
| Total repository commits | 50 (45 Claude-authored, 5 pre-dating Claude's involvement) |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

## Known Limitations

- `Tempest.App` does not run through `TempestHost` — it remains the
  pre-module-pipeline console front end. This is a disclosed, pre-existing
  gap, not introduced by this release.
- Zero real plugins and zero real hosted services ship in this release —
  both pieces of infrastructure are complete and tested; no Work Package
  has yet built a real consumer for either, by deliberate scope choice.
- `IHostedService`'s naming proximity to
  `Microsoft.Extensions.Hosting.IHostedService` remains an open,
  disclosed question — revisit-triggered on real usage evidence, which
  has not yet arrived.
- Two logging mechanisms still coexist (`ILogger` vs. the legacy
  `LoggingService`), and logging remains single-sink — both are
  pre-existing, disclosed debt items owned by a future Diagnostics Work
  Package.

Full, current detail: `docs/governance/Quality/Technical Debt Register.md`.

## What's Next

`WP 4.6A` (Navigation Architecture) is the next planned Work Package —
design-only, deciding what "navigation" means for TempestOS before any
implementation. It is followed by `WP 4.6B` (Navigation Implementation),
`WP 4.7` (Command Framework), `WP 4.8` (Diagnostics Improvements), and
`WP 4.9` (Developer Experience Improvements). None of these is part of
`v0.4.0` — see `docs/releases/v0.4.0/WorkPackages.md` for each one's full
scope, and `PROJECT_STATUS.md` for live, current status.

Future Work Packages are expected to build capability against this
foundation, not revisit it, absent documented evidence that requires
otherwise — see `docs/governance/Future Work Package Guidelines.md`.

## Acknowledgements

The Platform Foundation was developed using an architecture-first
engineering process: every non-trivial component was designed, reviewed,
implemented, tested, and documented, in that order, before the next one
began. Every genuine alternative seriously considered and declined was
recorded, not merely forgotten. This release marks the transition from
building the platform's own foundation to building capability on top of
it — the discipline established here is the asset the next milestone
inherits.

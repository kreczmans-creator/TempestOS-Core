# TempestOS v0.6.0 — "Platform Services"

**Release date:** 2026-07-30
**Tag:** `v0.6.0`
**Branch:** `feature/v0.6.0-platform-services` → `main`
**Certification:** CERTIFIED WITH ACCEPTED TECHNICAL DEBT

---

## Overview

TempestOS v0.6.0 closes the **Platform Services** phase: the first
release since the Runtime Foundation (v0.3.0) and the Platform
Foundation (v0.4.0) to add genuinely new, domain-facing capability
rather than infrastructure the platform needed to become usable
(v0.5.0, "Developer Experience"). Eight feature Work Packages —
Reporting, Permissions & Identity, Notifications, a REST API, Settings,
Audit, Licensing, and Export/Import — each built directly on the stable
foundation the first three releases established, none of them a
redesign of it.

A complete Architecture Package and Contract Review Package were both
produced and approved before any implementation began, and every one of
the eight feature Work Packages was implemented directly against those
unrevised documents. `WP 6.8` (Platform Services Integration Review &
Release Certification) then closed the release with an independent,
evidence-based certification review — not a rubber stamp: it
re-verified every architecture rule, every service registration, every
governance register, and the full test suite directly against the
repository, rather than trusting each prior Work Package's own claim,
and recommended **CERTIFIED WITH ACCEPTED TECHNICAL DEBT**. Product
Approval was then granted and this release was merged to `main`
(non-fast-forward), tagged, and pushed.

## Highlights

- **Eleven platform services now certified**, each with at least one
  verified real consumer — the REST API is the strongest evidence of
  reuse, with two entirely independent consumers
  (`ApiSampleModule`, `LicensingSampleModule`).
- **Three Security Roadmap trigger items resolved, not deferred.**
  Permissions & Identity (`WP 6.1`), the REST API (`WP 6.3`), and
  Licensing (`WP 6.6`) were each named in `docs/security/Security
  Roadmap.md` as requiring a dedicated architecture decision before
  implementation — all three landed with a genuine, written decision
  (`ADR-0043`/`ADR-0044`, `ADR-0047`–`ADR-0049`/`ADR-0052`,
  `ADR-0050`), confirmed by `WP 6.8`'s own Architecture Review.
- **One architectural decision proven empirically, not just reasoned
  about.** `WP 6.3`'s `CurrentPrincipalAccessor` request-scoping
  question was resolved by building both the rejected
  (`AsyncLocal<T>`) and shipped alternatives and running the real test
  suite against each — the rejected alternative regressed 17
  pre-existing tests (`ADR-0052`).
- **License validation resolves a genuinely open risk with an
  empirically-verified default.** A missing license file is a valid,
  unrestricted-but-uncapable default, never Host-fatal; a broken one
  (unreadable, malformed, expired) is — proven not to regress any of
  the 24 pre-existing tests that build a real `TempestHost`
  (`ADR-0050`).
- **1016 tests, up from 552 at v0.5.0** — zero regressions at any Work
  Package boundary, re-verified across six full-suite runs (four
  Debug, two Release) during `WP 6.8`'s own closing review alone.
- **A governance gap nine Work Packages wide, found and fully closed.**
  `Interface Register.md`, `Dependency Injection Register.md`, and
  `Module Register.md` had each gone stale since `v0.5.0`'s own close —
  `WP 6.7` first disclosed the gap, `WP 6.8` performed the full
  backfill (64 interfaces, 26 named DI registrations, 15 production
  modules), and independently discovered a second, previously
  undisclosed instance of the same pattern in `Governance Register.md`
  itself, backfilled in the same review.

## Major Features

| Feature | What It Does | Work Package |
|---|---|---|
| **Reporting** | `IReportingService`/`IReportTemplate<T>` — structured, formatted output from platform data, explicitly orthogonal to Export/Import (`ADR-0040`). | `WP 6.0` |
| **Permissions & Identity** | `IIdentityService`/`IPermissionEvaluator`/`CurrentPrincipalAccessor` — the platform's first concept of "who is doing this" and "are they allowed to." | `WP 6.1` |
| **Notifications** | `NotificationDispatcher` — a uniform way to tell a user, module, or external system something happened, derived from the Event Bus, not a replacement for it (`ADR-0046`). | `WP 6.2` |
| **REST API** | `RestApiHostedService` — invoke platform capability (the Command Framework, Diagnostics) from outside the running process, over ASP.NET Core/Kestrel, dispatched through the existing Command Framework (`ADR-0047`–`ADR-0049`). | `WP 6.3` |
| **Settings** | `ISettingsService`/`IPersistenceStore` — user-changeable, runtime-mutable configuration, backed by this platform's first real, shared Persistence abstraction (`ADR-0041`/`ADR-0042`). | `WP 6.4` |
| **Audit** | `IAuditService`/`IAuditQuery` — a durable, queryable, append-only record of who did what, when — distinct from Logging and Diagnostics (`ADR-0045`). | `WP 6.5` |
| **Licensing** | `ILicenseValidator`/`ILicenseProvider` — a pre-container, Host-startup, Host-fatal validation gate, with a missing license resolving to a valid, unrestricted-but-uncapable default (`ADR-0050`). | `WP 6.6` |
| **Export / Import** | `IExportService`/`IImportService` — user-facing, `Stream`-based, portable-artifact I/O, orthogonal to the internal Persistence abstraction, with Kind-routed multi-destination import (`ADR-0051`). | `WP 6.7` |

## Engineering Improvements

- **Kind-routed import dispatch** (`IExportableKind`/`IImportable`,
  `WP 6.7`) solves multi-destination import against a DI container that
  supports exactly one registration per service type, via a
  concrete-type dual registration reusing `ADR-0044`'s own
  `CurrentPrincipalAccessor` precedent — no container redesign
  required.
- **A genuine C# generic-constraint impossibility, resolved by mirroring
  rather than delegating** (`WP 6.2`): `NotificationDispatcher` cannot
  literally delegate to `IEventBus` without illegally tightening a
  generic constraint; resolved by mirroring `EventBus`'s own internal
  shape instead.
- **Two exact-static-type-dispatch defects found and fixed** against
  real sample-module consumers while writing their own integration
  tests (`WP 6.2`, `WP 6.5`) — caught by real test execution, not
  assumed correct from the design alone.
- **Zero `Service → Module`, zero `Module → Module` violations beyond
  one disclosed, constant-only exception** (`ApiSampleModule`
  referencing `ReportingSampleModule`'s public command-Id strings, not
  a live object reference) — confirmed by direct `grep`, not assumed,
  during `WP 6.8`'s Architecture Review.
- **One genuine, disclosed, non-blocking architectural finding**:
  `Tempest.Core.Diagnostics` imports `Tempest.Core.Runtime` for a
  single enum (`HostState`), a mutual namespace reference a literal
  reading of `ADR-0023` ("dependencies flow downward only") would
  flag. Shipped safely since `v0.5.0`; recommended for formal
  resolution (an accepted exception, or relocating `HostState`) as a
  `v0.7.0` candidate item.

## Architecture

- **13 new ADRs** (`ADR-0040`–`ADR-0052`), all Accepted, none
  superseded — `docs/adr/` now runs `ADR-0001` through `ADR-0052` with
  no gaps at all; every ADR `Required ADRs.md` ever reserved a number
  for is now a real, Accepted file.
- **Zero new Rejected Designs entries** this release — every genuine
  alternative this release considered was recorded within its own
  ADR's "Alternatives Considered" section instead, not a standalone
  Rejected Designs candidate (`Rejected Designs Register.md` remains at
  45 entries, unchanged).
- **The four-layer platform model** (`ADR-0023`) absorbed eight more
  genuinely new capabilities without needing to change, save the one
  disclosed `Runtime`↔`Diagnostics` finding above.
- **This platform's first substantial dependency on a pre-built
  framework component beyond the bare .NET SDK**: ASP.NET Core/Kestrel,
  confined entirely to one hosted-service type (`ADR-0049`).
- **No breaking changes** to any Platform Foundation or Developer
  Experience contract — Configuration, Logging, Discovery,
  Registration, Dependency Injection, Lifecycle, the Event Bus,
  Background Services, Plugin Manifest, Navigation, the Shell, the
  Command Framework, and Diagnostics are all unchanged.

## Security

Three Work Packages in this release were named triggers in `docs/
security/Security Roadmap.md` for security design work that had to
precede implementation, not follow it: `WP 6.1` (Permissions &
Identity, item 6), `WP 6.3` (REST API, item 7 — blocked on `WP 6.1`,
confirmed never started early), and `WP 6.6` (Licensing, item 8). `WP
6.8`'s own Architecture and Documentation Review confirmed all three
were resolved with a genuine, written architecture decision, not
quietly deferred again. The REST API — this platform's first
network-facing surface — binds to loopback only by default (`ADR-0049`)
pending real authentication and TLS design, both explicitly accepted,
disclosed future requirements (`TD-13`/`TD-14`), not current-release
defects, since the approved contract never promised either this
release.

## Documentation

- `docs/architecture/Platform Service Map.md` gained eight new service
  entries (Reporting, Identity & Permissions, Notifications, REST API,
  Settings, Persistence, Audit, Export/Import, Licensing), each
  following the identical documentation shape every prior new platform
  service's own entry has used.
- `Interface Register.md`, `Dependency Injection Register.md`, and
  `Module Register.md` are now fully backfilled and correct — every one
  of the 64 public interfaces, 26 named DI registrations, and 15
  production modules TempestOS ships is correctly recorded, closing a
  gap that had accumulated silently for six Work Packages before `WP
  6.7` disclosed it and `WP 6.8` closed it in full.
- `Governance Register.md`'s own Compliance Matrix — previously
  unmaintained since `v0.5.0`'s own close, missing all nine `v0.6.0`
  Work Packages entirely — was independently discovered stale during
  `WP 6.8`'s own closing review (not previously disclosed by any prior
  Work Package) and fully backfilled.

## Academy

86 articles across 7 categories (up from 77 at v0.5.0) — every
completed Work Package has a matching retrospective, including a new
`WP6.8-platform-services-integration-review.md` mirroring `WP 5.4`'s own
whole-release retrospective format (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways) rather than the standard 13-section per-feature
template.

## Governance

The 27-register governance suite held through nine Work Packages, with
one genuine, previously-undisclosed gap found and fully closed during
this release's own closing review (`Governance Register.md`'s
Compliance Matrix — see Documentation, above) and one larger,
already-disclosed gap (`Interface`/`Dependency Injection`/`Module
Register.md`) fully backfilled. All eight risks in `docs/releases/
v0.6.0/Risk Register.md` are now Closed or Mitigated, save one (`R8`)
Remaining by deliberate, disclosed design choice. Sixteen tracked debt
items and thirteen disclosed trade-offs were each classified Resolved,
Accepted, or Deferred — **zero Release Blocking**. See `WP6.8 Platform
Certification Report.md` for the complete decision and evidence.

## Testing

| Metric | v0.5.0 | v0.6.0 | Change |
|---|---|---|---|
| Automated tests | 552 | 1016 | +464 |
| Test failures | 0 | 0 | — |
| Build warnings | 0 | 0 | — |
| Build errors | 0 | 0 | — |

Re-verified across six full-suite runs (four Debug, two Release, two
from a fully clean rebuild) during `WP 6.8`'s own closing review alone
— the deepest test-stability verification any single Work Package this
release performed. Zero instances of the previously-disclosed,
non-reproducible `Console.Out`-capture flake (`WP 6.3`'s own finding)
were observed across any run.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 1016 |
| ADRs | 52 (`ADR-0001`–`ADR-0052`, no gaps), all Accepted |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) — unchanged this release |
| Academy articles | 86 |
| Governance registers | 27 (32 governance documents total) |
| Architecture documents | 20 (22 including the two release-scoped documents) |
| Platform services | 26 catalogued — 23 Implemented, 2 not implemented as platform services, 1 developer-convenience layer |
| Modules (production) | 15 |
| Hosted services (production) | 2 |
| Plugins (production) | 0 (infrastructure complete; `src/Plugins/` empty by deliberate scope decision) |
| Custom exception types | 52 |
| Commits (this release) | 13, `v0.5.0` → `v0.6.0`, including the non-fast-forward merge to `main` |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

## Known Limitations

- `TD-09`/`TD-10`/`TD-11` (no plugin/first-party trust isolation;
  `NavigationService.Unregister` has no ownership check; Command/
  Navigation registration-order squatting) remain **Deferred** — the
  enforcement mechanism (`IPermissionEvaluator`, `ADR-0044`) exists,
  but retrofitting it into these three call sites is not yet scoped to
  any Work Package; `src/Plugins/` remains empty, so no real
  third-party plugin exists this release to exploit the gap.
- `TD-13`/`TD-14` (the REST API has no real authentication and no TLS
  on its Kestrel listener) are **Accepted** — mitigated, not fixed, by
  binding to loopback only by default; a genuine, named future
  requirement once a deployment scenario beyond a trusted local network
  exists.
- `TD-16` (no cryptographic license file signature verification) is
  **Accepted** — no concrete distribution channel or tamper-threat
  model exists yet in this release's own approved scope.
- The Shell's own input handling remains unwired to the Command
  Framework (carried forward from v0.5.0, unchanged this release).

Full, current detail: `docs/releases/v0.6.0/WP6.8 Technical Debt
Disposition.md`.

## What's Next

`v0.7.0` ("Engineering Foundation," working name) begins on
`feature/v0.7.0-engineering-foundation`, cut from `main` at the `v0.6.0`
tag. No Work Package has been scoped or approved yet — see
`docs/releases/v0.7.0/WorkPackages.md` for candidate items sourced
directly from `WP 6.8`'s own recommendations, pending a dedicated
Architecture, Planning, and Contract Review phase. See
`PROJECT_STATUS.md` for current, live status.

## Acknowledgements

The Platform Services phase was developed using the same
architecture-first engineering process every prior release established:
a complete Architecture Package and Contract Review Package were both
approved before any Work Package began, and every Work Package was
implemented directly against those unrevised documents, disclosing
genuine implementation-phase findings via ADRs rather than silently
absorbing them. This release marks the platform's transition from an
application a person can use (`v0.5.0`) to a platform other services —
Reporting, Identity, Notifications, a network API, Settings, Audit,
Licensing, and Export/Import — genuinely depend on and integrate with,
each proven by at least one real, verified consumer.

# Repository Metrics Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Repository Metrics Register |
| **Purpose** | A single, dated snapshot of the repository's own size and shape — file counts, line counts, commit counts — so future baselines can measure growth against a known point rather than re-deriving it from scratch. |
| **Scope** | `src/`, `tests/`, `docs/`, and git history, as they stood at time of review. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct repository inspection (`find`, `wc`, `git log`) performed as part of this Work Package. |
| **Review Frequency** | Re-measured at each Governance Baseline review, or on request. |
| **Last Reviewed** | 2026-07-27 (WP 5.0C, Shell & Composition Framework Architecture). |
| **Related Documents** | `Test Register.md`; `Namespace Register.md`; `Engineering Evolution Register.md`. |
| **Related ADRs** | None directly. |
| **Related Academy Articles** | None directly — this is a raw metrics snapshot, not a teaching document. |
| **Coverage Status** | Complete (as a point-in-time snapshot; by nature, it goes stale the moment the repository changes further — see Review Frequency). |

---

## Snapshot: 2026-07-25 (WP 4.5A)

| Metric | Value |
|---|---|
| Total commits | 48 |
| Claude-authored commits (carry `Co-Authored-By: Claude` trailer) | 43 |
| Pre-Claude commits | 5 |
| First Claude-authored commit | `7514b9d`, 2026-07-21 |
| Most recent commit (at time of review) | `c460aaf`, 2026-07-25 |
| `src/` `.cs` files (excluding `obj`/`bin`) | 106 |
| `src/` `.cs` lines (excluding `obj`/`bin`) | 6,603 |
| `tests/` `.cs` files (excluding `obj`/`bin`) | 55 |
| `tests/` `.cs` lines (excluding `obj`/`bin`) | 7,310 |
| `docs/` `.md` files | 134 |
| `docs/` `.md` lines | 24,215 |
| Projects in solution | 4 (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`, `Tempest.Core.Tests`) |
| Namespaces under `src/` | 14 declared + 1 global (see `Namespace Register.md`) |
| ADRs | 30 (`ADR-0001`–`ADR-0030`), all Accepted |
| Rejected Designs entries | 29 (`RD-0001`–`RD-0029`) |
| Architecture documents (`docs/architecture/`) | 16 |
| Academy articles (`docs/academy/`, all subfolders) | 61 |
| Public interfaces (`src/Tempest.Core/`) | 26 |
| Custom exception types | 22 |
| Production modules | 2 (`ClockModule`, `ClockLifecycleObserverModule`) |
| Production event types | 1 (`ClockModuleLifecycleEvent`) |
| Production hosted services | 0 (infrastructure only — see `Hosted Services Register.md`) |
| Production plugins | 0 (`src/Plugins/` empty — see `Plugin Register.md`) |
| Test files | 55 |
| `[Fact]`/`[Theory]` attribute occurrences | 340 |
| Executed tests (`dotnet test`) | 355, 0 failures |
| Build warnings | 0 |
| Build errors | 0 |
| Current `VERSION` | 0.3.0 (v0.4.0 not yet tagged) |
| Current branch | `feature/v0.4.0-platform-services` |

## Snapshot: 2026-07-25 (WP 4.5B — Platform Foundation Closeout)

Documentation and governance only — no production or test code changed
since the WP 4.5A snapshot above; every `src/`/`tests/` figure is
unchanged. Shown here are only the metrics this Work Package's own
additions moved.

| Metric | WP 4.5A | WP 4.5B (current) |
|---|---|---|
| Root `.md` files | 2 (`README.md`, `LICENSE.md`) | 3 (adds `PROJECT_STATUS.md`) |
| `docs/` `.md` files | 134 | 152 |
| Academy articles (`docs/academy/`, all subfolders) | 61 | 63 (adds `Contributor Learning Path.md`, `06 Engineering Standards/Engineering Lifecycle.md`) |
| Governance documents (`docs/governance/`, all subfolders) | 31 | 32 (adds `Future Work Package Guidelines.md`) |
| `docs/releases/` documents | 6 | 7 (adds `Platform Foundation Completion Report.md`) |
| Engineering Standards documents | 4 | 5 (adds `Engineering Lifecycle.md`) |
| Executed tests (`dotnet test`) | 355, 0 failures | 355, 0 failures (unchanged) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (v0.4.0 Release Engineering — "Platform Foundation")

Release-preparation changes only — `VERSION`, `CHANGELOG.md`, release
documentation, and governance-register updates; no production or test
code changed since the WP 4.5B snapshot above.

| Metric | WP 4.5B | v0.4.0 (current) |
|---|---|---|
| `docs/releases/` `.md` files | 6 | 13 (adds `v0.4.0.md`, `v0.4.0/Release Notes.md`) |
| `docs/` `.md` files | 152 | 154 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — this release updates existing registers rather than adding new ones) |
| Root `VERSION` | `0.3.0` | **`0.4.0`** |
| Total commits | 48 | 50 (45 Claude-authored, 5 pre-Claude) |
| Commits since `v0.3.0` tag | — | 23 |
| Executed tests (`dotnet test`) | 355, 0 failures | 355, 0 failures (unchanged) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0A — Navigation Framework Architecture)

Architecture-only Work Package — no production or test code changed
since the v0.4.0 Release Engineering snapshot above; every `src/`/
`tests/` figure is unchanged. Shown here are only the metrics this Work
Package's own additions moved.

| Metric | v0.4.0 Release Engineering | WP 5.0A (current) |
|---|---|---|
| `src/` `.cs` files / lines | 106 / 6,603 | 106 / 6,603 (unchanged) |
| `tests/` `.cs` files / lines | 55 / 7,310 | 55 / 7,310 (unchanged) |
| ADRs | 30 (`ADR-0001`–`ADR-0030`) | 32 (`ADR-0001`–`ADR-0032`, adds `ADR-0031`, `ADR-0032`) |
| Rejected Designs entries | 29 (`RD-0001`–`RD-0029`) | 33 (`RD-0001`–`RD-0033`, adds `RD-0030`–`RD-0033`) |
| Architecture documents (`docs/architecture/`) | 16 | 17 (adds `Navigation Framework Architecture.md`) |
| Academy articles (`docs/academy/`, all subfolders) | 63 | 65 (adds `09-navigation-architecture.md`, `WP5.0A-navigation-framework-architecture.md`) |
| `docs/releases/` `.md` files | 13 | 15 (adds `docs/releases/v0.5.0/ReleasePlan.md`, `WorkPackages.md`) |
| `docs/` `.md` files | 154 | 161 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — this Work Package updates existing registers rather than adding new ones) |
| Root `VERSION` | `0.4.0` | `0.4.0` (unchanged — v0.5.0 not yet tagged) |
| Current branch | `main` (post-merge) | `feature/v0.5.0-developer-experience` |
| Total commits | 50 | 52 (47 Claude-authored, 5 pre-Claude) |
| Commits since `v0.4.0` tag | — | 0 (this Work Package not yet committed at time of this snapshot) |
| Executed tests (`dotnet test`) | 355, 0 failures | 355, 0 failures (unchanged — architecture-only, by design) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0B — Navigation Framework Implementation)

The first Work Package since `v0.4.0` Release Engineering to change
`src/`/`tests/` — implements `Tempest.Core.Navigation`, three new
`Tempest.Samples` reference modules, and 45 new tests.

| Metric | WP 5.0A | WP 5.0B (current) |
|---|---|---|
| `src/` `.cs` files / lines | 106 / 6,603 | 116 / 7,231 (adds 7 `Tempest.Core.Navigation` files, 3 `Tempest.Samples` files) |
| `tests/` `.cs` files / lines | 55 / 7,310 | 58 / 8,163 (adds 2 `Navigation/` test files, 1 `Samples/` test file; extends `DynamicPluginAssemblyBuilder.cs`) |
| Executed tests (`dotnet test`) | 355, 0 failures | **400, 0 failures** (45 new) |
| Namespaces under `src/` | 15 declared + 1 global | 16 declared + 1 global (adds `Tempest.Core.Navigation`) |
| Public interfaces (`src/Tempest.Core/`) | 26 | 27 (adds `INavigationProvider`) |
| Custom exception types | 22 | 25 (adds `NavigationException` and 2 subtypes) |
| Production modules | 2 (`ClockModule`, `ClockLifecycleObserverModule`) | 5 (adds `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`) |
| Production event types | 1 (`ClockModuleLifecycleEvent`) | 2 (adds `NavigationRequestedEvent`) |
| Platform services (Register total) | 16 catalogued, 11 Implemented | 16 catalogued, **12 Implemented** (Navigation moves from Designed to Implemented) |
| Architecture documents (`docs/architecture/`) | 17 | 17 (unchanged — no new document; `Navigation Framework Architecture.md`'s own status field updated in place) |
| Academy articles (`docs/academy/`, all subfolders) | 65 | 66 (adds `WP5.0B-navigation-framework-implementation.md`) |
| `docs/` `.md` files | 161 | 162 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Total commits | 52 | 53 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0C — Shell & Composition Framework Architecture)

Architecture-only Work Package — no production or test code changed
since the WP 5.0B snapshot above; every `src/`/`tests/` figure is
unchanged. Shown here are only the metrics this Work Package's own
additions moved.

| Metric | WP 5.0B | WP 5.0C (current) |
|---|---|---|
| `src/` `.cs` files / lines | 116 / 7,231 | 116 / 7,231 (unchanged) |
| `tests/` `.cs` files / lines | 58 / 8,163 | 58 / 8,163 (unchanged) |
| ADRs | 32 (`ADR-0001`–`ADR-0032`) | 35 (`ADR-0001`–`ADR-0035`, adds `ADR-0033`–`ADR-0035`) |
| Rejected Designs entries | 33 (`RD-0001`–`RD-0033`) | 37 (`RD-0001`–`RD-0037`, adds `RD-0034`–`RD-0037`) |
| Architecture documents (`docs/architecture/`) | 17 | 18 (adds `Shell & Composition Framework Architecture.md`) |
| Academy articles (`docs/academy/`, all subfolders) | 66 | 68 (adds `10-shell-and-application-composition.md`, `WP5.0C-shell-and-composition-framework-architecture.md`) |
| `docs/` `.md` files | 162 | 168 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — this Work Package updates existing registers rather than adding new ones) |
| Root `VERSION` | `0.4.0` | `0.4.0` (unchanged — v0.5.0 not yet tagged) |
| Current branch | `feature/v0.5.0-developer-experience` | `feature/v0.5.0-developer-experience` (unchanged) |
| Total commits | 53 | 54 (before this Work Package's own commit) |
| Executed tests (`dotnet test`) | 400, 0 failures | 400, 0 failures (unchanged — architecture-only, by design) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Governance Suite Size (Introduced by This Work Package, WP 4.5A)

| Metric | Value |
|---|---|
| Governance registers created | 27 |
| Governance top-level documents created (Index, Philosophy, Audit Report, Maturity Report) | 4 |
| Total governance documents | 31 |

## Methodology Note

Line counts are raw `wc -l` totals, including blank lines, comments, and
XML documentation — not a "logical lines of code" metric. File counts
explicitly exclude `obj`/`bin` build-artifact directories. These figures
are a snapshot, not a trend — no historical size-over-time series is
claimed or fabricated; where a prior snapshot does not exist (this is the
first Repository Metrics Register TempestOS has produced), no comparison
is offered.

## Cross-Reference Check

The ADR count (35), Rejected Designs count (37), Academy article count
(68), test count (400), and build status (0/0) above are each
cross-checked directly against `ADR Register.md`, `Rejected Designs
Register.md`, `Academy Register.md`, `Test Register.md`, and `Validation
Register.md` respectively — all consistent, no discrepancy found.

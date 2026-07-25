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
| **Last Reviewed** | 2026-07-25 (WP 4.5B). |
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

The ADR count (30), Rejected Designs count (29), Academy article count
(62), test count (355), and build status (0/0) above are each
cross-checked directly against `ADR Register.md`, `Rejected Designs
Register.md`, `Academy Register.md`, `Test Register.md`, and `Validation
Register.md` respectively — all consistent, no discrepancy found.

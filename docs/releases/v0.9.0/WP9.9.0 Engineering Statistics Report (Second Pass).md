# WP 9.9.0 — Release Preparation & Product Baseline — Engineering Statistics Report (Second Pass)

## Purpose

The engineering statistics baseline for `v0.9.0`, re-derived a second
time after `WP 9.8B` (Platform Service Register Reconciliation), every
figure verified directly against the repository, not carried forward
from the first pass's own `WP9.9.0 Engineering Statistics Report.md`.

## Headline Metrics — Delta Since the First Pass

| Metric | First Pass | Second Pass | Change | Cause |
|---|---|---|---|---|
| Automated tests | 2026 | 2026 | — | `WP 9.8B` added zero tests (documentation-only) |
| Test failures (this pass's own runs) | 0/4 runs | 1/5 runs (non-reproducible in isolation, resolved on re-run) | +1 observed instance | `TD-34`, newly registered — see Release Readiness Report §3 |
| Build warnings/errors | 0/0 | 0/0 | — | Unchanged |
| ADRs | 91 | 91 | — | `WP 9.8B` added zero ADRs |
| Rejected Designs | 45 | 45 | — | Unchanged |
| Academy articles | 125 → 126 (`WP 9.8B`) | 126 → 127 (this pass's own Academy Retrospective) | +1 | This pass's own required deliverable |
| **Platform Services catalogued** | **27 (claimed, actually 26 — a distinct arithmetic error)** | **30, verified consistent across all five governance documents** | **+4, `WP 9.8B`** | **Engineering Data Model, Materials, Engineering Calculations, Verification — the gap this Work Package's own first pass named as `v0.9.0`'s own top standing recommendation, now closed** |
| Modules (production) | 34 | 34 | — | Unchanged |
| Public interfaces | 168 | 168 | — | Unchanged |
| DI registrations | 44 raw / 42 named | 44 raw / 42 named | — | Unchanged |
| Technical Debt Register items | 33 | 34 | +1 | `TD-34`, newly registered this pass |
| Future Capability Register entries | 62 | 62 | — | Unchanged |

## Governance Documents Touched Between Passes

`WP 9.8B` (between the two `WP 9.9.0` passes): `Platform Services
Register.md` (+4 rows, 1 arithmetic correction, 1 metadata range
correction), `Platform Service Map.md` (+4 sections, 2 stale
"Depended on by" corrections), `Documentation Register.md`,
`Academy Register.md`, `PROJECT_STATUS.md`. This pass itself:
`Technical Debt Register.md` (+`TD-34`), plus the same five documents
`WP 9.8B` touched, updated again to reflect this pass's own findings.

## Test Verification — Full Detail

5 full-suite runs this pass (2 Debug, 2 Release, plus 1 Release re-run
after the one observed failure), plus 5 further isolated runs of the
one failing test class, plus 1 scoped Workspace-namespace run:

| Run | Configuration | Result |
|---|---|---|
| 1 | Debug (clean rebuild) | 2026/2026 |
| 2 | Debug | 2026/2026 |
| 3 | Release (clean rebuild) | **2025/2026** (`CompositeLogSinkTests`, `TD-34`) |
| 4 | Release (re-run) | 2026/2026 |
| 5 | Release (`scripts/new-release.ps1`) | 2026/2026 |
| 6–10 | `CompositeLogSinkTests` alone, ×5 | 11/11 each, 0 failures |
| 11 | Scoped (Workspace + `EngineeringCockpit`) | 516/516 |

**11 total runs this pass; 1 genuine flake instance, fully
characterised and formally registered (`TD-34`); zero reproducible
regressions.**

## Build Verification

4/4 projects, both configurations, each from a fully-removed `bin`/`obj`
tree, plus per-project Release builds of `Tempest.App`/`Tempest.Samples`
— identical clean result to the first pass, confirmed by independent
re-run rather than assumed still true.

## Security Reviews Performed, Cumulative

Eight dedicated Security Reviews across this release now (one per
implementation Work Package, plus `WP 9.8B`'s own) — this pass adds no
new one of its own, since it is itself a re-verification pass, not a
new implementation Work Package; its own security posture is covered
by the Release Readiness Report's own §18.

## Contributors

1 (repository owner; all commits co-authored by Claude), unchanged.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report (Second
Pass).md`; `docs/releases/v0.9.0/WP9.9.0 Engineering Statistics
Report.md` (first pass); `PROJECT_STATUS.md`.

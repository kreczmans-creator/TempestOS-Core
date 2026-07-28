# Validation Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Validation Register |
| **Purpose** | Records the validation gates every Work Package must pass, and the current pass/fail status of each gate as of this baseline. |
| **Scope** | The Build Gate, Test Gate, and documentation/governance gates defined by `docs/releases/v0.4.0/ReleaseChecklist.md` and Engineering Governance §2/§3. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/releases/v0.4.0/ReleaseChecklist.md`; `docs/releases/v0.4.0/Testing.md`; `docs/academy/06 Engineering Standards/Engineering Governance.md` (§2 Review Gates, §3 Definition of Done). |
| **Review Frequency** | Checked at the end of every Work Package (per-Work-Package gates) and before every release tag (release-level gates). |
| **Last Reviewed** | 2026-07-28 (WP 5.0S, Platform Security Baseline Audit) — Build/Test Gates re-run directly; 448/448, 2 new regression tests. |
| **Related Documents** | `docs/releases/v0.4.0/ReleaseChecklist.md`; `docs/releases/v0.4.0/Testing.md`; `Test Register.md`; `Repository Metrics Register.md`. |
| **Related ADRs** | None directly — this register concerns process gates, not architectural decisions. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/02-testing-strategy.md`. |
| **Coverage Status** | Complete. |

---

## Per-Work-Package Gates (Engineering Governance §2/§3, `ReleaseChecklist.md`)

| Gate | Requirement | Status as of This Baseline |
|---|---|---|
| Build Gate | `dotnet build` — 0 warnings, 0 errors | **Verified — pass.** Re-run directly as part of WP 4.5A: 0 warnings, 0 errors. Unchanged through WP 4.5B, `v0.4.0` Release Engineering, and WP 5.0A (none touched `src/`/`tests/`). Re-run again at `WP 5.0B` against the new `Tempest.Core.Navigation` source and three new sample modules: still 0 warnings, 0 errors. Re-run again at `WP 5.0C` (architecture-only, no `src/`/`tests/` change): still 0 warnings, 0 errors. Re-run again at `WP 5.0D` against `ITempestHost.Services` and the new `Tempest.App.Shell` namespace: still 0 warnings, 0 errors. Re-run again at `WP 5.0S` against the plugin manifest path-containment fix: still 0 warnings, 0 errors. |
| Test Gate | `dotnet test` — 100% pass, including every pre-existing test | **Verified — pass.** 355/355 passing at WP 4.5A/`v0.4.0`/WP 5.0A. **400/400** at `WP 5.0B` (355 pre-existing + 45 new, 0 failures) — re-run directly. Re-confirmed unchanged (400/400) at `WP 5.0C`. **446/446** at `WP 5.0D` (400 pre-existing + 46 new, 0 failures), re-verified stable across repeated runs. **448/448** at `WP 5.0S` (446 pre-existing + 2 new regression tests, 0 failures). |
| No `TODO`/dead code/commented-out code in changed files | Manual review per Work Package | **Verified for WP 4.5A, WP 4.5B, WP 5.0A, WP 5.0B, WP 5.0C, WP 5.0D, and WP 5.0S.** Not re-audited historically for every prior Work Package as part of this baseline — see Coverage Note below. |
| XML documentation on every public type/member introduced or touched | Manual review per Work Package | Not independently re-audited as part of this baseline — **Inferred** compliant from the consistently high documentation quality already found by `WP 4.4F`'s own Academy audit. |
| Every test category named in `Testing.md` has an identifiable, correctly-named test | Cross-check against `Testing.md`'s per-Work-Package table | **Verified** for WP 4.5 (Background Services) directly against `Testing.md`'s own row for it — see `Test Register.md`. |
| A completion report exists (Governance §4) | Per Work Package | **Inferred** — every Work Package's own retrospective structurally contains the required summary/files/decisions/results sections (Verified by direct inspection of the 13-section template). |
| An ADR exists where Governance §5 criteria are met | Per Work Package | **Verified** — see `ADR Register.md` and `Decision Register.md` for the boundary. |
| Academy documentation created/updated in the same Work Package | Per Work Package | **Verified** — see `Academy Register.md`. |
| `Architecture.md`'s reuse map checked | Per Work Package | Not independently re-audited for every historical Work Package — **Inferred** from `docs/releases/v0.4.0/Architecture.md`'s own content remaining internally consistent with `CHANGELOG.md` at every review point this baseline checked. |
| Work remains on the release's feature branch, unmerged into `main`, until the release is cut | Per Work Package | **Verified for WP 5.0S** — `git branch` confirms `feature/v0.5.0-developer-experience` is current; no merge to `main` has occurred. (Historical note: this row previously tracked `feature/v0.4.0-platform-services`, which *did* merge to `main` at the `v0.4.0` Release Engineering milestone — see the now-superseded "Release-Level Gates" table below, retained rather than deleted.) |

## Coverage Note

This baseline **re-runs** the Build and Test Gates directly (both
Verified pass, 448/448 tests as of `WP 5.0S`) and **re-checks**
the ADR/Academy/Decision boundary gates via the registers this Work
Package itself produces. It does **not** re-execute a manual "no
TODO/dead code" or "every public member documented" audit across all
prior Claude-authored commits — that would be a full re-review of the
entire codebase, out of scope for a documentation/architecture-only Work
Package. Those two rows are marked **Inferred**, not **Verified**, for
that reason, consistent with this Work Package's own rule that
Unknown/Inferred is preferable to a fabricated Verified claim.

## Release-Level Gates — `v0.4.0` ("Platform Foundation," Released 2026-07-27)

| Gate | Status |
|---|---|
| All Work Packages in `v0.4.0`'s final scope meet their own Acceptance Criteria | **Verified** — see `docs/releases/v0.4.0/WorkPackages.md`; Navigation/Command Framework/Diagnostics/DevEx were formally rescoped out to `v0.5.0` before tagging, not left incomplete inside `v0.4.0`. |
| `CHANGELOG.md` reflects every landed change | **Verified**, finalized at release. |
| `Risks.md` reviewed, every risk retired or explicitly accepted | **Verified** — see `Risk Register.md`. |
| Full solution build/test from a clean, committed tree | **Verified** — 355/355 tests, 0 warnings/errors, at merge to `main` (`5802b92`). |
| `docs/releases/v0.4.0.md` release notes written | **Verified.** |
| `VERSION` updated to `0.4.0` | **Verified** — tagged `v0.4.0`, pushed to `origin`. |

## Release-Level Gates — `v0.5.0` ("Developer Experience," Not Yet Applicable)

| Gate | Status |
|---|---|
| All Work Packages (`WP 5.0A`–`WP 5.3`) meet their own Acceptance Criteria | Not Yet Applicable — release not yet cut; `WP 5.0A`–`WP 5.0D` and `WP 5.0S` complete, `WP 5.1` onward remain to be done |
| `CHANGELOG.md` reflects every landed change | Not Yet Applicable — no `v0.5.0` entry started |
| `docs/releases/v0.5.0.md` release notes written | Not Yet Applicable — release not yet cut |
| `VERSION` updated to `0.5.0` | Not Yet Applicable — `VERSION` file still reads `0.4.0` (Verified directly) |

**Reason (Not Yet Applicable rows).** `v0.5.0` has not been released;
these gates are release-tagging gates, checked once, at the point of
tagging, not at every Work Package. **Review Trigger.** The Work Package
that proposes cutting the `v0.5.0` release tag.

## Cross-Reference Check

Every per-Work-Package gate above is cross-checked against
`ReleaseChecklist.md`'s own checklist item-for-item — no gate exists in
one document but not the other. The Test Gate's 448/448 figure is
cross-checked directly against `Test Register.md`'s own total, and
against `Repository Metrics Register.md`'s WP 5.0S snapshot — all
consistent.

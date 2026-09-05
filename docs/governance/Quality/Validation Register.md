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
| **Last Reviewed** | 2026-09-04 (WP 16.2A, Register and Status Currency) — **staleness disclosure and current-state correction, not a full per-Work-Package re-run.** This register's own header fields (Scope/Source of Truth citing `docs/releases/v0.4.0/ReleaseChecklist.md`) and its Per-Work-Package Gates table (frozen at `WP 5.3`, "552/552 tests") had not been updated in ten releases — `VERSION` now reads `0.15.0`, not `0.4.0`. See the new **Current State (`WP 16.2A`)** section below for the re-derived, current figures (2,725 `[Fact]`/`[Theory]` attributes; last real, CI-verified totals 3,088 Core / 408 Desktop at the `v0.15.0` tag, 412 Desktop after `WP 15.2A`) and the extended Release-Level Gates summary through `v0.15.0`. The Per-Work-Package Gates table and its `WP 5.3` "Last Reviewed" figure below are retained verbatim as the historical record they are — not deleted, not silently updated — per this register's own disclosure convention. See `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`. Previously reviewed 2026-07-28 (WP 5.3, Developer Experience Improvements) — Build/Test Gates re-run directly; 552/552, 10 new tests. |
| **Related Documents** | `docs/releases/v0.4.0/ReleaseChecklist.md`; `docs/releases/v0.4.0/Testing.md`; `Test Register.md`; `Repository Metrics Register.md`. |
| **Related ADRs** | None directly — this register concerns process gates, not architectural decisions. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/02-testing-strategy.md`. |
| **Coverage Status** | **Partial — current-state figures re-derived `WP 16.2A`; full per-Work-Package gate history not reconstructed for `v0.6.0`–`v0.15.0`.** See **Current State (`WP 16.2A`)** below. |

---

## Per-Work-Package Gates (Engineering Governance §2/§3, `ReleaseChecklist.md`)

| Gate | Requirement | Status as of This Baseline |
|---|---|---|
| Build Gate | `dotnet build` — 0 warnings, 0 errors | **Verified — pass.** Re-run directly as part of WP 4.5A: 0 warnings, 0 errors. Unchanged through WP 4.5B, `v0.4.0` Release Engineering, and WP 5.0A (none touched `src/`/`tests/`). Re-run again at `WP 5.0B` against the new `Tempest.Core.Navigation` source and three new sample modules: still 0 warnings, 0 errors. Re-run again at `WP 5.0C` (architecture-only, no `src/`/`tests/` change): still 0 warnings, 0 errors. Re-run again at `WP 5.0D` against `ITempestHost.Services` and the new `Tempest.App.Shell` namespace: still 0 warnings, 0 errors. Re-run again at `WP 5.0S` against the plugin manifest path-containment fix: still 0 warnings, 0 errors. Re-run again at `WP 5.1A` (architecture-only, no `src/`/`tests/` change): still 0 warnings, 0 errors. Re-run again at `WP 5.1B` against the new `Tempest.Core.Commands` implementation and `Tempest.Samples.CommandSampleModule`: still 0 warnings, 0 errors. Re-run again at `WP 5.2` against `CompositeLogSink`, `IDiagnosticsProvider`/`DiagnosticsProvider`, and `Tempest.Samples.DiagnosticsSampleModule`: still 0 warnings, 0 errors. Re-run again at `WP 5.3` against the `dotnet new tempest-module` template's own build/test tooling and `ReflectionFrameworkDiscoveryService`'s clearer failure message: still 0 warnings, 0 errors. |
| Test Gate | `dotnet test` — 100% pass, including every pre-existing test | **Verified — pass.** 355/355 passing at WP 4.5A/`v0.4.0`/WP 5.0A. **400/400** at `WP 5.0B` (355 pre-existing + 45 new, 0 failures) — re-run directly. Re-confirmed unchanged (400/400) at `WP 5.0C`. **446/446** at `WP 5.0D` (400 pre-existing + 46 new, 0 failures), re-verified stable across repeated runs. **448/448** at `WP 5.0S` (446 pre-existing + 2 new regression tests, 0 failures). Re-confirmed unchanged (448/448) at `WP 5.1A` (architecture-only, no tests added). **514/514** at `WP 5.1B` (448 pre-existing + 66 new, 0 failures). **542/542** at `WP 5.2` (514 pre-existing + 28 new, 0 failures). **552/552** at `WP 5.3` (542 pre-existing + 10 new, 0 failures). |
| No `TODO`/dead code/commented-out code in changed files | Manual review per Work Package | **Verified for WP 4.5A, WP 4.5B, WP 5.0A, WP 5.0B, WP 5.0C, WP 5.0D, WP 5.0S, WP 5.1A, WP 5.1B, WP 5.2, and WP 5.3.** Not re-audited historically for every prior Work Package as part of this baseline — see Coverage Note below. |
| XML documentation on every public type/member introduced or touched | Manual review per Work Package | Not independently re-audited as part of this baseline — **Inferred** compliant from the consistently high documentation quality already found by `WP 4.4F`'s own Academy audit. |
| Every test category named in `Testing.md` has an identifiable, correctly-named test | Cross-check against `Testing.md`'s per-Work-Package table | **Verified** for WP 4.5 (Background Services) directly against `Testing.md`'s own row for it — see `Test Register.md`. |
| A completion report exists (Governance §4) | Per Work Package | **Inferred** — every Work Package's own retrospective structurally contains the required summary/files/decisions/results sections (Verified by direct inspection of the 13-section template). |
| An ADR exists where Governance §5 criteria are met | Per Work Package | **Verified** — see `ADR Register.md` and `Decision Register.md` for the boundary. |
| Academy documentation created/updated in the same Work Package | Per Work Package | **Verified** — see `Academy Register.md`. |
| `Architecture.md`'s reuse map checked | Per Work Package | Not independently re-audited for every historical Work Package — **Inferred** from `docs/releases/v0.4.0/Architecture.md`'s own content remaining internally consistent with `CHANGELOG.md` at every review point this baseline checked. |
| Work remains on the release's feature branch, unmerged into `main`, until the release is cut | Per Work Package | **Verified for WP 5.3** — `git branch` confirms `feature/v0.5.0-developer-experience` is current; no merge to `main` has occurred. (Historical note: this row previously tracked `feature/v0.4.0-platform-services`, which *did* merge to `main` at the `v0.4.0` Release Engineering milestone — see the now-superseded "Release-Level Gates" table below, retained rather than deleted.) |

## Current State (`WP 16.2A`)

The Per-Work-Package Gates table below and the Release-Level Gates
tables for `v0.4.0`/`v0.5.0` are a frozen historical snapshot from
`WP 5.3` (2026-07-28) — retained verbatim, not updated, per this
register's own disclosure convention. This section states what is
actually current, at this Work Package's own base commit:

| Fact | Value | Derivation |
|---|---|---|
| `VERSION` | `0.15.0`, not `0.4.0` | `cat VERSION` |
| Test method count | 2,725 `[Fact]`/`[Theory]` attributes under `tests/` | `grep -rE '\[Fact\]\|\[Theory\]' tests --include=*.cs \| wc -l` |
| Last real, CI-verified full-suite totals | **3,088/3,088 Core, 408/408 Desktop**, 0 failures, both configurations, at the `v0.15.0` tag | `docs/releases/v0.15.0/Release Notes.md` §"Test Results" |
| Desktop total after the next landed Work Package | **412/412 Desktop**, 0 failures (Debug) | `docs/releases/v0.16.0/WP15.2A Desktop Test Suite Persistence Root Cleanup — Implementation Report.md` (its own directly-run suite output) |

The 2,725 attribute count is a **method** count, not a **run** count —
it is not directly comparable to the 3,088/408 CI totals above (a
`[Theory]` method with multiple `[InlineData]` cases runs more than
once; the CI totals are actual executed-test counts from a real
`dotnet test` run). Both figures are reported here, distinctly labelled,
rather than conflated into one misleading "test count." Neither this
Work Package nor `WP 16.1A` (Release Gate enforcement) re-ran the full
suite directly — that is `dotnet test`, explicitly not required by this
Work Package's own controlling instruction (attribute-counting is
sufficient); the 3,088/408/412 figures above are cited from already-
published, CI-backed sources, not re-executed here.

## Coverage Note

This baseline **re-runs** the Build and Test Gates directly (both
Verified pass, 552/552 tests as of `WP 5.3`) and **re-checks**
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
| All Work Packages (`WP 5.0A`–`WP 5.3`) meet their own Acceptance Criteria | **Complete** — `WP 5.0A`–`WP 5.0D`, `WP 5.0S`, `WP 5.1A`, `WP 5.1B`, `WP 5.2`, and `WP 5.3` all complete; release not yet cut (tagging is a separate, explicit Product Approval decision, §7) |
| `CHANGELOG.md` reflects every landed change | Not Yet Applicable — no `v0.5.0` entry started |
| `docs/releases/v0.5.0.md` release notes written | Not Yet Applicable — release not yet cut |
| `VERSION` updated to `0.5.0` | Not Yet Applicable — `VERSION` file still reads `0.4.0` (Verified directly) |

**Reason (Not Yet Applicable rows).** `v0.5.0` has not been released;
these gates are release-tagging gates, checked once, at the point of
tagging, not at every Work Package. **Review Trigger.** The Work Package
that proposes cutting the `v0.5.0` release tag.

**Superseded, `WP 16.2A`.** `v0.5.0` was released after this section was
written; its "Not Yet Applicable" rows are stale and retained verbatim
as the historical snapshot they are, per this register's own disclosure
convention. `v0.5.0`'s own real release facts, and every release
through `v0.15.0`, are in the summary table immediately below.

## Release-Level Gates Summary — `v0.5.0` through `v0.15.0` (`WP 16.2A`)

The per-release, per-gate tables above (`v0.4.0`, `v0.5.0`) are this
register's own original, detailed format — one table per release, one
row per gate. `WP 16.2A` extends coverage through `v0.15.0` as a single
summary table rather than ten more full detailed tables, a disclosed
simplification: reconstructing full per-gate detail for nine already-
released versions is a separate, substantial undertaking outside this
Work Package's own scope (register **currency**, not a historical
audit). `Release Register.md` is the fuller, corrected authority for
per-release facts (tag, merge commit, CI run, certification); this
table cross-checks against it directly.

| Release | Tag → Commit | Merged to `main` | Test Gate at release | Certified (§9 verdict) |
|---|---|---|---|---|
| `v0.6.0` | `v0.6.0` → `99ed285` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded (pre-dates the `ADR-0106` ERR process) |
| `v0.7.0` | `v0.7.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.8.0` | `v0.8.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.9.0` | `v0.9.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.10.0` | `v0.10.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.11.0` | `v0.11.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.12.0` | `v0.12.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.13.0` / `v0.13.1` | `v0.13.0`, `v0.13.1` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.14.0` | `v0.14.0` | Yes | Not re-verified by `WP 16.2A` — see `Release Register.md` | Not recorded |
| `v0.15.0` | `v0.15.0` → `a35365a` | Yes (`350922d` + follow-up `a35365a`) | **Verified** — 3,088/3,088 Core, 408/408 Desktop, 0 failures, both configurations (`Release Notes.md` §"Test Results") | **No** — GitHub Release `382812261` published without a §9 Product Approval verdict recorded in `Release Register.md`; see that register's own `v0.15.0` row, corrected `WP 16.2A` |

Every "Yes" in the "Merged to `main`" column is `git tag --merged main`
against this Work Package's own base branch — every listed tag is
reachable from `main`, confirmed directly. Per-gate detail (Build Gate
0/0, exact Test Gate figures, documentation gates) for `v0.6.0`–`v0.14.0`
is not reconstructed here; each release's own `docs/releases/vX.Y.0/`
folder (`WorkPackages.md`, Release Notes where one exists) remains the
authoritative source until a future Work Package backfills this
register's own detailed format.

## Cross-Reference Check

Every per-Work-Package gate above is cross-checked against
`ReleaseChecklist.md`'s own checklist item-for-item — no gate exists in
one document but not the other. The Test Gate's 552/552 figure is
cross-checked directly against `Test Register.md`'s own total, and
against `Repository Metrics Register.md`'s WP 5.3 snapshot — all
consistent.

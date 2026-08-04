# WP 8.9.0 — Release Preparation & Product Baseline — Release Checklist

## Purpose

A quick-reference checklist confirming every Definition of Done item
this Work Package's own controlling instruction named is complete.
Each line links to the document containing the full evidence — this
checklist does not re-argue any finding, only confirms it was checked.

## Checklist

- [x] **Repository verified.** Working tree clean (`docs/First_run/`
      excepted, untracked, out of scope); 9 clean commits, one per Work
      Package, zero merges/empties/WIP markers; no accidental debug
      artefacts found in this release's own changes. See Release
      Readiness Report §1.
- [x] **Builds clean.** 4/4 projects, both Debug and Release, 0
      warnings, 0 errors, from a fully clean rebuild — confirmed both
      per-project and via `src/TempestOS.slnx` (the exact path
      `scripts/new-release.ps1` uses). See Release Readiness Report §2.
- [x] **Test suite passes repeatedly.** 1631/1631, four full-suite runs
      (2 Debug, 2 Release) plus a fifth via the release script's own
      solution-file path, plus a dedicated 225-test scoped run, plus
      three targeted flaky-test probes — zero failures, zero flakes
      anywhere. See Release Readiness Report §3.
- [x] **Version verified.** `VERSION` correctly still reads `0.7.0`;
      `Directory.Build.props` derives, never duplicates; compiled
      assembly version matches exactly. See Release Readiness Report §4.
- [x] **Governance internally consistent.** All ten named registers
      audited; every "Total" line independently re-verified against
      direct `grep`/`find` output. Three genuine findings disclosed (one
      corrected, two deliberately left open as pre-existing/out-of-scope).
      See Release Readiness Report §7.
- [x] **Architecture audit complete.** Zero drift found against any of
      the nine `WP 8.0A`–`WP 8.2C` decisions checked; zero circular
      dependencies; zero layering violations. See Release Readiness
      Report §8 and Architecture Baseline Summary.
- [x] **Documentation complete.** `WorkPackages.md`, `ReleaseNotes.md`,
      `Retrospective.md` — all three found as stale skeletons at the
      start of this review, all three now fully populated. See Release
      Readiness Report §5.
- [x] **Release Notes complete.** `docs/releases/v0.8.0/ReleaseNotes.md`
      — executive summary, major capabilities, architecture highlights,
      key ADRs, statistics, known limitations, deferred work, technical
      debt summary, future roadmap, breaking changes (none).
- [x] **Product Approval Report complete.** Recommendation:
      **APPROVED** — see `WP8.9.0 Product Approval Report.md`.
- [x] **Merge readiness confirmed.** `feature/v0.8.0-engineering-workspace`
      is ready to merge into `main`; no outstanding release blockers; see
      Merge Readiness, below, and `WP8.9.0 Product Owner Release
      Checklist.md` for the prepared (not executed) command sequence.
- [x] **Release Checklist complete.** This document.
- [x] **Product Owner has everything required to perform the release
      manually.** See `WP8.9.0 Product Owner Release Checklist.md`.

## Merge Readiness

| Check | Result |
|---|---|
| Source branch | `feature/v0.8.0-engineering-workspace` (current `HEAD`, `89590eb` at the start of this Work Package) |
| Target branch | `main` (`61fb2db`, the `v0.7.0` merge commit) |
| Working tree | Clean |
| Outstanding release blockers | **None** |
| Recommended merge strategy | Non-fast-forward (`git merge --no-ff`), matching every prior release's own precedent (`v0.4.0` through `v0.7.0`, each a distinct, visible merge commit in `main`'s own history) |
| Expected merge result | A new merge commit on `main`, parenting both `61fb2db` and this branch's own final commit (`WP 8.9.0`'s own, once committed) |
| Expected release tag | `v0.8.0`, annotated, created on `main` after the merge, per `scripts/new-release.ps1`'s own validation (`Releases may only be created from 'main'`) |

## Related Documents

`docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md`;
`docs/releases/v0.8.0/WP8.9.0 Product Approval Report.md`;
`docs/releases/v0.8.0/WP8.9.0 Product Owner Release Checklist.md`;
`docs/releases/v0.8.0/ReleaseNotes.md`.

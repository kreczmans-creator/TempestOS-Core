# WP 9.9.1 — Final Release Checklist

**Work Package:** `WP 9.9.1` — Product Owner Release Execution
**Date:** 2026-08-07
**Use:** Product Owner verification before executing the remaining
(remote) release steps, and a record of what this Work Package itself
confirmed.

---

## A. Pre-conditions (confirmed complete before this Work Package began)

- [x] `WP 9.9.0` (First Pass) recommendation: **APPROVED**.
- [x] `WP 9.8B` closed the one disclosed gap the First Pass found
      (Platform Service Register/Map backfill).
- [x] `WP 9.9.0` (Second Pass) independently re-confirmed the closure
      and reconfirmed the recommendation: **APPROVED**, with one new,
      disclosed, non-release-blocking finding (`TD-34`).
- [x] All eight required `v0.9.0` release deliverables produced
      (Release Readiness Report, Engineering Statistics Report,
      Architecture Baseline Summary, Engineering Capability Summary,
      Product Approval Report, Release Notes, Retrospective, Academy
      Retrospective) — each pass's own set, both disclosed.
- [x] All governance registers up to date as of `WP 9.9.0` Second
      Pass's own final edits.

## B. Release mechanics performed by this Work Package

- [x] `VERSION` updated: `0.8.0` → `0.9.0`.
- [x] Working tree inspected before staging — confirmed 101 pending
      changes were all expected release content, zero stray files.
- [x] Pre-commit sanity build: `dotnet build src/TempestOS.slnx -c
      Debug` → 0 Warnings, 0 Errors.
- [x] All release changes staged (`git add -A`): 161 files (146 added,
      15 modified), zero unstaged changes remaining after staging.
- [x] Single release commit created: `Release: TempestOS v0.9.0`.
- [x] Commit identity verified against repository convention; a
      genuine auto-detection mismatch was found and corrected (see
      Product Owner Release Summary, §4) via `git commit --amend
      --reset-author --no-edit` before proceeding.
- [x] Merge into `main` assessed: **not required** — no feature branch
      exists; work is already on `main` directly. Disclosed, not
      silently reconciled with `PROJECT_STATUS.md`'s own historical
      narrative (see Product Owner Release Summary, §3).
- [x] Annotated tag `v0.9.0` created, message `TempestOS v0.9.0 -
      Engineering Domain Completion`, mirroring the `v0.8.0` tag's own
      format.
- [x] Tag target verified equal to the release commit
      (`9f258f16e07b89f6030d1bdcd90b799527fdcb8c`).
- [x] Post-tag repository state verified: working tree clean, `main`
      is the only branch, `VERSION` reads `0.9.0`, local `main` is 1
      commit ahead of `origin/main` and 0 behind.

## C. Explicit exclusions honored

- [x] **Not pushed.** No `git push` of any kind was run.
- [x] **No GitHub Release published.**
- [x] **No branches deleted** (none existed).
- [x] **No implementation changes** — zero files under `src/` or
      `tests/` were authored or edited by this Work Package.
- [x] **No architecture changes.**
- [x] **No governance changes** — `PROJECT_STATUS.md` and every
      governance register are unchanged by this Work Package (last
      touched by `WP 9.9.0` Second Pass).

## D. What the Product Owner should verify before pushing

Recommended checks, all of which this Work Package's own transcript
already supports but the Product Owner may wish to re-run directly:

- [ ] `git log -1 --format="%H %an <%ae> %s"` shows the expected
      commit hash, `kreczmans-creator <kreczmans@gmail.com>`, and
      subject `Release: TempestOS v0.9.0`.
- [ ] `git show v0.9.0 --no-patch` shows an annotated tag pointing at
      that same commit.
- [ ] `git status` shows `nothing to commit, working tree clean` and
      `Your branch is ahead of 'origin/main' by 1 commit`.
- [ ] `cat VERSION` reads `0.9.0`.
- [ ] Optionally, re-run `dotnet build` / `dotnet test` locally one
      more time immediately before pushing, as a final gate — this
      Work Package performed a build check only (release mechanics
      scope), not a full test-suite re-run (that was `WP 9.9.0` Second
      Pass's own responsibility, already discharged).

## E. Remaining steps (Product Owner execution — outside this Work Package's scope)

```
git push origin main
git push origin v0.9.0
```

Then, if desired: create the GitHub Release for tag `v0.9.0`,
referencing `docs/releases/v0.9.0/ReleaseNotes.md`.

---

**This Work Package stops here.** No remote operation has been
performed. The repository is prepared and awaits Product Owner
execution of the steps in §E.

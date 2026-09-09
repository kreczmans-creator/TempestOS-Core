# WP 9.1B — Development Baseline Consolidation — Product Owner Merge Checklist

## Purpose

The exact, prepared command sequence to merge
`feature/v0.9.0-mechanical-foundation` into `main`, establishing a clean
development baseline on `main` before continuing work on a different
workstation. **This is not a release** — no `VERSION` change, no tag, no
GitHub Release. Every command below is **prepared, not executed** — this
Work Package has run none of them. Only the Product Owner performs
this sequence.

## Prerequisites (Already Satisfied)

- [x] All `WP 9.0A`/`WP 9.0B`/`WP 9.1A` work is committed —
      `feature/v0.9.0-mechanical-foundation` at commit `71b49ea`, plus
      this Work Package's own four deliverables in one further commit on
      top (run `git rev-parse feature/v0.9.0-mechanical-foundation` for
      its exact hash — it was amended once after its own first draft to
      correct a self-reference, so is not quoted here).
- [x] Working tree clean except pre-existing, unrelated
      `docs/First_run/` (not part of this consolidation — see `WP9.1B
      Development Baseline Report.md`).
- [x] Build and test verification both pass, both configurations
      (1808/1808 tests, 0 warnings/errors).
- [x] `WP9.1B Merge Readiness Report.md` confirms zero expected
      conflicts, directly verified via `git merge-tree`.

## Step 1 — Commit Any Outstanding Work

Already done — `71b49ea` (`WP 9.0A`/`WP 9.0B`/`WP 9.1A`) plus one further
commit (`WP 9.1B`'s own deliverables) on
`feature/v0.9.0-mechanical-foundation` carry every change. Confirm
before proceeding:

```
git status --short
```

Expected output: nothing, or only `?? docs/First_run/` (pre-existing,
unrelated — leave it). If anything else appears, stop and investigate
before continuing — it means something changed after this checklist was
prepared.

## Step 2 — Checkout `main`

```
git checkout main
```

## Step 3 — Merge the Feature Branch

Non-fast-forward, matching every prior release's own precedent (`v0.4.0`
through `v0.8.0`):

```
git merge --no-ff feature/v0.9.0-mechanical-foundation -m "Merge branch 'feature/v0.9.0-mechanical-foundation' into main

Development baseline consolidation (WP 9.1B) - WP 9.0A (Mechanical
Product Structure), WP 9.0B (Product Configuration & BOM Management),
WP 9.1A (Requirements Management Workspace). Not a release: VERSION
unchanged at 0.8.0, no tag, no GitHub Release.

1808/1808 tests passing, 0 build warnings/errors (Debug and Release).
See docs/releases/v0.9.0/WP9.1B Development Baseline Report.md,
WP9.1B Commit Summary.md, and WP9.1B Merge Readiness Report.md for the
complete account."
```

**Expected result**: a new merge commit on `main`, parenting both
`183d2ef` (the `v0.8.0` tag) and the feature branch's own current tip.
Zero conflicts expected — see `WP9.1B Merge
Readiness Report.md`'s own direct `git merge-tree` verification.

## Step 4 — If (Unexpectedly) a Conflict Occurs

Not expected — `WP9.1B Merge Readiness Report.md` verified zero
conflicts directly against the actual current state of both branches.
If `main` has moved since that verification was performed, re-run it
before merging:

```
git merge-tree $(git merge-base main feature/v0.9.0-mechanical-foundation) main feature/v0.9.0-mechanical-foundation
```

If a real conflict does appear during Step 3, resolve each conflicted
file directly, then:

```
git add <resolved files>
git commit
```

(Do not use `git merge --abort` and retry with different flags unless
the conflict resolution itself is wrong — the `--no-ff` merge strategy
above is correct and should not need to change.)

## Step 5 — Push `main`

```
git push origin main
```

## Explicitly NOT Part of This Checklist

- **No `VERSION` change** — remains `0.8.0` on `main` after the merge.
- **No Git tag** — no `git tag` command anywhere above.
- **No GitHub Release** — no `gh release create` anywhere above.
- **No feature branch deletion** — `feature/v0.9.0-mechanical-foundation`
  is retained, per this project's own "feature branches are never
  deleted" convention.
- **No feature branch push** — only `main` is pushed in Step 5; the
  feature branch itself is not pushed unless the Product Owner has an
  independent reason to (for example, working from it on a second
  workstation before the next Work Package begins — in which case `git
  push origin feature/v0.9.0-mechanical-foundation` is a safe, additive
  push with no bearing on anything above).

## Verification After Each Step

- After Step 3: confirm `git log -1 --format="%P" main` shows two
  parent hashes (a true merge, not a fast-forward).
- After Step 3: confirm `cat VERSION` still reads `0.8.0`.
- After Step 5: confirm `git ls-remote origin main` matches `git
  rev-parse main` locally.

## After This Checklist

`v0.9.0` ("Mechanical Foundation") remains an in-progress, unreleased
phase — `main` now simply carries every Work Package completed so far
against it. No further Work Package begins until the Product Owner
gives further instruction, per `WP 9.1A`'s own explicit closing
instruction (unchanged by this consolidation).

## Related Documents

`WP9.1B Development Baseline Report.md`; `WP9.1B Commit Summary.md`;
`WP9.1B Merge Readiness Report.md`.

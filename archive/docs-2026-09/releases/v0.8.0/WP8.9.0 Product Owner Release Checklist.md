# WP 8.9.0 — Release Preparation & Product Baseline — Product Owner Release Checklist

## Purpose

The exact, prepared sequence of actions the Product Owner alone
performs to release `v0.8.0`, per this Work Package's own explicit
constraint: "The Product Owner alone shall perform the physical Git
merge, version bump, tag creation, GitHub Release and publication after
approval." Every command below is **prepared, not executed** — this
Work Package has run none of them.

## Prerequisites (Already Satisfied)

- [x] `WP8.9.0 Product Approval Report.md` recommends **APPROVED**.
- [x] `feature/v0.8.0-engineering-workspace` working tree is clean.
- [x] Build and test verification both pass, both configurations, both
      the per-project and `src/TempestOS.slnx` paths.

## Step 1 — Merge to `main`

Non-fast-forward, matching every prior release's own precedent
(`v0.4.0` through `v0.7.0`):

```
git checkout main
git merge --no-ff feature/v0.8.0-engineering-workspace -m "Merge branch 'feature/v0.8.0-engineering-workspace' into main

v0.8.0 (\"Engineering Workspace\") - nine Work Packages (WP 8.0A-8.2C):
the Engineering Workspace (architecture, contracts, shell,
navigation/project explorer, engineering cockpit) and the Engineering
Domain (architecture, contracts, implementation), closed by WP 8.9.0's
own release preparation review, which recommended APPROVED.

1631/1631 tests passing, 0 build warnings/errors (Debug and Release),
zero release-blocking findings. See docs/releases/v0.8.0/ReleaseNotes.md
and WP8.9.0 Product Approval Report.md for the complete account."
```

**Expected result**: a new merge commit on `main`, parenting both
`61fb2db` (the `v0.7.0` merge) and `feature/v0.8.0-engineering-workspace`'s
own final commit.

## Step 2 — Bump `VERSION`

On `main`, after the merge — never before, per this project's own
established "bump after tag" precedent:

```
echo 0.8.0 > VERSION
git add VERSION
git commit -m "v0.8.0: bump VERSION for release

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

## Step 3 — Build and Test via the Release Script

`scripts/new-release.ps1` performs its own branch/clean/`VERSION`
validation, then rebuilds and retests via `src/TempestOS.slnx`:

```
pwsh scripts/new-release.ps1 -Version 0.8.0
```

This creates the annotated tag (`v0.8.0`) as its own last step but does
**not** push unless `-Push` is also supplied — see Step 4.

## Step 4 — Tag (If Not Already Created by Step 3)

If tagging manually instead of via the script:

```
git tag -a v0.8.0 -m "TempestOS v0.8.0"
```

## Step 5 — Push

```
git push origin main
git push origin v0.8.0
```

(Equivalent to re-running Step 3 with `-Push`.)

## Step 6 — GitHub Release

`gh` CLI was unavailable in this environment throughout this Work
Package (the same disclosed limitation `v0.6.0`/`v0.7.0` each carried
forward) — prepared as the manual web-UI alternative, or via `gh` if
available to the Product Owner:

```
gh release create v0.8.0 \
  --title "TempestOS v0.8.0 — Engineering Workspace" \
  --notes-file docs/releases/v0.8.0/ReleaseNotes.md
```

## Step 7 — Prepare the Next Branch (Optional, Deferred)

Mirroring `v0.6.0`→`v0.7.0`'s own precedent (`18e61d5`) — cutting the
next feature branch and re-bumping `VERSION` forward is itself a
distinct, separate act of preparation, not part of closing `v0.8.0`
itself, and is **not** prepared here since Programme 9's own scope is
not yet named. Per this Work Package's own explicit closing instruction,
no further Work Package — including this step — begins until the
Product Owner gives further instruction.

## Verification After Each Step

- After Step 1: confirm `git log -1 --format="%P" main` shows two
  parent hashes (a true merge, not a fast-forward).
- After Step 2: confirm `cat VERSION` reads `0.8.0`.
- After Step 3: confirm the script's own final `RELEASE SUCCESSFUL`
  banner, and `git tag -l v0.8.0` shows the new tag.
- After Step 5: confirm `git ls-remote --tags origin` lists `v0.8.0`.

## Related Documents

`docs/releases/v0.8.0/WP8.9.0 Release Checklist.md`; `docs/releases/v0.8.0/
WP8.9.0 Product Approval Report.md`; `docs/releases/v0.8.0/ReleaseNotes.md`;
`scripts/new-release.ps1`.

# TempestOS v0.25.1 — Release Notes

A patch on `v0.25.0`.

## Fixed

- **The first-run "Where should your data live?" dialog no longer hangs (`C-01`).**
  - **Cause:** startup waited for the dialog by blocking the UI thread, so the dialog was drawn as an empty window and never took a click. On Windows it sometimes got through anyway, which is why it hung from the Start menu but not always from a shortcut.
  - **Fix:** startup now runs a nested dispatcher loop until the dialog closes, and closing the dialog no longer counts as closing the application's last window.
  - **Verified:** under Xvfb, the dialog draws, **Continue** works, and TempestOS opens on Home with its data in the chosen folder.
  - Code: `App.cs`.
  - The `--persistence-root` shortcut workaround from the `v0.25.0` runbook (step A2) is no longer needed.

## Changed

- **A release is tested once (`ADR-0164`).** CI no longer re-runs on the tag push, and `release.yml` no longer repeats the whole test suite (22 min 53 s for `v0.25.0`). A release still publishes only on a green `CI Gate` for its commit (`ADR-0160`). Tag only a commit CI has already run on: anything merged to `main` or pushed to `release/**`.
- `DisciplineWorkspaceTests` dropped from 31 s to 3 s. Only its restart test still boots the full composition.

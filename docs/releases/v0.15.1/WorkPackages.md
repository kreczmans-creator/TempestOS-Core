# v0.15.1 — Work Packages

## Status

**Not a release.** This folder holds `WP 15.2A`'s own documentation only.
`VERSION` remains `0.15.0`; no tag, no `Release Notes.md`, and no
release decision has been made here — see `WP 15.2A`'s own
Implementation Report §5 for why. A future release-preparation Work
Package may assign this (or further) work a real version, exactly as
`WP 15.1A` did for the `v0.14.0..main` backlog before it.

## Scope of This Document

One Work Package so far, identified by independently inspecting the
repository at `main` (`5165e3f`, `v0.15.0` released and published) —
not assumed from any prior roadmap.

## Work Packages

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 15.2A` | Desktop Test Suite Persistence Root Cleanup — closes `TD-120`. Every isolated persistence root `WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath()` returns now lives under one shared, per-test-run parent directory, deleted once by a new `ICollectionFixture` when the "Tempest.Desktop WorkspaceHost persistence" collection finishes, instead of accumulating one directory per test forever. `ResponsiveWorkspaceTests` — the one class among 40+ call sites missing the collection attribute entirely — joined it, closing an adjacent, unserialised-parallel-execution hazard the same collection exists to prevent. Verified empirically: 6,786 pre-existing stray directories in this session's own `/tmp` were left untouched (this fix does not retroactively clean historical debris); a full 412-test run after the fix left zero new ones. Debug and Release builds 0 Warnings/0 Errors; `Tempest.Desktop.Tests` 412/412 (408 + 4 new), `Tempest.Core.Tests` 3088/3088, unaffected by design. Test infrastructure only — zero `src/` files touched, no ADR, no architecture change. See `WP15.2A Desktop Test Suite Persistence Root Cleanup — Implementation Report.md`. | Implementation | **Complete** |

## Related Documents

`docs/releases/v0.15.1/WP15.2A Desktop Test Suite Persistence Root
Cleanup — Implementation Report.md`; `docs/governance/Quality/Technical
Debt Register.md` (`TD-120`); `docs/releases/v0.15.0/WorkPackages.md`
(the immediately preceding, closed release); `docs/academy/03 Work
Packages/WP15.2A-desktop-test-suite-persistence-root-cleanup.md`;
`PROJECT_STATUS.md`.

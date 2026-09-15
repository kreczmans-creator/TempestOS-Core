# TempestOS v0.21.0 — Release Notes

**Status: in preparation on `release/v0.21.0`, cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — the docking rewrite to `ADR-0153`,
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration, a
real-shell run in CI, and the mutation threshold met. See
`Execution Plan.md` for the packages and their waves.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.5A` (`WP RC.0A`, brought forward) | Velopack-packaged Windows installer (`TempestOS-<tag>-Setup.exe`) with in-place update, off by default until enabled in Settings → Updates; an installed run's default persistence root (`%LOCALAPPDATA%\TempestOS\persistence-data`), a first-run location dialog, and `--persistence-root`/`Persistence:RootPath` overrides, closing `TD-36` for the installed case; a pre-migration backup (`BackupService`, the online SQLite backup API) fired automatically when a launch finds an older schema version, and Settings → Data's own "Back up now…"/"Restore from backup…"; the support matrix in `PHYSICAL_REVIEW.md` §2a. Adds **Velopack 1.2.0 (MIT)** — see `THIRD-PARTY-NOTICES.md` — referenced only by `Tempest.Desktop`. | *(pending)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`

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
| `WP 21.5E` | Security review of every surface added since the `v0.5.0` baseline, widened mid-review (Product Owner) to the whole live `src/` tree: **1 RED, 1 AMBER, 11 GREEN** findings (`docs/security/Security Posture.md`; RED filed as `TD-184`, owned by `WP 21.4A`'s own files and applied at its merge; AMBER filed as `TD-185`). `docs/security/Security Posture.md` (`WP RC.0C`, brought forward) replaces `Threat Model.md`/`Security Roadmap.md` for `v1.0` (both kept, pointer added). Dependency vulnerability scanning is now a required `ci.yml` check (`dependency-scan`, parsed by `scripts/check-vulnerable-packages.ps1`); `.github/dependabot.yml` added; `THIRD-PARTY-NOTICES.md` added (20 direct packages, all MIT or Apache-2.0). Proved, with a test, that the frozen REST API/plugin-loading/licensing layers (`src/Frozen/`) are unreachable in a default build and configuration. | *(pending)* |
| `WP 21.5A` (`WP RC.0A`, brought forward) | Velopack-packaged Windows installer (`TempestOS-<tag>-Setup.exe`) with in-place update, off by default until enabled in Settings → Updates; an installed run's default persistence root (`%LOCALAPPDATA%\TempestOS\persistence-data`), a first-run location dialog, and `--persistence-root`/`Persistence:RootPath` overrides, closing `TD-36` for the installed case; a pre-migration backup (`BackupService`, the online SQLite backup API) fired automatically when a launch finds an older schema version, and Settings → Data's own "Back up now…"/"Restore from backup…"; the support matrix in `PHYSICAL_REVIEW.md` §2a. Adds **Velopack 1.2.0 (MIT)** — see `THIRD-PARTY-NOTICES.md` — referenced only by `Tempest.Desktop`. | *(pending)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`

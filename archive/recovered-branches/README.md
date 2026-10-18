# Recovered branches (2026-10-01)

Each `.bundle` is a branch that was pushed by an earlier session and later
deleted from GitHub without being merged. They were recovered from the
orphaned commits on 2026-10-01 after the PO asked for a sweep for lost work.
Each bundle holds only the commits not already in `main`, so `main` must be
present to unpack it.

Restore one as a branch:

    git fetch archive/recovered-branches/<file>.bundle '+refs/recovered/*:refs/heads/recovered/*'

| Bundle | Tip | Date | Session | What it holds | Status in `main` |
|---|---|---|---|---|---|
| `claude-tempestos-companion-mobile-ubznt3.bundle` | `0fcb279` | 2026-08-29 | 0146BrfR "TempestOS Companion mobile application" | The Avalonia Companion app: `src/Tempest.Companion*` (core, contracts, Desktop, Android and iOS heads), its tests, the Core `Api/ApiQuery*` REST query surface, original ADR-0113 to 0117, WP 14.0A–14.2.1 reports, security review, `mobile-heads.yml` | **Lost.** About 75 files never reached `main`; BACKLOG TD-82 records the companion as having no implementation. ADR-0113 to 0117 were later reused for other decisions. |
| `feature-v0.16.0-integration.bundle` | `3600f9b` | 2026-09-07 | 01QiCZrZ "A4 Bearing Library" | v0.16.0 integration, 135 commits | **Partial.** About 34 files never reached `main`: the five-calculator RC suite (BeamBending, BearingLife, Thermal, ThreadedJoint, ToleranceStack) with tests, the Engineering Calculations workspace view, `ThermalResistance` units, 12 v0.16.0 reports. `main` later built a different 11-module suite (WP 21.7A); ToleranceStack and ThermalResistance have no equivalent. |
| `integration-v0.16.0-review.bundle` | `a482afc` | 2026-09-07 | 013XNN8x "V1.0.0 release candidate audit" | v0.16.0 review integration, 80 commits | **Partial.** About 10 files: the App-level BracketCheck workspace, `ReferenceDataBootstrapper`, `BracketSectionCheckView` and tests (Core `BracketSectionCheck` is in `main`). |
| `claude-tempestos-roadmap-a-g-swenlf.bundle` | `d3660dd` | 2026-09-05 | 01H7FD5u "TempestOS parallel work roadmap A–G" | `knowledge-foundation/` (also extracted at `archive/knowledge-foundation-2026-09-05/`), 8 `docs/roadmap/Parallel Programme A–G` docs, `docs/data/… Field Definitions.md` | **Partial.** Data extracted to the archive; the roadmap docs exist only in this bundle. |
| `claude-plugins-marketplace-ecc-y9m05v.bundle` | `e6bff9e` | 2026-09-08 | 01Q9bMiN "Plugins marketplace ECC addition" | `.claude/settings.json` (+13 lines) marketplace entry, `.gitignore` | Lost, trivial. |

Checked and fully present in `main` by another route: the Stage 3
descriptor binding branch, the force-pushed `release/v0.20.0`, the CI
investigation branch, the A3 technical-debt register, the v0.16.0 scope and
WP 16.0B/16.4A branches, and the pre-2026-08-11 `main` history (still held by
tags v0.3.0 to v0.10.0). Tempest-Dashboard: nothing lost. All 12 closed PRs
were merged.

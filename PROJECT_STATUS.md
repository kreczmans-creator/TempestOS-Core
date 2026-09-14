# TempestOS — Project Status

**Branch:** `release/v0.18.0` (the one branch for every v0.18.0 Work Package, per the Product Owner's instruction of 2026-09-09; nothing is merged to `main` until the release is accepted)
**VERSION:** `0.18.0` (bumped at `WP 18.9.0` on the release candidate; `v0.17.0` was tagged from `main` on 2026-09-09)

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do. None has landed
yet — the programme that builds them starts with this release:

1. ○ Open a project for a client, with a PO reference, a budget and a
   pinned rate card. — target `v0.19.0` (`WP 19.0A`)
2. ● Record a calculation done in the engineer's own tool as evidence
   on the project: its files, subject, cited reference records at the
   revision held, and key figures, as one immutable record. — target
   `v0.18.0` (`WP 18.0A`, `WP 18.2A`; `D-028`)
3. ● Have a second principal independently check that evidence and
   issue it with an issue sheet attached to the project. — target
   `v0.18.0` (`WP 18.2B`)
4. ○ Record time and, on marking a deliverable complete, emit an
   invoice request to Xero or QuickBooks. — target `v0.19.0`
   (`WP 19.0A`, `WP 19.1A`)
5. ○ See utilisation, margin per project, work in progress and days
   sales outstanding on the Home cockpit. — target `v0.19.0`
   (`WP 19.1B`)

## Work in flight

`v0.18.0` Evidence and Check on `release/v0.18.0`, executed per
`docs/releases/v0.18.0/Execution Plan.md`. Merged and gated on
2026-09-09: `WP 18.0A`, `18.0B`, `18.0C`, `18.1A`, `18.1B`, `18.2A`,
`18.2B` part 1 (the issue-sheet renderer). In flight: `WP 18.2B` part 2
(Check and Issue in the editor, the sheet attached to the record,
supersession, subject retag, Libraries Revise), then `WP 18.9.0` (this
file's figures, the release notes, the physical review, the tag).
`v0.17.0` is released: merged to `main`, tagged, published 2026-09-09.

## Gate (the release-candidate head `2f4486c`, re-derived by `WP 18.9.0` on 2026-09-09)

- Core tests: 3,991 passed, 0 failed, 0 skipped, Debug and Release (981 archived with P02–P07 by `WP 18.0C`, file-backend-only tests deleted by `WP 18.1A`; Evidence, citation, seeding, search, sequence and snapshot tests added)
- Desktop tests: 532 passed, 0 failed, 0 skipped, Debug (6 m 22 s) and Release (5 m 32 s)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Architecture invariants (`DependencyDirectionTests`): 5/5 green
- Governance health check: 5/5 passed (Windows PowerShell 5.1 and PowerShell 7)
- CI: green on every pushed head of `release/v0.18.0` from the wave-1 merge onward (the opening commit failed only the Markdown-budget check, before any code landed)

## Where things are recorded now

- **`BACKLOG.md`** — the live technical-debt list a user could still notice.
- **`CONTRIBUTING.md`** — how a Work Package becomes a branch, a PR and a
  merge.
- **`PHYSICAL_REVIEW.md`** — the running-application checklist every
  release is verified against.
- **`docs/releases/v1.0.0/WorkPackages.md`** — the programme this and
  every following Work Package executes.

Everything this file used to carry — Work Package history, governance
narrative, superseded metrics — is archived, not deleted:
`archive/docs-2026-09/`.

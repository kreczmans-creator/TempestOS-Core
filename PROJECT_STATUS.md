# TempestOS — Project Status

**Branch:** `release/v0.19.0` (the one branch for every v0.19.0 Work Package, cut from `release/v0.18.0`'s candidate head `8df3466` on 2026-09-10; `release/v0.18.0` awaits the Product Owner's acceptance before its PR to `main`; nothing is merged to `main` until a release is accepted)
**VERSION:** `0.19.0` (bumped at `WP 19.9.0` on the release candidate; `v0.17.0` was tagged from `main` on 2026-09-09)

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do. All five have
landed on a release branch; two await the Product Owner's manual test of
`v0.18.0`, three that of `v0.19.0`:

1. ● Open a project for a client, with a PO reference, a budget and a
   pinned rate card. — `v0.19.0` (`WP 19.0A`, `ADR-0150`)
2. ● Record a calculation done in the engineer's own tool as evidence
   on the project: its files, subject, cited reference records at the
   revision held, and key figures, as one immutable record. —
   `v0.18.0` (`WP 18.0A`, `WP 18.2A`; `D-028`)
3. ● Have a second principal independently check that evidence and
   issue it with an issue sheet attached to the project. — `v0.18.0`
   (`WP 18.2B`)
4. ● Record time and, on marking a deliverable complete, emit an
   invoice request to Xero or QuickBooks (the Fake connector by
   default; the real connectors need a sandbox registration). —
   `v0.19.0` (`WP 19.0A`, `WP 19.1A`, `ADR-0151`)
5. ● See utilisation, margin per project, work in progress and days
   sales outstanding on the Home cockpit. — `v0.19.0` (`WP 19.1B`)

## Work in flight

`v0.19.0` Commercial Spine on `release/v0.19.0`, executed per
`docs/releases/v0.19.0/Execution Plan.md`. Merged and gated on
2026-09-10: `WP 19.0A`, `19.1A` (parts 1–3 and R1), `19.1B`, `19.2A`,
`19.2B`, `19.3A` (and R1); `WP 19.9.0` closed with three defects the
gate found (the one-live-request guard, the status-bar re-budget, two
opens told apart), VERSION 0.19.0, the release notes, `PHYSICAL_REVIEW.md`
§7b and the backlog reconciliation. `v0.18.0` Evidence and Check is a
release candidate on `release/v0.18.0` (head `8df3466`) under the Product
Owner's manual test; its PR to `main`, tag and GitHub Release follow
acceptance, then `v0.19.0`'s follow the same way. `v0.17.0` is released:
merged to `main`, tagged, published 2026-09-09.

## Gate (the merged head `86ea1c2`, re-derived by `WP 19.9.0` on 2026-09-10)

- Core tests: 4,255 passed, 0 failed, 0 skipped, Debug and Release (timesheets, deliverables, invoicing, connectors, reconciliation and KPI fixture tests added since 3,991)
- Desktop tests: 550 passed, 0 failed, 0 skipped, Debug (3 m 43 s) and Release (3 m 20 s)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Governance health check: 5/5 passed (Markdown 1,550 vs code 42,011 lines added since `origin/main`)
- CI: green on `4ce5146`; the runs on the candidate head are recorded in the release notes

## Gate as it stood for `v0.18.0` (the release-candidate head `2f4486c`, re-derived by `WP 18.9.0` on 2026-09-09)

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

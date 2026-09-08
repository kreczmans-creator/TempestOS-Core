# TempestOS — Project Status

**Branch:** `release/v0.17.0` (all v0.17.0 Work Packages land here; nothing is merged to `main` until the release is accepted)
**VERSION:** `0.17.0` (bumped by `WP 17.9.0`; not yet tagged or published — awaiting Windows verification and Product Approval)

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do. None has landed
yet — the programme that builds them starts with this release:

1. ○ Open a project for a client, with a PO reference, a budget and a
   pinned rate card. — target `v0.19.0` (`WP 19.0A`)
2. ○ Author a calculation sheet from named inputs, expressions and
   cited reference tables; run it; have it persisted as one immutable
   record. — target `v0.18.0` (`WP 18.0A`, `WP 18.2A`)
3. ○ Have a second principal independently check the sheet and issue
   it as a PDF calc sheet attached to the project. — target `v0.18.0`
   (`WP 18.2B`, `WP 18.3A`)
4. ○ Record time and, on marking a deliverable complete, emit an
   invoice request to Xero or QuickBooks. — target `v0.19.0`
   (`WP 19.0A`, `WP 19.1A`)
5. ○ See utilisation, margin per project, work in progress and days
   sales outstanding on the Home cockpit. — target `v0.19.0`
   (`WP 19.1B`)

## Work in flight

None. Every `v0.17.0` Work Package (`17.0A`, `17.0B`, `17.0C`, `17.1A`,
`17.1B`, `17.2A` parts 1 and 2, `17.2B`, `17.3A`) is merged to
`release/v0.17.0`. Next: Windows verification per `PHYSICAL_REVIEW.md`
§7, then merge to `main`, tag `v0.17.0`, publish. The next release is
`v0.18.0` Calculation as Document (`WP 18.0A` first).

## Gate (this release's own figures, filled in by `WP 17.9.0`)

- Core tests: 4,931 passed, 0 failed, 0 skipped (`WP 17.9.1` re-run: one Debug and one Release run after the hotfixes, on top of the three Debug and one Release runs at `WP 17.9.0`; 20–50 s each)
- Desktop tests: 498 passed, 0 failed, 0 skipped (same runs; about 2.5 min each)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Architecture invariants (`DependencyDirectionTests`): 5/5 green
- Governance health check: 5/5 passed (Windows PowerShell 5.1 and PowerShell 7)
- CI: not yet run — nothing pushed until Windows verification (see Release Notes, Warnings)

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

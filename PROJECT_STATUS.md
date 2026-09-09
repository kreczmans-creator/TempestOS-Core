# TempestOS — Project Status

**Branch:** `release/v0.17.0` (all v0.17.0 Work Packages land here; nothing is merged to `main` until the release is accepted)
**VERSION:** `0.17.0` (bumped by `WP 17.9.0`; accepted by the Product Owner 2026-09-09; tagged `v0.17.0` from `main`)

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do. None has landed
yet — the programme that builds them starts with this release:

1. ○ Open a project for a client, with a PO reference, a budget and a
   pinned rate card. — target `v0.19.0` (`WP 19.0A`)
2. ○ Record a calculation done in the engineer's own tool as evidence
   on the project: its files, subject, cited reference records at the
   revision held, and key figures, as one immutable record. — target
   `v0.18.0` (`WP 18.0A`, `WP 18.2A`; `D-028`)
3. ○ Have a second principal independently check that evidence and
   issue it with an issue sheet attached to the project. — target
   `v0.18.0` (`WP 18.2B`)
4. ○ Record time and, on marking a deliverable complete, emit an
   invoice request to Xero or QuickBooks. — target `v0.19.0`
   (`WP 19.0A`, `WP 19.1A`)
5. ○ See utilisation, margin per project, work in progress and days
   sales outstanding on the Home cockpit. — target `v0.19.0`
   (`WP 19.1B`)

## Work in flight

None. Every `v0.17.0` Work Package (`17.0A`, `17.0B`, `17.0C`, `17.1A`,
`17.1B`, `17.2A` parts 1 and 2, `17.2B`, `17.3A`) is merged to
`release/v0.17.0`, accepted by the Product Owner's Windows smoke tests
on 2026-09-09, merged to `main`, tagged `v0.17.0` and published. The
next release is
`v0.18.0` Evidence and Check (`D-028`; `WP 18.0A` first).

## Gate (this release's own figures, filled in by `WP 17.9.0`)

- Core tests: 4,961 passed, 0 failed, 0 skipped (`WP 17.9.3` re-run: one Debug and one Release run after the overnight round, on top of the runs at `WP 17.9.0`, `17.9.1` and `17.9.2`; 20–50 s each)
- Desktop tests: 508 passed, 0 failed, 0 skipped (same runs, plus one Debug and one Release run at `WP 17.9.4`; about 2.5 min each)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Architecture invariants (`DependencyDirectionTests`): 5/5 green
- Governance health check: 5/5 passed (Windows PowerShell 5.1 and PowerShell 7)
- CI: ran on the push of `release/v0.17.0` and its PR; `main` required `CI Gate` green to merge (see Release Notes, Warnings)

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

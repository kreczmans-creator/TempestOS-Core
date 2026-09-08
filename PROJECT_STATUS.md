# TempestOS — Project Status

**Branch:** `wp/17.0b-governance`, based on `release/v0.17.0` @ `eea8230`
**VERSION:** `0.16.0` (not yet bumped — `v0.17.0` is in progress, not released)

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

`WP 17.0B` — **Governance reset**: archiving Work Package retrospectives,
review-board dispositions and superseded registers; replacing this file
and adding `BACKLOG.md`/`CONTRIBUTING.md`; reducing the governance
health check to five source-derived checks. `WP 17.0A` (immediate
defects) landed first on this branch.

## Gate (this release's own figures, filled in by `WP 17.9.0`)

- Core tests: `<n>` passed, 0 failed, 0 skipped
- Desktop tests: `<n>` passed, 0 failed, 0 skipped
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Architecture invariants (`DependencyDirectionTests`): `<n>`/`<n>` green
- Governance health check: `<n>`/`<n>` passed

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

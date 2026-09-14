# TempestOS — Project Status

**Branch:** `release/v0.19.1` (the one branch for every v0.19.1 Work Package, cut from `release/v0.19.0`'s candidate head `947c50d` on 2026-09-14 after the Product Owner's first pass over it; `v0.19.1` supersedes `v0.19.0` for testing, and `release/v0.19.0` stays as it is, unreleased; nothing is merged to `main` until a release is accepted)
**VERSION:** `0.19.1` (bumped when the branch opened; `v0.18.0` was merged to `main`, tagged and published on 2026-09-14 after the Product Owner's acceptance; `v0.17.0` on 2026-09-09)

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do, plus the one the
Product Owner added on 2026-09-14. Two are released; the rest are on
the `v0.19.1` candidate under the Product Owner's manual test:

1. ● Open a project for a client, with a PO reference, a budget and a
   pinned rate card — and a quotation opened with the project that
   defines its requirements and deliverables when accepted. —
   `v0.19.0` (`WP 19.0A`, `ADR-0150`); `v0.19.1` (`WP 19.5A`, `19.5B`,
   `ADR-0152`)
2. ● Record a calculation done in the engineer's own tool as evidence
   on the project: its files, subject, cited reference records at the
   revision held, and key figures, as one immutable record. —
   **released** `v0.18.0` (`WP 18.0A`, `WP 18.2A`; `D-028`)
3. ● Have a second principal independently check that evidence and
   issue it with an issue sheet attached to the project. — **released**
   `v0.18.0` (`WP 18.2B`)
4. ● Record time and, on marking a deliverable complete, emit an
   invoice request to Xero or QuickBooks (the Fake connector by
   default; the real connectors need a sandbox registration). —
   `v0.19.0` (`WP 19.0A`, `WP 19.1A`, `ADR-0151`)
5. ● See the consultancy at a glance: task tiles and the commercial
   snapshot on Home, project status and a Gantt on Projects, receivable,
   payable and cash flow on Business, from read models over the
   project, quotation, invoicing and accounts data. — `v0.19.1`
   (`WP 19.5C`, `19.7B`, `19.8B`; the five KPI equations of `WP 19.1B`
   stay in the cockpit inside the Structure tab)
6. ● Work in the shell the Product Owner sketched: Home, Projects,
   Tasks, Engineering, Business, each a tree with a dashboard;
   Timesheets and Quotes under Business; a real editor for library
   records; files dropped onto any Attachments section; subscriptions
   and bills read from Xero. — `v0.19.1` (`WP 19.4A`, `19.4B`, `19.6A`,
   `19.7A`, `19.8B`)

## Work in flight

`v0.19.1` on `release/v0.19.1`, executed per
`docs/releases/v0.19.1/Execution Plan.md` against the Product Owner's
nine comments in `docs/releases/v0.19.1/Product Owner Comments.md`.
Merged and gated on 2026-09-14, in order: `WP 19.4A`, `19.4B`, `19.6A`,
`19.5A`, `19.8B`, `19.5C`, `19.5B`, `19.5D` (added: blank optional
parameters, the invocation contract over Quotations and Tasks), `19.7A`,
`19.7B`, `19.7C` (added: a view keeps reacting after it is shown again),
and `WP 19.9.1` (two idle-machine waits in the quotation journey, the
cockpit's card buttons named, `PHYSICAL_REVIEW.md` §7c, the backlog
reconciliation, the release notes, this file). It is a release
candidate under the Product Owner's manual test; its PR to `main`, tag
and GitHub Release follow acceptance, the way `v0.18.0` went.
`release/v0.19.0` (head `947c50d`) is superseded and stays unreleased.

## Gate (the candidate head `df2ebe8`, re-derived by `WP 19.9.1` on 2026-09-14)

- Core tests: 4,370 passed, 0 failed, 0 skipped, Debug and Release (quotation, lifecycle, status and tasks read models, accounts categorisation and readings, the invocation contract over Quotations and Tasks added since 4,255)
- Desktop tests: 594 passed, 0 failed, 0 skipped, Debug (9 m 57 s) and Release (9 m 18 s)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Governance health check: 5/5 passed
- CI: the CI Gate job green on every pushed head of the branch on 2026-09-14 (runs 34842170404 on `289b11c`, 34849599412 on `6c37f34`, 34866145341 on `fc67feb`, 34877649676 on `dfbd637`); the three runs on the candidate head itself are recorded on the candidate page

## Gate as it stood for `v0.19.0` (the candidate head `947c50d`, re-derived by `WP 19.9.0` on 2026-09-10)

- Core tests: 4,255 passed, 0 failed, 0 skipped, Debug and Release
- Desktop tests: 550 passed, 0 failed, 0 skipped, Debug (3 m 43 s) and Release (3 m 20 s)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Governance health check: 5/5 passed
- CI: the CI Gate job green three times on `947c50d` (runs 34533088379, 34537092971, 34543101821)

## Released

- `v0.18.0` Evidence and Check: PR merged to `main` (`3680257`), tag `v0.18.0`, GitHub Release published by run 34823372128 on 2026-09-14 with `TempestOS-v0.18.0.zip` and `TempestOS-v0.18.0-engineering-harness.zip`.
- `v0.17.0`: merged to `main`, tagged, published 2026-09-09.

## Where things are recorded now

- **`BACKLOG.md`** — the live technical-debt list a user could still notice (at its cap of 30 after the `v0.19.1` reconciliation).
- **`CONTRIBUTING.md`** — how a Work Package becomes a branch, a PR and a
  merge.
- **`PHYSICAL_REVIEW.md`** — the running-application checklist every
  release is verified against; §7c is the `v0.19.1` journey.
- **`docs/releases/v1.0.0/WorkPackages.md`** — the programme this and
  every following Work Package executes.

Everything this file used to carry — Work Package history, governance
narrative, superseded metrics — is archived, not deleted:
`archive/docs-2026-09/`.

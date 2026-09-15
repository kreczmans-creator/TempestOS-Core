# TempestOS — Project Status

**Branch:** `release/v0.20.0` (the debt tranche, cut from the `v0.19.1` candidate at `4f5ee83a` on 2026-09-15 at the Product Owner's instruction to close the P1–P3 technical debt before the release; it contains all of `release/v0.19.1`, whose final head `94998b9e` stays pushed, gated three times on CI, as the fallback candidate). `release/v0.19.0` (`947c50d`) is superseded and stays unreleased.
**VERSION:** `0.20.0` (bumped when the branch opened on 2026-09-15; `v0.18.0` was merged to `main`, tagged and published on 2026-09-14 after the Product Owner's acceptance; `v0.17.0` on 2026-09-09)

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

`v0.20.0` on `release/v0.20.0`, executed overnight on 2026-09-15 per
`docs/releases/v0.20.0/Execution Plan.md` as lead in the Product
Owner's absence, against the technical-debt rationalisation of
2026-09-14 (`docs/releases/v0.19.1/Technical Debt Rationalisation —
Part 1.md` and `Part 2.md`) and the Product Owner's seven decisions of
2026-09-15 (`docs/releases/v0.19.1/Product Owner Decisions
2026-09-15.md`). Twelve packages merged and gated, in order: `WP 20.3D`
(CI in shards), `20.0A` (`ADR-0153`, tear-out and dock everywhere —
*Proposed*, about 20 developer-days, for review), `20.1A1` (every
Requirements write reaches the change bus), `20.3A` (issue in one
transaction, export schema migrations, BOM units), `20.3B` (seven small
P3 closures), `20.2B` (DWG opens externally, rotation; SVG stopped
honestly), `20.1A2` (a business identifier is unique within its
project), `20.2C` (macros over real commands), `20.1C2` (index-first
rehydration; the lazy half's kill switch invoked, `TD-88` stays open),
`20.1B` (payment terms per client; a calculation is a task from
creation), `20.1C1` (attachments stored once by content hash, streamed
reads), `20.2A` (the object picker: Move and Copy for twelve commands,
`TD-115`'s three bindings, the contextual Palette); then `WP 20.9.0`
(the two CI defects on `94cbb5ba` — `ADR-0153`'s register row and a
bounded wait in `ProjectAreaAcceptanceTests` — the release notes,
`PHYSICAL_REVIEW.md` §7d, this file). The live backlog stands at 12 of
its cap of 30. It is a release candidate under the Product Owner's
manual test (§7c then §7d); its PR to `main`, tag and GitHub Release
follow acceptance, the way `v0.18.0` went. `v0.19.1` is the fallback
if this candidate is refused.

## Gate (the candidate head, re-derived by `WP 20.9.0` on 2026-09-15)

- Core tests: {{CORE_TESTS}} passed, 0 failed, 0 skipped, Debug and Release (from 4,455: payment terms and due dates, the calculation task, the identifier index, requirements on the change bus, transactional issue, export migrations, BOM units, macros, the attachment content store, the rehydration index, the fifteen picker bindings)
- Desktop tests: {{DESKTOP_TESTS}} passed, 0 failed, 0 skipped, Debug ({{DESKTOP_DEBUG_TIME}}) and Release ({{DESKTOP_RELEASE_TIME}})
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Governance health check: 5/5 passed
- CI: the sharded workflow (`WP 20.3D`, Core plus three Desktop shards per configuration, the job ceiling back at 45 minutes) — {{CI_LINE}}

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

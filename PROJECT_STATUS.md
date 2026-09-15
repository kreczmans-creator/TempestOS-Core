# TempestOS — Project Status

**Branch:** `release/v0.21.0` (the recovery tranche, cut from the `v0.20.0` candidate at `88311649` on 2026-09-15 at the Product Owner's instruction to close every technical weakness in the lead's assessment of that afternoon; it contains all of `release/v0.20.0` including the seven `WP 20.10` fixes from the Product Owner's test). Candidate: `b033651d` (the last code commit, the second `WP 21.7C` follow-up merge) plus the release documents committed on top.
**VERSION:** `0.21.0` (bumped when the branch opened on 2026-09-15; `v0.18.0` was merged to `main`, tagged and published on 2026-09-14 after the Product Owner's acceptance; `v0.17.0` on 2026-09-09; `v0.19.1` and `v0.20.0` remain candidates under manual test, both contained in this branch)

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do, plus the one the
Product Owner added on 2026-09-14. Two are released; the rest are on the `v0.21.0` candidate (all of
`v0.19.1` and `v0.20.0` merged in) for the Product Owner's manual test:

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

`v0.21.0` on `release/v0.21.0`, the recovery tranche, executed on
2026-09-15 from the afternoon into the night per
`docs/releases/v0.21.0/Execution Plan.md` as lead, from the Product
Owner's instruction to close every technical weakness in the lead's
assessment of that afternoon and the security rule stated the same day
(findings are fixed, never filed; the scope is the full live codebase).
Seventeen of twenty-two packages merged and gated, in order: the seven
`WP 20.10` fixes from the Product Owner's own test of `v0.20.0`; `21.5E`
(security posture, the dependency-scan job), `21.5A` (installer, backup,
restore), `21.3A` (typed intermediates, re-run, compare), `21.5F` (the
offensive audit: ten findings fixed with a proof-of-concept test each),
`21.5B` (lazy rehydration, the index idiom), `21.4A` (the viewer's
remaining formats and markup), `21.2B` (the Engineering Assets surfaces),
`21.7A` (eleven calculation modules), `21.2A` (documents from the
templates), `21.1A` (Undo across commands), `21.7B` (the Engineering
Calculators), `21.3B` (the commercial edges), `21.1B` (the editor split),
`21.0A` (docking steps 1–2 of `ADR-0153`), `21.7C` (the calculators
completed from the reference libraries), `21.6A` (the four audit
residuals fixed, Requirements undo), `21.5D` (the mutation threshold met,
scoped 89.5 %), then the two `21.7C` follow-ups from driving the real
application for the Product Owner's screenshots (a read-back
intermediate shown as its value, a renamed Calculate starting a new
calculation, constraint lines in the input's own unit, a humanised
comparison table); then `WP 21.9.0` (the release notes with thirteen
warnings, `PHYSICAL_REVIEW.md` §7j, the OAuth tests off the dynamic port
range, this file). The live backlog stands at 10 of its cap of 30. It is
a release candidate for the Product Owner's manual test (§7e–§7j on top
of §7c–§7d); its PR to `main`, tag and GitHub Release follow acceptance.
`v0.20.0` at `ec20a535` is the fallback. Owed and gated on the Product
Owner: `WP 21.0B`/`21.0C` (docking steps 3–4, after the `ADR-0153`
review), `WP 21.5C` (the real-shell CI run), `WP 21.6` (the first live
Xero authorisation) — 16.5 of the 94.5 planned days.

## Gate (the `v0.21.0` candidate: `b033651d` plus the release documents, re-derived by `WP 21.9.0` on 2026-09-15)

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors`
- Core tests: 5,317 passed, 0 failed, Debug and Release (`b033651d`)
- Desktop tests: 884 passed, 0 failed, Debug (9 m 16 s) and Release (8 m 9 s), both on `b033651d`
- Governance health check: 5 of 5 (`b033651d`)
- CI: the sharded workflow ran on every merge head tonight; on `76b90c77` (the 21.7C merge) fully green, on `4c393842` the one Debug core failure was the dynamic-port collision `904b0f81` fixed (Release core green on re-run); the three runs on this candidate head — the push run plus two dispatched — are recorded on the candidate page and in `PROJECT_STATUS.md` at acceptance, with the CI Gate job as the criterion

## Gate (the candidate head, re-derived by `WP 20.9.0` on 2026-09-15)

- Core tests: 4,666 passed, 0 failed, 0 skipped, Debug and Release (from 4,455: payment terms and due dates, the calculation task, the identifier index, requirements on the change bus, transactional issue, export migrations, BOM units, macros, the attachment content store, the rehydration index, the fifteen picker bindings)
- Desktop tests: 660 passed, 0 failed, 0 skipped, Debug (14 m 2 s) and Release (13 m 5 s)
- Build: 0 warnings, 0 errors, both configurations, `TreatWarningsAsErrors`
- Governance health check: 5/5 passed
- CI: the sharded workflow (`WP 20.3D`, Core plus three Desktop shards per configuration, the job ceiling back at 45 minutes) — the CI Gate job green on `4d06cf19` (run 34916910336, about 16 minutes wall-clock, the shards 5 to 14 minutes) once the two defects were fixed; the three runs on the candidate head itself are recorded on the candidate page, as for `v0.19.1`

## Gate (the candidate head `033ac18`, re-derived after the overnight tranche on 2026-09-15)

- Core tests: 4,455 passed, 0 failed, 0 skipped, Debug and Release (quotation, lifecycle, status and tasks read models, accounts categorisation and readings, the invocation contract over Quotations and Tasks added since 4,255)
- Desktop tests: 628 passed, 0 failed, 0 skipped, Debug (10 m 30 s) and Release (14 m 29 s)
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

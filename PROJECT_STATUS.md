# TempestOS — Project Status

**Branch:** `main`. `v0.22.0` was integrated on `release/v0.22.0` (`main` + `claude/tempestos-v1-final-acceptance-19hka8` + `claude/academy-docs-review-completion-iqzgwv`, merged 2026-09-23) and accepted by the Product Owner the same day — see `PRODUCT_OWNER_ACCEPTANCE.md` §9.
**VERSION:** `0.22.0`. `v0.17.0` and `v0.18.0` are the releases previously merged to `main`, tagged and published; `v0.19.0`–`v0.21.0` (the acceptance line) and the seven dashboard-export commits of 2026-09-20/21 (`main`, `ADR-0154`/`ADR-0155`) are now both in `v0.22.0`. `v1.0.0` is deferred until the live Xero and QuickBooks connectors have had their first real run.

## What a user can do today

Everything `PRODUCT_OWNER_ACCEPTANCE.md` records as delivered by the
overnight acceptance campaign (§2–§7 there; the five `v1.0.0`
capabilities are all ● as of `v0.21.0`), plus `main`'s own dashboard
export of real contract, quotation and project-health figures:

- `ADR-0154` — Contracts and `Pricing.PricingService` returned from
  `src/Frozen/` at the Product Owner's request; a sales `Quotation` kind
  added on the same pattern; the Dashboard Export writes
  `contracts.json`/`quotes.json` in Tempest-Dashboard's own shapes. Risk,
  Assets, Finance, Development, Operating and `CommercialIntelligence`
  stay frozen.
- `ADR-0155` — project health is a Core concept: the Engineering
  Cockpit's own health rollup scoped to each Project
  (`EngineeringCockpit.ProjectHealth`), shown on the Cockpit's Project
  Health card (formerly Recent Projects) and exported in `programme.json`
  schema v2 (`projects[].health`/`blockedCount`/`overdueActionCount`,
  `summary.byHealth`). RAG is `EngineeringHealthStatus`; no second
  vocabulary.

Owed, all on the Product Owner: docking steps 3–4 (`ADR-0153`) after
their own review; the first live Xero sign-in and QuickBooks connection
(`v1.0.0`'s own gate); the first `Setup.exe`; docking K4/K5 on two
monitors and the save-on-close half of persistence in a Windows session;
the answer to `TD-188` (per-line VAT on the Quote tab).

## Work in flight

None. `v0.22.0` is released, on `main`. The next Work Package is the
live Xero/QuickBooks connector run `v1.0.0` is waiting on.

## Gate (`release/v0.22.0` integration merge, re-derived 2026-09-23)

- Build: see `docs/releases/v0.22.0/Release Notes.md` for this
  integration's own re-derived Debug/Release figures.
- Governance health check: 5 of 5.
- The acceptance line's own last-recorded gate (its head `37264671`,
  2026-09-16): Core tests 5,314/5,324 (10 Windows-only failures, green on
  Windows CI); Desktop tests 901/902 both configurations (1 Linux-only
  failure, green on Windows CI); CI green on run 442. Full detail in
  `PRODUCT_OWNER_ACCEPTANCE.md` §3 and `docs/releases/v0.21.0/Release Notes.md`.
- Live backlog: `BACKLOG.md`.

## Released

- `v0.22.0`: `main` + the acceptance line (`v0.19.0`–`v0.21.0`) +
  Academy docs review, merged via `release/v0.22.0`, accepted by the
  Product Owner 2026-09-23 (`PRODUCT_OWNER_ACCEPTANCE.md` §9).
- `v0.18.0` Evidence and Check: PR merged to `main` (`3680257`), tag `v0.18.0`, GitHub Release published by run 34823372128 on 2026-09-14 with `TempestOS-v0.18.0.zip` and `TempestOS-v0.18.0-engineering-harness.zip`.
- `v0.17.0`: merged to `main`, tagged, published 2026-09-09.

## Where things are recorded now

- **`PRODUCT_OWNER_ACCEPTANCE.md`** — the acceptance pack; §9 is the Product Owner's `v0.22.0` verdict.
- **`OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md`** — the engineering record of the acceptance campaign.
- **`docs/releases/v0.22.0/Release Notes.md`** — what shipped in this release.
- **`BACKLOG.md`** — the live technical-debt list a user could still notice.
- **`CONTRIBUTING.md`** — how a Work Package becomes a branch, a PR and a merge.
- **`PHYSICAL_REVIEW.md`** — the running-application checklist every release is verified against.
- **`docs/releases/v1.0.0/WorkPackages.md`** — the programme the next Work Package executes.

Everything this file used to carry — Work Package history, governance
narrative, superseded metrics — is archived, not deleted:
`archive/docs-2026-09/`; the `v0.19`–`v0.21` gate histories are in the
release notes of those releases.

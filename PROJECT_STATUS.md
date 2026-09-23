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

## Gate (`release/v0.22.0` integration merge, re-derived 2026-09-23 on Linux)

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors`.
- Core tests: 5,447 passed of 5,457, Debug and Release — the 10 failures
  are the same known Windows-only tests as the acceptance line's own gate
  (6 spawn `powershell`, 4 DPAPI), expected green on Windows CI.
- Desktop tests: 902 passed of 903, both configurations — the 1 failure
  is the same known Linux-only test as the acceptance line's own gate
  (`StatusBarCollapseTests…`, font fallback), expected green on Windows CI.
- Architecture invariants (`DependencyDirectionTests`): 6 of 6.
- Governance health check: 5 of 5 (Python emulation of
  `scripts/governance-healthcheck.ps1`'s five checks — no PowerShell on
  this Linux worktree).
- Merge-caused failures (main's Dashboard Export code against the
  acceptance line's TD-88/WP 21.5B lazy-materialisation repository
  contract, a layout overflow in ADR-0155's new Project Health card, and
  one version-pinned test) found and fixed; see the "Fix merge-caused…"
  commit for detail.
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

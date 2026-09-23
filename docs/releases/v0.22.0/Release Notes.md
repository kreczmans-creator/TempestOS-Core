# TempestOS v0.22.0 — Release Notes

**Status: accepted.** Product Owner verdict recorded in
`PRODUCT_OWNER_ACCEPTANCE.md` §9, 2026-09-23: accepted as `v0.22.0`;
`v1.0.0` is deferred until the live Xero and QuickBooks connectors have
had their first real run.

## Summary

`v0.22.0` is the merge of three lines of work into `main`:

- `main` itself — `v0.18.0` (Evidence and Check) plus the seven
  dashboard-export commits of 2026-09-20/21 (`ADR-0154`, `ADR-0155`:
  Contracts and quotations return to the build, and project health as
  the Engineering Cockpit's own rollup).
- `claude/tempestos-v1-final-acceptance-19hka8` — `v0.19.0` through
  `v0.21.0` (Project commercial core, outbound invoicing, quotations,
  docking, the recovery tranche) plus the overnight acceptance campaign
  of 2026-09-15/16, 512 commits in total, including
  `release/v0.21.0` and `claude/ci-failures-investigation-stohyz`.
- `claude/academy-docs-review-completion-iqzgwv` — ten Academy
  documentation commits (already contained in the acceptance line by the
  time of this merge; no separate merge commit was needed).

`main`'s two 2026-09-21 ADRs (`ADR-0150`, `ADR-0151`) were renumbered to
`ADR-0154`/`ADR-0155` before merging, to clear the acceptance line's own
`ADR-0150`–`ADR-0153` (v0.19.0–v0.21.0 numbering). Every reference across
docs, code and tests was updated; see the ADR Register for the full
149 + 4 + 2 = 155-row accounting.

## What a user can do today

Everything `PRODUCT_OWNER_ACCEPTANCE.md` and
`docs/releases/v0.21.0/Release Notes.md` describe as delivered by the
acceptance campaign, plus `main`'s own dashboard export of real contract,
quotation and project-health figures (`ADR-0154`/`ADR-0155`). Owed, per
the acceptance pack: docking steps 3–4 (`ADR-0153`), the first live Xero
sign-in and QuickBooks connection, the first `Setup.exe`, and the answer
to `TD-188`.

## v1.0.0

Deferred at the Product Owner's instruction of 2026-09-23 until the live
Xero and QuickBooks connectors named in `PRODUCT_OWNER_ACCEPTANCE.md` §7
have had their first real run. `v0.22.0` is the released state in the
meantime.

# TempestOS v1.0.0 — Release Notes

**Status: DRAFT (2026-10-01), not released.** `VERSION` still reads
`0.22.0`; `scripts/new-release.ps1 -Version 1.0.0` refuses until the
Product Owner bumps it. The one remaining gate is the first live Xero
sign-in (`Xero Sign-in Runbook.md`, this folder; `PHYSICAL_REVIEW.md`
§7k X1–X5), which needs the Product Owner's own Xero app and consent.
The QuickBooks first run is deferred past `v1.0.0` (no app registered).

## Summary

`v1.0.0` is `v0.22.0` (accepted 2026-09-23) plus the release-candidate
tranche of 2026-10-01 (`claude/next-stretch-j1fqz2`, PR #14):

- `TD-186` — status bar, refusals and completion messages name objects
  by display name, never by GUID (`WorkspaceStatusBar`,
  `DeliverableService`, `InvoicingService`, `TimesheetService`).
- `TD-187` — the Invoices "Outstanding / Overdue" heading says what the
  group means instead of citing a backlog id.
- `TD-188` — a per-line VAT rate picker on the Quote tab; a line left
  on "Settings default" behaves exactly as before.
- `TD-183` — the OAuth loopback port moved from 49301 to 48131, below
  Windows' dynamic range; redirect URI `http://127.0.0.1:48131/callback/`.
- Requirements undo — create/delete of a Requirement Group or
  Collection is now undoable/redoable (`UndeleteRequirementGroupCommand`,
  `UndeleteRequirementCollectionCommand`). Still not undoable, as
  disclosed: add-to-collection, revise, set-owner, set-priority, link.
- `scripts/run-realshell-linux.sh` creates `--out` before starting Xvfb
  (it failed on a fresh directory).
- Dependabot: `actions/checkout` 6, `actions/setup-dotnet` 5,
  `actions/upload-artifact` 5 merged; Stryker 5.0.0 (#13) held.

## Known limitations carried into v1.0.0

- The one-off `SIGABRT` on exit (`WP 21.5C`, 1 of 9 runs) — see the
  "Exit" section below for the 2026-10-01 reproduction attempt.
- 10 Core tests fail on Linux (DPAPI, PowerShell) and 1 Desktop test;
  all pass on the Windows CI runners.

## Exit stability (SIGABRT) — 2026-10-01

_Filled in from the three real-shell journey runs under Xvfb on this
branch once they complete; see PR #14._

## Gate

_Re-derived on the `v1.0.0` head before tagging: build (0/0, both
configurations), Core and Desktop test counts, architecture invariants,
governance health check. Linux figures from PR #14; Windows from CI._

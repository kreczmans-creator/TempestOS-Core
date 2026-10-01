# TempestOS v0.23.0 — Release Notes

**Status: draft.** Awaiting the Product Owner's verdict. This draft lives
on `claude/focused-dirac-k0qilf`, five commits on top of `main`
(`v0.22.0`).

## Summary

`v0.23.0` acts on the Product Owner's RC runbook results (22 Sep) and
the follow-up messages of 2026-10-01. The full action register,
including items waiting on a decision, is
`docs/reviews/RC PO Feedback Actions (2026-10-01).md`.

## What changed

- **Home right rail entries are working links.** Continue entries open
  their project. Recent, Favourite and Recently changed now switch to
  Engineering before opening the object. Before, they opened it into the
  hidden Engineering document area, and the click appeared to do nothing.
- **Every module's landing page is called "Dashboard".** "Dashboard +
  Reports" (Projects, Engineering) and "Dashboard & Reports" (Business)
  are renamed, and Home's page title now reads Dashboard.
- **Reports is withdrawn from the shell for now.** The issued-sheet and
  project-document list is no longer shown under Engineering's
  dashboard. `ReportsView` is kept so it can return once real client use
  shows what reports need to be. Until then, an issued evidence sheet
  cannot be opened or exported from the shell.
- **Ctrl+K and the header search box (runbook D9).** Ctrl+K now opens
  the Command Palette even when the focused control has already handled
  the key. Clicking the header search box now opens the palette with the
  cursor in its query. Before, the box sat inert until Enter was pressed.
- **Fixed-list choices are dropdowns.** Every command parameter with a
  fixed option list is now picked from a dropdown instead of typed. This
  covers Create Document's Kind and every other `Choice`/`EnumChoice`
  parameter, in the Ribbon and the Command Palette alike.
- **Xero sign-in will be accepted for a new Xero app.** Xero apps
  created on or after 2 March 2026 cannot be granted the broad
  `accounting.transactions` scope, so the consent would have been
  refused. TempestOS now requests `accounting.invoices`,
  `accounting.contacts.read` and `accounting.reports.banksummary.read`,
  one scope per endpoint it calls. The Bank Summary reading also lacked
  a reports scope until now.

## Documentation

- `docs/guides/Xero Setup - Step by Step.md` — a numbered, one-action-
  per-step guide to the first live Xero connection, with a
  troubleshooting table.
- `docs/reviews/RC PO Feedback Actions (2026-10-01).md` — the action
  register for every runbook comment. It includes the scope and work
  packages for live 2D reference diagrams on every calculation.

## Known, carried forward

- F8 — the first-run data-location dialog hangs when TempestOS is
  launched from the Start menu; the desktop shortcut works (`C-01`).
- The Xero loopback port moves from 49301 to 48131 (`TD-183`) on
  `claude/next-stretch-j1fqz2`, which is not part of this release. Until
  that branch merges, register `http://127.0.0.1:49301/callback/`.
- Runbook items D10–D13, E5–E12 and F1–F7 are still to be run.

## Gate (Linux, this branch's head)

- Build: Desktop, Core and RealShell test projects build with 0 errors.
- Core tests (invoicing, OAuth and Xero subset): 263/267. The 4
  failures are the known Windows-only DPAPI tests.
- Desktop tests: GATE_PENDING.
- Not run here: the Windows real-shell journey and the installer.

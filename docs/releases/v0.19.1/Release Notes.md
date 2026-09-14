# TempestOS v0.19.1 — Release Notes

**Status: in preparation on `release/v0.19.1`, cut from the `v0.19.0`
candidate `947c50d` on 2026-09-14 after the Product Owner's first pass
over it.** Nothing in this document is certification.

## Summary

**v0.19.1 is the Product Owner's first-pass corrections to the
Consultancy Seam** — eight items raised on 2026-09-14 while testing the
`v0.19.0` candidate, led by the Quotation: a quote opened with the
project defines its requirements and deliverables, and everything
downstream (completion, invoice request) hangs from it. Alongside it: the
shell laid out the way the Product Owner sketched it (Home, Projects,
Tasks, Engineering, Business, each with a dashboard), Timesheets under
Business, subscriptions and bills read from the accounting package rather
than entered, a real editor for library records, drag-and-drop
attachments, and three defects in the `v0.19.0` Structure tab.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 19.4A` The Structure tab contained; the menu bar removed; the ribbon scoped | The engineering surface lays out inside the Structure tab's content bounds (the negative-margin trick is gone; each project tab carries its own padding); the layout walk gains a tab-strip overlap check that fails on the old layout; the menu bar and its command row are removed everywhere the surface renders, with Reset Layout, Theme, Macros and View Relationships as Command Palette entries and Undo / Redo on their shortcuts; the engineering ribbon shows an allow-list of engineering categories, so Quotations, Deliverables, Invoicing and Timesheets stay in their own areas | 2026-09-14 (9eb5f32) |
| `WP 19.4B` Real files on every Attachments section | Browse on every Kind with attachments (every canonical Kind), a drop zone that stores the dropped files' bytes, size and SHA-256 through the same path, the typed metadata form kept under "Record a reference without the file"; a Calculation's attachment opens in the viewer; seven tests | 2026-09-14 (6710d64) |
| `WP 19.6A` A real editor for library records | `ReferenceRecordView`: identity, the definition's fields rendered generically with units (every library's definition types, nested groups and tables), revision history with the current marked, source citation, cited by (one Evidence scan through `ReferenceCitationIndex`), Verify, Release and Revise on the record; Libraries becomes master and detail with Open and double-tap, folding to a Back control when compact; all eight libraries listed (Manufacturing, Components and rate cards newly wired); Add and Revise open the record right up | 2026-09-14 (e9aeabc) |
| `WP 19.5A` Quotation core (ADR-0152) | The `Quotation` Kind under a project: reference (`Q-yyyy-nnn` or given), date, client, currency, validity, terms, lines with hours × rate or a fixed price, totals; Draft → Sent → Accepted / Declined with refusals as results; Accept creates one Deliverable per line under a milestone named after the reference and one Requirement per line allocated to the quotation and so to the project; `AddDeliverableAsync` for deliverables added directly; five `quotation.*` commands under the Quotations category; explorer area, editor declaration, registration guards | 2026-09-14 (564a074) |
| `WP 19.8B` Accounts reads through the connector | Read-only `IAccountsConnector` (bills due, repeating bills, cash position) on the Fake, Xero (primary: ACCPAY invoices, repeating invoices, the bank summary) and QuickBooks Online connectors; a cached reading at `<persistence root>/accounts/last-reading.json` refreshed hourly and on demand; Hardware / Software / Premises from Xero account names with keyword defaults; `AccountsSnapshot` with the four tiles, receivable and payable buckets and a twelve-week cash-flow series; one Settings line with Refresh now | 2026-09-14 (901ce26) |

## Figures

*(re-derived at `WP 19.9.1`)*

## Warnings

- **Accounts reads are written to the API documents, not proven live**
  (`WP 19.8B`): Xero's bank summary is parsed by column title rather than
  position and carries no per-account currency, so
  `Invoicing:Xero:BaseCurrency` (default GBP) applies; the account name on
  a Xero repeating invoice is read from its tracking and account code
  fields as documented; QuickBooks Online's `RecurringTransaction` query
  and nested `Bill` shape follow the documentation. The first live
  authorisation is the test.
- **Three menu entries went without a replacement** (`WP 19.4A`): the
  View toggles for the Explorer, Inspector and Output panels, the three
  layout presets (Engineering, Review, Documentation) and About. A closed
  core panel comes back on re-entering Engineering or through Reset Layout
  on the Command Palette; the presets had no user beyond the menu. Say if
  any of the three should return.
- *(further warnings filled at each merge)*

## Related

- `docs/releases/v0.19.1/Execution Plan.md`
- `docs/releases/v0.19.0/Release Notes.md`

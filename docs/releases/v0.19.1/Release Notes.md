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
| `WP 19.5C` Project lifecycle, status and tasks read models, the ManualTask Kind | `ProjectLifecycleService`: Hold, Resume, Sign off (statement and record) and Reopen, one transaction and audit row each; a project's listing group (Open, Closed under 90 days, Archive over 90 days) derived from its closed date; writes on an archived project refused in the commercial, quotation, deliverable, timesheet and invoicing services; `ProjectStatusReadModel`: one scan giving each project one of On hold, Blocked, Overdue, At risk, Ready to invoice or On track with the reason, counts and the Gantt schedule fields; `ManualTask` Kind with its own service, commands (`task.create`, `task.complete`) and registration; `ITasksReadModel`: Overdue, Due today, Due this week, Later, Reviews, Approvals and Finance buckets over deliverables, milestones, manual tasks, evidence, invoice requests and quotations | 2026-09-14 (ab505b3) |
| `WP 19.5B` The Quote tab, the Quotes area, the quote export | `ProjectQuoteView` as the project's Quote tab: identity, lines editable while Draft (add, edit, remove through `quotation.update-line` / `quotation.remove-line`), totals, terms, Send / Accept / Decline with confirmation, Export, and after Accept the deliverables and requirements it created with open-right-up; `QuotesView` under Business (New, Sent, Outstanding, with Open, Export and New Quote through a project picker); `QuotationSheetRenderer`: an A4 quote PDF paginated with the table header and footer on every page (SkiaSharp, the issue sheet's own two-phase layout) saved through the save picker as `<reference>-quote.pdf`; New Project prompts to open a quotation with the project (on by default, ADR-0152); Add Deliverable on the Deliverables tab through `deliverable.add`; the editor's Quotation lines section; `QuotationJourneyTests`, renderer tests over extracted PDF text, a rail contract for Quotes | 2026-09-14 (1f81c27) |
| `WP 19.5D` A blank optional parameter reaches the binding; the invocation contract covers Quotations and Tasks | `InputDialog.PromptAsync` gains `allowBlank` and the command prompt passes it, so a Ribbon or Palette parameter the binding accepts blank (`quotation.create`'s reference, generated as `Q-yyyy-nnn`; `deliverable.add`'s target date) completes without typing one, while the other twenty-eight prompts keep refusing blanks; `CommandInvocationContractTests` now covers the Quotations and Tasks categories, which exposed `quotation.update-line` / `remove-line`'s line-id parameter as unsatisfiable from the Palette — given a default the validator accepts and the service refuses cleanly; a journey creating a quotation from the Palette with a blank reference | 2026-09-14 (c83f99c) |
| `WP 19.7A` The shell as sketched | The rail reads Home, Projects, Tasks, Engineering, Business and nothing else; a header with the mark, a global search that opens the Command Palette, a notifications bell with its flyout and the signed-in principal (Settings behind it); Projects → Dashboard + Reports, Open, Closed (under 90 days), Archive, each project opening its workspace with Overview, Quote, Structure, Deliverables, Requirements, Evidence, Sign off, Documents, Tasks, Risks and Timeline tabs; Engineering → Dashboard + Reports, Tasks (Reviews, Approvals), Modules (Mechanical, Engineering Calculations), Reference data (the eight libraries); Business → Dashboard & Reports, Quotes, Invoices, Timesheets, Subscriptions (repeating bills by Hardware / Software / Premises and bills due, read-only from the accounts reading); Tasks lists every bucket with open-right-up and New task; Sign off through the lifecycle service; five real defects fixed on the way (a double-parenting crash, a sticky selection loop, stale group content, a closed popup's false bounds, disposed-host handlers); nineteen suites re-navigated | 2026-09-14 (2c3ce7e) |
| `WP 19.7B` The four dashboards | Home: Overdue, Due today, Due this week, Approvals and Finance tiles (each opening the Tasks area on that bucket), the commercial snapshot (open quotes, invoices sent, outstanding, overdue), the project status chart, the next milestones, the task list, and Continue / Recent / Favourite; Projects: Active, At risk, On hold and Ready to invoice tiles, the Blocked, At risk and Ready to invoice lists with reasons, a Gantt of open projects with quoted and recorded hours; Engineering: open tasks and engineering reviews; Business: Invoiced, Overdue, Due 30 and Due 90 tiles, receivable and payable lists, quotes open with the over-seven-day chase list, a twelve-week cash-flow line; one `DashboardChart` helper drawing bars, lines and Gantt rows with Avalonia shapes in theme colours, every figure also as text; every empty panel says why; `DashboardsTests` over a six-project fixture with hand-computed values | 2026-09-14 (8216db9) |
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
- **Archived-project write guards stop at the five Core services**
  (`WP 19.5C`): the commercial, quotation, deliverable, timesheet and
  invoicing services refuse writes on an archived project; the
  Workspace-layer milestone and engineering-task services, the evidence and
  requirements services and the new task service do not yet. Nothing in
  the shell offers those writes on an archived project, but the guard is
  not structural there.
- **"Finance" tasks use a thirty-day heuristic** (`WP 19.5C`): no payment
  terms field exists on an invoice request, so a Sent request unpaid for
  thirty days, and a Sent quotation older than seven days, are what the
  Finance bucket lists. When terms are recorded the bucket should read
  them.
- **The quote PDF is about 600 KB even for one page** (`WP 19.5B`):
  the font subset is embedded, as the issue sheet's is. The renderer
  paginates (the table header and the footer repeat on every page;
  `QuotationSheetRendererTests` proves it with forty lines).
- **The header search opens the Command Palette without the typed text**
  (`WP 19.7A`): the palette has no public way to seed its query yet, so
  the search box opens the Objects search and the text is typed again
  there. Ctrl+K is unchanged.
- **Engineering → Tasks lists Reviews and Approvals only** (`WP 19.7A`):
  the tasks read model has no Calculations bucket (a calculation is not a
  task until someone asks for one), so the sketched Calculations node is
  not shown rather than shown empty.
- **A project-context refresh race is fixed at the test, not the source**
  (`WP 19.7A`): `ProjectContext.RefreshAsync` closes the context when an
  overlapping render does not yet find a just-created project; the New
  Project with quotation journey exposed it and the test now waits. The
  real fix belongs in `ProjectContext`; carried to the backlog.
- *(further warnings filled at each merge)*

## Related

- `docs/releases/v0.19.1/Execution Plan.md`
- `docs/releases/v0.19.0/Release Notes.md`

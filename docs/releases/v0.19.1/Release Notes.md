# TempestOS v0.19.1 — Release Notes

**Status: release candidate on `release/v0.19.1`, cut from the `v0.19.0`
candidate `947c50d` on 2026-09-14 after the Product Owner's first pass
over it; the candidate is the tip of the branch as pushed on 2026-09-15
after the overnight tranche (`WP 19.10A`–`19.10R`, the What shipped table
below); under the Product Owner's manual test.** Nothing in this
document is certification.

## Summary

**v0.19.1 is the Product Owner's first-pass corrections to the
Consultancy Seam** — nine items raised on 2026-09-14 while testing the
`v0.19.0` candidate (eight comments and the answers that a quote export
ships now and that Xero is the accounting package), led by the Quotation: a quote opened with the
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
| `WP 19.7C` A view keeps reacting after it has been shown, hidden and shown again | Since `WP 18.2A` every area view and tab that reads the change feed dropped its subscription the first time it left the visual tree and never took it back, so on any second visit it re-read on entry but no longer reacted to a change made while it was showing; `WorkspaceChangesSubscription` now subscribes on attach and unsubscribes on detach for all thirteen views (Home, Projects, Engineering, Business, Tasks, Evidence, Invoicing, Quotes, Reports, Timesheets, the Deliverables and Quote tabs, the object editor); `WorkspaceChangesReattachTests` proves each one reacts, stops while hidden, and reacts again; a Tasks journey leaves and returns before creating a task; a Home journey leaves and returns, then completes a task while Home is showing and loses the row with no re-entry — the one journey that fails on the pre-fix sources | 2026-09-14 (df2ebe8) |
| `WP 19.8B` Accounts reads through the connector | Read-only `IAccountsConnector` (bills due, repeating bills, cash position) on the Fake, Xero (primary: ACCPAY invoices, repeating invoices, the bank summary) and QuickBooks Online connectors; a cached reading at `<persistence root>/accounts/last-reading.json` refreshed hourly and on demand; Hardware / Software / Premises from Xero account names with keyword defaults; `AccountsSnapshot` with the four tiles, receivable and payable buckets and a twelve-week cash-flow series; one Settings line with Refresh now | 2026-09-14 (901ce26) |

| `WP 19.9.1` Release | Two idle-machine races in `QuotationJourneyTests` fixed by waiting for the last side effect (the attached sheet after Send; the export file closed, not merely created); every cockpit card action names itself, so the automation-name walk has no exemption left; `PHYSICAL_REVIEW.md` §7c (D1–D19) written from the code, which also corrected this document's own wrong "one page" claim about the quote PDF; the backlog reconciled (nine open rows re-verified unchanged, six raised from the packages' own disclosures, one raised and closed in the same pass, the live list at exactly 30); the nine Product Owner comments checked against the code (five answered, four answered with a disclosed limit, none unanswered); figures, warnings, `PROJECT_STATUS.md`; three green CI Gate runs on the candidate head; the CI Build & Test ceiling raised from 45 to 90 minutes after the Desktop leg reached 43 | 2026-09-14 (df2ebe8) |
| `WP 19.10D` Business → Invoices grouped as the Product Owner sketched | `InvoicingView` regroups New (Draft — Review, Send, Void) / Available to invoice (a live `DeliverableCompletion` not yet invoiced and not carried by any live request's own lines — Open completion, Raise invoice) / Sent (Sending, Sent, Accepted not yet Outstanding — Review, Reconcile now) / Outstanding-Overdue (Sent/Accepted unpaid more than thirty days after being sent — the identical heuristic the Finance task bucket uses — plus Reauthorise and Unknown, which always need attention) / Closed (Rejected, Voided — Review only, collapsed by default); Raise invoice dispatches the identical `invoicing.raise` command the Deliverables tab already uses; every `InvoiceRequestStatus` this platform ever persists lands in exactly one group; `InvoicesGroupingTests` proves the placement, the group captions and each group's own empty text | 2026-09-14 (0e069a76) |
| `WP 19.10Q` New Project from Projects → Open opens the project right up | `ProjectBrowserView.CreateAsync` finds the project it just created through the directory itself, not its own filtered `_current` list — a snapshot `ProjectsAreaView` takes at group-selection time, before the new project exists, which left `CreateAsync` unable to find its own work and returning without ever opening it; a `ProjectCreated` event lets `ProjectsAreaView` extend the Open group's own visible set synchronously as part of the same create path, so the list and the filter agree the moment the project opens; the explicit re-open `QuotationJourneyTests` carried for this is gone; `ProjectBrowserViewTests` proves the journey through `ProjectsAreaView` end to end — a `PHYSICAL_REVIEW.md` §7c D2 gap found by `WP 19.10B`'s file-based tracing | (d46e910) |
| `WP 19.10O` The rail and tree-column collapse on demand, and stay collapsed | A chevron at the navigation rail's own foot folds it to icon width at any window size (Ctrl+B, and "Collapse navigation" on the Command Palette), independent of the existing responsive fold, which still wins below `CompactShellWidth`; a new shared `CollapsibleColumn` control gives the Projects, Engineering and Business tree columns the identical chevron, collapsing each to a 28px strip carrying the expand chevron and a vertical caption naming the area, with the right pane filling the freed width; both states persist through the existing Desktop panel UI state document and are restored before first render; the layout walk gains a sibling covering both collapsed states at both existing sizes | 2026-09-14 (b7ba555) |
| `WP 19.10P` The rehearsal's own findings: Open deliverable opens, money shows its symbol, empty libraries show a heading | Open deliverable (the D6 blocker) now opens right up — no `IWorkspaceViewFactory` was ever registered for the Deliverable Kind, only for its own `DeliverableCompletion`; `MoneyDisplay.Format` renders a currency symbol (£/€/$, the ISO code otherwise) wherever an amount is shown to a person, across the Home/Business/Projects dashboards, the Quote tab, Quotes and Invoicing; the Home dashboard's Invoices line renders `AccountsSnapshot.UnavailableReason` once, verbatim, dropping a doubled period and dead fallback text; `LibrariesView` gives every one of the eight governed libraries its own heading, an empty one reading "No records yet"; the eighth library's own screen name is "Rate cards", its routing key unchanged | 2026-09-14 (ca181b23) |
| `WP 19.10R` The Structure tab's ribbon and the Command Palette refuse a write on an archived project (`TD-179` residual) | `CommandBinding.Mutates` (defaults `false`, so every untouched binding is unaffected); `ArchivedProjectCommandGuard` resolves the project a command would mutate (the selected object's own ancestry via `IHasParent`, else the shell's open project scope) and refuses "Project '{code}' is archived — read only." when it is Archive; `CommandRegistry.Evaluate` consults the guard for any binding whose `Mutates` is set, so the Ribbon, the Palette and a macro replaying either all see the identical refusal from the one place availability is already decided; `mutates: true` set on every genuine write across the five discipline registrations (Mechanical, Manufacturing, Documents, Verification, Requirements) plus Quotations, Deliverables, Tasks and Evidence — sixty binding sites, seventy-seven commands — with `mechanical.validate-configuration` the one deliberate exception (a read-only consistency check); registered in `TempestHost`'s own container so the shipped `CommandRegistry` singleton carries a real guard; `ArchivedProjectCommandGuardTests` (Core) and an extension to `ArchivedProjectReadOnlyTests` (Desktop, the Structure tab) | 2026-09-14 (pending merge) |

## Figures

Re-derived on the candidate head `033ac18` (`WP 19.9.1` and the overnight tranche)
(`git grep -c '' HEAD -- 'src/*.cs'` excluding `Frozen/`; the test
counts from the gate).

| Figure | v0.19.0 (`947c50d`) | v0.19.1 (`df2ebe8`) |
|---|---|---|
| Live source lines (`src/`, excluding `Frozen/`) | 126,156 | 141,032 |
| Live test lines (`tests/`) | 107,824 | 142,421 |
| `Tempest.Core` source lines | 59,472 | 63,623 (quotations, lifecycle, tasks, accounts reads, the overnight integrity fixes) |
| Core tests | 4,255 | 4,455 |
| Desktop tests | 550 | 628 |
| ADRs | 151 | 152 (ADR-0152) |
| Commits on the branch | — | 161 since `947c50d`, thirty-one of them merges |
| Effort | — | 30 days across twelve Work Packages on 2026-09-14 plus the overnight tranche `WP 19.10A`–`19.10R` (about 14 days) into 2026-09-15 |

## Gate on the candidate head

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors`
- Core tests: 4,370 passed, 0 failed, Debug and Release
- Desktop tests: 594 passed, 0 failed, Debug (9 m 57 s) and Release (9 m 18 s)
- Governance health check: 5 of 5
- CI: the CI Gate job green on every pushed head of the branch on 2026-09-14 (runs 34842170404 on `289b11c`, 34849599412 on `6c37f34`, 34866145341 on `fc67feb`, 34877649676 on `dfbd637`); the three runs on the candidate head itself are recorded on the candidate page

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
- **Archived-project write guards stop at `IRequirementsService`**
  (`WP 19.5C`, narrowed `WP 19.10H`, narrowed again `WP 19.10R`): the
  commercial, quotation, deliverable, timesheet, invoicing, milestone,
  engineering-task, evidence and manual-task services all refuse writes on
  an archived project now, the project workspace disables every write
  control it can reach on Evidence, Tasks, Timeline and the Quote tab with
  an "Archived project — read only" tooltip, and the Structure tab's Ribbon
  and the Command Palette (and a macro replaying either) now refuse too:
  `Tempest.Core.Commands.CommandRegistry.Evaluate` consults one
  `ArchivedProjectCommandGuard`, resolving the project a command would
  mutate (the selected object's own ancestry, else the shell's open
  project) for every binding across the five discipline registrations
  (Mechanical, Manufacturing, Documents, Verification, Requirements) plus
  Quotations, Deliverables, Tasks and Evidence whose own
  `CommandBinding.Mutates` marker is set, refusing "Project '{code}' is
  archived — read only." — the disabled Ribbon button carries this as its
  own tooltip, and the Palette lists the command with the same reason.
  `IRequirementsService.CreateAsync` still takes no project id, so guarding
  it needs a design step rather than a copy of the pattern every other
  guarded write now follows — the one remaining residual, disclosed as
  `TD-179`.
- **The quote PDF is about 600 KB even for one page** (`WP 19.5B`):
  the font subset is embedded, as the issue sheet's is. The renderer
  paginates (the table header and the footer repeat on every page;
  `QuotationSheetRendererTests` proves it with forty lines).
- **The CI Build & Test ceiling is now 90 minutes** (`WP 19.9.1`): at 594
  Desktop tests under coverage collection the Debug leg took 43 minutes on
  a hosted runner and the next run was cancelled at 45 on both legs while
  still passing tests several seconds each; no hang (the per-test
  five-minute backstop never fired). The ceiling is raised, not the
  cause: the suite's CI time roughly doubles with coverage collection,
  which is diagnostic only. Worth deciding whether Debug needs it.

- **An editor on a background Document Area tab misses a change made
  while it is hidden** (`WP 19.7C`): a tab control detaches the content
  of the tab it leaves, so `WorkspaceChangesSubscription` unsubscribes
  the editor there and subscribes it again on return — but nothing
  re-reads an editor when its tab is reselected, so a change committed
  elsewhere while it was hidden shows only at the next change or its own
  Save. The area views are not affected: every one re-reads on entry.

## Related

- `docs/releases/v0.19.1/Execution Plan.md`
- `docs/releases/v0.19.0/Release Notes.md`

# TempestOS v0.19.1 — Execution Plan

**Branch:** `release/v0.19.1`, cut from the `v0.19.0` candidate `947c50d` on
2026-09-14. **Source:** `Product Owner Comments.md` in this folder — the
eight comments raised while testing `v0.19.0`, with the nine sketches of
the intended layout. **Method:** as `v0.19.0` — one branch on origin, one
worktree per Work Package on a throwaway local branch, Sonnet for every
implementation and audit agent, the full gate at every merge, half-hourly
status to the Product Owner.

## 1. What the Product Owner gets

1. A **Quotation** opened with the project: reference, date, client, lines
   (each a deliverable with its hours and price); Draft → Sent →
   Accepted / Declined; accepting it creates the project's deliverables and
   its initial requirements from the lines. Deliverables can also be added
   directly. Quotes live under Business (New, Sent, Outstanding) and on the
   project.
2. The shell laid out as sketched: **Home, Projects, Tasks, Engineering,
   Business** on the left; global search, notifications and the signed-in
   principal across the top; a dashboard at the head of Home, Projects,
   Engineering and Business.
3. **Projects** as Open / Closed (under 90 days) / Archive, each open project
   with Deliverables, Requirements, Evidence and Sign off; a project
   dashboard with status tiles, blocked and at-risk lists, ready-to-invoice,
   and a simple Gantt from the quote's hours and dates.
4. **Engineering** as Dashboard, Tasks (calculations, reviews, approvals),
   Modules (Mechanical today; Electrical and Structural are named as future,
   never shown empty), Reference data (standard parts, materials, the other
   libraries, and a constants library).
5. **Business** as Dashboard, Quotes, Invoices (available to invoice, sent,
   outstanding/overdue), Timesheets (project drop-down and hours),
   Subscriptions and bills **read from the accounting package through the
   connector**, never entered in Tempest; a cash-flow chart from the same
   reads.
6. Library records opened in a real editor: the definition's fields, units
   and values; revision history and state; source citation; cited by;
   Verify, Release and Revise on the record.
7. Drag a file onto any Attachments section (Calculation first) and it is
   stored with its bytes, size and hash; a Browse button beside it.
8. The Structure tab contained inside its tab, and the menu bar above the
   ribbon gone.

## 2. Waves

| Wave | Work Package | Depends on | Effort (days) | Model | Owns |
|---|---|---|---|---|---|
| 1 | `WP 19.4A` Structure tab contained; menu bar removed; ribbon scoped to engineering | — | 1.5 | Sonnet | `ProjectWorkspaceView`, `MainMenuFactory`, `MainWindowComposer.Layout`, `RibbonView` category filter, the layout walk's overlap check |
| 1 | `WP 19.4B` Real files on every Attachments section | — | 1.5 | Sonnet | `ObjectEditorView` attachments section, `IFilePicker` reuse, attachment bytes storage, viewer |
| 1 | `WP 19.5A` Quotation core (ADR-0152) | — | 3 | Sonnet | `Core/Quotations/*`, `Workspace/Quotations/*`, commands, editor declaration, tests |
| 1 | `WP 19.6A` Reference-record editor | — | 2.5 | Sonnet | a reference-record editor view, `LibrariesView` open actions, open-right-up from Add/Revise |
| 1 | `WP 19.8B` Accounts reads through the connector | — | 3 | Sonnet | `IAccountsConnector` (read-only), Fake/Xero/QuickBooks reads, cached reading, tests |
| 2 | `WP 19.5B` Quotation surfaces | 19.5A | 3 | Sonnet | quote at project creation, project Quote tab, Deliverables Add, Business → Quotes |
| 2 | `WP 19.5C` Tasks, project status, Sign off, Open/Closed/Archive read models | 19.5A | 3 | Sonnet | `Workspace/Tasks/*`, project lifecycle (Sign off, Closed, Archive), status read model |
| 3 | `WP 19.7A` The shell: rail, header, the three trees, Timesheets under Business | 19.4A, 19.5B, 19.5C | 4 | Sonnet | `ShellArea*`, `GlobalNavigationRail`, `ShellHeaderView`, area views, `TimesheetWeekView` prompt |
| 3 | `WP 19.7B` The four dashboards | 19.5C, 19.8B, 19.7A | 4 | Sonnet | Home, Project, Engineering and Business dashboard views and their read models |
| 3 | `WP 19.5D` Optional command parameters reach the binding; the invocation contract covers Quotations and Tasks | 19.5B, 19.5C | 0.5 | Sonnet | `InputDialog` allow-blank, `DesktopCommandPrompt`, `CommandInvocationContractTests` — two defects found by 19.5B and 19.5C, added 2026-09-14 |
| 3 | `WP 19.7C` A view keeps reacting after it has been shown, hidden and shown again | 19.7B | 0.5 | Sonnet | `WorkspaceChangesSubscription` helper, the thirteen views that null their feed on detach, a re-attach test — defect found by 19.7B, present since 18.2A, added 2026-09-14 |
| 4 | `WP 19.9.1` Release | all | 1.5 | lead | notes, `PHYSICAL_REVIEW.md` §7c, backlog audit, VERSION, CI, the candidate page |
| 5 | `WP 19.10D` Business → Invoices grouped as the Product Owner sketched | 19.7A | 0.5 | Sonnet | `InvoicingView` regrouped New / Available to invoice / Sent / Outstanding-Overdue / Closed, `InvoicesGroupingTests` — a `PHYSICAL_REVIEW.md` §7c D11 gap disclosed as a Release Notes Warning at `WP 19.9.1`, closed here |
| 5 | `WP 19.10I` TD-41: a Requirement opens in its own editor, not the three-line fallback | — | 0.5 | Sonnet | `ObjectEditorView.cs` (the gate and the Requirement body), `CreatedObjectOpensRightUpTests.cs`, `ObjectEditorViewTests.cs`, one assertion in `QuotationJourneyTests.cs`, `BACKLOG.md` |
| 5 | `WP 19.10H` TD-179: an archived project offers no write, and the remaining services refuse one | 19.5C | 1 | Sonnet | `ProjectMilestoneService`, `ProjectTaskService`, `EvidenceService`, `Tempest.Core.Tasks.TaskService` archived-project guards; `ProjectWorkspaceView` hands the archived flag to `ProjectTasksView`, `ProjectTimelineView`, `EvidenceWorkspaceView`, `ProjectQuoteView`; `ArchivedProjectReadOnlyTests` |
| 5 | `WP 19.10Q` New Project from Projects → Open opens the project right up | 19.10B | 0.5 | Sonnet | `ProjectBrowserView.cs` (`CreateAsync`, the `ProjectCreated` event), `ProjectsAreaView.cs` (the Open group's visible set, refreshed synchronously on create), `ProjectBrowserViewTests.cs`, the re-open block in `QuotationJourneyTests.cs` — a `PHYSICAL_REVIEW.md` §7c D2 gap found by `WP 19.10B`'s file-based tracing, closed here |
| 5 | `WP 19.10O` The rail and tree-column collapse on demand, and stay collapsed | — | 1 | Sonnet | `GlobalNavigationRail`, `CollapsibleColumn`, `ProjectsAreaView`/`EngineeringAreaView`/`BusinessAreaView` tree wrappers, `DesktopPanelUiState`, `MainWindowComposer.Wire.cs`/`Layout.cs` |
| 5 | `WP 19.10P` The rehearsal's own findings: Open deliverable opens, money shows its symbol, empty libraries show a heading | 19.10A | 0.5 | Sonnet | `DeliverableCompletionWorkspaceRegistration.cs`, `ProjectQuoteView.cs`, `MoneyDisplay.cs` and every surface using it, `HomeDashboardView.cs`, `LibrariesView.cs`, `ReferenceRecordView.cs`, `QuotationJourneyTests.cs`, `MoneyDisplayTests.cs`, `DashboardsTests.cs`, `LibrariesTabLoadsOnEntryTests.cs` — `docs/releases/v0.19.1/Rehearsal.md`'s D6 blocker and four D1/D15 differences, closed here |

| 5 | `WP 19.10R` TD-179 residual: no engineering command mutates an archived project, from the Ribbon, the Palette or a macro | 19.10H | 1 | Sonnet | `CommandBinding.cs` (`Mutates` marker), `ArchivedProjectCommandGuard.cs`, `CommandRegistry.cs` (`Evaluate` consults it), `TempestHost.cs` (DI registration); `mutates: true` set across the five discipline registrations plus Quotations, Deliverables, Tasks and Evidence; `ArchivedProjectCommandGuardTests` (Core), `ArchivedProjectReadOnlyTests` extended for the Structure tab (Desktop) |

Effort: 30 days. Status is reported per Work Package as a percentage of
its own scope and as its effort-weighted share of the 30.

## 3. Engineering decisions taken while planning

1. **`v0.19.1` supersedes the `v0.19.0` candidate for testing.** `v0.19.0`
   stays on its branch, unreleased; the Product Owner tests `v0.19.1`.
2. **The honest rail still applies** (WP 19.2B). Electrical and Structural
   modules, and any dashboard panel with no data behind it, are named in
   the notes as future rather than shown empty. Tasks is real because it
   is a read model over what exists — deliverables due, evidence awaiting
   check or issue, invoice requests to chase — plus a small manual Task.
3. **A quote line becomes one Deliverable and one Requirement** on
   acceptance, both named from the line; the engineer refines them
   afterwards. Declining a quote creates nothing. Sending records the date
   and offers the export: the quote rendered as a PDF (reference, date,
   client, lines with hours and price, totals, terms) through the same
   SkiaSharp path as the issue sheet — the Product Owner wants to test it
   before running the consultancy on it.
4. **Sign off** closes a project: who, when, a statement; the project moves
   to Closed, and to Archive (read-only) 90 days after that.
5. **Accounts payable is read, never written.** Bills due, repeating bills
   (subscriptions) and the cash position come through a read-only
   connector interface beside `IInvoicingConnector`; the Fake connector is
   scripted; the Xero and QuickBooks Online reads are written to their API
   documents with recorded-response tests and cannot be proven against a
   live sandbox from here. The last reading is cached with its time and
   shown as "unavailable since" when the connector is not authorised.
   The consultancy uses Xero, so the Xero reads are the primary, best-tested
   path and QuickBooks Online is secondary. Subscription categories
   (Hardware, Software, Premises) map from Xero's own account names and
   tracking categories with keyword defaults, editable in Settings.
6. **Timesheets under Business**, the project chosen from a drop-down of
   open projects, hours first.
7. **The constants library** seeds a small cited set (standard gravity, π,
   e, common material constants) through the same reference-record path as
   the other libraries; the Product Owner extends it.

## 4. Questions answered by the Product Owner on 2026-09-14

1. A quote export is wanted in this release (WP 19.5B).
2. The accounting package is Xero; categories map from its account names and tracking categories.

## 5. Risks

| Risk | Mitigation |
|---|---|
| The shell rework re-pins many tests (rail order, layout walk, automation names, product spine) | seam map lists every such test; `WP 19.7A` updates them with rationale, the walk re-runs at both sizes |
| The Quotation touches the project's create path and the deliverables the invoice seam depends on | `WP 19.5A` is core-only with journeys; `WP 19.5B` wires the UI once the core is green |
| Accounts reads cannot be verified live | recorded-response contract tests; the Fake carries the journeys; disclosed in the notes |
| Dashboards drift into placeholders | every panel reads a real snapshot or says "unavailable" with the reason; the rail-surface contract test extends to the four dashboards |

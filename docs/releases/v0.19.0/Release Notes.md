# TempestOS v0.19.0 — Release Notes

**Status: release candidate in preparation on `release/v0.19.0`, built on
the `v0.18.0` candidate; figures below are re-derived at `WP 19.9.0`
before the tag.** Nothing in this document is certification.

## Summary

**v0.19.0 is Consultancy Seam and Desktop** — the third release of the
[v1.0.0 Release Candidate Programme](../v1.0.0/WorkPackages.md). It adds
the thin business model a consultancy bills with (client, purchase order,
budget, pinned rate card with billing and cost rates, time by the week,
deliverable completion), one outbound accounting seam (Xero and
QuickBooks Online, with a fake connector carrying every test), the five
KPIs as read models defined in `ADR-0150` before any card was built, a
Desktop whose rail contains only what works, and a layout walk that
screenshots every screen in CI.

## What a user will notice

- A project carries its **client, PO reference, budget, rate card, dates
  and project manager** in the editor's Commercial section.
- **Timesheets** on the rail: a week at a time, each entry freezing the
  billing and cost rate it was recorded at; utilisation for the week from
  the working pattern in Settings.
- A project's **Deliverables** tab: complete a deliverable once, with the
  issued evidence and documents it delivers, and **raise an invoice** from
  the unbilled time and fixed-price value.
- **Invoicing** on the rail: requests by status; Send, Reconcile, Void;
  every connector outcome shown as a state, never a retry loop; the fake
  connector until a sandbox is authorised in Settings.
- **Home** shows utilisation, margin per project, work in progress, days
  sales outstanding and calc throughput, with period selection.
- The rail is **honest**: Home, Projects, Evidence, Timesheets, Invoicing,
  Reports, Engineering Calculations, Settings. Engineering lives inside a
  project as its Structure tab. Tasks, Commercial, Resources, Knowledge and
  Administration are gone rather than dimmed.
- Every screen is walked, screenshotted and bounds-checked by CI on
  Windows; the screenshots are a downloadable artifact of every run.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 19.0A` Project commercial core (ADR-0150) | Project client, PO reference, budget, rate-card pin (Released cards only), dates, project manager; `RateCard` cost rate per grade; `TimesheetEntry` (rates frozen at entry, `InvoicedBy` set once) and `DeliverableCompletion` (once only) Kinds; working pattern per principal; weekly timesheet view, Deliverables tab, Commercial pickers | 2026-09-10 |
| `WP 19.1A` Outbound invoicing (ADR-0151) | `IInvoicingConnector`; `InvoiceRequest` with the request id as idempotency key and a nine-state lifecycle; `InvoicingService`; `FakeInvoicingConnector`; DPAPI secret store; reconciliation poller; OAuth 2.0 with PKCE over a loopback listener; `XeroConnector` and `QuickBooksOnlineConnector` with contract tests against recorded responses; the Invoicing area; connector authorisation in Settings | 2026-09-10 |
| `WP 19.1B` KPI read models and Home cockpit | One `KpiSnapshot` per period; the five equations from `ADR-0150` with hand-computed fixture tests; period presets persisted; the engineering placeholder cards removed | 2026-09-10 |
| `WP 19.2A` Desktop composition | `MainWindowComposer` (BuildViews → BuildCoordinators → Wire → Layout); `MainWindow.cs` 1,475 → 595 lines; no null-forgiving field capture; area registry; `CommandIds` constants | 2026-09-10 |
| `WP 19.2B` The honest rail | The rail reads Home, Projects, Evidence, Timesheets, Invoicing, Reports, Engineering Calculations, Settings — nothing declared-but-empty; Engineering is the project's Structure tab; Reports (issued sheets, project documents) and Settings (the dialog's sections as an area) are real areas; the ribbon compacts to icons below 1,200 px; 122 controls gained automation names with a structural test; `RailSurfaceContractTests` runs six behavioural checks per rail entry; two defects found at merge and fixed: the Structure tab's embedded surface steered the project tab strip through bubbled selection events, and the Commercial section now shows the client's name and the card's code through the release branch's resolvers | 2026-09-10 (6d0ceaa) |
| `WP 19.3A` Layout verification in CI | In-process walk under headless Skia of every rail entry and project tab at two sizes; bounds and overlap checks; `layout-screenshots` artifact; it found and fixed a rail title overflow, a status-bar squeeze and a clipped tab strip on its first runs | 2026-09-10 |
| `WP 19.9.0` Release | *(this document, the physical review, the tag)* | |

## Figures

| Measure | `v0.18.0` | `v0.19.0` |
|---|---|---|
| Live source lines (`src/`, excluding `Frozen/`) | 113,343 | 126,156 |
| Live test lines (`tests/`, excluding `Frozen/`) | 99,709 | 107,824 |
| `Tempest.Core` source lines | 54,366 | 59,472 (timesheets, deliverables, invoicing, connectors, reconciliation, KPI read models) |
| Core tests | 3,991 | 4,255 |
| Desktop tests | 532 | 550 (commercial section, timesheets, invoicing and rail-contract journeys; automation-name coverage; the layout walk; the status-bar collapse) |
| ADRs | 149 | 151 (0150, 0151 new) |
| Commits on the release branch | 68 | 70 since `release/v0.18.0`'s candidate head `8df3466` |

## What changed for a developer

- One new NuGet package, `System.Security.Cryptography.ProtectedData`
  (Microsoft), for DPAPI on Windows; tokens live under
  `<persistence root>/secrets/`, never in the database.
- `IHostedService` (the platform's own) has its first real consumer, the
  invoice reconciliation poller, driven by `TimeProvider`.
- The layout walk is a normal Desktop test (`Category=LayoutWalk`) that CI
  reruns on the Release leg to publish screenshots.

## Fixed during the release gate (`WP 19.9.0`)

- **One live invoice request per source** (333fad6): completing a
  deliverable raises the request through the completion hook; Raise
  invoice on the same completion raised a second Draft with the same
  lines. Refused now, naming the first (`ADR-0151`, last addendum).
- **The status bar re-budgets its collapse when a segment's text grows**
  (9e11551): the layout walk at 1180×760 found the message area 11 px
  short once a long Explorer area title landed in AREA; the bar's own
  measure had never re-run. Found by the walk, fixed with a unit test.
- **Two opens told apart** (333fad6): `MainWindow` records every
  open-right-up phase with the object's id, and a failed open is reported
  in the status bar rather than swallowed.

## Warnings

- **Sending an invoice is sequential, not one transaction**: status,
  connector call, outcome, then one link per line. `ADR-0151` §5 records
  why; a crash between steps is reconciled by reference on the next poll.
- **QuickBooks Online needs an item reference** the product has no
  catalogue for; `Invoicing:QuickBooksOnline:DefaultItemId` is the opt-in.
- **Sandbox registration**: the OAuth loopback redirect URI is now fixed
  rather than a fresh ephemeral port every run — register
  `http://127.0.0.1:49301/callback/` exactly in the Xero or QuickBooks
  Online sandbox app console. Configure `Invoicing:OAuth:LoopbackPort` to
  use a different port; `0` keeps the old ephemeral behaviour (tests only
  — a real sandbox app needs one fixed URI registered ahead of time). If
  the configured port is already in use, connecting reports the exact
  port and configuration key rather than failing silently. See `ADR-0151`'s
  own addendum.
- **Completing a deliverable raises the invoice request itself** (the
  completion hook, `ADR-0151` §7). Raise invoice on the Deliverables tab
  is the retry for a completion whose hook refused (no client, no
  rate-card pin); on a completion a live request already carries it now
  refuses, naming that request. Before this guard it raised a second Draft
  with the same lines — found by the Desktop journey one run in two, fixed
  at `WP 19.9.0`, `ADR-0151`'s own last addendum.
- *(further warnings filled at `WP 19.9.0`)*

## Related

- [Execution Plan](Execution%20Plan.md)
- [v0.18.0 Release Notes](../v0.18.0/Release%20Notes.md)

# v0.19.0 Consultancy Seam and Desktop — Execution Plan

**Written by:** the chief engineer of record, 2026-09-10, while `v0.18.0`
is under the Product Owner's manual test.
**Governs:** how the seven Work Packages of `docs/releases/v1.0.0/WorkPackages.md`
§`v0.19.0` are executed as one set of work, by sub-agents at the lowest
useful model, to a release candidate ready to merge once `v0.18.0` is
accepted.
**Base and branch:** `release/v0.19.0` branched from the `v0.18.0`
release candidate (`8df3466`); any `v0.18.0` hotfix from the Product
Owner's testing is merged forward into it. One branch on origin; agents
work in throwaway local worktrees merged `--no-ff` and deleted the same
hour, as for `v0.18.0`.
**Method:** the `v0.18.0` method. Seam maps before briefs, one worktree
per Work Package, a compiling checkpoint early, the full gate at every
merge, Sonnet for every implementation agent, an audit before the tag
that verifies every closure claim against the code.

## 1. What the Product Owner gets

A build titled `TempestOS 0.19.0 (<commit>)` in which:

1. A project carries its client, purchase-order reference, budget, pinned
   rate card (billing and cost rate per grade), dates and project manager.
2. Time is recorded against a project by the week, each entry freezing the
   billing and cost rate it was recorded at; a deliverable is completed
   once, with the issued evidence and documents it delivers.
3. Completing a deliverable raises an invoice request from the unbilled
   time and fixed-price value; the engineer reviews it and sends it to
   Xero or QuickBooks Online through a connector that treats every failure
   as a state, not a retry; a fake connector drives the tests and the
   smoke test when no sandbox is authorised.
4. The Home cockpit shows utilisation, margin per project, work in
   progress, days sales outstanding and calc throughput, each defined in
   `ADR-0150` before its card was built, with period selection.
5. The rail is honest: Home, Projects, Evidence, Timesheets, Invoicing,
   Reports, Settings, and Engineering Calculations (kept on the Product
   Owner's instruction); Engineering is a project's Structure tab; the
   five declared modules that never worked are gone rather than dimmed.
   Reports lists issued evidence sheets and project documents; Settings
   holds the persistence root, principal override, connector
   authorisation, working pattern and theme. The rail folds below
   1,200 px and the ribbon compacts to icons.
6. Every screen is walked, screenshotted and checked for overlapping or
   escaping controls by CI on Windows, with the screenshots published for
   the physical review.

## 2. Waves

| Wave | Work Package | Depends on | Model | Files it owns |
|---|---|---|---|---|
| 1 | `WP 19.0A` part 1, commercial core model | — | Sonnet | `Project` fields, `RateCard` cost rate, new `Core/Timesheets`, `Core/Deliverables`, `ProjectCommercialService`, Workspace registrations, editor declarations, `ADR-0150` (with the five KPI equations), tests |
| 1 | `WP 19.2A` Desktop composition | — | Sonnet | `MainWindowComposer` (BuildViews → BuildCoordinators → Wire → Layout), `MainWindow.cs`, coordinators, `SurfaceCommandPolicy` constants, area registry |
| 1 | `WP 19.3A` Layout walk | — | Sonnet | `tests/Tempest.Desktop.Tests/Layout/**`, helper extension, CI steps and screenshot artifact |
| 2 | `WP 19.0A` part 2, timesheet and deliverables views | 19.0A p1, 19.2A | Sonnet | `Views/TimesheetWeekView.cs`, `Views/ProjectDeliverablesView.cs`, a `Timesheets` area through the registry, the project's Deliverables tab |
| 2 | `WP 19.1A` part 1, invoicing model | 19.0A p1 | Sonnet | `Core/Invoicing`: `IInvoicingConnector`, `InvoiceRequest` Kind and lifecycle, idempotency key, `FakeInvoicingConnector`, token store abstraction with DPAPI on Windows, reconciliation poller as a hosted service, deliverable completion raising a request, `ADR-0151` |
| 3 | `WP 19.1A` part 2, real connectors | 19.1A p1 | Sonnet | `XeroConnector`, `QuickBooksOnlineConnector` over `HttpClient`, OAuth 2.0 authorisation code with a loopback listener and the system browser, contract tests against recorded responses |
| 3 | `WP 19.1A` part 3, Invoicing area | 19.1A p1, 19.2A | Sonnet | `Views/InvoicingView.cs`: requests, lines, send, status, re-authorise; area entry |
| 3 | `WP 19.1B` KPI read models and Home cockpit | 19.0A p1, 19.1A p1 | Sonnet | `Workspace/Kpi/*` snapshot read models with fixture tests pinning the equations, cockpit cards with period selection, engineering placeholder cards removed |
| 4 | `WP 19.2B` The honest rail | 19.0A p2, 19.1A p3, 19.2A | Sonnet | `ShellArea` additions (Timesheets, Invoicing, Reports, Settings), rail descriptors, Engineering as Structure tab, Reports view, Settings area, compact ribbon, accessibility items, the six behavioural checks per surface as tests |
| 5 | `WP 19.3A` re-point, `WP 19.9.0` release | all | lead | walk re-pointed at the final rail; backlog audit; release notes; `PHYSICAL_REVIEW.md` §7b; VERSION 0.19.0; the RC report |

Effort: 19.0A 6, 19.1A 9, 19.1B 4, 19.2A 4, 19.2B 6, 19.3A 3, 19.9.0 2; 34 days.

## 3. Engineering decisions taken while planning

1. **Engineering Calculations stays on the rail** (Product Owner, 2026-09-09) even though the `WP 19.2B` row's list omits it.
2. **The layout walk runs in-process under Avalonia.Headless with Skia**, not by driving the built executable through OS automation, which would need a dependency the programme does not name. Automation names are still what the walker uses to find things; screenshots come from the headless renderer.
3. **`WP 19.1A` is three parts.** The model, the fake connector, the token store and the poller first, so `WP 19.1B` and the Invoicing area can build on them while the real connectors are written; the real connectors carry contract tests against recorded responses and are exercised by hand against a sandbox organisation.
4. **Areas are added through a registry** (`WP 19.2A`), so Timesheets, Invoicing, Reports and Settings each arrive as one entry.
5. **The Deliverable completion is a new Kind**, distinct from the existing milestone-parented `Deliverable`, which stays as it is.
6. **Per-principal settings use a composite key** (`Timesheet.WorkingPattern:{identityId}`) because the settings substrate has no enumeration.
7. **KPI read models scan the object-state collection inside one read transaction** and filter in memory; the query store has no field filtering, and the volumes of a consultancy do not need one yet.

## 4. Questions the Product Owner will need to answer before the physical review (not before work starts)

1. **Sandbox accounting organisations.** The real connectors need a Xero app (client id and secret, redirect `http://localhost:<port>/callback`) and a QuickBooks Online sandbox company with an app registration. The fake connector carries every test and the smoke test until then; the physical review's "send to a sandbox Xero organisation" step needs the registration. Default: the review runs on the fake connector, and the sandbox step is marked "when authorised".
2. **Currency.** Default: `GBP` for budgets and rate cards, with `CurrencyCode` free.

## 5. Risks

| Risk | Mitigation |
|---|---|
| `WP 19.2A` rewrites `MainWindow.cs` while wave 1 runs | 19.0A part 1 and 19.3A are told not to touch it; wave 2 starts from the merged composer |
| OAuth against real providers cannot be automated | contract tests use recorded responses; a fake connector covers every lifecycle state; the real path is a physical-review step |
| Rail removals break tests that pinned the declared modules | the seam map lists every such test; 19.2B updates them with rationale |
| Headless capture unavailable | 19.3A falls back to `RenderTargetBitmap`, else stops and reports |

# v1.0.0 — Work Packages (Release Candidate Programme)

## Status

**Proposed, 2026-09-08; amended the same day after Product Owner review**
(five surgical amendments: binary-payload transaction consistency in
`WP 17.1B`; checker-independence semantics in `WP 17.2A`/`WP 18.2B`; a
frozen expression grammar and early golden examples in `WP 18.0A`; invoice
idempotency and failure states in `WP 19.1A`; KPI equations and cost-rate
provenance in `WP 19.1B`; plus snapshot coherence in `WP 18.1A`, citation
integrity in `WP 18.0B`, behavioural acceptance in `WP 19.2B`, the macOS
claim in `WP RC.0A`, and an explicit parallelism assumption under the
programme total). Supersedes the 2026-09-04 proposal that stood in
this file, which was derived from the `v1.0.0 Release Candidate Audit` and
assumed the code shape at `v0.15.0` was the shape to release. The full
review of the tree at `f19e231` (build, tests, running app, every
namespace, the registers and the git history) found that three substrates
are the wrong shape for the stated business, that four layers are
speculative, and that the governance process now costs more than the
product. This programme replaces them rather than certifying them.

Nothing below has begun. `VERSION` is `0.16.0`. Every row is scoped to be
demonstrable in the running Desktop application in under two minutes by a
person who did not build it; that demonstration is the acceptance test.

## What v1.0.0 is

**A single-user, locally-trusted desktop system of record for a small
engineering consultancy**, in which an engineer can:

1. Open a project for a client, with a purchase-order reference, a budget
   and a pinned rate card.
2. Author a calculation sheet from named inputs, expressions and cited
   reference tables; run it; have the sheet, its inputs, its intermediate
   values and every reference revision it stood on persisted as one
   immutable record.
3. Have a second principal independently check the sheet, and issue it as
   a PDF calc sheet attached to the project as evidence.
4. Record time against the project and, on marking a deliverable complete,
   emit an invoice request to Xero or QuickBooks.
5. See utilisation, margin per project, work in progress and days sales
   outstanding on the Home cockpit.

Everything that survives from the current tree is kept because it serves
one of those five sentences. Everything that does not is frozen, archived
or deleted.

**Explicitly out of v1.0.0, unchanged from `D-021`:** multi-user, cloud
synchronisation, third-party plugins, inbound REST beyond loopback, the
Companion application, regulated-environment compliance, and any
Engineering Intelligence, Knowledge or Commercial Intelligence surface.

## The standard that does not change

Every Work Package still ships with: a Release build at 0 warnings and 0
errors under `TreatWarningsAsErrors`; every test passing in both
configurations on Windows CI; the five architecture invariants
(`DependencyDirectionTests`) green; an ADR for any decision that
constrains future code; a row in `PHYSICAL_REVIEW.md` §7 for any new user
surface; and a Release Notes line. What changes is the *form* of the
record (a PR, not a report) and the *volume* (capped, see `WP 17.0B`).

## Sequencing rule

Substrates before surfaces. The calculation-sheet model (`v0.18.0`) sits on
SQLite (`v0.17.0`) and the dimension vector (`v0.17.0`); the business seam
(`v0.19.0`) sits on the sheet model because an invoice line cites an
issued sheet; the Desktop rework (`v0.19.0`) waits for the async read
surface because every new screen would otherwise inherit the sync-over-
async debt. Effort is in developer-days for one developer working with
agents; a five-day week is assumed.

---

## Release `v0.17.0` — Reset and Substrates

**Goal.** Stop the bleeding, cut the process to what a second human needs
on day one, and replace the persistence, configuration, logging and units
substrates so that every later Work Package builds on ground that survives
a power cut. Expected duration: six weeks.

| Work Package | Scope | Type | Closes | Effort |
|---|---|---|---|---|
| `WP 17.0A` | **Immediate defects.** Set `Grid.Column` on the `ScrollViewer`s, not the panels, in `EngineeringCalculationView.BuildLayout` (both columns currently render in column 0); extend `AssertUsable` in the Desktop tests to fail when any two sibling controls' `Bounds` intersect and to assert the right column's `Bounds.X` ≥ its declared offset; open the temporary file in `PersistenceStore.WriteAtomicallyAsync` with `FileOptions.WriteThrough` and flush before the rename; in `HostedServiceManager.StartServiceAsync` resolve the instance before the `try` so a critical service whose constructor throws is Host-fatal; make `TempestServiceProvider`'s default-parameter fallback log at Warning and apply only to nullable reference parameters; scale every fixed render deadline in `Tempest.Desktop.Tests` by a `TEMPEST_TEST_TIMEOUT_FACTOR` environment variable (CI sets 3); rewrite `PersistenceStoreTests` so no test touches `PersistenceStore.DefaultRootPath`; delete untracked `main.log` and `persistence-data.bak/` and add both patterns to `.gitignore`; correct the "well under a minute" duration claim in `ci.yml`. | Fix | `TD-137`, `TD-166` residual, the `TD-119` recurrence, the unregistered defect in `HostedServiceManager` | 3 |
| `WP 17.0B` | **Governance reset.** Move `docs/academy/03 Work Packages/`, every file under `docs/releases/v0.*/` except each release's `Release Notes.md`, every review board disposition, status report, readiness review and completion certification, `docs/academy/01 Engineering Principles/`, and every register under `docs/governance/` except a new slim `BACKLOG.md` into `archive/docs-2026-09/` with a one-paragraph README explaining what they were. Replace `PROJECT_STATUS.md` with a one-screen status. Write `CONTRIBUTING.md` (one page): a Work Package is a branch and a PR; the PR description is the retrospective; one review per PR, one round, findings fixed or filed; a PR may not add more Markdown lines than code lines; "Release Blocking" means data loss a user can reproduce from the UI. Triage the 93 open Technical Debt rows into `BACKLOG.md` (cap 30, each mapped to a Work Package below or marked archived) and freeze the 468 KB register as history. Reduce `governance-healthcheck.ps1` to the six checks derived from `src/` (ADR files, interfaces, exceptions, namespaces, VERSION, release folder) plus a new Markdown-versus-code line check on each PR; delete the ten Markdown-versus-Markdown checks. Configure branch protection on `main` in GitHub (required `CI Gate`, CODEOWNERS review) and record the settings page in `CONTRIBUTING.md`. Keep `FOUNDATION.md`, `VISION.md` (trimmed to one page), the ADR folder (frozen; new ADRs still written), `PHYSICAL_REVIEW.md`, `README.md`, and the ten `docs/architecture/` documents the code still depends on. | Governance | `TD-45`, `TD-123`, `TD-124`, `TD-125`, `TD-127`, `TD-151`–`TD-153`, `TD-164` | 4 |
| `WP 17.0C` | **Test-suite consolidation.** One `RunningHostFixture` replacing the sixty copy-pasted `await Task.Delay(5)` host-polling loops; a `RecordingLogger` sink replacing every `Console.Out` capture so the 83 files under the console-capture collection run in parallel; convert `Assert.Equal(N, x.Count)` fixture pins in `SeedDatasetTests`, `BracketScenarioTests` and `GovernedBracketCheckTests` to property assertions; delete or invert the six defect-characterisation facts in `R7FalsificationTests` (a passing test that documents a bug is a backlog row wearing a test's clothes); trim the agent-workflow narrative in the R7 files to what the next reader needs; add Stryker.NET scoped to `Tempest.Core/Calculations` and `Tempest.Core/UnitsAndQuantities` with a CI threshold, and stop writing "mutation-proved" anywhere a tool did not produce the number; enable the coverage collector already referenced in both test projects and publish the figure in the CI summary; record suite duration per assembly in the CI summary. Target: Core suite under three minutes, Desktop suite under six, on the CI runner. | Tests | `TD-34`, `TD-114`, `TD-119`, `TD-150` | 5 |
| `WP 17.1A` | **SQLite persistence (ADR-0144).** `Microsoft.Data.Sqlite`, one database file per persistence root, WAL journal, `synchronous=FULL`. `SqlitePersistenceStore` implements the existing `IPersistenceStore` and `IBinaryPersistenceStore` unchanged, plus a new `IQueryablePersistenceStore` (list keys by prefix, read many, write many in one transaction, delete many, and a secondary-index table so catalogues stop scanning). Every existing `PersistenceStoreTests`, `PersistenceStoreHostileNameTests` and golden-corpus test passes against the new store. A one-shot importer reads an existing `persistence-data/` tree into the database on first launch and renames the old tree `persistence-data.imported`. Cross-process safety comes from SQLite's own locking; a second TempestOS instance on the same root gets a clear refusal, not interleaved writes. `PersistenceStore` (file-per-key) is retained behind a configuration switch for one release, then deleted in `v0.18.0`. | Substrate | `TD-12`, `TD-18`, `TD-20`, `TD-36`, `TD-59`, `TD-67`, `TD-88`, `TD-137`, `TD-149`, `TD-156` | 6 |
| `WP 17.1B` | **Transactional engineering object store (ADR-0145).** One store is authoritative: object state, document revisions and relationships are written in a single SQLite transaction by `EngineeringObjectFactory.CreateAsync`, `LinkAsync`, `MoveAsync`, `DeleteAsync` and every mutator, with graph invariants (no parent cycle, no delete with live children, supersession) checked inside the transaction under a project-scoped write lock. `InMemoryEngineeringObjectRepository` and `InMemoryEngineeringRelationshipRepository` become a read-through cache invalidated by the transaction, never a co-equal writer. Delete `RollBackOnFailureAsync`, `MutationRollbackPoint`, `EngineeringObjectStateStore`'s per-object lock choreography and `AttachmentWriteIntentStore`; `EngineeringObjectRehydrationService` becomes a lazy, per-kind cache warm-up. Reduce `EngineeringObjectBase` from 1,819 lines to the facet plumbing that remains, and collapse the three verification models (`Core/Verification`, `EngineeringDomain/RequirementsVerification`, `EngineeringAssets/Verification`) to the one `VerificationArtefact` model that the calc-sheet chain uses. The R7 per-mutator regression facts are re-pointed at the transactional path and must still pass. **Binary payload consistency (decided here, not discovered mid-migration):** attachment and document bytes are SQLite BLOBs in the same database, written in the same transaction as the `Document` row that references them, so a committed `Document` can never reference an unavailable payload and a failed transaction leaves no orphaned bytes; `IBinaryPersistenceStore` is implemented over the BLOB table; content is verified by SHA-256 on read exactly as today; payloads above 256 MB are refused with a clear message in v1.0 (a calc-sheet PDF is under 5 MB, a scanned drawing under 50 MB); `AttachmentWriteIntentStore` and the reconciliation sweep are deleted because the failure they compensated cannot occur. Acceptance: a fault-injection test that kills the process between the BLOB write and the commit, on restart finds neither the `Document` nor the bytes; a second kills it after commit and finds both. | Substrate | `TD-23`, `TD-32`, `TD-68`, `TD-86`, `TD-95`–`TD-97`, `TD-135`, `TD-136`, `TD-138`–`TD-148`, `TD-158`, `TD-170` | 8 |
| `WP 17.2A` | **Platform trim (ADR-0146).** Adopt `Microsoft.Extensions.Configuration` (`appsettings.json` in the persistence root, environment variables, command line) so `Runtime:*`, `Identity:*` and `Persistence:RootPath` are reachable by an operator; adopt `Microsoft.Extensions.Logging` with a rolling file sink in the persistence root and Information-level chatter in DI resolution, event publish and command dispatch reduced to Debug. Move `Tempest.Core/Plugins` (signing, trust store, capability enforcement), `Tempest.Core/Api`, `Tempest.Core/Licensing` and the plugin-trust branches in `EventBus`, `CommandRegistry`, `CommandHandlerTable`, `IdentityService` and `NavigationService` to `src/Frozen/` outside the solution build, with their tests; keep `PluginManifestDiscoveryService` frozen in place for option value. Collapse `Identity` to `ISessionPrincipal` with a **stable identity id** (the OS security identifier on Windows, the user id elsewhere, optionally mapped to a configured person id), a display name, and a **role** (Engineer or Checker) that is separate from the identity. The identity id is what every audit row, authorship field and check record stores; the role governs which commands are offered. A configuration override may change the display name or role; it cannot change the identity id, which is read from the OS each launch and never from configuration. Keep `IPermissionEvaluator` as a one-line seam. Make `IAuditRecorder` real: every calc-sheet run, check, issue, material release, invoice request and project status change writes an audit row, queryable by object and by date through the new index table. The custom DI container is kept and frozen. | Substrate | `TD-01`–`TD-04`, `TD-06`, `TD-13`–`TD-16`, `TD-49`–`TD-56`, `TD-61`, `TD-62`, `TD-64`, `TD-69`, `TD-94`, `TD-103`, `TD-129`, `TD-130` (by removal or by closure) | 6 |
| `WP 17.2B` | **Repackage the workspace layer.** `Tempest.App` becomes `Tempest.Workspace` (class library) holding `Workspace/`, `Projects/`, `Shell/`, `Engineering/`, `Composition/`; `WorkspaceShell.cs` and `Program.cs` move to `Tempest.Harness` (console exe). Remove `InternalsVisibleTo("Tempest.Desktop")` by promoting `Workspace.Cockpit` to `IWorkspace`. The dependency-direction tests gain the new project names. `ADR-0101` is amended in place to record the code change it originally declined to make. | Refactor | `TD-75` residual, `TD-162` | 2 |
| `WP 17.3A` | **Units as a dimension vector (ADR-0147).** A runtime `Dimension` record of seven integer exponents (L, M, T, Θ, I, N, J) with multiplication and division; a non-generic `Quantity` (value, unit, dimension) that converts across units of the same dimension automatically in `+`, `−`, comparison and equality, and multiplies and divides across dimensions; a `TemperatureDelta` unit family so ΔT arithmetic is legal while absolute-Celsius addition still throws; derived dimensions the current library lacks (second moment of area, section modulus, frequency, stress distinct from pressure by alias, electrical base set); JSON round-trip; and the existing `Quantity<TDimension>` kept as a compile-time typed facade over it so no existing definition or record breaks. Property-based tests (CsCheck) over every unit pair: conversion round-trip, invariance of every calculation result under input-unit change, monotonicity of the bracket margin in load. UnitsNet was evaluated and not adopted because its quantities cannot carry a `ReferencePin`. | Substrate | `TD-19` residual, `TD-29` precondition | 5 |
| `WP 17.9.0` | **`v0.17.0` release.** Physical review on Windows per `PHYSICAL_REVIEW.md` §7 with the new persistence root; three consecutive full-suite runs on the Windows CI runner under the `TEMPEST_TEST_TIMEOUT_FACTOR`; Release Notes; tag; `release.yml` publish. No Engineering Readiness Review document: the PR set, CI history and the recorded physical review are the record. | Release | — | 2 |

**Release total: 41 developer-days.**

---

## Release `v0.18.0` — Calculation as Document

**Goal.** Make the calculation the product. A sheet is an authored,
versioned document that cites its sources, retains its inputs and can be
re-run, diffed, checked and issued. The six compiled definitions become
built-in functions. Expected duration: seven weeks.

| Work Package | Scope | Type | Closes | Effort |
|---|---|---|---|---|
| `WP 18.0A` | **Calc-sheet model and expression engine (ADR-0148).** `CalcSheet` is a canonical Kind: ordered named cells, each an input (`Quantity` with optional `ReferencePin`), an expression, a table lookup, a check (criterion, comparator, limit) or an output. **The expression language is deliberately boring and frozen in `ADR-0148` before the first line of parser is written**, as an EBNF grammar plus a semantics table covering: identifiers (cell names only; a cell may reference other cells and table lookups, nothing else); literals with unit suffixes parsed invariant-culture with `.` as the decimal separator; `+ − × ÷ ^` with `^` restricted to integer or rational exponents so the dimension vector stays integral; function calls to the fixed built-in set; `if(cond, a, b)` as the only conditional, with both branches dimension-checked; evaluation in topological order of the cell dependency graph, which must be acyclic (a cycle is a sheet validation error naming the cells); an undefined identifier, a division by zero, a dimension mismatch and a domain error (`sqrt` of a negative) are each a typed error on the cell that propagates to every dependent cell as `Indeterminate`, never as a number; IEEE double arithmetic with no implicit rounding, and display precision declared per cell; a check compares two dimension-compatible quantities with a declared comparator and an optional relative tolerance. The grammar gains nothing in v1.0 that is not in the ADR. Built-in functions: the six existing definitions (`BoltShear`, `BeamBending`, `BearingStress`, `VesselWall`, `MaterialMargin`, `BracketSection`) exposed as functions over `Quantity`, plus `min`, `max`, `abs`, `sqrt`, `interp`. `CalcSheetRun` is an immutable record: the sheet revision, every input value and pin, every evaluated cell, every check outcome, the engine version, the executing principal and the timestamp; `ICalculationEngine.ExecuteAsync` is re-implemented over it so `CalculationRecord` gains `Inputs` and re-run from a record is one call. Sheets and runs are diffable cell by cell. **Correctness evidence starts here, not at RC:** two published worked examples (one bending, one bolted joint) are authored as sheets and reproduced to their stated precision in this Work Package's own tests, with the source cited. `CalculationTemplateRegistry`, `GovernedBracketCheckService`, `BracketEngineeringRecordService`, `BracketCalculationWorkbench` and `BracketCalculationContracts` are deleted once `WP 18.2A` reproduces the bracket journey on the sheet model. | Substrate | `TD-21`, `TD-22`, `TD-29`, `TD-30`, `TD-159`, `TD-167`–`TD-169` | 12 |
| `WP 18.0B` | **Reference tables with structured citation and interpolation (ADR-0149).** `ReferenceTable` is a reference-data Kind: named columns with dimensions, rows, an interpolation policy per column (none, linear, log-linear, nearest, refuse), and a `SourceCitation` (publisher, work, edition, page, table or figure, row or entry). `ReferenceQuantityValue.Conditions` becomes structured: a list of `(property, comparator, quantity)` bands rather than prose, so a material's yield strength is selected by thickness at lookup time and an 80 mm S355J2 bar gets 325 MPa, not 355. Migrate `MaterialSeed`, `FastenerSeed`, `BearingSeed` and `ConstantSeed` to the new shapes; every seed row carries a citation; `StandardSeed` titles sourced from a tertiary index are marked `Unverified` until checked against the standard. Every seeded library reaches the product through one `Populate reference libraries` action. Authoring format: a `.tempest-table.json` file per table so an engineer can author a table outside the application and import it. **Citation integrity is an invariant, not an intention:** a sheet run stores, for every reference-derived value, the table revision, the exact row and the condition band that selected it, and the citation; a released table revision is immutable; a superseded revision remains readable by pin forever; and a run whose pins all resolve reproduces byte-for-byte the same values on re-run. Acceptance: supersede a table after a run, re-run from the record, and get the original values with the original citation, while a fresh run of the same sheet gets the new values and the new citation. **Machinery's Handbook and standards content:** tables are cited, never reproduced wholesale; each imported table records who transcribed it and from which edition and page, and the PDF report cites the source rather than reprinting it. Licensing of any transcribed table is the operator's responsibility and the import dialog says so. | Substrate | `TD-17`, `TD-155`, `TD-157`, `TD-163` | 6 |
| `WP 18.1A` | **Async workspace read surface and change notification.** The invariant is not "everything returns Task"; it is **no blocking UI-thread access to persistence or workspace operations, and every view renders one coherent committed snapshot.** Every transaction commit increments a store sequence number; `IWorkspaceChanges.Changed(WorkspaceChange)` is raised once per commit carrying that sequence, the object id, kind and change type; a view responds by taking one `WorkspaceSnapshot` read at that sequence (a single `Task<Snapshot>` per view per change, returning an immutable record composed inside one read transaction), never by composing several independent reads that could straddle a commit. `*CockpitReadModel` and `EngineeringCockpit` are rewritten to that shape; every Desktop view subscribes and refreshes itself from its snapshot; a `UiThreadRelay` applies `CheckAccess ? invoke : Post` at the one App-to-Desktop boundary; a test fails the build on any `GetAwaiter().GetResult()` or `.Result` in `Tempest.Desktop` outside `App.cs`; startup runs off the UI thread behind a splash. Delete the sixteen cockpit-refresh and seventeen explorer-reload call sites. | Refactor | `TD-58`, `TD-66`, `TD-90`, `TD-108`, `TD-111`, `TD-117`, `TD-118`, `TD-121` | 6 |
| `WP 18.2A` | **Calc-sheet editor.** One schema-driven `CalcSheetView` replacing `EngineeringCalculationView`: a cell grid (name, expression or input, unit, value, dimension, status), an input panel with typed quantity entry and unit picker, a reference picker that pins a library record revision or a table row, a checks panel, a run button, a run history with cell-by-cell diff between any two runs, and the sheet's own revision history. Every one of the six built-in calculations is drivable from a template sheet shipped with the product; the bracket journey (`EngineeringCalculationJourneyTests`) is reproduced on the new surface and the old surface is deleted. Layout is asserted by intersection, not visibility. | Surface | `TD-79` (calculations), `TD-160` (calculations), `TD-165` | 8 |
| `WP 18.2B` | **Independent check and issue.** Sheet lifecycle `Draft → Checked → Issued → Superseded`; a check is recorded against a specific run, with the checker's own independently entered figures compared cell by cell to the run (tolerance declared on the sheet). **What "independent" means in v1.0, stated so the claim matches the evidence:** the check record stores the checker's identity id (`WP 17.2A`) and is refused when it equals the run's author identity id; role does not enter the comparison, so switching a configuration to "Checker" on the same OS account does not make a check independent; two OS accounts on one machine do. This is a workflow and data constraint, not an authentication guarantee: v1.0 does not claim to enforce organisational segregation of duties, and `WP RC.0C` says so; a `Requirement` gains a quantified acceptance criterion (`property`, comparator, `Quantity`) so a requirement, a sheet check and a `VerificationArtefact` are linked and evaluated numerically; issue is refused without a passed check and an audit row is written for every transition. | Surface | `TD-25`, `TD-31`, `TD-38` (for sheets) | 4 |
| `WP 18.3A` | **Calc-sheet PDF report.** QuestPDF-rendered report from an issued run: title block (project, client, sheet reference and revision, author, checker, issue date), inputs with units and pins, every cell with expression, value and dimension, every check with outcome, every reference citation with library, record, revision and source page, assumptions and stated limits, and a signature block. Saved as a `Document` of classification `CalcSheet` attached to the project through the existing attachment store, opened in the existing document viewer, and exportable to a file. The report is regenerated from the run, never edited. | Surface | `TD-98` partial, `TD-160` (documents) | 4 |
| `WP 18.9.0` | **`v0.18.0` release.** Physical review on Windows: author a sheet from a Handbook-derived table, run, check as a second principal, issue, open the PDF, restart, re-run from the record, diff. Three consecutive CI runs. Delete `PersistenceStore` (file-per-key). Release Notes; tag; publish. | Release | — | 2 |

**Release total: 42 developer-days.**

---

## Release `v0.19.0` — Consultancy Seam and Desktop

**Goal.** The thin business model a consultancy actually bills with, one
outbound accounting integration, KPIs as read models, and a Desktop whose
rail contains only what works. Expected duration: six weeks.

| Work Package | Scope | Type | Closes | Effort |
|---|---|---|---|---|
| `WP 19.0A` | **Project commercial core (ADR-0150).** `Project` gains client (`Organisation`, kept from `BusinessOperations.Crm`), purchase-order reference, budget (`Money`, kept from `BusinessGovernance`), rate-card pin (`RateCard`, kept), start and target dates and a project manager principal. `RateCard` gains a **cost rate** per grade beside its billing rate (the loaded cost of an hour of that grade, set by the operator), so one pinned card answers both what an hour bills at and what it costs. New Kinds: `TimesheetEntry` (principal, project, task, date, hours, billable flag, **billing rate and cost rate both resolved from the pinned card and grade at entry time and frozen thereafter**, and an `InvoicedBy` link set once and never cleared), `Deliverable` completion event (project, deliverable, issued sheet or document references, completion date, principal; a deliverable can be completed once, and a second completion is refused with the first shown). Each principal has a working pattern setting (available hours per week) that utilisation reads. Both are canonical engineering objects with lifecycle, audit and rehydration. A weekly timesheet view and a project deliverables view in Desktop. | Model + Surface | `TD-76` residual, `TD-81` (Commercial) | 6 |
| `WP 19.0B` | **Archive P02 to P07.** Move `Tempest.Core/EngineeringIntelligence`, `Knowledge`, `CommercialIntelligence`, `BusinessOperations` (except `Crm/Organisation`, `Crm/Contact`, `Finance/Budget`), `BusinessGovernance` (except `Money`, `CurrencyCode`, `EffectivePeriod`, `Pricing/RateCard*`), and `EngineeringAssets` (except `Verification`) to `src/Frozen/` outside the solution build with their tests and seeds, and their six architecture documents and sixteen ADRs to `archive/`. `ADR-0127` to `ADR-0142` are marked Frozen, not superseded: the vocabulary is sound and comes back when a client pays for it. Reflection guards that referenced the archived namespaces are deleted. | Refactor | `TD-161`, `TD-162` | 2 |
| `WP 19.1A` | **Outbound invoicing connector (ADR-0151).** `IInvoicingConnector` (create draft invoice from an `InvoiceRequest`, read invoice status, list contacts) with `XeroConnector` and `QuickBooksOnlineConnector` over `HttpClient` and OAuth 2.0 authorisation-code flow launched in the system browser; tokens stored with DPAPI on Windows and the platform keychain elsewhere, never in the persistence database; a `Deliverable` completion raises an `InvoiceRequest` (client, PO reference, lines from unbilled timesheet entries and fixed-price deliverables at the pinned rates, currency from the client) that the engineer reviews and sends; the connector's invoice number and status are written back to the request and polled on a background hosted service. **Idempotency and failure states are in the model, not in a retry loop.** The `InvoiceRequest` id is the idempotency key: it is sent as Xero's `Idempotency-Key` header and QuickBooks Online's `RequestId`, and is also written into the invoice's reference field, so a lost response is resolved by looking the invoice up by reference before any retry. Request lifecycle: `Draft → Sending → Sent (external id known) → Accepted | Rejected (reason shown) | Voided`, plus `Unknown` (request sent, response lost; the poller reconciles by reference and moves it to `Sent` or back to `Draft`), `Reauthorise` (token expired or revoked; nothing is sent until the operator re-authorises), and `Unavailable` (connector unreachable; the request stays `Draft` and is not retried automatically). `Paid` is read from the accounting system only. Every timesheet entry and deliverable on a request gains its `InvoicedBy` link when the request reaches `Sent`, and a line already linked cannot appear on a second request. A `FakeInvoicingConnector` records requests, can be told to time out or reject, and drives the tests for every state above and the smoke test. Nothing in TempestOS marks an invoice paid. | Integration | `FCR-0018` in the outbound direction | 9 |
| `WP 19.1B` | **KPI read models and Home cockpit.** The equations and their provenance are written into `ADR-0150` before any card is built, so no number on the cockpit is undefined. **Utilisation** = Σ billable hours ÷ Σ available hours, per principal, over the selected period; available hours come from the principal's working pattern (`WP 19.0A`), not from calendar days. **Margin per project** = (Σ billable hours × frozen billing rate + Σ fixed-price deliverable value) − Σ all hours × frozen cost rate, over entries dated in the period, shown as a currency amount and a percentage of billable value; both rates come from the timesheet entry, never re-resolved. **Work in progress** = Σ billable value of entries with no `InvoicedBy` link, by project, with age from the oldest entry. **Days sales outstanding** = Σ over invoices in `Sent` or later of (paid date or today − invoice date) ÷ count, using dates read from the connector; where no connector is authorised the card reads "unavailable" and never zero. **Calc throughput** = sheets reaching `Issued` in the period. Each is a `Task<Snapshot>` read model over `TimesheetEntry`, `Deliverable`, `InvoiceRequest` and `Project` using the query store, with unit tests over authored fixtures that pin every equation above with hand-computed expected values. The Home cockpit shows the five KPIs with period selection; the engineering cockpit's placeholder KPI cards are removed. | Surface | `TD-33`, `TD-118` residual | 4 |
| `WP 19.2A` | **Desktop composition.** `MainWindowComposer` with `BuildViews → BuildCoordinators → Wire → Layout` phases replacing the 700-line constructor and every `_field!` lazy capture; `WorkspaceViewCoordinator` reduced to the collaborators it owns once `IWorkspaceChanges` exists; `SurfaceCommandPolicy` string literals replaced by the command-descriptor constants. | Refactor | `TD-105`–`TD-107`, `TD-109`, `TD-112`, `TD-113`, `TD-115` | 4 |
| `WP 19.2B` | **An honest rail and the remaining surfaces.** Rail entries: Home, Projects, Engineering, Calculations, Timesheets, Invoicing, Reports, Settings. Commercial, Resources, Knowledge and Administration are removed from the rail rather than dimmed; Reports lists issued calc-sheet PDFs and project documents; Settings holds the persistence root, principal override, connector authorisation and theme. Compact navigation below 1,200 px width; the ribbon compacts to icons rather than scrolling. Accessibility residual: dialog modality, `AutomationProperties.Name` on every interactive control, keyboard reach for Digital Thread edges and docking moves. **Acceptance is behavioural, not visual, for every surface on the rail:** click it and something happens; select it and the selection has meaning; open it and usable content appears; edit it and state changes; restart and the state remains; navigate away and back and the state is coherent. A surface that fails any of the six is removed from the rail rather than shipped dimmed. | Surface | `TD-65` residual, `TD-73`, `TD-74`, `TD-77` residual, `TD-81`, `TD-128`, `TD-132`, `TD-133` | 6 |
| `WP 19.3A` | **Layout verification in CI.** A Windows CI job launches the built Desktop, walks every rail entry and every project tab by automation id, screenshots each, and fails on any intersecting sibling `Bounds` or any control outside its parent; screenshots are published as artefacts for the physical review. The Linux `linux-launch-smoke` job is kept as a crash detector. | Tests | `TD-83`, the `WP 17.0A` overlap class | 3 |
| `WP 19.9.0` | **`v0.19.0` release.** Physical review on Windows: open a client project, record a week of time, issue a sheet, complete a deliverable, send the invoice request to a sandbox Xero organisation, see the KPIs move. Release Notes; tag; publish. | Release | — | 2 |

**Release total: 36 developer-days.**

---

## Release `v1.0.0` — Release Candidate and General Availability

**Goal.** Prove the whole journey on a clean machine, make installation
and upgrade a non-event, state the security posture, and ship. Expected
duration: three weeks.

| Work Package | Scope | Type | Closes | Effort |
|---|---|---|---|---|
| `WP RC.0A` | **Installer and upgrade.** Velopack-packaged Windows installer with in-place update from the GitHub Release feed; the persistence database is upgraded by the schema-version migrations on first launch and backed up beside itself before migration; a `--persistence-root` argument and a first-run dialog choose the data location; Support matrix stated to match the evidence: **Windows supported**; **macOS built and launch-validated, not production-supported unless subsequently validated**; **Linux build and test only**. `release.yml` publishes the installer. | Release engineering | `TD-116` disposition, `D-025` | 4 |
| `WP RC.0B` | **Golden-example coverage.** Every built-in calculation and every shipped template sheet reproduces at least one published worked example (Roark, Shigley, a Eurocode or ASME worked example, or the Handbook's own example where one exists) with the source cited in the test; Stryker threshold raised for `Calculations` and `UnitsAndQuantities`; a property test per built-in that its result is invariant under input-unit change. | Tests | `WP 17.0C` residual | 3 |
| `WP RC.0C` | **Security posture statement.** One document: single-user, local trust, OS-user principal, DPAPI-held connector tokens, outbound HTTPS only, no listener, what an attacker with the laptop can do, what the operator must do (disk encryption, OS account), and what is deliberately not defended. Replaces `Threat Model.md` and `Security Roadmap.md` for v1.0. Dependency vulnerability scan is a required CI check. | Documentation | `FCR-0003`/`FCR-0004` disposition for v1.0 | 2 |
| `WP RC.0D` | **Determinism and load.** Five consecutive full-suite runs on Windows CI while the layout-verification job runs concurrently; any failure is a defect to fix, not a matrix to re-run. Suite duration recorded. | Tests | `TD-119` class | 2 |
| `WP RC.0E` | **Physical review on a clean machine, recorded.** A person who did not build it follows `PHYSICAL_REVIEW.md` from a fresh Windows install through the five sentences in "What v1.0.0 is", using the installer, in under thirty minutes; every finding is fixed or filed in `BACKLOG.md` before tagging; the recording (screenshots and the reviewer's notes) is committed under `docs/releases/v1.0.0/`. | Verification | — | 2 |
| `WP RC.0F` | **Release.** `VERSION` → `1.0.0`; Release Notes summarising `v0.17.0` to `v1.0.0`; root `CHANGELOG.md`; tag via `new-release.ps1`; `release.yml` publishes the installer and the harness; Product Approval recorded as one line in the Release Notes by the Product Owner. | Release | — | 1 |

**Release total: 14 developer-days.**

---

## Programme total

| Release | Developer-days | Calendar |
|---|---|---|
| `v0.17.0` Reset and Substrates | 41 | weeks 1–6 |
| `v0.18.0` Calculation as Document | 42 | weeks 7–13 |
| `v0.19.0` Consultancy Seam and Desktop | 36 | weeks 14–19 |
| `v1.0.0` Release Candidate | 14 | weeks 20–22 |
| **Total** | **133** | **22 weeks** |

**The 22 weeks is a programme target, not a delivery date, and the
arithmetic behind it is stated so nobody mistakes sequencing for
contingency.** 133 developer-days in 22 calendar weeks (110 working days)
assumes roughly 1.2 parallel streams from agents working independent Work
Packages within a release; executed strictly serially it is 27 weeks.
Contingency is not added as a line; if a release slips, its surfaces slip
with it and the next release's substrate work still starts on time. **The
critical path is tracked explicitly** and is the chain
`WP 17.1A → 17.1B → 18.0A → 18.2A → 18.2B → 18.3A → 19.0A → 19.1A → RC.0A → RC.0E`;
everything else can run beside it. A slip on that chain is a slip on the
programme and is reported as such in the Release Notes of the release it
lands in.

## New ADRs this programme requires

| ADR | Decision | Work Package |
|---|---|---|
| `ADR-0144` | Persistence is SQLite in WAL mode with full synchronous writes; the file-per-key store is retired | `WP 17.1A` |
| `ADR-0145` | One store is authoritative; object state, revisions and relationships commit in one transaction; in-memory repositories are caches | `WP 17.1B` |
| `ADR-0146` | Configuration and logging are Microsoft.Extensions; plugin trust, inbound REST and licensing are frozen outside the build; identity is one session principal | `WP 17.2A` |
| `ADR-0147` | Units are a runtime dimension vector; the generic `Quantity<TDimension>` is a typed facade over it | `WP 17.3A` |
| `ADR-0148` | A calculation is an authored, versioned sheet; a run is a snapshot of the whole evaluated sheet including its inputs; compiled definitions are built-in functions | `WP 18.0A` |
| `ADR-0149` | Reference tables are first-class with structured citation and declared interpolation; value conditions are structured bands, not prose | `WP 18.0B` |
| `ADR-0150` | Time, deliverable completion and rate resolution are engineering objects on the project; billing and cost rates freeze at entry; the five KPI equations and their provenance are defined here; TempestOS holds no ledger | `WP 19.0A`, `WP 19.1B` |
| `ADR-0151` | Invoicing is an outbound connector to the accounting system; the request id is the idempotency key and the invoice reference; TempestOS creates draft invoices and reads status; it never marks anything paid | `WP 19.1A` |

`ADR-0056` (calculation purity) and `ADR-0086` (template registry) are
superseded by `ADR-0148`. `ADR-0041` and `ADR-0053` are superseded by
`ADR-0144`. `ADR-0054` is superseded by `ADR-0147`. `ADR-0107` to
`ADR-0112` (plugin trust) and `ADR-0047` to `ADR-0050` (REST, licensing)
are marked Frozen. `ADR-0127` to `ADR-0142` are marked Frozen.

## Disposition of the current Technical Debt Register

`WP 17.0B` performs the triage; the rule it applies is recorded here so
the outcome is predictable.

- **Closed by a substrate** (`WP 17.1A`, `17.1B`, `17.2A`, `17.3A`,
  `18.0A`): every row whose mechanism is the file-per-key store, the
  dual-write object model, the plugin trust seams, the marker-type units or
  the compiled calculation definition. About sixty rows. They close when
  the substrate lands and its tests pass, not by relabelling.
- **Closed by a surface** (`WP 18.1A`, `18.2A`, `19.2A`, `19.2B`): every
  Desktop row about refresh, threading, composition, the rail, layout or
  accessibility. About twenty rows.
- **Archived with the layer** (`WP 17.2A`, `19.0B`): every row about
  plugins, REST, licensing, or P02 to P07. They are not debt in a product
  that does not ship the layer. About fifteen rows.
- **Carried into `BACKLOG.md`**: whatever remains that a user could
  notice, capped at thirty, each with the Work Package that owns it.
- **`TD-147`** (creation registers an object whose initial write failed)
  is Release Blocking today and is closed by `WP 17.1B`, because in a
  transactional factory there is no window in which a registered object
  has no durable state. It is the first row `v0.17.0`'s physical review
  checks.

## What is deliberately not in this programme

- No Engineering Readiness Review document, Colour Review Board, Academy
  retrospective, register re-derivation or status reconciliation. The PR,
  the CI history, the recorded physical review and the Release Notes are
  the record. If a second reviewer is wanted, it is one agent, one round,
  a fixed checklist, and the findings go into the PR.
- No re-run of a determinism matrix after a change. A change gets a CI
  run; a release gets three (five for `v1.0.0`) under load.
- No new discipline beyond the six that ship, and no new reference
  library beyond materials, fasteners, bearings, standards and constants
  populated by `WP 18.0B`.
- No Companion, no plugins, no inbound API, no multi-user. Each has an
  ADR or decision record that says why, and each comes back only when a
  paying client asks for it.
- No erasure. The governance reset archives; it does not delete. Every
  retrospective, board disposition and register moves under
  `archive/docs-2026-09/` in git, retrievable by path and by history, and
  no decision recorded there is rewritten to make the tree look cleaner
  than it was.

## Related Documents

`docs/releases/v1.0.0/v1.0.0 Release Candidate Audit.md` (the 2026-09-04
audit this programme replaces the plan of); `docs/governance/Product
Roadmap.md` (Phase 5.5, to be amended by `WP 17.0B` to point here);
`PHYSICAL_REVIEW.md`; `FOUNDATION.md`; `VISION.md`.

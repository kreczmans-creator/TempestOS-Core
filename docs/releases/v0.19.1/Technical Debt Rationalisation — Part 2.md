# Technical Debt Rationalisation — Part 2

Programme-owned, archived-with-the-layer and historical rows. Part 1 (a
parallel agent) covers the Live Backlog and the debt outside it; this
document covers everything else the debt history holds, per the Product
Owner's request of 2026-09-14 (evening) for "a full breakdown of the
technical debt acquired through the implementation of this software."

**Method.** Every row below was re-verified against the code on
`wp/19.10G` at `5abeb57d` (worktree `D:/tempest-wt/19.10G`, clean),
read-only, by five parallel evidence passes plus direct inspection —
never assumed from `BACKLOG.md`'s own prose. `BACKLOG.md`'s "Owned by
Programme" section (lines 173–345) is the *Part D* source; its
"Archived with the Layer" section (lines 346–377) is *Part E*'s; the
frozen `archive/docs-2026-09/governance/Quality/Technical Debt
Register.md` (170 rows, `TD-01`–`TD-170`) is *Part F*'s. Prior
reconciliations `947c50d` (v0.19.0) and this branch's own `WP 19.9.1`
passes (`b0fc7025`, `1f237d62`, `dfbd6379`) were read first and are
built on, not repeated.

**Headline counts.** Of the 57 rows `BACKLOG.md` places "Owned by
Programme": **26 Closed, 4 Partly closed, 27 Not closed.** Two of the
26 (`TD-137`, `TD-149`) are closed but `BACKLOG.md` does not yet say so
— a real, disclosed drift this document corrects. Of the 16
"Archived with the Layer" rows, 13 genuinely sit in frozen code with no
live dependency; 2 (`TD-161`, `TD-162`) were never actually frozen
despite the section's own claim, and 1 (`TD-82`) names an application
that was never built. Of 170 rows ever entered in the frozen register,
69 (63 Resolved + 6 Closed) closed before the `WP 17.0B` triage
(2026-09-08); a further 15 IDs (`TD-171`–`TD-185`) were minted since,
of which 3 were withdrawn as not-debt and 1 closed in the same pass it
was raised.

---

## Part D — the programme-owned rows, closure verified

### D.1 All 57 rows

| ID | Title | Claimed by | Verified state | Evidence |
|---|---|---|---|---|
| `TD-142` | Refuse-after-mutate defect recurs on twelve concrete-Kind mutators | `WP 17.1B` | **Closed** | `EngineeringObjectBase.cs:248-276` (`MutateTypeStateAndPersistAsync`) commits before mutating; `tests/Tempest.Core.Tests/EngineeringDomain/R7FalsificationTests.cs:181` |
| `TD-144` | A failed move's durable link write can leave a partial reparent | `WP 17.1B` | **Closed** | `EngineeringObjectBase.cs:664-703` (`MoveAsync`), one transaction; `tests/Tempest.Core.Tests/EngineeringDomain/R7RegressionProofTests.cs:202` |
| `TD-145` | Two concurrent moves can form an undetected parent cycle | `WP 17.1B` | **Closed** | `EngineeringObjectBase.cs:720-740` (`GuardAgainstCircularParent`, inside the write lock); `tests/Tempest.Core.Tests/EngineeringDomain/TransactionalWriteFaultInjectionTests.cs:169` |
| `TD-146` | A delete can commit while a concurrent move gives the object a live child | `WP 17.1B` | **Closed** | `EngineeringObjectBase.cs:763-796` (live-children check inside the delete transaction); `TransactionalWriteFaultInjectionTests.cs:236` |
| `TD-147` | **Was Release Blocking.** Creation whose initial write fails still registered in memory | `WP 17.1B` | **Closed** | `EngineeringObjectFactory.cs:52-87` (`Register` only in `afterCommit`); `TransactionalWriteFaultInjectionTests.cs:51` |
| `TD-148` | `DeleteAsync` can report failure after the soft delete already committed | `WP 17.1B` | **Closed** | One commit path, `EngineeringDomainContext.cs:272`; `R7RegressionProofTests.cs:232` |
| `TD-03` | No disposal tracking for reflection-constructed singletons | `WP 17.2A` | **Not closed** | `TempestServiceProvider.cs:107-129` has no disposal list; `TempestHost.cs:1221-1225` says so directly |
| `TD-04` | `IHostedService` name clashes with `Microsoft.Extensions.Hosting.IHostedService` | `WP 17.2A` | **Not closed** | `BackgroundServices/IHostedService.cs:1,28` unrenamed; `ADR-0024` records the deliberate non-rename |
| `TD-12` | `IPersistenceStore` has no native query/filter capability | `WP 17.1A` | **Not closed** | `IPersistenceStore.cs:8-43`; `ReferenceDataCatalog.cs:220-228` (`FilterAsync`) still filters client-side |
| `TD-18` | `LinkAsync` concurrency under many simultaneous calls is untested | `WP 17.1A` | **Not closed** | No parallel-battery test exercises `LinkAsync`; `MutatorRefusalAdversarialTests.cs:309-339` omits it |
| `TD-20` | `MaterialCatalog` reads a full revision history for a latest-only lookup | `WP 17.1A` | **Not closed** | `ReferenceDataCatalog.cs:487-511` (`ReadDtoAsync`/`ReadRecordAsync`) call `GetRevisionHistoryAsync` and take `history[^1]` |
| `TD-21` | `ICalculationDefinition.Calculate` carries no `CancellationToken` | `WP 18.0A` | **Not closed** | `ICalculationDefinition.cs:21` — no token parameter |
| `TD-22` | `CalculationContext` has no result bound; intermediate values untyped | `WP 18.0A` | **Not closed** | `CalculationContext.cs:20-72` non-generic; `CalculationIntermediateResult.cs:11` is `(string Name, object Value)` |
| `TD-23` | `VerificationService.RecordAsync`'s multi-step link sequence is not transactional | `WP 17.1B` | **Not closed** | `VerificationService.cs:98-136` — create then three independent `LinkAsync` calls, no shared transaction |
| `TD-29` | `CalculationRecord` never retains its input | `WP 17.3A` / `WP 18.0A` | **Not closed** | `CalculationRecord.cs:26-36` has no `Input`/`TInput` field |
| `TD-30` | `ICalculationResult`/`IVerificationResult`/`IApprovalGate` have zero implementations | `WP 18.0A` | **Not closed** | Zero concrete implementers anywhere; `CalculationsPropertyFacetProvider.cs:22-24` discloses this directly |
| `TD-32` | Verification's `verifiedBy` link is invisible to `RelationshipRepository` | `WP 17.1B` | **Not closed** | `VerificationService.cs:122` writes via `IEngineeringDocumentStore.LinkAsync`, bypassing `IEngineeringRelationshipRepository` entirely |
| `TD-36` | `PersistenceStore.DefaultRootPath` resolves relative to the process CWD | `WP 17.1A` | **Not closed** | `SqlitePersistenceStore.cs:104,177-180,196` — still CWD-relative |
| `TD-67` | Crash-window write ordering can strand an invisible orphan document | `WP 17.1A` | **Not closed** | `ReferenceDataCatalog.cs:143-149`, `RequirementsService.cs:137-141`, `VerificationService.cs:119-128` — document-then-index/link, no wrapper transaction |
| `TD-86` | Engineering object mutation writes are per-object and unbatched | `WP 17.1B` | **Not closed** | `EngineeringObjectBase.cs:164-201` — one transaction per call, no batch entrypoint |
| `TD-88` | Startup rehydration is eager and linear, never lazy or project-scoped | `WP 17.1A` | **Not closed** | `EngineeringObjectRehydrationService.cs:69-84,140` lists and loads the whole estate unconditionally at every startup |
| `TD-95` | Attachment bytes are stored per attachment, never deduplicated by content | `WP 17.1B` | **Not closed** | `AttachmentContentStore.cs:73-78,165` keys by attachment Id, not content hash, though `ComputeHash` already exists |
| `TD-96` | `IBinaryPersistenceStore` materialises whole file content in memory | `WP 17.1B` | **Not closed** | `IBinaryPersistenceStore.cs:57` has no `Stream` overload; `SqlitePersistenceStore.cs:320` materialises the whole BLOB |
| `TD-130` | Reconciliation services (one of which deletes data) have no authorization seam | `WP 17.2A` | **Not closed** | `RequirementsReconciliationService.cs:35-43,223,323`, `MaterialCatalogReconciliationService.cs:23-31,122` — no `IPermissionEvaluator`, unreachable but unguarded |
| `TD-137` | `PersistenceStore`'s atomic writes are crash-safe but not `fsync`'d | `WP 17.1A` | **Closed (undisclosed)** | `SqlitePersistenceStore.cs:874-877` — `PRAGMA synchronous=FULL` in WAL mode fsyncs every commit; `ADR-0144` states this closes `TD-137` |
| `TD-141` | Two durable relationship-write paths carry no supersession guard | `WP 17.1B` | **Partly closed** | `EngineeringObjectBase.LinkAsync` (`:484-503,495`) now calls `ThrowIfSuperseded()`; `EngineeringRelationshipFactory.CreateAsync` (`EngineeringObjectFactory.cs:120-153`) still has no check |
| `TD-149` | A deleted legacy-encoded record can resurrect as live on delete failure | `WP 17.1A` | **Closed (undisclosed)** | `SqlitePersistenceStore.DeleteAsync:271-287` is one atomic SQL `DELETE`; no dual-file/legacy-encoding concept survives `ADR-0144`; `R7FalsificationTests.cs:59-68` |
| `TD-156` | A superseded reference record keeps its secondary index entry | `WP 17.1A` | **Not closed** | `ReferenceDataCatalog.SupersedeAsync:350-405` never updates `SecondaryIndexCollectionName`, unlike `ReviseCoreAsync:290-302` |
| `TD-158` | `ReferenceDataCatalog` composes durable writes with no all-or-nothing semantics | `WP 17.1B` | **Not closed** | `ReferenceDataCatalog.cs:143-149` (`RegisterAsync`), `:378-391` (`SupersedeAsync`) — sequential unguarded writes |
| `TD-169` | The canonical lifecycle permits no `Draft` → `Archived` transition | `WP 18.0A` | **Not closed** | `LifecycleTransitionTable.cs:15,19-22` — no edge from `Draft`/`InReview`/`Approved` to `Archived` |
| `TD-170` | Naming an executed calculation is create-then-link with no compensation | `WP 17.1B` | **Partly closed** | `EngineeringCalculationRegister.NameAsync:106-161` now runs `TryWithdrawAsync` (`:202-216`) on link failure; a failure of the compensation itself still orphans (disclosed in `:476-479`) |
| `TD-171` | Three verification models remain; collapse deferred to `WP 18.2B` | `WP 18.2B` | **Not closed** | Three namespaces still live (`Core/Verification`, `EngineeringDomain/RequirementsVerification`, `EngineeringAssets/Verification`); `WP 18.2B`'s actual scope was "Independent check and issue," not a collapse |
| `TD-77` | Command Palette is not contextual; most real commands are unavailable there | `WP 19.2B` (residual) | **Not closed** | `CommandPaletteOverlay.cs:198-201,207,253-256` still lists every registered command, filtering only availability annotation, never membership |
| `TD-79` | Engineering Workspace has deep domain support and almost no dedicated UI | `WP 18.2A` | **Not closed** | No `ProfilesView`/`LoadsView`/`EnvironmentsView`/`CompareView`/etc. exist under `src/Tempest.Desktop`; only `EngineeringCalculationView.cs` and `LibrariesView.cs` do |
| `TD-90` | A docking re-render does not restore keyboard focus | `WP 18.1A` (claimed, not closed) | **Not closed** | Zero `Focus` references in `WorkspaceDockingComposer.cs`, `WorkspaceLayoutController.cs`, `WorkspaceLayoutHost.cs` |
| `TD-115` | Three registered commands have no production construction path | `WP 19.2A` (claimed, not closed) | **Not closed** | Zero construction sites for `LinkRequirementCommand`/`AddRequirementToCollectionCommand`/`CompareBaselinesCommand`; `FutureCapabilityCommandTests.cs:88-112` still asserts the absence |
| `TD-160` | The whole merged engineering capability has no Desktop UI surface | `WP 18.2A` (partial) | **Partly closed** | `LibrariesView.cs` covers 8 reference libraries; `WorkspaceHost.cs:316,388` (`EngineeringTrace`) and its `BracketEngineeringRecords` property have zero Desktop consumers |
| `TD-165` | The bracket verification artefact cannot be filled in from the Desktop | `WP 18.2A` (claimed, not closed) | **Not closed** | `BracketEngineeringRecordService`'s only reference under `src/Tempest.Desktop` is its own construction site, `WorkspaceHost.cs:266,401` — no consumer |
| `TD-01` | Two logging mechanisms coexist | `WP 17.2A` | **Closed** | Legacy `LoggingService` has zero references in `src/`; `TempestLoggerProvider.cs:35-141` bridges `Microsoft.Extensions.Logging` into `ILogger` |
| `TD-173` | Verification result links Activity, never the Requirement Subject | `WP 17.9.3` | **Closed** | `RecordVerificationResultCommand.cs:148` links the record from the Activity's subject too |
| `TD-172` | Creation placement and visibility | `WP 17.9.3` / `WP 17.9.4` | **Closed** | `CreationPlacement.cs:32-42`; `ManufacturingWorkspaceRegistration.cs:152`; shell switch on Create, `MainWindow.cs:745-781` |
| `TD-17` | Document revision content is an opaque string | `WP 18.0A` | **Closed** | `Evidence/DeclaredFigure.cs:26`, validated by `EvidenceService.cs:178-193` against `EvidenceUnitCatalog.KnownUnits` |
| `TD-155` | Materials library reuses an incompatible payload shape | `WP 18.0B` | **Closed** | `MaterialSpecificationDto` has zero references in `src/`; `MaterialCatalog.cs:24` is solely `ReferenceDataCatalog<MaterialDefinition>` |
| `TD-175` | A Part has no BOM input; only a read-only Where-used readout | `WP 18.1C` / `WP 18.2A` | **Closed** | `KindEditorDeclarations.cs` — `Part()` (`:71-79`) no BOM; `Assembly()` (`:82-96`) has the editable BOM section, `ObjectEditorView.cs:1219` (`SetBomLineCommand`) |
| `TD-66` | Refresh-architecture debt: Cockpit, Explorer, open tabs | `WP 18.1A` | **Closed** | `CockpitView.cs:117-131`, `ProjectExplorerView.cs:195-220`, `ObjectEditorView.cs:387-480` each subscribe one `IWorkspaceChanges.Changed` handler |
| `TD-108` | Blocking `.GetAwaiter().GetResult()` calls | `WP 18.1A-R1` | **Closed** (allow-list stale) | `NoBlockingPersistenceCallsTests.cs`; the disclosed allow-list has grown from 11 to 16 sites since `WP 18.1A-R1` (later WPs added 5 more, all still disclosed) |
| `TD-118` | Cockpit's read surface is synchronous by shape | `WP 18.1A-R1` | **Closed** | `EngineeringCockpit.cs:225-232` — `PrimeAsync` awaits all six discipline `LoadAsync` calls |
| `TD-65` | Systemic Desktop accessibility gaps | `WP 19.2B` | **Closed** | `AutomationNameCoverageTests.cs:54-100` walks a real running window across every rail surface |
| `TD-73` | Rail and ribbon never compact | `WP 19.2B` | **Closed** | `DesignTokens.cs:171` (`CompactShellWidth = 1200`); `GlobalNavigationRail.cs:99`, `RibbonView.cs:143` |
| `TD-74` | No global navigation architecture | `WP 19.2B` | **Closed (citation stale)** | Closed in substance, but the cited 8-entry rail was superseded by `WP 19.7A`'s 5-module rail (`NavigationAvailability.cs:70-72,165-167`) |
| `TD-81` | Whole mock-up modules unimplemented | `WP 19.2B` | **Closed (citation stale)** | No dead placeholder module exists; but `WP 19.7A` re-added Tasks to the rail, so "all five removed" no longer matches `NavigationAvailability.cs:139-142` |
| `TD-128` | Digital Thread graph edges are keyboard-unreachable | `WP 19.2B` | **Closed** | `DigitalThreadGraphView.cs:607-628`; `KeyboardOnlyJourneyTests.cs:27` |
| `TD-132` | Every relationship row's "Open" button shares one accessible name | `WP 19.2B` | **Closed** | `ObjectEditorView.cs:1059-1098,1095` — named per row |
| `TD-133` | Docking-panel repositioning and tab reordering are mouse-only | `WP 19.2B` (partial) | **Partly closed** | Repositioning/resizing closed (`LayoutTabGroupView.cs:62-70,231-256`); tab *reordering* confirmed still mouse-only, no keyboard path anywhere |
| `TD-109` | `MainWindow` is a 1,577-line god object | `WP 19.2A` | **Closed (figures stale)** | `MainWindowComposer*.cs`, four phases confirmed in order (`MainWindow.cs:179-182`); `MainWindow.cs` is now 836 lines (782 at the `WP 19.2B` merge, +54 from later unrelated WPs), test moved to line 433 |
| `TD-02` | Single-sink logging | (pre-table) `WP 17.0C` | **Closed** | `CompositeLogSink.cs:28-103,86-102`; wired at `TempestHost.cs:265` |
| `TD-103` | The principal boundary | (pre-table, predates `WP 17.2A`) | **Closed** | `WorkspaceHost.cs:177-204` established the session principal in commit `671a18bd` (2026-08-29), ~10 days before `WP 17.2A`'s own touch |

### D.2 The rows not fully closed — what remains

**`TD-03`** — No disposal tracking for reflection-constructed singletons. `TempestServiceProvider` keeps no disposal list for anything it builds by reflection; the gap is explicitly disclosed, unclosed, in `TempestHost.cs:1221-1225`. Affects: Runtime/Host composition root — currently no live container-constructed `IDisposable` singleton exists (all current disposables are manually composed), so the risk is latent. Scale: S, ~1–2d. Priority: **P3**.

**`TD-04`** — `IHostedService` name clash with `Microsoft.Extensions.Hosting.IHostedService`. `ADR-0024` records a deliberate non-rename with a named revisit trigger; no live reference to the Microsoft interface exists anywhere in the tree, so the trigger has not fired. Affects: `BackgroundServices` namespace hygiene, future risk only. Scale: S, ~0.5–1d. Priority: **P4** — explicit documented deferral.

**`TD-12`** — `IPersistenceStore` has no native query/filter capability; `ReferenceDataCatalog<T>.FilterAsync` (`:220-228`) still reads the whole collection and filters client-side, by design, per its own comment. Affects: all 11 reference-library searches. Scale: M, ~3–4d (one base-class fix serves every library). Priority: **P3** — catalogues are small (≤41 seed records today), no measured latency problem.

**`TD-18`** — `LinkAsync` concurrency is untested; the write path is already serialised under the one domain write lock, so this is a coverage gap, not a known defect. Affects: Digital Thread/relationship linking under bulk use. Scale: S, ~0.5–1d. Priority: **P3**.

**`TD-20`** — `MaterialCatalog`/every `ReferenceDataCatalog<T>` reads a full revision history twice per single lookup (`ReadDtoAsync`/`ReadRecordAsync`, `:487-511`). Affects: every material/reference lookup (Libraries tab, BOM resolution, calc citations). Scale: S, ~1–2d. Priority: **P3** — wasteful, not user-visible yet.

**`TD-21`** — `ICalculationDefinition.Calculate` takes no `CancellationToken`, so `ICalculationEngine.ExecuteAsync`'s own token cannot reach a mid-compute cancellation. Affects: calculation engine execution. Scale: S, ~1–2d. Priority: **P3** — `Calculate` is a synchronous pure function, low impact today.

**`TD-22`** — `CalculationContext` has no `TResult` binding; `CalculationIntermediateResult.Value` is a bare `object`. Affects: calculation evidence read-back, Property Inspector's "Latest Result"/intermediate display. Scale: M, ~3–5d. Priority: **P2** — consumed by a real UI read path; untyped storage risks a silent cast/display bug.

**`TD-23`** — `VerificationService.RecordAsync` creates the record then runs three independently-awaited `LinkAsync` calls (`:98-136`) with no shared transaction; a failure partway leaves a durably-committed, partially-linked verification record. Affects: verification/requirements-traceability recording — a compliance-relevant evidentiary record. Scale: M, ~2–3d (wrap in `ExecuteInTransactionAsync`, the pattern `WP 17.1B` already built). Priority: **P1** — a genuine partial-write data-integrity defect, same class as the release-blocking `TD-147`.

**`TD-29`** — `CalculationRecord` (`:26-36`) has no `Input`/`TInput` field, blocking a parameterless re-run. Affects: calculation reproducibility. Scale: M, ~3–5d (needs the field on the record, DTO, engine and every reader). Priority: **P2** — missing capability, not corruption.

**`TD-30`** — `ICalculationResult`/`IVerificationResult`/`IApprovalGate` have zero concrete implementations anywhere; self-disclosed in `CalculationsPropertyFacetProvider.cs:22-24` ("honestly resolves empty"). Affects: Digital Thread/evidence-trail journey for Calculations and Verification. Scale: L, ~8–10d. Priority: **P2** — degrades silently (empty trail) rather than corrupting; disclosed, non-blocking.

**`TD-32`** — Verification's `verifiedBy` link bypasses `IEngineeringRelationshipRepository` entirely, including through `TD-173`'s own later fix, which uses the same raw `LinkAsync`. `RelationshipDiscoveryService` (backing Digital Thread/impact analysis) reads only the repository. Affects: Digital Thread graph under-reports verification edges the Verification tab itself finds fine. Scale: M, ~2d. Priority: **P2** — a visible inconsistency between two read surfaces, not data loss.

**`TD-36`** — `SqlitePersistenceStore.DefaultRootPath` (`:104,177-180,196`) still resolves relative to the process CWD. Affects: location of all durable data at Desktop/Harness startup. Scale: S, ~0.5–1d. Priority: **P3** — predictability only, no correctness consequence.

**`TD-67`** — Document-then-index/link write ordering with no wrapper transaction, confirmed across `ReferenceDataCatalog.RegisterAsync`, `RequirementsService.cs:137-141`, and `VerificationService.RecordAsync`. Affects: Materials/Requirements/Verification record creation, crash-window integrity. Scale: M, ~2–3d (`ExecuteInTransactionAsync` already exists to build on). Priority: **P2** — real risk, but only on an actual crash mid-sequence.

**`TD-86`** — `MutateAndPersistAsync` (`:164-201`) is one transaction per call; no multi-object batch entrypoint exists. Affects: bulk mutation throughput (bulk requirement edits, mass BOM/import). Scale: M, ~2–3d. Priority: **P4** — the register's own text: no measured workload demands it yet.

**`TD-88`** — `EngineeringObjectRehydrationService.RehydrateAsync` (`:69-84,140`) lists and loads the whole persisted estate unconditionally at every startup (`EngineeringWorkspaceComposer.cs:357-373`). Affects: Desktop/Harness startup latency and memory, scaling with total persisted estate across all projects. Scale: L, ~4–6d (needs a read-surface redesign). Priority: **P2** — user-facing cost that grows with data.

**`TD-95`** — Attachment bytes are keyed by attachment Id, never content hash (`AttachmentContentStore.cs:73-78,165`), though `ComputeHash` already exists for integrity checking. Affects: disk cost when one file is attached to multiple objects. Scale: M, ~2–3d (content-addressing + refcounted delete). Priority: **P4** — deliberately deferred; costs disk only.

**`TD-96`** — `IBinaryPersistenceStore.ReadBytesAsync` (`:57`) has no `Stream` overload; `SqlitePersistenceStore.cs:320` materialises the whole BLOB. Affects: Document Viewer/attachment reads for large files — a real Document Viewer now exists and uses this path (`src/Tempest.Desktop/Viewing/DocumentViewerView.cs`), so the historical "no consumer streams today" deferral condition has fired. Scale: M, ~1–2d. Priority: **P2** (upgraded from the historical P4 now that the trigger condition is live).

**`TD-130`** — Reconciliation services (`RequirementsReconciliationService`, `MaterialCatalogReconciliationService`) construct with no `IIdentityService`/`IPermissionEvaluator` and one (`SweepAsync`) deletes data; both are currently unreachable from any command/CLI/route. Affects: Requirements/Materials reconciliation tooling, once wired. Scale: S, ~0.5–1d for the seam itself. Priority: **P4** — deliberately not designed against a surface that does not exist yet, but escalates to P1 the moment either is wired to a caller.

**`TD-141`** — Partly closed. `EngineeringObjectBase.LinkAsync` (`:484-503,495`) now calls `ThrowIfSuperseded()` inside the transaction; `EngineeringRelationshipFactory.CreateAsync` (`EngineeringObjectFactory.cs:120-153`) still takes raw ids with no supersession check, and `IEngineeringObject` still exposes no public `IsSuperseded`. Affects: engineering relationship/Digital Thread graph integrity — a retired object can still receive a new durable relationship via the unguarded path. Scale: M, ~2–3d (needs a design decision on the public surface, not a quick guard). Priority: **P1** — deterministic, no race or crash required, and corrupts the relationship graph.

**`TD-156`** — `ReferenceDataCatalog.SupersedeAsync` (`:350-405`) never updates the secondary index, unlike `ReviseCoreAsync`; `FindBySecondaryKeyAsync` applies no lifecycle filter, so a lookup still resolves the superseded record, and `RequireSecondaryKeyFreeAsync` then refuses a correctly-named replacement. Affects: supersession/correction workflow across all eight `ReferenceDataCatalog<T>` libraries; a stale lookup can silently feed a retired spec into an evidence citation. Scale: S, ~1d. Priority: **P1** — deterministic on every supersession, blocks the correction journey.

**`TD-158`** — `ReferenceDataCatalog.RegisterAsync` (`:143-149`) and `SupersedeAsync` (`:378-391`) each compose two-or-three durable writes with no try/catch, no compensation, no transaction boundary — unlike `TD-170`'s calculation-naming path, this class has no compensation anywhere. Affects: registration and supersession across all 8 reference libraries. Scale: M, ~3d (a saga/compensation wrapper matching `TD-170`'s pattern). Priority: **P1** — can corrupt or strand durable data.

**`TD-169`** — `LifecycleTransitionTable` (`:15,19-22`) has no `Draft`/`InReview`/`Approved` → `Archived` edge. Affects: lifecycle/retirement governance for every Engineering Domain object. Scale: S, ~1d (if the edge is wanted at all). Priority: **P3** — the register's own text: not release blocking, closing it properly needs a board decision on whether the edge should exist platform-wide.

**`TD-170`** — Partly closed. `EngineeringCalculationRegister.NameAsync` now compensates a failed link with `TryWithdrawAsync` (soft-delete), disclosed via `EngineeringCalculationNamingException.WasWithdrawn`; if the compensating soft-delete itself fails, the orphan persists — real atomicity is still absent. Affects: Engineering Calculations naming/retire journey. Scale: S, ~1–2d (a real unregister rather than soft-delete-based compensation). Priority: **P2** — user-noticeable only in a narrow double-failure edge case; the calculation itself is never lost.

**`TD-171`** — Three verification models (`Core/Verification`, `EngineeringDomain/RequirementsVerification`, `EngineeringAssets/Verification`) remain distinct namespaces; `WP 18.2B`'s actual delivered scope ("Independent check and issue," `WorkPackages.md:137`) never touched the collapse. Affects: verification/requirements traceability code health, not a user journey. Scale: L, ~5d+. Priority: **P4** — deferred by explicit decision, not an accident.

**`TD-77`** — Command Palette still lists every registered command unless a text query substring-matches; availability is evaluated only to annotate/disable a row, never to filter membership (`CommandPaletteOverlay.cs:198-201,207,253-256`). `WP 19.2B`'s own touch to this file was a single automation-name line, per its own commit message. Affects: Command Palette discoverability for every discipline. Scale: M, ~3–5d (context-aware ranking/filtering). Priority: **P2**.

**`TD-79`** — Engineering Workspace has deep domain support behind almost no dedicated UI: no `ProfilesView`/`LoadsView`/`EnvironmentsView`/`CompareView`/`OptimizationView`/`SensitivityView`/`MathToolsView`/`ValidationPaneView`/`ResultsGridView` exist; only the bracket-journey calculation editor and the 8-library `LibrariesView` do. Affects: Engineering Workspace UX across roughly nine undelivered discipline surfaces. Scale: L, ~10d+. Priority: **P2**.

**`TD-90`** — No focus-capture/restore mechanism exists anywhere in the docking subsystem (`WorkspaceDockingComposer.cs`, `WorkspaceLayoutController.cs`, `WorkspaceLayoutHost.cs` — zero `Focus` references across all three). Affects: keyboard-only docking/re-layout UX. Scale: M, ~2–3d. Priority: **P2**.

**`TD-115`** — `LinkRequirementCommand`/`AddRequirementToCollectionCommand`/`CompareBaselinesCommand` are registered under real command ids but have zero construction sites anywhere in `src/`; `FutureCapabilityCommandTests.cs:88-112` still asserts the absence, and `WP 19.2A`'s merge only renamed string-literal ids to constants in the same registration file. Affects: Requirements/Mechanical — three features unreachable by any user. Scale: L, ~5d+, gated on the `FCR-0073` object picker. Priority: **P4** — deliberately deferred, a pinned `WP-H` decision.

**`TD-160`** — Partly closed. `LibrariesView.cs` genuinely browses all 8 reference libraries, closing one of the row's three named gaps. `WorkspaceHost.cs:316,388` still constructs and exposes `EngineeringTraceRegister`'s `EngineeringTrace` and `BracketEngineeringRecords` with zero Desktop consumers of either. Affects: calculation provenance/traceability visibility. Scale: M, ~3–4d (a trace panel wired to the existing `TraceCalculationAsync`). Priority: **P2**.

**`TD-165`** — `BracketEngineeringRecordService`'s only reference under `src/Tempest.Desktop` is its own construction site (`WorkspaceHost.cs:266,401`); no screen consumes it. Affects: bracket independent-check/verification-artefact completion — the one governed act the entire "bracket journey" demonstration depends on, with zero UI path. Scale: M, ~2–3d (wire into a command/dialog off the existing calculation or object editor views). Priority: **P1** — fully blocks that journey on Desktop.

**`TD-133`** (residual only — repositioning/resizing already closed) — Tab *reordering* within a docked panel group is confirmed still mouse-only: `LayoutTabGroupView.cs`'s `TabDragStarted` fires only from a `PointerPressedEvent` handler; no keyboard path exists anywhere in `src/Tempest.Desktop` or `src/Tempest.Workspace`. Tab *switching* (not reordering) is already keyboard-reachable via `Ctrl+Tab`/`Ctrl+Shift+Tab` (`DocumentAreaView.cs:247-251`). Affects: keyboard-only/screen-reader users who want to change persistent tab order within one panel group; every tab stays reachable regardless. Scale: S — one shared mechanism, so one fix surfaces everywhere tab groups appear. Priority: **P3** — an accessibility-completeness item, not a functional blocker.

### D.3 Two closures the file doesn't yet credit

`TD-137` (fsync) and `TD-149` (legacy-encoded resurrection) are both genuinely **closed** — `SqlitePersistenceStore.cs` (`:874-877` for the WAL `synchronous=FULL` pragma; `:271-287` for the single atomic `DELETE`) and `ADR-0144` state this outright — but `BACKLOG.md` still lists their owner as merely `WP 17.1A` (i.e., not yet moved to a "Closed" table). Both landed with `WP 17.1A`/`WP 18.1A`'s `ADR-0144` rewrite (2026-09-08/09), the same substrate change that closed the neighbouring `TD-36`/`TD-67`/etc. rows this document confirms are still open — the register was evidently updated selectively at the time. This is a `BACKLOG.md` housekeeping gap, not a code gap; no code action follows from it.

---

## Part E — the archived-with-the-layer rows

Three of the sixteen rows do not fit the section's own premise and are marked as such. `Tempest.Core.Api` (9 files / 724 lines), `Tempest.Core.Licensing` (10 files / 351 lines) and the frozen `Tempest.Core.Plugins` trust platform (22 files / 2,045 lines) hold no live reference anywhere outside `src/Frozen/` or `tests/Frozen/` — confirmed by `grep -rn "Tempest.Core.Api\|Tempest.Core.Licensing\|Tempest.Core.Plugins" src --include=*.csproj --include=*.cs | grep -v src/Frozen`, whose only hits are the deliberately-kept-live, differently-located `src/Tempest.Core/Plugins/` manifest-*discovery* service (same namespace name, a different physical project entirely — `src/Frozen/README.md:18-21` names this split explicitly) and one doc-comment in `Tempest.Core.csproj`.

| ID | What it is | Frozen layer | Unfreeze cost (rough) | Live dependency |
|---|---|---|---|---|
| `TD-161` | New app surfaces (`EngineeringTraceRegister`, `BracketEngineeringRecordService`) write engineering assets with no project scope | **None — never actually frozen.** `WP 18.0C` claims this row but the code stays live in `src/Tempest.Workspace`/`src/Tempest.Core`, not moved to `src/Frozen/` | N/A — this is a live-code gap wearing an "archived" label, not a freeze | **Yes.** `WorkspaceHost.cs:266,401` still constructs and exposes `BracketEngineeringRecordService`; `AssetApplicability.ProjectIdentifiers` (`AssetApplicability.cs:75`) is declared and read (`TemplateCatalog.cs:152`) but never assigned anywhere in `src/` |
| `TD-162` | `ProjectDependencyRegister` (P04) is unreferenced | **None — never actually frozen.** Same `WP 18.0C` claim, same non-move; `src/Tempest.Workspace/Projects/ProjectDependencyRegister.cs` remains live | N/A | **No.** Confirmed zero construction sites (`new ProjectDependencyRegister`/`IProjectDependencyRegister` — nothing); the only reference anywhere is one XML-doc `<see cref>` in `ReferenceCitationIndex.cs:33` |
| `TD-82` | Companion (mobile/field) application has zero implementation | **None — not a freeze at all.** Explicitly out of `v1.0.0` scope (`docs/releases/v1.0.0/WorkPackages.md`, "What v1.0.0 is") | N/A — nothing exists to reactivate; this would be new build, not an unfreeze | No |
| `TD-13` | REST API identity resolution carries no real authentication | `Tempest.Core.Api` (`ADR-0146`) | ~5–8d for the whole layer (small surface, 724 lines, but a real auth mechanism — API keys/OAuth/mTLS — is itself non-trivial) | No |
| `TD-14` | No TLS on the REST API's Kestrel listener | `Tempest.Core.Api` | (same layer re-activation) | No |
| `TD-15` | Audit records "unknown actor" for REST-invoked commands | `Tempest.Core.Api` | (same layer) | No |
| `TD-129` | REST 404-vs-401 split lets an unauthenticated caller enumerate routes | `Tempest.Core.Api` | (same layer) | No |
| `TD-16` | License file contents are trusted with no signature verification | `Tempest.Core.Licensing` (`ADR-0146`) | ~3–5d (small, 351 lines, but needs a real signing/verification design) | No |
| `TD-49` | TOCTOU window between plugin signature verification and load | `Tempest.Core.Plugins` (trust platform, `ADR-0146`) | ~10–15d for the whole layer (2,045 lines, security-sensitive — needs re-review against current `Tempest.Core`, not just a re-point) | No |
| `TD-50` | First-party certificate trust is a filename convention, not a certificate attribute | `Tempest.Core.Plugins` (trust) | (same layer) | No |
| `TD-53` | A hosted-service construction failure can be misclassified as non-critical | `Tempest.Core.Plugins` (trust) | (same layer) | No |
| `TD-54` | `ITempestServiceProvider`'s DI non-registration is incidental, not enforced | `Tempest.Core.Plugins` (trust) | (same layer) | No |
| `TD-55` | `PluginDeniedTypeRegistry` can wrongly deny an innocent shared assembly's types | `Tempest.Core.Plugins` (trust) | (same layer) | No |
| `TD-56` | A plugin constructor runs with a `null` (first-party) component scope | `Tempest.Core.Plugins` (trust) | (same layer) | No |
| `TD-61` | Plugin-folder containment check does not resolve symlinks | `Tempest.Core.Plugins` (trust) | (same layer) | No |
| `TD-64` | `TD-52`'s gate closure has no end-to-end production-wiring test | `Tempest.Core.Plugins` (trust) | (same layer) | No |

For scale, the same `WP 18.0C` freeze also moved six much larger namespaces out of `Tempest.Core` under `ADR-0127`–`ADR-0142` — `EngineeringIntelligence` (P02, 9,100 lines), `CommercialIntelligence` (P03, 7,461 lines), `BusinessOperations` (P04, 2,505 lines), `EngineeringAssets` (P05, 1,755 lines), `Knowledge` (P06, 3,966 lines) and `BusinessGovernance` (P07, 7,527 lines) — 32,308 lines total (`src/Frozen/README.md:44-53`). No `BACKLOG.md` row names any of these six directly (their own debt, if any, froze with them); they are noted here only as size context for what a P02–P07 unfreeze would actually cost, an order of magnitude past the REST/Licensing/Plugins figures above.

---

## Part F — the history

### F.1 What the frozen register holds

**170 rows ever raised** (`TD-01`–`TD-170`, contiguous, no gaps — `archive/docs-2026-09/governance/Quality/Technical Debt Register.md:13`). Of those, **69 closed before the `WP 17.0B` triage** (2026-09-08): 63 Resolved + 6 Closed (register metadata, same line), leaving the 101 (93 Open + 7 Partially resolved + 1 Deferred) that became `BACKLOG.md`'s own starting set — matching `docs/releases/v0.17.0/Release Notes.md:137`'s own "101 debt rows triaged: 27 live, 61 owned by a programme WP, the rest archived with their layer or closed."

**By what they closed:** the large majority — 66 of the 69 — closed through implementation work credited to a specific Work Package, numbered (`WP 6.1`, `WP 13.2A`, `WP 16.4B-R7`, …) or lettered (`WP-A1`, `WP-A2`, `WP-C`, `WP-D1`, `WP-D2`, `WP-F`, `WP-G`, `WP-Z2`, …) in the row's own Status cell. The remaining 3 (`TD-70`, `TD-71`, `TD-72` region — specifically `TD-70`, `TD-71`, `TD-94`) closed through a dated review/audit pass (the "2026-08-28 audit pass," "cleanliness pass") that predates the programme's WP-lettering convention rather than carrying a WP id of its own. None of the 69 read as a pure re-scoping decision with no code change — every Resolved/Closed row this register carries names a concrete fix, a test, or a superseding architecture.

### F.2 The ten largest closures by scope

Ranked by the length and depth of the register's own remediation text for each row (a proxy for how much was actually re-architected, not merely patched):

| Rank | ID | What it was | Closed by |
|---|---|---|---|
| 1 | `TD-51` | `PluginAssemblyLoader.EnforceTrust`'s constructor-conformance check crashed a real end-to-end Host test (`Expected: Running, Actual: Faulted`) | `WP 13.11B` (after being reopened by `WP 13.11A`, on top of earlier `WP 13.10B`/`WP 13.10C` work) |
| 2 | `TD-143` | The round-2 refusal invariant (`SupersededEngineeringObjectException`) held for nothing but that one exception type — a mutation whose durable write failed still kept its in-memory mutation on all seven mutators | `WP 16.4B-R7` — compensates the in-memory mutation only on evidence the durable write did not land |
| 3 | `TD-119` | `Tempest.Desktop.Tests` used fixed `Task.Delay` waits as synchronisation against async work the tests never joined | `WP 16.4A` — removed the dead wait, 30/30 consecutive passes |
| 4 | `TD-140` | A mutation REFUSED with `SupersededEngineeringObjectException` still became durable, on five of seven mutators | `WP 16.4B-R6` round 2, for the supersession-refusal route |
| 5 | `TD-136` | `ReviseAsync` was the one durable-write path `TD-135`'s fix never routed through the per-object write lock | `WP 16.4B-R6` |
| 6 | `TD-139` | `AttachContentAsync` stranded its write-intent marker when the state write was refused (four-step sequence, no exception handling) | `WP 16.4B-R6` |
| 7 | `TD-34` | `CompositeLogSinkTests` intermittently failed under the full suite from a shared `Console.Error` stream dependency | `WP 16.4A` — injected `TextWriter`, default `Console.Error`, test-isolated |
| 8 | `TD-72` | Docking was a fixed three-dock, compile-time 5×3 `Grid` — not a real docking system, no drag, no resize beyond the fixed geometry | The 2026-08-29 docking pass, `ADR-0095` |
| 9 | `TD-85` | The engineering object graph was not durable — `TempestHost` registered in-memory repositories, so reconstructed objects never survived a restart | The 2026-08-28 object-rehydration pass, `ADR-0113` |
| 10 | `TD-114` | Test-suite brittleness around the `TD-77` grouping row — exact-count assertions on 74/56/18/14/42/41/30 spread across four files broke ~12 assertions per new command | `WP-F` (2026-09-01) |

### F.3 Rows raised vs. closed, per release

| Release | Rows raised | Rows closed |
|---|---|---|
| Pre-`v0.17.0` (the frozen register, `WP` 2.x–16.x) | 170 | 69 (63 Resolved + 6 Closed, before the `WP 17.0B` triage) |
| `v0.17.0` | 3 (`TD-171`–`173`) | 9 (`TD-01`, `142`, `144`–`148`, `172`, `173` — two of the nine, `172`/`173`, raised and closed in the same release) |
| `v0.18.0` | 2 (`TD-174`, `175`) | 7 (`TD-17`, `66`, `108`, `118`, `155`, `163`, `175` — one, `175`, raised and closed in the same release) |
| `v0.19.0` | 0 | 8 full + 1 partial (`TD-65`, `73`, `74`, `76`, `81`, `109`, `128`, `132`; `TD-133` partial) |
| `v0.19.1` | 10 minted (`TD-176`–`185`); 3 withdrawn as not-debt (`183`–`185`, folded into the release notes' own Warnings); net 6 remain open (`176`, `177`, `179`–`182`) | 1, same-pass (`TD-178`, raised and closed in the `WP 19.9.1` reconciliation itself); 0 pre-existing rows (`WP 19.9.1`'s own reconciliation: "0 closed/0 partly/0 not closed") |

(Sources: `BACKLOG.md`'s own "Owned by Programme"/"Live Backlog" closure notes; `docs/releases/v0.17.0/Release Notes.md:137,163-164`; `947c50d` and this branch's `b0fc7025`/`1f237d62`/`dfbd6379` commit messages; first-appearance commits for `TD-171`–`185` found by `git log --reverse -S"TD-nnn" -- BACKLOG.md`.)

**What the trend says.** The pattern is not "raising faster than closing" in any worrying sense — it is front-loaded payoff followed by a deliberate audit spike. `v0.17.0` and `v0.18.0` together closed sixteen rows, including the release-blocking `TD-147` and the whole refuse-after-mutate class, while raising only five, three of which closed in the same release they were raised. `v0.19.0` raised nothing and closed nine (a full accessibility/navigation/composition sweep) as pure debt reduction. `v0.19.1`'s ten new IDs look like a spike, but it is a disclosed one: it is the product of a deliberate audit (`WP 19.9.1` cross-checking every v0.19.1 Work Package's own report against `BACKLOG.md`, closing zero pre-existing rows in the process precisely because it found none it could honestly claim), three of the ten were judged not to be product debt at all and were withdrawn before the release closed, and every surviving row is individually small (a UI edge case, a missing bucket, a heuristic) and already named in the v0.19.1 Release Notes' own Warnings section rather than hidden. The two undisclosed closures this document found (`TD-137`, `TD-149`, Part D.3) point the other way — the register understates what has already been paid down, not what remains owed.

---

*Every row above was checked against the code on `wp/19.10G` at `5abeb57d`. No file outside `docs/releases/v0.19.1/` was written by this audit.*

# Backlog

The live technical-debt list. Replaces the 468 KB `Technical Debt
Register.md`, frozen as history at
[`archive/docs-2026-09/governance/Quality/Technical Debt Register.md`](archive/docs-2026-09/governance/Quality/Technical%20Debt%20Register.md).

**Triage basis (`WP 17.0B`, 2026-09-08):** every register row whose
final Status cell led with `Open`, `Partially resolved` or `Deferred` —
101 rows (170 total rows in the register; 93 of the 101 are `Open`
alone, which is the figure `docs/releases/v1.0.0/WorkPackages.md`
names). The disposition rule is the one that document states under
"Disposition of the current Technical Debt Register": a row closed by
a substrate Work Package (`WP 17.1A`, `17.1B`, `17.2A`, `17.3A`,
`18.0A`) or a surface Work Package (`WP 18.1A`, `18.2A`, `19.2A`,
`19.2B`) is **Owned by programme**, below, mapped from each Work
Package's own "Closes" column; a row about plugins, REST, licensing or
a frozen P02–P07 layer is **Archived with the layer**; everything
else a user could still notice is the **Live Backlog**, capped at 30.

Nine rows close by this Work Package itself rather than by a future
one: `TD-45` (branch protection, now configured — see
`CONTRIBUTING.md`), `TD-123`–`TD-125`, `TD-127`, `TD-151`–`TD-153` and
`TD-164` (all governance-register drift or precision complaints —
moot once the registers they complain about are archived and the
health check that watched them is reduced), and `TD-43` (the health
check's generic exception handler already names the failing check in
its `Fail` result; carried forward unchanged into the reduced script).
None of these nine appear below.

## Live Backlog (24 of 30 cap)

`TD-147` — an object creation whose initial durable write failed still
registered the object in memory, so its next successful write made a
reported failure real — was listed here first and marked Release
Blocking. It is **closed by `WP 17.1B`** (`ADR-0145`) and no longer
appears below: the document, its first revision, the object's state
record and the creation audit row are one transaction, and
`EngineeringObjectFactory<T>.CreateAsync` calls `Register` only after it
commits, so a creation whose write fails leaves nothing in the repository
and nothing on disk.

| ID | Title | Owner |
|---|---|---|
| `TD-05` | Module discovery still requires a parameterless constructor outside the `[ModuleMetadata]` lift | unowned |
| `TD-24` | `VerificationContext` has no bound on criteria, evidence or links recorded | unowned |
| `TD-25` | `RequirementsService` has no compare-and-swap; concurrent edits can silently clobber | `WP 18.2B` |
| `TD-27` | `InMemoryEngineeringObjectRepository` iteration order is unguaranteed | `WP 17.1B` (judgement — see note) |
| `TD-28` | Bulk requirement commands don't auto-refresh an already-open view | `WP 18.1A` (judgement — see note) |
| `TD-33` | `EngineeringCockpit.FormatCoverage` returns a hardcoded, wrong-discipline empty-state string | `WP 19.1B` (claimed, not closed — see note) |
| `TD-38` | `EngineeringObjectFactory` enforces no business-identifier uniqueness | `WP 18.2B` |
| `TD-41` | `ObjectEditorView` never resolves a real Requirement; always falls back to the generic body | unowned (claimed by `WP 18.1B`/`WP 18.2A`, not actually closed — see note) |
| `TD-42` | `new-release.ps1`'s `git tag`/`git push` calls never check `$LASTEXITCODE` | unowned |
| `TD-63` | `TD-40`'s dirty-tab-close fix is not pinned on its production path | unowned |
| `TD-78` | Brand design system (colours, fonts) is absent from the Desktop | unowned |
| `TD-84` | Grouping row: `TD-74`/`76`/`79`/`81` are one Product Spine deficiency, not four | unowned |
| `TD-91` | `IWorkspaceLayout` cannot express a tabbed or floating panel | unowned |
| `TD-92` | Drag-to-dock has no live preview adorner | unowned |
| `TD-93` | `Tempest.Samples` redeclares 13 canonical vocabulary strings; can't reference the owner | unowned |
| `TD-98` | Document viewer has no markup, annotation or rotation | `WP 18.2B` (partial) |
| `TD-99` | DWG and SVG attachments report `Unsupported` in the viewer | unowned |
| `TD-101` | A page rasterises at full size even when only part of it is visible | unowned |
| `TD-131` | Focus-ring contrast test can't see any `Flat`-treatment state | unowned |
| `TD-134` | `SettingsDocument<TDocument>` has no per-consumer notion of "current version" | unowned |
| `TD-150` | `PersistenceStore`'s post-commit failure window: 0 of 3,330 tests would notice a revert | `WP 17.0C` |
| `TD-154` | CI's `linux-launch-smoke` marker now fires before the composition root runs | unowned |
| `TD-157` | "Pinned source superseded" warning can never fire; the resolver is never wired up | `WP 18.0B` (unresolved — see note) |
| `TD-174` | A Part carries none of what a calculation and a drawing need from it: no material assignment pinned to a released reference revision (`IPart.MaterialId` is a bare string nothing on the Desktop sets), no standard-versus-custom designation (a Component is the de-facto standard part but nothing says so), no part number distinct from the display name, no mass. **Not ERP**: no procurement, supplier, cost or stock fields; the attributes are the ones a calc sheet cites and a title block shows (Product Owner, second Windows review, 2026-09-09) | `D-028` (re-scoped: material is cited on evidence, `WP 18.0A`; part number, mass and standard-versus-custom deferred until a drawing or a calc sheet needs them) |

**Judgement calls, not named in any Work Package's "Closes" column:**
`TD-27` and `TD-150` sit squarely in the persistence/object-store
mechanism `WP 17.1A`/`WP 17.1B` replace, but neither row is literally
listed; `TD-28` sits in the refresh/notification mechanism `WP 18.1A`
replaces, same caveat. Owners other than "unowned" that are not one of
the programme Work Packages (`WP 18.0B`, `18.2B`,
`19.1B`, `17.0C`) are real, named in that WP's own "Closes"
column in `WorkPackages.md`, but fall outside the specific
"substrate"/"surface" set that rule defines — they are kept here,
with their real owner shown, rather than mislabelled "unowned."

**Verified against `v0.18.0`'s own "Closes" columns (`WP 18.9.0`,
2026-09-09) and left open, not moved:** `TD-41` — `ObjectEditorView.TryCreate`
still gates on `EngineeringDomainContext.Repository.FindAsync`, which still
returns `null` for a Requirement; the current tree's own
`CreatedObjectOpensRightUpTests` says so directly ("the generic editor
cannot resolve a Requirement yet, `TD-41`"). `TD-157` — `WP 18.0A`'s
citation-time refusal of an unreleased record is real (`EvidenceService`,
`RecordNotReleased`), but the row's own subject, the never-fires
"Pinned source superseded" warning, is unchanged: `TempestHost` registers
no `IReferencePinResolver`/`CatalogPinResolver`, so `CalculationPackValidationService`
and `VerificationArtefactValidationService` still resolve an empty
dictionary. `TD-174` — re-scoped by `D-028` (Product Owner, 2026-09-09): material is
cited on the evidence that used it (`WP 18.0A`), never assigned to the
Part, so the material half of this row is closed by decision;
`IPart.MaterialId` stays a bare string nothing sets. Part number, mass
and standard-versus-custom stay open until a drawing title block or a
calc sheet needs them, and are then one field each on
`KindEditorDeclarations.Part()` (`WP 18.2A`).

**Closed by `WP 18.0B-R1` (2026-09-09), with evidence — moved out of the
Live Backlog:** `TD-163`. The gap this row named was real: `WP 18.0A` gave
all 41 seeded records (materials 6, fasteners 7, bearings 2, standards 14,
constants 12) a structured source citation, but only `MaterialSeed`
reached a shipped call site
(`BracketCalculationWorkbench.PopulateMaterialLibraryAsync`); `FastenerSeed`,
`BearingSeed`, `StandardSeed` and `ConstantSeed` — 35 of the 41 records —
had none, so the Libraries tab (`WP 18.2A`) opened with four of its six
libraries permanently empty. `EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync`
— the one composition-root sequence both `Tempest.Desktop` and
`Tempest.Harness` run at start, already the seam `WP 18.1B`'s search-index
self-heal uses — now also applies all five seed datasets through the new
`ReferenceSeedService.ApplyIfEmptyAsync`: a library still holding no
record at all is populated, Draft, each record carrying its dataset's own
citation; a library already touched — by a person, or, in a debug/test
launch, by the sample module's own fictional Materials demonstration
record — is left exactly alone, so a re-launch adds nothing and the
shipped corpus can never override a value someone has since corrected.
The existing "Populate Material Library" button is unchanged and stays
correct: pressing it after the automatic pass reports the library already
held every shipped record rather than duplicating anything.
`tests/Tempest.Core.Tests/Population/StartupReferenceLibrarySeedingTests.cs`
proves, through the real composition root: a fresh host over an empty
root holds all 41 records after start, every one `Draft` with its
citation; a second start over the same root adds none; a root where a
material was registered by hand before first seeding leaves that library
alone while the other four still seed in full.
`tests/Tempest.Desktop.Tests/EngineeringDataJourneyTests.cs`'s own
composition-root assertion is updated to the new real counts a launch now
reaches. `TD-157` (the pin-supersession resolver) and the deferred
interpolation/value-band half of `ADR-0149` are unaffected and remain as
this section already describes them.

**Closed by `WP 19.0A` (2026-09-10), with evidence — moved out of the
Live Backlog:** `TD-76`. The row's own residual — real project context
already existed (`IProjectContext`/`ProjectContext`,
`src/Tempest.Workspace/Projects/`, predating this Work Package), but
`Project` carried none of the fields the archived register's own note
named as remaining: "customer, manager, dates, budget." `Project`
(`src/Tempest.Core/EngineeringDomain/Implementation/ProgrammeHierarchy.cs:62-111`)
now carries `ClientOrganisationId`, `PurchaseOrderReference`, `Budget`,
`RateCardPin`, `StartDate`, `TargetDate` and `ProjectManagerIdentityId`,
each mutated through its own `ProjectCommercialService.Set*Async` method
under one transaction with an audit row. Proven end-to-end, over a real
SQLite root, through a restart, by
`tests/Tempest.Core.Tests/Projects/ProjectCommercialJourneyTests.cs:32`
(`CommercialCore_TimeAndDeliverableCompletion_SurviveARestart_WithAuditRows`).

## Owned by Programme

Every remaining row closed by one of the nine substrate/surface Work
Packages, per that Work Package's own "Closes" column. These do not
need a backlog entry of their own — they close when the Work Package
lands and its tests pass, not by triage.

### Closed by `WP 17.1B`

Landed, with their tests, on `wp/17.1b-transactional` (`ADR-0145`). Each
was a consequence of an engineering object's truth living in four places
with no transaction boundary; all four writers now commit through one
`IPersistenceTransaction` under one domain-wide write lock, and memory is
mutated only after that commits.

| ID | Title | Closed by |
|---|---|---|
| `TD-142` | The refuse-after-mutate defect recurs on twelve concrete-Kind mutators | `MutateTypeStateAndPersistAsync` projects and commits the Kind's next type state before applying it to the Kind's own fields, so a failed commit leaves the field agreeing with disk |
| `TD-144` | A failed move's durable link write can leave a durable partial reparent | The `groupedUnder` reference record and the object state are written in one transaction; a failed move leaves neither |
| `TD-145` | Two concurrent moves can form an undetected parent cycle | `GuardAgainstCircularParent` runs inside the transaction, under the one write lock that spans the check and the write, so the walk reads a graph that cannot change under it |
| `TD-146` | A delete can commit while a concurrent move gives the object a live child | The live-children check runs inside the same transaction as the delete it guards |
| `TD-147` | **Was Release Blocking.** An object creation whose initial durable write fails still registers the object in memory | `EngineeringObjectFactory<T>.CreateAsync` registers the instance only after the transaction commits |
| `TD-148` | `DeleteAsync` can report failure after the soft delete already committed | There is one commit; a delete that reports failure did not commit |

Three rows this Work Package owns are **not** closed by it and stay in
the table below, with the reason: `TD-141` (the second relationship-write
path, `EngineeringRelationshipFactory.CreateAsync`, is transactional but
still carries no supersession guard, because it takes ids rather than an
instance and there is no public way to ask whether a source object has
been retired); `TD-158` and `TD-170` (both are compositions of several
already-transactional calls in `ReferenceDataCatalog` and the calculation
naming path, which need their own transaction boundary rather than this
one); and `TD-86`, `TD-95`, `TD-96` (batching, content-addressed
deduplication and streaming payloads, none of which this Work Package
touched).

| ID | Title | Owner |
|---|---|---|
| `TD-03` | No disposal tracking for reflection-constructed singletons | `WP 17.2A` |
| `TD-04` | `IHostedService` name clashes with `Microsoft.Extensions.Hosting.IHostedService` | `WP 17.2A` |
| `TD-12` | `IPersistenceStore` has no native query or filter capability | `WP 17.1A` |
| `TD-18` | `LinkAsync` concurrency under many simultaneous calls is untested | `WP 17.1A` |
| `TD-20` | `MaterialCatalog` reads a full revision history for a latest-only lookup | `WP 17.1A` |
| `TD-21` | `ICalculationDefinition.Calculate` carries no `CancellationToken` | `WP 18.0A` |
| `TD-22` | `CalculationContext` has no result bound; intermediate values aren't type-safe on read-back | `WP 18.0A` |
| `TD-23` | `VerificationService.RecordAsync`'s multi-step link sequence is not transactional | `WP 17.1B` |
| `TD-29` | `CalculationRecord` never retains its input, blocking a parameterless re-run | `WP 17.3A` / `WP 18.0A` |
| `TD-30` | `ICalculationResult`/`IVerificationResult`/`IApprovalGate` have zero implementations | `WP 18.0A` |
| `TD-32` | Verification's `verifiedBy` link is invisible to `RelationshipRepository` | `WP 17.1B` |
| `TD-36` | `PersistenceStore.DefaultRootPath` resolves relative to the process CWD | `WP 17.1A` |
| `TD-67` | Crash-window write ordering can strand an invisible orphan document | `WP 17.1A` |
| `TD-86` | Engineering object mutation writes are per-object and unbatched | `WP 17.1B` |
| `TD-88` | Startup rehydration is eager and linear, never lazy or project-scoped | `WP 17.1A` |
| `TD-95` | Attachment bytes are stored per attachment, never deduplicated by content | `WP 17.1B` |
| `TD-96` | `IBinaryPersistenceStore` materialises whole file content in memory | `WP 17.1B` |
| `TD-130` | Reconciliation services (one of which deletes data) have no authorization seam | `WP 17.2A` |
| `TD-137` | `PersistenceStore`'s atomic writes are crash-safe but not `fsync`'d | `WP 17.1A` |
| `TD-141` | Two durable relationship-write paths carry no supersession guard | `WP 17.1B` |
| `TD-149` | A deleted legacy-encoded record can resurrect as live on delete failure | `WP 17.1A` |
| `TD-156` | A superseded reference record keeps its secondary index entry | `WP 17.1A` |
| `TD-158` | `ReferenceDataCatalog` composes durable writes with no all-or-nothing semantics | `WP 17.1B` |
| `TD-169` | The canonical lifecycle permits no `Draft` → `Archived` transition | `WP 18.0A` |
| `TD-170` | Naming an executed calculation is create-then-link with no compensation | `WP 17.1B` |
| `TD-171` | Three verification models remain (`Core/Verification`, `EngineeringDomain/RequirementsVerification`, `EngineeringAssets/Verification`); collapse deferred to `WP 18.2B` | `WP 18.2B` |
| `TD-77` | Command Palette is not contextual; most real commands are unavailable there | `WP 19.2B` (residual — see note below the `WP 19.2B` closures; no Command Palette contextuality change landed in this Work Package's own worktree) |
| `TD-79` | Engineering Workspace has deep domain support and almost no dedicated UI | `WP 18.2A` |
| `TD-90` | A docking re-render does not restore keyboard focus | `WP 18.1A` (claimed, not closed — see note) |
| `TD-115` | Three registered commands have no production construction path | `WP 19.2A` (claimed, not closed — see note) |
| `TD-160` | The whole merged engineering capability has no Desktop UI surface | `WP 18.2A` (partial — see note) |
| `TD-165` | The bracket verification artefact cannot be filled in from the Desktop | `WP 18.2A` (claimed, not closed — see note) |

### Closed

Rows above whose owning Work Package has actually landed and whose fix
this table can now point at, rather than merely name.

| ID | Title | Closed by |
|---|---|---|
| `TD-01` | Two logging mechanisms coexist (`ILogger` vs. legacy `LoggingService`) | `WP 17.2A` — the legacy `LoggingService` no longer exists in the live tree, and `Tempest.Core.Logging.TempestLoggerProvider` now forwards any `Microsoft.Extensions.Logging` caller into the same, single `ILogger`/`ILogSink` pipeline rather than leaving it as an unconnected second mechanism. |
| `TD-173` | A verification result links its evidence to the Activity, never to the Requirement it names as Subject, so a Requirement's Verification Coverage always reads "Not Verified" | `WP 17.9.3` — `RecordVerificationResultCommandHandler` also links the record from the Activity's subject, so the subject's own coverage read finds it. |
| `TD-172` | Creation placement and visibility: objects created from the Ribbon or Palette were parentless, unreachable in their own tree, or shown on a tab the user was not looking at | `WP 17.9.3` (placement, Ungrouped, Manufacturing ids) and `WP 17.9.4` (the shell switches to the object's area, reveals it and opens it after every Create). |
| `TD-17` | Document revision content is an opaque string with no structured payload support | `WP 18.0A` — `Evidence`'s own `DeclaredFigure` (`src/Tempest.Core/Evidence/DeclaredFigure.cs`) is a named, typed quantity payload, checked against `EvidenceUnitCatalog.KnownUnits`, not the opaque `IDocumentRevision.Content` string the row named. |
| `TD-155` | Materials library reuses an incompatible payload shape under the old document Kind | `WP 18.0B` — `MaterialSpecificationDto` no longer exists anywhere in the tree; `MaterialCatalog` (`src/Tempest.Core/Materials/MaterialCatalog.cs`) is now solely `ReferenceDataCatalog<MaterialDefinition>`'s shape, so the incompatible payload the row named cannot recur. |
| `TD-175` | A Part has no BOM input at all: the bill of materials is authored on the Assembly (its lines: child, quantity, find number, item number, reference designator), and a Part shows only a read-only **Where used** readout derived from the assembly it sits in and that assembly's chain. **Not PLM**: the single-parent tree stays, there is no part-occurrence model, no multi-assembly usage tracking and no change control on BOM lines | `WP 18.1C` (model — `IHasBomLine`/`SetBomLineAsync` predate this programme) and `WP 18.2A` (page) — `KindEditorDeclarations.Part()` carries no Bill-of-Materials section, only a read-only *Where used* row built from `IHasParent.ParentId`; `KindEditorDeclarations.Assembly()` carries the editable BOM section, wired to `SetBomLineCommand` in `ObjectEditorView.cs`. |
| `TD-66` | Refresh-architecture debt beyond `TD-58`: Cockpit, Explorer, open tabs | `WP 18.1A` — `CockpitView`, `ProjectExplorerView` and `ObjectEditorView` now refresh from one `IWorkspaceChanges.Changed` event each; the old ad hoc `RefreshAsync`/reload call sites in those three views are gone. |
| `TD-108` | Blocking `.GetAwaiter().GetResult()` calls, several on the UI thread | `WP 18.1A-R1` — the specific instance this row and `WP 18.9.0`'s verification named is gone: `EngineeringCockpit.cs`'s own eight direct blocking reads and all six per-discipline `*CockpitReadModel.cs` collaborators (`Mechanical`/`Requirements`/`Calculations`/`Documents`/`Verification`/`Manufacturing`, seventeen more) are converted — each collaborator exposes an async `LoadAsync`, composed by `EngineeringCockpit.PrimeAsync`, which `CockpitView.RefreshAsync` awaits once per render; twenty-five blocking sites closed, verified by `NoBlockingPersistenceCallsTests`'s extended scan of `src/Tempest.Workspace`. Eleven remain, named and disclosed in that same test's allow-list: six are `IWorkspaceViewFactory.Create`'s own frozen, synchronous `WP8.0B` factory contract (an interface this remediation does not own — `RequirementsWorkspaceViewFactory.cs` and its five siblings), the other five predate this row (`WorkspaceManager.cs`'s own non-blocking rethrow, `MacroWorkspaceRegistration`'s startup composition) or are owned by a parallel Work Package (`WP 18.2B`'s `EvidenceObjectView.cs`). |
| `TD-118` | The Engineering Cockpit's read surface is synchronous by shape | `WP 18.1A-R1` — the shape itself is now async: `EngineeringCockpit.PrimeAsync(CancellationToken)` composes every discipline collaborator's own `Task LoadAsync`, plus this class's own cross-cutting reads (Decisions/Risks/Milestones/Tasks/Digital Thread/Recently Changed), and `CockpitView.RefreshAsync` awaits it before rendering a single card; no property on `EngineeringCockpit` or any of its six collaborators performs I/O of its own any more — each is a pure, in-memory read of what the last `PrimeAsync` loaded. |
| `TD-65` | Systemic Desktop accessibility gaps: dialogs, focus, `AutomationProperties` | `WP 19.2B` — the residual: `AutomationNameCoverageTests` walks a real running window's own logical tree across every rail surface and every project tab and asserts every `Button`/`TextBox`/`ComboBox`/`CheckBox`/`ListBox`/`TabItem`/`GridSplitter` carries a real `AutomationProperties.Name`, closing the gaps that structural walk found across `LibrariesView.cs`, `MacroManagerDialog.cs`, `RibbonView.cs`'s own discipline tabs, `CitationPicker.cs`, `SubjectPicker.cs`, `DeclaredFigureEntry.cs`, `CheckEntry.cs`, `IssueEntry.cs`, `ReviseReferenceRecordEntry.cs`, `OrganisationPicker.cs`, `RateCardPicker.cs`, `TimesheetEntryPrompt.cs` and `DeliverableCompletionPrompt.cs`; dialog modality was already installed on all twelve overlays and is unchanged. Keyboard reach for Digital Thread edges (`TD-128`) and docking moves (`TD-133`), both systemic accessibility gaps this row also named, close alongside it — see their own rows below. |
| `TD-73` | Rail and ribbon never compact; `MinWidth` bars small displays | `WP 19.2B` — `DesignTokens.CompactShellWidth` (1,240 → 1,200, the row's own figure) now folds both: `GlobalNavigationRail.SetCompact` already folded the rail to icons; `RibbonView.SetCompact` is new and hides every command button's own label, icon and automation name intact, so `LayoutWalkTests` shows every ribbon group at 1180×760 with no horizontal scrollbar. |
| `TD-74` | No global navigation architecture; three-level mock-up model collapsed to one | `WP 19.2B` — the rail is now Home, Projects, Evidence, Timesheets, Invoicing, Reports, Engineering Calculations, Settings, with Tasks/Commercial/Resources/Knowledge/Administration removed from `ShellAreas` entirely (not dimmed) and Engineering reached inside a project as its own Structure tab (`ProjectWorkspaceView`'s `_structureHost`) or from open-right-up, restoring the designed `Module → Project → Workspace` three-level model the row named as collapsed. |
| `TD-81` | Whole mock-up modules unimplemented: Tasks, Commercial, Resources, Knowledge, Admin | `WP 19.2B` — all five are removed from the rail (`ShellAreas`) rather than left as unimplemented placeholders; the two capabilities Reports named for itself (issued evidence sheets, project documents) are delivered by the new `ReportsView.cs` rail area instead of a dimmed module, and `ProjectAreas.Reports`/`ProjectAreas.Settings` (the project-tab-level counterpart) are removed the same way, so no descriptor anywhere still claims a capability with nothing behind it. |
| `TD-128` | Digital Thread graph edges are keyboard-unreachable | `WP 19.2B` — `DigitalThreadGraphView`'s own edge hit-test `Line` is now `Focusable`, in a deterministic Tab order following each node's own outgoing edges, named `"<from> → <to>"`, and `Enter`/`Space` selects the edge and moves keyboard focus to its target node; proven end-to-end, no simulated pointer event, by `KeyboardOnlyJourneyTests.AKeyboardOnlyJourney_SelectsADigitalThreadEdge`. |
| `TD-132` | Every relationship row's "Open" button shares one accessible name | `WP 19.2B` — `ObjectEditorView.BuildRelationshipRowAsync`'s own Open button is now named `"Open {direction} {relationshipKind} — {displayName}"` per row, mirroring the sibling `BuildObjectReferenceRowAsync`/attachment-row buttons that were already fixed this same way. |
| `TD-133` | Docking-panel repositioning and tab reordering are mouse-only | `WP 19.2B` — repositioning: with a panel header focused, `Ctrl+Shift+Arrow` moves it to the workspace edge in that direction (`LayoutTabGroupView`'s own `MoveRequested` event, `WorkspaceLayoutHost`'s `DockToEdge`), and `Ctrl+Shift+[`/`Ctrl+Shift+]` resizes its own split share (`WorkspaceLayoutTree.ResizeSplit`), both documented in the panel header's own `AutomationProperties.HelpText`; proven end-to-end by `KeyboardOnlyJourneyTests.AKeyboardOnlyJourney_MovesADockedPanelToTheOppositeEdge`. Tab *reordering* (dragging one tab before another within a group) is unchanged and stays mouse-only — out of this Work Package's own brief, which named panel repositioning and resizing only. |
| `TD-109` | `MainWindow` is a 1,577-line god object | `WP 19.2A` — `MainWindowComposer`'s four phases (`BuildViews` → `BuildCoordinators` → `Wire` → `Layout`, `src/Tempest.Desktop/Composition/MainWindowComposer*.cs`) replace the constructor; `MainWindow.cs` is now 782 lines, and `tests/Tempest.Desktop.Tests/MainWindowCompositionTests.cs:394` (`MainWindowComposer_FourPhases_ExistAndAreCalledInOrder`) pins that all four phases exist and are invoked, in that order, from `MainWindow`'s own constructor. |

**Claimed by `v0.18.0` Work Packages and verified NOT closed, `WP 18.9.0`
(2026-09-09):** `TD-90` — no focus-capture/restore mechanism exists
anywhere in the docking subsystem (`WorkspaceLayoutController`,
`WorkspaceLayoutHost`, `WorkspaceDockingComposer`: no `Focus` reference
in any of them); a docking re-render still does not restore keyboard
focus. `TD-108` and `TD-118` were re-verified true at that date —
`EngineeringCockpit`'s dozens of `.GetAwaiter().GetResult()` calls were
unchanged, and `CockpitView.Refresh()` still ran them via
`Dispatcher.UIThread.Post`, i.e. on the UI thread — and are **now closed
by `WP 18.1A-R1`**, below. `TD-160` — **partial only:** the new `LibrariesView`
(`src/Tempest.Desktop/Views/LibrariesView.cs`) now browses all five
reference libraries, closing one of the row's three named gaps, but
`EngineeringTraceRegister`'s `CalculationTrace` is still rendered nowhere
and `BracketEngineeringRecordService` is still reachable from no screen
(`TD-165`). `TD-165` — unchanged: `BracketEngineeringRecordService` has
no consumer anywhere under `src/Tempest.Desktop`.

**Re-verified on `release/v0.19.0`, `WP 19.9.0` (2026-09-10) — all five
`WP 18.9.0` findings above still hold, unchanged by this release's own
Work Packages:** `TD-90` — still no `Focus` reference in
`WorkspaceLayoutController`, `WorkspaceLayoutHost` or
`WorkspaceDockingComposer` (`WP 19.2B`'s own keyboard work for `TD-133`
added `Ctrl+Shift+Arrow`/`Ctrl+Shift+[`/`]` to `LayoutTabGroupView.cs`,
not a focus-restore path). `TD-108`/`TD-118` stay closed (`WP 18.1A-R1`
predates this branch's own point of divergence, `8df3466`, and nothing
in `v0.19.0` touches `EngineeringCockpit.PrimeAsync`). `TD-160` —
still partial: `CalculationTrace` still has no consumer under
`src/Tempest.Desktop`. `TD-165` — still unchanged:
`src/Tempest.Desktop/WorkspaceHost.cs` was not touched by any `v0.19.0`
Work Package; `BracketEngineeringRecordService` is constructed there
(line 266) and exposed as a property (line 401), but nothing under
`src/Tempest.Desktop` reads `WorkspaceHost.BracketEngineeringRecords`.

**Claimed by `v0.19.0` Work Packages and verified NOT closed, `WP 19.9.0`
(2026-09-10):** `TD-33` — `EngineeringCockpit.FormatCoverage` is now
`CockpitFormatting.FormatCoverage`
(`src/Tempest.Workspace/Workspace/CockpitFormatting.cs:29-30`, moved out
of `EngineeringCockpit` by `WP 12.0B`, long before this release) and
still returns the fixed string `"— (no requirements yet)"` for a zero
denominator regardless of which discipline calls it — its own remarks
disclose this explicitly as unfixed. `WP 19.1B`'s merge (`f4db8ac`) adds
the KPI read models and the Home cockpit's five cards; it never touches
`CockpitFormatting.cs`, and `EngineeringCockpit.cs` itself no longer
calls `FormatCoverage` at all (its placeholder KPI cards were removed),
so the row's real subject — the shared, wrong-discipline string still
used by `CalculationsCockpitReadModel`/`VerificationCockpitReadModel` —
is untouched. `TD-115` — the three commands
(`AddRequirementToCollectionCommand`, `CompareBaselinesCommand`,
`LinkRequirementCommand`) still have no production construction path;
`tests/Tempest.Core.Tests/Workspace/FutureCapabilityCommandTests.cs` (an
unchanged file on this branch, still asserting the absence as a `WP-H`
pinned decision pending `FCR-0073`, the object-picker) still passes.
`WP 19.2A`'s merge (`42319a2`) touches
`RequirementsWorkspaceRegistration.cs` only to replace string-literal
command ids with `RequirementsCommandIds` constants (its own actual
scope: `MainWindowComposer`, `WorkspaceViewCoordinator`,
`SurfaceCommandPolicy`) — no object picker, and no construction path for
any of the three commands, was added.

**`TD-02`, `WP 17.0C`, before this table existed:** "single-sink
limitation" — closed by `CompositeLogSink` (see that class's own
remarks) prior to the 2026-09-08 triage this file's Live Backlog is
built from, so it was never a row here to move.

**Considered for `WP 17.2A` and left in the table above, not closed:**
`TD-03`'s own text is "no disposal tracking for **reflection-constructed
singletons**" — `WP 17.2A` added one more `AddInstance`-registered,
already-tracked instance (`RollingFileLogSink`), not tracking for
`Singleton<TService, TImplementation>()` registrations, so the row's own
literal subject is untouched. `TD-04` (the `IHostedService` name clash)
and `TD-130` (reconciliation services' authorization seam) are outside
this Work Package's actual configuration/logging/identity/audit scope;
neither was touched. `TD-103` (the principal boundary) was already
closed by earlier Desktop work — `WorkspaceHost` was already
establishing a session principal before this Work Package began — so,
like `TD-02`, it was never a row here to move.

## Archived with the Layer

Not debt in a product that does not ship the layer:

| ID | Title | Reason |
|---|---|---|
| `TD-161` | New application surfaces write engineering assets with no project scope | `WP 18.0C` claims this (moved forward from `WP 19.0B`), but does not close it: `EngineeringTraceRegister` and `BracketEngineeringRecordService` are still live in `src/Tempest.Workspace`/`src/Tempest.Core`, not moved to `src/Frozen/`, and `AssetApplicability.ProjectIdentifiers` is still never populated anywhere in `src/`. Left here, not moved — its subject still ships. |
| `TD-162` | `ProjectDependencyRegister` (`P04`) is unreferenced | `WP 18.0C` claims this (moved forward from `WP 19.0B`), but does not close it: `src/Tempest.Workspace/Projects/ProjectDependencyRegister.cs` is outside `WP 18.0C`'s own stated scope (`Tempest.Core` namespaces only), was not moved to `src/Frozen/`, and remains unreferenced. Left here, not moved — its subject still ships, unconstructed and unconsumed. |
| `TD-82` | Companion (mobile/field) application has zero implementation on this branch | Companion is explicitly out of `v1.0.0` scope (`docs/releases/v1.0.0/WorkPackages.md`, "What v1.0.0 is") |
| `TD-13` | REST API identity resolution carries no real authentication | frozen by `WP 17.2A` — the inbound REST API moved to `src/Frozen/Tempest.Core.Api` (`ADR-0146`) |
| `TD-14` | No TLS on the REST API's own Kestrel listener | frozen by `WP 17.2A` |
| `TD-15` | Audit records the "unknown actor" for every REST-invoked command | frozen by `WP 17.2A` |
| `TD-16` | License file contents are trusted with no signature verification | frozen by `WP 17.2A` — Licensing moved to `src/Frozen/Tempest.Core.Licensing` (`ADR-0146`) |
| `TD-49` | TOCTOU window between plugin signature verification and load | frozen by `WP 17.2A` — the plugin trust platform moved to `src/Frozen/Tempest.Core.Plugins` (`ADR-0146`) |
| `TD-50` | First-party certificate trust is a filename convention, not a certificate attribute | frozen by `WP 17.2A` |
| `TD-53` | A hosted-service construction failure can be misclassified as non-critical | frozen by `WP 17.2A` |
| `TD-54` | `ITempestServiceProvider`'s DI non-registration is incidental, not enforced | frozen by `WP 17.2A` |
| `TD-55` | `PluginDeniedTypeRegistry` can wrongly deny an innocent shared assembly's types | frozen by `WP 17.2A` |
| `TD-56` | A plugin constructor runs with a `null` (first-party) component scope | frozen by `WP 17.2A` |
| `TD-61` | Plugin-folder containment check does not resolve symlinks | frozen by `WP 17.2A` |
| `TD-64` | `TD-52`'s gate closure has no end-to-end production-wiring test | frozen by `WP 17.2A` |
| `TD-129` | REST 404-vs-401 split lets an unauthenticated caller enumerate routes | frozen by `WP 17.2A` |

Sixteen rows land here in the currently-*open* subset — three from
earlier triage, and thirteen moved by `WP 17.2A` (`ADR-0146`): the
plugin-trust, REST and Licensing rows above, whose subject moved to
`src/Frozen/` and out of the `v1.0.0` build. The "about fifteen rows"
`WorkPackages.md` estimates for this bucket counts the full register,
most of which (the plugin-trust and REST rows, e.g. `TD-09`–`TD-11`) were
already `Resolved` and never entered this triage — this Work Package's
own move is a second, later wave the estimate did not anticipate by
number.

# ADR Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | ADR Register |
| **Purpose** | The complete, authoritative index of every Architecture Decision Record TempestOS has produced — what each one decided, which Work Package produced it, and whether it remains in force. |
| **Scope** | Every file in `docs/adr/`, from `ADR-0001` through the highest-numbered ADR present at time of review. |
| **Owner** | Project Maintainer — sole contributor of record across all 77 repository commits (git author `kreczmans-creator`; no separate architecture-review board or team structure exists as of this baseline). |
| **Source of Truth** | `docs/adr/` (the ADR files themselves). This register is a governance index over that source, not a replacement for it — the full Context/Decision/Consequences reasoning lives only in each ADR file. |
| **Review Frequency** | Updated whenever a new ADR is created, superseded, or reversed (Engineering Governance §5) — in practice, once per Work Package that meets the §5 ADR criteria. |
| **Last Reviewed** | 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — `ADR-0102` added (Accepted): fault-injection modules (a deliberately-always-failing module exists solely to validate module-isolation behaviour, ADR-0013) are isolated by project reference (a new `Tempest.Validation` project neither `Tempest.App` nor `Tempest.Desktop` references) *and* filtered by a default-excluded discovery marker (`IFaultInjectionModule`, `ReflectionFrameworkDiscoveryService`'s own new `includeFaultInjectionModules` flag, `ITempestHostBuilder.EnableFaultInjectionModules()`) — neither alone was sufficient (project isolation alone does not survive a future process that happens to load the assembly for its own reason; the marker alone leaves the module physically inside `Tempest.Samples`). 100 → 101 ADR total. `DuplicateNavigationSampleModule` moved out of `Tempest.Samples` into `Tempest.Validation.FaultInjection` as `DuplicateNavigationModule` in the same Work Package — closes a genuine, previously-undisclosed defect this Work Package's own investigation found: every real `Tempest.App`/`Tempest.Desktop` run permanently carried this module in `ModuleState.Failed`, confirmed directly against `ModuleLifecycleStabilityTests.cs`'s own pre-existing special-case exclusion (now deleted, no longer needed). See `WP12.3A Fault Injection & Validation Framework Architecture.md`, `WP12.3B Fault Injection & Validation Framework Implementation.md`. Previously reviewed 2026-08-11 (WP 11.3B, Presentation Strategy Implementation) — `ADR-0101` added (Accepted): `Tempest.App`/`WorkspaceShell` formally classified as TempestOS's Internal Engineering Harness, not a shipped product, ratifying `WP11.3A Presentation Strategy Review.md`'s own recommendation; cites `ADR-0068`/`ADR-0092` directly, neither modified. 99 → 100 ADR total. `TempestShell`/`IPage`/`PlaceholderPage` retired in the same Work Package (dead code since `ADR-0068`, `WP 8.1A`) — a code removal, not itself a new architectural decision requiring a further ADR. Previously reviewed 2026-08-11 (WP 10.9A, `v0.10.0` Release Candidate & Engineering Sign-Off) — reviewed, zero new ADRs added: this register had gone unreviewed since `WP 10.5C` despite five further Work Packages completing in the interim (`WP 10.6B`, `WP 10.6C`, `WP 10.6D`, `WP 10.7A`, `WP 10.8A`) — a disclosed drift, corrected here. Each was independently re-checked directly against Engineering Governance §5's own ADR-creation criteria: `WP 10.6B`/`WP 10.6D` were audit-only (no implementation); `WP 10.6C` was a methodology-artifact investigation (no production code changed); `WP 10.7A`/`WP 10.8A` both explicitly required "no new architecture" as a controlling constraint and were confirmed, by this Work Package's own direct source re-verification (zero diff on `ICommandDispatcher`/`ICommandRegistry`; `IWorkspaceManager` carries exactly the three precedented `ADR-0096`/`ADR-0097` Kind-keyed additions, no fourth), to have introduced none. 99 ADRs confirmed unchanged directly (`ls docs/adr/*.md` re-counted, matching the total this field already carried). `v0.10.0`'s own sixteenth Work Package by completion order. Previously reviewed 2026-08-10 (WP 10.5C, Commercial User Experience & Application Completion) — reviewed, zero new ADRs added: every real decision (a fifth colour-mapping class, `DisciplineColors`, following the identical already-established `HealthColors`/`SeverityColors`/`LifecycleColors`/`CategoryColors` pattern; two additive, defaulted trailing parameters on already-Workspace-owned data records) was checked directly against Engineering Governance §5's own ADR-creation criteria and found not to meet it (`WP10.5C Architecture Review.md`). Zero Workspace contract changes of any kind. 99 ADRs unchanged. `v0.10.0`'s own thirteenth Work Package by completion order. Previously reviewed 2026-08-10 (WP 10.6A, Command Execution & Productivity Experience) — three new ADRs added (all Accepted): `ADR-0098` (Undo/Redo is a Desktop-local `UndoableAction` delegate stack over `IWorkspaceManager.RenameObjectAsync`'s own already-Kind-agnostic dispatch, not a new `ICommand`/`IUndoableCommand` contract), `ADR-0099` (a Macro is realised as a registered `Command` — `RunMacroCommand` over `IMacroManager`, so the Command Palette/a future `IInputBindingProvider` invoke a macro through exactly the same path as any other command, with zero Command Framework special-casing), and `ADR-0100` (External Controller integration is an `IInputBindingProvider`/`IExternalControllerProvider`/`IInputBindingRegistry` abstraction — one real `KeyboardCommandBindingProvider`, one test-only `StubExternalControllerProvider`, zero vendor SDKs, per this Work Package's own explicit Out-of-Scope). 96 → 99 ADR total. Also found-and-fixed this register's own pre-existing drift: the Entries table was missing its own `ADR-0097` row (added, `WP 10.3A`), and the `Total`/`Related ADRs` fields were stale at `95`/`96` (both corrected). Two new Platform Services (`IMacroManager`, `IInputBindingRegistry`), zero Engineering Domain/Runtime files touched beyond `TempestHost.cs`'s own two new `services.Singleton` registration lines; every frozen `WP8.0B` Workspace contract and the Command Framework's own two public contracts (`ICommandDispatcher`/`ICommandRegistry`) remain unmodified — every new capability is additive (`RunMacroCommandHandler` is one new registered handler; `UndoableAction`/`IUndoRedoStack` are new, Desktop/App-layer-only types). `v0.10.0`'s own twelfth Work Package. Previously reviewed 2026-08-10 (WP 10.5B, Desktop Workflow & Professional Interaction) — reviewed, zero new ADRs added: this Work Package's own controlling instruction explicitly required any genuine architectural finding be documented via ADR rather than silently implemented, and none was found — every real decision (a four-dialog Framework sharing tokens rather than a common base class, `WindowUiState`'s own persistence shape mirroring `DesktopPanelUiState`, wiring exactly one discipline's Create/Duplicate flow) is Desktop-local, additive, and reversible, independently assessed against Engineering Governance §5's own ADR-creation criteria and found not to meet it (`WP10.5B Architecture Review.md` §1-§2). Zero files touched under `src/Tempest.Core/` or `src/Tempest.App/Workspace/`. 96 ADRs unchanged. `v0.10.0`'s own eleventh Work Package. Previously reviewed 2026-08-10 (WP 10.5A, Workspace Visual Polish & Engineering User Experience) — reviewed, zero new ADRs added: this Work Package's own controlling instruction explicitly required any genuine architectural finding be documented via ADR rather than silently implemented, and none was found — every real decision (a new theme-reactive brush helper, four new reusable feedback controls, two closed Technical Debt items) is Desktop-local, additive, and reversible, independently assessed against Engineering Governance §5's own ADR-creation criteria and found not to meet it (`WP10.5A Architecture Review.md` §1). Zero files touched under `src/Tempest.Core/` or `src/Tempest.App/Workspace/` — matching `WP 10.3B`/`WP 10.4A`'s own cleanest layering-compatibility result again. 96 ADRs unchanged. `v0.10.0`'s own tenth Work Package. Previously reviewed 2026-08-09 (WP 10.4A, Digital Thread Visualisation) — reviewed, zero new ADRs added: this Work Package's own controlling instruction named `ADR-0093` directly ("Honour `ADR-0093`") rather than commissioning a new decision — realising an already-Accepted ADR as real, working code is not itself a new decision meeting Engineering Governance §5's own ADR-creation criteria. Every named scope item realised entirely within `Tempest.Desktop` (`DigitalThreadGraphModel`/`DigitalThreadGraphView`, new; `MainWindow`, modified) — zero files under `src/Tempest.Core/` or `src/Tempest.App/Workspace/` touched, matching `WP 10.3B`'s own cleanest layering-compatibility result exactly. The one genuine architectural choice this Work Package made — reusing `ObjectEditorView`'s own bidirectional `RelationshipRepository` read over `IEvidenceComposer` — was assessed directly against Engineering Governance §5's own ADR-creation criteria and found not to meet it either: choosing which already-existing read to reuse is an implementation-stage judgment, not a new architectural decision (`WP10.4A Architecture Review.md` §2). 96 ADRs unchanged. `v0.10.0`'s own ninth Work Package. Previously reviewed 2026-08-09 (WP 10.3B, Ribbon, Toolbar & Command Experience) — reviewed, zero new ADRs added: every named scope item realised entirely within `Tempest.Desktop` (`RibbonView`, new; `CommandPaletteOverlay`/`StatusBarView`/`MainWindow`, modified) — zero files under `src/Tempest.Core/` or `src/Tempest.App/Workspace/` touched, so none of the twelve frozen `WP8.0B` Workspace contracts, `ADR-0096`, or `ADR-0097` were even reachable to extend. Reusing the already-Kind-keyed Rename/Delete/Revise dispatch verbs for the Ribbon's own selection-aware buttons was assessed directly against Engineering Governance §5's own ADR-creation criteria and found not to meet it — applying an existing, already-decided pattern a further time is not itself a new decision (`WP10.3B Architecture Review.md` §2). 96 ADRs unchanged. `v0.10.0`'s own eighth Work Package — the cleanest layering-compatibility result of any Work Package this release. Previously reviewed 2026-08-09 (WP 10.3A, Engineering Object Editors) — `ADR-0097` added (Accepted): `IWorkspaceManager` gains a sixth Kind-keyed provider category (`RegisterReviseFactory`/`CanRevise`/`ReviseObjectAsync`), additive only, mirroring `ADR-0096`'s own `RegisterRenameFactory` shape exactly a third time — realises the Object Editor Framework's own real, dispatched Content field, honouring `ADR-0063`'s own "every mutation dispatches through a Command" decision throughout. 95 → 96 ADR total. One genuinely new command written (`ReviseMechanicalObjectCommand`) — Mechanical was the only discipline of six with no Revise command before this Work Package, closed here, restoring symmetry. Zero Engineering Domain/Runtime files touched; every other Workspace contract unchanged. `v0.10.0`'s own seventh Work Package. Previously reviewed 2026-08-09 (WP 10.2B, Docking & Workspace Layouts) — reviewed, zero new ADRs added: every named scope item (Bottom dock, Collapse, Auto-Hide, saved/restored/reset layouts, three predefined presets, the new Output panel) is realised without touching any of the twelve frozen `WP8.0B` Workspace contracts — `WorkspaceDockPosition.Bottom` was already a real enum member since `WP 8.0B`, simply never wired to a dock surface before now; `IWorkspaceLayout.SetPlacement`/`ResetToDefault` are called, never extended; Collapse/Auto-Hide/Output are new, additive Desktop-local types (`DockingGrid`, `PanelHostControl`, `PredefinedLayouts`, `DesktopPanelUiState`, a fourth `IWorkspacePanel` implementer `OutputPanel`) persisted through a second, sibling `ISettingsProvider` key alongside `WorkspaceState`'s own — the identical "extend additively at the Desktop layer, never reopen a frozen shape" discipline `ADR-0080`/`ADR-0082`/`ADR-0096` each already established, applied here by needing no contract surface at all, not even an additive one. 95 ADRs unchanged. Also corrects this register's own stale `Related ADRs` field (read "All 94" while 95 rows have been present since `WP 10.2A`) — disclosed and fixed here, the same class of drift `WP 9.1A`/`WP 10.0A` each previously found and fixed in this identical field. `v0.10.0`'s own sixth Work Package. Previously reviewed 2026-08-07 (WP 10.2A, Workspace Modernisation) — `ADR-0096` added (Accepted): `IWorkspaceManager` gains a fourth and fifth Kind-keyed provider category (`RegisterRenameFactory`/`RegisterDeleteFactory`, plus `CanRename`/`CanDelete`/`RenameObjectAsync`/`DeleteObjectAsync`), additive only, mirroring `ADR-0082`'s own `RegisterFacetProvider` shape exactly — realises the real object Rename/Delete dispatch "inline rename"/"editable controls where appropriate" required, closing a genuine, `WP 9.0A`-disclosed gap ("a future context-menu action," never previously built). 94 → 95 ADR total. Zero Engineering Domain/Runtime files touched; every other Workspace contract unchanged. Previously reviewed 2026-08-07 (WP 10.1B, Runtime Host & Module Discovery Hardening) — reviewed, zero new ADRs added: both fixes (`TD-26` at its own `WorkspaceManager` source, `TD-37` via idempotent sample-module seeding plus test isolation) are implementation-level corrections to already-decided behaviour, not new decisions meeting Engineering Governance §5's own ADR-creation criteria — no existing ADR's own Context/Decision/Consequences was reopened or reversed. 94 ADRs unchanged. `ADR-0095` remains reserved, not yet written. Previously reviewed 2026-08-07 (WP 10.1A, Engineering Cockpit Implementation) — reviewed, zero new ADRs added: this Work Package extends `EngineeringCockpit` (not one of the twelve frozen `WP 8.0B` Workspace contracts, a disclosed implementation-phase class every real-discipline Work Package since `WP 9.0A` has already extended) and upgrades `Tempest.Desktop`'s own `TD-26` mitigation strategy — neither is a decision meeting Engineering Governance §5's own ADR-creation criteria. 94 ADRs unchanged. Previously reviewed 2026-08-07 (WP 10.0B, Desktop Application Framework) — ADR-0094 added (Accepted): Avalonia 11.2.3 selected and justified as the concrete cross-platform .NET desktop UI framework `ADR-0092` reserved, resolving the first of `WP 10.0A`'s own two reserved ADR numbers. 93 → 94 ADR total. This is `v0.10.0`'s own second Work Package, and its first to write real implementation code — `Tempest.Desktop`, a new project, plus a shared `EngineeringWorkspaceComposer` extracted from `Tempest.App`'s own console entry point. Zero Workspace contract touched, confirmed by direct re-read of all twelve `WP 8.0B` contracts against the new implementation; two small, disclosed, non-contract changes made in `Tempest.App.Workspace` (an `InternalsVisibleTo` grant, and a found-and-fixed pre-existing defect — see Technical Debt Register `TD-35`). `ADR-0095` (floating/multi-monitor panel contract extension) remains reserved, not yet written. Previously reviewed 2026-08-07 (WP 10.0A, User Experience Architecture) — ADR-0092/ADR-0093 added (both Accepted), and this register's own first two Superseded markings: ADR-0092 supersedes ADR-0066 (Engineering Workspace presentation moves from a terminal interface to a graphical desktop application, resolving ADR-0066's own named reversal condition — a real, demonstrated need — met by the Product Owner's own Programme 10 commissioning), and ADR-0093 supersedes ADR-0065 (Object Relationship Views render one-hop composed reads as a progressively-expandable node-link graph, carrying forward ADR-0065's own "no new traversal mechanism" finding rather than reversing it). Both superseded files' own Status sections were edited to add a superseded note per Engineering Governance §5; their own Context/Decision/Consequences bodies are unedited. 91 → 93 ADR total (2 of the 93 now Superseded, 91 Accepted). Two ADR numbers newly reserved (`ADR-0094`, `ADR-0095`), not yet written, mirroring `WP 8.0A`'s own original precedent for `ADR-0066`/`ADR-0067`. Also corrects this register's own previously-stale `Related ADRs` field (read "All 85" while 91 rows were already present) and confirms zero `src/`/`tests/` files touched by this architecture-only Work Package, per its own explicit "no implementation, no code, no contract changes" constraint. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — Second Pass) — reviewed, zero new ADRs added: a second, independent verification pass, commissioned after `WP 9.8B` closed the first pass's own top standing recommendation. All 91 ADRs re-verified directly a second time, 91 total unchanged since the first pass — `WP 9.8B` (documentation-only) introduced no ADR. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — First Pass) — reviewed, zero new ADRs added: verification-only Work Package, per its own explicit "no architectural changes" constraint. All 91 ADRs (`ADR-0001`–`ADR-0091`) re-verified directly against `docs/adr/`, zero gaps, zero non-Accepted status lines, 91 total unchanged — see `WP9.9.0 Release Readiness Report.md` §8 (Architecture Review) and `WP9.9.0 Architecture Baseline Summary.md`. Previously reviewed 2026-08-07 (WP 9.5A, Manufacturing Workspace) — ADR-0091 added (Accepted) — one genuinely new implementation-stage decision: Routings/Operations/Supplier Operations (three of this Work Package's own thirteen named scope items) are realised as `Classification`-tagged `ManufacturingOperation` objects, sequenced via the existing `IHasBomLine.ItemNumber` field, rather than a new `Routing`/`SupplierOperation` Domain Kind or a new sequencing mechanism (ADR-0091); 90 → 91 ADR total, zero Domain-layer (`Tempest.Core`) files touched — the fourth real-discipline Work Package needing none, after WP 9.2A, WP 9.4A, and WP 9.3A. This Work Package's own controlling instruction closes with "await instruction before WP 9.9.0 Release Preparation," skipping WP 9.6A–WP 9.8A entirely — recorded here as a plain observation, not an inconsistency requiring correction. Previously reviewed 2026-08-07 (WP 9.3A, Verification Management Workspace) — ADR-0089/ADR-0090 added (both Accepted) — two genuinely new implementation-stage decisions: "Execute"/"Record Result"/"Attach Evidence" realised as one command over `IVerificationService.RecordAsync`, since the Framework has no separate execution step needing a `CalculationTemplateRegistry`-equivalent adapter (ADR-0089), and "Verification Plan"/"Verification Activity" realised as one Domain Kind (`VerificationActivity`) distinguished only by `LifecycleState`, with Review/Approve/Archive as `CommandDescriptor` aliases mirroring `ADR-0087` (ADR-0090); 88 → 90 ADR total, zero Domain-layer (`Tempest.Core`) files touched — the third real-discipline Work Package needing none, after WP 9.2A and WP 9.4A. This Work Package closes the disclosed `WP 9.3A` numbering gap `WP 9.2A` left open and `WP 9.4A` recommended filling (`FCR-0055`); it completed, in real time, after `WP 9.4A`, despite its own earlier number — recorded here plainly, not reordered. Previously reviewed 2026-08-06 (WP 9.4A, Engineering Documents Workspace) — ADR-0088 added (Accepted) — one genuinely new implementation-stage decision: Specification/Report/Procedure/Standard/Datasheet/External Reference (six of this Work Package's own eight named Document types) are realised as `Classification`-tagged `Document` objects rather than five new concrete Domain classes, since no dedicated Domain Kind for any of them exists anywhere in the platform (ADR-0088); 87 → 88 ADR total, zero Domain-layer (`Tempest.Core`) files touched — a second consecutive real-discipline Work Package needing none, after WP 9.2A. This Work Package's own controlling instruction was received under a disclosed `WP 9.3A` numbering gap (see `PROJECT_STATUS.md`), not silently resolved by this register. Previously reviewed 2026-08-05 (WP 9.2A, Engineering Calculations Workspace) — ADR-0086/ADR-0087 added (both Accepted) — two genuinely new implementation-stage decisions, both confined entirely to the Workspace layer: a Workspace-layer, JSON-marshalled type-erasure adapter (`CalculationTemplateRegistry`) connecting the already-real Calculation Framework to one non-generic Execute/Recalculate command, never a Domain-layer registry (ADR-0086), and Calculation Management's Lock/Unlock/Review/Approve/Archive verbs realised as `CommandDescriptor` aliases over the existing `IHasLifecycle.TransitionAsync`/`LifecycleTransitionTable`, with Approval State read from `LifecycleState` alone, since no `IApprovalGate`/`IApproval` implementation exists anywhere in the platform (ADR-0087); 85 → 87 ADR total, zero Domain-layer (`Tempest.Core`) files touched by either. Previously reviewed 2026-08-05 (WP 9.1A, Requirements Management Workspace) — ADR-0084/ADR-0085 added (both Accepted) — two genuinely new implementation-stage decisions: additive `IRequirementsService` lifecycle/ownership/priority/enumeration methods rather than a facet-composition retrofit onto the Requirements framework's own genuinely different immutable-snapshot architecture (ADR-0084), and additive `SelectedItems`/`ToggleSelectionAsync` members on the frozen `ISelectionService`/`IWorkspaceContext` contracts resolving `FCR-0039` (ADR-0085), both mirroring `ADR-0080`/`ADR-0082`'s own established "extend additively, never reopen a frozen shape" pattern; 83 → 85 ADR total. Also corrects this register's own previously-stale `Related ADRs` count (read "All 79" while 83 rows were already present) — disclosed here, fixed to the current total, not a silent correction. Previously reviewed 2026-08-05 (WP 9.0B, Product Configuration & BOM Management) — ADR-0083 added (Accepted) — one genuinely new decision, a fourth additive Domain facet (`IHasBomLine`) following `ADR-0080`'s own composition pattern, plus the disclosed reasoning for why Unit of Measure deliberately does not reuse `Tempest.Core.UnitsAndQuantities`; 82 → 83 ADR total. Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — ADR-0080/ADR-0081/ADR-0082 added (all Accepted) — three genuinely new implementation-stage decisions, this Work Package's own first three ADRs against `v0.9.0`'s own Mechanical Foundation phase: additive Domain structural-mutation facets rather than reopening any frozen `WP8.2B` interface (ADR-0080), a live `ParentId` plus an append-only relationship history rather than mutating the frozen `ChildIds`/`ParentAssemblyId` (ADR-0081), and a third Kind-keyed Workspace provider category added to the frozen `IWorkspaceManager` contract (ADR-0082); 79 → 82 ADR total. Previously reviewed 2026-08-04 (WP 8.2C, Engineering Domain Implementation) — ADR-0077/ADR-0078/ADR-0079 added (all Accepted) — three genuinely new implementation-stage decisions: reusing the existing `IEngineeringDocumentStore` in production while introducing a new in-memory repository layer (resolving a tension against ADR-0072), not duplicating the five already-Implemented canonical Kinds, and realising object/relationship factories as few generic types rather than dozens of hand-written ones; 76 → 79 ADR total. Previously reviewed 2026-08-04 (WP 8.2B, Engineering Domain Contracts) — ADR-0075/ADR-0076 added (both Accepted) — two genuinely new contract-shape decisions, resolving composition-over-inheritance and relationship-category governance against ADR-0072/ADR-0073's own already-locked platform decisions; 74 → 76 ADR total. Previously reviewed 2026-08-04 (WP 8.2A, Engineering Domain Architecture) — ADR-0072 through ADR-0074 added (all Accepted) — three genuinely new platform-wide decisions, each formalising a pattern the Engineering Core's own four already-shipped frameworks had independently converged on; 71 → 74 ADR total. Previously reviewed 2026-08-04 (WP 8.1B, Navigation & Project Explorer) — ADR-0071 added (Accepted) — corrects ADR-0067's own worked registration example against the real Host/Workspace boundary ADR-0062 already established; 70 → 71 ADR total. Previously reviewed 2026-08-04 (WP 8.0C, Engineering Workspace UX Specification) — ADR-0069/ADR-0070 added (both Accepted) — two genuinely new decisions surfaced by UX specification, not reserved numbers answered; 68 → 70 ADR total. Previously reviewed 2026-07-30 (WP 8.1A, Workspace Shell) — ADR-0068 added (Accepted) — a genuinely new decision (`Tempest.App`'s own default launch target), not a reserved number answered; 67 → 68 ADR total. Previously reviewed 2026-07-30 (WP 8.0B, Workspace Contracts) — ADR-0066/ADR-0067 added (both Accepted), resolving both ADRs `WP 8.0A` reserved — zero reserved-but-unwritten ADR numbers remain. Previously reviewed 2026-07-30 (WP 8.0A, Engineering Workspace Architecture) — ADR-0062 through ADR-0065 added (all Accepted), the first ADRs of the `v0.8.0` release; ADR-0066/ADR-0067 newly reserved for a future Contract Review Work Package, not yet written. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — ADR-0058 through ADR-0061 added (all Accepted), closing the entire reserved range `WP7.2C Required ADR Catalogue.md` named. Previously reviewed 2026-07-30 (WP 7.1E, Verification Framework) — ADR-0057 added (Accepted) — the fifth and final Engineering Foundation framework ADR, closing the `ADR-0053`–`ADR-0057` range `WP7.0C Required ADR Catalogue.md` reserved. Previously reviewed 2026-07-30 (WP 7.1D, Engineering Calculation Framework) — ADR-0056 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1C, Materials Framework) — ADR-0055 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1B, Units & Quantities Framework) — ADR-0054 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1A, Engineering Data Model) — ADR-0053 added (Accepted); disclosed a small, previously-uncorrected staleness in this very field (it had not been updated since WP 6.6, despite WP 7.0C's own edit to this register's Numbering Integrity narrative in the interim). Previously reviewed 2026-07-29 (WP 6.6, Licensing). |
| **Related Documents** | `docs/academy/06 Engineering Standards/Engineering Governance.md` (§5, ADR Creation Rules); `Decision Register.md`; `Rejected Designs Register.md`; `Traceability Matrix.md`; `docs/releases/v0.6.0/Required ADRs.md`. |
| **Related ADRs** | All 101 — this register's entire subject matter. |
| **Related Academy Articles** | Every Work Package retrospective under `docs/academy/03 Work Packages/` cites the ADR(s) it produced or realised; see each retrospective's own "ADR references" or "Architectural Principles" section. |
| **Coverage Status** | Complete — every ADR file present in `docs/adr/` at time of review is listed below. |

---

## How to Read This Register

Each ADR is **Verified** directly from its own file: number, title, Status
line, and originating Work Package are all read from the file itself, not
inferred or assumed. **As of `WP 10.0A` (2026-08-07), two ADRs carry a
Superseded status** (`ADR-0065`, `ADR-0066` — see Entries below and
`ADR-0092`/`ADR-0093`, the ADRs that superseded them) — the first use of
this project's own supersession mechanism (`Engineering Governance.md`
§5) across 93 ADRs. Every other ADR carries a **Status: Accepted**
line, verified directly.

## Entries

| ADR | Title | Status | Originating Work Package | Date | Verification |
|---|---|---|---|---|---|
| ADR-0001 | RuntimeModule Is Immutable | Accepted | WP 2.2 | 2026-07-22 | Verified |
| ADR-0002 | Lifecycle State Is Managed Externally, Not On the Module | Accepted | WP 2.3 | 2026-07-22 | Verified |
| ADR-0003 | Module Constructors Must Be Side-Effect-Free | Accepted | WP 2.1 (reaffirmed WP 2.3, WP 2.4) | 2026-07-21/22 | Verified |
| ADR-0004 | Dispose Is Permitted From Every State Except Disposed | Accepted | WP 2.3 (reviewed under architectural review, WP 2.7B) | 2026-07-22 | Verified |
| ADR-0005 | Build a Custom, Minimal Dependency Injection Container | Accepted | WP 2.4 | 2026-07-22 | Verified |
| ADR-0006 | Constructor Injection Only | Accepted | WP 2.4 | 2026-07-22 | Verified |
| ADR-0007 | The Service Provider Owns All Module Construction | Accepted | WP 2.4 | 2026-07-22 | Verified |
| ADR-0008 | Discovery Does Not Depend on the Dependency Injection Container | Accepted | WP 2.1 (reaffirmed WP 2.4) | 2026-07-21/22 | Verified |
| ADR-0009 | The Composition Root Owns Externally-Created Services | Accepted | WP 2.5 | 2026-07-22 | Verified |
| ADR-0010 | The Module Pipeline Depends on the Logging Abstraction, Not a Concrete Logger | Accepted | WP 2.6 | 2026-07-22 | Verified |
| ADR-0011 | Discovery and Registration Precede Dependency Injection Container Construction | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0012 | The Runtime Host Owns Its Own, Independent State Machine | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0013 | Platform-Service Failures Abort Host Startup; Module Failures Remain Isolated | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0014 | Cancellation and Shutdown-Request Are Distinct Signals | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0015 | Runtime Hosts Are Not Restartable | Accepted | WP 2.7 (Open Question 2) | 2026-07-22 | Verified |
| ADR-0016 | The Host Lives in Tempest.Core.Runtime, Distinct From Tempest.Core.Hosting | Accepted | WP 2.7 (Open Question 3) | 2026-07-22 | Verified |
| ADR-0017 | Discovery, Registration, and Lifecycle Remain Host-Owned Collaborators, Not Public DI Services | Accepted | WP 2.7 (Open Question 4) | 2026-07-22 | Verified |
| ADR-0018 | Startup Cancellation Transitions to Controlled Shutdown | Accepted | WP 2.7 (final open question) | 2026-07-22 | Verified |
| ADR-0019 | Host Disposal Is Always an Explicit, Idempotent Call | Accepted | WP 2.7B | 2026-07-22 | Verified |
| ADR-0020 | The Event Bus Is a DI-Public Platform Service | Accepted | v0.4.0 planning (WP 4.0 / WP 4.4) | 2026-07-23 | Verified |
| ADR-0021 | Background Service Failures Are Isolated by Default; Criticality Is Opt-In | Accepted | v0.4.0 planning (WP 4.0 / WP 4.5) | 2026-07-23 | Verified |
| ADR-0022 | Navigation and Commands Are Orthogonal Platform Services | Accepted | v0.4.0 planning (WP 4.0 / WP 4.6A / WP 4.7) | 2026-07-23 | Verified |
| ADR-0023 | Platform Layering — Dependencies Flow Downward Only | Accepted | v0.4.0 planning (platform-wide) | 2026-07-23 | Verified |
| ADR-0024 | Platform Contracts Are Packaged by Capability, Not a Shared Contracts Namespace | Accepted | WP 4.0 | 2026-07-23 | Verified |
| ADR-0025 | Plugin Failure Classification | Accepted | WP 4.2B | 2026-07-23 | Verified |
| ADR-0026 | Plugin Discovery and Plugin Loading Lifecycle Placement | Accepted | WP 4.2C | 2026-07-23 | Verified |
| ADR-0027 | A Declarative `ModuleMetadataAttribute` Decouples Discovery From Construction | Accepted | WP 4.4A | 2026-07-24 | Verified |
| ADR-0028 | Event Bus Dispatch, Subscription, and Failure Model | Accepted | WP 4.4 (architecture) | 2026-07-25 | Verified |
| ADR-0029 | Background Service Discovery, Ownership, and Orchestration Model | Accepted | WP 4.5 (design phase) | 2026-07-25 | Verified |
| ADR-0030 | Background Service Host Lifecycle Placement | Accepted | WP 4.5 (design phase) | 2026-07-25 | Verified |
| ADR-0031 | Navigation Contracts Belong in Tempest.Core; Rendering Remains an Application Responsibility | Accepted | WP 5.0A (Navigation Framework Architecture) | 2026-07-27 | Verified |
| ADR-0032 | Navigation Is a DI-Public Platform Service, Registered Imperatively, Reusing the Event Bus | Accepted | WP 5.0A (Navigation Framework Architecture) | 2026-07-27 | Verified |
| ADR-0033 | The Shell Is a Composition Root Layered Above the Runtime Host, Not a Module or a Hosted Service | Accepted | WP 5.0C (Shell & Composition Framework Architecture) | 2026-07-27 | Verified |
| ADR-0034 | ITempestHost Exposes a Read-Only Service Resolution Surface for External Consumers | Accepted | WP 5.0C (Shell & Composition Framework Architecture) | 2026-07-27 | Verified |
| ADR-0035 | The Shell Owns Page/View Construction, Independent of the Platform's DI Container | Accepted | WP 5.0C (Shell & Composition Framework Architecture) | 2026-07-27 | Verified |
| ADR-0036 | The Command Framework Is a DI-Public Platform Service | Accepted | WP 5.1A (Command Framework Architecture) | 2026-07-28 | Verified |
| ADR-0037 | Commands Register Imperatively, in Two Parts — a Type-Keyed Handler and an Id-Keyed Descriptor | Accepted | WP 5.1A (Command Framework Architecture) | 2026-07-28 | Verified |
| ADR-0038 | Command Dispatch Propagates Handler Exceptions to the Caller, Diverging Deliberately from the Event Bus's Per-Subscriber Isolation | Accepted | WP 5.1A (Command Framework Architecture) | 2026-07-28 | Verified |
| ADR-0039 | Diagnostics Is a DI-Public, Lazily-Projected Read-Only Service Over Host-Owned Lifecycle State | Accepted | WP 5.2 (Diagnostics Improvements) | 2026-07-28 | Verified |
| ADR-0040 | Reporting Is DI-Public and Orthogonal to Export/Import — Template Abstraction, Cross-Service Integration, and Scope Boundaries | Accepted | WP 6.0 (Reporting Framework) | 2026-07-29 | Verified |
| ADR-0041 | A Shared Persistence Abstraction Serves Settings and Audit | Accepted | WP 6.4 (Settings Framework) | 2026-07-29 | Verified |
| ADR-0042 | Settings Is DI-Public and Distinct From Configuration | Accepted | WP 6.4 (Settings Framework) | 2026-07-29 | Verified |
| ADR-0043 | Identity Model Scope Is Local-Only, Extensible | Accepted | WP 6.1 (Permissions & Identity) | 2026-07-29 | Verified |
| ADR-0044 | `IPermissionEvaluator` Is the Single Authorization Enforcement Point; `CurrentPrincipalAccessor` Is Ambient, Not Request-Scoped | Accepted | WP 6.1 (Permissions & Identity) | 2026-07-29 | Verified |
| ADR-0045 | Audit Is a Durable, Queryable, Append-Only Record, Distinct From Logging and Diagnostics — Recording Model, Permission Gating, and Persistence Sufficiency | Accepted | WP 6.5 (Audit Framework) | 2026-07-29 | Verified |
| ADR-0046 | Notifications Are Derived From Events, Not a Replacement Pub/Sub — Dispatch Model, Severity/Category Elaboration, and Logging Level | Accepted | WP 6.2 (Notification Framework) | 2026-07-29 | Verified |
| ADR-0047 | The REST API Is a Background Hosted Service | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0048 | REST Endpoints Dispatch Through the Existing Command Framework | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0049 | Adopting ASP.NET Core/Kestrel for the REST API | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0050 | License Validation Is a Host-Startup, Host-Fatal Gate — Except a Missing License File, Which Is a Valid, Unrestricted Default | Accepted | WP 6.6 (Licensing Framework) | 2026-07-29 | Verified |
| ADR-0051 | Export/Import Is Orthogonal to the Internal Persistence Abstraction — Kind Routing, Format/Serialization Abstractions, and Scope Boundaries | Accepted | WP 6.7 (Export/Import) | 2026-07-29 | Verified |
| ADR-0052 | The REST API Resolves Identity Per-Request Without Touching the Ambient Current Principal — Empirically Verified | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0053 | The Engineering Data Model Is Built Directly on the Existing Persistence Abstraction — No New Storage Mechanism | Accepted | WP 7.1A (Engineering Data Model) | 2026-07-30 | Verified |
| ADR-0054 | Units & Quantities — Representation, Precision, and Registration Model | Accepted | WP 7.1B (Units & Quantities Framework) | 2026-07-30 | Verified |
| ADR-0055 | Materials Framework — Property Typing and Platform-Service Classification | Accepted | WP 7.1C (Materials Framework) | 2026-07-30 | Verified |
| ADR-0056 | Calculation Framework — Purity Enforcement and Dispatch Model | Accepted | WP 7.1D (Engineering Calculation Framework) | 2026-07-30 | Verified |
| ADR-0057 | Verification Framework — Relationship to Audit and Method Vocabulary | Accepted | WP 7.1E (Verification Framework) | 2026-07-30 | Verified |
| ADR-0058 | Requirements Engine Classification, Storage, and Relationship to the Engineering Data Model | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0059 | Requirement Identity, Status, and Category Representation | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0060 | Requirement Concurrency and Traceability Integrity Model | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0061 | Requirements Engine — Internal vs. Calling-Layer Permission Enforcement | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0062 | Engineering Workspace Is a Graphical Evolution of the Composition Root, Additive to the Console Shell | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0063 | Workspace Views Read Directly; Mutations Dispatch Through the Command Framework | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0064 | Workspace Layout and Session State Is Persisted via the Existing Settings Service | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0065 | Digital Thread Visualisation Composes Existing Reads, Introduces No New Traversal Mechanism | **Superseded by ADR-0093** | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0066 | Engineering Workspace Presentation Is Terminal-Based, Not a Graphical Desktop Framework | **Superseded by ADR-0092** | WP 8.0B (Workspace Contracts) | 2026-07-30 | Verified |
| ADR-0067 | Workspace Extensibility Is Kind-Keyed Registration, for Both Views and Explorer Nodes | Accepted | WP 8.0B (Workspace Contracts) | 2026-07-30 | Verified |
| ADR-0068 | Engineering Workspace Is `Tempest.App`'s Own Default Launch Target | Accepted | WP 8.1A (Workspace Shell) | 2026-07-30 | Verified |
| ADR-0069 | The Engineering Cockpit Is the Workspace's Own Default Landing Screen | Accepted | WP 8.0C (Engineering Workspace UX Specification) | 2026-08-04 | Verified |
| ADR-0070 | The Command Palette Is a First-Class, Global Entry Point | Accepted | WP 8.0C (Engineering Workspace UX Specification) | 2026-08-04 | Verified |
| ADR-0071 | Workspace Extensibility Registrations Are Made by the Composition Root, Not by Discovered Modules | Accepted | WP 8.1B (Navigation & Project Explorer) | 2026-08-04 | Verified |
| ADR-0072 | Every Canonical Engineering Object Is an `IEngineeringDocumentStore`-Backed `Kind`, Never a New Storage/Type Hierarchy | Accepted | WP 8.2A (Engineering Domain Architecture) | 2026-08-04 | Verified |
| ADR-0073 | Relationships Between Engineering Objects Are Open-String `DocumentReference`s, Platform-Wide | Accepted | WP 8.2A (Engineering Domain Architecture) | 2026-08-04 | Verified |
| ADR-0074 | Lifecycle Status Is a Common Canonical Vocabulary, Specialised Per Object Family | Accepted | WP 8.2A (Engineering Domain Architecture) | 2026-08-04 | Verified |
| ADR-0075 | Engineering Object Contracts Are Composed From Small Facet Interfaces, Never One Monolithic Interface | Accepted | WP 8.2B (Engineering Domain Contracts) | 2026-08-04 | Verified |
| ADR-0076 | Relationship Contracts Are Realised as One Generic `IEngineeringRelationship` Interface, Not a Closed Set of Per-Category Types | Accepted | WP 8.2B (Engineering Domain Contracts) | 2026-08-04 | Verified |
| ADR-0077 | Engineering Domain Shared Services Reuse the Existing `IEngineeringDocumentStore` in Production; a New In-Memory Repository Layer Is the "In-Memory Repositories" Deliverable | Accepted | WP 8.2C (Engineering Domain Implementation) | 2026-08-04 | Verified |
| ADR-0078 | The Five Already-Implemented Canonical Kinds Are Not Given a Competing Concrete Realisation in the Engineering Domain Implementation | Accepted | WP 8.2C (Engineering Domain Implementation) | 2026-08-04 | Verified |
| ADR-0079 | Object and Relationship Factories Are Generic Types, Instantiated Once per Kind | Accepted | WP 8.2C (Engineering Domain Implementation) | 2026-08-04 | Verified |
| ADR-0080 | Product Structure Mutation (Rename/Move/Delete) Is Three New, Additive Facet Interfaces — Never a Reopening of Any Frozen `WP8.2B` Contract | Accepted | WP 9.0A (Mechanical Product Structure) | 2026-08-05 | Verified |
| ADR-0081 | `Move` Records a New `groupedUnder` Relationship Link and Updates a Live `ParentId` Field — It Never Removes History, and Never Mutates the Frozen `ChildIds`/`ParentAssemblyId` | Accepted | WP 9.0A (Mechanical Product Structure) | 2026-08-05 | Verified |
| ADR-0082 | Property Inspector Facet Sourcing Is a Third Kind-Keyed Provider Category, Added to the Frozen `IWorkspaceManager` Contract | Accepted | WP 9.0A (Mechanical Product Structure) | 2026-08-05 | Verified |
| ADR-0083 | A Bill of Materials Line Is a Fourth Additive Domain Facet (`IHasBomLine`); Unit of Measure Is a Plain String, Never `Quantity<TDimension>` | Accepted | WP 9.0B (Product Configuration & BOM Management) | 2026-08-05 | Verified |
| ADR-0084 | Requirements Lifecycle, Ownership, Priority, and Enumeration Operations Are Additive `IRequirementsService` Methods — Never a Facet-Composition Retrofit | Accepted | WP 9.1A (Requirements Management Workspace) | 2026-08-05 | Verified |
| ADR-0085 | Multi-Selection Is Additive Members on the Frozen `ISelectionService`/`IWorkspaceContext` Contracts — Single-Selection Behaviour Is Completely Unchanged | Accepted | WP 9.1A (Requirements Management Workspace) | 2026-08-05 | Verified |
| ADR-0086 | `CalculationTemplateRegistry` Is a Workspace-Layer, JSON-Marshalled Type-Erasure Adapter Over `ICalculationEngine` — Never a Domain-Layer Registry | Accepted | WP 9.2A (Engineering Calculations Workspace) | 2026-08-05 | Verified |
| ADR-0087 | Calculation Management's Lock/Unlock/Review/Approve/Archive Verbs Are `CommandDescriptor` Aliases Over `IHasLifecycle.TransitionAsync` — Approval State Is `LifecycleState` Alone | Accepted | WP 9.2A (Engineering Calculations Workspace) | 2026-08-05 | Verified |
| ADR-0088 | The Document Classification Taxonomy (Specification/Report/Procedure/Standard/Datasheet/External Reference) Is Realised as `Classification`-Tagged `Document` Objects, Never New Concrete Domain Classes | Accepted | WP 9.4A (Engineering Documents Workspace) | 2026-08-06 | Verified |
| ADR-0089 | "Execute" and "Record Result" Are One Command (`RecordVerificationResultCommand`) Over `IVerificationService.RecordAsync` — No Adapter Is Needed | Accepted | WP 9.3A (Verification Management Workspace) | 2026-08-07 | Verified |
| ADR-0090 | "Verification Plan" and "Verification Activity" Are One Domain Kind (`VerificationActivity`), Distinguished Only by `LifecycleState`; Review/Approve/Archive Are `CommandDescriptor` Aliases Over `TransitionAsync` | Accepted | WP 9.3A (Verification Management Workspace) | 2026-08-07 | Verified |
| ADR-0091 | Routings, Operations, and Supplier Operations Are `Classification`-Tagged `ManufacturingOperation` Objects, Sequenced via the Existing `IHasBomLine.ItemNumber` Field | Accepted | WP 9.5A (Manufacturing Workspace) | 2026-08-07 | Verified |
| ADR-0092 | Engineering Workspace Presentation Moves to a Graphical Desktop Application, Superseding ADR-0066 | Accepted | WP 10.0A (User Experience Architecture) | 2026-08-07 | Verified |
| ADR-0093 | Object Relationship Views Are a Progressively-Expandable Node-Link Graph, Superseding ADR-0065 | Accepted | WP 10.0A (User Experience Architecture) | 2026-08-07 | Verified |
| ADR-0094 | Avalonia Is the Concrete Desktop UI Framework | Accepted | WP 10.0B (Desktop Application Framework) | 2026-08-07 | Verified |
| ADR-0096 | Object Rename and Delete Dispatch Are a Fourth and Fifth Kind-Keyed Provider Category, Added to the Frozen `IWorkspaceManager` Contract | Accepted | WP 10.2A (Workspace Modernisation) | 2026-08-07 | Verified |
| ADR-0097 | Object Content-Revise Dispatch Is a Sixth Kind-Keyed Provider Category, Added to the Frozen `IWorkspaceManager` Contract | Accepted | WP 10.3A (Engineering Object Editors) | 2026-08-09 | Verified |
| ADR-0098 | Undo/Redo Is a Desktop-Local `UndoableAction` Delegate Stack, Not a New `ICommand` Contract | Accepted | WP 10.6A (Command Execution & Productivity Experience) | 2026-08-10 | Verified |
| ADR-0099 | A Macro Is Realised as a Registered Command (`RunMacroCommand` over `IMacroManager`) | Accepted | WP 10.6A (Command Execution & Productivity Experience) | 2026-08-10 | Verified |
| ADR-0100 | External Controller Integration Is an `IInputBindingProvider` Abstraction — No Vendor SDK, One Real Keyboard Provider, One Test-Only Stub Controller | Accepted | WP 10.6A (Command Execution & Productivity Experience) | 2026-08-10 | Verified |
| ADR-0101 | `Tempest.App`/`WorkspaceShell` Is TempestOS's Internal Engineering Harness, Not a Shipped Product | Accepted | WP 11.3B (Presentation Strategy Implementation) | 2026-08-11 | Verified |
| ADR-0102 | Fault-Injection Modules Are Isolated By Project Reference *and* a Default-Excluded Discovery Marker | Accepted | WP 12.3A/WP 12.3B (Fault-Injection Validation Framework) | 2026-08-12 | Verified |

**Total: 100 ADRs — 98 Accepted, 2 Superseded (`ADR-0065`, `ADR-0066`,
both superseded by `WP 10.0A`, the first supersessions in this
project's history — Verified directly against `docs/adr/`: both
superseded files carry an added Status-section note naming their own
superseding ADR, per `Engineering Governance.md` §5, with their own
original Context/Decision/Consequences bodies left unedited).
`ADR-0094` resolved the first of the two ADR numbers `WP 10.0A`
reserved (concrete desktop UI framework selection — Avalonia 11.2.3,
`WP 10.0B`). `ADR-0096`/`ADR-0097` are both genuinely new decisions, not
the reservation being resolved — `ADR-0095` (floating/multi-monitor
panel placement contract extension) remains reserved, not yet written,
explicitly out of `WP 10.2A`'s own "Do NOT implement: Floating
windows... Multi-monitor" scope; each was numbered after it
deliberately, the identical "skip the still-reserved number, use the
next free one" convention this register's own Numbering Integrity
section already documents for `ADR-0068` and `ADR-0071`. `ADR-0098`
through `ADR-0100` (`WP 10.6A`) continue past `ADR-0097` for the
identical reason. **Disclosed, found-and-fixed by `WP 10.6A`:** this
register's own Entries table was missing its own `ADR-0097` row (the
file existed, `WP 10.3A`'s own narrative in this cell already named it,
but no table row had ever been added for it) — added here, and this
`Total`/`Related ADRs` line corrected from a stale `95`/`96` to the
true, current count, the same class of drift `WP 9.1A`/`WP 10.0A`/`WP
10.2B` each already found and fixed in this identical field.**

## Numbering Integrity

Sequential and complete, `ADR-0001` through `ADR-0094`, plus `ADR-0096`
through `ADR-0100`, with one genuine, disclosed, still-open gap —
`ADR-0095` — reserved,
named explicitly as not-yet-written, mirroring `WP 8.0A`'s own original
treatment of what became `ADR-0066`/`ADR-0067`. `ADR-0066`/`ADR-0067`,
reserved by `WP 8.0A`, were resolved by the
very next Work Package (`WP 8.0B`, its own Contract Review), the same
one-Work-Package-later cadence `ADR-0058`–`ADR-0061` established for
the Requirements Engine (reserved `WP 7.2B`/`WP 7.2C`, answered
`WP 7.3A`) — here compressed even further, since both were answered by
the Contract Review stage itself rather than waiting for
implementation. `ADR-0068` was not reserved by any prior Work Package —
a genuinely new question (`Tempest.App`'s own default launch target)
that only became answerable once both composition roots
(`TempestShell`, the Workspace) were real, compiled code, which did not
happen until `WP 8.1A` itself. `ADR-0069`/`ADR-0070` were likewise not
reserved by any prior Work Package — both are genuinely new product/UX
decisions (default landing screen, global command discoverability)
that only became answerable once the full target experience was
specified (`WP 8.0C`), not anticipated at the architecture or contract
stage. `ADR-0071` was likewise not reserved — it is a correction,
surfaced by `WP 8.1B`'s own first real registration against `ADR-0067`'s
own mechanism, of a worked example inside `ADR-0067` itself that does
not hold against the real Host/Workspace boundary `ADR-0062` already
established. `ADR-0067` remains Accepted and unmodified — its own core
Kind-keyed-registration decision is unaffected; only its illustrative
example was wrong, and `ADR-0071` records the correction as a new,
separate ADR rather than editing an already-Accepted one, per Engineering
Governance §5. `ADR-0072`–`ADR-0074` were likewise not reserved by any
prior Work Package — each formalises, as binding platform-wide
architecture, a pattern the Engineering Core's own four already-shipped
frameworks (`Tempest.Core.Requirements`/`Verification`/`Materials`/
`Calculations`) had independently converged on without coordination;
`WP 8.2A`'s own contribution is naming that convergence once, not
inventing a new decision from nothing. `ADR-0075`/`ADR-0076` were
likewise not reserved — both are genuinely new contract-shape decisions
that only became answerable once `WP 8.2A`'s own canonical objects and
relationships existed for a contract layer to be designed against;
`ADR-0076` specifically resolves a direct tension between `WP 8.2B`'s
own controlling instruction (seventeen named relationship categories)
and `ADR-0073`'s own prior, binding decision, rather than silently
picking one reading over the other. `ADR-0077`–`ADR-0079` were likewise
not reserved — each is a genuinely new implementation-stage decision
that only became answerable once `WP 8.2C` began compiling `WP 8.2B`'s
own contracts against real code: `ADR-0077` resolves a direct tension
between this Work Package's own "no persistence" constraint and
`ADR-0072`'s own prior, binding decision (the same shape of tension
`ADR-0076` already resolved once, one layer up, at the contract stage);
`ADR-0078` resolves a direct tension between "implement every canonical
object class" and "write no Requirements/Verification/Calculations
logic," both stated in the same controlling instruction; `ADR-0079`
extends `ADR-0076`'s own "few generic types, many instances" reasoning
from relationship contracts to object/relationship factories.
`docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md` reserved
`ADR-0053` through `ADR-0057` for the five Engineering Foundation
frameworks' own anticipated architectural decisions, one per framework
— all five (`ADR-0053` Engineering Data Model, `ADR-0054` Units &
Quantities, `ADR-0055` Materials, `ADR-0056` Calculation, `ADR-0057`
Verification) are now real, Accepted files, each implemented exactly as
that catalogue anticipated, each also resolving at least one genuine
question the catalogue did not itself anticipate (`ADR-0054`:
affine/offset unit conversion; `ADR-0055`: `IMaterialCatalog`'s own
direct `IPersistenceStore` dependency; `ADR-0056`: `Calculate`'s own
signature change to accept a `CalculationContext`; `ADR-0057`:
verification history queried via the Data Model's own existing
reference mechanism, requiring no new index or Persistence dependency
at all). This closes `WP7.0C Required ADR Catalogue.md`'s own entire
reserved range — every Engineering Foundation ADR it anticipated is now
Accepted, exactly as `ADR-0040`–`ADR-0052` were once only a catalogue
entry before their own owning Work Packages implemented them.

`docs/releases/v0.7.0/WP7.2C Required ADR Catalogue.md` reserved
`ADR-0058` through `ADR-0061` for the Requirements Engine's own
anticipated implementation decisions — all four are now real, Accepted
files, each implemented exactly as that catalogue anticipated
(`ADR-0058`: Platform Service classification and Engineering Data Model
reuse; `ADR-0059`: independent representation decisions for status,
identifier, and category; `ADR-0060`: no compare-and-swap concurrency
mechanism, accepted as `TD-25`; `ADR-0061`: no internal permission
gating, mirroring Materials'/Calculations' own precedent). This closes
`WP7.2C Required ADR Catalogue.md`'s own entire reserved range.
`docs/releases/v0.6.0/Required ADRs.md` reserved `ADR-0040` through
`ADR-0051` in advance, as a catalogue of anticipated decisions, one
range per `v0.6.0` Work Package, before any of those Work Packages began
implementation. `WP 6.1`, `WP 6.4`, `WP 6.5`, `WP 6.2`, `WP 6.0`, `WP
6.3`, `WP 6.7`, and now `WP 6.6` are all eight of those Work Packages,
each having now implemented; their own reserved numbers
(`ADR-0040`–`ADR-0051`) are now real, Accepted files. `ADR-0052` is new,
genuinely implementation-driven — not anticipated by `Required ADRs.md`
at all — documenting a decision `WP 6.3`'s own brief authorised ("if
deviation is required... produce the appropriate ADR"): identity
resolution and audit attribution for the REST API, resolved without
touching `CurrentPrincipalAccessor`'s own already-shipped design (see
that ADR's own Context for the empirical verification behind this).
Verified by direct enumeration of `docs/adr/` cross-checked against
that table. Per Engineering Governance §5, a superseded ADR would be
marked as such in its own Status section with a new ADR created
referencing it, rather than renumbered or deleted; no such case exists
yet in this repository.

## Cross-Reference Check

- Every ADR above is cited by at least one entry in `Decision Register.md`
  and at least one Work Package retrospective (`Traceability Matrix.md`
  gives the full chain for each major feature). Confirmed by direct
  grep of `docs/academy/03 Work Packages/` for each ADR number — no
  orphaned ADR (one cited nowhere outside its own file) was found.
- ADR-0021 (Background Service failure classification) was decided during
  original v0.4.0 planning, *before* WP 4.5 existed as a named Work
  Package — this register records its originating WP as "v0.4.0 planning
  (WP 4.0 / WP 4.5)" rather than forcing it into a single WP, matching
  how `CHANGELOG.md` and `WorkPackages.md` themselves describe it.

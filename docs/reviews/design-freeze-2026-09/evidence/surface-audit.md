# TempestOS Surface Audit — Design-Freeze Review 2026-09

Read-only, code-derived audit of the shipped Desktop shell against what it claims to be. Every finding cites `file:line`. No recommendations — objective map only.

Context: Product Owner's first Windows review (2026-09-08): "the engineering workspace doesn't open properly, it's a really hard to navigate place that doesn't actually contain half of the things it claims to have." Two concrete defects cited: Create Mechanical Object made an object nobody could find, and no way to add a material — both marked fixed as WP 17.9.2 in-repo.

Editor's note (chief engineer, after this audit was written): the placement defect this audit calls "TD-171-class" was renumbered `TD-172` during the review, because `TD-171` was already assigned in `BACKLOG.md` to the verification-model collapse. The findings in §3 and §9 (items 5–8) are now recorded as `TD-172` and `TD-173` in `BACKLOG.md`. Read "TD-171" below as `TD-172`.

---

## 1. Shell map

The navigation model is `Module (ShellArea) → Project (ProjectArea) → Workspace → Engineering Object` (`src/Tempest.Workspace/Shell/ShellArea.cs:7`). The rail is built from a single declared table, `ShellAreas` (`src/Tempest.Workspace/Shell/NavigationAvailability.cs:63-108`), which every rail button and every "not yet implemented" surface reads from — so the claim and the code cannot disagree by construction. `MainWindow.RenderCurrentModuleAsync` (`src/Tempest.Desktop/MainWindow.cs:995-1053`) is the single place that maps the current `ShellLocation` to a rendered control.

| ShellArea | Rail entry (`ShellAreas.cs`) | Availability | Control rendered | file:line | Real or placeholder |
|---|---|---|---|---|---|
| Home | "Home" | Implemented | `_engineeringSurface` (same DockPanel as Engineering: ribbon + docking grid; `CockpitView` is set as the Document Area's permanent Home tab) | `MainWindow.cs:1011-1022`, construction at `MainWindow.cs:319-341`, `485-489` | Real — but note Home has no control of its own; it is literally the Engineering surface with Cockpit as its default open tab |
| Projects | "Projects" | Implemented | `ProjectBrowserView` | `MainWindow.cs:492`, rendered `MainWindow.cs:1001-1004` | Real |
| ProjectWorkspace | (not on rail — reached by opening a project, `NavigationAvailability.cs:57-61`) | Implemented | `ProjectWorkspaceView` | `MainWindow.cs:493-495`, rendered `MainWindow.cs:1006-1009` | Real shell, but internally a tab strip built from `ProjectAreas.All` (`ProjectWorkspaceView.cs:231-232`) where 2 of 9 tabs (Reports, Settings) fall through to `DeclaredCapabilityView` (`ProjectWorkspaceView.cs:371-374`) |
| Engineering | "Engineering" | Implemented | `_engineeringSurface` (ribbon + `WorkspaceDockingComposer.View`), scope-aware (project vs. standalone via `IEngineeringScope`) | `MainWindow.cs:1012-1022` | Real |
| EngineeringCalculation | "Engineering Calculations" | Implemented | `EngineeringCalculationView` | `MainWindow.cs:515`, `587`, rendered `MainWindow.cs:1025-1037` | Real |
| Tasks | "Tasks" | **Declared** | `DeclaredCapabilityView` | `MainWindow.cs:1043-1044`; declaration `NavigationAvailability.cs:89-91` | Placeholder — note text says cross-project view is missing but a project's own Tasks tab (`ProjectArea.Tasks`) is real |
| Commercial | "Commercial" | **Declared** | `DeclaredCapabilityView` | same fallthrough, `MainWindow.cs:1039-1045`; declaration `NavigationAvailability.cs:93-95` | Placeholder — declaration states "No commercial domain exists yet — this module has no implementation in any layer" |
| Resources | "Resources" | **Declared** | `DeclaredCapabilityView` | declaration `NavigationAvailability.cs:97-99` | Placeholder — "No resourcing domain exists yet" |
| Knowledge | "Knowledge" | **Declared** | `DeclaredCapabilityView` | declaration `NavigationAvailability.cs:101-103` | Placeholder — declaration itself admits materials/units/calc templates exist as services but "no knowledge surface aggregates them" |
| Administration | "Administration" | **Declared** | `DeclaredCapabilityView` | declaration `NavigationAvailability.cs:105-107` | Placeholder — "Identity, roles and permissions are real, enforced platform services — the administrative surface over them is not built" |

5 of 10 rail modules (Tasks, Commercial, Resources, Knowledge, Administration) are `NavigationAvailability.Declared` and render `DeclaredCapabilityView` (`src/Tempest.Desktop/Views/DeclaredCapabilityView.cs`), a real, project-aware "not yet implemented" screen with a badge (`DeclaredCapabilityView.cs:35`, "Not yet implemented"), a note, and a tracking id (`TrackedBy`, e.g. `TD-81`, `TD-79`). This is not a dead button and not a lying "coming soon" — the rail visibly marks these with a violet dot and "planned, not yet built" legend (`GlobalNavigationRail.cs:89-96`, `184-194`) — but it is exactly half the rail (5 of 10) that is not a real surface, which matches the Product Owner's "doesn't actually contain half of the things it claims to have."

Inside `ProjectWorkspaceView`, the tab strip is likewise built from a declared table, `ProjectAreas.All` (`src/Tempest.Workspace/Shell/ProjectAreas.cs:31-61`): Overview, Engineering, Documents, Requirements, Tasks, Risks, Timeline are `Implemented`; Reports and Settings are `Declared` (`ProjectAreas.cs:54-60`) and render `DeclaredCapabilityView` (`ProjectWorkspaceView.cs:371-374`, refreshed per-project at `ProjectWorkspaceView.cs:378-387`).

`CommandPaletteOverlay.cs` (262 lines) and `RibbonView.cs` (694 lines) are global chrome shared across every `ShellArea`, not per-area surfaces — see Section 2 for what they expose.

---

## 2. Ribbon and Palette inventory

74 `CommandDescriptor` registrations exist across the six discipline registrations in `src/Tempest.Workspace/Workspace/*/*WorkspaceRegistration.cs` (verified: `grep -c "RegisterDescriptor(new CommandDescriptor("` = 74; per-file breakdown: Mechanical 10, Requirements 18, Calculations 14, Documents 11, Verification 11, Manufacturing 10). Every descriptor carries either a real `CommandBinding` (usable from Ribbon and Palette by Id) or `CommandBinding.Unavailable(reason)` — registered, listed and described, but `Evaluate` refuses invocation and reports the reason verbatim (`src/Tempest.Core/Commands/CommandBinding.cs:86-107`). Context key: **N**=None, **S**=SelectedObject, **S+M**=SelectedObject+MultipleAllowed. "Tested" = number of files under `tests/Tempest.Desktop.Tests` containing the id string literal.

18 of 74 (24%) carry `Binding = CommandBinding.Unavailable(...)`: every discipline's Move and Copy (12 commands total — no destination-parent object-picker exists anywhere in the Desktop UI), `calculations.execute`/`calculations.recalculate` (need a per-template structured JSON input form that does not exist), `documents.attach` (needs a file picker), `mechanical.compare-baselines` (needs a second-object picker), `requirements.link`, `requirements.move-group`, `requirements.add-to-collection` (each needs an object/collection picker). **No discipline's objects can be moved or copied to a new parent from the Ribbon or Palette today, and a Requirement can never be linked to anything (goal 8b) through a bound command.**

### Mechanical (10) — `src/Tempest.Workspace/Workspace/Mechanical/MechanicalWorkspaceRegistration.cs`

| id | displayName | Ctx | Usable | Tested | What it does |
|---|---|---|---|---|---|
| mechanical.create | Create Mechanical Object | N | Bound | 5 | Creates Project/Assembly/SubAssembly/Part/Component |
| mechanical.rename | Rename Mechanical Object | S | Bound | 4 | Renames selected object |
| mechanical.edit | Edit Mechanical Object | S | Bound | 1 | Records new content revision |
| mechanical.delete | Delete Mechanical Object | S | Bound | 4 | Soft-deletes (rejected if live children) |
| mechanical.move | Move Mechanical Object | — | **Unavailable** | 2 | Reparent — needs object picker, none exists |
| mechanical.copy | Copy Mechanical Object | — | **Unavailable** | 0 | Copy under chosen parent — needs object picker |
| mechanical.duplicate | Duplicate Mechanical Object | S | Bound | 2 | Copy under current parent |
| mechanical.set-bom-line | Set BOM Line | S | Bound | 0 | Sets Qty/UoM/FindNo/ItemNo/RefDesignator |
| mechanical.compare-baselines | Compare Baselines | — | **Unavailable** | 0 | Needs a second Baseline/Release picker |
| mechanical.validate-configuration | Validate Configuration | S | Bound | 1 | Checks Baseline/Release member consistency |

### Requirements (18) — `src/Tempest.Workspace/Workspace/Requirements/RequirementsWorkspaceRegistration.cs`

| id | displayName | Ctx | Usable | Tested | What it does |
|---|---|---|---|---|---|
| requirements.create | Create Requirement | N | Bound | 1 | New Requirement with id + statement |
| requirements.revise | Revise Requirement | S | Bound | 2 | New statement revision |
| requirements.set-status | Set Requirement Status | S | Bound | 1 | Sets lifecycle status |
| requirements.set-owner | Set Requirement Owner | S | Bound | 0 | Sets owner |
| requirements.set-priority | Set Requirement Priority | S | Bound | 0 | Sets priority |
| requirements.delete | Delete Requirement | S | Bound | 1 | Soft-delete |
| requirements.move | Move Requirement | — | **Unavailable** | 1 | Regroup — needs group picker |
| requirements.duplicate | Duplicate Requirement | S | Bound | 0 | Copy under new identifier |
| requirements.link | Link Requirement | — | **Unavailable** | 0 | Typed relationship to another object — needs object picker; **the requirement-to-part linking goal (8b) has no bound command** |
| requirements.create-group | Create Requirement Group | N | Bound | 0 | New group |
| requirements.move-group | Move Requirement Group | — | **Unavailable** | 0 | Reparent group — needs picker |
| requirements.delete-group | Delete Requirement Group | S | Bound | 2 | Soft-delete (rejected if non-empty) |
| requirements.create-collection | Create Requirement Collection | N | Bound | 0 | New empty collection |
| requirements.delete-collection | Delete Requirement Collection | S | Bound | 1 | Soft-delete collection |
| requirements.add-to-collection | Add Requirement to Collection | — | **Unavailable** | 0 | Needs collection picker |
| requirements.bulk-set-status | Bulk Set Requirement Status | S+M | Bound | 0 | Sets status on a selection |
| requirements.bulk-set-owner | Bulk Set Requirement Owner | S+M | Bound | 0 | Sets owner on a selection |
| requirements.bulk-set-priority | Bulk Set Requirement Priority | S+M | Bound | 0 | Sets priority on a selection |

### Calculations — "Calculation" object kind (14) — `src/Tempest.Workspace/Workspace/Calculations/CalculationsWorkspaceRegistration.cs`

| id | displayName | Ctx | Usable | Tested | What it does |
|---|---|---|---|---|---|
| calculations.create | Create Calculation | N | Bound | 0 | New Calculation or Calculation Set |
| calculations.rename | Rename Calculation | S | Bound | 1 | Renames |
| calculations.edit | Edit Calculation | S | Bound | 1 | New method-statement revision |
| calculations.delete | Delete Calculation | S | Bound | 1 | Soft-delete |
| calculations.move | Move Calculation | — | **Unavailable** | 0 | Reparent — needs picker |
| calculations.copy | Copy Calculation | — | **Unavailable** | 0 | Copy under chosen parent — needs picker |
| calculations.duplicate | Duplicate Calculation | S | Bound | 1 | Copy under current parent |
| calculations.execute | Execute Calculation | — | **Unavailable** | 0 | Run a registered Template — needs a per-template structured JSON input form, none exists |
| calculations.recalculate | Recalculate | — | **Unavailable** | 0 | Re-run with fresh input — same missing form |
| calculations.lock | Lock Calculation | S | Bound | 0 | Status → Approved |
| calculations.unlock | Unlock Calculation | S | Bound | 0 | Status → Draft |
| calculations.request-review | Request Review | S | Bound | 3 | Status → InReview |
| calculations.approve | Approve Calculation | S | Bound | 2 | Status → Approved |
| calculations.archive | Archive Calculation | S | Bound | 0 | Status → Archived |

This is the "Calculation"/"CalculationSet"/"CalculationTemplate" object-kind model with 5 representative templates registered inline (Bolt, Beam, Bearing, Pressure Vessel, Material Selection — `CalculationsWorkspaceRegistration.cs:279-292`). See Section 6: this is a **second, separate calculation model** from the `EngineeringCalculationView`/`BracketCalculationWorkbench` governed-calculation surface, and neither `calculations.execute` nor `calculations.recalculate` is drivable through the Palette (both `Unavailable`).

### Documents (11) — `src/Tempest.Workspace/Workspace/Documents/DocumentsWorkspaceRegistration.cs`

| id | displayName | Ctx | Usable | Tested | What it does |
|---|---|---|---|---|---|
| documents.create | Create Document | N | Bound | 0 | New Document/Drawing/CAD Model |
| documents.rename | Rename Document | S | Bound | 1 | Renames |
| documents.edit | Edit Document | S | Bound | 1 | New content revision |
| documents.delete | Delete Document | S | Bound | 1 | Soft-delete |
| documents.move | Move Document | — | **Unavailable** | 0 | Reparent — needs picker |
| documents.copy | Copy Document | — | **Unavailable** | 0 | Copy — needs picker |
| documents.duplicate | Duplicate Document | S | Bound | 0 | Copy under current parent |
| documents.attach | Attach File | — | **Unavailable** | 0 | Attach a file reference — needs a file picker; the two constructors take real bytes or a measured size, neither expressible as collected text |
| documents.request-review | Request Review | S | Bound | 1 | Status → InReview |
| documents.approve | Approve Document | S | Bound | 1 | Status → Approved |
| documents.release | Release Document | S | Bound | 1 | Status → Released |

### Verification (11) — `src/Tempest.Workspace/Workspace/Verification/VerificationWorkspaceRegistration.cs`

| id | displayName | Ctx | Usable | Tested | What it does |
|---|---|---|---|---|---|
| verification.create | Create Verification Activity | S (needs subject) | Bound | 1 | New Verification Activity against selected object |
| verification.rename | Rename Verification Activity | S | Bound | 1 | Renames |
| verification.edit | Edit Verification Activity | S | Bound | 1 | New content revision |
| verification.delete | Delete Verification Activity | S | Bound | 1 | Soft-delete |
| verification.move | Move Verification Activity | — | **Unavailable** | 0 | Reparent — needs picker |
| verification.copy | Copy Verification Activity | — | **Unavailable** | 0 | Copy — needs picker |
| verification.duplicate | Duplicate Verification Activity | S | Bound | 0 | Copy under current parent |
| verification.record-result | Record Verification Result | S | Bound | 0 | Records Pass/Fail/Conditional result — the goal (8e) command |
| verification.request-review | Request Review | S | Bound | 0 | Status → InReview |
| verification.approve | Approve Verification Activity | S | Bound | 0 | Status → Approved |
| verification.archive | Archive Verification Activity | S | Bound | 0 | Status → Archived |

### Manufacturing (10) — `src/Tempest.Workspace/Workspace/Manufacturing/ManufacturingWorkspaceRegistration.cs`

| id | displayName | Ctx | Usable | Tested | What it does |
|---|---|---|---|---|---|
| manufacturing.create | Create Manufacturing Object | N | Bound | 0 | New ManufacturingOperation/WorkInstruction/Inspection |
| manufacturing.rename | Rename Manufacturing Object | S | Bound | 1 | Renames |
| manufacturing.edit | Edit Manufacturing Object | S | Bound | 1 | New content revision |
| manufacturing.delete | Delete Manufacturing Object | S | Bound | 1 | Soft-delete |
| manufacturing.move | Move Manufacturing Object | — | **Unavailable** | 0 | Reparent/re-sequence in Routing — needs picker |
| manufacturing.copy | Copy Manufacturing Object | — | **Unavailable** | 0 | Copy — needs picker |
| manufacturing.duplicate | Duplicate Manufacturing Object | S | Bound | 0 | Copy under current parent |
| manufacturing.release | Release | S | Bound | 0 | Status → Released |
| manufacturing.archive | Archive | S | Bound | 0 | Status → Archived |
| manufacturing.record-inspection-result | Record Inspection Result | S | Bound | 1 | Records result against an Inspection — cross-discipline reuse of `RecordVerificationResultCommand` (`ManufacturingWorkspaceRegistration.cs:236-255`) |

Totals: Mechanical 10 + Requirements 18 + Calculations 14 + Documents 11 + Verification 11 + Manufacturing 10 = **74**, matching `grep -c "RegisterDescriptor(new CommandDescriptor("`. `Unavailable`-binding count per discipline: Mechanical 3 (move, copy, compare-baselines), Requirements 4 (move, link, move-group, add-to-collection), Calculations 4 (move, copy, execute, recalculate), Documents 3 (move, copy, attach), Verification 2 (move, copy), Manufacturing 2 (move, copy) = **18**.

---

## 3. Discipline tabs versus explorer areas

| Discipline | NavigationItemId | Node provider — roots / children | View factory (double-click) | Facet provider (Properties) |
|---|---|---|---|---|
| Mechanical | `tempest.mechanical.product-structure` (`MechanicalWorkspaceExplorerModule.cs:37`) | `MechanicalProductStructureNodeProvider` — rooted at every live `"Project"`; every other node from the live `IHasParent.ParentId` pointer. The tree **is** the BOM hierarchy (`MechanicalProductStructureNodeProvider.cs:5-19`) | `MechanicalWorkspaceViewFactory` → `MechanicalWorkspaceView` per Kind | `MechanicalPropertyFacetProvider` — Id/Name/Description/Revision/Status/Owner/Discipline/Classification/Tags, Baseline/Released-state, BOM line facets (`MechanicalPropertyFacetProvider.cs:3-9`) |
| Requirements | `tempest.requirements.management` | `RequirementsNodeProvider` — roots = every live `RequirementCollection` + every live **root** `RequirementGroup` (`ParentGroupId is null`) only (`RequirementsNodeProvider.cs:47-61`). A Requirement is a leaf under a Group or a Collection | `RequirementsWorkspaceViewFactory` → `RequirementsWorkspaceView` | `RequirementsPropertyFacetProvider` — Identifier/Statement/Category/Status/Owner/Priority/Revision/Group/Deleted + Verification Coverage |
| Calculations | `tempest.calculations.management` | `CalculationsNodeProvider` — roots = synthetic "Templates" node + every live `"CalculationSet"` + every live **un-parented** `"Calculation"` (`CalculationsNodeProvider.cs:6-17`) | `CalculationsWorkspaceViewFactory` → `CalculationsWorkspaceView` | `CalculationsPropertyFacetProvider` — generic Mechanical-style facets plus calculation-specific ones |
| Documents | `tempest.documents.management` | `DocumentsNodeProvider` — roots = one synthetic category node per `DocumentCategory` (Drawings, CAD Models, Specifications, …, Uncategorized), each containing every live **un-parented** Document in it; a Document that is itself a real parent nests its children (`DocumentsNodeProvider.cs:9-19`) | `DocumentsWorkspaceViewFactory` → `DocumentsWorkspaceView` | `DocumentsPropertyFacetProvider` |
| Verification | `tempest.verification.management` | `VerificationActivityNodeProvider` — roots = one synthetic category node per `VerificationMethodCategory` (Inspection/Analysis/Test/Demonstration/Other), each containing every live **un-parented** Activity (`VerificationActivityNodeProvider.cs:5-14`) | `VerificationActivityWorkspaceViewFactory` → `VerificationActivityWorkspaceView` | `VerificationActivityPropertyFacetProvider` |
| Manufacturing | `tempest.manufacturing.management` | `ManufacturingNodeProvider` — roots = one synthetic category node per `ManufacturingCategory` (Routings, Operations, Supplier Operations, Work Instructions, Inspections), each containing every live **un-parented** object across all 3 Kinds (`ManufacturingNodeProvider.cs:5-15`) | `ManufacturingWorkspaceViewFactory` → `ManufacturingWorkspaceView` (Operation); `WorkInstruction`/`Inspection` reuse Documents'/Verification's own view factories directly (`ManufacturingWorkspaceRegistration.cs:89-105`) | `ManufacturingOperationPropertyFacetProvider` for Operation; `WorkInstruction`/`Inspection` reuse Documents'/Verification's own facet providers |

**Mechanical is architecturally the odd one out**: it is the only discipline whose Explorer tree is rooted at Project membership (`IHasParent.ParentId` chain up to a `"Project"`). The other five root on synthetic category/group/collection nodes that show every live object regardless of project. This shape difference is exactly why the Product Owner's original defect ("Create Mechanical Object made an object nobody could find") was possible only in Mechanical, and why the WP 17.9.2 fix (`MechanicalCreateParentPolicy.cs`) is Mechanical-only — and why the equivalent defect still exists, in a different form, in every other discipline (below).

### Create-command default placement, checked discipline by discipline

- **Mechanical** — fixed (`WP 17.9.2`): the `mechanical.create` binding resolves a parent via `MechanicalCreateParentPolicy.Resolve` — the selected container if selectable, else the open project, else none (`MechanicalWorkspaceRegistration.cs:106-116`, `MechanicalCreateParentPolicy.cs:42-55`). This is the one discipline where a newly created object reliably lands where the Explorer tree can show it.
- **Documents, Calculations, Manufacturing** — **not fixed**: their `*.create` bindings pass only `kind` and `displayName` to the command, under `CommandContextRequirement.None`, discarding the selection/context entirely (`DocumentsWorkspaceRegistration.cs:84-92`, `CalculationsWorkspaceRegistration.cs:99-111`, `ManufacturingWorkspaceRegistration.cs:137-145`). `ParentId` is therefore always `null`. Because these three disciplines root their Explorer tree on synthetic category nodes that list every un-parented object (see table above), the created object **is still visible in its own discipline's tree** — but it is never a member of any project: `ProjectDocumentRegister`/generic `ProjectMembership.ListProjectMembersAsync` resolve project membership by walking the live `IHasParent.ParentId` chain (`ProjectDocumentRegister.cs:90-118`, `EngineeringScope.cs:55-58`), so a `ParentId: null` object never appears in `ProjectWorkspaceView`'s own Documents tab, and never counts toward `EngineeringScope.ListObjectsAsync` for the open project. **This is journey (a)/(d)'s concrete blocker**: a Document or Calculation created from the Ribbon/Palette cannot be "put under" a project or a part at all — there is no parent parameter collected, so it is always an orphan by construction, just one that happens to still be visible in the wrong (module-wide) tree instead of no tree.
- **Manufacturing — worse than an orphan, an outright failure for the default Kind**: `manufacturing.create`'s default Kind is `ManufacturingOperationKind` (`ManufacturingWorkspaceRegistration.cs:142`), but `CreateManufacturingObjectCommandHandler.HandleAsync` throws `ArgumentException("PartId is required to create a ManufacturingOperation.")` whenever `PartId` is null (`CreateManufacturingObjectCommand.cs:87-90`) — and the binding never collects or supplies a `PartId` (`ManufacturingWorkspaceRegistration.cs:137-145`). Because `RibbonView` now invokes every Create action through this exact same `ICommandRegistry.InvokeAsync`/binding path (`RibbonView.cs:516`; the former dedicated `RibbonObjectActionHandlers` was removed, per `MechanicalWorkspaceRegistration.cs:382-388`), invoking "Create Manufacturing Object" with its own default selection (Operation) **always fails** with that message — the user is shown an error toast rather than an orphaned object, but the command is not usable for its own default case from either the Ribbon or the Palette. `WorkInstruction`/`Inspection` fail the same way (`ManufacturingOperationId`/`SubjectId` required, never supplied).
- **Requirements — the TD-171 defect reproduced, undetected, outside Mechanical**: `requirements.create`'s bound command, `CreateRequirementCommand`, has no `GroupId` parameter at all (`CreateRequirementCommand.cs:14`), and `RequirementsService.CreateAsync` never assigns one (`RequirementsService.cs:123-147`, the `RequirementDto` it writes carries no group). `RequirementsNodeProvider.GetRootNodesAsync` returns **only** Collections and root Groups (`RequirementsNodeProvider.cs:47-61`) — there is no "Uncategorized"/catch-all node the way Documents/Verification/Manufacturing have one. **A Requirement created through the shipped Ribbon or Palette therefore has no node anywhere in the Requirements Explorer tree** — it exists in the domain (`GetEvidenceAsync`, `FindAsync` etc. can all reach it by Id) but there is no click path to it. This is the identical defect class the Product Owner's Windows review flagged for Mechanical, present in Requirements, and with no analogous fix on record.
- **Verification** — `verification.create` requires `CommandContextRequirement.SelectedObject` and reads the selection as the Activity's own `SubjectId` (`VerificationWorkspaceRegistration.cs:91-105`) — a real, working placement rule (an Activity is always created "against" whatever is selected), the one discipline besides Mechanical whose Create path was designed around real context.

**TD-171-class summary**: of six disciplines, one (Mechanical) has a real, working default-placement rule; one (Verification) has a real rule by different means (subject-from-selection); one (Manufacturing) fails outright for its own default Create case; and three (Documents, Calculations, Requirements) create objects with no parent/group, of which Requirements' object becomes **fully unreachable** in its own module's Explorer tree — a strictly worse instance of the exact bug WP 17.9.2 was written to fix.

---

## 4. Object Editor

`src/Tempest.Desktop/Editors/ObjectEditorView.cs` (1104 lines) is one generic, reused editor engine applied to every real object across all six disciplines (`ObjectEditorView.cs:23-33`) — "reads directly, mutates only through Commands" (`ObjectEditorView.cs:36-48`). It has 5 always-present generic sections plus 6 Kind-gated sections:

| Section | Gate | Kinds that see it | file:line |
|---|---|---|---|
| Identity (Name) | none — always rendered; input `IsEnabled` follows `IWorkspaceManager.CanRename(_objectKind)` | All Kinds render the field; only Kinds with a registered Rename factory can type into it | `ObjectEditorView.cs:344`, `461`, `1020` |
| Content | none — always rendered; input `IsEnabled` follows `IWorkspaceManager.CanRevise(_objectKind)` | All Kinds render the field; only Kinds with a registered Revise factory can type into it | `ObjectEditorView.cs:345`, `465`, `1021` |
| Lifecycle | `is IHasLifecycle` — else honest text "This object carries no lifecycle." | Most Kinds; a non-lifecycle object states so rather than showing empty controls | `ObjectEditorView.cs:484-492` |
| Relationships | `is IHasRelationships` for outgoing; incoming always read from `RelationshipRepository` | All Kinds (incoming always shown; outgoing only if supported) | `ObjectEditorView.cs:531-543` |
| Validation | `is IValidatable` — else "This object supports no validation." Real `ValidateAsync()` call — genuinely closes a gap `PropertyInspectorView`'s own Validation section never closed (see Section 5) | Kinds implementing `IValidatable` | `ObjectEditorView.cs:592-615` |
| Bill of Materials | `is IHasBomLine` **and** `_objectKind` in the `BomKinds` allowlist (Assembly, SubAssembly, Part, Component, Configuration) | 5 Mechanical Kinds only | `ObjectEditorView.cs:657-670` |
| Owner / Priority | `_objectKind == RequirementsService.RequirementDocumentKind` | Requirement only | `ObjectEditorView.cs:723-736` |
| Execute / Calculation pointer | `_objectKind is "Calculation" or "CalculationSet"` | Calculation, CalculationSet | `ObjectEditorView.cs:794-807` |
| Record Result | `is IVerificationActivity` | VerificationActivity, Inspection (Manufacturing's reuse) | `ObjectEditorView.cs:850-861` |
| Attachments | `is IHasAttachments` | Document, Drawing, CadModel, WorkInstruction, and any other attachment-carrying Kind | `ObjectEditorView.cs:893-909` |

**A disclosed, named historical instance of exactly the "generic-for-all-Kinds, meaningless on some Kind" failure the audit asked about**: the BOM section's doc comment states outright that "the first Windows review of `v0.17.0` saw Quantity, Find Number and Reference Designator **on a Project and on a Calculation**" because every canonical object implements `IHasBomLine` (`ADR-0075`'s facet plumbing) and the section was originally gated on that interface alone; `WP 17.9.1` fixed it by adding the `BomKinds` allowlist so the section is gated on both the interface and Kind (`ObjectEditorView.cs:649-660`).

**A live, unfixed instance of the same pattern**: Identity and Content are never hidden, only disabled, so a Kind with no registered Rename/Revise factory (`RequirementGroup`/`RequirementCollection` — `RequirementsWorkspaceRegistration.cs:49-70` registers Delete for both but Rename for neither and Revise for neither) still shows a Name field and a Content box in the editor; both simply render disabled and empty, rather than being omitted for a Kind they mean nothing for.

---

## 5. Property Inspector

`src/Tempest.Desktop/Views/PropertyInspectorView.cs` (378 lines) renders whatever `IPropertyInspector.CurrentFacets` returns, grouped into 5 real `PropertyFacetKind` groups (Identity/Revision/Provenance/DisciplineSpecific/Relationship) plus one derived Lifecycle section (extracted by string-matching any facet named `"Status"`/`"Lifecycle"`, `PropertyInspectorView.cs:187-191`) plus one Validation section. Every facet is read-only text except the Identity group's `Name` row, which is a real editable field when `IWorkspaceManager.CanRename` allows it (`PropertyInspectorView.cs:303-325`).

**Every Kind shows two facets that are raw internal plumbing, not engineering information**: `Id` (`target.Id.ToString()`, a raw GUID) and `Parent` (`hasParent.ParentId?.ToString() ?? "(top level)"`, also a raw GUID string when set) appear, unresolved, in every facet provider — confirmed identical in Mechanical (`MechanicalPropertyFacetProvider.cs:46,89`), Documents (`DocumentsPropertyFacetProvider.cs:54,115`), and Calculations (`CalculationsPropertyFacetProvider.cs:67,100,150`). Unlike the `Principal`-kind facets (e.g. "Last Revised By"), which the Property Inspector explicitly resolves through `IPrincipalDirectory.Describe` before display (`PropertyInspectorView.cs:307-309`, called out with the comment "a principal facet holds the stored identity id (a Windows SID); the person reads a name"), the `Parent` facet receives no equivalent resolution to the parent object's own display name — an engineer sees a bare GUID where they would expect a name like "Bracket Assembly Rev B".

**Validation section status — a documentation/implementation mismatch worth flagging on its own**: `ObjectEditorView.cs:74-84` states `PropertyInspectorView`'s Validation section "remains the disclosed placeholder it always was (unmodified)... since it only ever sees `PropertyFacet`s, never the real object." This is stale: `PropertyInspectorView.AddValidationSection` (`WP 10.8A`, `PropertyInspectorView.cs:214-274`) now resolves the real object via `EngineeringDomainContext.Repository.FindAsync` and calls the real `IValidatable.ValidateAsync()` directly — genuinely real validation for every Kind **except Requirement**, which is disclosed as a known exception (`TD-41`): "A Requirement never resolves via `EngineeringDomainContext.Repository`" (`PropertyInspectorView.cs:224-230`), so a Requirement's Validation section always reads "Real validation is not available for this object here." (`PropertyInspectorView.cs:270`).

### Facet counts per discipline (facet names, by provider)

| Provider | Kinds covered | Facet names | Count |
|---|---|---|---|
| `MechanicalPropertyFacetProvider` | Project/Assembly/SubAssembly/Part/Component/Configuration/Baseline/Release | Id, Kind, Name, Engineering Identifier, Owner, Discipline, Classification, Tags, Description/Notes, Status, Released, Revision, Last Revised By, Parent, Deleted*, + BOM (Quantity, Unit of Measure, Find Number, Item Number, Reference Designator)* + Configuration Members*, Baseline* | ~14 base + up to 7 conditional |
| `RequirementsPropertyFacetProvider` | Requirement / RequirementCollection / RequirementGroup (3 separate facet sets in one provider) | Requirement: Id, Kind, Identifier, Statement, Category, Status, Owner, Priority, Revision, Created By, Created At, Group, Deleted* (13). Collection: Id, Kind, Name, Members, Deleted* (5). Group: Id, Kind, Name, Parent Group, Deleted* (5) | 13 / 5 / 5 |
| `CalculationsPropertyFacetProvider` | Calculation/CalculationSet (generic Mechanical-style set + calc-specific) / CalculationTemplate | Generic: Id, Kind, Name, Engineering Identifier, Owner, Discipline, Category, Tags, Description/Notes, Status, Revision, Last Revised By, Parent, Deleted* (14) + Members/Result History/Latest Result/Latest Result Outcome/Latest Executed At/Latest Executed By/Based On Calculation(s)/Used By (8). Template: Id, Kind, Name, Calculation Id, Category, Description (6) | ~22 (real Calculation) / 6 (Template) |
| `DocumentsPropertyFacetProvider` | Document/Drawing/CadModel | Id, Kind, Name, Engineering Identifier, Document Number, Drawing Number, Model Format, Classification, Owner, Discipline, Category, Tags, Description/Notes, Status, Revision, Last Revised By, Parent, Deleted*, References (Digital Thread), Documents (Digital Thread) | 20 |
| `VerificationActivityPropertyFacetProvider` | VerificationActivity/Inspection | Id, Kind, Name, Subject, Method, Owner, Discipline, Description/Notes, Status, Revision, Last Revised By, Parent, Deleted*, Result History, Latest Outcome, Latest Verified At, Latest Verified By, Referenced Materials, Based On Calculation Record(s), Referenced Document(s), Verifies (Digital Thread), References (Digital Thread) | 22 |
| `ManufacturingOperationPropertyFacetProvider` | ManufacturingOperation | Id, Kind, Name, Engineering Identifier, Part, Classification, Owner, Discipline, Description/Notes, Status, Revision, Last Revised By, Parent, BOM Sequence (ItemNumber), BOM Quantity, Deleted*, References (Digital Thread), Manufactured By/Documented By/Verified By (Digital Thread) | 20 |

(* = conditional, only shown when the underlying value is set or the object is deleted)

Every provider re-derives the same ~14-facet generic base (Id/Kind/Name/Engineering Identifier/Owner/Discipline/Classification-or-Category/Tags/Description/Status/Revision/Last Revised By/Parent/Deleted) independently rather than sharing one implementation — the same shape repeated six times, each with its own raw-GUID `Id`/`Parent` plumbing facets.

---

## 6. Engineering Calculations workspace

**How many calculation definitions exist, how many are drivable**: `EngineeringCalculationCatalogue.All()` (`src/Tempest.Workspace/Engineering/EngineeringCalculationCatalogue.cs:41-57`) registers exactly **6** calculation definitions with the engine — Bracket Section Check, Bolt Shear Capacity, Beam Bending Stress, Bearing Load Capacity, Pressure Vessel Wall Thickness, Material Selection Margin — and states outright, in its own doc comment, that "all five [non-bracket] genuinely compute, but only the bracket section check has a governed entry point that resolves its inputs from a released reference" (`EngineeringCalculationCatalogue.cs:20-28`). **Only 1 of 6 (17%) is drivable** from `EngineeringCalculationView`; the other 5 carry `IsDrivableHere: false` and a fixed, disclosed reason string, `NotDrivableHere` (`EngineeringCalculationCatalogue.cs:34-37`): "Registered and computable, but this workspace has no input form for it yet... which only the bracket section check has today." The comment explicitly names the defect class this avoids repeating: `TD-159`, "a template offered and an execution that throws."

**What a user can do end to end, for the one drivable calculation** (`BracketCalculationWorkbench.cs`, `src/Tempest.Workspace/Engineering/BracketCalculationWorkbench.cs`), wired to `EngineeringCalculationView` buttons in `MainWindow.cs:515-528`:
1. **Populate** the material library from the shipped seed corpus, additive/idempotent, every record landing `Draft` (`PopulateMaterialLibraryAsync`, `BracketCalculationWorkbench.cs:181-186`).
2. **Add Material** — the WP 17.9.2 fix for "no way to add a material": a hand-entered record (name, designation, yield strength, density, source organisation, source document) registered as `Draft` (`AddMaterialAsync`, `BracketCalculationWorkbench.cs:113-154`).
3. **Release** — verify a Draft material against a named source, then release it, both governed by `ReferenceReviewService`, refused outright if nobody is signed in (`VerifyAndReleaseAsync`, `BracketCalculationWorkbench.cs:199-212`).
4. **Calculate** — run the bracket section check against a released material via `GovernedBracketCheckService`; a refusal (e.g. unreleased material) is reported, never silently computed anyway (`RunAsync`, `BracketCalculationWorkbench.cs:374-418`).
5. The run is **named and recorded as a real governed `"Calculation"` object** — the identical Kind the Workspace/Calculations discipline itself uses — and **parented to the currently open project** when one is open: `EngineeringCalculationRegister.NameAsync` reads `_projects?.Current?.Id` and passes it as `parentId` (`EngineeringCalculationRegister.cs:79`, `97-119`). **This is a real, working answer to journey goal (c)** ("run a governed calculation and attach it to a project") — the one place in the audited codebase where a Create-with-project-placement path other than Mechanical's actually works.
6. Past calculations can be **opened read-only** (immutable record, only "reference now" fields re-read live so the panel can show the reference has moved on without the recorded result changing — `OpenAsync`, `BracketCalculationWorkbench.cs:288-308`), **renamed**, and **retired** (a soft, reversible lifecycle transition to `Cancelled`, never a delete — `RetireAsync`, `RenameAsync`, `EngineeringCalculationRegister.cs` remarks at `:54-67`).

**Two distinct calculation models exist in this codebase**, confirmed by direct file comparison:

| | Engineering Calculations workspace (`ShellArea.EngineeringCalculation`) | "Calculation" object kind (`Workspace/Calculations`) |
|---|---|---|
| Files | `src/Tempest.Desktop/Views/EngineeringCalculationView.cs`, `src/Tempest.Desktop/Composition/EngineeringCalculationCoordinator.cs`, `src/Tempest.Workspace/Engineering/BracketCalculationWorkbench.cs`, `EngineeringCalculationCatalogue.cs` | `src/Tempest.Workspace/Workspace/Calculations/CalculationsWorkspaceRegistration.cs` (+ `CalculationsNodeProvider`, `CalculationsWorkspaceViewFactory`, `CalculationsPropertyFacetProvider`) |
| Reached via | Its own rail entry, "Engineering Calculations" | The generic Engineering Workspace's Calculations Explorer area, or the `calculations.*` Ribbon/Palette commands (Section 2) |
| Drives execution | Yes, for exactly 1 of 6 registered definitions (Bracket Section Check), through a governed, reference-resolving flow | No — `calculations.execute`/`calculations.recalculate` are both `Binding.Unavailable` (Section 2); this discipline has no input form either |
| Templates | None of its own; reads the same 6 engine-registered definitions the Calculations discipline's `CalculationTemplateRegistry` also registers 5 of (`CalculationsWorkspaceRegistration.cs:279-292`) | Registers 5 representative "CalculationTemplate" entries (Bolt, Beam, Bearing, Pressure Vessel, Material Selection) as a distinct synthetic Kind, discoverable in the Explorer's "Templates" node (`CalculationsNodeProvider.cs`) but with no working Execute path either |
| Convergence point | `EngineeringCalculationRegister.NameAsync` creates a real object of Kind `"Calculation"` (`CalculationObjectFactoryRegistry.CalculationKind`) — the exact same Kind the other model owns | Once a bracket calculation is named, it becomes visible in this discipline's own Explorer tree/Property Inspector like any other `"Calculation"` object |

The two surfaces converge only at the data layer (both write/read the same `"Calculation"` Kind); the workflows, UI, and commands are entirely separate, and a user exploring the Ribbon/Palette's `calculations.*` commands would have no way to discover that the actually-working governed calculation flow lives on a different rail entry entirely.

---

## 7. Cockpit / Home

`CockpitView` (`src/Tempest.Desktop/Views/CockpitView.cs`) renders `EngineeringCockpit` (`src/Tempest.Workspace/Workspace/EngineeringCockpit.cs`), reached as `IWorkspace.Cockpit` and set as the Document Area's own permanent Home tab (Section 1). `EngineeringCockpit`'s own doc comment states, in one place, precisely which of its regions are real and which are not (`EngineeringCockpit.cs:62-71`):

- **Real, live reads**: `RecentActivity`, `ContinueWhereILeftOff`, `AreaCount`, `OpenDocumentCount`, `AvailableCommands`, and the per-discipline counts for Requirements/Calculations/Documents/Verification/Manufacturing, each sourced from its own dedicated `*CockpitReadModel` collaborator (`EngineeringCockpit.cs:81-129`). Decisions/Risks/Milestones are also real, read directly on `EngineeringCockpit` itself. "Overdue Actions" was a disclosed, permanent empty placeholder ("no due-date field exists anywhere in this Domain to compute overdue from honestly") until `EngineeringTask.DueDate`/`IsOverdue` became real domain state, at which point it was wired to the real field (`EngineeringCockpit.cs:308-325`).
- **Mechanical is present but excluded from the health rollup by design, not oversight**: `MechanicalCockpitReadModel`'s own doc comment states "Mechanical carries no `Status`/`KpiCards` member of its own... unlike Requirements/Calculations/Documents/Verification/Manufacturing, `EngineeringCockpit.Health`/`HealthScoreDisplay` never included a Mechanical discipline status in their own rollup — a pre-existing asymmetry this move preserves exactly" (`MechanicalCockpitReadModel.cs:16-22`). Its own `LiveProjects`/`ProjectName`/`RecentProjects` reads remain real.
- **Genuinely fabricated/permanent placeholders**: `ReviewStatus` always returns `EngineeringHealthStatus.Unknown` — "the Review discipline's own status — always `Unknown` today" (`EngineeringCockpit.cs:189-190`), never computed from anything. `Materials` is explicitly "not wired to the Workspace at all" (`EngineeringCockpit.cs:70`); one KPI/attention entry says so on the surface itself: "Other disciplines still placeholder... Materials remain out of the Workspace's own scope until their own Work Package integrates them." (`EngineeringCockpit.cs:248`).
- **KPI cards degrade honestly, not silently**: each discipline's KPI card shows the real total when it is non-zero, and a plain "—" with `IsPlaceholder: true` when it is zero, rather than fabricating a number (`EngineeringCockpit.cs:472-477`).

**Conclusion**: the Cockpit's numbers are, in the overwhelming majority, real read models over live domain state — not a mocked dashboard — with a small number of explicitly self-disclosed exceptions (`ReviewStatus`, Materials) that the code itself names as placeholders rather than concealing.

---

## 8. Journey gaps table

| Goal | Reachable today? | Steps | What is missing | Citations |
|---|---|---|---|---|
| (a) Create a project and put a part under it | **Yes** | Rail → Engineering → `mechanical.create` (Kind=Project) → select the new Project → `mechanical.create` (Kind=Part) | Nothing — this is the one Create path with a real default-placement rule | `MechanicalCreateParentPolicy.cs:42-55`, `MechanicalWorkspaceRegistration.cs:95-117` |
| (b) Record a requirement and link it to a part | **No, end to end** | `requirements.create` succeeds (Bound, `CommandContextRequirement.None`) | The created Requirement has no `GroupId` and belongs to no Collection, so it has **no node anywhere in the Requirements Explorer tree** (Section 3) — nothing to select to link from. Even if reached by Id, `requirements.link` is `Binding.Unavailable` (needs an object-picker that does not exist) | `CreateRequirementCommand.cs:14`, `RequirementsService.cs:123-147`, `RequirementsNodeProvider.cs:47-61`, `RequirementsWorkspaceRegistration.cs:205-211` |
| (c) Run a governed calculation and attach it to a project | **Yes** | Rail → Engineering Calculations → Populate/Add Material → Release → Calculate | Nothing — `EngineeringCalculationRegister.NameAsync` parents the resulting `"Calculation"` object on the currently open project automatically | `EngineeringCalculationRegister.cs:79`, `97-119`, `BracketCalculationWorkbench.cs:374-418` |
| (d) Add a document/drawing revision to a part | **No** | `documents.create` succeeds (Bound) but ignores context entirely | `documents.create`'s binding passes no `ParentId` (`CommandContextRequirement.None`), so the Document can never be structurally placed under a Part or a Project. `Part`/`Assembly`/`Component` do not implement `IHasAttachments`, so a file cannot be attached directly to a Part via the Object Editor either. `documents.attach` (metadata-only, no real file picker) is `Binding.Unavailable` from the Ribbon/Palette — though the Object Editor's own Attachments mini-form (filename/content-type/size, no real upload) does work when an attachable object is selected | `DocumentsWorkspaceRegistration.cs:84-92`, `138-140`, `PhysicalConfiguration.cs:3-20` (no `IHasAttachments`), `ObjectEditorView.cs:963-991` (working manual Attach form) |
| (e) Record a verification result against a requirement | **No, structurally** | Select a (findable) Requirement → `verification.create` (Bound, subject = selection) creates a `VerificationActivity` whose `SubjectId` is a bare, unresolved field (no relationship edge created) → select the Activity → `verification.record-result` (Bound) records real evidence | The recorded evidence's `VerifiedBy` edge is created from the **Activity's own id**, never from the Requirement's id (`RecordVerificationResultCommand`'s own doc comment: "linked back to the Verification Activity itself... one link-hop earlier" than the sample module's "directly-against-a-Requirement" mechanism). The Requirement's own "Verification Coverage" facet counts only edges from its own id (`GetRelationshipsAsync(requirementId)` filtered to `VerifiedByRelationshipKind`), so it **always reads "Not Verified"** no matter how many Activities reference it as Subject and have real results recorded. No bound command lets `verification.record-result` target a Requirement directly (`appliesToKinds` is `VerificationActivity`-only) | `VerificationActivityFactoryRegistry.cs:41-59` (Subject stored, no link created), `RecordVerificationResultCommand.cs:22-29`, `RequirementsPropertyFacetProvider.cs:87-102`, `VerificationWorkspaceRegistration.cs:79` (`boundKinds` = `["VerificationActivity"]` only) |
| (f) Find any object by name from anywhere | **Partially** | `Ctrl+K`/header search opens `CommandPaletteOverlay`; `Ctrl+F` filters the currently-open Explorer area's own tree | The Command Palette searches `ICommandRegistry.Items` — **commands, not objects** ("Create Mechanical Object", not "Bracket Assembly Rev B"). The Explorer filter (`ProjectExplorerView.cs:81`) is real and keeps recent searches, but is scoped to whichever single discipline area is currently open — there is no cross-discipline, cross-project "find this object anywhere" search | `CommandPaletteOverlay.cs:10-17` ("a real overlay over `ICommandRegistry.Items`"), `MainWindow.cs:595` (header search opens the same palette), `ProjectExplorerView.cs:81-227` |
| (g) See what changed recently | **Yes** | Rail → Home → Cockpit's "Recent Activity"/"Continue where I left off" cards | Nothing — both are named as real, live reads in `EngineeringCockpit`'s own "Real vs. placeholder" disclosure | `EngineeringCockpit.cs:62-64` |

---

## 9. Summary — objective findings

1. 5 of the 10 global rail modules (Tasks, Commercial, Resources, Knowledge, Administration) render `DeclaredCapabilityView`, not a real surface — `src/Tempest.Workspace/Shell/NavigationAvailability.cs:89-107`.
2. `ShellArea.Home` renders the identical control as `ShellArea.Engineering` (`_engineeringSurface`) — there is no surface unique to Home — `src/Tempest.Desktop/MainWindow.cs:1011-1022`.
3. 2 of the 9 `ProjectWorkspace` tabs (Reports, Settings) are placeholders — `src/Tempest.Workspace/Shell/ProjectAreas.cs:54-60`.
4. 18 of 74 (24%) registered `CommandDescriptor`s carry `Binding = CommandBinding.Unavailable(...)`, including every discipline's own Move and Copy command (12 of the 18) — `src/Tempest.Core/Commands/CommandBinding.cs:86-107`; example: `src/Tempest.Workspace/Workspace/Mechanical/MechanicalWorkspaceRegistration.cs:163-165`.
5. `manufacturing.create`'s default Kind is `ManufacturingOperation`, which requires a `PartId` the binding never supplies — invoking it with its own default selection always throws `ArgumentException("PartId is required to create a ManufacturingOperation.")` — `src/Tempest.Workspace/Workspace/Manufacturing/CreateManufacturingObjectCommand.cs:87-90`, `src/Tempest.Workspace/Workspace/Manufacturing/ManufacturingWorkspaceRegistration.cs:137-145`.
6. A Requirement created via the bound `requirements.create` command has no `GroupId` and belongs to no Collection; `RequirementsNodeProvider.GetRootNodesAsync` returns only Collections and root Groups, so the new Requirement has no node anywhere in its own Explorer tree — the same defect class as the Product Owner's original Mechanical bug, present here without an analogous fix — `src/Tempest.Workspace/Workspace/Requirements/CreateRequirementCommand.cs:14`, `src/Tempest.Workspace/Workspace/Requirements/RequirementsNodeProvider.cs:47-61`.
7. `documents.create`, `calculations.create`, and `manufacturing.create` all bind under `CommandContextRequirement.None` and never supply a `ParentId`, so objects created through them are always unparented — never a member of any project via `ProjectMembership`'s `IHasParent` walk — `src/Tempest.Workspace/Workspace/Documents/DocumentsWorkspaceRegistration.cs:84-92`, `src/Tempest.Workspace/Projects/ProjectDocumentRegister.cs:90-118`.
8. A verification result recorded through the shipped `verification.record-result` command links its evidence to the selected `VerificationActivity`'s own id, never to the Requirement the Activity merely names as `SubjectId`; `RequirementsPropertyFacetProvider`'s "Verification Coverage" facet counts only edges from the Requirement's own id, so it reads "Not Verified" regardless — `src/Tempest.Workspace/Workspace/Verification/RecordVerificationResultCommand.cs:22-29`, `src/Tempest.Workspace/Workspace/Requirements/RequirementsPropertyFacetProvider.cs:87-102`.
9. Every facet provider in all six disciplines displays the `Id` and `Parent` facets as raw, unresolved GUID strings (`target.Id.ToString()`, `hasParent.ParentId?.ToString()`) with no name resolution, unlike `Principal`-kind facets which are resolved through `IPrincipalDirectory.Describe` — `src/Tempest.Workspace/Workspace/Mechanical/MechanicalPropertyFacetProvider.cs:46,89`, `src/Tempest.Desktop/Views/PropertyInspectorView.cs:307-309`.
10. The Object Editor's own Bill-of-Materials section previously showed Quantity/Find Number/Reference Designator on a Project and on a Calculation because every canonical object implements `IHasBomLine`; `WP 17.9.1` fixed it with a `BomKinds` Kind allowlist — `src/Tempest.Desktop/Editors/ObjectEditorView.cs:649-660`.
11. Only 1 of the 6 calculation definitions registered with the engine (`Bracket Section Check`) is drivable from the Engineering Calculations workspace; the other 5 carry a fixed "no input form for it yet" reason string — `src/Tempest.Workspace/Engineering/EngineeringCalculationCatalogue.cs:20-37`.
12. Two separate calculation models exist in the codebase — the `"Calculation"`/`"CalculationSet"`/`"CalculationTemplate"` object-kind model (`src/Tempest.Workspace/Workspace/Calculations/CalculationsWorkspaceRegistration.cs`) and the governed Engineering Calculations workspace (`src/Tempest.Workspace/Engineering/EngineeringCalculationCatalogue.cs`, `BracketCalculationWorkbench.cs`) — converging only where `EngineeringCalculationRegister.NameAsync` writes into the shared `"Calculation"` Kind — `src/Tempest.Workspace/Engineering/EngineeringCalculationRegister.cs:106-119`.
13. The Command Palette (`Ctrl+K`, and the header's own search field) searches `ICommandRegistry.Items` — commands — not engineering objects by name; no cross-discipline "find this object anywhere" search exists in the Desktop — `src/Tempest.Desktop/Views/CommandPaletteOverlay.cs:10-17`, `src/Tempest.Desktop/MainWindow.cs:595`.
14. `ObjectEditorView`'s own doc comment states `PropertyInspectorView`'s Validation section "remains the disclosed placeholder it always was... since it only ever sees `PropertyFacet`s, never the real object" — stale: `PropertyInspectorView.AddValidationSection` (`WP 10.8A`) resolves the real object and calls the real `IValidatable.ValidateAsync()` directly, for every Kind except Requirement — `src/Tempest.Desktop/Editors/ObjectEditorView.cs:74-84`, `src/Tempest.Desktop/Views/PropertyInspectorView.cs:214-274`.
15. `EngineeringCockpit.ReviewStatus` always returns `EngineeringHealthStatus.Unknown`, and Materials is explicitly disclosed as "not wired to the Workspace at all" — `src/Tempest.Workspace/Workspace/EngineeringCockpit.cs:189-190`, `:70`.

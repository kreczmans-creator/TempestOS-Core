# WP 9.1A — Requirements Management Workspace — Implementation Report

## Status

Complete. `v0.9.0` ("Mechanical Foundation")'s own third Work Package —
the complete Requirements Management experience, integrated into the
Engineering Workspace, Engineering Cockpit, and Digital Thread, using
the already-real Requirements Framework (`WP 7.3A`). The second real
Engineering discipline wired into the Workspace, after Mechanical
(`WP 9.0A`/`WP 9.0B`).

## What Was Implemented

**Domain layer** (`Tempest.Core.Requirements`) — seven new, additive
`IRequirementsService` methods (`SetOwnerAsync`, `SetPriorityAsync`,
`DeleteAsync`, `MoveToGroupAsync`, `MoveGroupAsync`, `DeleteGroupAsync`,
`DeleteCollectionAsync`) plus two more additive enumeration methods
(`ListCollectionsAsync`, `ListGroupsAsync`, added during implementation
once the Explorer tree's own real need for them was discovered —
`ADR-0084`). `IRequirement` gains `Owner`, `Priority` (a new
`RequirementPriority` enum), `IsDeleted`, `GroupId`; `IRequirementGroup`/
`IRequirementCollection` each gain `IsDeleted`. `RequirementGroupDto`'s
own parent-resolution storage model is corrected (`ADR-0084`). A new,
small `IRequirementValidationService`/`RequirementValidationService`
(duplicate identifier, orphan, missing verification, missing allocation,
advisory relationship kind) reuses `IValidationResult`/`IValidationDiagnostic`'s
own generic result shape — never `IValidationRule` itself, which is
scoped to `IEngineeringObject` and cannot validate an `IRequirement`.

**Workspace layer** (`Tempest.App.Workspace.Requirements`, new
namespace, mirrors `.Mechanical`'s own shape) — `RequirementsNodeProvider`
(rooted at every live Collection and root Group; a Requirement node is
always a leaf), `RequirementsWorkspaceView`/`RequirementsWorkspaceViewFactory`
and `RequirementsPropertyFacetProvider` (each registered for all three
Requirements Kinds — `"Requirement"`/`"RequirementCollection"`/
`"RequirementGroup"`), eighteen commands (Create/Revise/SetStatus/
SetOwner/SetPriority/Delete/Move/Duplicate/Link, per-Group and
per-Collection Create/Move/Delete, AddToCollection, and three Bulk
commands for Status/Owner/Priority), and `RequirementsWorkspaceRegistration`
— the composition-root entry point, registered from `Program.cs`
alongside `MechanicalWorkspaceRegistration`, after `shell.StartAsync()`,
for the identical, already-disclosed reason. Search needed no new code
at all: `ProjectExplorer.FilterAsync` (`WP8.1B`) already walks whatever
provider is registered for the current area — registering
`RequirementsNodeProvider` made Requirements search work automatically.

**Multi-selection** (`Tempest.App.Workspace`, frozen `WP8.0B` contracts)
— `ISelectionService` gains `SelectedItems`/`ToggleSelectionAsync`;
`IWorkspaceContext` gains `SelectedItems`; a new
`WorkspaceSelectionSetChangedEvent` fires alongside the existing,
unchanged `WorkspaceSelectionChangedEvent` (`ADR-0085`). Resolves
`FCR-0039` (`WP 9.0A`'s own Future Capability Assessment) now that Bulk
editing is a real consumer.

**Import/Export** — `RequirementCollectionExportAdapter` (`Tempest.Samples`,
not `Tempest.App` — required by the project-reference direction, see
Lessons Learned) exports/re-imports a whole Requirement Collection,
scaling `RequirementExportAdapter`'s (`WP 7.3A`) own single-requirement
demonstration to Requirement Set granularity, reusing the same
`IExportable`/`IExportableKind`/`IImportable` triad and
`ImportService.RegisterImportable` precedent.

**Engineering Cockpit** (`Tempest.App.Workspace.EngineeringCockpit`) —
`RequirementsStatus` is now a real, derived `EngineeringHealthStatus`
(`Unknown`/`Blocked`/`Attention`/`Healthy`, driven by
`IRequirementValidationService`); the `"Requirements"` entry in
`KpiCards` is real once a live Requirement exists; a new
`RequirementsKpiCards` property supplies the full nine-card breakdown
this Work Package's own controlling instruction names (Total/Draft/
Review/Approved/Released/Verification Coverage/Allocation Coverage/
Requirement Health/Outstanding Actions — see the "Released" status-name
mapping disclosure, below); `AttentionItems` and `OpenActions` each gain
a real, conditional Requirements entry.

**Representative data** (`RequirementsWorkspaceSampleModule`, new,
constructor-injects `MechanicalProductStructureSampleModule` directly —
see Lessons Learned) — a three-level Group hierarchy, one Group created
as a root and then moved (a direct, working proof of the
`RequirementGroupDto` fix); ten Requirements spanning `Draft`/
`Reviewed`/`Approved`/`Allocated`/`Verified`/`Satisfied`; `DependsOn`/
`DerivesFrom`/`Satisfies` links; allocations to the real Mechanical
sample data's own Wing Assembly and Spar Web Plate (`AllocatedTo` — the
real cross-discipline integration point); one verification recorded
through `IVerificationService.RecordAsync`; one soft-deleted requirement;
two Requirement Collections; one `RequirementCollectionExportAdapter`
registration.

## Disclosed Status-Name Mapping (`RequirementsKpiCards`, design note)

This platform's own `RequirementStatus` (`WP7.2C Requirement Lifecycle
Model.md`) has no `"Released"` value — the closed set is `Draft`/
`Reviewed`/`Approved`/`Allocated`/`Verified`/`Satisfied`/`Obsolete`. The
Cockpit's own `"Released"` card reports the `Satisfied` count — the
closest existing terminal-success status — rather than inventing a new
status value, which this Work Package's own controlling instruction
forbids. `Allocated`/`Verified` are not silently dropped: both remain
visible via `RequirementsStatus`'s own validation-driven derivation and
every live requirement's own Property Inspector facets.

## Two Disclosed, Pre-Existing/Pre-Commit Findings — Both Fixed

**`RequirementValidationService`/`RequirementsPropertyFacetProvider`/
`EngineeringCockpit` originally called the permission-gated
`GetEvidenceAsync`.** Found while running the Workspace integration
suite against real seeded data under the sample module's own
unprivileged principal: `IRequirementsService.GetEvidenceAsync` is
transitively gated on `VerificationService.ReadPermission` (`ADR-0061`,
unchanged, correct as originally designed in `WP 7.3A`) — but a
Property Inspector selection, a Cockpit KPI read, and a validation read
must never throw because the current principal lacks a narrower
capability than "can view this at all." Fixed by reading
`GetRelationshipsAsync` for the `verifiedBy` relationship link directly
(the same fact `GetEvidenceAsync` itself is built from) in all three
call sites — no weaker check, only a non-gated one. `EngineeringCockpit`
additionally keeps a defensive `catch (PermissionDeniedException)` around
its own validation-results read, since `IRequirementValidationService`
is an interface, not a sealed contract to today's one implementation.

**`RequirementGroupHasChildrenException`'s own guard could not see live
sub-groups.** `IEngineeringDocumentStore` has no list-by-Kind capability,
and Groups were never enumerable before this Work Package's own
`ListGroupsAsync` was added (for the Explorer tree's own unrelated
need) — once it existed, `DeleteGroupAsync`'s own has-children guard was
extended to use it, closing a gap its own exception type had only just
disclosed moments earlier in the same implementation session, before a
committed record of the narrower version ever existed.

## New ADRs

`ADR-0084` — Requirements lifecycle/ownership/priority/enumeration
operations are additive `IRequirementsService` methods, never a
facet-composition retrofit, plus the `RequirementGroupDto` storage-model
fix. `ADR-0085` — multi-selection is additive members on the frozen
`ISelectionService`/`IWorkspaceContext` contracts, resolving `FCR-0039`.

## Engineering Core Integration

Reuses, unmodified: the entire `WP 7.3A` Requirements Framework (every
new Domain method follows `SetStatusAsync`'s own already-proven `dto
with {...}` + `ReviseAsync` mutation shape); `ProjectExplorer.FilterAsync`
(`WP8.1B`, its own first real cross-discipline reuse for Search);
`IVerificationService.RecordAsync`/`GetRelationshipsAsync` (Digital
Thread reads, no new traversal, per this Work Package's own explicit
"Do not implement new traceability mechanisms"); the `WP 6.7` Export/
Import framework; `EngineeringHealthStatus`/`CockpitKpiCard`/
`CockpitAttentionItem` (`WP8.1C`, unchanged vocabulary). Zero new
Platform Services; zero new persistence mechanism beyond the two small,
`ADR-0059`-precedented registries `ADR-0084` records; zero duplication
of any existing framework.

## Testing

70 new tests (1738 → 1808): 32 Requirements Domain
(`RequirementsLifecycleExtensionsTests` — 24, `RequirementValidationServiceTests`
— 8), 12 multi-selection (`SelectionServiceTests`, 7 → 19), 10 Cockpit
Requirements KPIs (`EngineeringCockpitTests`, 27 → 37), 16 full Workspace
integration tests against the real seeded graph
(`RequirementsWorkspaceIntegrationTests`). Two pre-existing tests
corrected for the new module/discipline counts (`ClockModuleDiscoveryTests`'
own sample-module-discovery count, `+2` for the two new Requirements
Workspace sample modules; one `WorkspaceShellTests` placeholder-text
assertion, unrelated content correction — see Lessons Learned).
1808/1808 passing, zero failures, four full clean-rebuild-and-test runs
across this Work Package's own verification (two Debug, two Release via
`src/TempestOS.slnx`), all clean.

## Repository Metrics

4 new files under `src/Tempest.Core/Requirements/`
(`RequirementPriority.cs`, `RequirementGroupHasChildrenException.cs`,
`IRequirementValidationService.cs`, `RequirementValidationService.cs`);
25 new files under `src/Tempest.App/Workspace/`
(`WorkspaceSelectionSetChangedEvent.cs` plus 24 under the new
`Requirements/` sub-namespace — 4 provider/view/factory, 18 commands, 1
registration); 3 new files under `src/Samples/Tempest.Samples/`
(`RequirementsWorkspaceExplorerModule.cs`,
`RequirementsWorkspaceSampleModule.cs`,
`RequirementCollectionExportAdapter.cs`); 23 existing source/test files
edited (11 in `Tempest.Core.Requirements`, `TempestHost.cs`; 6 in
`Tempest.App.Workspace`, `Program.cs`; 4 test files corrected/extended;
2 more test files extended in place); 2 new ADRs.

## Related Documents

`ADR-0084`; `ADR-0085`; `WP9.1A Engineering Review Report.md`; `WP9.1A
Security Review Report.md`; `WP9.1A Systems Engineering Review.md`;
`WP9.1A Architecture Conformance Review.md`; `WP9.1A Technical Debt
Assessment.md`; `WP9.1A Future Capability Assessment.md`; `WP9.1A
Lessons Learned.md`; `WP9.0A Implementation Report.md`; `WP9.0B
Implementation Report.md`.

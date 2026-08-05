# WP 9.1A — Requirements Management Workspace — Architecture Conformance Review

## Purpose

Independently re-verifies that every new or changed piece of this Work
Package sits in its own correct architectural layer, introduces no
circular dependency, and follows the frozen Dependency Rules exactly
where each already applies.

## 1. Layering

| Component | Layer | Depends on | Verdict |
|---|---|---|---|
| Seven new `IRequirementsService` methods + `ListCollectionsAsync`/`ListGroupsAsync` | `Tempest.Core.Requirements` | `IEngineeringDocumentStore`/`IPersistenceStore` (already-existing dependencies only) | Conforms — no new dependency added |
| `IRequirementValidationService`/`RequirementValidationService` | `Tempest.Core.Requirements` | `IRequirementsService` only | Conforms |
| `RequirementsNodeProvider`/`RequirementsWorkspaceView(Factory)`/`RequirementsPropertyFacetProvider` | `Tempest.App.Workspace.Requirements` | `Tempest.Core.Requirements` directly (the Engineering Discipline integration layer, per `WP 9.0A`'s own precedent) | Conforms |
| 18 Requirements commands + `RequirementsWorkspaceRegistration` | `Tempest.App.Workspace.Requirements` | `Tempest.Core.Requirements`/`Tempest.Core.Commands` | Conforms |
| `ISelectionService`/`WorkspaceContext` (extended) | `Tempest.App.Workspace` | Unchanged dependency shape | Conforms — no new dependency added |
| `EngineeringCockpit` (extended) | `Tempest.App.Workspace` | `IRequirementsService`/`IRequirementValidationService` (two new constructor dependencies, both already-existing services) | Conforms |
| `RequirementsWorkspaceSampleModule` | `Tempest.Samples` | `IRequirementsService`/`IVerificationService`/`ImportService`/`MechanicalProductStructureSampleModule` | Conforms — see §2 for the one genuinely new dependency shape |
| `RequirementCollectionExportAdapter` | `Tempest.Samples` | `Tempest.Core.Requirements`/`Tempest.Core.ExportImport` | Conforms — corrected into this project after an initial placement error (`Tempest.App`); see Lessons Learned |

No new project reference was added anywhere — `RequirementCollectionExportAdapter`'s
relocation was a same-project-set file move, not a new reference.

## 2. Circular Dependency Analysis

None introduced. **One genuinely new dependency shape, disclosed
directly:** `RequirementsWorkspaceSampleModule` constructor-injects
`MechanicalProductStructureSampleModule` — the first sample module ever
to depend on another sample module's own instance. Verified safe by
direct inspection of `ModuleServiceCollectionExtensions.AddDiscoveredModules`
(every discovered module type is registered as a DI singleton) and
`ModuleLifecycleManager`'s own ordinal-Id initialisation ordering
(`tempest.samples.mechanicalproductstructure` sorts before
`tempest.samples.requirementsworkspace`) — by the time
`RequirementsWorkspaceSampleModule.InitialiseAsync` runs, the Mechanical
module's own has already completed and populated its own Ids. Not a
circular dependency: the edge is one-directional
(`RequirementsWorkspaceSampleModule` → `MechanicalProductStructureSampleModule`,
never the reverse), and a host that discovers the former without the
latter fails DI resolution immediately (`ServiceNotRegisteredException`),
never silently.

## 3. Extension-Point Conformance

`ProjectExplorer.FilterAsync`, `IWorkspaceManager.RegisterExplorerArea`/
`RegisterView`/`RegisterFacetProvider`, and `ICommandDispatcher.RegisterHandler`/
`ICommandRegistry.RegisterDescriptor` are all consumed exactly as their
own `WP8.0B`/`WP8.1B`/`WP 9.0A` precedent already established — verified
by direct comparison of `RequirementsWorkspaceRegistration.Register`
against `MechanicalWorkspaceRegistration.Register`, confirming an
identical call shape for every shared extension point.

## 4. Two In-Place Fixes — Conformance-Specific Verification

**`GetEvidenceAsync` → `GetRelationshipsAsync` correction.** Confirmed
via direct inspection that `RequirementValidationService`,
`RequirementsPropertyFacetProvider`, and `EngineeringCockpit` now share
the identical `VerificationService.VerifiedByRelationshipKind`-relationship-presence
check, reading only `IRequirementsService.GetRelationshipsAsync`
(unchanged, unmodified, not permission-gated) — no new Domain read
capability was added; this is a call-site correction only.

**`RequirementGroupDto` storage-model fix.** Confirmed the fix is
`internal`-only — `IRequirementGroup`'s own public shape carries
`ParentGroupId { get; }` exactly as before; only the private storage
mechanism `FindGroupAsync`/`MoveGroupAsync` read from changed. No public
contract was reopened.

## 5. API Stability Classification

| Member | Classification | Rationale |
|---|---|---|
| Seven new `IRequirementsService` methods, `ListCollectionsAsync`/`ListGroupsAsync` | **Provisional, additive** | First release carrying them; every existing method's own shape is unchanged |
| `IRequirement.Owner`/`Priority`/`IsDeleted`/`GroupId`, `IRequirementGroup.IsDeleted`, `IRequirementCollection.IsDeleted` | **Provisional, additive** | New properties on existing interfaces; every existing property is unchanged |
| `ISelectionService.SelectedItems`/`ToggleSelectionAsync`, `IWorkspaceContext.SelectedItems` | **Provisional, additive** | New members on frozen `WP8.0B` contracts; `Current`/`SelectAsync`/`ClearAsync` are byte-for-byte unchanged in behaviour |
| `IRequirementValidationService` | **Provisional** | First release carrying it |
| 18 Requirements commands, `RequirementsNodeProvider`/`RequirementsWorkspaceView(Factory)`/`RequirementsPropertyFacetProvider` | **Internal** | `Tempest.App`/`Tempest.Samples` implementation detail, not a published contract surface |
| `IRequirementsService`'s original 13 methods, `IRequirementGroup`/`IRequirementCollection`'s original shape, `ISelectionService.Current`/`SelectAsync`/`ClearAsync`, `WorkspaceSelectionChangedEvent` | **Stable, unchanged** | Confirmed byte-for-byte identical to their own pre-`WP 9.1A` shape |

## 6. Overall Verdict

**Fully conformant.** Every new dependency edge either already existed
in shape elsewhere in the platform, or is the one disclosed, verified-safe
genuine first (`RequirementsWorkspaceSampleModule` → `MechanicalProductStructureSampleModule`).
The two in-place fixes touch only this session's own not-yet-committed
code and correct, rather than introduce, a layering/availability
concern.

## Related Documents

`ADR-0067`; `ADR-0084`; `ADR-0085`; `WP9.0A Architecture Conformance
Review.md`; `WP9.0B Architecture Conformance Review.md`; `WP8.0B
Dependency Rules.md`; `WP8.2B Dependency Rules.md`.

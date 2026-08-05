# WP 9.0A — Mechanical Product Structure — Architecture Conformance Review

## Purpose

Independently re-verifies that every new or changed piece of this Work
Package sits in its own correct architectural layer, introduces no
circular dependency, and follows `WP8.0B Dependency Rules.md`/`WP8.2B
Dependency Rules.md` exactly where each already applies.

## 1. Layering

| Component | Layer | Depends on | Verdict |
|---|---|---|---|
| `IRenamable`/`IHasParent`/`IDeletable` | `Tempest.Core.EngineeringDomain` (Contracts) | `IEngineeringObject` only | Conforms — no dependency on `Tempest.App` |
| `EngineeringObjectBase` (extended) | `Tempest.Core.EngineeringDomain` (Implementation) | `EngineeringDomainContext`, `IEngineeringObjectRepository` (already-existing dependencies) | Conforms — no new dependency added |
| `IPropertyFacetProvider` | `Tempest.App.Workspace` (Contracts) | `PropertyFacet` only | Conforms — mirrors `IProjectExplorerNodeProvider`/`IWorkspaceViewFactory` exactly |
| `MechanicalProductStructureNodeProvider`/`MechanicalPropertyFacetProvider`/`MechanicalWorkspaceView(Factory)`/`MechanicalObjectFactoryRegistry`/six commands | `Tempest.App.Workspace.Mechanical` | `Tempest.Core.EngineeringDomain` (directly — this *is* the Engineering Discipline integration layer, the one place `Tempest.App` is expected to depend on a specific discipline's own Core namespace) | Conforms — mirrors how `Tempest.Samples`' own sample modules already depend on `Tempest.Core.EngineeringDomain` directly |
| `MechanicalProductStructureSampleModule`/`MechanicalWorkspaceExplorerModule` | `Tempest.Samples` | `Tempest.Core.EngineeringDomain`, `Tempest.Core.Modules`, `Tempest.Core.Navigation` | Conforms — identical dependency shape to `EngineeringDomainSampleModule`/`WorkspaceExplorerSampleModule` |
| `MechanicalWorkspaceRegistration` | `Tempest.App.Workspace.Mechanical` | `Tempest.Samples` (for `MechanicalWorkspaceExplorerModule.NavigationItemId`) | Conforms — `Tempest.App` already depends on `Tempest.Samples` (`Program.cs`'s own pre-existing, identical precedent for `WorkspaceExplorerSampleModule.NavigationItemId`) |

No new project reference was added anywhere; the existing `Tempest.Core`
→ `Tempest.Samples` → `Tempest.App` dependency direction (confirmed via
existing `.csproj` `ProjectReference`s) is unchanged.

## 2. Circular Dependency Analysis

None introduced. `Tempest.Samples` still never references `Tempest.App`
— confirmed explicitly by design (`MechanicalProductStructureSampleModule`'s
own XML documentation states this directly, and its seeding deliberately
does not exercise Copy/Duplicate, both `Tempest.App`-only types, for
exactly this reason).

## 3. Kind-Keyed Extension Point Conformance (`ADR-0067`)

Both new area/view registrations (`RegisterExplorerArea`, `RegisterView`
× 5 Kinds) and the new `RegisterFacetProvider` × 5 follow the identical
`TryAdd`/`DuplicateWorkspaceRegistrationException` shape every existing
registration already uses — verified by dedicated tests
(`RegisterFacetProvider_DuplicateKind_Throws...`). All registration is
performed by `Tempest.App`'s own composition root (`Program.cs`, via
`MechanicalWorkspaceRegistration`), never from inside a Host-discovered
module — `ADR-0071` remains fully honoured; only the composition root's
own internal timing (after `shell.StartAsync()`, not before) is new, and
is itself disclosed and reasoned in `ADR-0082`/the Implementation Report.

## 4. API Stability Classification

| Member | Classification | Rationale |
|---|---|---|
| `IRenamable`, `IHasParent`, `IDeletable`, `IPropertyFacetProvider` | **Provisional** | First release carrying them; mirrors how `WP8.2B`'s own original ten facets were provisional through `v0.8.0` |
| `IWorkspaceManager.RegisterFacetProvider` | **Provisional** | New member on an otherwise-Stable, frozen `WP8.0B` interface |
| `MechanicalObjectFactoryRegistry`, six commands, node/view/facet providers | **Internal** | `Tempest.App.Workspace.Mechanical` implementation detail, not part of any published contract surface |
| `IHasBusinessIdentifier`, `IAssembly.ChildIds`, `ISubAssembly.ParentAssemblyId`, `ISelectionService`, `LifecycleState` | **Stable, unchanged** | Confirmed byte-for-byte identical to `v0.8.0` |

## 5. Dependency Rules — Overall Verdict

**Fully conformant.** Every new dependency edge already existed in shape
elsewhere in the platform (Domain facet composition, Kind-keyed
registration, `Tempest.App` → `Tempest.Samples`); none is genuinely new
in kind, only in the specific pair of types it connects.

## Related Documents

`ADR-0067`; `ADR-0071`; `ADR-0080`; `ADR-0081`; `ADR-0082`; `WP8.0B
Dependency Rules.md`; `WP8.2B Dependency Rules.md`; `WP6.8 Platform
Architecture Conformance Report.md` (methodology precedent).

# WP 9.5A — Manufacturing Workspace — Architecture Conformance Review

## Purpose

Independently re-verifies that every new or changed piece of this Work
Package sits in its own correct architectural layer, introduces no
circular dependency, and follows the frozen Dependency Rules exactly
where each already applies.

## 1. Layering

| Component | Layer | Depends on | Verdict |
|---|---|---|---|
| `ManufacturingObjectFactoryRegistry`/`ManufacturingNodeProvider`/`ManufacturingCategory`/`ManufacturingWorkspaceView(Factory)`/`ManufacturingOperationPropertyFacetProvider` | `Tempest.App.Workspace.Manufacturing` | `Tempest.Core.EngineeringDomain` directly (the Engineering Discipline integration layer, per `WP 9.0A`'s own precedent) | Conforms |
| Eight Manufacturing commands + `ManufacturingWorkspaceRegistration` | `Tempest.App.Workspace.Manufacturing` | `Tempest.Core.EngineeringDomain`/`Tempest.Core.Commands`, plus `Tempest.App.Workspace.Documents`/`Tempest.App.Workspace.Verification` (the disclosed reuse edge, see §2) | Conforms |
| `EngineeringCockpit` (extended) | `Tempest.App.Workspace` | `EngineeringDomainContext` (already an existing constructor dependency — zero new dependency added, identical to `WP 9.2A`'s/`WP 9.4A`'s/`WP 9.3A`'s own zero-new-dependency finding) | Conforms |
| `DocumentObjectFactoryRegistry`/`DocumentsNodeProvider` (extended) | `Tempest.App.Workspace.Documents` | Unchanged dependency shape — three new `string` constants/category labels only | Conforms |
| `EngineeringManufacturingWorkspaceSampleModule` | `Tempest.Samples` | `IIdentityService`/`EngineeringDomainContext`/`IVerificationService`/`MechanicalProductStructureSampleModule`/`RequirementsWorkspaceSampleModule`/`EngineeringCalculationsWorkspaceSampleModule`/`EngineeringDocumentsWorkspaceSampleModule` | Conforms — see §2 for the cross-sample-module dependency edges |
| `ManufacturingWorkspaceExplorerModule` | `Tempest.Samples` | `INavigationProvider` only | Conforms — identical shape to `DocumentsWorkspaceExplorerModule`/`VerificationWorkspaceExplorerModule` |

No new project reference was added anywhere.
`Tempest.App.Workspace.Manufacturing` references
`Tempest.Core.EngineeringDomain` for its own three types, plus
`Tempest.App.Workspace.Documents`/`Tempest.App.Workspace.Verification`
for the disclosed facet/view provider reuse — the first intra-`Tempest.App`
Workspace-namespace-to-Workspace-namespace dependency this platform has
introduced; verified deliberate and one-directional (§2), not a layering
violation, since all three namespaces already sit at the identical
architectural layer (Workspace-layer Engineering Discipline
integration).

## 2. Circular Dependency Analysis

**Two kinds of new dependency edge, both verified safe.**

**Intra-`Tempest.App` namespace dependency (new to this project):**
`Tempest.App.Workspace.Manufacturing` references
`Tempest.App.Workspace.Documents.DocumentsPropertyFacetProvider`/
`DocumentsWorkspaceView(Factory)` and
`Tempest.App.Workspace.Verification.VerificationActivityPropertyFacetProvider`/
`VerificationActivityWorkspaceView(Factory)`/
`RecordVerificationResultCommand`/`Handler` directly. Verified
one-directional: neither `Tempest.App.Workspace.Documents` nor
`Tempest.App.Workspace.Verification` references
`Tempest.App.Workspace.Manufacturing` anywhere — confirmed by direct
`grep` of both namespaces for `Manufacturing` — so no cycle exists.
`ManufacturingWorkspaceRegistration.Register` is required to run after
`VerificationWorkspaceRegistration.Register` in `Program.cs` (it
dispatches through, rather than re-registers, the already-registered
`RecordVerificationResultCommand` handler) — a genuine ordering
requirement, disclosed in both files' own remarks and verified directly
by `ManufacturingWorkspaceIntegrationTests`'s own registration order.

**Cross-sample-module dependency edges (constructor-injected, the same
four `EngineeringDocumentsWorkspaceSampleModule` itself already
establishes — extended by none):**
`EngineeringManufacturingWorkspaceSampleModule` constructor-injects
`MechanicalProductStructureSampleModule`, `RequirementsWorkspaceSampleModule`,
`EngineeringCalculationsWorkspaceSampleModule`, and
`EngineeringDocumentsWorkspaceSampleModule`. Verified safe by the
identical mechanism every prior real-discipline Work Package's own
Architecture Conformance Review already verified —
`ModuleServiceCollectionExtensions.AddDiscoveredModules` registers every
discovered module type as a DI singleton, and `ModuleLifecycleManager`
initialises modules in ordinal Id order.
`tempest.samples.engineeringdomain`, then
`tempest.samples.mechanicalproductstructure`, then
`tempest.samples.requirementsworkspace`, then
`tempest.samples.workspacecalculations`, then
`tempest.samples.workspacedocuments`, then this module's own
`tempest.samples.workspacemanufacturing` sort in exactly that order (`e`
< `m` < `r` < `w`, then `workspacec` < `workspaced` < `workspacem`
ordinally) — confirmed directly by inspecting all six literal Id
strings, not merely assumed.

**Deliberately absent edge, checked and disclosed, not merely
omitted:** this module does **not** constructor-inject
`EngineeringVerificationWorkspaceSampleModule`. Decisive: that module's
own id (`tempest.samples.workspaceverification`) sorts **after** this
module's own id (`tempest.samples.workspacemanufacturing`) — `m` < `v`
ordinally — confirmed directly. A constructor dependency on it would
therefore have been a genuine `ModuleLifecycleManager` initialisation-order
defect (this module would run, and fail resolving a not-yet-initialised
dependency, before the dependency's own `InitialiseAsync` ever runs),
not merely an unneeded coupling. This module instead builds its own
`Inspection` object directly.

A further, disclosed edge is a **query, not a constructor injection**,
mirroring `WP 9.4A`'s/`WP 9.3A`'s own identical precedent exactly: this
module reads `_context.Repository.ListByKindAsync("Supplier")` at
runtime to find the base `EngineeringDomainSampleModule`'s own
already-created live Supplier object, rather than constructor-injecting
that module itself — the identical, deliberately looser coupling
already established, robust to `EngineeringDomainSampleModule` not
being discovered at all in a given host composition.

All edges are one-directional; a host that discovers
`EngineeringManufacturingWorkspaceSampleModule` without also discovering
its four constructor-injected dependencies fails DI resolution
immediately (`ServiceNotRegisteredException`), never silently —
confirmed directly by `ManufacturingWorkspaceIntegrationTests`'s own
explicit module list.

## 3. Extension-Point Conformance

`ProjectExplorer.FilterAsync`, `IWorkspaceManager.RegisterExplorerArea`/
`RegisterView`/`RegisterFacetProvider`, and `ICommandDispatcher.RegisterHandler`/
`ICommandRegistry.RegisterDescriptor` are all consumed exactly as their
own `WP8.0B`/`WP8.1B`/`WP 9.0A`–`WP 9.3A` precedent already established —
verified by direct comparison of `ManufacturingWorkspaceRegistration.Register`
against `DocumentsWorkspaceRegistration.Register`/
`VerificationWorkspaceRegistration.Register`, confirming an identical
call shape for every shared extension point, plus two `RegisterView`/
`RegisterFacetProvider` calls per reused Kind (`"WorkInstruction"`/
`"Inspection"`) constructing an existing provider type with a new Kind
string argument — the identical call shape those providers' own
constructors already accept, no new overload or extension point needed.

## 4. `SetBomLineCommand`/`RecordVerificationResultCommand` — Verified to Introduce No New Cross-Discipline Coupling

Both commands are dispatched against a `"ManufacturingOperation"`/
`"Inspection"` target through the identical `ICommandDispatcher`/
`ICommandHandler<T>` mechanism every command in this platform already
uses — neither handler's own implementation was touched, extended, or
special-cased for Manufacturing in any way; confirmed by direct
inspection (`Mechanical/SetBomLineCommand.cs`,
`Verification/RecordVerificationResultCommand.cs`, both byte-for-byte
unchanged from their own pre-`WP 9.5A` shape) and proven empirically by
dedicated tests.

## 5. `Classification`-Tagged `ManufacturingOperation` — Verified Against `ADR-0088`'s Own Precedent

`ManufacturingCategory.Of`/`ManufacturingObjectFactoryRegistry`'s own
three `Classification` constants are verified, by direct inspection, to
follow `ADR-0088`'s own identical shape exactly: free-text, unvalidated,
read only by this Work Package's own Workspace-layer categorisation
code — never a Domain-layer enum, never validated at write time,
identical open-string precedent `RelationshipCategory`/
`RelationshipKindCategoryMap` already establish platform-wide
(`ADR-0076`).

## 6. `ManufacturingOperation` Facet Casting — Verified Against `ADR-0080`'s Own Composition Rule

Every cast this Work Package performs (`target is IHasLifecycle`, `is
IHasRevisions`, `is IRenamable`, `is IHasParent`, `is IDeletable`, `is
IHasRelationships`, `is IHasBomLine`, `is IHasMetadata`) is to a facet
`IManufacturingOperation` does **not** itself separately re-declare
beyond what it already composes — confirmed directly against
`Contracts/TestManufacturing.cs`'s own frozen shape. Every successful
cast succeeds only because the concrete `ManufacturingOperation` class
derives from `EngineeringObjectBase`, which implements every facet
unconditionally (`ADR-0075`'s own composition rule governing contracts)
— identical to every prior discipline's own already-verified reliance
on the same mechanism.

## 7. API Stability Classification

| Member | Classification | Rationale |
|---|---|---|
| `ManufacturingCategory`, eight Manufacturing commands, `ManufacturingNodeProvider`/`ManufacturingWorkspaceView(Factory)`/`ManufacturingOperationPropertyFacetProvider`/`ManufacturingObjectFactoryRegistry`/`ManufacturingWorkspaceRegistration` | **Internal** | `Tempest.App`-only implementation detail, not a published contract surface |
| `EngineeringManufacturingWorkspaceSampleModule`/`ManufacturingWorkspaceExplorerModule` | **Internal** | `Tempest.Samples` reference/representative content, not a published contract surface |
| `IManufacturingOperation`/`ManufacturingOperation`/`IWorkInstruction`/`WorkInstruction`/`IInspection`/`Inspection`/`EngineeringObjectBase` | **Stable, unchanged** | Confirmed byte-for-byte identical to their own pre-`WP 9.5A` shape — zero Domain-layer edits anywhere in this Work Package |
| `DocumentObjectFactoryRegistry`/`DocumentsNodeProvider`'s pre-existing public surface | **Stable, additive only** | Three new `string` constants/category labels added; every pre-existing member/behaviour is untouched, confirmed by the full, unmodified `WP 9.4A` test suite passing unchanged |
| `EngineeringCockpit`'s pre-existing public surface (`RequirementsStatus`, `CalculationStatus`, `DocumentationStatus`, `VerificationStatus`, `KpiCards`, etc.) | **Stable, unchanged** | `ManufacturingStatus`/`ManufacturingKpiCards` are both wholly new, additive members — no existing placeholder property is replaced this time, unlike every prior discipline's own "reused slot" pattern; every prior member is untouched, confirmed by the full, unmodified suites passing unchanged |

## 8. Overall Verdict

**Fully conformant.** Every new dependency edge either already existed
in shape elsewhere in the platform, is the one disclosed,
verified-safe further instance of `WP 9.4A`'s/`WP 9.3A`'s own
already-established cross-sample-module precedent, or is the one
disclosed, verified-safe, verified-one-directional new intra-`Tempest.App`
namespace dependency this Work Package introduces (§2). Zero
Domain-layer (`Tempest.Core`) files were edited by this Work Package —
the entire implementation is additive at the Workspace (`Tempest.App`)
and representative-data (`Tempest.Samples`) layers, the strongest
possible conformance signal against "no architectural redesign, no
contract redesign." The one deliberately absent dependency edge
(`EngineeringVerificationWorkspaceSampleModule`) was checked and
correctly omitted, not merely unconsidered — confirmed by direct
ordinal-Id comparison before any code was written.

## Related Documents

`ADR-0067`; `ADR-0075`; `ADR-0076`; `ADR-0080`; `ADR-0088`; `ADR-0091`;
`WP9.0A Architecture Conformance Review.md`; `WP9.4A Architecture
Conformance Review.md`; `WP9.3A Architecture Conformance Review.md`;
`WP8.0B Dependency Rules.md`; `WP8.2B Dependency Rules.md`.

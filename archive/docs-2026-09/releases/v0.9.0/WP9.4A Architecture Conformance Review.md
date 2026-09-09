# WP 9.4A — Engineering Documents Workspace — Architecture Conformance Review

## Purpose

Independently re-verifies that every new or changed piece of this Work
Package sits in its own correct architectural layer, introduces no
circular dependency, and follows the frozen Dependency Rules exactly
where each already applies.

## 1. Layering

| Component | Layer | Depends on | Verdict |
|---|---|---|---|
| `DocumentObjectFactoryRegistry`/`DocumentsNodeProvider`/`DocumentCategory`/`DocumentsWorkspaceView(Factory)`/`DocumentsPropertyFacetProvider` | `Tempest.App.Workspace.Documents` | `Tempest.Core.EngineeringDomain` directly (the Engineering Discipline integration layer, per `WP 9.0A`'s own precedent) | Conforms |
| Nine Documents commands + `DocumentsWorkspaceRegistration` | `Tempest.App.Workspace.Documents` | `Tempest.Core.EngineeringDomain`/`Tempest.Core.Commands` | Conforms |
| `EngineeringCockpit` (extended) | `Tempest.App.Workspace` | `EngineeringDomainContext` (already an existing constructor dependency — zero new dependency added, identical to `WP 9.2A`'s own zero-new-dependency finding) | Conforms |
| `EngineeringDocumentsWorkspaceSampleModule` | `Tempest.Samples` | `IIdentityService`/`EngineeringDomainContext`/`MechanicalProductStructureSampleModule`/`RequirementsWorkspaceSampleModule`/`EngineeringCalculationsWorkspaceSampleModule` | Conforms — see §2 for the cross-sample-module dependency edges |
| `DocumentsWorkspaceExplorerModule` | `Tempest.Samples` | `INavigationProvider` only | Conforms — identical shape to `CalculationsWorkspaceExplorerModule`/`RequirementsWorkspaceExplorerModule`/`MechanicalWorkspaceExplorerModule` |

No new project reference was added anywhere. `Tempest.App.Workspace.Documents`
references only `Tempest.Core.EngineeringDomain`/`Tempest.Core.Commands`
— unlike `Tempest.App.Workspace.Calculations` (which references
`Tempest.Samples` for representative Template types),
`Tempest.App.Workspace.Documents` needs no `Tempest.Samples` reference
at all, since it introduces no synthetic, registry-backed Kind of its
own.

## 2. Circular Dependency Analysis

None introduced. **Three cross-sample-module dependency edges (one
constructor-injected trio, extending `WP 9.2A`'s own two-dependency
precedent by one) plus one further, disclosed query-not-inject edge:**
`EngineeringDocumentsWorkspaceSampleModule` constructor-injects
`MechanicalProductStructureSampleModule`, `RequirementsWorkspaceSampleModule`,
and `EngineeringCalculationsWorkspaceSampleModule`. Verified safe by the
identical mechanism `WP 9.1A`/`WP 9.2A`'s own Architecture Conformance
Reviews already verified — `ModuleServiceCollectionExtensions.AddDiscoveredModules`
registers every discovered module type as a DI singleton, and
`ModuleLifecycleManager` initialises modules in ordinal Id order.
`tempest.samples.mechanicalproductstructure`, then
`tempest.samples.requirementsworkspace`, then
`tempest.samples.workspacecalculations`, then this module's own
`tempest.samples.workspacedocuments` sort in exactly that order (`m` <
`r` < `w`, then `workspacecalculations` < `workspacedocuments`
ordinally, `c` < `d`) — confirmed directly by inspecting the four
literal Id strings, not merely assumed.

A fourth, disclosed edge is a **query, not a constructor injection**:
`EngineeringDocumentsWorkspaceSampleModule` reads
`_context.Repository.ListByKindAsync("Risk")` at runtime to find the
base `EngineeringDomainSampleModule`'s own already-created live Risk
object, rather than constructor-injecting `EngineeringDomainSampleModule`
itself. Verified safe by the same ordinal-ordering guarantee —
`tempest.samples.engineeringdomain` sorts before all four modules named
above (`e` < `m`/`r`/`w`) — and additionally robust to
`EngineeringDomainSampleModule` not being discovered at all in a given
host composition (the query degrades to "no Risk found," a `null`-checked,
non-throwing path, never a hard DI-resolution failure) — a deliberately
looser coupling than the constructor-injected trio, chosen because the
Risk object itself, not the module that created it, is the only thing
this Work Package actually needs.

All four edges are one-directional; a host that discovers
`EngineeringDocumentsWorkspaceSampleModule` without also discovering the
three constructor-injected dependencies fails DI resolution immediately
(`ServiceNotRegisteredException`), never silently — confirmed directly
by `DocumentsWorkspaceIntegrationTests`'s own explicit module list.

## 3. Extension-Point Conformance

`ProjectExplorer.FilterAsync`, `IWorkspaceManager.RegisterExplorerArea`/
`RegisterView`/`RegisterFacetProvider`, and `ICommandDispatcher.RegisterHandler`/
`ICommandRegistry.RegisterDescriptor` are all consumed exactly as their
own `WP8.0B`/`WP8.1B`/`WP 9.0A`–`WP 9.2A` precedent already established —
verified by direct comparison of `DocumentsWorkspaceRegistration.Register`
against `CalculationsWorkspaceRegistration.Register`/
`MechanicalWorkspaceRegistration.Register`/`RequirementsWorkspaceRegistration.Register`,
confirming an identical call shape for every shared extension point,
including the three-Kind-per-provider loop
`RequirementsWorkspaceRegistration` already established.

## 4. `DocumentCategory` — Verified to Introduce No New Domain Concept

`DocumentCategory.Of` is a pure, `Tempest.App`-only, static mapping
function — never referenced by `Tempest.Core` or `Tempest.Samples`, and
holding no state of its own (unlike `CalculationTemplateRegistry`, which
holds a live, mutable registration map, `DocumentCategory` computes its
own result fresh from each object's own already-real `Kind`/
`Classification` on every call). Confirmed to introduce no persistence,
no caching, and no new Domain-layer concept — the nine category labels
exist only as Explorer-tree display grouping, never written back to any
Document's own state.

## 5. `Document`/`Drawing`/`CadModel` Facet Casting — Verified Against `ADR-0080`'s Own Composition Rule

Every cast this Work Package performs (`target is IHasLifecycle`, `is
IHasRevisions`, `is IRenamable`, `is IHasParent`, `is IDeletable`, `is
IHasAttachments`) is to a facet `IDocument`/`IDrawing`/`ICadModel` do
**not** themselves separately re-declare beyond what `IDocument` already
composes — confirmed directly against `Contracts/DocumentationDesign.cs`'s
own frozen shape (`IDocument : IEngineeringObject, IHasBusinessIdentifier,
IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships,
IHasAttachments`). Every successful cast succeeds only because the
concrete `Document`/`Drawing`/`CadModel` classes derive from
`EngineeringObjectBase`, which implements every facet unconditionally
(`ADR-0075`'s own composition rule governing contracts) — identical to
`CalculationsPropertyFacetProvider`'s own already-verified reliance on
the same mechanism. No Domain contract was reopened to make any of these
casts succeed.

## 6. API Stability Classification

| Member | Classification | Rationale |
|---|---|---|
| `DocumentCategory`, nine Documents commands, `DocumentsNodeProvider`/`DocumentsWorkspaceView(Factory)`/`DocumentsPropertyFacetProvider`/`DocumentObjectFactoryRegistry`/`DocumentsWorkspaceRegistration` | **Internal** | `Tempest.App`-only implementation detail, not a published contract surface |
| `EngineeringDocumentsWorkspaceSampleModule`/`DocumentsWorkspaceExplorerModule` | **Internal** | `Tempest.Samples` reference/representative content, not a published contract surface |
| `IDocument`/`IDrawing`/`ICadModel`/`Document`/`Drawing`/`CadModel`/`EngineeringObjectBase` | **Stable, unchanged** | Confirmed byte-for-byte identical to their own pre-`WP 9.4A` shape — zero Domain-layer edits anywhere in this Work Package |
| `EngineeringCockpit`'s pre-existing public surface (`RequirementsStatus`, `CalculationStatus`, etc.) | **Stable, unchanged** | `DocumentationStatus` is a real-implementation replacement of an existing, already-public placeholder property (not a signature change); `DocumentsKpiCards` is additive; every `WP 9.1A`/`WP 9.2A` member is untouched, confirmed by the full, unmodified suites passing unchanged |

## 7. Overall Verdict

**Fully conformant.** Every new dependency edge either already existed
in shape elsewhere in the platform, or is the one disclosed, verified-safe
further instance of `WP 9.1A`/`WP 9.2A`'s own already-established
cross-sample-module precedent, extended by a deliberately looser
query-based variant for the one edge that did not need a full
constructor dependency. Zero Domain-layer (`Tempest.Core`) files were
edited by this Work Package — the entire implementation is additive at
the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`)
layers, the strongest possible conformance signal against "no
architectural redesign, no contract redesign."

## Related Documents

`ADR-0067`; `ADR-0075`; `ADR-0080`; `ADR-0088`; `WP9.0A Architecture
Conformance Review.md`; `WP9.1A Architecture Conformance Review.md`;
`WP9.2A Architecture Conformance Review.md`; `WP8.0B Dependency
Rules.md`; `WP8.2B Dependency Rules.md`.

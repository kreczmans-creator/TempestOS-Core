# WP 9.2A — Engineering Calculations Workspace — Architecture Conformance Review

## Purpose

Independently re-verifies that every new or changed piece of this Work
Package sits in its own correct architectural layer, introduces no
circular dependency, and follows the frozen Dependency Rules exactly
where each already applies.

## 1. Layering

| Component | Layer | Depends on | Verdict |
|---|---|---|---|
| `EngineeringCalculationDefinitions.cs` (five `ICalculationDefinition`s) | `Tempest.Samples` | `Tempest.Core.Calculations`/`Tempest.Core.UnitsAndQuantities` (already-existing dependencies only) | Conforms — no new dependency added |
| `CalculationObjectFactoryRegistry`/`CalculationTemplateRegistry`/`CalculationRecordReader`/`CalculationsNodeProvider`/`CalculationsWorkspaceView(Factory)`/`CalculationsPropertyFacetProvider` | `Tempest.App.Workspace.Calculations` | `Tempest.Core.EngineeringDomain`/`Tempest.Core.Calculations` directly (the Engineering Discipline integration layer, per `WP 9.0A`'s own precedent) | Conforms |
| Ten Calculations commands + `CalculationsWorkspaceRegistration` | `Tempest.App.Workspace.Calculations` | `Tempest.Core.EngineeringDomain`/`Tempest.Core.Calculations`/`Tempest.Core.Commands` | Conforms |
| `EngineeringCockpit` (extended) | `Tempest.App.Workspace` | `EngineeringDomainContext` (already an existing constructor dependency — zero new dependency added, unlike `WP 9.1A`'s own two new Requirements service dependencies) | Conforms |
| `EngineeringCalculationsWorkspaceSampleModule` | `Tempest.Samples` | `IIdentityService`/`EngineeringDomainContext`/`ICalculationEngine`/`IRequirementsService`/`MechanicalProductStructureSampleModule`/`RequirementsWorkspaceSampleModule` | Conforms — see §2 for the one further cross-sample-module dependency edge |
| `CalculationsWorkspaceExplorerModule` | `Tempest.Samples` | `INavigationProvider` only | Conforms — identical shape to `RequirementsWorkspaceExplorerModule`/`MechanicalWorkspaceExplorerModule` |

No new project reference was added anywhere. `Tempest.App.Workspace.Calculations`
references `Tempest.Samples` (for the five representative Templates'
concrete `TInput`/`TResult` types, in `CalculationTemplateRegistry`'s own
registration call) — the same, already-established direction
`RequirementsWorkspaceRegistration`/`MechanicalWorkspaceRegistration`
already use; never the reverse.

## 2. Circular Dependency Analysis

None introduced. **One further cross-sample-module dependency edge,
disclosed directly, mirroring `WP 9.1A`'s own already-established first
one:** `EngineeringCalculationsWorkspaceSampleModule` constructor-injects
both `MechanicalProductStructureSampleModule` and
`RequirementsWorkspaceSampleModule`. Verified safe by the identical
mechanism `WP 9.1A`'s own Architecture Conformance Review already
verified — `ModuleServiceCollectionExtensions.AddDiscoveredModules`
registers every discovered module type as a DI singleton, and
`ModuleLifecycleManager` initialises modules in ordinal Id order.
`tempest.samples.mechanicalproductstructure`, then
`tempest.samples.requirementsworkspace`, then this module's own
`tempest.samples.workspacecalculations` sort in exactly that order (`m`
< `r` < `w`) — confirmed directly by inspecting the three literal Id
strings, not merely assumed. Both edges are one-directional; a host that
discovers this module without also discovering both dependencies fails
DI resolution immediately (`ServiceNotRegisteredException`), never
silently.

## 3. Extension-Point Conformance

`ProjectExplorer.FilterAsync`, `IWorkspaceManager.RegisterExplorerArea`/
`RegisterView`/`RegisterFacetProvider`, and `ICommandDispatcher.RegisterHandler`/
`ICommandRegistry.RegisterDescriptor` are all consumed exactly as their
own `WP8.0B`/`WP8.1B`/`WP 9.0A`/`WP 9.1A` precedent already established —
verified by direct comparison of `CalculationsWorkspaceRegistration.Register`
against `MechanicalWorkspaceRegistration.Register`/`RequirementsWorkspaceRegistration.Register`,
confirming an identical call shape for every shared extension point,
including the three-Kind-per-provider loop
`RequirementsWorkspaceRegistration` already established (here extended
to the synthetic `"CalculationTemplate"` Kind, which the loop treats
identically to a real Domain Kind — `RegisterView`/`RegisterFacetProvider`
require nothing more than a `string` Kind key).

## 4. `CalculationTemplateRegistry` — a Genuinely New Type, Verified Against `WP8.2B Dependency Rules.md` §8

`WP8.2B Dependency Rules.md` §8 proposes no Domain-level registry
contract, and `MechanicalObjectFactoryRegistry`'s own precedent (`WP 9.0A`)
already established that a Workspace-layer registry answering that same
need is conformant. `CalculationTemplateRegistry` is verified to be the
identical shape one further time: a plain, `Tempest.App`-only class,
never referenced by `Tempest.Core` or `Tempest.Samples`, holding no
Domain state of its own (every execution still durably recorded by the
unmodified `ICalculationEngine`/`IEngineeringDocumentStore`; the
registry's own state is purely a `CalculationId`→adapter map plus a
synthetic display Guid per Template — never persisted, rebuilt fresh on
every process start via `CalculationsWorkspaceRegistration.Register`).

## 5. `Calculation`/`CalculationSet` Facet Casting — Verified Against `ADR-0080`'s Own Composition Rule

Every cast this Work Package performs (`target is IHasLifecycle`,
`is IHasRevisions`, `is IRenamable`, `is IHasParent`, `is IDeletable`) is
to a facet `ICalculation`/`ICalculationSet` do **not** themselves declare
— confirmed directly against `Contracts/Calculations.cs`'s own frozen
shape (`ICalculation : IEngineeringObject, IHasBusinessIdentifier,
IHasMetadata`; `ICalculationSet` adds only `IHasRelationships`). Every
successful cast succeeds only because the concrete `Calculation`/
`CalculationSet` classes derive from `EngineeringObjectBase`, which
implements every facet unconditionally (`ADR-0075`'s own composition
rule governing contracts; `EngineeringObjectBase` implementing every
facet regardless is ordinary implementation reuse, orthogonal to it,
exactly as that class's own XML documentation states) — identical to
`MechanicalPropertyFacetProvider`'s own already-verified reliance on the
same mechanism. No Domain contract was reopened to make any of these
casts succeed.

## 6. API Stability Classification

| Member | Classification | Rationale |
|---|---|---|
| `CalculationTemplateRegistry`, `CalculationRecordReader`, `CalculationExecutionSummary`, `CalculationTemplateDescriptor`, `CalculationRecordSnapshot` | **Internal** | `Tempest.App`-only implementation detail, not a published contract surface |
| Ten Calculations commands, `CalculationsNodeProvider`/`CalculationsWorkspaceView(Factory)`/`CalculationsPropertyFacetProvider`/`CalculationObjectFactoryRegistry` | **Internal** | `Tempest.App`/`Tempest.Samples` implementation detail, not a published contract surface |
| Five `ICalculationDefinition` implementations, their `Input`/`Result` records | **Internal** | `Tempest.Samples` reference/representative content, not a published contract surface |
| `ICalculationEngine`/`ICalculationDefinition<TInput,TResult>`/`CalculationRecord<TResult>`/`ICalculation`/`ICalculationSet`/`EngineeringObjectBase` | **Stable, unchanged** | Confirmed byte-for-byte identical to their own pre-`WP 9.2A` shape — zero Domain-layer edits anywhere in this Work Package |
| `EngineeringCockpit`'s pre-existing public surface (`RequirementsStatus`, `RequirementsKpiCards`, etc.) | **Stable, unchanged** | `CalculationStatus`/`CalculationsKpiCards` are additive; every `WP 9.1A` member is untouched, confirmed by the full, unmodified `WP 9.1A` test suite passing unchanged |

## 7. Overall Verdict

**Fully conformant.** Every new dependency edge either already existed
in shape elsewhere in the platform, or is the one disclosed, verified-safe
further instance of `WP 9.1A`'s own already-established cross-sample-
module precedent. Zero Domain-layer (`Tempest.Core`) files were edited by
this Work Package — the entire implementation is additive at the
Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`)
layers, the strongest possible conformance signal against "no
architectural redesign, no contract redesign."

## Related Documents

`ADR-0067`; `ADR-0075`; `ADR-0080`; `ADR-0086`; `ADR-0087`; `WP9.0A
Architecture Conformance Review.md`; `WP9.1A Architecture Conformance
Review.md`; `WP8.0B Dependency Rules.md`; `WP8.2B Dependency Rules.md`.

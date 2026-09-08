# WP 9.0A — Mechanical Product Structure — Implementation Report

## Status

Complete. `v0.9.0` ("Mechanical Foundation")'s own first Work Package —
the first Work Package to deliver a real Engineering Discipline into the
real Engineering Workspace, on `feature/v0.9.0-mechanical-foundation`,
cut from `main` at the `v0.8.0` tag.

## What Was Implemented

**Domain layer** (`Tempest.Core.EngineeringDomain`) — three new, additive
facet interfaces (`IRenamable`, `IHasParent`, `IDeletable`,
`Contracts/StructuralMutation.cs`), composed into `IProject`/`IAssembly`/
`ISubAssembly`/`IPart`/`IComponent` (already-frozen, already-`WP8.2C`-
implemented Kinds — no new concrete classes were needed, since these six
Kinds already existed). `EngineeringObjectBase` implements all three
unconditionally, mirroring how it already implements every other facet.
Two new exceptions (`CircularParentAssignmentException`,
`EngineeringObjectHasChildrenException`) and two new validation rule
codes (`TEMPEST-VAL-006`, `TEMPEST-VAL-007` — renumbered from an
initially-assigned `-002`/`-003` by `WP 9.0B`, which found these
collided with `IReferenceIntegrityChecker`'s own pre-existing,
already-shipped use of those exact codes since `WP 8.2C`; corrected
before this Work Package's own work was ever committed). See
`ADR-0080`/`ADR-0081`.

**Workspace layer** (`Tempest.App.Workspace`) — the first real Kind-keyed
provider registrations against the extension points `WP8.1B`/`WP8.1C`
built but never populated with real data: `MechanicalProductStructureNodeProvider`
(`IProjectExplorerNodeProvider`), `MechanicalWorkspaceViewFactory`/
`MechanicalWorkspaceView` (`IWorkspaceViewFactory`/`IWorkspaceView`), and
a new third provider category, `IPropertyFacetProvider`
(`MechanicalPropertyFacetProvider`), added to the frozen `IWorkspaceManager`
contract as `RegisterFacetProvider` (`ADR-0082`). Six `IWorkspaceCommand`/
`ICommand` implementations — Create, Rename, Delete, Move, Copy, Duplicate
— each with a registered `CommandDescriptor` (`category: "Mechanical"`),
closing `WP8.1B`'s own disclosed "no concrete `IWorkspaceCommand` is
implemented by this Work Package" gap. `EngineeringCockpit` gains three
narrow, real reads (`ProjectName`, `RecentProjects`, one `AttentionItems`
entry) in place of fixed placeholder text, honestly empty when no
Mechanical `Project` exists yet.

**Representative data** (`Tempest.Samples`) —
`MechanicalProductStructureSampleModule` builds a Project ("Falcon
Structural Assembly Project"), two top-level Assemblies, a three-level-
deep Sub-Assembly chain, five Parts (one soft-deleted, demonstrating
`IDeletable`), one Component referenced — not parented — from two Parts
across two different Assemblies (the shared-component/cross-reference
scenario, via the existing Relationship framework, never a second parent
pointer), and one `Configuration` baselining two objects. Exercises
`RenameAsync`/`MoveAsync`/`DeleteAsync` directly against real data during
seeding, and transitions one Assembly through `LifecycleState.Released`.
`MechanicalWorkspaceExplorerModule` registers the Mechanical area's own
`NavigationItem`, mirroring `WorkspaceExplorerSampleModule` exactly.
`MechanicalWorkspaceRegistration.Register` is the single composition-root
entry point `Program.cs` calls — necessarily *after* `WorkspaceShell.StartAsync`
rather than before, since (unlike the fixed sample content) it needs a
running Runtime Host to resolve `EngineeringDomainContext`/
`ICommandDispatcher`/`ICommandRegistry` — a disclosed first (see Systems
Engineering Review).

## Contract Fidelity

Every `WP8.2B`/`WP8.0B` frozen contract this Work Package touches was
extended, never redesigned: `IHasBusinessIdentifier.DisplayName`,
`IAssembly.ChildIds`, `ISubAssembly.ParentAssemblyId`, `ISelectionService`,
and all ten original facet interfaces are byte-for-byte unchanged.
`IWorkspaceManager` gained one new member (`RegisterFacetProvider`);
`EngineeringObjectBase`'s public shape grew, never shrank or changed. See
`ADR-0080`/`ADR-0081`/`ADR-0082` for the full reasoning behind each
deviation, and `WP9.0A Architecture Conformance Review.md` for
independent verification.

## Three New ADRs, Resolved

`ADR-0080` (structural mutation is three additive facets, not a reopened
contract), `ADR-0081` (`Move` records a new relationship link and a live
`ParentId`; frozen `ChildIds`/`ParentAssemblyId` stay snapshots),
`ADR-0082` (Property Inspector facet sourcing is a third Kind-keyed
provider category, added to `IWorkspaceManager`).

## Disclosed Findings

**Canonical Object Catalogue correction.** `WP8.2A Canonical Object
Catalogue.md` still marks `Project`/`Assembly`/`SubAssembly`/`Part`/
`Component`/`Configuration` "Conceptual" — stale since `WP8.2C` gave all
six real, tested concrete classes. Left exactly as originally written in
that dated historical artefact, per "never silently modify historical
records"; the correction is recorded here and in the Documentation
Register, not applied in place.

**Pre-existing Runtime Host timing characteristic (`TD-26`).** Manual
console verification found `WorkspaceManager.StartAsync`'s own
`WaitForServicesAsync` returns as soon as `ITempestHost.Services` is
non-null — which `TempestHost.cs` sets *before* running module
`InitialiseAsync` — so a Workspace read immediately after `StartAsync`
can occur before module-registered data (a `NavigationItem`, a seeded
`Project`) exists. Confirmed **pre-existing on the unmodified `v0.8.0`
tag** (reproduced via a disposable `git worktree` comparison, not merely
suspected) — not introduced by this Work Package. Every automated test
in this Work Package's own suite explicitly awaits the seeded module's
own `HasRegistered` flag before asserting, and is unaffected. See `WP9.0A
Technical Debt Assessment.md` for the full disclosure; not fixed here —
a Runtime Host/`WorkspaceManager` concern, out of this Work Package's own
scope.

## Engineering Core Integration

Reuses, unmodified: `EngineeringObjectFactory<T>` (`ADR-0079`, Create),
`IHasRelationships.LinkAsync` (Move's own history, Copy/Duplicate's
target-parent reuse), `IEngineeringObjectRepository` (every read), the
existing `Configuration`/`IConfiguration` shape (baseline display), and
the existing Command Framework (`ICommandDispatcher`/`ICommandRegistry`,
unchanged). Zero new Platform Services; zero new persistence mechanism;
zero duplication of `IEngineeringDocumentStore`.

## Testing

64 new tests (1631 → 1695): 13 Domain (`StructuralMutationTests`), 6
`WorkspaceManager.RegisterFacetProvider`, 17 node-provider/facet-provider/
view (`MechanicalNodeProviderAndFacetsTests`), 21 commands
(`MechanicalCommandsTests`), 7 full Workspace integration
(`MechanicalWorkspaceIntegrationTests`, real `ITempestHost` +
`WorkspaceManager` + seeded module + real Command Framework, exercising
Create → Rename → Move → Copy → Duplicate → Delete end to end). Three
pre-existing tests updated for real, no-longer-placeholder behaviour
(`ClockModuleDiscoveryTests` module count 22→24;
`EngineeringCockpitTests.ProjectName`/`RecentProjects`, now honest-empty-
state assertions). 1695/1695 passing, zero failures, four full clean
rebuild-and-test runs (two Debug, two Release), both via per-project
paths and `src/TempestOS.slnx`.

## Platform Integration Demonstrated

A real console session (`dotnet run --project src/Tempest.App`) starts,
loads all 24 discovered modules (22 prior + this Work Package's two new
ones) including the Mechanical Product Structure sample data, and shuts
down cleanly with no unhandled exceptions.

## Repository Metrics

12 new files under `src/Tempest.App/Workspace/Mechanical/`; 1 new file
under `src/Tempest.Core/EngineeringDomain/Contracts/`; 2 new files under
`src/Samples/Tempest.Samples/`; 1 new file,
`src/Tempest.App/Workspace/IPropertyFacetProvider.cs`; 5 existing source
files edited (`EngineeringObjectBase.cs`, `EngineeringDomainException.cs`,
`ProgrammeHierarchy.cs`, `PhysicalConfiguration.cs`, `Validation.cs` in
`Tempest.Core`; `IWorkspaceManager.cs`, `WorkspaceManager.cs`,
`PropertyInspector.cs`, `EngineeringCockpit.cs`, `Program.cs` in
`Tempest.App`); 5 new test files, 3 existing test files edited; 3 new
ADRs.

## Related Documents

`ADR-0080`; `ADR-0081`; `ADR-0082`; `WP9.0A Engineering Review Report.md`;
`WP9.0A Security Review Report.md`; `WP9.0A Systems Engineering
Review.md`; `WP9.0A Architecture Conformance Review.md`; `WP9.0A
Technical Debt Assessment.md`; `WP9.0A Future Capability Assessment.md`;
`WP9.0A Lessons Learned.md`; `WP8.2C Engineering Domain Implementation
Report.md`; `WP8.1B Implementation Report.md`; `WP8.1C Implementation
Report.md`.

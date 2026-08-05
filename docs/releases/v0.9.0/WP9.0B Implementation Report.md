# WP 9.0B — Product Configuration & BOM Management — Implementation Report

## Status

Complete. `v0.9.0` ("Mechanical Foundation")'s own second Work Package —
Bill of Materials and Configuration Management over the Mechanical
Product Structure `WP 9.0A` delivered.

## What Was Implemented

**Domain layer** (`Tempest.Core.EngineeringDomain`) — one new, additive
facet, `IHasBomLine` (`Quantity`, `UnitOfMeasure`, `FindNumber`,
`ItemNumber`, `ReferenceDesignator`, `SetBomLineAsync`), composed into
`IAssembly`/`IPart`/`IComponent` (`Contracts/BillOfMaterials.cs`;
`ADR-0083`). Five new `IValidationRule` implementations
(`Implementation/BillOfMaterialsValidationRules.cs`) — duplicate Item/
Find Number, non-positive quantity, missing parent, circular hierarchy —
registered through `ValidationRuleSet.Register`, the extension point
that type's own XML documentation already anticipated and no prior Work
Package had used. `IReferenceIntegrityChecker.CheckBaselineMembersAsync`
(`WP8.2C`, already real, never previously called outside a test) is now
wired into the Workspace for the first time. `Configuration`/`Baseline`/
`Release` (`Baseline : Configuration`, `Release : Baseline`, all
`WP8.2C`) needed no new code at all — "working" vs. "released" is
already fully expressible through `IHasLifecycle.Status`.

**Workspace layer** (`Tempest.App.Workspace.Mechanical`) —
`MechanicalObjectFactoryRegistry` extended from five Kinds to eight
(`Configuration`/`Baseline`/`Release` added); `MechanicalPropertyFacetProvider`
surfaces all five new BOM facets plus a Configuration Members count;
`MechanicalProductStructureNodeProvider` renders BOM-aware node titles
(`"0010 ×4 Wing Skin Panel"`) and orders a fully-Item-Numbered sibling
group numerically — the existing Product Structure tree *is* the BOM
hierarchy, never a second, competing one. Three new commands —
`SetBomLineCommand`, `CompareBaselinesCommand` (plain `ICommand`, diffs
two `IConfiguration.MemberRevisions` lists — added/removed/revision-
changed), `ValidateConfigurationCommand` (wraps
`CheckBaselineMembersAsync`) — bringing the Mechanical discipline's own
command count from six to nine.

**Representative data** (`MechanicalProductStructureSampleModule`,
extended in place, not a new module) — every existing Part/Sub-Assembly/
Component now carries a real BOM line; a plain working `Configuration`;
an `Baseline` frozen and taken to Approved; a `Release` with a larger,
later member set (one added Part, one revision-changed Part) taken all
the way to `Released` — comparing the Baseline against the Release
(`CompareBaselinesCommand`) shows a real, non-trivial diff. "Product
variants" remain placeholder architecture only, exactly as this Work
Package's own controlling instruction specifies — see "Product Variant
Placeholder Architecture," below; no code was written for it.

## Product Variant Placeholder Architecture (design note, no code)

A future `IProductVariant` concept would compose alongside
`IHasBomLine`/`IConfiguration` rather than replacing either: a Variant
would be a named axis (for example, "Left-Hand"/"Right-Hand") whose own
resolution selects between alternate BOM lines for the same logical
position — most naturally represented as an optional `VariantId` on a
future, richer BOM line shape, filtered at read time by the Workspace
layer, never a new parallel structural tree. Recorded as `FCR-0044`
(Future Capability Register); no interface, class, or test exists for
this today.

## Two Disclosed, Pre-Existing-Code Findings — Both Fixed Before Any Commit

**`TEMPEST-VAL` code collision.** `WP 9.0A`'s own `NoCircularParent`/
`NoDeleteWithLiveChildren` were assigned `-002`/`-003` — already in use
by `IReferenceIntegrityChecker.CheckAsync` (`WP8.2C`, relationship
source/target existence). Renumbered to `-006`/`-007`; `-004`/`-005`
(baseline member checks, already shipped, never catalogued) are now
named constants too. All four `WP 9.0A` documentation references
corrected to match. Neither code had ever been referenced by a commit or
tagged release, so this is a pre-commit correction, not a rewrite of a
historical record.

**`ReviseAsync` silently discarded structural/BOM state.** Found while
building the representative data's own revision example:
`EngineeringObjectBase.ReviseAsync`'s `_selfFactory` closure only ever
knew the values passed to the *original* `EngineeringObjectFactory<T>`
call — a revised instance reverted to construction-time
`DisplayName`/`ParentId`/`IsDeleted`, and would have reverted
`Quantity`/`UnitOfMeasure`/etc. too, the moment any of `WP 9.0A`'s or
this Work Package's own mutators had been used before a revision. Fixed
by copying every mutable structural field from the pre-revision instance
onto the freshly-constructed one, inside `ReviseAsync` itself — `IHasRevisions`'s
own contract shape is unchanged; only this base class's own previously-
incomplete implementation of it is corrected. Four new regression tests
(`StructuralMutationTests.cs`) plus one more (`BillOfMaterialsTests.cs`)
prove it directly; the representative data's own revised Spar Web Plate
proves it a second time, end to end.

## New ADR

`ADR-0083` — `IHasBomLine` is a fourth additive facet (mirroring
`ADR-0080`'s own pattern a fourth time); Unit of Measure is a plain
string, deliberately never `Tempest.Core.UnitsAndQuantities.Quantity<TDimension>`.

## Engineering Core Integration

Reuses, unmodified: `ValidationRuleSet.Register` (its own first real
caller); `IReferenceIntegrityChecker.CheckBaselineMembersAsync` (its own
first real caller); `Configuration`/`Baseline`/`Release` (`WP8.2C`,
zero new code); `EngineeringObjectFactory<T>` (three more Kind branches
in the existing registry, no new pattern). Zero new Platform Services;
zero new persistence mechanism; zero duplication of any existing
framework.

## Testing

43 new tests (1695 → 1738): 16 Domain BOM (`BillOfMaterialsTests`,
including the `ReviseAsync` BOM-preservation regression), 4 more
`ReviseAsync` regression tests in `StructuralMutationTests`, 10 for the
three new commands (`MechanicalCommandsTests`), 7 for the extended node/
facet providers (`MechanicalNodeProviderAndFacetsTests`), 6 more full
Workspace integration tests against the real seeded Baseline/Release/BOM
data (`MechanicalWorkspaceIntegrationTests`). One pre-existing `WP 9.0A`
test corrected for the new command count (6 → 9). One flaky test found
and fixed during verification — see Technical Debt Assessment (`TD-27`).
1738/1738 passing, zero failures, six full clean-rebuild-and-test runs
across this Work Package's own verification (two Debug, two Release via
`src/TempestOS.slnx`, plus two ad hoc full-suite reruns while chasing the
flake), all clean.

## Repository Metrics

3 new files under `src/Tempest.App/Workspace/Mechanical/`
(`SetBomLineCommand.cs`, `CompareBaselinesCommand.cs`,
`ValidateConfigurationCommand.cs`); 2 new files under
`src/Tempest.Core/EngineeringDomain/` (`Contracts/BillOfMaterials.cs`,
`Implementation/BillOfMaterialsValidationRules.cs`); 10 existing source
files edited (`EngineeringObjectBase.cs`, `Validation.cs`,
`ReferenceIntegrityChecker.cs`, `PhysicalConfiguration.cs` in
`Tempest.Core`; `MechanicalObjectFactoryRegistry.cs`,
`MechanicalPropertyFacetProvider.cs`,
`MechanicalProductStructureNodeProvider.cs`,
`MechanicalWorkspaceRegistration.cs`, `CreateMechanicalObjectCommand.cs`,
`CopyMechanicalObjectCommand.cs`, `Program.cs` in `Tempest.App`;
`MechanicalProductStructureSampleModule.cs` in `Tempest.Samples`); 3 new
test files' worth of coverage spread across 5 files (2 new, 3 extended);
1 new ADR.

## Related Documents

`ADR-0083`; `WP9.0B Engineering Review Report.md`; `WP9.0B Security
Review Report.md`; `WP9.0B Systems Engineering Review.md`; `WP9.0B
Architecture Conformance Review.md`; `WP9.0B Technical Debt
Assessment.md`; `WP9.0B Future Capability Assessment.md`; `WP9.0B
Lessons Learned.md`; `WP9.0A Implementation Report.md`.

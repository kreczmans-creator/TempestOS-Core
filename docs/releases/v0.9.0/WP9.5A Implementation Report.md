# WP 9.5A — Manufacturing Workspace — Implementation Report

## Status

Complete. `v0.9.0` ("Mechanical Foundation")'s own seventh Work Package
by completion order (fifth by intended number) — the complete
Manufacturing Workspace experience, integrated into the Engineering
Workspace, Engineering Cockpit, and Digital Thread, using exclusively
the already-real Engineering Domain, Workspace, and Digital Thread. The
sixth real Engineering discipline wired into the Workspace, after
Mechanical (`WP 9.0A`/`WP 9.0B`), Requirements (`WP 9.1A`), Calculations
(`WP 9.2A`), Documents (`WP 9.4A`), and Verification (`WP 9.3A`).

## Disclosed Sequencing Note

This Work Package's own controlling instruction closes with "await
Product Owner instruction before `WP 9.9.0` Release Preparation" —
explicitly skipping `WP 9.6A` through `WP 9.8A`, none of which is named
or reserved anywhere in this repository. Recorded here as a plain
observation, not an inconsistency requiring correction: the Product
Owner is free to sequence Work Packages as instructed, and no prior
governance record commits this repository to a `WP 9.6A`–`WP 9.8A`
range existing at all. `PROJECT_STATUS.md`'s own Near-Term Roadmap
records this explicitly.

## What Was Implemented

**Domain layer — zero changes**, the fourth consecutive real-discipline
Work Package needing none, after `WP 9.2A`, `WP 9.4A`, and `WP 9.3A`.
`ManufacturingOperation`, `WorkInstruction`, and `Inspection`
(`Contracts/TestManufacturing.cs`/`Implementation/TestManufacturing.cs`,
`WP 8.2C`) are ordinary `EngineeringObjectBase`/`Document`/
`VerificationActivity`-derived concrete classes, confirmed by direct
repository-wide search to have been instantiated by no sample module or
test anywhere before this Work Package — the identical clean starting
point every prior real discipline began from. `Test` (also a real,
compiled `VerificationActivity` subtype, `WP 8.2C`) is deliberately
never constructed here — this Work Package's own scope names "Inspection
Operations," never "Test Operations" (see Technical Debt Assessment).

**Nine of the thirteen named scope items needed zero new Domain
representation, only reuse — verified, not assumed:**

- **Manufacturing BOM already worked before this Work Package wrote a
  single line of code.** `IHasBomLine` (`WP 9.0B`) is already
  unconditionally implemented by every `EngineeringObjectBase`-derived
  object, including `ManufacturingOperation`.
  `Mechanical.SetBomLineCommand`/`Handler` (`WP 9.0B`) is already fully
  Kind-agnostic — confirmed by direct read, and proven empirically by a
  dedicated integration test dispatching it against a live
  `"ManufacturingOperation"` and reading the result back through
  `ManufacturingOperationPropertyFacetProvider`
  (`SetBomLineCommand_AgainstALiveManufacturingOperation_UpdatesTheRealFacet_ProvingZeroNewCode`).
- **Manufacturing Assemblies/Parts** reuse `Assembly`/`SubAssembly`/
  `Part`/`Component` (`WP 9.0A`) directly — a `ManufacturingOperation`'s
  own required `PartId` plus a real `"references"` link connect it to
  the same Part, never a new Mechanical concept.
- **Manufacturing Resources/Tooling/Fixtures** are realised as plain
  `"Document"` objects with three new `Classification` values
  (`"Resource"`/`"Tooling"`/`"Fixture"`), extending
  `DocumentObjectFactoryRegistry`'s own already-open, unvalidated
  taxonomy (`ADR-0088`) — never a new Domain type.
  `DocumentsNodeProvider`/`DocumentCategory` (`WP 9.4A`) gain three more
  category labels, the identical extension mechanism that taxonomy was
  designed for.
- **Supplier Operations** are a `ManufacturingOperation` with
  `Classification = "Supplier Operation"`, linked to a real `Supplier`
  via the already-mapped `"manufacturedBy"` relationship kind — the
  identical direction the base sample's own `PurchaseItem
  --manufacturedBy--> Supplier` already establishes.
- **Manufacturing Readiness/Production Status** are Cockpit-only
  concepts, computed live from the real `ManufacturingOperation`/
  `Inspection` graph — no Domain representation needed.

**Routings — the one item needing a genuine, disclosed design decision
(`ADR-0091`).** A `ManufacturingOperation` with `Classification =
"Routing"` is a sequence container; its own real `IHasParent` children
(plain `ManufacturingOperation`s, `Classification = "Operation"`) are
the Routing's own steps, ordered via the existing
`IHasBomLine.ItemNumber` field — reusing
`MechanicalProductStructureNodeProvider.OrderForBom`'s own already-
established "ItemNumber as sibling sequence" convention.

**Significant, disclosed cross-Work-Package reuse on the read side — a
first for this project.** `"Inspection"` objects reuse
`Tempest.App.Workspace.Verification.VerificationActivityPropertyFacetProvider`/
`VerificationActivityWorkspaceView(Factory)` **directly**, constructed
with `kind: "Inspection"`; `"WorkInstruction"` objects reuse
`Tempest.App.Workspace.Documents.DocumentsPropertyFacetProvider`/
`DocumentsWorkspaceView(Factory)` **directly**, constructed with
`kind: "WorkInstruction"` — both types were already generic over their
own `Kind` parameter, confirmed by direct read; zero new facet/view code
was written for either. Recording an Inspection's own result reuses
`Verification.RecordVerificationResultCommand`/`Handler` **directly** —
already Kind-agnostic, dispatching through
`IVerificationService.RecordAsync` by Id alone.
`ManufacturingWorkspaceRegistration` deliberately does **not**
re-register that command handler — it dispatches through the handler
`VerificationWorkspaceRegistration` already registered, and must
therefore run after it in `Program.cs`.

**Commands remain this Work Package's own**, never reused from
Documents/Verification, mirroring every prior Work Package's own
established pattern of a fresh, thin command set per discipline — a
deliberate asymmetry (read-side reuse, write-side fresh commands):
reused commands would show a `"Documents"`/`"Verification"` Command
Palette category for a Manufacturing object, and
`DocumentObjectFactoryRegistry`'s/`VerificationActivityFactoryRegistry`'s
own Create machinery cannot construct a `"WorkInstruction"`/
`"Inspection"` at all — both require Manufacturing-specific fields
(`ManufacturingOperationId`/`SubjectId`) their own factories never
accept.

**Workspace layer** (`Tempest.App.Workspace.Manufacturing`, new
namespace) — `ManufacturingObjectFactoryRegistry` (three Create methods,
wrapping three separate `EngineeringObjectFactory<T>` instances —
`ManufacturingOperation`/`WorkInstruction`/`Inspection`); eight commands
(Create/Rename/Revise/Delete/Move/Copy/Duplicate/SetStatus —
"Release"/"Archive" descriptor names map directly onto
`LifecycleState.Released`/`.Archived`, no aliasing trick needed,
mirroring `WP 9.4A`'s own identical "already matches 1:1" finding);
`ManufacturingNodeProvider` (root = one category node per
`ManufacturingCategory` label — Routings/Operations/Supplier
Operations/Work Instructions/Inspections — mirroring
`DocumentsNodeProvider`'s own category precedent, listing all three
Manufacturing Kinds together in one tree); and
`ManufacturingWorkspaceRegistration` — the composition-root entry point,
registered from `Program.cs` after Verification's own registration, for
the reason above.

**Engineering Cockpit** (`Tempest.App.Workspace.EngineeringCockpit`) — a
genuinely new `ManufacturingStatus` derived `EngineeringHealthStatus`
(unlike `VerificationStatus`/`DocumentationStatus`, no `WP 8.1C`
placeholder slot existed to reuse); a new `ManufacturingKpiCards`
property supplying this Work Package's own named seven-card breakdown
(Manufacturing Objects/Manufacturing Readiness/Released Items/Open
Operations/Supplier Status/Inspection Status/Production Health);
`AttentionItems` and `OpenActions` each gain a real, conditional
Manufacturing entry. `KpiCards` itself gains no new entry — `WP 8.0C`
never named a `"Manufacturing"` placeholder row there to replace,
confirmed by direct read; this Work Package's own dedicated card set is
purely additive, unlike every prior discipline's own "replaces the
generic placeholder card" shape. Zero new constructor dependency was
needed — every read is sourced from the already-existing
`EngineeringDomainContext` and `VerificationRecordReader` (the identical
reader `WP 9.3A`'s own Verification KPIs already use, for the
Inspection Kind's own recorded results — never a new traversal).

**Representative data** (`EngineeringManufacturingWorkspaceSampleModule`,
new) — one real Routing (`Classification = "Routing"`) with three real,
sequenced Operation steps (`ItemNumber` "1"/"2"/"3"): step 1 verifies/
references the real Mechanical Wing Assembly (`InReview`), step 2
verifies/references the real Spar Web Plate and the real, already-
executed Beam Bending Stress Calculation (`Released`), step 3 verifies/
references the real Shared Fastener Component (left `Draft`, the
honest, un-started "Open" baseline). One Supplier Operation
(`manufacturedBy` the base sample's own real, already-live Supplier,
queried by Kind, never duplicated). One Tooling and one Fixture plain
`"Document"`. One Work Instruction, `documentedBy`-linked from the
Routing's own first step. One Inspection, `verifiedBy`-linked from the
same step, with a real, recorded `Pass` result via
`IVerificationService.RecordAsync` directly, referencing the Documents
sample's own real Test Report. The Routing itself `references` a real
Requirement — every named Digital Thread node this Work Package's own
scope lists (Requirements/Mechanical/Calculations/Verification/
Documents/Manufacturing) reached via already-mapped relationship kinds
only (`"references"`/`"manufacturedBy"`/`"documentedBy"`/`"verifiedBy"`)
— zero new kinds.
`EngineeringManufacturingWorkspaceSampleModule` is the platform's
thirty-fourth module (`tempest.samples.workspacemanufacturing`),
constructor-injecting `MechanicalProductStructureSampleModule`,
`RequirementsWorkspaceSampleModule`,
`EngineeringCalculationsWorkspaceSampleModule`, and
`EngineeringDocumentsWorkspaceSampleModule` directly — the same four
`EngineeringDocumentsWorkspaceSampleModule` itself already establishes.
Deliberately **not**
`EngineeringVerificationWorkspaceSampleModule` — see Disclosed Design
Decisions, below.

## Disclosed Design Decisions

**Routings/Operations/Supplier Operations are `Classification`-tagged
`ManufacturingOperation` objects, sequenced via the existing
`IHasBomLine.ItemNumber` field (`ADR-0091`).** No dedicated `Routing`
Domain Kind exists anywhere in the platform; introducing one would be
exactly the "contract redesign" this Work Package's own controlling
instruction forbids — the identical situation `ADR-0088` already faced
for Document classification.

**No constructor dependency on `EngineeringVerificationWorkspaceSampleModule`
— checked, not merely omitted.** This Work Package's own sample module
builds its own Inspection directly rather than reusing anything from
that module's own sample data. Decisive: that module's own id
(`tempest.samples.workspaceverification`) sorts **after** this module's
own id (`tempest.samples.workspacemanufacturing`) — `ModuleLifecycleManager`
initialises modules in ordinal Id order, so a constructor dependency on
it would have been a genuine ordering defect, not merely unneeded. The
approved implementation plan initially listed it as a fifth
cross-sample-module dependency (extending `WP 9.3A`'s own five-module
precedent); this was caught and corrected during implementation
planning, before any code was written — see Lessons Learned.

**`ManufacturingKpiCards`'s own "Manufacturing Readiness"/"Supplier
Status" cards do not reuse the existing `EngineeringCockpit.FormatCoverage`
helper.** That helper's own zero-denominator text is hardcoded
Requirements-specific (`"— (no requirements yet)"`) — a pre-existing,
disclosed minor inaccuracy already latent in
`CalculationsKpiCards`'/`VerificationKpiCards`'s own reuse of it, found
while writing this Work Package's own Cockpit code, out of this Work
Package's own scope to fix (it is live, shared, already-shipped Cockpit
code, not newly introduced). This Work Package's own two coverage cards
instead format locally, with an accurate empty-state message, rather
than compounding the inaccuracy with a third instance — see Technical
Debt Assessment.

## New ADRs

`ADR-0091` — Routings/Operations/Supplier Operations are
`Classification`-tagged `ManufacturingOperation` objects, sequenced via
the existing `IHasBomLine.ItemNumber` field, never a new Domain Kind,
container type, or sequencing mechanism.

## Engineering Core Integration

Reuses, unmodified: the entire `WP 8.2C` Engineering Domain
(`ManufacturingOperation`/`WorkInstruction`/`Inspection`,
`EngineeringObjectBase`'s own unconditional facet implementation,
`EngineeringObjectFactory<T>`, `LifecycleTransitionTable`,
`RelationshipKindCategoryMap`'s own pre-existing `"references"`/
`"manufacturedBy"`/`"documentedBy"`/`"verifiedBy"` mappings); the entire
`WP 7.1E` Verification Framework, via direct reuse of `WP 9.3A`'s own
Workspace-layer types; the entire `WP 9.4A` Documents Workspace-layer
types, reused the identical way; `Mechanical.SetBomLineCommand`
(`WP 9.0B`), dispatched unmodified against a new Kind;
`ProjectExplorer.FilterAsync` (`WP8.1B`);
`IHasRelationships.GetRelationshipsAsync`/
`EngineeringDomainContext.RelationshipRepository` (Digital Thread reads,
no new traversal); `EngineeringHealthStatus`/`CockpitKpiCard`/
`CockpitAttentionItem` (`WP8.1C`, unchanged vocabulary). Zero new
Platform Services; zero new persistence mechanism; zero duplication of
any existing framework.

## Testing

54 new tests (1972 → 2026): 24 command tests
(`ManufacturingCommandsTests` — Create for all three Kinds/Rename/
Revise/Delete/Move/Copy/Duplicate/SetStatus, including the
Routing-parent/Operation-child construction and the
impermissible-transition/not-found failure paths); 19 node-provider/
facet/view tests (`ManufacturingNodeProviderAndFacetsTests`, including
`ManufacturingCategory`'s own classification mapping, and dedicated
proof that `DocumentsPropertyFacetProvider`/
`VerificationActivityPropertyFacetProvider` genuinely produce correct
facets when constructed with `kind: "WorkInstruction"`/`"Inspection"`);
11 full Workspace integration tests against the real seeded graph
(`ManufacturingWorkspaceIntegrationTests` — Explorer tree shape,
Property Inspector facets including reused-provider facets and Digital
Thread links, Command Palette count, full Create→Revise→SetStatus→Delete
lifecycle, the dedicated `SetBomLineCommand` zero-new-code proof, real
Cockpit KPIs). One pre-existing test corrected for the two new sample
modules (`ClockModuleDiscoveryTests`, `+2`, 32 → 34, re-verified by
direct count of the test's own `Assert.Contains` lines before editing,
per `WP 9.3A`'s own disclosed "never carry a stated total forward
unchecked" discipline). 2026/2026 passing, zero failures, four full
clean-rebuild-and-test runs across this Work Package's own verification
(two Debug, two Release, via `src/TempestOS.slnx`, plus per-project
Release builds of `Tempest.App`/`Tempest.Samples`), all clean, 0
warnings, 0 errors throughout.

## Repository Metrics

14 new files under `src/Tempest.App/Workspace/Manufacturing/` (5
provider/view/factory/registry/registration, 8 commands, 1 node
provider); 2 new files under `src/Samples/Tempest.Samples/`
(`ManufacturingWorkspaceExplorerModule.cs`,
`EngineeringManufacturingWorkspaceSampleModule.cs`); 3 new test files
under `tests/Tempest.Core.Tests/Workspace/`; 5 existing files edited
(`Program.cs`, `EngineeringCockpit.cs`, `ClockModuleDiscoveryTests.cs`,
`DocumentObjectFactoryRegistry.cs`, `DocumentsNodeProvider.cs`); 1 new
ADR.

## Related Documents

`ADR-0091`; `WP9.5A Engineering Review Report.md`; `WP9.5A Security
Review Report.md`; `WP9.5A Systems Engineering Review.md`; `WP9.5A
Architecture Conformance Review.md`; `WP9.5A Technical Debt
Assessment.md`; `WP9.5A Future Capability Assessment.md`; `WP9.5A
Lessons Learned.md`; `WP9.3A Implementation Report.md`; `WP9.4A
Implementation Report.md`.

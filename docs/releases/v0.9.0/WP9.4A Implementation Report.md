# WP 9.4A — Engineering Documents Workspace — Implementation Report

## Status

Complete. `v0.9.0` ("Mechanical Foundation")'s own fifth Work Package —
the complete Engineering Documents experience, integrated into the
Engineering Workspace, Engineering Cockpit, and Digital Thread, using
the already-real Documentation & Design Domain family
(`IDocument`/`Document`, `IDrawing`/`Drawing`, `ICadModel`/`CadModel`,
`WP 8.2C`). The fourth real Engineering discipline wired into the
Workspace, after Mechanical (`WP 9.0A`/`WP 9.0B`), Requirements
(`WP 9.1A`), and Calculations (`WP 9.2A`).

## Disclosed Numbering Gap

`PROJECT_STATUS.md` and `docs/releases/v0.9.0/` record `WP 9.0A` →
`WP 9.0B` → `WP 9.1A` → `WP 9.1B` → `WP 9.2A` complete, then explicitly
"stops here — no `WP 9.3A` begins... per this project's own standing
discipline... until the Product Owner gives further instruction." No
`WP 9.3A` deliverable exists anywhere in this repository. The Product
Owner's own instruction commissioning this Work Package names `9.4A`
directly, skipping that number — recorded here plainly, not silently
renumbered and not silently backfilled, since an explicit Product Owner
instruction is exactly the standing discipline's own precondition for
proceeding at all. A practical, disclosed consequence: `9.3A` was
expected to be a Verification Workspace, so no live Verification Domain
object exists anywhere in this platform today (`TD-30`, `WP 9.2A`,
confirmed still open by this Work Package) — see "Digital Thread", below,
for exactly what this means for Documents↔Verification traceability.

## What Was Implemented

**Domain layer** — zero changes. `Document`/`Drawing`/`CadModel` are
ordinary `EngineeringObjectBase`-derived concrete classes (`WP 8.2C`),
architecturally identical to Calculation/Mechanical — every Document
Management verb this Work Package's own scope names (Create/Edit/
Rename/Delete/Copy/Duplicate/Move/Revision management/Status changes)
is realised entirely by reading `EngineeringDomainContext.Repository`
and casting to the facets `EngineeringObjectBase` already implements
unconditionally — `IRenamable`, `IHasParent`, `IDeletable`,
`IHasRevisions`, `IHasLifecycle`, `IHasAttachments`, `IHasRelationships`
— exactly `CalculationsWorkspaceRegistration`'s own established pattern.
Specification/Report/Procedure/Standard/Datasheet/External Reference —
five of the eight Document types this Work Package's own scope names —
have no dedicated Domain Kind; they are realised as plain `"Document"`
objects distinguished by `EngineeringObjectMetadata.Classification`
(`ADR-0088`), never new concrete classes. `IHasLifecycle.Status`'s own
existing values (`Draft`/`InReview`/`Approved`/`Released`) already map
1:1 onto this Work Package's own named statuses — unlike Calculations'
Lock/Unlock aliasing, no descriptive alias command was needed.

**Workspace layer** (`Tempest.App.Workspace.Documents`, new namespace,
mirrors `.Calculations`'s own shape, one level simpler — no Template/
Execute concept) — `DocumentObjectFactoryRegistry` (Create, wraps
`EngineeringObjectFactory<Document>`/`<Drawing>`/`<CadModel>`, and
declares the six named `Classification` constants `ADR-0088`
establishes); nine commands (Create/Rename/Revise/Delete/Move/Copy/
Duplicate/SetStatus, all direct mirrors of Calculations' own, plus one
genuinely new command, `AttachDocumentCommand`, wrapping the
already-existing `IHasAttachments.AttachAsync` — unused by any Workspace
command until now); `DocumentsNodeProvider` (root = one synthetic,
read-only category node per `DocumentCategory` label — mirrors
`CalculationsNodeProvider`'s own synthetic `"Templates"` node precedent
— plus every live, un-parented Document; real `IHasParent` nesting
between Documents is fully supported, demonstrated by the Detail Drawing
nested under the GA Drawing in the representative data);
`DocumentsWorkspaceView(Factory)` and `DocumentsPropertyFacetProvider`
(three Kinds — `"Document"`/`"Drawing"`/`"CadModel"`); and
`DocumentsWorkspaceRegistration` — the composition-root entry point,
registered from `Program.cs` alongside Mechanical/Requirements/
Calculations, after `shell.StartAsync()`, for the identical, already-disclosed
reason. Search needed zero new code — `ProjectExplorer.FilterAsync`
(`WP8.1B`) is already generic over whatever provider is registered.

**Engineering Cockpit** (`Tempest.App.Workspace.EngineeringCockpit`) —
`DocumentationStatus` (an existing, fixed `Unknown` placeholder property
since `WP 8.1C`) is now a real, derived `EngineeringHealthStatus`; the
`"Documentation"` entry in `KpiCards` is real once a live Document
exists; a new `DocumentsKpiCards` property supplies the full breakdown
this Work Package's own controlling instruction names (Total Documents/
Draft/Review/Approved/Released/Outstanding Reviews/Missing Evidence/
Documentation Health — see the disclosed "Missing Evidence" heuristic,
below); `AttentionItems` and `OpenActions` each gain a real, conditional
Documents entry. Every other Cockpit member (Risk Summary, Open
Decisions, Verification/Review status, Materials) remains untouched,
still disclosed placeholder — out of this Work Package's own scope.

**Representative data** (`EngineeringDocumentsWorkspaceSampleModule`,
new; `DocumentsWorkspaceExplorerModule`, new) — nine real Document
Domain objects: a General Arrangement Drawing and a Detail Drawing (real
`DrawingNumber`s, the Detail Drawing structurally nested under the GA
Drawing — a real Explorer-nesting demonstration), a Specification, a
Test Report (carries a real `Attachment`), a Design Report, a Material
Datasheet, a Procedure, a Standard, and an External Reference —
covering every named Document type this Work Package's own scope lists,
expanding on the six the "Representative Data" section names by name
(disclosed the same way `WP 8.1C` disclosed its own scope expansion).
Digital Thread cross-links, using only already-mapped relationship
kinds (`"documentedBy"`/`"references"`, both already `RelationshipCategory
.Documentation`/`.Reference` since `WP 8.2C`): the GA Drawing and Detail
Drawing are `documentedBy`-linked from the real Mechanical sample data's
own Wing Assembly/Spar Web Plate; the Specification `references` a real
Requirement; the Test Report `references` the one real Requirement with
an actually-recorded Verification; the Design Report `references` a
real Calculation; the Material Datasheet `references` the real Spar Web
Plate Part; the Procedure `references` the base sample's own
already-existing live Risk (queried, never duplicated) and, together
with the Standard, demonstrates a Document↔Document link. One `Decision`
(`WP 8.2C`, instantiated by no sample module anywhere before this) is
created and `references` the GA Drawing, honouring the "Documents ↔
Decisions" Digital Thread requirement. The External Reference document
is deliberately left with zero Attachments and zero relationships — the
Cockpit's own real "Missing Evidence" KPI's sole, honest example.
`EngineeringDocumentsWorkspaceSampleModule` is the platform's thirtieth
module, constructor-injecting `MechanicalProductStructureSampleModule`,
`RequirementsWorkspaceSampleModule`, and
`EngineeringCalculationsWorkspaceSampleModule` directly — a disclosed,
deliberate fourth cross-sample-module dependency, mirroring `WP 9.2A`'s
own already-established precedent, extended by one; safe for the
identical ordinal-Id-ordering reason (`tempest.samples.engineeringdomain`
< `mechanicalproductstructure` < `requirementsworkspace` <
`workspacecalculations` < `workspacedocuments`).

## Disclosed Design Decisions

**Document classification taxonomy realised as `Classification`-tagged
`Document` objects (`ADR-0088`).** Specification/Report/Procedure/
Standard/Datasheet/External Reference have no dedicated Domain Kind
anywhere in the platform. Rather than add five new concrete Domain
classes (exactly the "contract redesign" this Work Package's own
controlling instruction forbids), they are realised as plain
`"Document"` objects, distinguished only by the existing
`IHasMetadata.Classification` free-text facet — the identical kind of
disclosed, precedent-following mapping `WP 9.2A`'s own "Failed"/
"Out-of-date" KPI names and `WP 9.1A`'s own "Released→Satisfied" mapping
already established.

**`AttachDocumentCommand`, the one genuinely new command.**
`IHasAttachments` has existed since `WP 8.2C`, on every `IDocument`, but
no Workspace command anywhere wrapped it before this Work Package — a
disclosed, narrow, additive gap-fill, not a new Domain capability.

**"Missing Evidence", a disclosed heuristic, mirroring `WP 9.2A`'s own
"Out-of-date" precedent.** A live Document has "Missing Evidence" if it
carries zero Attachments (`IHasAttachments.GetAttachmentsAsync`) *and*
zero `"documentedBy"`/`"references"` relationships in either direction
(`EngineeringDomainContext.RelationshipRepository`, the existing Digital
Thread read, never a new traversal or `ITraceable.GetEvidenceAsync`,
which honestly resolves empty for every Document today — `TD-30`,
confirmed still open, not introduced by this Work Package).

**Documents↔Verification traceability is structurally proven, not
populated end-to-end.** No live Verification Domain object exists
anywhere in the platform (see "Disclosed Numbering Gap", above); the
Test Report's own `references` link targets the one real Requirement
with an actually-recorded Verification instead — the closest real, live
anchor available — rather than fabricating a Verification object this
Work Package has no mandate to create.

## New ADRs

`ADR-0088` — Specification/Report/Procedure/Standard/Datasheet/External
Reference are realised as `Classification`-tagged `Document` objects,
never new concrete Domain classes; `Drawing`/`CadModel` remain their own
distinct, already-compiled Kinds.

## Engineering Core Integration

Reuses, unmodified: the entire `WP 8.2C` Documentation & Design Domain
family (`Document`/`Drawing`/`CadModel`, `EngineeringObjectBase`'s own
unconditional facet implementation, `EngineeringObjectFactory<T>`,
`LifecycleTransitionTable`, `RelationshipKindCategoryMap`'s own
pre-existing `"documentedBy"`/`"references"` mappings, `Attachment`/
`IAttachment`); `ProjectExplorer.FilterAsync` (`WP8.1B`);
`IHasRelationships.GetRelationshipsAsync`/`IEngineeringRelationshipRepository
.GetIncomingAsync` (Digital Thread reads, no new traversal, per this
Work Package's own explicit "Reuse the existing Digital Thread"
instruction); `EngineeringHealthStatus`/`CockpitKpiCard`/
`CockpitAttentionItem` (`WP8.1C`, unchanged vocabulary); the base
`EngineeringDomainSampleModule`'s own already-live Risk object (`WP 8.2C`,
queried, never duplicated). Zero new Platform Services; zero new
persistence mechanism; zero duplication of any existing framework.

## Testing

57 new tests (1865 → 1922): 24 command tests (`DocumentsCommandsTests`
— Create/Rename/Revise/Delete/Move/Copy/Duplicate/SetStatus/Attach,
including the impermissible-transition and not-found failure paths); 23
node-provider/facet/view tests (`DocumentsNodeProviderAndFacetsTests`,
including `DocumentCategory`'s own classification mapping); 10 full
Workspace integration tests against the real seeded graph
(`DocumentsWorkspaceIntegrationTests` — Explorer tree shape including
category nodes and real parent nesting, Property Inspector facets
including Digital Thread links, Command Palette count, full
Create→Attach→SetStatus→Delete lifecycle, real Cockpit KPIs). One
pre-existing test corrected for the two new sample modules
(`ClockModuleDiscoveryTests`, `+2`, 28 → 30). 1922/1922 passing, zero
failures, four full clean-rebuild-and-test runs across this Work
Package's own verification (two Debug, two Release, via
`src/TempestOS.slnx`, plus per-project Release builds of `Tempest.App`/
`Tempest.Samples`), all clean, 0 warnings, 0 errors throughout.

## Repository Metrics

15 new files under `src/Tempest.App/Workspace/Documents/` (6 provider/
view/factory/registry/registration, 9 commands); 2 new files under
`src/Samples/Tempest.Samples/` (`DocumentsWorkspaceExplorerModule.cs`,
`EngineeringDocumentsWorkspaceSampleModule.cs`); 3 new test files under
`tests/Tempest.Core.Tests/Workspace/`; 3 existing files edited
(`Program.cs`, `EngineeringCockpit.cs`, `ClockModuleDiscoveryTests.cs`);
1 new ADR.

## Related Documents

`ADR-0088`; `WP9.4A Engineering Review Report.md`; `WP9.4A Security
Review Report.md`; `WP9.4A Systems Engineering Review.md`; `WP9.4A
Architecture Conformance Review.md`; `WP9.4A Technical Debt
Assessment.md`; `WP9.4A Future Capability Assessment.md`; `WP9.4A
Lessons Learned.md`; `WP9.0A Implementation Report.md`; `WP9.1A
Implementation Report.md`; `WP9.2A Implementation Report.md`.

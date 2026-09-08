# WP 9.2A — Engineering Calculations Workspace — Implementation Report

## Status

Complete. `v0.9.0` ("Mechanical Foundation")'s own fourth Work Package —
the complete Engineering Calculations experience, integrated into the
Engineering Workspace, Engineering Cockpit, and Digital Thread, using
the already-real Calculation Framework (`WP 7.1D`,
`Tempest.Core.Calculations`) and its Engineering Domain counterpart
(`ICalculation`/`ICalculationSet`, `WP 8.2C`). The third real Engineering
discipline wired into the Workspace, after Mechanical (`WP 9.0A`/
`WP 9.0B`) and Requirements (`WP 9.1A`).

## What Was Implemented

**Domain layer** — zero changes. `Calculation`/`CalculationSet` are
ordinary `EngineeringObjectBase`-derived concrete classes (`WP 8.2C`),
architecturally much closer to Mechanical than to Requirements — every
Calculation Management verb this Work Package's own scope names (Create/
Edit/Delete/Duplicate/Copy/Move/Lock/Unlock/Review/Approve/Archive) is
realised entirely by reading `EngineeringDomainContext.Repository` and
casting to the facets `EngineeringObjectBase` already implements
unconditionally — `IRenamable`, `IHasParent`, `IDeletable`,
`IHasRevisions`, `IHasLifecycle` — exactly `MechanicalWorkspaceRegistration`'s
own established pattern, never a Requirements-style dedicated service.

**Workspace layer** (`Tempest.App.Workspace.Calculations`, new
namespace, mirrors `.Mechanical`'s own shape) — `CalculationObjectFactoryRegistry`
(Create, mirrors `MechanicalObjectFactoryRegistry`); `CalculationTemplateRegistry`
(new — a Workspace-layer type-erasure adapter, `ADR-0086`, making every
registered `ICalculationDefinition<TInput,TResult>` dispatchable through
one non-generic Execute/Recalculate command, and Explorer/Property
Inspector browsable, without ever needing `TInput`/`TResult` statically);
`CalculationRecordReader` (a generic, `JsonDocument`-based reader over
the same `IEngineeringDocumentStore` `CalculationEngine` itself already
writes `CalculationRecord`s into — the Workspace layer's own read side of
that same, unmodified store, never a second one); `CalculationsNodeProvider`
(one Explorer area — a synthetic, read-only "Templates" category, every
live `CalculationSet`, every live un-parented `Calculation`);
`CalculationsWorkspaceView(Factory)` and `CalculationsPropertyFacetProvider`
(three Kinds — `"Calculation"`/`"CalculationSet"`/`"CalculationTemplate"`,
mirroring `RequirementsPropertyFacetProvider`'s own multi-Kind shape);
fourteen commands (Create/Rename/Edit(Revise)/Delete/Move/Copy/Duplicate/
SetStatus/Execute/Recalculate, plus five status-alias `CommandDescriptor`s
— Lock/Unlock/Request Review/Approve/Archive — all dispatched through the
one `SetCalculationStatusCommand`, `ADR-0087`); and
`CalculationsWorkspaceRegistration` — the composition-root entry point,
registered from `Program.cs` alongside Mechanical/Requirements, after
`shell.StartAsync()`, for the identical, already-disclosed reason. Search
needed zero new code — `ProjectExplorer.FilterAsync` (`WP8.1B`) is
already generic over whatever provider is registered.

**Engineering Cockpit** (`Tempest.App.Workspace.EngineeringCockpit`) —
`CalculationStatus` is now a real, derived `EngineeringHealthStatus`
(`Unknown`/`Blocked`/`Attention`/`Healthy`); the `"Calculations"` entry in
`KpiCards` is real once a live Calculation exists; a new
`CalculationsKpiCards` property supplies the full breakdown this Work
Package's own controlling instruction names (Total/Draft/Review/Approved/
Failed/Out-of-date/Verification Coverage/Calculation Health — see the
disclosed status-name mappings, below); `AttentionItems` and
`OpenActions` each gain a real, conditional Calculations entry; the
closing "Other disciplines still placeholder" sentence is edited to drop
"Calculations" (only Materials remains), disclosed here rather than
silently rewritten.

**Representative data** (`EngineeringCalculationsWorkspaceSampleModule`,
new; `EngineeringCalculationDefinitions.cs`, new, five real — if
simplified — engineering calculations: Bolt Shear Capacity, Beam Bending
Stress, Bearing Load Capacity, Pressure Vessel Wall Thickness, Material
Selection Margin) — one `CalculationSet` ("Wing Attach Bolt
Calculations") grouping the bolt shear and bearing checks; a
`"basedOnCalculation"` chain (bearing based on bolt shear); a mix of
lifecycle statuses (Approved, InReview, Draft); one genuine
`CalculationValidationOutcome.Conditional` outcome (Material Selection —
applied stress exceeds allowable — the Cockpit's own real "Failed" KPI,
not fabricated); one Calculation revised after being executed (the
Cockpit's own real "Out-of-date" KPI); two Digital Thread links to the
real Mechanical sample data (Wing Assembly, Spar Web Plate, both
`"calculatedBy"`) and one to the real Requirements sample data (also
`"calculatedBy"`) — a disclosed, deliberate third cross-sample-module
dependency, mirroring `RequirementsWorkspaceSampleModule`'s own already-
established first such dependency, safe for the identical ordinal-Id-
ordering reason.

## Disclosed Design Decisions

**`CalculationTemplateRegistry` (`ADR-0086`).** `ICalculationEngine.ExecuteAsync<TInput,TResult>`
is generic; a single Workspace command cannot carry a compile-time
`TInput`/`TResult` for an arbitrary registered Template. Rather than one
hand-written command per Template (which would not scale past this Work
Package's own five), a small, `Tempest.App`-only adapter marshals each
Template's own input/output as JSON — the identical type-erasure
principle `CalculationEngine` itself already uses internally
(`ADR-0056`), one layer higher, introducing no Domain-layer registry
contract (`WP8.2B Dependency Rules.md` §8's own "no registry contract is
proposed" instruction, honoured exactly as `MechanicalObjectFactoryRegistry`
already honours it for object construction).

**Calculation Status and Approval State reuse `IHasLifecycle` alone.**
No `IApprovalGate`/`IApproval`/`IReview`/`IReviewGate` implementation
exists anywhere in this platform — a genuine, pre-existing gap, not
introduced here (see Technical Debt Assessment, `TD-30`). "Lock"/
"Unlock"/"Request Review"/"Approve"/"Archive" are therefore five
descriptive `CommandDescriptor`s over the one, real
`SetCalculationStatusCommand`/`IHasLifecycle.TransitionAsync` mechanism
(`ADR-0087`) — "Lock"/"Approve" both transition to `Approved`, "Unlock"
back to `Draft`, "Request Review" to `InReview`, "Archive" to the
terminal `Archived` — never five new mechanisms, and the "Approved"
Property Inspector/Cockpit facet is derived from `LifecycleState` alone,
exactly mirroring `MechanicalPropertyFacetProvider`'s own already-shipped
"Released" facet.

**Evidence reads `GetRelationshipsAsync` directly, never `GetEvidenceAsync`.**
No concrete class anywhere implements `ICalculationResult`/
`IVerificationResult` (the Domain-level evidence contracts) —
`EvidenceComposer`/`ITraceable.GetEvidenceAsync` honestly resolves empty
for every Calculation today, a genuine, pre-existing gap (`TD-30`).
`CalculationsPropertyFacetProvider`/`CalculationRecordReader` read the
`"calculatedBy"` relationship trail and the shared `IEngineeringDocumentStore`
directly instead — the existing Digital Thread read, never a new
traversal — exactly mirroring `RequirementsPropertyFacetProvider`'s own
already-disclosed identical treatment of the same class of gap
(`WP9.1A Technical Debt Assessment.md`).

**"Failed"/"Out-of-date"/"Verification Coverage", disclosed status-name
mappings, mirroring `RequirementsKpiCards`'s own "Released→Satisfied"
precedent.** `CalculationValidationOutcome` has no literal `"Failed"`
value — a genuine constraint violation throws
`CalculationInputInvalidException` instead, producing no record at all;
the Cockpit's own "Failed" card reports the count of live Calculations
whose most recent execution recorded `Conditional` (a real result was
still produced, alongside an unmet advisory constraint). "Out-of-date"
is a disclosed heuristic: a Calculation's own latest content revision is
newer than its own latest executed record. "Verification Coverage"
reports the share of live Calculations executed at least once — real,
evidentiary execution, never fabricated.

## New ADRs

`ADR-0086` — the `CalculationTemplateRegistry` Workspace-layer,
JSON-marshalled type-erasure adapter over `ICalculationEngine`, mirroring
`ADR-0079`'s own precedent for the Engineering Domain's own object
factories. `ADR-0087` — Calculation Management's Lock/Unlock/Review/
Approve/Archive verbs are `CommandDescriptor` aliases over the existing
`IHasLifecycle.TransitionAsync`/`LifecycleTransitionTable`, never new
Domain state; Approval State is derived from `LifecycleState` alone.

## Engineering Core Integration

Reuses, unmodified: the entire `WP 7.1D` Calculation Framework
(`ICalculationEngine`/`ICalculationDefinition`/`CalculationContext`/
`CalculationRecord<TResult>`, every execution still durably recorded as
an `IEngineeringDocument` in the same shared `IEngineeringDocumentStore`);
the entire `WP 8.2C` Engineering Domain (`Calculation`/`CalculationSet`,
`EngineeringObjectBase`'s own unconditional facet implementation,
`EngineeringObjectFactory<T>`, `LifecycleTransitionTable`,
`RelationshipKindCategoryMap`'s own pre-existing `"calculatedBy"`/
`"basedOnCalculation"` mappings); `ProjectExplorer.FilterAsync`
(`WP8.1B`); `IHasRelationships.GetRelationshipsAsync`/
`IEngineeringRelationshipRepository.GetIncomingAsync` (Digital Thread
reads, no new traversal, per this Work Package's own explicit "Reuse the
existing Digital Thread" instruction); `EngineeringHealthStatus`/
`CockpitKpiCard`/`CockpitAttentionItem` (`WP8.1C`, unchanged vocabulary);
`Tempest.Core.UnitsAndQuantities` (Length/Force/Pressure/Area — no new
Dimension). Zero new Platform Services; zero new persistence mechanism;
zero duplication of any existing framework.

## Testing

57 new tests (1808 → 1865): 30 command tests (`CalculationsCommandsTests`
— Create/Rename/Revise/Delete/Move/Copy/Duplicate/SetStatus/Execute/
Recalculate, including the impermissible-transition and invalid-input-JSON
failure paths); 15 node-provider/facet/view tests
(`CalculationsNodeProviderAndFacetsTests`, including `CalculationRecordReader`);
12 full Workspace integration tests against the real seeded graph
(`CalculationsWorkspaceIntegrationTests` — Explorer tree shape, Property
Inspector facets including the Conditional outcome and Digital Thread
links, Command Palette count, full Create→Execute→Recalculate→SetStatus→
Delete lifecycle, real Cockpit KPIs). One pre-existing test corrected for
the two new sample modules (`ClockModuleDiscoveryTests`, `+2`).
1865/1865 passing, zero failures, four full clean-rebuild-and-test runs
across this Work Package's own verification (two Debug, two Release, via
`src/TempestOS.slnx`, plus per-project Release builds of `Tempest.App`/
`Tempest.Samples`), all clean, 0 warnings, 0 errors throughout.

## Repository Metrics

18 new files under `src/Tempest.App/Workspace/Calculations/` (8
provider/view/factory/registry/reader, 10 commands — `Execute`/
`Recalculate` each carry both a command and a handler in one file,
matching every other command file's own convention); 3 new files under
`src/Samples/Tempest.Samples/` (`EngineeringCalculationDefinitions.cs`,
`CalculationsWorkspaceExplorerModule.cs`,
`EngineeringCalculationsWorkspaceSampleModule.cs`); 3 new test files
under `tests/Tempest.Core.Tests/Workspace/`; 3 existing files edited
(`Program.cs`, `EngineeringCockpit.cs`, `ClockModuleDiscoveryTests.cs`);
2 new ADRs.

## Related Documents

`ADR-0086`; `ADR-0087`; `WP9.2A Engineering Review Report.md`; `WP9.2A
Security Review Report.md`; `WP9.2A Systems Engineering Review.md`;
`WP9.2A Architecture Conformance Review.md`; `WP9.2A Technical Debt
Assessment.md`; `WP9.2A Future Capability Assessment.md`; `WP9.2A
Lessons Learned.md`; `WP9.0A Implementation Report.md`; `WP9.1A
Implementation Report.md`.

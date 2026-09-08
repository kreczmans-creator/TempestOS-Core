# WP 9.2A — Engineering Calculations Workspace — Engineering Review Report

## Purpose

Reviews whether the shipped implementation satisfies `WP 9.2A`'s own
controlling instruction, and whether every engineering judgement call
made along the way was reasonable and disclosed.

## Acceptance Criteria Review

| Requirement | Verdict | Evidence |
|---|---|---|
| Calculation Sets, Records, Templates, Inputs, Outputs, Assumptions, References, Safety Factors, Units, Evidence, Status, Approval State, Revision History | **Met, with disclosed representations for the two items with no dedicated contract** | `ICalculationSet`/`ICalculation`/`CalculationRecord<TResult>`/`ICalculationDefinition` (`WP 7.1D`/`WP 8.2C`, unchanged); Safety Factor is a named `CalculationIntermediateResult` (no dedicated contract exists); Approval State is `IHasLifecycle.Status` alone (no `IApproval`/`IApprovalGate` implementation exists anywhere — see Technical Debt Assessment). |
| Workspace / Cockpit / Explorer / Property Inspector / Navigation / context menus / Command Palette / Search / Workspace Commands | **Met** | `CalculationsNodeProvider`/`CalculationsWorkspaceView(Factory)`/`CalculationsPropertyFacetProvider`, 14 registered commands, real Cockpit KPIs; Search needed zero new code (`ProjectExplorer.FilterAsync`, `WP8.1B`, already generic). |
| Create/Edit/Delete/Duplicate/Copy/Move/Execute/Recalculate/Lock/Unlock/Review/Approve/Archive | **Met, with a disclosed shared mechanism for the five status verbs** | Ten command classes; Lock/Unlock/Review/Approve/Archive are `CommandDescriptor` aliases over the one `SetCalculationStatusCommand` (`ADR-0087`) — see Scope Discipline Review, below. |
| Input editing, unit validation, safety factor management, formula execution, engineering assumptions, reference management, result history, revision history, calculation evidence | **Met** | `ExecuteCalculationCommand`/`CalculationTemplateRegistry` (formula execution, unit-aware `Quantity<TDimension>` inputs); `CalculationRecordReader` (result history, distinct from `IHasRevisions`' own object-content revision history); `CalculationsPropertyFacetProvider` (assumptions, safety factor, referenced materials, evidence). |
| Traceability reusing the existing Digital Thread; no new traceability mechanism | **Met** | Every Property Inspector/Cockpit read is `GetRelationshipsAsync`/`GetIncomingAsync`-derived, reusing the pre-existing `"calculatedBy"`/`"basedOnCalculation"` relationship kinds; zero new traversal code. |
| Engineering Cockpit real KPIs (Total/Draft/Review/Approved/Failed/Out-of-date/Calculations awaiting review/Verification coverage/Engineering health indicators) | **Met, three disclosed status-name mappings** | `CalculationsKpiCards`/`CalculationStatus`/`OutstandingCalculationActions`; "Failed"→`Conditional` outcome, "Out-of-date"→a disclosed revision-vs-execution-timestamp heuristic, "Verification Coverage"→executed-at-least-once share — see Implementation Report. |
| Representative data: Bolt/Beam/Bearing/Pressure/Material Selection, demonstrating Digital Thread integration with Requirements and Mechanical Product Structure | **Met** | `EngineeringCalculationsWorkspaceSampleModule` — five real definitions, one Calculation Set, one calculation chain, real links to the Mechanical sample's Wing Assembly/Spar Web Plate and one Requirements sample requirement. |
| Quality: existing architecture/layering/contracts, Digital Thread compatibility, Workspace consistency | **Met** | See Architecture Conformance Review. |
| Unit/integration/Workspace tests; repeated Debug/Release verification | **Met** | 57 new tests, 1865/1865, four full clean-rebuild-and-test runs. |
| Documentation and Governance | **Met** | This document and its siblings; governance registers updated. |
| No architectural redesign; no contract redesign; no duplicate framework; reuse existing services exclusively | **Met, with two disclosed additive Workspace-layer deviations (`ADR-0086`, `ADR-0087`), zero Domain-layer changes** | See Architecture Conformance Review. |

## Scope Discipline Review

**Lock/Unlock/Review/Approve/Archive are one command, five descriptors,
not five command classes.** No dedicated Lock/Approval contract exists
anywhere in the Domain (confirmed directly — `Contracts/Lifecycle.cs`'s
own `IApprovalGate`/`IApproval`/`IReview`/`IReviewGate` have zero
concrete implementations across the entire platform, not merely
uninvolved in this Work Package). Building five real, independent
mechanisms would either invent new Domain state this Work Package's own
"no contract redesign" instruction forbids, or fake five distinct
behaviours that all reduce to the same `LifecycleState` transition.
Judged sufficient — and more honest — to expose the one real mechanism
under five task-oriented names, exactly the same engineering call
`WP 9.0A`/`WP 9.0B` already made for "Copy"/"Duplicate" both reducing to
one `Create`+`Move` machinery.

**Calculation Sets have no runtime "add member"/"remove member"
command.** `ICalculationSet.MemberCalculationIds` is frozen at
construction (`WP 8.2C`) — an intentional Domain shape, identical to
Mechanical's own `Configuration.MemberRevisions` (`WP 9.0B`), which
likewise received no mutator command. `CreateCalculationObjectCommand`
accepts a member list at creation time, matching `CreateMechanicalObjectCommand`'s
own identical `memberRevisions` parameter exactly — no new Domain
mutation was invented to work around a deliberately frozen shape.

**"Recalculate" requires fresh input, not a stored one.**
`CalculationRecord<TResult>` never retained its own producing input
(only `Result`/`Assumptions`/`IntermediateResults`/`Validation`/
`ReferencedMaterialIds`) before this Work Package, and this Work Package
adds no Domain-layer change to retain one — extending the stored shape
was judged out of proportion for a Work Package whose own controlling
instruction is "reuse the existing Calculation Framework... do not
redesign calculation execution." Disclosed as `TD-29`, not silently
narrowed.

## Engineering Judgement Calls Requiring Explicit Ratification

1. **`CalculationTemplateRegistry`, a new Workspace-layer type-erasure adapter, not five (or fifty) hand-written commands.** Ratified — the only way to keep Execute/Recalculate genuinely generic over every registered Template without a Domain-layer registry contract (`WP8.2B Dependency Rules.md` §8); recorded as `ADR-0086`.
2. **Safety Factor represented as a named `CalculationIntermediateResult`, not a new type.** Ratified — the Calculation Framework's own `CalculationIntermediateResult.Value` is explicitly documented as "not constrained to" any particular shape; inventing a dedicated `SafetyFactor` record would be exactly the kind of new Domain contract this Work Package's own controlling instruction forbids.
3. **Approval State read from `IHasLifecycle.Status` alone, never from `IApprovalGate`.** Ratified — `IApprovalGate`/`IApproval` have no concrete realisation anywhere in the platform (a pre-existing, now-formally-registered gap, `TD-30`); building one to serve this Work Package alone would be a genuine, out-of-proportion architectural addition, not an integration.
4. **`EngineeringCalculationsWorkspaceSampleModule` constructor-injects both `MechanicalProductStructureSampleModule` and `RequirementsWorkspaceSampleModule`.** Ratified — the same, already-established precedent `RequirementsWorkspaceSampleModule` set for its own first cross-sample-module dependency; safe for the identical ordinal-Id-ordering reason, confirmed directly by four clean test runs with zero flakes.

## Verdict

**No Release Blocking findings.** Every acceptance criterion is met; the
two items with no dedicated Domain contract (Safety Factor, Approval
State) are represented honestly through the framework's own existing,
open shapes rather than through invented new ones; every engineering
judgement call above is ratified with its own recorded reasoning.

## Related Documents

`WP9.2A Implementation Report.md`; `ADR-0086`; `ADR-0087`; `WP9.2A
Architecture Conformance Review.md`; `WP9.2A Technical Debt Assessment.md`.

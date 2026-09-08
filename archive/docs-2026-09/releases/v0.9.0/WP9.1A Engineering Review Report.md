# WP 9.1A — Requirements Management Workspace — Engineering Review Report

## Purpose

Reviews whether the shipped implementation satisfies `WP 9.1A`'s own
controlling instruction, and whether every engineering judgement call
made along the way was reasonable and disclosed.

## Acceptance Criteria Review

| Requirement | Verdict | Evidence |
|---|---|---|
| Requirement Sets, Requirements, Groups, Hierarchies, Relationships, Allocation, Status, Classification, Priority, Ownership, Evidence, Traceability | **Met** | `IRequirementCollection`/`IRequirementGroup`/`IRequirement` (`WP 7.3A`, extended `ADR-0084`); Owner/Priority new; Status/Category/Evidence/Relationships already real. |
| Workspace / Cockpit / Explorer / Property Inspector / Navigation / context menus / Command Palette / Search / Workspace Commands | **Met** | `RequirementsNodeProvider`/`RequirementsWorkspaceView(Factory)`/`RequirementsPropertyFacetProvider`, 18 registered commands, real Cockpit KPIs; Search needed zero new code (`ProjectExplorer.FilterAsync`, `WP8.1B`, already generic). |
| Create/Edit/Delete/Duplicate/Move/Copy/Group/Reorder/Import/Export/Bulk editing/Multi-selection | **Met, one disclosed scope reduction (Copy folded into Duplicate+Move; no "remove from collection")** | See Scope Discipline Review, below. |
| Traceability reusing the existing Digital Thread; no new traceability mechanism | **Met** | Every Property Inspector/Cockpit read is `GetRelationshipsAsync`/`GetEvidenceAsync`-derived; zero new traversal code. |
| Requirement validation / duplicate / orphan / missing verification / missing allocation / invalid relationships / status validation | **Met** | `IRequirementValidationService` — five checks, `TEMPEST-REQ-VAL-001`–`005`; status validation is a disclosed read-time confirmation only (`SetStatusAsync` is the sole enforcement point). |
| Engineering Cockpit real KPIs (Total/Draft/Review/Approved/Released/Verification Coverage/Allocation Coverage/Requirement Health/Outstanding Actions) | **Met, one disclosed status-name mapping** | `RequirementsKpiCards`; "Released" maps to `Satisfied` — see Implementation Report. |
| Representative data: multiple Requirement Sets, hierarchical Requirements, cross-links, verification relationships, allocations to assemblies/parts, representative evidence | **Met** | `RequirementsWorkspaceSampleModule` — two Collections, three-level Group hierarchy, ten Requirements, real allocations to the Mechanical sample data's own Wing Assembly/Spar Web Plate. |
| Quality: existing architecture/layering/contracts, Digital Thread compatibility, Workspace consistency | **Met** | See Architecture Conformance Review. |
| Unit/integration/Workspace tests; repeated Debug/Release verification | **Met** | 70 new tests, 1808/1808, four full clean-rebuild-and-test runs. |
| Documentation and Governance | **Met** | This document and its nine siblings; governance registers updated. |
| No architectural redesign; no contract redesign; no duplicate framework; reuse existing services | **Met, with two disclosed additive deviations (`ADR-0084`, `ADR-0085`) and three disclosed pre-existing/pre-commit fixes** | See Architecture Conformance Review and Technical Debt Assessment. |

## Scope Discipline Review

**"Copy" folded into Duplicate + Move, not a separate command.**
Requirements have no `IHasParent`-style single structural parent the way
Mechanical objects do — "Copy under a different parent" and "Duplicate
in place" collapse to the same operation (`DuplicateRequirementCommand`,
optionally followed by `MoveRequirementCommand`) once Group is the only
positional concept a requirement carries. Judged sufficient rather than
building a second, near-identical command.

**No "remove from collection" command.** `IEngineeringDocumentStore` has
no unlink primitive (confirmed directly, unchanged since `WP 7.3A`).
Building one would fake a capability that would not actually work
correctly against the underlying store; the WP's own text names "Group"
as the capability required, satisfied by the real `MoveToGroupAsync`/
`MoveGroupAsync`/`DeleteGroupAsync` methods this Work Package adds — a
collection-level equivalent was judged out of reach without a genuine
Domain-level capability this WP's own "no contract redesign" instruction
would forbid adding speculatively.

**No Domain-level `ISearchQuery`/`ISearchResult` implementation.**
`Contracts/Search.cs` (`WP8.2B`) remains unimplemented anywhere in the
platform; `ISearchResult.Matches` is typed `IReadOnlyList<IEngineeringObject>`,
which no Requirements type implements. Search is instead satisfied
entirely by registering `RequirementsNodeProvider` with the already-real,
already-generic `ProjectExplorer.FilterAsync` (`WP8.1B`) — a scope-fit
finding, not a new ADR (a choice between two already-existing patterns).

## Engineering Judgement Calls Requiring Explicit Ratification

1. **New `IRequirementValidationService`, not a retrofit of `IValidationRule` onto Requirements.** Ratified — `IValidationRule.EvaluateAsync` is scoped to `IEngineeringObject`, which `IRequirement` does not implement; this is a disclosed deviation from the plan's own original text, corrected during implementation once the type mismatch was confirmed directly.
2. **`ListCollectionsAsync`/`ListGroupsAsync` added mid-implementation, beyond the plan's own original scope.** Ratified — the Explorer tree cannot be rooted at real Requirement Sets/Groups without an enumeration capability, and none existed; `ADR-0084` records the decision and its `ADR-0059` precedent.
3. **`RequirementValidationService`/`RequirementsPropertyFacetProvider`/`EngineeringCockpit` all corrected away from `GetEvidenceAsync` toward `GetRelationshipsAsync`.** Ratified — a real, pre-commit defect (a permission-gated read reachable from a passive status/validation surface), found and fixed with the representative data as the reproducing case, exactly as `WP 9.0B`'s own `ReviseAsync` finding was.
4. **`RequirementCollectionExportAdapter` placed in `Tempest.Samples`, not `Tempest.App`.** Ratified — required by the project-reference direction (`Tempest.App` depends on `Tempest.Samples`, never the reverse); disclosed in Lessons Learned as a design correction made before any file was left in the wrong location across a commit boundary.

## Verdict

**No Release Blocking findings.** Every acceptance criterion is met, two
disclosed scope reductions are both individually justified against the
Domain's own real capabilities, and every engineering judgement call
above is ratified with its own recorded reasoning.

## Related Documents

`WP9.1A Implementation Report.md`; `ADR-0084`; `ADR-0085`; `WP9.1A
Architecture Conformance Review.md`; `WP9.1A Technical Debt Assessment.md`.

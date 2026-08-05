# WP 9.0A — Mechanical Product Structure — Engineering Review Report

## Purpose

Reviews whether the shipped implementation actually satisfies `WP 9.0A`'s
own controlling instruction, and whether every engineering judgement call
made along the way was a reasonable, disclosed one rather than an
undisclosed scope expansion.

## Acceptance Criteria Review

| Requirement | Verdict | Evidence |
|---|---|---|
| Projects, Assemblies, Sub-Assemblies, Parts, Components | **Met** | All six already-real `WP8.2C` Kinds; no new concrete classes needed. |
| Project Explorer / Cockpit / Navigation / Selection / Properties / Context menus / Workspace commands / Command Palette / Status | **Met, with one disclosed exception (context menus)** | Real node provider, view factory, facet provider, six commands, Cockpit real reads. `RenderContextMenu` (`WorkspaceShell`, `WP8.1B`) already lists actions generically per node type — this Work Package adds no Mechanical-specific menu text beyond what that existing, generic mechanism already renders; a bespoke per-Kind context menu was judged unnecessary scope, not overlooked. |
| Breadcrumb navigation | **Met, via existing mechanism** | `ProjectExplorer.CurrentPath`/`WorkspaceShell.BuildBreadcrumb` (`WP8.1B`) already provide this generically for any registered provider — no new code was needed. `MechanicalProductStructureNodeProvider.GetAncestryAsync` is a supplementary, disclosed convenience, not a requirement. |
| Parent/Child, Expand/Collapse, Create/Rename/Delete/Move/Copy/Duplicate, Object validation | **Met** | `ADR-0080`/`ADR-0081`; 64 new tests including cycle/has-children rejection. |
| Drag-and-drop, Multi-selection | **Explicitly deferred, per the WP's own "if supported by current UI technology" clause** | Terminal UI (`ADR-0066`) has no drag gesture; `ISelectionService` is a frozen, single-selection `WP8.0B` contract. Both recorded in the Future Capability Register, not silently dropped. |
| Engineering Identifier, Name, Description, Revision, Status, Owner, Discipline, Classification, Tags, Notes | **Met** | `MechanicalPropertyFacetProvider`, sourced entirely from existing `IHasBusinessIdentifier`/`IHasMetadata`/`IHasLifecycle`/`IHasRevisions` facets. |
| Configuration Items, Revision display, Baseline awareness, Released state display; no configuration management workflow | **Met** | Baseline facet derived from `IConfiguration.MemberRevisions`; Released facet from `LifecycleState`; no create/edit/approve Configuration workflow exists anywhere in this Work Package. |
| Parent, Child, References, Related objects | **Met** | `IHasParent` (parent/child), existing `IHasRelationships.LinkAsync` reused for references (the shared-Component cross-reference in sample data). |
| Representative data: multiple assemblies, deep hierarchy, multiple parts, shared components, cross references | **Met** | See Implementation Report. |
| No architectural redesign; no contract redesign; no duplicate engineering concepts | **Met, with disclosed additive deviations** | `ADR-0080`/`ADR-0081`/`ADR-0082` — every deviation is additive, ADR-recorded, and reviewed independently in `WP9.0A Architecture Conformance Review.md`. |
| Comprehensive unit/integration/Workspace tests; stable across multiple clean Debug/Release runs | **Met** | 64 new tests, 1695/1695 passing, four clean rebuild-and-test runs. |
| Documentation and Governance | **Met** | This document and its nine siblings; governance registers updated per `WP9.0A Governance Update Summary` (folded into `PROJECT_STATUS.md`'s own entry). |

## Scope Discipline Review

No Requirements/Verification/Calculation/Manufacturing discipline logic
was written anywhere in this Work Package — `EngineeringCockpit`'s own
still-placeholder `RequirementsStatus`/`VerificationStatus`/
`CalculationStatus` are untouched. No Configuration Management workflow
(create/approve/release a baseline) was built — `Configuration` display
is read-only. No new Platform Service, no new persistence mechanism, no
new UI framework was introduced.

## Engineering Judgement Calls Requiring Explicit Ratification

1. **Extending two frozen contracts (`Tempest.Core.EngineeringDomain`'s
   facet set, `IWorkspaceManager`) rather than working around their
   absence.** Ratified: the alternative (Workspace-layer-only mutation)
   was explored and found unbuildable — `DisplayName` has no setter, no
   delete concept exists anywhere, no live parent pointer exists anywhere
   in the frozen Domain. See `ADR-0080` §Alternatives Considered.
2. **`createDefault` omitted from all six Mechanical `CommandDescriptor`s.**
   Ratified: none has a meaningful parameterless default in a shell with
   no pre-selected object context; all six remain registered, listed, and
   dispatchable with real data. Recorded as a Future Capability, not
   silently limited.
3. **Single academy documentation file, not two, for the WP's own
   "Academy Concept Guide"/"Academy Implementation Retrospective" naming.**
   Ratified: preserves the existing one-file-per-Work-Package academy
   folder convention (`docs/academy/03 Work Packages/`); the file is
   structured in two clearly headed parts satisfying both names. Disclosed
   as a documentation-structure decision, not a silent substitution.

## Verdict

**No Release Blocking findings.** Every acceptance criterion is met,
either directly or through an explicitly disclosed, reasoned deferral.
Every engineering judgement call above is ratified with its own recorded
reasoning.

## Related Documents

`WP9.0A Implementation Report.md`; `ADR-0080`; `ADR-0081`; `ADR-0082`;
`WP9.0A Architecture Conformance Review.md`; `WP9.0A Future Capability
Assessment.md`.

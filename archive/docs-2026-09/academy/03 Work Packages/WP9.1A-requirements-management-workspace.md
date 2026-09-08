# WP 9.1A — Requirements Management Workspace

> This file satisfies `WP 9.1A`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Implementation Retrospective" — as
> two clearly headed parts within one file, mirroring
> `WP9.0A-mechanical-product-structure.md`/`WP9.0B-product-configuration-and-bom-management.md`'s
> own identical, disclosed documentation-structure decision, preserving
> this folder's own established one-file-per-Work-Package convention.

# Part I — Concept Guide

## 1. Introduction

`WP 9.1A` is `v0.9.0`'s own third Work Package, and the second real
Engineering discipline wired into the Engineering Workspace, after
Mechanical (`WP 9.0A`/`WP 9.0B`). Where the two Mechanical Work Packages
proved the Kind-keyed Workspace extension model and the additive-facet
Domain model against `EngineeringObjectBase`'s own facet-composed
architecture, `WP 9.1A` proves the Workspace half of that model
generalises to a genuinely *different* Domain architecture — the
Requirements Framework's immutable-snapshot, service-oriented design,
built two full releases earlier (`WP 7.3A`), for a different purpose
entirely.

## 2. Purpose

To give the already-real Requirements Framework a complete Workspace
presence — a browsable Explorer tree rooted at real Requirement Sets and
Groups, a Property Inspector showing real facets including Verification
Coverage and Allocation, eighteen commands covering the full requirement
lifecycle, real Engineering Cockpit KPIs, and Import/Export — using
nothing the Domain layer did not already, or could not additively,
provide.

## 3. Background

By the time this Work Package began, `Tempest.Core.Requirements`
(`WP 7.3A`) was a complete, tested, but Workspace-invisible framework:
Create/Revise/SetStatus/Link/Group/Collect/GetEvidence all worked, all
correctly, all unreachable from the Engineering Workspace a user
actually opens. Unlike Mechanical, Requirements had never needed
Delete, Owner, Priority, or any enumeration of its own Groups/Collections
— nothing before this Work Package needed to browse a tree, only to
create, read, and validate one requirement at a time.

## 4. The Problem

Three distinct problems, the first two echoing both Mechanical Work
Packages' own shape, the third genuinely new:

**Presentation and wiring.** Surfacing already-real data through the
Workspace — the by-now-familiar mechanical work.

**New capability with nowhere to live.** Delete, Owner, Priority,
Move-between-groups, Move-a-group, Delete-a-group, Delete-a-collection —
none existed anywhere in `IRequirementsService` before this Work
Package.

**An architecture the existing extension mechanism was never proven
against.** `ADR-0080`'s own facet-composition pattern assumes an
`EngineeringObjectBase`-derived object with a mutable base class to
compose into. `Requirement`/`RequirementGroup`/`RequirementCollection`
are immutable snapshot classes with no such base — the pattern's own
*mechanism* does not fit; its *principle* (extend additively, never
reopen a frozen shape) had to be re-derived against a different shape
of Domain object entirely.

## 5. The Design

Every new Domain capability is a new `IRequirementsService` method,
following `SetStatusAsync`'s own already-proven `dto with {...}` +
`IEngineeringDocumentStore.ReviseAsync` mutation shape — never a new
base class, never a facet interface (`ADR-0084`). The Workspace layer
mirrors Mechanical's own shape almost exactly:
`RequirementsNodeProvider`/`RequirementsWorkspaceViewFactory`/
`RequirementsPropertyFacetProvider` read `IRequirementsService` directly
instead of `EngineeringDomainContext.Repository`, but the registration
shape, the Kind-keyed dispatch, and the command/handler pattern are
byte-for-byte the same pattern, proven a second time against a
different Domain shape underneath.

Multi-selection (`ADR-0085`) exists because Bulk editing is a real
consumer for the first time — `ISelectionService` gains `SelectedItems`/
`ToggleSelectionAsync` additively, `WorkspaceSelectionChangedEvent`
unchanged, a new `WorkspaceSelectionSetChangedEvent` fired alongside it.

## 6. Alternatives Considered

**Retrofit Requirements onto `EngineeringObjectBase`'s own
facet-composition model** — considered and rejected; would be exactly
the "architectural redesign" this Work Package's own controlling
instruction forbids, for a framework `WP 7.3A` deliberately built
differently, on purpose, for its own good reasons (a thin, typed index
over a shared document store, not a second storage mechanism).

**Retrofit `IValidationRule`/`ValidationRuleSet` onto Requirements
validation** — considered, attempted, and corrected during
implementation once `IValidationRule.EvaluateAsync(IEngineeringObject
subject, ...)` was confirmed structurally incompatible with `IRequirement`.
A new, small `IRequirementValidationService` reusing only the
type-agnostic `IValidationResult`/`IValidationDiagnostic` result shapes
was built instead — reuse of the *vocabulary*, not the *rule interface*
that does not fit.

**A Domain-level `ISearchQuery`/`ISearchResult` implementation** —
considered and rejected; `ISearchResult.Matches` is typed
`IReadOnlyList<IEngineeringObject>`, which `IRequirement` does not
implement, and no consumer across three Work Packages has yet needed
cross-discipline search badly enough to justify reopening that contract
(`FCR-0049`).

## 7. Why This Solution Was Chosen

Every alternative either reopened a contract this Work Package's own
controlling instruction explicitly forbade reopening, or retrofitted a
mechanism onto an object shape it was never designed to fit. The chosen
design — additive service methods, a new small validation contract
reusing only the parts of the old one that genuinely generalise, and a
Workspace layer that mirrors an already-proven pattern exactly — costs
nothing extra for `EngineeringDomain`'s own Kinds, and proves the
Kind-keyed Workspace extension model is a genuine platform capability,
not an artefact of Mechanical's own particular Domain shape.

## 8. Architectural Principles

**An extension model's own generality is proven by its second, genuinely
different use, not its first.** `ADR-0067`'s Kind-keyed registration was
designed once, against one Domain architecture; `WP 9.1A` is the proof
it was never actually coupled to that architecture's own particular
shape.

**Reuse the vocabulary, not the interface that does not fit.**
`IRequirementValidationService` reusing `IValidationResult`/
`IValidationDiagnostic` while deliberately not reusing `IValidationRule`
itself is this Work Package's own clearest instance of "additive, not
retrofitted" — take what generalises, build new only what does not.

**A framework built for one consumer can have real gaps a second
consumer surfaces immediately.** `ListCollectionsAsync`/`ListGroupsAsync`
did not exist because nothing before this Work Package ever needed to
browse a tree — the gap was invisible until a tree needed to exist.

## 9. Files Added

4 new files under `src/Tempest.Core/Requirements/`; 25 new files under
`src/Tempest.App/Workspace/` (1 event, 24 under the new `Requirements/`
sub-namespace); 3 new files under `src/Samples/Tempest.Samples/`. See
`WP9.1A Implementation Report.md` for the complete list including edited
files.

## 10. Trade-offs

No "remove from collection" command exists — `IEngineeringDocumentStore`
has no unlink primitive (`FCR-0048`). No Domain-level Search — Workspace-
layer `ProjectExplorer.FilterAsync` reuse was judged sufficient
(`FCR-0049`). Bulk commands do not automatically refresh every touched
item's own open view (`TD-28`, `FCR-0050`). All three accepted,
disclosed, not silently absorbed.

## 11. Common Mistakes

Assuming a permission gate correct for one call site (`GetEvidenceAsync`,
correctly gated for its own `WP 7.3A` evidence-aggregation purpose) stays
correct when a new, different call site (a passive Property Inspector
facet, a Cockpit KPI, a validation read) starts calling it — three
separate call sites made exactly this assumption during this Work
Package's own implementation, all three corrected the same way, once
the pattern was recognised.

## 12. Future Evolution

Collection membership removal (`FCR-0048`), Domain-level cross-discipline
Search (`FCR-0049`), and multi-target Workspace view refresh (`FCR-0050`)
are all named, deliberate non-scope for this Work Package. Multi-selection
(`FCR-0039`) is now resolved, not deferred further.

## 13. Key Takeaways

The Kind-keyed Workspace extension model (`ADR-0067`) has now been
proven across two genuinely different Domain architectures without a
single frozen Workspace contract being reopened — the strongest
evidence yet that the abstraction boundary between "Workspace
extension point" and "Domain implementation shape" was drawn correctly
from the start.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for additive `IRequirementsService` methods, a
Kind-keyed Workspace layer mirroring Mechanical's own shape, and reuse of
everything else. What was built matched that plan, plus three things the
plan's own text did not fully anticipate: the `IRequirementValidationService`
design correction (`IValidationRule` does not fit `IRequirement`), the
`ListCollectionsAsync`/`ListGroupsAsync` addition (the Explorer tree
could not be built without them), and the `GetEvidenceAsync`-to-
`GetRelationshipsAsync` correction across three call sites (found via
the representative data, not a unit test) — all three disclosed fully in
the Implementation Report, Technical Debt Assessment, and Lessons
Learned rather than silently absorbed.

## Verification Rigour

70 new tests, 1808/1808 passing, across four full clean rebuild-and-test
runs (two Debug, two Release via `src/TempestOS.slnx`). The two
permission/availability defects were found specifically by running the
Workspace integration suite against real seeded data under a
deliberately unprivileged sample principal — not by any Domain-layer
unit test, each of which grants exactly the permission its own method
under test needs.

## Governance Discipline

Two new ADRs (`ADR-0084`, `ADR-0085`) record the two genuine new
architectural decisions this Work Package made. Two genuine
implementation defects were fixed in place, precisely because neither
had ever been part of a commit or tagged release — the same "never
silently modify historical records" principle `WP 9.0B` already applied
correctly, applied again here without overreach.

## Retrospective Verdict

The Kind-keyed Workspace extension model proved itself under real,
second-time use against a genuinely different Domain architecture —
the strongest practical test that pattern has faced yet. Building real,
representative data under a real, unprivileged principal — not only
Domain-layer unit tests, each scoped to grant exactly the permission it
needs — surfaced two genuine correctness/availability defects a narrower
testing strategy would very likely have shipped, the second consecutive
Work Package (after `WP 9.0B`'s own `ReviseAsync` finding) where this
project's own "representative data, not placeholders" standard proved
itself a verification technique, not just a presentation nicety.

## Related Documents

`WP9.1A Implementation Report.md`; `WP9.1A Lessons Learned.md`;
`ADR-0084`; `ADR-0085`; `WP9.0A-mechanical-product-structure.md`;
`WP9.0B-product-configuration-and-bom-management.md`.

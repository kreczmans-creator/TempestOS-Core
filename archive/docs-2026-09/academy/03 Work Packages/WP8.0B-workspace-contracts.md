# WP 8.0B — Workspace Contracts

## What This Document Is

A contract-review-only milestone Work Package, mirroring `WP7.2C
Requirements & Verification Platform Contract Review`'s own format —
no production code written, no implementation performed. This document
follows the same whole-review shape (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways) rather than the standard 13-section per-feature
template, since no code exists yet for that template's own "Files
Added"/"Trade-offs" sections to describe.

## Introduction

`WP 8.0B` is `v0.8.0`'s own second Work Package, following `WP 8.0A`'s
own architecture directly — the identical two-stage sequence the
Requirements Engine already proved out (`WP 7.2B` → `WP 7.2C` →
`WP 7.3A`). It defines the complete public contract for the twelve
interfaces `WP8.0A Workspace Architecture Document.md` named but did
not design, and resolves both ADRs that Work Package deliberately
reserved.

## What Was Achieved

Twelve interfaces fully specified in proposed C# — `IWorkspace`,
`IWorkspaceManager`, `IWorkspaceView`, `IWorkspacePanel`,
`IWorkspaceLayout`, `INavigationService`, `ISelectionService`,
`IWorkspaceContext`, `IWorkspaceState`, `IProjectExplorer`,
`IPropertyInspector`, `IWorkspaceCommand` — plus the supporting types
each genuinely needs (`WorkspaceSelection`, `WorkspacePanelPlacement`,
`ProjectExplorerNode`, `PropertyFacet`, `IWorkspaceViewFactory`,
`IProjectExplorerNodeProvider`, and three new exception types), produced
as four documents (`Workspace Contracts`, `Sequence Diagrams`,
`Lifecycle Definitions`, `Dependency Rules`). Both reserved ADRs
resolved: `ADR-0066` (terminal-based presentation, not a graphical
desktop framework) and `ADR-0067` (Kind-keyed registration for both
object views and explorer nodes, mirroring `IReportDefinition`/
`IReportRenderer<T>`). Zero code compiled — every signature remains
proposed, documentation-only C#, exactly as `WP7.2C Requirements
Platform Contracts.md` established the precedent for.

## Architectural Lessons

**Designing two contracts together (`IWorkspaceView`,
`IProjectExplorer`) revealed that `ADR-0067`'s own reserved question was
really two questions, not one.** `WP8.0A Workspace Architecture
Document.md` §10 described a single "object-view extensibility
contract" as the open question. Once `IWorkspaceView` (object
presentation) and `IProjectExplorer` (tree population) were both
concretely specified, it became clear a `Category`/`Group`/`Collection`
tree node has no backing `IWorkspaceView` at all (`WP8.0A Navigation
Specification.md` §3.1) — meaning one factory-shaped registration
cannot serve both needs. This is a genuine finding contract design
surfaced that architecture design, working at a coarser grain, could
not have caught — the exact reason this project's own two-stage
architecture-then-contracts discipline exists.

**Reusing `IReportDefinition`/`IReportRenderer<T>`'s own precedent for
`ADR-0067` was a deliberate search, not a coincidence.** Before
proposing a new registration idiom, this Work Package checked every
existing Kind-keyed or Id-keyed registry this platform already ships
(`INavigationProvider`, `ICommandRegistry`, `IReportingService`) and
found `IReportingService`'s own definition/renderer split was the
closest structural match — confirming, again, that a genuine second
example of an already-solved problem is worth finding before designing
a third solution to it.

## Implementation Lessons

Not applicable in the usual sense — no implementation was performed.
The closest analogue: writing `IWorkspaceCommand`'s own contract
surfaced that context-menu filtering (`WP8.0A UI Architecture.md` §4)
does **not** need a new mechanism at all — the existing
`CommandDescriptor.CanExecute` predicate, closing over the new
`IWorkspaceContext.CurrentSelection`, already does the entire job. The
first draft of this contract gave `IWorkspaceCommand` its own
`IsApplicable(selection)` method, duplicating `CanExecute` — caught and
removed before this document was finalised, once the ambient-context
design (`IWorkspaceContext`, mirroring `ICurrentPrincipalAccessor`) made
the duplication visible. `IWorkspaceCommand`'s own final, narrower
purpose (tagging a command with `TargetObjectId`/`TargetKind` so generic
Workspace infrastructure can auto-refresh an open view after a
successful dispatch) is a genuinely new, non-redundant capability the
first draft did not have.

## Repository Maturity

**Every dependency named in the twelve new contracts was checked
against the existing Platform Service surface before being accepted.**
`WP8.0B Dependency Rules.md` confirms zero new Platform Service, zero
new persistence mechanism, zero new pub/sub mechanism, and zero new
command-dispatch mechanism — the complete list of what is reused
(`INavigationProvider`, `ICommandDispatcher`/`ICommandRegistry`,
`IEventBus`, `ISettingsProvider`) was verified directly against each
service's own real, shipped interface, not assumed from memory. No
governance register required correction as part of this review.

## Recommendations for the Next Work Package

1. **An implementation Work Package should follow directly**, building
   the twelve frozen contracts in `Tempest.App.Workspace` exactly as
   specified — mirroring `WP 7.3A`'s own "implement the approved
   contracts exactly" discipline.
2. **Select a specific TUI library** as part of, or immediately before,
   that implementation Work Package — a narrower choice than `ADR-0066`
   itself required, needing no further ADR (`WP8.0B Workspace
   Contracts.md` §"Related Documents").
3. **Build the first `IWorkspaceViewFactory`/`IProjectExplorerNodeProvider`
   pair for Requirements** as the implementation Work Package's own
   proof of the extensibility mechanism (`ADR-0067`) — the Requirements
   Engine is the only Systems Engineering Foundation capability
   currently Implemented, making it the natural first real consumer.
4. **Revisit whether a fifth registration mechanism is needed for
   Property Inspector facet contribution** once a second engineering
   discipline's own `DisciplineSpecific` facets need presenting — not
   before, since only Requirements currently has any such facets to
   display.

## Key Takeaways

1. Designing multiple related contracts together, not in isolation, is
   what actually surfaces whether one architecture-stage "open
   question" was really two questions in disguise — `ADR-0067` could
   not have been correctly scoped without both `IWorkspaceView` and
   `IProjectExplorer` existing side by side first.
2. A contract's own first draft is allowed to duplicate existing
   infrastructure before that duplication is caught — the discipline
   that matters is catching it before the contract is frozen, not never
   drafting an imperfect first version.
3. Reusing an existing registration pattern (`IReportDefinition`/
   `IReportRenderer<T>`) for a structurally similar new problem is
   itself a decision worth actively searching for, not merely accepting
   if it happens to be remembered — this Work Package's own explicit
   search across every existing registry is what surfaced the closest
   match.

## Related Documents

`docs/releases/v0.8.0/WP8.0B Workspace Contracts.md` and its three
companion deliverables; `ADR-0066`; `ADR-0067`; `docs/academy/
02 Runtime Architecture/17-engineering-workspace.md`; `docs/academy/
03 Work Packages/WP8.0A-engineering-workspace-architecture.md`;
`docs/academy/03 Work Packages/
WP7.2C-requirements-and-verification-platform-contract-review.md` (the
format precedent this document follows).

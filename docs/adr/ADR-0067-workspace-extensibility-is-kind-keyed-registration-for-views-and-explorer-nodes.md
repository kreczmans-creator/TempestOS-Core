# ADR-0067: Workspace Extensibility Is Kind-Keyed Registration, for Both Views and Explorer Nodes

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0B` (Workspace
Contracts), 2026-07-30. Resolves the question `ADR-0062`/
`WP8.0A Workspace Architecture Document.md` §10 deliberately reserved:
how does a future Engineering Discipline Module give its own
`IEngineeringDocument` `Kind` a presentation in the Workspace, without
the Workspace itself ever needing compiled knowledge of that `Kind`?

## Context

`WP8.0A Workspace Architecture Document.md` §10 named this as a real,
necessary capability without designing it, deferring the interface
shape until a Contract Review Work Package could design it against the
concrete `IWorkspaceView`/`IWorkspacePanel` contracts it depends on —
those contracts did not exist yet at the architecture stage.
`WP 8.0B`'s own act of defining `IWorkspaceView` (object presentation)
and `IProjectExplorer` (object tree presentation) makes the shape of
this question concrete: **two** distinct extension points are needed,
not one — a future module must be able to contribute both "how does one
of my objects render" and "how does my area's own tree get populated,"
and neither is naturally reducible to the other (`IProjectExplorer`'s
own tree nodes are not `IWorkspaceView` instances; a `Category`/`Group`/
`Collection` node, per `WP8.0A Navigation Specification.md` §3.1, has no
backing `IWorkspaceView` of its own at all).

This platform already has a directly comparable precedent:
`IReportingService`'s own `IReportDefinition`/`IReportRenderer<T>`
registration pattern (`WP 6.0`) — a caller registers a definition and a
renderer keyed by the report's own `Id`, and `IReportingService` never
needs compiled knowledge of what any specific report actually contains.

## Decision

**Workspace extensibility is Kind-keyed registration, mirroring
`IReportingService`'s own established pattern, applied twice: once for
object presentation (`IWorkspaceViewFactory`, registered via
`IWorkspaceManager.RegisterView(string kind, IWorkspaceViewFactory)`)
and once for tree population (`IProjectExplorerNodeProvider`,
registered via `IWorkspaceManager.RegisterExplorerArea(string kind,
IProjectExplorerNodeProvider)`).** Both registries are keyed by the
identical `Kind` string an `IEngineeringDocument` (or a future,
non-engineering Workspace area) already carries — no new identifier
scheme is introduced. A future Engineering Discipline Module's own
composition code calls both registration methods once, exactly as it
already calls `INavigationProvider.Register`/`ICommandRegistry.
RegisterDescriptor` today (`WP8.0A User Workflow Diagrams.md` Journey
5).

Full contracts: `WP8.0B Workspace Contracts.md` §2 (`IWorkspaceManager`
registration methods), §3 (`IWorkspaceViewFactory`), §10
(`IProjectExplorerNodeProvider`).

## Consequences

**Positive:**

- `IWorkspace`, `IWorkspaceManager`, `IProjectExplorer`, and
  `IWorkspaceView` all remain fully ignorant of every concrete
  Engineering Core service (`WP8.0B Dependency Rules.md` §3's own
  forbidden-dependency rule) — the entire extensibility surface is two
  small registration methods and two small factory-shaped interfaces,
  not a compiled `switch` over known `Kind` values anywhere in the
  Workspace's own code.
- Directly reuses a pattern this platform has already shipped and
  proven (`IReportDefinition`/`IReportRenderer<T>`, `WP 6.0`) rather
  than inventing a new registration idiom — the seventh consecutive
  Workspace-adjacent decision to reach "reuse what exists"
  (`WP8.0B Dependency Rules.md` §6).
- A module registering only a view factory, only an explorer provider,
  or both, is a legitimate, unforced choice — nothing requires a module
  to support tree presentation if its own objects are better reached
  purely through Digital Thread links from elsewhere, for instance.

**Negative:**

- Two registration calls, not one, are required for a module to reach
  full Workspace presentation for its own `Kind` — a minor, disclosed
  ergonomic cost accepted because the two concerns (object rendering,
  tree population) are genuinely separate responsibilities
  (`WP8.0B Workspace Contracts.md` §10's own "one reason to change"
  rationale), not because combining them was overlooked.
- Duplicate registration for the same `Kind` (two modules both
  registering a view factory for `"Requirement"`, for instance) throws
  `DuplicateWorkspaceRegistrationException` rather than silently
  preferring one — a deliberate fail-fast choice
  (`FOUNDATION.md`'s own Fail Fast principle), not yet a config-driven
  override mechanism, since no real need for one has been demonstrated.

## Alternatives Considered

**A single, combined registration (`IWorkspaceKindProvider` supplying
both a view factory and a node provider together)** — considered and
rejected. This would force every registrant to implement both concerns
even when only one is relevant to its own `Kind`, and would couple two
independently-varying responsibilities into one interface, contrary to
`FOUNDATION.md`'s own "one reason to change" principle applied
consistently elsewhere in this Work Package's own contracts (§3, §10 of
`WP8.0B Workspace Contracts.md`).

**Attribute-based or reflection-driven discovery** (scanning loaded
assemblies for types marked with a `[WorkspaceView("Requirement")]`-
style attribute, mirroring `[ModuleMetadata]`) — considered and
rejected. Every other Workspace-adjacent registry this platform has
built (`INavigationProvider`, `ICommandRegistry`, `IReportingService`)
uses explicit, imperative registration calls, not attribute scanning;
introducing a second registration idiom for the Workspace specifically
would be inconsistent with no offsetting benefit — explicit registration
calls are already this project's own established, proven pattern.

**Deferring both registries to implementation, leaving `ADR-0067`
reserved** — considered and rejected. `WP 8.0B`'s own act of defining
`IWorkspaceView`/`IProjectExplorer` already makes the extensibility
question concrete enough to answer now; deferring it further would
leave the twelve contracts in `WP8.0B Workspace Contracts.md`
internally incomplete — `IWorkspaceManager` would have no stated way to
satisfy the very extensibility `WP8.0A Workspace Architecture
Document.md` §10 named as a real requirement.

## Related Documents

`ADR-0062`; `WP8.0A Workspace Architecture Document.md` §10;
`WP8.0B Workspace Contracts.md` §2, §3, §10; `WP8.0B Dependency
Rules.md`; `docs/architecture/Platform Service Map.md` (Reporting
entry, `IReportDefinition`/`IReportRenderer<T>` — the pattern this
decision mirrors).

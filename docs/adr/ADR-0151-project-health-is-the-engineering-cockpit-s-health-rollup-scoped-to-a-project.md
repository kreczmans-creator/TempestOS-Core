# ADR-0151: Project Health Is the Engineering Cockpit's Health Rollup Scoped to a Project

## Status

Accepted — Product Owner request ("RAG built into the Core system visuals
giving us the 'project health' view"), 2026-09-21.

Builds on `ADR-0069` (the Cockpit) and `ADR-0103` (its collaborators);
amends `ADR-0150`'s export schema note (`programme.json` is now v2).

## Context

`WP 8.1C` gave the Engineering Cockpit a "Project Health Dashboard": five
per-discipline statuses (Requirements, Calculations, Verification,
Documentation, Manufacturing), one rollup (`EngineeringCockpit.Health`)
and one score line (`HealthScoreDisplay`). Its vocabulary,
`EngineeringHealthStatus { Unknown, Healthy, Attention, Blocked }`, is
already the platform's RAG: the desktop colours it grey/green/amber/red
(`HealthColors`) and the Dashboard Export writes it lower-cased.

Two facts shaped this decision:

1. **"Project" there meant the whole workspace.** Every discipline
   read-model lists all live objects, so one Failed activity anywhere
   turned every project red. The owner wants health per Project.
2. **Project membership already has one definition.**
   `ProjectMembership.ResolveOwningProjectAsync` walks the structural
   parent chain to the owning `"Project"`; a requirement is in a project
   by `ProjectRequirementRegister`'s rule (a relationship targets a
   member). Neither needed a new field.

## Decision

**1. RAG is `EngineeringHealthStatus`.** No second Red/Amber/Green enum,
no dashboard-side translation. The words stay Healthy/Attention/Blocked/
Unknown on the desktop, in `engineering-status.json` and in `programme.json`.

**2. Project health is a read model, never stored state.**
`ProjectHealthReadModel` (an `ADR-0103` collaborator constructed by
`EngineeringCockpit`, loaded last in `PrimeAsync`) reports one
`CockpitProjectHealth` per live Project through
`EngineeringCockpit.ProjectHealth`/`GetProjectHealth`: the five discipline
statuses, the overall rollup, the score text, and the Project's own
blocked-item and overdue-action counts. `WP 19.0A` may add client, dates
and a PM to `Project`; health stays derived.

**3. Scoping is the discipline read-model itself, filtered.** Each
`*CockpitReadModel` gains `ScopedTo(includes)`: the same class over a
filtered copy of the data `LoadAsync` already loaded — no second load, no
second rule. The rollup, the score wording and the overdue rule are
stated once, in `EngineeringHealthRollup`, and both the workspace figures
and every per-project figure call it. The two can differ only in scope.

**4. Membership costs one in-memory walk per candidate object.** Every
object the disciplines loaded, every requirement relationship target and
every live Task is resolved once per prime through `ProjectMembership`
(dictionary lookups over the in-memory graph, O(objects × depth), no
persistence I/O, no projectId column, no index).

**5. Two surfaces.** The Cockpit's "Recent Projects" card becomes the
"Project Health" card (same card component; dot, word and score per
row). `programme.json` becomes schema v2: `projects[].health`
(`overall`, `byDiscipline`, `score`), `projects[].blockedCount`,
`projects[].overdueActionCount` and `summary.byHealth`, every v1 key
unchanged. *Later note (2026-09-21):* still within v2, additively,
`projects[].tasks[]` (the Project's open tasks, from the Project
Workspace's own `ProjectTaskRegister`; `overdueActionCount` and the
tasks' `isOverdue` count agree by construction) and `summary.tasks`
(`open`, `overdue`, `blocked`) — see `ProgrammeHierarchyExportAdapter`'s
remarks.

## Consequences

**Positive.** Project health is computed by Core, once, and shown and
exported verbatim; it cannot disagree with the workspace Cockpit in rule.
`ProgrammeHierarchyExportAdapter` now reads a headless
`EngineeringCockpit` exactly as `EngineeringStatusExportAdapter` does.

**Negative.** Each prime adds the membership walk. The adapter now needs
the Cockpit's dependencies. The Project Explorer's Project node carries no
health indicator yet: `ProjectExplorerNode` has no decoration slot and
node providers are built synchronously from the object alone, so that is
new infrastructure, deferred.

## Alternatives Considered

**A stored health field or projectId column.** Rejected: derivable state
that would drift from the objects it summarises.

**Re-loading each discipline read-model per Project.** Rejected: N×
per-object I/O per prime, and two loads that can disagree.

**Deriving health on the dashboard.** Rejected: the owner asked for Core
to compute it, and the Pi renders what the desktop computes (`ADR-0150`).

## Related Documents

`ADR-0069`; `ADR-0103`; `ADR-0150`; `WP 8.1C`; `WP 19.0A`;
`src/Tempest.Workspace/Workspace/ProjectHealthReadModel.cs`;
`src/Tempest.Workspace/Projects/ProjectMembership.cs`.

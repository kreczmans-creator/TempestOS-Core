# Project Timeline, Risks, Issues & Decisions

**Release:** `v0.14.0` (Product Convergence & Recovery programme, 2026-08-30)
· **Work Package(s):** Project Risks, Issues & Decisions; Project Timeline ·
**Debt:** `TD-81` (further narrowed) · **Decision:** `ADR-0117` (reused, not
amended) · **Code:** `Tempest.Core.EngineeringDomain.GovernanceWorkflow`,
`Tempest.Workspace.Projects.ProjectGovernanceRegister`/
`ProjectMilestoneRegister`, `Tempest.Desktop.Views.ProjectRisksView`/
`ProjectTimelineView`

**In plain terms.** Every real project accumulates things that are not tasks:
dangers that might happen (risks), things that have already gone wrong
(issues), choices the team made and why (decisions), and dates the project has
promised to hit (milestones), with the pieces of work due by each date
(deliverables). TempestOS already knew how to store all five kinds of record
forever; nobody could actually raise a risk, close an issue, take a decision,
or set a milestone from inside the product, because the screen for it did not
exist. These two Work Packages built that screen — honestly, without inventing
a project-scheduling tool the platform does not have.

## Two Work Packages, one shape already proven

`45d8a99` gave Risk, Hazard, Issue and Decision a workflow and a surface.
`f9fd9e0`, the same day, gave Milestone and Deliverable theirs. Both follow
`41-project-tasks-and-delivery-workflow.md`, which closed Tasks the release
before and left `TD-81` naming Risks and Timeline as still Declared. Neither
invents a new pattern: both apply what `ADR-0117` established for tasks — a
family-specific status mapped onto the canonical lifecycle through
`IFamilySpecificState`, membership read through the parent chain — for the
second, third, fourth and fifth time this release.

## The model was real; the workflow was not

`Risk`, `Hazard`, `Issue`, `Decision`, `Milestone` and `Deliverable` have all
been real, compiled, persistable, rehydratable domain types since `WP 8.2C`.
Both Work Packages checked that directly rather than assumed it. For Timeline
the check was blunt: not one construction of `Milestone` or `Deliverable`
existed in `src/` outside the sample harness, which no longer ships (`TD-75`).
For Risks it was worse than "unused" — it was "unusable": `Issue` had no
status, priority or owner; `Risk` had `Likelihood` and `Severity` as immutable
strings set once at creation; `Decision` had an immutable `Rationale`. A
surface built over exactly what existed would have been a Risks area where
nobody could close a risk or triage an issue — the "claims a capability it
does not have" defect class `TD-102` names, arrived at by building too little
rather than by lying about what was built.

## Three vocabularies, one shared meaning

The fix is the one tasks used: give each family mutable state of its own
rather than borrow `LifecycleState`, and map every value onto the canonical
lifecycle so cross-domain code still gets one answer per Kind.

```csharp
public enum RiskStatus { Open, Mitigating, Accepted, Closed }
```

The distinctions are the point. `Accepted` is not `Closed` — an accepted risk
is still live, because the team chose to carry it, and folding that into
Closed would hide exactly the risks a review needs to see. An issue's
`Resolved` still counts as open — a fix nobody has confirmed is still
somebody's problem. A decision's `Superseded` is the one genuinely terminal
state of the nine declared across the three families: bringing one back would
rewrite what the project decided and when. Every other state can be reopened,
because risks and issues recur.

`Likelihood` and `Severity` stay free text rather than becoming an enum,
deliberately — every organisation scores a risk on its own scale, and freezing
one in would make the field wrong for anyone using a different one. What
changed is only that they can now be *set*.

Priority is shared, not duplicated, the same way. Tasks already had
`TaskPriority`; issues needed the same four values, and `ADR-0105` says one
canonical declaring class per value — so `TaskPriority` became `WorkPriority`
(17 usages renamed across 5 files, `EngineeringTask.Priority`'s own type
included) rather than a second enum "High" could drift away from.

## Membership and one area, three registers

No risk, issue, decision, milestone or deliverable model carries a
`ProjectId`. Each belongs to a project the way documents, requirements and
tasks already do: through `ProjectMembership`'s durable `IHasParent` chain — a
risk raised against a Part inside an Assembly is a project risk three levels
down, because the platform already answers that question, and a field
repeating the answer would be a second one, free to disagree with the first. A
deliverable adds one more link in the same style: parented to its milestone,
not to the project, which is what makes it a project deliverable
*transitively* rather than by a presentation-only grouping.

Risks, Issues and Decisions share a single project-workspace tab, because
`ProjectAreas` had always described it as "risks, issues and decisions for
this project" — three switchable registers inside one area, read in a single
pass over the same membership result rather than walking the parent chain
three times for one screen. `Assumption`, the fifth type in the same family,
is deliberately not surfaced: the other four each have a workflow a team runs,
while an assumption is only ever standing or invalidated, and inventing a
status vocabulary for it would have been designing a capability nobody asked
for. `FCR-0056` now reads **Implemented** (2026-08-30) with `Assumption`'s
omission stated in its own Notes field, rather than left looking forgotten.

## Milestones, deliverables, and what the Timeline will not claim

`Milestone` keeps its immutable `TargetDate` — no setter, so rescheduling is
disclosed as absent rather than faked. Creation exists because without it the
surface would be permanently empty (`TD-102` again); a milestone's title and
description can be edited afterwards, but its date cannot. A task reaches a
milestone by either of two routes, and the register keeps the difference:
`ContributesTo`, one relationship kind, points at either a Milestone or a
Deliverable, because a Deliverable already knows its own `MilestoneId` —
reaching the milestone through it is a read, not a second link, and which
route the work took is kept on the entry rather than merged into one
indistinguishable count.

`Milestone` also has no "achieved" state, so the register does not invent one.
It states only what it can know:

```csharp
public bool HasLinkedWork => Contributions.Count > 0 || Deliverables.Count > 0;
public bool IsPastWithOutstandingWork => IsPast && HasOutstandingWork;
public bool IsPastWithNothingLinked => IsPast && !HasLinkedWork;
```

A date past with open work against it, and a date past with nothing ever
attached to it, are different failures — a project running late against one
that was never engaged with — and a reviewer needs to tell them apart rather
than see one flattened "overdue" flag cover both.

## What the new tests found, and how they proved it

An issue's originating risk was read by filtering the issue's own *outgoing*
relationships for a link pointing at itself — structurally impossible, since
`GetRelationshipsAsync` returns outgoing edges only, so every issue silently
reported no originating risk. The fix reads the relationship repository's
incoming edges instead, since the risk is the side that writes the link:

```csharp
var incoming = await _context.RelationshipRepository
    .GetIncomingAsync(issue.Id, cancellationToken).ConfigureAwait(false);

return incoming
    .Where(r => r.RelationshipKind == GovernanceRelationshipKinds.Realises)
    .Select(r => (Guid?)r.SourceId)
    .FirstOrDefault();
```

A second finding came from a mutation, not a failing test: dropping the
persist call from `Risk.ScoreAsync` survived its first run, caught only by the
test reading the state store directly — a later restart test's own persist had
re-captured the whole state and masked the omission. Chapter 41 found the
identical shape of gap in the task family; restart tests and persistence tests
stayed in separate suites for exactly that reason.

Both surfaces also reuse the rendering discipline chapter 41 established:
acceptance journeys that show the real `MainWindow`, relaunch it, and read
view controls with `.Distinct()` before `.Single()`, since a shown
`TabControl` materialises its selected tab's content twice. Journeys are named
for what they prove —
`Journey_RaiseARisk_ScoreIt_OwnIt_MitigateIt_ThenRelaunch`,
`Journey_SetAMilestone_AddADeliverable_LinkWork_ThenRelaunch` among them — and
each asserts the area's descriptor reads `Implemented` and draws no
declared-capability card, not only that the domain objects changed underneath.
Eight mutations ran against the governance work and eight were killed; six ran
against the timeline work and six were killed, both suites reporting 0
warnings and 0 errors under `TreatWarningsAsErrors=true`.

## What was deliberately not built

No Gantt chart, no time axis, no scheduling engine, no dependency graph
between milestones, no critical path, no rollup of dates from tasks upward, no
enumerated Likelihood/Severity scoring scale, no status vocabulary for
`Assumption`, and no rescheduling of a milestone once set. The Timeline lists
what exists and what is attached to it; it does not plan, forecast or reorder
anything.

## How `TD-81` changed

Before these two Work Packages, `TD-81` still named Commercial, Resources,
Knowledge, Administration, Timeline/Gantt and a managed Milestone surface.
After them, both commits record the same narrower remainder: Commercial,
Resources, Knowledge, Administration, and "Gantt/scheduling proper".
`ProjectAreas` now marks both `Risks` and `Timeline` **Implemented**, leaving
only Reports and Settings `Declared` in the project workspace's tab strip.

`BACKLOG.md`'s own live `TD-81` row, written by the later `WP 17.0B` register
triage, has not caught up: it still reads "Whole mock-up modules
unimplemented: Tasks, Commercial, Resources, Knowledge, Admin" — wording that
predates all three closures, Tasks included. The two Work Packages' own commit
messages are the more precise record of what `TD-81` covers today; the Backlog
row is cited as it stands, not corrected, since narrowing a stale register
entry was not this chapter's brief.

## What to take away

- **A capability that cannot finish its own workflow is the same defect as
  one never built** — extending the model honestly beats shipping a surface
  that cannot close what it opens.
- **Distinct per-family status vocabularies, mapped onto one canonical
  lifecycle, preserve distinctions a single shared vocabulary would
  flatten** — Accepted-but-live, Resolved-but-open and Superseded-but-
  terminal are three different facts, not one.
- **State only what the underlying model can actually know.** A date passed
  with nothing attached and a date passed with work still open are different
  failures; one "missed" flag covering both would have thrown that
  difference away.

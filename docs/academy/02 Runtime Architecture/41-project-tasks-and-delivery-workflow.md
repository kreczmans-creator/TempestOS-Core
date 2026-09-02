# Project Tasks & Delivery Workflow

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-81` (partially resolved — Tasks only) ·
**Decision:** `ADR-0117` ·
**Code:** `Tempest.Core.EngineeringDomain.TaskWorkflow`,
`Tempest.App.Projects.ProjectTaskRegister`/`ProjectTaskService`,
`Tempest.Desktop.Views.ProjectTasksView`

## The model was already there

`EngineeringTask`, `EngineeringAction`, `Milestone` and `Deliverable` had
been real, compiled, persistable domain types for several releases. Nothing
in the product created one, assigned one, dated one, or reported on one.

So this was not a modelling exercise. It was giving an existing model a
workflow, a surface and a place in a project — while resisting the pull to
build a second, more convenient task model beside the one already there.

## The decision that shaped everything: status

The obvious answer is `LifecycleState`. Every engineering object has one,
there is a transition table, the plumbing is done.

It is the wrong answer, and the reason is one line in
`LifecycleTransitionTable`:

```csharp
[LifecycleState.Released] = new[] { LifecycleState.Superseded, LifecycleState.Obsolete },
```

**Released cannot go back to Draft.** That rule is correct and load-bearing:
a released drawing silently becoming a draft again is exactly what an
engineering platform exists to prevent.

But a finished task must be **reopenable**. That is the same transition.

Two ways to resolve it:

1. Loosen the canonical table so tasks can reopen.
2. Give the task family its own vocabulary.

Option 1 weakens a rule protecting released engineering data across the
whole platform, in order to serve one family. It would have been fewer
lines. It would also have been the kind of change that looks harmless in a
diff and is discovered years later by someone wondering how a released
drawing became editable.

Option 2 turned out to be something the platform had already anticipated:

```csharp
public interface IFamilySpecificState
{
    string Name { get; }
    LifecycleState CanonicalEquivalent { get; }
}
```

Declared, documented — and never implemented by anything. `TaskWorkState` is
its first implementation. Todo, In progress, Blocked, Done, Cancelled, with
its own transition table that permits reopening, and a mapping onto the
canonical lifecycle so anything reasoning across the whole domain still gets
one answer per Kind.

**When a rule is in your way, check whether the codebase already predicted
the exception before you weaken the rule.**

## Cancelled is finished, not open

A small decision worth stating, because the alternative is a slow lie.

`Cancelled` counts as *finished* for the purposes of "is this open". An
abandoned task is not outstanding work. Counting it as open would make every
open-task figure in the product drift further from the truth every time
someone cancelled something — and nobody would ever notice, because the
number would still look plausible.

Same reasoning for overdue: a task is overdue only while it is **still
open**. The question a user is asking is "what still needs chasing", not
"what was late".

## Membership: the third time the same answer

Documents belong to a project through `ProjectMembership` — the durable
parent chain. Requirements belong through the allocation link the platform
already records. Tasks now belong through the parent chain too.

A `ProjectId` field on the task model would have been simpler to write and
faster to query. It would also have been a *third* answer to "what project
is this in", free to disagree with the other two the moment anything moved.

The test that matters here is the nesting one: a task hung on a Part inside
an Assembly inside a Project is a project task, three levels down, because
membership is transitive over an edge that already exists and is already
durable.

## The mutation that found a real gap

Twelve of thirteen mutations died on the first run. The thirteenth survived:

```csharp
// mutation: ChangeWorkStateAsync no longer persists
_workState = target;
return Task.CompletedTask;   // was: PersistStateAsync(cancellationToken)
```

Every test still passed. Not because the tests were thin — they checked
transitions, refusals, board columns, project isolation — but because they
all read **the object they had just changed**.

An in-memory read cannot distinguish *saved* from *set on this instance*.

The fix was a test that reads back what the state store actually holds:

```csharp
await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.InProgress);
Assert.Equal("InProgress", (await fixture.LoadStoredStateAsync(task.Id)).Type("WorkState"));
```

The desktop acceptance test would eventually have caught it via its restart,
but slowly and in a different suite. The lesson generalises: **when testing
that something was saved, read it from the thing that saves it.**

## Two test fragilities worth knowing

**A revision returns a new instance.** `ReviseAsync` constructs a fresh
object carrying the same state and re-registers it (`ADR-0113`). An
assertion holding the original reference reads a stale object — it would
pass while the product showed the old text, or fail while the product was
right. Re-read from the repository, which is what the surface does.

**`Single()` fails on a shown window.** Once a window is actually shown, a
`TabControl` materialises the selected tab's content through its presenter
as well, so `GetLogicalDescendants().OfType<T>()` yields the same control
twice. That is one surface enumerated twice, not two surfaces. `.Distinct()`
before `.Single()`. Found by rendering the window for real — every test that
had never shown a window passed happily.

## Rendering it for real

Logical presence is not visual presence. A control can exist, respond to
clicks, and render at 0×0 — indistinguishable to a user from missing.

So there is a test that shows the window, runs a real layout pass, and
asserts the task card and every action button have non-zero bounds. Writing
it turned up nothing broken, which is the outcome you want and not a reason
to skip it — the first draft of the probe *did* report 0×0 everywhere, and
the twenty minutes spent establishing that this was the probe's own missing
layout pass rather than a product defect is exactly the work such a test
does automatically from now on.

## What was deliberately not built

No Gantt. No scheduling engine. No resource or capacity planning. No
critical path, task dependencies, recurring tasks, swimlanes or WIP limits.
The board is a status board: five columns, every one rendered even when
empty, so it keeps its shape as work moves.

`TD-81` still names Commercial, Resources, Knowledge, Administration,
Timeline/Gantt and managed Milestone surfaces. Its row now lists them
individually rather than reading as a programme in flight, so nobody has to
guess how much of it Tasks closed.

One module delivered and said so beats a programme claimed.

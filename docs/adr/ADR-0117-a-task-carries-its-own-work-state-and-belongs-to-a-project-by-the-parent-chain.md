# ADR-0117: A Task Carries Its Own Work State, and Belongs to a Project by the Parent Chain

## Status

Accepted — `WP — Project Tasks & Delivery Workflow`, 2026-08-29. Builds on `ADR-0113` (`TD-85` persistence and rehydration), `ADR-0116` (the principal boundary), `ADR-0095` (the data-driven workspace layout) and the project-membership rule established for documents and requirements. Partially resolves `TD-81` — Tasks only.

## Context

`EngineeringTask`, `EngineeringAction`, `Milestone` and `Deliverable` have been real, compiled, persistable domain types for several releases. Nothing in the product created one, assigned one, dated one, or reported on one. Tasks was the last project area still honestly marked `Declared`, and `TD-81` tracked it as the P0 item of a per-module programme.

So the work was not "model a task". The model existed. The work was to give it a workflow, a surface, and a place in a project — without inventing a second task model beside the one already there.

Two questions decided the design.

**What is a task's status?** The obvious answer is `LifecycleState`, which every engineering object already has. It is the wrong answer, and the reason is `LifecycleTransitionTable`: the canonical lifecycle runs Draft → InReview → Approved → Released → Superseded → Archived, and **forbids Released → Draft**. That rule is correct and must stay — a released drawing silently becoming a draft again is exactly the kind of thing an engineering platform exists to prevent. But a task that is finished must be **reopenable**, which is the same transition. A task is also never "in review" on its way to being done.

**What makes a task belong to a project?** Documents answer this through `ProjectMembership` — the durable `IHasParent` chain — and requirements answer it through the allocation link they already record. A third answer would be a third place project membership could disagree with itself.

## Decision

**1. A task's status is a family-specific state, mapped onto the canonical lifecycle.**

`TaskWorkState` — Todo, In progress, Blocked, Done, Cancelled — with `TaskWorkStateTransitions` permitting Done → Todo/In progress and Cancelled → Todo. `TaskWorkStates` maps every value to a `LifecycleState` so anything reasoning across the whole domain still gets one answer per Kind.

This is the **first implementation of `IFamilySpecificState`**, a contract the platform declared and had never used. The alternative — loosening the canonical table so tasks could reopen — would have weakened a rule that protects released engineering data in order to serve one family. The mapping is deliberately lossy in one direction (Todo, In progress and Blocked all read as `Draft`) and nothing is lost by it, because the task keeps its own state; the canonical value is what cross-domain consumers read.

Cancelled counts as **finished**, not open. An abandoned task is not outstanding work, and counting it as such would make every open-task figure in the product slowly become a lie.

**2. `EngineeringTask` gained its state; no new task type was created.**

Work state, priority, due date and a mutable assignee live on `EngineeringTask` itself, captured in `CaptureTypeState` and read back by its own rehydrator, so a task persists and rehydrates through the production path `TD-104` established with no new registration. Every mutator follows the base class's existing mutate-then-`PersistStateAsync` pattern, which is what makes an assignment durable without this feature knowing anything about persistence. A record written before these fields existed comes back as an ordinary Todo at Normal priority — `TD-85`'s established "a missing field comes back visibly empty" rule.

`DueDate` is nullable. "No due date" is the honest state of most tasks most of the time, and defaulting one to the creation date would make every overdue figure meaningless. A task is overdue only while it is **still open**: the question a user is asking is "what still needs chasing", not "what was late".

**3. Project membership is the parent chain, exactly as it is for documents.**

`ProjectTaskService.CreateAsync` parents the new task under the project, and `ProjectTaskRegister` lists what `ProjectMembership` returns. A task hung on a Part inside an Assembly is a project task, transitively. **No `ProjectId` field was added to the task model** — it would be a competing answer to a question the platform already answers, the same reasoning that kept one off the requirements model.

Actions are listed alongside tasks, because an `IAction` *is* an `ITask` — one raised by a review. Splitting them into their own surface would hide work from the person whose job it is to see all of it.

**4. Ownership comes from the principal boundary, and is not a permission.**

`AssignToCurrentPrincipalAsync` reads `ICurrentPrincipalAccessor` through the domain context (`ADR-0116`). No authentication is performed and no permission is checked: assignment says who is *doing* a piece of work, not who is *allowed* to. Unassigned remains a first-class state and is reported as such rather than as a blank.

**5. One relationship kind links a task to what it is working toward.**

`TaskRelationshipKinds.ContributesTo` (`contributesTo`) points at a Milestone *or* a Deliverable. One kind for both, because a Deliverable already knows its own `MilestoneId` — a second kind would be a second answer to a question the domain can already answer. A task linked to a deliverable reports the date of that deliverable's milestone, since a deliverable has no date of its own.

**6. The surface is ordinary, and it decides nothing.**

`ProjectTasksView` is a plain `UserControl` in the project workspace's own tab host — no reserved slot, no task-specific window, no change to the `TD-72` layout architecture. It raises intent; `MainWindow` performs it through `IProjectTaskService`. The board offers only the moves `TaskWorkStateTransitions` actually permits from a task's current state, because a button whose only outcome is an error is worse than no button.

Every board column renders, including the empty ones. A board that drops "Blocked" when nothing is blocked reshapes itself under the user as work moves.

## Consequences

**Good.** Tasks created in the product are real engineering objects: durable, rehydratable, traceable, revision-tracked, and visible to the same project rules everything else obeys. The Cockpit's "Overdue Actions" card stops being an honest placeholder — it carried the note "no due-date field exists on any Task/Action Domain object yet" for as long as it existed, and that reason is now gone. An empty card means *nothing is overdue* rather than *we cannot tell*.

**Accepted cost.** `TaskWorkState` is a second status vocabulary in a platform that has one canonical lifecycle, and a reader now has to know which of the two a given surface is showing. That cost is contained by `IFamilySpecificState` — the mapping is declared in one table — and it buys a task family that behaves the way people expect tasks to behave.

**Deliberately not built.** No Gantt, no scheduling engine, no resource or capacity planning, no critical path, no dependencies between tasks, no recurring tasks, no swimlanes, no WIP limits, no drag-between-columns on the board. `Milestone` and `Deliverable` are reachable through one link and have no managed surface of their own. These are `TD-81`'s remaining scope and the row now names them individually rather than reading as a programme in flight.

**A gap this work found in its own tests.** A mutation that made `ChangeWorkStateAsync` skip its persist call survived: every assertion read the in-memory object it had just changed, which cannot distinguish "saved" from "set on this instance". Closed by a test that reads back what the state store actually holds. The other twelve mutations were killed first time.

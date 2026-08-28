# The Product Spine

**Programme:** Product Convergence & Recovery, 2026-08-28 ·
**Debt:** `TD-84`, `TD-85` · **Code:** `Tempest.App.Projects`,
`Tempest.App.Shell`

## Four tickets that were really one

A compliance audit found four P0 gaps: no global navigation, no project
context, no per-discipline engineering surfaces, no project modules. The
tempting reading is four features. The Product Owner rejected that
reading, and was right to:

> Conceptually, they are a single architectural deficiency: the Product
> Shell / Project Context layer is missing. Otherwise you risk building a
> Projects screen, a navigation rail, a project dashboard and some
> engineering links — without creating the underlying relationship
> between them.

That is the whole lesson of this Work Package. Four symptoms of one
missing layer, worked as four tickets, produce four surfaces that each
look right in a screenshot and do not compose. Worked as one deficiency,
they produce a spine.

They are now grouped as `TD-84` in the register, so the framing survives
the people who set it.

## What made this cheap

The engineering platform was already strong. The audit's own finding was
that TempestOS was *"an excellent engineering platform behind a modest
shell"* — so the correct move was to **connect**, not rewrite.

Two discoveries made the spine small:

1. **A project already existed as a domain object.** `IProject`
   (`Portfolio → Programme → Project`) already had lifecycle,
   relationships, traceability, metadata and revisions. Nothing needed
   inventing; the product layer needed to *use* it.
2. **Object ownership already existed.** `IHasParent`/`MoveAsync` already
   parents engineering objects to a project. "Engineering work belongs to
   a project" was already true in the domain and merely unexpressed in the
   product.

So the spine is three services and a shell rule — not a new domain.

## The one thing worth copying: navigation as a value

```csharp
public sealed record ShellLocation(ShellArea Area, Guid? ProjectId, ProjectArea? ProjectArea);
```

Navigation state as a **single immutable value** rather than flags spread
across views buys three things at once:

- **"Where am I" has exactly one answer.** Two surfaces cannot disagree.
- **The shell has one decision point.** `RenderCurrentModuleAsync` derives
  what is on screen from the navigator; there is nowhere else that decides.
- **It is testable without a UI.** A test asserts a location, not a
  sequence of control-visibility side effects. Most of this Work Package's
  coverage needs no window at all.

And one invariant carries the product decision into the type system:

```csharp
public Task GoToEngineeringAsync(CancellationToken ct = default) =>
    MoveToAsync(ShellLocation.ForEngineering(RequireOpenProject()), ct);
```

`RequireOpenProject()` throws when nothing is open. "Engineering happens
within a project" is therefore enforced by construction, not by
convention — and a mutation that relaxes it to `?? Guid.Empty` fails a
test immediately.

Note the ordering inside `OpenProjectAsync`: the **context opens first**,
then the location moves. A failed open cannot leave the shell pointing
into a project that was never opened. Ordering *is* the invariant here.

## Investigate before you reuse — and be willing to retire

The brief said to investigate `ProjectModel`/`ProjectService`/
`IProjectRepository` and "reuse where appropriate rather than duplicate".
Investigation is what makes that instruction useful, because the honest
answer was **reuse the concepts, retire the implementation**:

- `ProjectService` news up its own repository (`new JsonProjectRepository()`)
  — no DI.
- It writes folder trees straight to disk, bypassing `IPersistenceStore`,
  audit, revisions and lifecycle.
- `ProjectModel` is a mutable POCO with denormalised counts.

Wiring it in would have imported a pre-platform architecture into the
product's newest layer. The `P-NNNN` identifier scheme carried forward;
nothing else did. *"Reuse where appropriate" is a question, not an
instruction — and sometimes the appropriate reuse is none.*

The same discipline applied to project contents: the Project Workspace
**counts** the object graph rather than storing a count, precisely the
mistake `ProjectModel`'s `RequirementCount`/`CalculationCount` fields made.

## The boundary the journey found

The Definition-of-Done journey — launch, open a project, do engineering,
close, reopen — failed at the last step, and the failure was real:

> **The engineering object graph is in-memory by design** (`ADR-0077`).
> Documents persist; the objects reconstructed over them do not.

This is why acceptance journeys are worth writing. Every unit test passed;
the *product promise* did not hold. No amount of testing the parts would
have surfaced it, because it lives exactly between them.

The fix chosen was deliberately narrow: `ProjectDirectory` keeps a small
durable `Projects.Index` in `IPersistenceStore` — the identical pattern
`MaterialCatalog` already uses for the identical reason (`ADR-0055`). The
live object stays authoritative when it exists; the index is the fallback
that makes a project reopenable.

That is a **workaround for projects, not a fix for the boundary**, and it
is recorded as `TD-85` saying exactly that. Every other engineering object
still vanishes on restart. Naming the workaround as a workaround is what
stops it being mistaken for the fix in six months.

> **Postscript (`TD-85`, `ADR-0113`).** It did not take six months. The
> very next work package fixed the boundary — engineering object state is
> now durable and every persisted object is rehydrated at startup — and
> **deleted `Projects.Index` in the same change.** That is the other half
> of the discipline: naming a workaround honestly is what lets the next
> person find and remove it, and removing it is what stops it becoming a
> second source of truth. See
> `34-engineering-object-rehydration.md`.

## Proving it, not screenshotting it

The controlling instruction was blunt: *"Do not report success because the
screens exist. Prove the workflow."*

So the acceptance test asserts **state and surface together** at every
step — a window that renders a project-looking page without a real project
context fails, and a context that changes without the shell following
fails too. Four mutations were run against the finished spine:

| Mutation | Result |
|---|---|
| Engineering reachable with no project (`?? Guid.Empty`) | **killed** |
| Restore a saved location whose project no longer exists | **killed** (2 tests) |
| Open a project that does not exist | **killed** (3 tests) |
| Durable index not written on create | **killed** (unit *and* acceptance) |

The fourth is the one that matters most: it fails the acceptance journey,
not just a unit test. That is the difference between coverage and proof.

## What this deliberately did not do

Full docking, the workspace layout abstraction, the drawing viewer, the
remaining project modules and Companion integration are later objectives
in the programme's own stated order. The spine had to exist first — every
one of them attaches to it. Building any of them earlier because it was
more visually attractive is precisely the reversal the programme's
implementation order forbids.

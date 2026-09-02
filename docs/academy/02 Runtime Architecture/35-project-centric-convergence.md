# Project-Centric Convergence

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-89` (resolved for the spine) · **Code:** `Tempest.App.Shell`,
`Tempest.App.Projects`

## When the right decision is slightly too strong

The Product Spine (`TD-84`) took the product decision — *TempestOS is a
project-centric engineering operating environment* — and enforced it
completely: Engineering was reachable **only** from an open project, by
construction. That was deliberate, defensible, and tested. It was also
too strong.

The authoritative model has two branches, not one:

```
TempestOS
  ├── Projects → Project Workspace → Engineering → Engineering Objects
  └── Standalone engineering → Calculations / Calculation Sets
```

An engineer wanting to check a bolt in ninety seconds should not have to
invent a project first. "Project-centric" describes where serious work
*lives*, not a toll gate on the calculator.

> **The transferable lesson.** A principle enforced by construction is
> excellent when the principle is exactly right, and expensive when it is
> 90% right. Ours was 90% right. Watch for the case the principle was
> never really about — here, the two-minute calculation — before you weld
> the principle into a type.

## Scope is a value, not an absence

The naive fix is a boolean: `isStandalone`. The real fix is that
**`ProjectId` already is the scope**:

- `ShellLocation.ProjectId` non-null → engineering inside that project.
- `ShellLocation.ProjectId` null on `ShellArea.Engineering` → standalone.

That let the shell's load-bearing invariant be *restated* rather than
weakened. It used to read "a project-scoped area always carries a
project". It now reads:

> A location that **claims** a project must agree with the current
> project. A location that claims none cannot disagree with anything.

Same guarantee, wider truth. `IsProjectScoped` became `ProjectId is not
null` — derived from the data instead of switch-cased over the enum — and
standalone engineering needed no special case anywhere in the navigator.

> **The transferable lesson.** When new requirements strain an invariant,
> try to find the more general statement that implies the old one. If the
> new rule needs a special case in the enforcement code, you probably
> found a patch rather than the principle.

## One definition of "in this project"

Two things needed to answer "which objects belong to this project": the
project workspace's contents, and the Engineering Workspace's scope. Two
implementations would have drifted within a release.

`ProjectMembership` is the one answer. It walks the durable `IHasParent`
chain upward: reach a `Project`, you belong to it; reach nothing, you are
standalone. **No `ProjectId` column was added to the domain**, because the
structural parent edge already meant this and has been durable since
`TD-85`.

It also fixed a real bug found on the way: contents were previously
resolved from *direct children only*, so a Part inside an Assembly inside
a project was not "in" the project. A two-level product structure — the
normal case — looked almost empty.

> **The transferable lesson.** "Belongs to" questions attract new fields.
> Look first for the edge that already encodes the relationship; a second
> encoding is a second thing to keep in sync, and it will lose.

## Honest navigation beats both alternatives

The spine showed three modules, because those were the three the platform
could serve. That was chosen to avoid decorative navigation — and it made
TempestOS *look* like a three-module application.

There were three options, not two:

1. **Hide unbuilt modules.** Honest, but misrepresents the product.
2. **Show them working.** Dishonest.
3. **Show them, marked, with a real destination.** ✔

`ShellAreas` and `ProjectAreas` declare the designed module and area sets
*and* which have a capability behind them — as **application state a test
asserts**, not a caption a view renders. A test now fails if any declared
area is unimplemented without naming the debt item that tracks it.

> **The transferable lesson.** Make "not implemented yet" a typed,
> assertable fact. A placeholder screen decays into a lie the moment
> someone half-builds it; a declaration that a test checks cannot.

## The journey that could not previously exist

Journey 3 is the whole point of this pass:

> Launch → Engineering → standalone calculation → run it → save a
> calculation set → close → reopen → the work is back → still no project.

It runs through the real `MainWindow`, the real command dispatcher, and
the real calculation template registry, across two application lifetimes.
Under the previous spine, its second step threw.

Six mutations were run against this work and all six were killed —
including "make Engineering require a project again", which fails Journey
3 specifically. That is the check that the journey proves something.

## Phase 7: the discipline of not building it

The brief asked for the *minimum* refactor so navigation would not be
welded to today's compile-time 5×3 docking grid — and explicitly not to
build full docking.

The investigation's answer was: **nothing needs refactoring.**
`MainWindow` already hosts modules in a plain content seam, and
`DockingGrid` already lives strictly *inside* the Engineering surface. So
three tests were added to hold that seam — including one asserting the
docking grid is *absent* from the tree outside Engineering.

Finding that no change is needed, and then spending the effort on tests
that keep it true, is a real result. The temptation is to refactor
something anyway, to have produced code.

> **The transferable lesson.** When an investigation concludes "this is
> already right", pin it with a test that fails if it stops being right.
> That is how a lucky property becomes a guaranteed one.

## Related

`TD-89` · `TD-84` · `TD-85` ·
`docs/architecture/Product Spine Architecture.md` ·
`33-the-product-spine.md` · `34-engineering-object-rehydration.md`

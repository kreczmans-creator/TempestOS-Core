# The Engineering Calculation Workspace

**Release:** `v0.16.0` follow-on, 2026-09-07 (`WP 16.4B`) · fixed in
`v0.17.0` (`WP 17.0A`, `17.9.1`, `17.9.2`) · retained by `D-028` in
`v0.18.0` · **Debt:** `TD-159`, `TD-160` (partial), `TD-167`–`TD-170` ·
**Decision:** `ADR-0143`, `D-028` · **Code:**
`Tempest.Workspace.Engineering.BracketCalculationWorkbench`/
`EngineeringCalculationRegister`, `Tempest.Desktop.Views.EngineeringCalculationView`,
`Tempest.Core.Calculations`

**In plain terms.** An engineer sometimes needs to check whether a
bracket, a bolt or a beam is strong enough for the load it will carry — a
calculation. TempestOS could already do this correctly and keep an
honest, permanent record of it, but there was no screen where a person
could sit down and use it. This chapter tells the story of building that
screen, the mistakes two rounds of real reviewers found in it, and the
later decision to leave it exactly where it landed.

## A governed calculation with nobody at the keyboard

`13-calculation-framework.md` describes `ICalculationEngine` and
`ICalculationDefinition`, in the platform since `v0.7.0`. By `2026-09-07`
the bracket section check worked end to end through the host's own
composition; `TD-160` recorded that "the whole merged engineering
capability has no Desktop UI surface" — no screen reached it.

`3b36868` closed the gap in three layers and, deliberately, nothing else —
"no second calculation engine, no duplicated arithmetic, no business
logic in the Desktop layer." `BracketCalculationWorkbench`
(`Tempest.Workspace/Engineering`) composes four governed acts it does not
own — populate, verify-and-release, check, recover — formatting every
value with its unit before the Desktop sees it. `EngineeringCalculationView`
collects text and renders what comes back, parsing nothing itself.
`EngineeringCalculationCoordinator` is an `ADR-0103` collaborator whose
only judgement is which sentence goes on the status line.

Its own tests found two real defects: re-entry still called a superseded
material current, because traceability was read once at load rather than
re-read on entry; and the first attempt tried to *revise* a Released
record, correctly refused — a released record is superseded, not
corrected in place.

## One class of bug: registered for the test, not for the user

Sixteen minutes earlier, `5fd224c` had closed `TD-159`, a recurring shape
of bug. `TD-75` had moved the five calculation *definitions* into
`Tempest.Core.Calculations`, but the `RegisterDefinition` calls that make
each reachable stayed behind in
`EngineeringCalculationsWorkspaceSampleModule`, in `Tempest.Samples` — an
assembly nothing shipped references, though both test projects do. A
shipped Desktop run offered five templates and threw
`CalculationDefinitionNotFoundException` on every one, while the test
suite passed against the composition that happened to register them.

**A definition registered only where a sample or test assembly registers
it will pass every test and fail every user**, because the test process's
dependency graph is not the production host's. The fix,
`ProductCalculationCatalogue.RegisterAll`, runs from `TempestHost` itself
before the first module initialises — not the discipline's own
composition root, which runs *after* start-up and a module may
legitimately calculate while still initialising. The guard that pins it
starts a host with **no modules loaded at all**, so only the host itself
can have registered anything.

## From one calculation to a workspace

The second manual Windows review called the result *"a strong engineering
framework but a poor product"*: an engineer could run the bracket check
and could not see what calculations existed, select one, open one, or
tell which were usable here. The blocker, `TD-167`: persisted records
could not be enumerated, because the engine's own remarks recorded a
record as "never looked up later by a caller-chosen key" — true only
while its one consumer was the caller that had just run it. `c29faf0`
gave the engine a `Calculations.RecordIndex`, the same `IPersistenceStore`
index convention every reference catalogue already uses; `ListRecordsAsync`
returns `CalculationRecordSummary`, not full records, because a mixed
list needs each entry's own type.

That closed `TD-168`: a **Calculations** list of every persisted record, a
**Reference Library** list of materials by lifecycle state, and an
**Active calculation** panel of Inputs, Results, Traceability and
Verification, opened read-only. The catalogue lists all five calculations
and says, of the four with no governed entry point, that they are "not
available here yet" — the honest alternative to a Calculate button that
would reproduce `TD-159` on its own surface.

## Naming: giving a calculation the object it already had

`f9553f8` closed `TD-168`'s remaining half: a calculation could be run and
listed, but not named — the list showed a name and eight hex characters —
nor filed under a project nor retired. Nothing new was invented: the
platform already had a governed `Calculation` object with a display name,
a parent, a lifecycle, and already-registered rename, move and status
commands. `EngineeringCalculationRegister` gives each executed calculation
one of these objects, linked to its immutable record through the existing
`calculatedBy` relationship.

Retirement was deliberately not a delete. `IDeletable.DeleteAsync` is a
one-way door — nothing clears the flag, every read model filters the
object out — so using it would make traceable work vanish for good.
Retirement instead moves the object to the nearest terminal state the
canonical lifecycle table permits: `Cancelled`, not `Archived`, since the
table permits no `Draft → Archived` transition — opened as `TD-169` rather
than worked around, because widening that table is a board decision, not
one Work Package's.

## What review after review kept finding

The *first* manual Windows review, answered by `49c3b91`, found two
"placement and visibility errors in work that already existed", missed by
this build's own tests. The rail entry sat at the bottom, titled
ambiguously "Calculation"; retitled and moved beside Engineering, in
**rail order only** (the `ShellArea` ordinal is untouched, since
`ShellLocation` is persisted by it). And "Populate Material Library", the
action the whole workflow starts from, vanished the moment the library
held anything. Neither survived its own tests, because "a synthetic
`Button.ClickEvent` reaches a hidden control exactly as happily as a
visible one" — every click now asserts `IsVisible` and non-zero `Bounds`
first.

A Colour Review Board pass on the workspace commit, `6da6f6c`, then found
a calculation's project read as one `IHasParent` hop rather than asked of
`ProjectMembership` — the platform's single definition of that question
(`41-project-tasks-and-delivery-workflow.md` reached for the same one) —
and retirement as one unconfirmed click, as irreversible as the soft
delete it replaced; the first press now only describes what will happen,
the second acts. Naming a calculation's create-then-link step has no
compensation and cannot have one, so a failure there is now reported
precisely, by a typed exception naming what was created and what it
could not be linked to, rather than glossed — opened as `TD-170`. The
re-review, `f19e231`, then caught a regression the fix itself planted:
opening a different calculation cleared the warning but left the
retirement still armed, so the next press could retire unconfirmed — the
exact transition the confirmation existed to prevent. Every operation now
disarms it first. **A guard cleared only by the path that set it is not
a guard against every other path.**

`WP 17.0A` found the same class of bug one layer down, in layout:

```csharp
Grid.SetColumn(left, 0);
Grid.SetColumn(right, 1);
page.Children.Add(new ScrollViewer { Content = left, ... });
page.Children.Add(new ScrollViewer { Content = right });
```

The `Grid`'s actual children are the two `ScrollViewer`s; the column was
set on the `StackPanel`s inside them, so both sat in column 0 and the
right-hand panel drew on top of the left — "visible, enabled, non-zero
size" was still true of two controls printed over each other. The fix
moves `Grid.SetColumn` onto the viewers; `AssertNoSiblingOverlap` now
fails whenever two visible sibling controls' bounds intersect.

## `ADR-0143`: a calculation carries the revision it stood on

`BracketSectionCheckResult` took a bare material id, and a record's
properties can be corrected after the fact — so a stored calculation could
name the material it used and still not let anyone reproduce it.
`ADR-0143` gives the result a `ReferencePin(Library, RecordId,
RevisionNumber)` carried *inside* it, serialised and recovered with the
numbers rather than kept beside them. `ICalculationEngine` gains
`FindRecordAsync<TResult>`, so a stored record reads back as the typed
result it was — what makes a pinned revision reachable after a restart.
`GovernedBracketCheckService` alone resolves a material, refuses a Draft
one, and builds the pin; the ADR refuses to generalise this into a
resolver for every calculation, because each has a different right answer
for what is missing.

Amended the next day: the design-freeze review found any signed-in
principal could release *any* reference record (hazard `H8`). `WP 17.9.3`
gated verify and release behind `reference.verify`/`reference.release`
permissions through `IPermissionEvaluator` (`ADR-0044`), so the reviewer
named on a record is who the platform actually authorised.

## Retiring the JSON box; letting an engineer add a material

`WP 17.9.1` retired the object editor's old "Execute" section — a
template picker over a raw JSON textbox, "a developer seam" the first
Windows review met as the first thing offered on a Calculation. It stays
hidden, command wiring untouched; a Calculation now points instead to the
Engineering Calculations workspace, on the rail.

`WP 17.9.2` (`31bba97`) answered "no ability to add material in the
calculation page." `BracketCalculationWorkbench.AddMaterialAsync`
registers an engineer's own record as Draft, refusing a blank source, a
non-positive or non-numeric figure, or a duplicate designation — "a
record naming no source can never be released." The Inputs panel's
Material picker offers released records only.

## `D-028`: kept in place, not built on

After the second Windows smoke test of `v0.17.0`, the Product Owner asked
whether Tempest should be doing calculations at all, or should tag
evidence to work done elsewhere. The answer, `D-028`: TempestOS `v1` is a
client project system of record that evidence is tagged to. "Nothing new
computes in Tempest in `v1.0`." `WP 18.3A` — the expression grammar, the
cell-grid editor, the run-by-run diff — was withdrawn before it started.

The in-app surfaces were not removed, in the Product Owner's own words:
*"I'd rather have it in place and we can pivot and strip out later than
have to build it later."* The `v0.18.0` Execution Plan records the
consequence: no file under `Workspace/Calculations` changes, "none of
their 76 tests change in this release," and the material-release screen
stays in the workspace because the *Libraries* tab is where all five
reference libraries are reviewed and released — what evidence cites. See
`59-evidence.md` for what was built instead.

## What was deliberately not built

`ADR-0143` refuses a generic property-resolution layer, a
`CalculationBase` hierarchy, a second persistence mechanism and any
automatic approval — a calculation reports what the arithmetic found, a
person approves. `D-028` refused, in turn, anything that would compete
with a spreadsheet on the engineer's own desk: an expression grammar, a
cell-grid editor, a run-by-run diff, none ever started.

## What to take away

**A definition registered only in a sample or test assembly will pass
every test and fail every user, because the test process's composition is
not the production host's.** **A guard against a one-click irreversible
action must be cleared by every path that could bypass it, not only the
path that armed it.** **Real reviewers on real hardware find what a test
suite structurally cannot, and the fix each time was to make the test
drive the same thing a person would.**

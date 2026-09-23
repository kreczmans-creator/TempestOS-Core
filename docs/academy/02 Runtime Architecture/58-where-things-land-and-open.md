# Where Things Land and Open: the First Windows Reviews

**Release:** `v0.17.0` · **Work Package(s):** `WP 17.9.1` (`92c2870`,
`45692b6`) · `WP 17.9.2` (`31bba97`) · `WP 17.9.3` (`dfc913c`, `cd71441`,
`42b88ac`) · `WP 17.9.4` (`9e52a53`, `b94c438`) · Part-model finding
(`62fe312`, `97dca85`, `9abd02c`) · **Debt:** `TD-172`, `TD-173`, `TD-174`,
`TD-175`, `TD-41` · **Decision:** `ADR-0143` (amended) · `D-028` · **Code:**
`Tempest.Desktop.Composition.WorkspaceDockingComposer`,
`Tempest.Core.Identity.IPrincipalDirectory`,
`Tempest.Core.Commands.CommandContext`/`CommandResult`,
`Tempest.Workspace.CreationPlacement`,
`Tempest.Workspace.Mechanical.MechanicalCreateParentPolicy`,
`Tempest.Workspace.DisciplineAreas`,
`Tempest.Desktop.MainWindow.OpenCreatedObjectAsync`

**In plain terms.** Before this release, nobody had sat at a real Windows
computer and used the finished software the way a customer would. Thousands of
automated checks were passing, but a check only asks the question someone
thought to write down. On 8 and 9 September 2026 the Product Owner did that,
and objects vanished, panels went missing, and a Windows security code
appeared where a person's name should have been. This chapter covers those
sessions, the fixes that followed, and a deeper discovery — a "Part" was
missing what an engineer needs from it — that changed what the next release
was allowed to build.

## What thousands of tests could not see

Core alone ran 4,961 tests, green, on a transactional database, one commit per
change, a real unit system — all proven
(`52-units-as-a-runtime-dimension-vector.md`, `53-sqlite-persistence.md`,
`54-one-transaction-per-engineering-change.md`). None had ever been driven by
a person clicking through the finished application on its own machine. The
Product Owner's own Windows session — the physical review
`06 Engineering Standards/08-the-physical-review-and-the-release-gate.md`
describes — was that first drive; the design-freeze review
(`05 Case Studies/07-the-design-freeze-review.md`) followed it the same
day. What the session found in the first hour was not subtle: Engineering
showed only the Cockpit, full width; a newly created Part could not be found
anywhere; a person's name read `S-1-5-21-…`. No headless test had ever been
positioned to catch any of this, because nobody had asked whether it was
true.

## Panels, names and a box that should not have shipped (`WP 17.9.1`)

The first fix (`92c2870`) is
`WorkspaceDockingComposer.EnsureCorePanelsPresent`: entering Engineering, if
the Explorer or the Properties panel is missing, both are put back on their
home edge. The honest part is what it does not claim. A journey test,
`EngineeringEntryLayoutTests`, walks the Product Owner's exact route twice,
and both panels are present every time under the automated harness — whatever
hid them on that one machine never reproduced. Rather than chase a cause
nobody could see twice, the fix makes the outcome unconditional: **whatever a
saved layout says, entering Engineering restores both panels.** Saying so
plainly is the correct move — it tells the next person how much is known (they
will be there) and how much is not (why they vanished, once).

A Windows account identifier under "Last Revised By" is now a name, through
`IPrincipalDirectory.Describe` — session principal first, Windows account via
SID second, the id itself only if neither resolves — fully
`55-configuration-logging-and-the-session-principal.md`'s subject. The object
editor had shown a Bill of Materials section on every Kind, Project and
Calculation included, because every canonical object implements `IHasBomLine`;
it now shows only where the fields mean something. And the Calculation
editor's "Execute" section — a raw "Input (JSON)" box — is retired: hidden,
its wiring untouched, replaced by a note pointing at the Engineering
Calculations workspace, where a calculation is actually run, named and traced
(`50-the-engineering-calculation-workspace.md`).

## Landing where you stand (`WP 17.9.2`)

The second round (`31bba97`) fixed something worse than a missing panel:
"Create Mechanical Object", with a project open, reported success and created
a Part that could not be found anywhere. The Ribbon and Palette bindings never
passed a parent, and the Project Explorer only walks down from a Project
through parent links, so a parentless object had no path into the tree. It
existed. It was simply unreachable.

`CommandContext` had held only the current selection
(`43-one-way-to-run-a-command.md`); this round adds `ProjectId`, the shell's
open project. `MechanicalCreateParentPolicy` uses it: a new object goes under
the selected container, else the open project, else nowhere. Because a
parentless object can still exist, `MechanicalProductStructureNodeProvider`
now lists every one under a **"Not in any project"** node. Fixing this
surfaced three parentless objects the sample module had itself been seeding,
invisible until now. The round also let an engineer add their own material
record, entered as Draft through the same verify-and-release review a shipped
record uses.

## Every discipline, one rule (`WP 17.9.3`)

Overnight, the design-freeze review's own findings and a wider audit of every
discipline's Create command landed together (`dfc913c`, `cd71441`, `42b88ac`).
Documents and Calculations had the identical placement defect, so the rule was
pulled into one policy:

```csharp
public static Guid? ParentFor(CommandContext context, IReadOnlyList<string> containerKinds)
{
    var primary = context.Primary;
    if (primary is not null && containerKinds.Contains(primary.Kind, StringComparer.Ordinal))
        return primary.ObjectId;

    return context.ProjectId;
}
```

`MechanicalCreateParentPolicy` now calls this directly; Documents and
Calculations use it too. Requirements gained an **"Ungrouped"** node for the
same reason — something created with nothing selected must still be findable.

Two high-rated substrate defects were fixed the same night:
`ListChildrenAsync` replaced "copy every object and filter" — paid once per
tree node, and once by the delete guard under the write lock — with a
self-healing by-parent index; and `ReferenceReviewService` began requiring
`reference.verify`/`reference.release` permissions before a governed record —
the libraries `51-engineering-reference-data.md` covers — could be verified or
released by anyone merely signed in (`ADR-0143`, amended). Smaller findings
came from reading the surface, not a user report: Properties now shows a
parent by name, never a bare identifier; the editor hides an empty, disabled
Content box on a Kind that can never be revised; and a verification result now
also links from the Activity's own subject, so a Requirement's Verification
Coverage finally shows it (`TD-173`).

## Opening right up (`WP 17.9.4`)

The next morning's second smoke test found the placement fix was not enough. A
Part landed under the right project, then "disappeared into the ether": the
Explorer was on another tab, the project node was collapsed, nothing opened.
`9e52a53` moved the guarantee to the shell itself: **nothing a user creates
may drop out of sight.** `CommandResult` now carries `SubjectId`/`SubjectKind`
— every create handler reports what it made — and
`MainWindow.OpenCreatedObjectAsync` switches the Explorer to the right area
(`DisciplineAreas.AreaFor(kind)`), reloads it, reveals the object with every
ancestor expanded, selects it, and opens its tab. The Explorer stopped
collapsing on every reload, so expansion now survives a refresh.

A Requirement opens through its own discipline view here, not the generic
Object Editor, which still cannot resolve a Requirement (`TD-41` — open, still
open at `v0.18.0`, as the `CreatedObjectOpensRightUpTests` comment says
outright). The guarantee routed around that row rather than waiting for it.

The same round put the running build in the title bar — "TempestOS 0.17.0
(9e52a53)" — because the second smoke test had been run against a stale
clone's compiled executable inside the working tree, and nothing on screen
said so. `PHYSICAL_REVIEW.md` §3 now tells a reviewer to check that title
against `git log` first.

Worth naming honestly: the Release Notes' "known surface gaps" list, written
between `17.9.3` and `17.9.4`, still records this class as a `TD-172` residual
owed to `WP 18.1A`. `BACKLOG.md`'s register shows `TD-172` closed in full by
`17.9.3` and `17.9.4` together — the line outlived the fix that overtook it
twelve hours later.

## What a Part did not have, and `D-028`

Once objects opened reliably, the Product Owner could finally look at one. A
Part carried nothing a calculation or a drawing needs: `IPart.MaterialId` is a
bare string nothing sets, no standard-versus-custom flag, no part number, no
mass — and a "Bill of Materials" section that was really just the part's own
line in its parent's assembly.

The first proposal to fix this drifted toward a part-occurrence model —
definitions kept separate from where they are used, the shape a PLM (product
lifecycle management) system takes — within an hour of being written down
(`97dca85`). The Product Owner stopped it the same day:

> We need to be very careful here not to reinvent the system as an ERP
> system or a PLM system. This is not the intent.

The proposal was withdrawn before it was built (`9abd02c`); `TD-174` and
`TD-175` record the gap and the guard together. The next question went past
the Part model: "Is this the place for doing calculations? … Should Tempest be
a dedicated customer project management system that we then tag evidence to?"
`D-028` answered it: TempestOS `v1.0` is a system of record that evidence is
tagged to; calculations happen wherever an engineer already does them, and
Tempest records the result — files, references cited at the revision held, key
figures, its check, its issue to the client.

Applied to the Part model, `TD-174` and `TD-175` dissolve rather than become
their own Work Package: a material is cited on the evidence that used it,
never assigned to the Part, and a Part shows a read-only **Where used** with
no Bill-of-Materials *input* at all. `KindEditorDeclarations.Part()` at HEAD
carries exactly that shape; `Assembly()` keeps the editable BOM section,
because the bill of materials is authored on the assembly, not the part.
Deliberately absent from both: any part-occurrence model, multi-assembly usage
tracking, change control on a BOM line, or a procurement, supplier, cost or
stock field — an attribute earns its place only if a calc sheet cites it, a
drawing shows it, or the invoicing seam needs it.

## What to take away

- **A test proves what someone thought to ask; a person at a real keyboard
  asks what nobody thought to.** Nearly five thousand passing tests did not
  catch a missing panel, an unreachable Part, or a Windows SID, because no
  test had been written to look for any of them.
- **When a cause will not reproduce, guarantee the outcome instead of
  guessing at the cause.** `EnsureCorePanelsPresent` is honest about the
  limits of what was actually diagnosed, and still closes the gap.
- **A drift toward the wrong shape is cheapest to catch in the first hour,
  not the fifth month.** The Part-model proposal that drifted toward PLM
  was named and withdrawn the same day it was written.

## Postscript (release candidates, September 2026)

`TD-41` — a Requirement still opening the generic editor's empty body —
closes on the unreleased `v0.19.1` candidate: `WP 19.10I` (`31418a4`)
falls back to `IRequirementsService` when the domain repository has
nothing for the id, and a Requirement finally opens on its own real
body. The v0.19.1 rationalisation independently reverified `TD-172`
fully Closed, exactly as this chapter's residual note expected. The
"opens right up" guarantee is extended twice more: `WP 19.10P`
(`ca181b2`) registers the `IWorkspaceViewFactory` a Deliverable never
had; `WP 19.10Q` (`d46e910`) fixes New Project's own stale-filter gap,
unrelated to a missing factory. `WP 20.10A` (`e179616`), on `v0.20.0`,
gives a project a Details tab and Client/Rate card/PO fields at
creation. See `65-the-project-commercial-core.md`. None of this is
released.









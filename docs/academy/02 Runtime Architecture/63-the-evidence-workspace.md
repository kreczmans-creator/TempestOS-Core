# The Evidence Workspace: File Picker, Editor Declarations and Libraries

**Release:** `v0.18.0` · **Work Package(s):** `WP 18.2A`, `WP 18.9.1` ·
**Debt:** `TD-174`, `TD-175` · **Decision:** `D-028` ·
**Code:** `src/Tempest.Desktop/Views/EvidenceWorkspaceView.cs`,
`src/Tempest.Desktop/Views/LibrariesView.cs`,
`src/Tempest.Desktop/Files/AvaloniaFilePicker.cs`,
`src/Tempest.Workspace/Files/IFilePicker.cs`,
`src/Tempest.Workspace/Editors/KindEditorDeclaration(s).cs`,
`src/Tempest.Workspace/Workspace/Evidence/`

**In plain terms.** `59-evidence.md` explains what a piece of evidence
*is* — a governed record of a calculation done elsewhere. This chapter
covers the screen a person actually uses to create one: the button that
opens a real "choose a file" dialog, the page that shows a Part
differently from an Evidence record because they mean different things,
the tab where reference numbers (material strengths, fastener sizes)
get checked off as trustworthy, and two mistakes — found the first time
the finished release ran on a real Windows machine — that show why a
screen working in a test is not the same as a screen working.

## A rail entry beside Projects, not buried among the unbuilt

The global navigation rail (`GlobalNavigationRail`) is a fixed list of
modules declared in `ShellAreas`, each marked `Implemented` or
`Declared` (`NavigationAvailability`) — a Declared module still shows,
honestly, with what tracks the gap (`TD-81`, `TD-79`), so the rail never
lies about what today's build can do. `Evidence` joins `ShellArea` as a
new, appended member — the enum is persisted by ordinal, so a member is
only ever added at the end — and sits in the rail's own order **right
after Projects, above the five Declared modules**: evidence belongs
beside the project catalogue, not lost beneath a wall of "not built
yet". `MainWindow` wires the area to `EvidenceWorkspaceView` like any
other rail destination.

## A dialog a robot cannot click

Creating evidence starts with picking real files off disk, and nothing
in the shell had ever done that before — every existing attachment was
typed in by hand as metadata. That calls for a genuine "Open File"
dialog, provided by the operating system, not by TempestOS. The
problem: a computer running an automated test has no screen and no
mouse, so it cannot see a Windows or Linux file dialog, let alone click
a file in it — a test that needs one either cannot run, or has to fake
a real user sitting there.

The fix is a pattern worth knowing on its own: `IFilePicker` is an
**interface** — a plain description of "a thing that can be asked to
pick files", with no promise about *how*. The real application uses
`AvaloniaFilePicker`, which asks Avalonia's own storage layer to show
the operating system's dialog; the test suite uses `StubFilePicker`,
which hands back the bytes of a real file already on disk — no dialog,
no screen, nothing to click:

```csharp
public interface IFilePicker
{
    Task<IReadOnlyList<PickedFile>> PickFilesAsync(
        FilePickerRequest request, CancellationToken cancellationToken = default);
}
```

Everywhere else, "pick some files" means only "call `IFilePicker`" —
the Evidence workspace, the test suite and the running application all
call the identical method, none needing to know which implementation
answers. That is what "behind an interface" means, and it is Execution
Plan decision 6: put a real capability behind a described shape so a
test can supply a harmless stand-in without touching the code under
test. `MainWindow`'s constructor takes an optional
`evidenceFilePickerOverride`, `null` in the shipped application,
`StubFilePicker` in every journey test — the same seam
`WorkspaceHost`'s `sessionPrincipals` parameter already used for the
signed-in user. Two paths reach it: the Create button
(`evidence.create-from-files`) and dropping files onto the Evidence
list, reusing `ProjectExplorerView`'s own drag-and-drop handling — both
ask for a classification and an optional subject, then attach every
file's bytes in one transaction.

## A Kind's editor is a declaration, not a branch

Before this Work Package, `ObjectEditorView` decided what to show for an
object by branching on its Kind inside the rendering code itself. The
fix flips that: `KindEditorDeclaration` is data, not code — a Kind, an
ordered list of named `EditorSectionDeclaration`s, each an ordered list
of fields with a label, a control kind and whether it can be edited
here — held by `IKindEditorDeclarationRegistry` and looked up with a
`HasSection(key)` check.
`KindEditorDeclarations.RegisterAll` registers exactly four: Evidence,
Part, Assembly, Component — a deliberately narrow scope (Execution
Plan §6 risk table). Every other Kind — Requirement, Document,
Calculation and the rest — has none, so `ObjectEditorView` renders it
exactly as it always has; `PopulateBom`, `PopulateDescription` and
`PopulateWhereUsedAsync` each ask the registry first and fall back to
the old hard-coded Kind lists only when nothing is declared.

This is where `TD-174` and `TD-175` are actually closed. The design
freeze review found that once an object could finally be opened
(`58-where-things-land-and-open.md`), the first thing it showed was
that a Part carried none of what a calculation or a drawing needs from
it, and its "Bill of Materials" was really just its own line in its
parent's assembly. `D-028` re-scoped both debts rather than building a
Part-modelling Work Package: material is cited on the evidence that
used it, not assigned to the part, and a Part's declaration carries
**no Bill-of-Materials section at all** — only a read-only *Where used*
row built from `IHasParent.ParentId`. Assembly's declaration keeps the
editable BOM section; Component's matches Part's. Evidence's own
declaration is different again — Files, Subject, Citations, Declared
figures, its own Status/Check/Issue vocabulary, Audit — it is not a
mechanical object at all.

Three pickers do the actual pointing, all wired through
`EvidenceEditorSupport`: the citation picker (`CitationPicker`) lists
**released library records only** — an unreleased one is never offered,
matching the refusal `CiteEvidenceCommand`'s handler already enforces;
the subject picker (`SubjectPicker`) lists the project's own Parts,
Assemblies, Requirements and Deliverables and hands back only an id,
never a managed link (`D-028`: a tag, not PLM); the declared-figure
entry (`DeclaredFigureEntry`) collects a name, a role (input or result)
and a typed quantity with a unit drawn from the platform's own
dimension catalogues. Leaving any of the three unset in a test leaves
the matching action honestly unavailable, never run with nothing on
screen.

## Libraries beside evidence, not behind it

The *Libraries* tab (`LibrariesView`) is where the five governed
reference libraries evidence cites — Materials, Fasteners, Bearings,
Standards, Constants — get **Verify**red, **Release**d and, for
Materials, **Add**ed, through the one existing review flow
(`ReferenceReviewService`), never a second one. Execution Plan decision
7 put it here rather than as its own rail module: the records are what
evidence cites, so they sit beside it. Releasing a Draft record verifies
it first if needed, then releases it, as two separately audited acts;
citation only ever offers a `Released` record.

A related toggle lives in Settings, not here: a calculation's *Check*
can be recorded by anyone by default (a one-person consultancy has
nobody else to ask), but *Independent check required* — read and
written through `ISettingsProvider` under
`EvidenceService.IndependentCheckSettingKey` — refuses the record's own
author as its checker once switched on.
`64-independent-check-and-the-issue-sheet.md` covers the rule itself.

## The record that could not open

The first cut of this Work Package compiled clean but had one working
gap: nothing had registered an `IWorkspaceViewFactory` for the Evidence
Kind, so `IWorkspaceNavigation.OpenAsync("Evidence")` threw
`WorkspaceViewFactoryNotFoundException` — a created record could never
actually open. `fcffd48` fixes it by registering
`EvidenceObjectViewFactory`, producing a plain `EvidenceObjectView` —
not itself a rendered control, so
`WorkspaceViewCoordinator.BuildDocumentContent` falls through to the
Object Editor's own declaration-per-Kind rendering, exactly as it
already did for Mechanical Kinds.

The same commit disclosed a second fact rather than hiding it: the
Object Editor's document tabs live inside the **Engineering** module's
own docking layout, not on screen while the Evidence rail area itself
is showing. Opening a record from the Evidence list has to switch
modules first:

```csharp
private async Task OpenEvidenceRecordAsync(Guid id, string kind)
{
    await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
    await RenderCurrentModuleAsync().ConfigureAwait(true);
    await _viewCoordinator.NavigateToObjectAsync(id, kind).ConfigureAwait(true);
}
```

Without the switch, "opens right up" (the standing Product Owner guard
from `WP 17.9.4`) would technically be true and practically false — the
tab would exist behind a module nobody was looking at. The Release
Notes name this plainly as a Warning rather than let a user discover it:
*"Opening an evidence record from the Evidence rail switches to the
Engineering module … the record is on screen either way."*

## The tab that loaded nothing but the form

`WP 18.2A` shipped a `RefreshAsync` on `EvidenceWorkspaceView` that
reloaded the evidence list on entering the area and never asked the
Libraries tab to load anything. Every journey test that touched
Libraries called `librariesView.RefreshAsync()` by hand before asserting
against it — so every test passed, and the first real Windows run of
`v0.18.0` opened the tab to nothing but the Add Material form.

`1144b28` (`WP 18.9.1`) is the fix, and it is one line with a large
consequence:

```csharp
if (_libraries is LibrariesView libraries)
    await libraries.RefreshAsync().ConfigureAwait(true);
```

placed at the top of `EvidenceWorkspaceView.RefreshAsync`, so entering
the area loads both tabs the way a user experiences it. The commit also
**removed** the two tests' own manual refresh calls — leaving them
would have kept masking a future regression the same way — and added
`LibrariesTabLoadsOnEntryTests`, which reaches the tab exactly as the
application does: enter the area, render it, select the second tab,
assert every library is listed, with no refresh call of its own
anywhere in the test. It also made Add Material refuse a blank name or
designation with a plain sentence, instead of surfacing the argument
check's own raw message.

`8df3466`, the same day against the same first Windows run, closes a
sibling gap the same way: an earlier journey always chose "No subject",
so what the picker actually *listed* was never asserted. The new test
creates a real Assembly and Part through `mechanical.create`, opens the
picker from the editor's own "Change Subject" button, and asserts both
appear and that choosing one tags the record.

Both fixes carry the same lesson: **a test that sets up its own
prerequisites by hand can pass while the path a user actually takes is
broken.** A refresh call added by a test proves the view *can* show the
data; it does not prove the view *will*, unhelped, when a person just
clicks the tab.

## What to take away

- **A dependency a test cannot drive belongs behind an interface, with a
  harmless stand-in on the other side of it** — not skipped, not
  mocked away silently, named and swapped in deliberately.
- **A Kind's editor should be what it declares, not what a branch
  happens to render** — a registry of sections makes "does a Part show
  a BOM box" a fact you can read, not a behaviour you have to trace.
- **If your test sets up the very thing you are testing whether the
  application sets up, you are not testing the application.**

## Postscript (release candidates, September 2026)

On the unreleased `v0.19.1` candidate, `WP 19.4B` (`6710d64`) gives
every canonical Kind's Attachments section the Browse button and drop
zone this chapter describes only for Evidence. `WP 19.6A` (`e9aeabc`)
makes Libraries a real master and detail over `ReferenceRecordView`,
with all eight libraries listed (Manufacturing, Components and rate
cards newly wired). `WP 19.10P` (`ca181b2`) fixes the rehearsal's own
D15 finding on the same view: an empty library previously showed no
heading at all, and the eighth library's internal name,
"BusinessRateCards," now reads "Rate cards" on screen. `WP 19.7A`
(`2c3ce7e`) moves Evidence itself out of the global rail this chapter
describes and onto each project's own workspace, a tab beside Sign off.
See `72-real-files-and-real-records.md`. None of this has shipped.

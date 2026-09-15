# The Honest Rail and the Structure Tab

**Release:** `v0.19.0` release candidate (`release/v0.19.0`) and `v0.19.1`
release candidate (`release/v0.19.1`) — both unreleased; the rail's own
*contents* were superseded by `WP 19.7A` (see `71-the-shell-as-sketched.md`)
· **Work Package(s):** `WP 19.2B`, `WP 19.4A` · **Debt:** `TD-74`, `TD-81`,
`TD-73`, `TD-133` · **Decision:** Product Owner Decisions 2026-09-15 §3 ·
**Code:** `Tempest.Workspace.Shell.ShellArea`,
`Tempest.Workspace.Shell.NavigationAvailability`,
`Tempest.Desktop.Views.ProjectWorkspaceView`,
`Tempest.Desktop.Views.RibbonView`, `Tempest.Desktop.Views.GlobalNavigationRail`

**In plain terms.** The rail is the strip of buttons down the left of the
screen that gets you anywhere in TempestOS. For several releases it listed
modules — Tasks, Commercial, Resources, Knowledge, Administration — that
looked exactly as clickable as the real ones but did nothing behind them,
because nobody had built them yet. A menu that shows things which do not
work teaches a person not to trust the menu. This work took those five
modules out of the rail entirely rather than merely greying them out, moved
the real engineering tools inside a project's own "Structure" tab, and
turned two settings screens into proper places you navigate to. When the
Product Owner then tried it and found the engineering tools spilling out
over the project's own header, and a toolbar of "generic bits" sitting on
top of everything, the very next release contained the overlap and removed
the toolbar — while keeping the one rule that mattered throughout: never
show something that does not work.

## The rule this continues

`35-project-centric-convergence.md` had already rejected hiding unbuilt
modules and rejected faking them, and chosen a third option: show them,
marked "Declared", with a real destination that says plainly what is
missing. `WP 19.2B` (`99cc411`) found that the honest answer, weeks later,
was stronger still — **remove them**. A "Declared" card is honest the day
it is written; left in place for a release with nothing behind it still
growing, it reads less like candour and more like a promise nobody is
keeping. `TD-81`'s own backlog row says why: the two capabilities Reports
had claimed for itself — issued evidence sheets, project documents —
shipped as a real `ReportsView` rail area instead, so "no descriptor
anywhere still claims a capability with nothing behind it."

The enum members themselves were not deleted, only their rail descriptors:

```csharp
// `WP 19.2B` (`TD-81`): removed from the rail rather than
// dimmed — their descriptors leave `ShellAreas` entirely, so nothing
// in application state claims they exist any more. The members stay,
// unused, because `ShellLocation` is persisted by ordinal and a
// member once shipped is never removed or renumbered; a session
// restored pointing at one of them now falls back to Home
Tasks,
```

`ShellLocation` is saved to disk by the enum's numeric position, not its
name. Deleting `Tasks` from the enum would silently repoint every saved
session at whatever module happened to inherit its number next. Removing
the *descriptor* — the thing `RailModules` reads to draw a button — gets
the honesty without that hazard.

## Engineering moves inside the project it belongs to

The other half of `WP 19.2B`: Engineering leaves the rail as a destination
of its own and becomes a project's **Structure** tab. `ProjectWorkspaceView`
gained a placeholder host (`_structureHost`), built before the
ribbon-and-docking surface even exists, that `SetEngineeringSurface` fills
once the composer builds it and `ClearEngineeringSurface` empties again —
the same single control instance also serves standalone engineering at
Home when no project is open, never two copies of one surface. Reports
(issued sheets and project documents) and Settings (the old preferences
dialog's own sections) became real rail areas the same way `Tasks` and its
siblings left: as application state, not a caption. And below 1,200 logical
pixels (`DesignTokens.CompactShellWidth`, `TD-73`) both the rail and the
ribbon fold to icon-only, closing the long-standing complaint that neither
ever compacted at all.

Along the way, `WP 19.2B` also closed `TD-133`'s "edge tab-index block": a
node's own `TabIndex` now reserves a 100-index block instead of a bare
render-order position, so its graph edges — newly keyboard-focusable in
this same package — can sit right after it in tab order; render order
still decides the ordering, only the multiplier changed.

## Naming everything, and proving the rail actually works

Two structural tests came out of this package rather than one more
feature. `AutomationNameCoverageTests` walks a real running window's own
logical tree across every rail surface and every project tab, asserting
every `Button`/`TextBox`/`ComboBox`/`CheckBox`/`ListBox`/`TabItem`/
`GridSplitter` carries a real `AutomationProperties.Name` — the accessible
label a screen reader announces (`49-accessibility-baseline.md` covers why
that matters). It found 122 unnamed controls across the whole application
and named every one.

`RailSurfaceContractTests` runs six behavioural checks against every rail
entry, in the same order: **click** it and something real renders;
**select** something in it and the selection has meaning; **open** it and
usable content appears; **edit** it and state changes, through its own
command; **restart** and the state remains; **navigate away and back** and
it is coherent. Eight rail entries, each proving all six, table-driven by
one dedicated test method per surface rather than one generic driver —
because what "select" and "edit" mean genuinely differs between a
reference library, a recorded timesheet hour and an issued evidence sheet.
Reports has no command of its own, so its "edit" check is met honestly:
issuing an evidence sheet elsewhere while Reports is on screen, read live
through its own change-feed subscription, is the proof — not a fabricated
button.

## Two defects the merge found

Merging `WP 19.2B` found two real problems, both fixed before the branch
closed.

**The Structure tab's embedded surface steered the project tab strip.**
Avalonia's `SelectionChanged` is a *bubbling* event shared by every
`SelectingItemsControl`. Once the whole engineering surface — the Project
Explorer tree, the ribbon's own tab strip, the calculation pickers — sat
inside the Structure tab's content, any of those nested controls changing
selection bubbled up and fired the project tab strip's own handler exactly
as a real click would. With the Structure tab still named as the last
selection, that silently re-entered `GoToProjectAreaAsync(Engineering)` and
swapped the whole shell's module host away from whatever the user actually
had open — in the failing test, Timesheets, which then permanently lost its
live change-feed subscription. One guard fixed it: `if
(!ReferenceEquals(e.Source, _areas)) return;` — only a selection change
that originated at the tab strip itself counts, the same bubbled-event
discipline `DigitalThreadGraphView` already used for its own hit-testing.

**The Commercial section's name resolvers.** `WP 19.2B`'s own composer
wiring and a resolver already added to the release branch collided on the
same file. The merge commit kept the release branch's resolvers and
re-pointed `WP 19.2B`'s wiring at them, so the Commercial section shows the
client's actual name and the rate card's code — not a bare internal id —
through the real `IOrganisationCatalog`/`IRateCardCatalog` lookups.

## The Product Owner's first pass: the surface sits over other things

Testing the finished `v0.19.0` candidate, the Product Owner found two
problems `WP 19.2B`'s own tests had not caught. Comment 1: opening a
project's Structure tab rendered the engineering surface as a layer
overlapping the project's own tab strip, half-hiding it. Comment 3, in the
Product Owner's own words: *"remove this taskbar, lots of generic bits in
here that aren't offering anything or even relevant"* — the menu bar and
Quick Access Toolbar sitting above the ribbon, plus Deliverables,
Invoicing, Projects and Timesheets categories cluttering an engineering
ribbon that had no business showing them.

`WP 19.4A` (`1f82c3c`, `289b11c`) answered both. The cause of comment 1 was
`WP 19.2B`'s own fix for a different problem: a negative top margin on the
Structure tab's content, added to cancel an ambient page margin elsewhere
in the view so the embedded surface could reach the window edges. Escaping
that margin meant bleeding sixteen pixels upward — straight into the tab
strip's row. The real fix removed the ambient margin instead of fighting
it: the header now carries the page padding directly, the tab control
carries none, and every *other* tab adds its own padding back — so the
Structure tab's content is the tab's own bounds, with nothing left to
escape.

The layout walk gained a check for exactly this shape of bug, because the
existing sibling-overlap check could not see it — the tab strip and its
content are template parts of one control, never `Panel` siblings. A
synthetic test reproduces the old negative margin on a scratch
`TabControl`, and asserts the walk names "tab strip" in its findings on the
broken layout and stays silent once the margin is gone —
`TabStripContentOverlap_FailsOnTheOldNegativeMargin_PassesOnTheNewLayout`.

The menu bar (`MainMenuFactory`) was deleted outright, not hidden. Every
command that still mattered gained its one missing route: Reset Layout,
Toggle Theme, Macros and View Relationships moved to the Command Palette;
Undo and Redo already worked from Ctrl+Z/Ctrl+Y independent of any button.
And the ribbon gained `SetCategoryFilter`, an **allow-list** applied only
inside the engineering surface — Calculations, Documents, Evidence,
Manufacturing, Mechanical, Requirements and Verification, never
Deliverables, Invoicing, Projects, Timesheets or Quotations. An allow-list
rather than a deny-list on purpose: a future category this ribbon has
never heard of is excluded simply by never being named, with no second
edit required when the next business-scoped category arrives. The Command
Registry itself is untouched — the Command Palette still lists every
command regardless — so nothing was hidden from the application, only from
one ribbon that was never the right home for it.

## What was left unresolved, on purpose

Three menu entries went without a replacement: the View toggles for the
Explorer, Inspector and Output panels, the three layout presets
(Engineering, Review, Documentation), and About. A closed core panel
already comes back on re-entering Engineering or through Reset Layout, and
the presets had no user beyond the menu itself — but nobody had asked the
Product Owner directly, so Decision §3 records the answer as *"to be
confirmed once the application is open in front of the Product Owner"* —
deferred to acceptance, and still a Warning in the `v0.19.1` release notes.

## The rule outlives the shape

`WP 19.7A` reshaped the rail again, to the Product Owner's own sketches:
Home, Projects, Tasks, Engineering, Business, and nothing else — Evidence,
Timesheets, Invoicing, Reports, Engineering Calculations and Quotes all
folded inside one of those five as trees rather than standing as their own
rail buttons. `71-the-shell-as-sketched.md` covers that shell in full. What
did not change is the rule this chapter is about: the `v0.19.1` Execution
Plan's own second engineering decision states it as "the honest rail still
applies" — Electrical and Structural modules under Engineering, and any
dashboard panel with nothing behind it, are named in the interface as
future work rather than shown as if they worked. The rail's contents moved
twice in five days; the discipline that decides what a rail is allowed to
say did not move at all.

## What to take away

- **Removing an empty module is more honest than dimming it, and a
  "Declared" placeholder is only honest for as long as it stays
  temporary.** Left in place, it becomes exactly the lie it was built to
  avoid.
- **A layout bug that bleeds past a control's own template part will not
  be caught by a check for overlapping siblings — the check has to look at
  what actually renders, not at the boxes the layout system thinks it
  drew.**
- **An allow-list survives being forgotten about; a deny-list does not.**
  Naming what belongs, once, costs less over time than remembering to
  exclude everything that should never have been there.

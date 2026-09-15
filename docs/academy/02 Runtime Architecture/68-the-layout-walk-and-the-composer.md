# The Layout Walk and the Composer

**Release:** `v0.19.0` release candidate (`release/v0.19.0`, head
`947c50d`, superseded by `v0.19.1`); extended at `WP 19.4A` (`v0.19.1`
candidate), `WP 19.9.0` and `WP 19.10O`; its honest limit is named in
the unreleased `v0.21.0` Execution Plan — none of this is merged to
`main` · **Work Package(s):** `WP 19.3A`, `WP 19.3A-R1`, `WP 19.2A` ·
**Debt:** `TD-83` (the `WP 17.0A` overlap class), `TD-109` ·
**Decision:** `v0.19.0` Execution Plan, decisions 2 and 4 · **Code:**
`tests/Tempest.Desktop.Tests/Layout/LayoutWalkTests.cs`,
`tests/Tempest.Desktop.Tests/DesktopTestHelpers.cs`,
`src/Tempest.Desktop/Composition/MainWindowComposer*.cs`,
`src/Tempest.Desktop/MainWindow.cs`

**In plain terms.** A screen can look right in the one screenshot a
developer happens to take and still be broken at the window size a real
customer actually uses — text running past its edge, one panel drawn on
top of another, a tab nobody can click any more. This chapter is about a
robot that checks every screen in TempestOS automatically, at two window
sizes, every time the code changes, before a person has to notice
anything. It also covers something less visible but just as necessary:
the file that builds the whole application window had grown into an
830-line block nobody could safely touch, split here into four plainly
named steps. Neither change gives a user a new feature; both are why the
features already shipped keep working release after release.

`v0.19.0` shipped a much bigger rail, creating two risks at once:
nobody could look at every screen by hand before every merge, and the
file assembling the window those screens live in, `MainWindow`, had
become too large to change with confidence. `WP 19.3A` answers the
first, `WP 19.2A` the second — the same wave of the same plan, told
together here.

## What "in-process under headless Skia" means

"Headless" means running the real application without opening a visible
window on a real screen. "Skia" is the actual graphics engine Avalonia
(TempestOS's UI framework) paints with — the same one a real window
uses. The plan's own second decision names, and rejects, the
alternative:

> The layout walk runs in-process under Avalonia.Headless with Skia, not
> by driving the built executable through OS automation, which would need
> a dependency the programme does not name. Automation names are still
> what the walker uses to find things; screenshots come from the headless
> renderer.

"Driving the built executable through OS automation" means launching the
finished `.exe` and controlling it as a person would — a robot mouse
clicking real Windows controls, via a library such as FlaUI or
WinAppDriver. Neither exists in this repository, and adding one was
outside the plan. So `LayoutWalkTests` constructs the real `MainWindow`
inside the test process and drives it through the same navigation calls
the product's own rail buttons make (`IShellNavigator`,
`MainWindow.RenderCurrentModuleAsync`), letting the real rendering
engine paint the real controls. "Automation names" — the labels controls
carry for screen readers — are still how the walk finds things; nothing
is found or driven by a simulated click.

Commit `20a0412` walks ten rail entries and nine project tabs at two
sizes, 1600×900 and 1180×760 (the second below where the rail folds and
the ribbon compacts), checking that nothing overlaps a visible sibling
and nothing sits outside its parent's bounds — over the window's
**logical** tree, not its visual one, which failed on a `Window`'s own
background chrome, template plumbing every Avalonia window carries and
never a product defect.

## What the first runs found, and what R1 changed

Real screens, checked for the first time, produced real defects: a rail
title overflowing by 14 px ("Engineering Calculations" measured at
infinite width because its button aligned content `Left` rather than
`Stretch`; fixed, `4c6fe68`, with a hard `MaxWidth` and a tooltip
carrying the full name); a status-bar segment squeezed to zero width
whose text never shrank with it, so it overflowed instead of
disappearing; and a docking tab strip that measured every tab at its
natural width regardless of the room available, silently losing one near
the end. The last two were found and fixed together in `WP 19.3A-R1`
(`8c3f100`), which also fixed a bug in the walk itself
(`captureMethod ??= SaveFrame(...)` short-circuited after the first
screenshot, so only one of thirty-eight PNGs was ever actually written
though the run reported thirty-eight), and, more importantly, changed
the walk from throwing on the first finding to collecting every finding
before failing once, with the complete list — one run tells the whole
story instead of one screen at a time.

R1 also added two exemptions, each capped to the exact thing that
justifies it rather than widened generally. A docking pane's
`GridSplitter` legitimately extends its grab handle onto the pane beside
it, capped to the splitter's own arranged thickness, never more. A
control's own **negative** margin — an established technique elsewhere
in the product (`CockpitCardControl.AddAction`, `CockpitView`'s card
grid) for cancelling a margin placed somewhere else — is exempted only
to that exact magnitude, on that one edge. Sized instead to "whatever
the control asks for", either exemption would have let the very defect
described next slip straight through.

## A check only catches what it was written to look for

`WP 19.2B` gave a project's Structure tab an embedded engineering
surface, placed with the same "cancel an ambient margin" trick — except
this one bled sixteen pixels **upward**, over the project's own tab
strip. Every layout-walk run had passed. The likely-cause note the lead added
under the Product Owner's comment says exactly why:

> the layout walk passed because it checks bounds against parents, not
> against sibling overlap across the tab strip

A tab strip's header and a `TabItem`'s selected content are two
different template parts of the same `TabControl`, never `Panel`
siblings, so neither of the walk's two existing checks was ever
comparing them. Passing the walk had never been sufficient proof against
this shape of defect, because the walk had never been asked the
question.

`WP 19.4A` (`1f82c3c`, merged `9eb5f32`) removed the negative-margin
trick — every page-shaped project tab now adds its own padding instead —
and gave the walk a third check: the tab-strip header
(`PART_ItemsPresenter`) compared, in the control's own coordinate space,
against whatever the selected tab actually renders. A synthetic test
proves the new check fails on the old layout and passes on the fixed
one. The transferable point is the check, not the fix: **a check only
proves what it was built to check.**

## Found again, extended again, and its honest limit

`WP 19.9.0`'s release gate found a second status-bar defect at
1180×760: a growing segment's text never reached the bar's own
re-measure, because an intermediate `DockPanel` clamped its desired
size to what it had already been given; every text setter now
invalidates the measure (`9e11551`). `WP 19.10O` later gave the rail and
each area tree their own collapsed states, adding a small sibling that
walks only those new states rather than doubling the whole matrix. The
screenshots feed a CI artefact for the physical review; an early run
wrote them where the upload never found them (`6bf0319`) — the path is
now absolute, and an empty upload fails the leg outright rather than
just warning.

None of this proves the product works on a real window, a real
operating system, real hardware. The unreleased `v0.21.0` Execution Plan
says so directly, naming "headless tests pass where the real shell
fails" against two test references (`T4`, `T6`) from the lead's own
assessment, and answers it with a planned `WP 21.5C`: "a real-shell run
in CI: the built application launched on the Windows runner and driven
through a smoke journey by UI Automation, mouse only." That is the gap
this walk cannot close by construction: it runs inside one test process
that owns its own simulated window completely, so it cannot see real
window ownership, a second real monitor or an unusual DPI setting, or an
actual mouse finding the actual pixel a control renders at. `WP 21.5C`
is plan only — no branch, no code — as this chapter is written.

## The composer: an 830-line constructor becomes four phases

Before `WP 19.2A`, `MainWindow`'s constructor was, in the Work Package's
own words, "the 830-line constructor" — every view, dialog, coordinator
and cross-wiring built inline, in one pass, with objects needing each
other before either existed reached through a field read behind `!` (the
"null-forgiving operator": telling the compiler "trust me, this is never
null" in a way it cannot itself verify).

`MainWindowComposer` (`5d8ab4d`) replaces that pass with four phases,
each returning a small record the next one consumes:

```csharp
var views = composer.BuildViews(host, this, evidenceFilePickerOverride, callbacks);
var coordinators = composer.BuildCoordinators(host, this, views, callbacks);
composer.Wire(host, this, views, coordinators, callbacks);
var layout = composer.Layout(host, this, views, coordinators, callbacks);
```

`BuildViews` builds everything that needs no coordinator yet.
`BuildCoordinators` builds the pieces that do. `Wire` connects every
cross-collaborator event, delegate, palette binding and keyboard
shortcut. `Layout` assembles the root grid, the dock, the overlays and
the rail — and no `_field!` null-forgiving read remains in either
`MainWindow` or the workspace view coordinator it builds. The same Work
Package replaced the `RenderCurrentModuleAsync` switch with an area
registry, `Dictionary<ShellArea, ShellAreaRender>` — its own fourth
decision: "Areas are added through a registry, so Timesheets, Invoicing,
Reports and Settings each arrive as one entry" — and gave each
discipline's `WorkspaceRegistration` a public `<Discipline>CommandIds`
class, so a command id is a constant, never a repeated literal.

`MainWindow.cs` went from 1,475 lines to 579 at that commit (`wc -l`
on the two revisions). By
the time `BACKLOG.md` closed `TD-109` against it the figure quoted there
was 782; at the `v0.20.0` candidate head it is 894 (`wc -l`). Chapter
`45-deleting-dead-architecture-and-consolidating-duplicates.md` already
tracked this same file once, from 1,577 to 1,042 lines at `WP-G`, then
growing back to 1,475 by the time `WP 19.2A` ran: a deliberate
extraction pulls the number down, every ordinary release afterwards adds
a little back, and only another deliberate extraction brings it down
again.

## The fields the test suite pinned by name

Two follow-up commits the same day show what a large refactor still owes
its own tests. `CommandDescriptorBindingTests` scans the registration
source files by regular expression for `id: "literal"`; the new
`CommandIds` constants made that expression match nothing, so `0079bff`
taught the scan to resolve a constant reference by reflection instead.
More tellingly, several existing Desktop tests reached into
`MainWindow`'s own collaborators by field name
(`DesktopTestHelpers.GetPrivateField`), and one read `MainWindow.cs`'s
own source text for a specific line — trimming those collaborators to
constructor-local variables, which the refactor's first pass did, left
the objects behaving identically and unreachable by name from outside.
`4d8cbdb` restored fourteen fields under their exact original names,
assigned from the composer's own records: a private field reached by
name from outside its class is, in that sense, already a contract,
whether or not anyone wrote it down as one. `895d7b7` then added
structural tests pinning that the four phases exist and run in order,
and that neither file re-admits a null-forgiving capture.

**Deliberately not built:** an OS-automation dependency to make the walk
more "real" (`WP 21.5C` is the later, separate answer); a doubled
collapsed-state matrix (only the new axes are walked); and any change to
the floating-window or cross-monitor docking model, which is
`ADR-0153`'s subject for `v0.21.0`.

## What to take away

- **A green check only proves what it was written to check** — the
  Structure tab passed the walk for a release and a half because nobody
  had yet asked it the sibling-overlap question that mattered.
- **Bound an exemption to the exact fact that justifies it**, or it stops
  being an exemption and becomes a hole the next real defect fits through.
- **Reducing a large file to size is a repeatable event, not a permanent
  one** — the same constructor has now been cut twice, and it will need
  cutting again unless growth itself is watched.

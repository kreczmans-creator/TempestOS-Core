# ADR-0153: Tear Out and Dock Everywhere, Across Monitors

## Status

Proposed — `WP 20.0A` (design), for the Product Owner's review, 2026-09-15.
Steps 1–2 delivered by `WP 21.0A`.
Decision 8 (keyboard tab reorder, `Ctrl+Shift+,`/`.`) and the
visible-focus refinement of decision 7 (a typed gesture restores focus as
*keyboard* focus, so the ring is drawn) delivered by `WP 21.0K`
(2026-09-16), which also fixed a floating-window close defect carried
since `WP 20.10D`. Steps 3–4 await the Product Owner's review of this
ADR.

## Context

The Product Owner's P0, stated the night of 2026-09-14: *"I definitely
want the ability to drag bits out and review side by side so let's make
as much as practicable draggable and dockable. And definitely across
monitors, that's a P0 must have."*

`ADR-0095` (`TD-72`, 2026-08-29) already replaced the compile-time
five-column grid with `WorkspaceLayoutTree` — a pure, testable, arbitrary
tree of splits and tab groups, plus a list of floating windows, each a
real top-level `Window`. It was built exactly for this: decision 4 of
that ADR states "there is no privileged centre slot," and decision 5
promises "the operating system places [a floating window], the user
drags it onto whichever display they like, and its screen coordinates
are what persist." Both promises are true today, but only for a narrow
slice of the product: `WorkspaceDockingComposer`
(`src/Tempest.Desktop/Composition/WorkspaceDockingComposer.cs:83-90`)
registers exactly four panels — Project Explorer, the Document Area (as
one whole, non-floatable unit, `CanFloat: false` at line 85), Property
Inspector, Output — and `AttachmentViewerLauncher`
(`src/Tempest.Desktop/Viewing/AttachmentViewerLauncher.cs:170-184`) adds a
fifth kind, a drawing/document viewer, tabbed alongside the Document Area
inside that same tree. All five live inside one arrangement, itself
embedded either standalone at Home or inside a project's own *Structure*
tab (`ProjectWorkspaceView.SetEngineeringSurface`/`ClearEngineeringSurface`,
`src/Tempest.Desktop/Views/ProjectWorkspaceView.cs:410-426`) — one control
instance, moved, never duplicated.

Everything else the Product Owner is asking to drag is outside that tree
entirely, in three different plain-`TabControl`/split shapes that
`WorkspaceLayoutTree` has never touched:

1. **Document tabs.** `DocumentAreaView` (`src/Tempest.Desktop/Views/DocumentAreaView.cs`)
   is a bare Avalonia `TabControl` wrapped in its own bespoke chrome — pin
   buttons, close glyphs, a dirty-state asterisk, pin-ordering
   (`InsertInPinOrder`, lines 346-374) — none of which
   `LayoutTabGroupView` (the one tab-strip renderer the tree actually
   knows) has ever needed to reproduce, because until now nothing inside
   a `TabControl` was ever a dockable unit in its own right. Every object
   editor tab and the Quote tab's own kind of content lives here,
   two full levels of indirection below the outer docking tree (outer
   tree → the single "Documents" panel → this control's own `TabItem`s).
2. **Project workspace tabs.** `ProjectWorkspaceView._areas`
   (`src/Tempest.Desktop/Views/ProjectWorkspaceView.cs:50,297-307,543-579`)
   is a second, independent `TabControl` — Overview, Quote, Structure,
   Documents, Requirements, Tasks, Risks, Timeline, Deliverables,
   Evidence, Sign off — one tab per `ProjectArea`, entirely outside
   `WorkspaceLayoutTree`. The *Structure* tab happens to embed the whole
   engineering docking tree described above as its own content
   (`_structureHost`), which is how two genuinely different docking
   systems already coexist, nested, in the one running application,
   without either knowing about the other.
3. **Area panes.** A rail area like Projects
   (`src/Tempest.Desktop/Views/ProjectsAreaView.cs:43-51`) is a plain
   `TreeView` beside a `ContentControl` detail pane, built once and never
   reparented. Home's cockpit, Reports, Settings and every other rail
   surface are the same shape: one fixed control, swapped wholesale by
   the rail, never a participant in any tree.

Three named gaps in the docking model itself compound the problem rather
than sitting beside it, and the brief for this Work Package names all
three plus a fourth, unnumbered one:

- **`TD-90`** — no docking re-render restores keyboard focus. Verified
  absent as recently as `WP 19.9.0` (2026-09-10): "still no `Focus`
  reference in `WorkspaceLayoutController`, `WorkspaceLayoutHost` or
  `WorkspaceDockingComposer`" (`BACKLOG.md:591-595`).
- **`TD-91`** — `IWorkspaceLayout`, the frozen `WP8.0B` shadow contract
  (`src/Tempest.Workspace/Workspace/IWorkspaceLayout.cs`), speaks only in
  `Left`/`Right`/`Bottom`, a size, and visibility, and cannot express a
  tab group or a floating window. `WorkspaceDockingComposer.SyncWorkspacePlacements`
  (lines 210-234) already gives it "the nearest honest answer" for the
  four-panel case; nothing downstream of it makes a real decision from
  what it reports (confirmed: every non-test caller of
  `IWorkspaceLayout.GetPlacement`/`PanelPlacements` is this same
  projection's own write side, or `Workspace.cs`'s own container
  plumbing — the only *readers* for a live decision are the projection's
  own tests).
- **`TD-133`** — closed for repositioning and resizing
  (`Ctrl+Shift+Arrow` moves a panel to an edge, `Ctrl+Shift+[`/`]`
  resizes its split share, `WP 19.2B`) but explicitly not for tab
  *reordering*, left "out of this Work Package's own brief" (`BACKLOG.md:566`).
- **The capture-lost routing defect**, disclosed by `WP 19.10N` but never
  given a row: `WorkspaceLayoutController`'s own `PointerCaptureLostEvent`
  handler is registered `RoutingStrategies.Tunnel`
  (`WorkspaceLayoutController.cs:77`), but reflection against the
  referenced Avalonia 11.3.20 confirms the event is declared `Direct` —
  confirmed independently by `WorkspaceLayoutHost.cs:121-125`'s own
  correctly-`Direct` registration and its neighbouring comment. A
  handler registered for a routing strategy the event never uses is
  never invoked, so `CancelDrag()` likely never runs on a real
  OS-forced capture loss — only on the two pointer-released paths that
  call it directly. `_draggingPanelId`/`_dragActive` are the residual
  exposure: state that likely never resets on a real capture-loss
  cancel (`BACKLOG.md:441-458`, "Recommend a follow-up row").

This ADR is the follow-up row, for all four, because a package that adds
many more draggable things, across many more windows, is exactly the
package that makes each of these worse rather than incidental: more
tab strips need focus restored across a bigger re-render; a shadow
contract already honest only about three edges gets asked to describe a
window on a second monitor; more tabs invite more reordering; and a
drag that can now leave the *pointer's own starting window* is a drag
far more likely to lose OS capture along the way.

## Decision

**1. One shared arrangement, generalised from "a root plus a floating
list" into an ordered set of window entries — not a tree per window.**
`WorkspaceLayoutTree` already keeps every floating panel's own subtree
inside one record (`Floating: IReadOnlyList<FloatingLayoutWindow>`,
`WorkspaceLayoutTree.cs:34-37`); this decision is smaller than it
sounds. `Root`/`Floating` is replaced by one list,
`Windows: IReadOnlyList<WorkspaceLayoutWindow>`, each entry
`(Id, Root, IsPrimary, ScreenRect, MonitorKey)` holding its own docked
subtree exactly as a `FloatingLayoutWindow` does today; exactly one
entry carries `IsPrimary = true` — the application's own main window,
where the rail, header and ribbon chrome live outside the tree
entirely, unchanged. One `WorkspaceLayoutController` still owns the
whole structure and is still the only class that applies an operation
to it (`ADR-0095`'s own "one owner" discipline, restated for N windows
instead of one-plus-floating). The alternative — one independent
`WorkspaceLayoutTree`/`WorkspaceLayoutController` pair per top-level
window — is rejected: it does not remove `WorkspaceLayoutController`'s
own machinery (drag tracking, responsive collapse, the drop-target
highlight, the auto-hide flyout), it duplicates the owner of it once per
window; and a cross-window drag (decision 4) needs to remove a panel
from one subtree and insert it into another as a single atomic
operation, trivial against one model and one owner, and a genuine
distributed-consistency problem across N independent ones. Persistence
stays one `IWorkspaceLayoutStore` document (`ADR-0064`'s "own JSON blob
under its own Settings key," restated, not a second mechanism, and not
one document per window) — `WorkspaceLayoutSerializer`'s existing
version tag is exactly what absorbs this shape change without orphaning
a layout saved by an older build (`ADR-0095` §7's own reason for
choosing a hand-written, versioned DTO in the first place).

**2. What becomes dockable: every document tab, every area pane, and
every project workspace tab — one shell-level tree, deliberately
separate from the existing engineering tree inside the Structure
panel.** Concretely: `DocumentAreaView.ShowTab` (`DocumentAreaView.cs:127-148`)
and `ProjectWorkspaceView`'s own per-`ProjectArea` `TabItem` construction
(`ProjectWorkspaceView.cs:297-307`) both stop building `TabItem`s
directly and instead register a `WorkspacePanelDescriptor` per tab
against a new, shell-scoped `WorkspacePanelRegistry` and apply
`WorkspaceLayoutTree.Dock`/`DockToEdge` to place it, exactly as
`AttachmentViewerLauncher.Dock` already does for a viewer
(`AttachmentViewerLauncher.cs:170-184`) — the pattern this package
generalises is already proven in production, once. A rail area like
Projects registers its own tree/detail split as two panels rather than
one fixed `TreeView`-beside-`ContentControl` pair
(`ProjectsAreaView.cs:43-51`), so the tree can be dragged wide beside
its own detail on a second monitor while list-scanning a big project.
**The nested engineering tree stays exactly as `ADR-0095` left it**, a
second, independent `WorkspaceLayoutTree`/`WorkspaceLayoutController`
pair owned by the existing `WorkspaceDockingComposer`, embedded as the
Structure panel's own content. Flattening it into the shell-level tree
would promote Explorer/Inspector/Output to shell-level peers of Quote
and Documents for no capability the Product Owner named, and would
re-open `ADR-0095`'s own settled arrangement to satisfy this one. The
disclosed cost of keeping two levels: tearing the Explorer out from
beside a Quote tab on a different monitor is two gestures, not one —
tear the Structure panel out first (this ADR), then tear Explorer out of
that floated window (`ADR-0095`, unchanged) — never one drag straight
from a project tab to a discipline panel. The rail continues to decide
which panel *set* is active by default per area (a preset the way
`WorkspaceLayoutPresets` already builds one for Engineering's four); it
stops being a hard partition once a panel has been dragged loose,
exactly as `ADR-0095` decision 4 already established for the four
panels it covers today.

**3. Every torn-out project-workspace surface must be self-sufficient
outside `ProjectWorkspaceView`'s own chrome.** Today the outer header
(project name, lifecycle banner, "Archived — nothing here can be
changed") is drawn once, above `_areas`
(`ProjectWorkspaceView.cs:359-386`), and every tab's content trusts it
is still visible. A `ProjectQuoteView` or `ProjectDeliverablesView`
floated into its own window carries none of that chrome with it, so
each such view gains its own compact identity strip (project name,
archived/closed state) the way `AttachmentViewerLauncher`'s own viewer
already titles its window from the attachment's file name
(`FloatingPanelWindow.cs:44`) — a real, per-view migration named here
rather than discovered per view once the tearing already ships.

**4. Drag between windows: a shared drag, tracked in screen
coordinates, resolved against every window's own candidates.**
Today each `WorkspaceLayoutHost` tracks a drag only in its own local
coordinate space (`WorkspaceLayoutController`'s constructor wires
`PointerMoved`/`PointerReleased` once per `Host`,
`WorkspaceLayoutController.cs:75-77`), and "outside every pane" always
means "float here" (`CompleteDrag`, lines 243-266) because there is
nowhere else to go. With one controller owning every window (decision
1), `CurrentCandidates()` (lines 276-293) is generalised to enumerate
every window's own `TabGroups`, each candidate's bounds translated to
**screen** coordinates via `Control.PointToScreen` rather than one
`Host`'s local space; `UpdateDrag`/`CompleteDrag` resolve the pointer's
current screen position (not `Host`-local) against that combined set.
A drop resolving inside a live window's own candidate — including a
window other than the one the drag started in — docks there via the
identical `Dock`/`DockToEdge` operation (removing from the source
window's subtree and inserting into the target's, one tree, one
`Apply`); a drop resolving inside a window's own bounds but over no
candidate pane docks to that window's nearest edge; a drop resolving
outside every known window's bounds entirely spawns a new floating
window at the drop point — `Float`'s existing behaviour
(`WorkspaceLayoutTree.cs:167-177`), unchanged, now reachable from any
source window rather than only the main one. A source window left with
an empty subtree closes, mirroring `FloatingPanelWindow`'s own existing
"a window whose last panel was removed is not an empty window — it is a
window that should no longer exist" rule (`WorkspaceLayoutTree.Remove`,
lines 198-199) — extended so a *secondary* window, not only a floating
panel, can be the one that closes; the Primary window never closes this
way regardless of what its subtree holds, since it also carries the
rail and header.

**5. Persistence carries a monitor identity and a monitor-relative
rectangle; an absent monitor falls back to the primary, clamped
on-screen.** `FloatingLayoutWindow` stores raw `X`/`Y`/`Width`/`Height`
doubles today, with no monitor identity anywhere in the type or its
serializer, and nothing in this codebase reads Avalonia's `Screens` API
at all (confirmed by search — zero references outside this ADR). The
generalised `WorkspaceLayoutWindow` gains a `MonitorKey` (a best-effort
stable identity Avalonia's `Screen` exposes — `DisplayName` paired with
the screen's own `Bounds`, since no platform this product ships on
guarantees a persistent hardware id) and stores its rectangle **relative
to that monitor's own working-area origin**, not the virtual desktop's
absolute coordinates — the same technique every mainstream multi-monitor
IDE uses, so a monitor reattached to a different port, or a laptop
redocked with its external display now to the left rather than the
right, still restores its own windows sensibly relative to themselves.
On restore, `WorkspaceLayoutController` resolves `MonitorKey` against
`Screens.All`; no match (the monitor is unplugged, or this is a
different machine entirely) falls back to the primary screen's own
working area, and the restored rectangle is clamped inside it — never
placed off every current screen, which `Window.Position`'s setter does
not itself guard against and which would otherwise leave a window with
no visible title bar to drag back. This is new code, proven at the
seam (decision 8) rather than against Avalonia's own real screen
enumeration, which no CI runner here has more than one of (risk 2).

**6. `IWorkspaceLayout` (`TD-91`) stops being written from the real
arrangement; it is not deleted.** The frozen `WP8.0B` contract, its own
`Workspace.cs`/`WorkspaceState.cs` container, and their own tests are
untouched — nothing in this package needs `IWorkspaceLayout` removed,
and no consumer beyond its own tests reads `GetPlacement`/`PanelPlacements`
to make a live decision (confirmed by the search in Context, above).
What is removed is `WorkspaceDockingComposer.SyncWorkspacePlacements`
(`WorkspaceDockingComposer.cs:210-234`) and its `Project` helper — the
one piece of code that tries to answer "where is this panel" in
`Left`/`Right`/`Bottom`-and-a-size vocabulary from the real tree. That
projection was already disclosed as lossy for a tab group or a floating
panel when it was written (`ADR-0095`'s own Negative consequence); once
panels can be torn onto a second monitor entirely, "the nearest honest
answer" degrades from *approximate* to *actively misleading* (there is
no sane `Left`/`Right`/`Bottom` for a dashboard on a different screen),
and a projection nobody reads for behaviour is worse kept wrong than
removed. `IWorkspaceLayout` remains available to a caller that wants the
frozen contract's own in-memory model on its own terms; it is simply no
longer kept in sync with what `WorkspaceLayoutTree` actually holds.

**7. Focus is restored after every re-render that moves a panel,
including across windows — closing `TD-90`.** `WorkspaceLayoutController.Apply`
records which control held keyboard focus (or which tab header
initiated the current drag) immediately before calling the operation;
after `Adopt`/`Host.Update` re-renders, it locates that panel's own new
tab header — by `PanelId`, not by control identity, since
`LayoutTabGroupView` instances are rebuilt per render while panel
*content* is reparented rather than rebuilt
(`LayoutTabGroupView.Detach`, lines 347-362, the same mechanism that
already preserves scroll and selection state) — and calls `Focus()` on
it. When the panel's new home is a window that did not previously have
input focus (a cross-window dock, or a brand-new floating window), the
window itself is explicitly activated first (`Window.Activate()`)
before the control focus call, since a focused control in a background
window is invisible to the keyboard until the window is. This closes
`TD-90` as a proven behaviour, not a restated claim: it needs a headless
proof that raises a real re-render across two windows and asserts which
control now has focus, the way `KeyboardOnlyJourneyTests` already
proves the `TD-133` keyboard moves end-to-end with no simulated pointer.

**8. Keyboard tab reordering closes `TD-133`'s own named residual,
in the same package that generalises the tab strip it lives in.**
`WorkspaceLayoutTree` gains `ReorderTab(Guid groupId, Guid panelId, int
direction)`, a pure operation the same shape as `ResizeSplit` — it moves
`panelId` one position within its own `LayoutTabGroupNode.PanelIds`,
clamped at either end, and is a no-op off either end or when the panel
is not in that group. `LayoutTabGroupView`'s existing focused-tab-header
`KeyDown` handler (`LayoutTabGroupView.cs:236-268`) gains
`Ctrl+Shift+,`/`Ctrl+Shift+.`, documented in the same
`AutomationProperties.HelpText` the repositioning keys already carry
(line 229-231). This is deliberately scoped to reordering *within* one
tab group; moving a tab to a different group or window by keyboard alone
is not built here — decision 4's cross-window move stays a mouse gesture
for this package, disclosed rather than silently assumed, exactly as
`TD-133`'s own original closure disclosed reordering itself as
out of scope.

**9. The capture-lost routing defect is fixed, not merely inherited.**
`WorkspaceLayoutController`'s `PointerCaptureLostEvent` registration
moves from `RoutingStrategies.Tunnel` to `RoutingStrategies.Direct`
(`WorkspaceLayoutController.cs:77`), matching the routing strategy
`WorkspaceLayoutHost`'s own neighbouring handler already correctly uses
and the strategy reflection against the referenced Avalonia build
confirms the event actually carries. `CancelDrag()` additionally resets
`_draggingPanelId`/`_dragActive` unconditionally rather than only from
the two pointer-released call sites, so a real OS-forced capture loss —
now materially more likely once a drag can cross a window boundary
(decision 4) and briefly leave every window's own input root — cannot
leave the controller believing a drag is still active. A test drives a
real `PointerCaptureLostEventArgs` through the real
`WorkspaceLayoutController` (not just `WorkspaceLayoutHost`, which
`WP 19.10N`'s own new test already covers) and asserts `IsDragging`
becomes `false` — the fact reflection found but no test in the tree
previously proved either way.

**10. Proof is headless, generalised from a pattern the tree already
uses once.** `WorkspaceLayoutControllerTests.BuildRig`
(`tests/Tempest.Desktop.Tests/WorkspaceLayoutControllerTests.cs:29-49`)
already constructs a real `Window` and records real `FloatingPanelWindow`
instances — a second real `Window`-derived type — inside one headless
test process, with no special multi-window configuration: Avalonia's
headless platform hosts as many top-level windows as the test opens.
This package's own new coverage is layered the same way `ADR-0095`
layered its own: (a) pure model tests over the generalised
`WorkspaceLayoutTree` — dock, float, remove and normalise across a
multi-window forest — with no UI at all; (b) `WorkspaceLayoutController`
tests that open two or more real headless windows and drive a
cross-window drag via screen coordinates translated through each
`Host`'s own `PointToScreen`, asserting the panel lands in the target
window's subtree and an emptied source window closes; (c) a seam-level
test for monitor fallback — an injected screen list with no entry
matching a saved `MonitorKey` — proving the primary-and-clamp rule
without needing Avalonia's own `Screens` to report more than the one
synthetic screen headless mode actually gives it (risk 3, below); (d)
a `KeyboardOnlyJourneyTests` addition proving focus lands correctly
after a cross-window dock, and one proving `Ctrl+Shift+,`/`.` reorders a
tab. `ShellDockingSeamTests.cs`, `ResponsiveWorkspaceTests.cs`,
`MainWindowResizeTests.cs` and `ProductSpineAcceptanceTests.cs` are
existing suites this package's own tab-strip migration (decision 2)
touches and must keep green, named here as seams that change rather than
discovered as failures later.

## Seams That Change, and Seams That Do Not

**Change:** `WorkspaceLayoutTree.cs` (Root/Floating → `Windows`, plus
`ReorderTab`); `IWorkspaceLayoutStore`'s serialised shape (a new
`WorkspaceLayoutSerializer` version, per `ADR-0095` §7's own discipline);
`WorkspaceLayoutController.cs` (multi-window ownership, screen-space
drag, monitor persistence, focus restore, the `Direct` routing fix);
`FloatingPanelWindow.cs` (generalises to host any panel, not only the
four engineering ones, and gains `MonitorKey`); `DocumentAreaView.cs`
(its bespoke pin/close/dirty tab chrome is retired in favour of
`LayoutTabGroupView`, which gains pin state, a dirty marker and pin
ordering it does not carry today); `ProjectWorkspaceView.cs` (`_areas`
stops building `TabItem`s directly); `ProjectsAreaView.cs` and its
sibling rail areas (tree/detail and dashboard panes become registered
panels); `WorkspaceDockingComposer.cs` (loses `SyncWorkspacePlacements`;
gains nothing else — it keeps owning the inner engineering tree
unchanged, per decision 2).

**Do not change:** `WorkspaceLayoutHost.cs`'s own rendering (still a
pure function of a subtree; a Primary window's `Host` sits inside
`MainWindow`'s own rail/header chrome exactly as today, a secondary
window's `Host` is still its entire content); `LayoutTabGroupView.cs`'s
own drop-zone and strip/flyout mechanics; `DockTargetResolver.cs`'s own
five-zone geometry; the inner engineering docking tree inside the
Structure panel, and every one of `ADR-0095`'s own decisions about it;
`AttachmentViewerLauncher.cs`, which already docks a real panel into
that inner tree and needs nothing from this package; `IWorkspaceLayout.cs`
itself, its own tests, and `Workspace.cs`/`WorkspaceState.cs` (decision
6 removes a writer, not the contract).

## Estimate

1. Model — generalise `WorkspaceLayoutTree` to a window forest, add
   `ReorderTab`, version the serializer, pure tests: **3 days.**
2. Controller — multi-window ownership, screen-space drag tracking,
   cross-window `Dock`/`Float`, monitor persistence and fallback, focus
   restore, the `Direct` routing fix: **5 days.**
3. Shell-level docking surface — `DocumentAreaView` and
   `ProjectWorkspaceView` tabs and the rail area panes become registered
   panels; `LayoutTabGroupView` gains pin/dirty/ordering; each torn-out
   project view gains its own identity strip (decision 3): **6 days.**
4. Keyboard and accessibility closure — `TD-90` proof, `TD-133` tab
   reorder keys, the capture-lost fix's own test: **2 days.**
5. `IWorkspaceLayout`/`TD-91` — remove `SyncWorkspacePlacements`,
   confirm no live reader breaks: **1 day.**
6. Multi-window headless test suite, plus the `WP 19.3A` Windows CI
   layout walk extended to open a second window once: **3 days.**

**Total: 20 developer-days** — larger than any single Work Package in
the `v0.19.0`/`v1.0.0` programme to date (`WP 19.1A` at 9 is the current
largest), so this ADR recommends splitting delivery across two or three
Work Packages (model and controller first, since every later step
depends on them; the shell-level migration second; keyboard/test closure
third) rather than one, mirroring how `WP 19.1A` itself shipped in named
parts.

## Risks

**Multi-window input capture on Windows.** Decision 4's cross-window
drag depends on pointer input delivered to whichever window the pointer
is physically over continuing to reach the controller that started
tracking it — Avalonia's own pointer capture is per top-level window's
input root, not global to the input device, so a drag that leaves
window A's bounds while A still holds capture may simply stop
delivering `PointerMoved` the moment the cursor crosses into window B's
screen rectangle, rather than continuing to report screen positions
under B. This cannot be proven by a headless model test alone (headless
windows have no real OS input focus to lose or transfer) and needs a
manual verification pass on real Windows hardware before this ADR's
decision 4 can be called done, not only tested.

**DPI per monitor.** Nothing in this codebase touches Avalonia's own
`Screens`/`RenderScaling` today (confirmed by search); decision 5's
monitor-relative persistence must convert through each window's own
scaling correctly at both save and restore, and a rectangle saved on a
150%-scaled 4K monitor and restored after that window moves to a
100%-scaled one must not compute an off-screen or degenerate size. This
is entirely new code and cannot be exercised against a real mixed-DPI
multi-monitor rig in CI, which has none.

**The headless walk's own blind spot.** `WorkspaceLayoutControllerTests`
already proves several real `Window` instances can coexist in one
headless test process, which is what makes decision 10's model-level
and controller-level proofs possible at all — but Avalonia's headless
platform reports one synthetic screen, so the monitor-fallback rule
(decision 5) can be proven at the seam level against an injected screen
list, never end-to-end against Avalonia's own real multi-monitor
`Screens` enumeration. The genuine "drag from a window on monitor A onto
a window on monitor B" gesture is provable only by the physical review,
on real hardware — `WP 19.3A`'s own Windows CI layout-verification job
walks one window's rail entries today and does not open a second window
at all, so this package's most novel behaviour has no automated safety
net beyond the model-level proofs decision 10 commits to.

## Consequences

**Positive:** the Product Owner's own words — "drag bits out and review
side by side," "definitely across monitors" — become literally true for
every document, every project tab and every area pane, not only the
four engineering panels `ADR-0095` already covered; `TD-90`, `TD-91`,
`TD-133`'s own named residual, and the capture-lost routing defect all
close in the same package that touches the files each of them already
lives in, rather than as four separate future rows; the inner
engineering docking tree `ADR-0095` built is untouched and unrisked,
because this package deliberately does not flatten it into the new
shell-level one.

**Negative:** twenty developer-days is a real, disclosed cost, larger
than any single Work Package this programme has run; every project-area
view (Quote, Deliverables, Documents, Requirements, Tasks, Risks,
Timeline, Evidence, Sign off) needs its own minimal identity strip
(decision 3) so it still makes sense torn out of `ProjectWorkspaceView`'s
own chrome — a real, per-view migration cost, not a mechanical one;
`DocumentAreaView`'s bespoke tab chrome is retired in favour of
`LayoutTabGroupView` gaining equivalent capability it does not have
today (pin ordering, a dirty marker), so every existing test asserting
against `DocumentAreaView`'s own tab behaviour needs re-proving against
the new host rather than merely relocating; multi-monitor input capture
and DPI correctness (risks 1-2, above) cannot be fully proven before a
real-hardware pass, so "Accepted" for this ADR should not be read as
"multi-monitor dragging is proven," only as "the model, the persistence
shape, and the single-window mechanics are."

## Alternatives Considered

**One `WorkspaceLayoutTree`/`WorkspaceLayoutController` per top-level
window.** Rejected in decision 1: it does not remove the controller's
own machinery, it multiplies the owner of it, and it turns a
cross-window drag into a distributed operation across independent
models rather than one atomic edit to one tree.

**Flattening the inner engineering tree into the new shell-level one.**
Rejected in decision 2: it would promote Explorer/Inspector/Output to
peers of every project tab for no capability the Product Owner named,
and would re-open `ADR-0095`'s own settled arrangement rather than build
beside it.

**A hard partition keeping each panel confined to the rail area or
project tab it was born in, with dragging only *within* that scope.**
Rejected: it is not what "as much as practicable draggable and dockable
... across monitors" asks for, and `ADR-0095` already rejected the
identical idea for its own four panels ("there is no privileged centre
slot").

**Keyboard-only tab moves between groups and windows, alongside the
mouse gesture, in this same package.** Deferred, not rejected: decision
8 closes `TD-133`'s own named reordering residual but leaves
cross-group and cross-window moves mouse-only, disclosed rather than
silently assumed, so a future Work Package can pick it up as a named
residual rather than this ADR quietly overclaiming full keyboard parity.

## Related Documents

`ADR-0095` (the layout tree and floating windows this ADR generalises,
never replaces); `ADR-0064` (the settings substrate the arrangement
persists through, unchanged in mechanism); `ADR-0115` (the document
viewer, already proof that a new panel kind can dock, tab, split and
float into the inner tree with no code of its own for any of it);
`BACKLOG.md` (`TD-90`, `TD-91`, `TD-133`, and the capture-lost routing
defect, each absorbed by this package — see its own reconciliation
note); `docs/design/Docking Everywhere — Design Note.md` (the Product
Owner-facing walk of the same decisions); `WorkPackages.md` (this row
reserved as `WP 20.0A` design; implementation split per the Estimate,
above).

# Tear-Out and Dock Everywhere: a Panel Can Never Be Lost

**Release:** `v0.20.0` release candidate (`release/v0.20.0`, head
`3ce8f20`, 2026-09-15) for `WP 20.0A`'s design and `WP 20.10D`'s fix; the
`v0.21.0` plan (`WP 21.0A`–`WP 21.0C`, `WP 21.5C`) is opened on
`release/v0.21.0` (head `bb286db`) with no code of its own — none of
this is merged to `main` · **Work Package(s):** `WP 20.0A` (design,
`ADR-0153` *Proposed*), `WP 20.10D`; planned `WP 21.0A`–`21.0C`, `21.5C`
· **Debt:** `TD-90`, `TD-91`, `TD-133` (its reordering residual) — all
three named by `ADR-0153`, none yet closed · **Decision:** `ADR-0153`
(*Proposed*, 2026-09-15) · **Code:**
`src/Tempest.Desktop/Docking/WorkspaceLayoutController.cs`,
`FloatingWindowPlacement.cs`, `FloatingPanelWindow.cs`,
`src/Tempest.Desktop/Composition/WorkspaceDockingComposer.cs`

**In plain terms.** Every panel in TempestOS — the project tree, an
inspector, a document — can be dragged into a new arrangement: pulled
into its own little window, tabbed with something else, moved to a
second monitor. This chapter is about a promise the team makes about
that dragging: however clumsy the drag, a panel can never actually
vanish. While testing the release by hand, the Product Owner found the
opposite — a panel being moved seemed to disappear. This chapter
explains the small, real fix that shipped for that finding, and why the
much bigger job — making everything on screen draggable, across
monitors — was written up as a plan first and left deliberately unbuilt,
once its honest cost turned out to be three times what anyone assumed.

## A vocabulary that ran out, twice

`24-docking-and-workspace-layouts.md` and
`36-workspace-layout-and-docking.md` cover the earlier half of this
story: TempestOS once had exactly three places a panel could live —
left, right, bottom — and `ADR-0095` replaced that with
`WorkspaceLayoutTree`, a tree of splits and tab groups able to express
tabbing, arbitrary splitting and a real floating window, for four
Engineering panels (Explorer, the Document Area, Inspector, Output).

That tree left one thing behind: `IWorkspaceLayout`, the older, frozen
`WP8.0B` contract, which still only knows an edge, a size and whether a
panel is visible. `WorkspaceDockingComposer.SyncWorkspacePlacements`
feeds it "the nearest honest answer" it can compute from the real tree —
it cannot say "in a tab group" or "floating on the second monitor",
because its vocabulary has no words for either. The technical-debt
rationalisation of 2026-09-14 recorded this as `TD-91`, rated low
priority because nothing reads that contract to make a live decision any
more. Then the Product Owner asked, the next night, for the one thing
that turns a cosmetic gap into a real one: drag *everything*, across
monitors. `ADR-0153` is what happens when a shadow contract nobody reads
is asked to describe a window that has left the building.

## What the Product Owner actually found

The Product Owner's own words, recorded in the code that answers them
(`MainWindowComposer.Layout.cs`):

> "dock the Requirements tree beside a requirement's editor … its
> disappeared somewhere and broken away — need to review it all."

The Product Owner was not trying to tear a panel out, only doing an
ordinary thing — dragging the Requirements tree to sit beside an open
requirement — and a panel vanished. The mechanism was small and
embarrassing: `CompleteDrag` floated a panel on *any* release that
landed on no drop target, including one a single pixel short of it,
inside the 4-pixel gutter between two panes. A deliberate tear-out and
an accidental miss looked identical to the code. No written test script
would ever have found this — nothing says "miss by one pixel" — but a
person's own hand, holding a mouse, found it in minutes. That is what a
*physical* review is for: not proving the steps as written, but doing
what a real user actually does.

## "A panel can never be lost," as five invariants

`WP 20.10D` (`412b799`, merged `3ce8f20`) turns that phrase from an
aspiration into invariants — things that must stay true no matter what a
user does, each backed by its own test rather than trusted by feel.

**A tear-out is one specific gesture, not "anything else."**
`CompleteDrag` now floats a panel only when the release has left the
workspace's own rendered bounds:

```csharp
else if (IsOutsideWorkspace(position))
{
    RememberDockAnchor(panelId);
    var origin = screenPosition ?? new PixelPoint((int)position.X, (int)position.Y);
    Apply(t => t.Float(panelId, origin.X, origin.Y, 420, 320));
    Announced?.Invoke($"{title} undocked into its own window.");
}
else
{
    Announced?.Invoke($"{title} stays where it was — drop it on a highlighted target to move it.");
}
```

A release inside the workspace but over no target now changes nothing
and says so in the status bar — a user who lets go and sees nothing move
needs to be told that was deliberate, not left wondering what broke.

**A floating window is owned, shown in front, and never off-screen.**
Its screen position used to come from a real coordinate bug: whatever
converted a drag's position into a screen pixel silently had no real
point supplied to it, so a fallback quietly reused the workspace's own
local coordinates as screen coordinates, every time. The fix,
`FloatingWindowPlacement.Clamp`, is deliberately a pure function — no
window, no screen, just numbers in and out — so its edge cases (a
rectangle on no screen at all, one merely spilling past an edge) can be
tested directly rather than through a real, flaky window.
`FloatingPanelWindow.ClampToScreen` calls it before every `Show()`,
torn out or restored onto a monitor that is no longer there. It is also
shown owned by, and in front of, the main window (`Window.Show(owner)`
plus `Activate()`) — a window nobody can see behind the main one is
exactly as lost as one that never opened.

**Closing a floating window redocks it, rather than discarding it,** at
a remembered `DockAnchor` — where it sat immediately before it floated —
so the title bar's close button stops meaning "get rid of this."
**"Show Panel: `<title>`" always finds it:** the Command Palette gains
one command per registered panel, bringing it to the front from wherever
it is — docked, floating behind the main window, or hidden — and docking
it back in first if it was floating. **Reset Layout folds every stray
panel back**, not only the ones it knows about: `ResetTo` docks every
panel the default arrangement does not itself place — a floating
attachment viewer registered after that default was written, say — to
an edge, rather than silently dropping it.

`PHYSICAL_REVIEW.md` §7d's own T4 now walks all five in one script:
miss the target (nothing moves); tear out past the edge (a real, owned,
on-screen window); reopen from the Palette; close from the title bar;
Reset Layout. `IWorkspaceLayout` itself is untouched by any of it —
`TD-91`'s row is not touched either, deliberately, because none of these
five fixes needed it.

## Why a design was written before the code, this time

Every ADR in this repository but one says **Accepted** — 142 of them,
plus ten frozen by `ADR-0146` — meaning the decision was made and the
code exists. `ADR-0153` alone says **Proposed**: a decision written out
in full — context, alternatives rejected, cost — submitted for the
Product Owner's review, nothing built against it yet. *Accepted* records
what was done; *Proposed* asks "is this the right thing to do," and
treating a Proposed ADR as settled is the mistake this whole story warns
against.

The reason a design came first is the estimate. The tranche plan that
opened `v0.20.0` carried this work as a rough six days. Working through
`ADR-0153`'s ten numbered decisions — one shared arrangement across
every window rather than a tree per window, a shell-level docking tree
kept separate from the existing Engineering tree, cross-window drag in
screen coordinates, monitor-relative persistence with a primary-screen
fallback, focus restored across a re-render that crosses windows — the
honest total came to **twenty developer-days across six steps**. The
Execution Plan and Release Notes both say so
plainly: *"not the six days the tranche plan first carried."* Two of the
ADR's own named risks, real multi-monitor pointer capture and
per-monitor DPI scaling, cannot be closed by writing more tests at all,
because no CI runner here has more than one screen to test them on.

Finding that out cost the half a developer-day the plan gave the design
rather than weeks of building against a wrong assumption, discovered mid-package.
That is the argument for a Proposed ADR: cheaper to be wrong about scope
on paper than in a half-finished tree.

## The plan that follows, and that none of it is built

`v0.21.0`'s execution plan (`release/v0.21.0`, head `bb286db`) splits
`ADR-0153`'s twenty days into three Work Packages, gated on the Product
Owner's own review of the ADR: `WP 21.0A` generalises the layout tree
into a forest of window entries under one controller; `WP 21.0B` adds
torn-out windows and cross-window drag in real screen coordinates;
`WP 21.0C` makes areas dockable units, restores a layout monitor-aware,
restores focus after a re-render, and closes `TD-133`'s residual and
`TD-90` as proven behaviour rather than a restated claim.

`WP 21.5C` follows once `21.0C` lands, for a reason stated outright:
*"headless tests pass where the real shell fails (T4, T6)."* T4 is this
very finding — a bug no script would have written could not have been
caught by one — so `WP 21.5C` runs the built application on a real
Windows CI runner, driven by UI Automation and an actual mouse, beyond
the headless tests `68-the-layout-walk-and-the-composer.md` covers and
whose own honest limits that chapter names.

None of `WP 21.0A`–`21.0C` or `WP 21.5C` has been built. The `v0.21.0`
Release Notes' own "What shipped" table is empty; `ADR-0153` is still
*Proposed* in both worktrees this chapter draws from — stated as
plainly as the source states it, because letting a well-written plan
read as a finished feature is precisely what this Academy exists to
prevent.

## What was deliberately not built yet

Everything past the four Engineering panels stays exactly as it was: a
Quote tab, a Requirements list, a document tab, the Projects dashboard —
none of them drag, tab, split or float, because `ADR-0153`'s
shell-level docking tree for document tabs, project tabs and rail area
panes is design only. The capture-lost routing defect the ADR also
names — the drag controller's own pointer-capture-lost handler is
registered for a routing strategy the event does not use, so a real
OS-forced capture loss likely never resets an in-progress drag — is
disclosed and left exactly as found; `WP 20.10D` did not touch it.
Cross-window and cross-group tab moves stay mouse-only even in the plan;
the ADR closes only in-group reordering.

## What to take away

- **"Proposed" and "Accepted" are two different promises, and confusing
  them is how a plan gets built as if it were a decision.** Writing and
  reviewing a design before any code exists is not caution for its own
  sake — it is how this project found a six-day estimate was really
  twenty, for the cost of half a day's writing.
- **A guarantee like "a panel can never be lost" is only real once it is
  broken into invariants a test can check one at a time** — one
  tear-out gesture, an owned and clamped window, a redock on close, one
  findable route back, and a Reset that forgets nothing.
- **A human moving a real mouse finds bugs a written script never
  will**, because a script only ever tests what someone thought to write
  down, and a one-pixel miss in a 4-pixel gutter was never going to be a
  line in anyone's test plan.

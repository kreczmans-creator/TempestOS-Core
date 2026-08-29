# Workspace Layout & Docking Architecture

**Realises `TD-72` and `ADR-0095`. Replaces the compile-time docking
geometry shipped by `WP 10.0B`/`WP 10.2B`.**

## The question this answers

> Can a user arrange the workspace the way the mock-ups show — several
> surfaces open at once, side by side, tabbed, split, floated onto a second
> monitor — and will TempestOS still be arranged that way tomorrow?

Before `TD-72` the answer was no, and not because of missing features:
because the arrangement was *unrepresentable*. Docking was a five-column,
three-row `Grid`, and panels were assigned to named slots once at
composition time. There were three places a panel could be, and they were
all taken.

## The model

```
WorkspaceLayoutTree
├── Root : WorkspaceLayoutNode?
│    ├── LayoutSplitNode    (Orientation, Children[], Weights[])
│    └── LayoutTabGroupNode (PanelIds[], SelectedIndex)          ← the only leaf
├── Floating : FloatingLayoutWindow[]   (subtree + screen rectangle)
└── Panels   : PanelId → PanelPresentation(IsPinned, IsCollapsed)
```

Four properties carry the whole design:

| Property | Why it matters |
|---|---|
| **Arbitrary nesting** | Splits contain splits or tab groups, either orientation, any depth. Arbitrary splitting is not a feature; it is the shape. |
| **A leaf is always a tab group** | A single docked panel is a group of one, so "drop a panel onto another to tab them" is ordinary, not a special case. |
| **Immutable, pure operations** | Every edit returns a new normalised tree. No half-applied drag can exist, and the whole system is testable with no UI. |
| **Proportional weights** | A layout restored into a smaller window keeps its proportions instead of starving the working pane. |

## Operations

`Dock` · `DockToEdge` · `Float` · `MoveFloating` · `Remove` ·
`SelectPanel` · `SetWeights` · `SetPinned` · `SetCollapsed`

Each is a total function returning a normalised tree. **Docking is always
a move**: the panel is removed from wherever it is before being inserted,
so a panel can never appear twice.

**Normalisation** runs after every edit and collapses the debris an edit
leaves behind — splits reduced to one child, and nested splits sharing
their parent's orientation. Without it, repeated dock/undock grows an
ever-deeper tree of one-child wrappers; a test drives ten cycles and
asserts the depth is unchanged.

## The extension point

```csharp
registry.Register(new WorkspacePanelDescriptor(id, "Drawing", drawingView));
```

That is the whole integration. There is **no privileged centre slot** — the
document area is a descriptor exactly like every other — and no per-panel
code in the model, the renderer, or the drag logic. A registered panel
gains docking, tabbing, splitting, floating, collapse, auto-hide and
persistence at once.

This is not theoretical: `DigitalThreadGraphView`'s own source recorded
that it became a document tab rather than a panel because *"there are
exactly three physical dock slots, all already occupied"*. That reason no
longer exists, and its comment now says so.

## Rendering

`WorkspaceLayoutHost` is a total function from tree to visual tree:

- `LayoutSplitNode` → a `Grid` with star-sized panes and a real
  `GridSplitter` between each pair. A splitter drag reads the resulting
  pixel sizes back out and stores them as proportions.
- `LayoutTabGroupNode` → a `LayoutTabGroupView`: tab strip, chrome
  (collapse, pin, close), and the selected panel's content.
- A collapsed or auto-hidden pane → a fixed-width strip, handing its share
  back to its siblings.
- `FloatingLayoutWindow` → a real `FloatingPanelWindow`, hosting the same
  renderer, so a floating window can itself contain tabs and splits.

The renderer **decides nothing**. Every gesture becomes a pure operation on
the tree and the result is re-rendered, so the visual tree cannot drift out
of step with the model — the failure mode that makes hand-rolled docking
degrade over a session.

## Drag to dock

```
tab press → threshold → pointer move → DockTargetResolver → release → Dock/Float
```

`DockTargetResolver` is pure geometry with no Avalonia types: given
candidate rectangles and a point, it returns one of five zones — a
generous centre that tabs, and four edge bands that split. A release
outside every pane is the undock gesture, and floats the panel at that
point.

A press is not a drag until the pointer has travelled past a threshold, so
clicking a tab selects it rather than re-docking it.

## Persistence

Serialised through `ISettingsProvider` (`ADR-0064`) — application session
state, deliberately **not** the engineering persistence authority
(`TD-85`). Where someone put their panels is not engineering data.

The format is a hand-written, **versioned** DTO rather than polymorphic
serialisation of the model types, so renaming a node type cannot orphan
every saved layout. Reading is total:

| Input | Result |
|---|---|
| Absent, empty, malformed JSON | no saved layout → fallback |
| Unknown version | no saved layout → fallback |
| Structurally impossible (empty tab group, childless split) | no saved layout → fallback |
| Declares a root that cannot be reconstructed | no saved layout → fallback |
| Declares no root, no floating content | a legitimately empty arrangement |
| Names a panel this build no longer registers | that panel dropped, the rest restored |

The last two rows are the subtle ones. "Every panel closed" and "the saved
root was corrupt" both end with a null root, so they are distinguished
explicitly rather than conflated.

## What was preserved

- **`TD-70` responsive behaviour.** The working pane never gets starved by
  side panels. Now expressed against the tree, so it holds for *any*
  arrangement rather than three named docks — and wired to the window's own
  resize for the first time, closing a gap `TD-83` recorded (it previously
  existed but nothing except a test called it). A panel the layout
  collapsed on the user's behalf is re-expanded when the room returns; one
  the user collapsed themselves is left alone.
- **`TD-71` splitter preferences**, as durable proportions.
- **Ribbon minimisation**, untouched.
- **Collapse and auto-hide** (`WP 10.2B`), including the flyout.
- **Existing user preferences.** `WorkspaceLayoutMigration` carries a
  returning user's widths, visibility, collapse and pin state into the
  equivalent tree on first launch after the upgrade, preserving a recorded
  width as the exact fraction it was.
- **`IWorkspaceLayout`** (`WP 8.0B`), as a live **projection** of the tree
  rather than a stale second account — `InferEdge` recovers the edge,
  `ShareOf` the size.

## What was deleted

`DockingGrid`, `PanelHostControl` and `PredefinedLayouts`, with their
tests. Their guarantees were re-proven against the new host rather than
carried across, which is more work than adapting the assertions and the
only way to know the *behaviour* survived.

## Not attempted

- Focus preservation across a re-render (`TD-90`).
- `IWorkspaceLayout` cannot express tabbing or floating; its projection is
  lossy for those arrangements by construction (`TD-91`).
- No live drag preview adorner follows the pointer (`TD-92`).

## Proven by

- `tests/Tempest.Core.Tests/Workspace/Layout/` — 81 tests over the model,
  drop-zone geometry, serialisation, presets and migration, with no UI.
- `tests/Tempest.Desktop.Tests/WorkspaceLayoutHostTests.cs` — 25 tests over
  the renderer.
- `tests/Tempest.Desktop.Tests/WorkspaceLayoutControllerTests.cs` — 13
  tests over drag-to-dock, floating and persistence.
- Eleven mutations run against the critical layout paths; all eleven
  killed, one of which exposed a real coverage gap before it was closed.

## Related Documents

`ADR-0095`; `ADR-0092` (which reserved it); `ADR-0064`; `ADR-0103`;
`TD-70`/`TD-71`/`TD-83`;
`docs/academy/02 Runtime Architecture/36-workspace-layout-and-docking.md`.

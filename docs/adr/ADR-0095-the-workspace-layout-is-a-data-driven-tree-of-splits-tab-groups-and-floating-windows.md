# ADR-0095: The Workspace Layout Is a Data-Driven Tree of Splits, Tab Groups and Floating Windows

## Status

Accepted — `TD-72` (Full Workspace Layout & Docking), 2026-08-29.

**This ADR number was reserved, and left unwritten, by `ADR-0092`
(`WP 10.0A`)** for precisely this question: *"`WorkspaceDockPosition`/
`WorkspacePanelPlacement` need a genuine contract extension — a graphical
desktop application supports undocking a panel into its own top-level
window... and placing that window on a specific monitor."* It has stood
reserved since 2026-08-05. This is the Work Package it was reserved for.

## Context

The docking surface shipped by `WP 10.0B` and extended by `WP 10.2B` was a
compile-time `Grid`: five columns and three rows, with panels assigned to
named slots once at composition time (`SetLeftPanel`/`SetRightPanel`/
`SetBottomPanel`). It served well and was honestly disclosed as what it
was — a hand-rolled host, chosen because `WorkspaceDockPosition` had no
`Floating` value and a full docking framework's capability would have been
unused surface.

That is no longer true. The original TempestOS design brief and all five
desktop mock-ups require fully dockable workspaces: several modules and
documents open at once, side by side, arranged by the user. Against a
fixed grid, none of it is *expressible*:

- **Drag-to-dock** has nowhere to drop to — there are three slots, and they
  are already occupied.
- **Tabbed panel groups** have no representation at all.
- **Arbitrary splitting** is capped at one horizontal and one vertical
  division, fixed at compile time.
- **Floating panels** contradict `WorkspaceDockPosition`, which has only
  `Left`, `Right` and `Bottom`.
- **Extensibility** fails at the fourth panel: `DigitalThreadGraphView`'s
  own source recorded that it became a document tab rather than a panel
  because *"there are exactly three physical dock slots, all already
  occupied"*. The layout was actively shaping the product.

The controlling instruction for `TD-72` is explicit: *"Do not merely add
drag handles to the existing 5×3 `DockingGrid`; replace the underlying
abstraction properly."*

## Decision

**1. The layout is a tree, and the tree is data.**

```
WorkspaceLayoutTree
  ├── Root:     WorkspaceLayoutNode?
  │               ├── LayoutSplitNode(Orientation, Children[], Weights[])
  │               └── LayoutTabGroupNode(PanelIds[], SelectedIndex)
  ├── Floating: FloatingLayoutWindow[]  (subtree + screen rectangle)
  └── Panels:   PanelId → PanelPresentation(IsPinned, IsCollapsed)
```

Arbitrary nesting, either orientation, any depth. A single docked panel is
a tab group of one, which is what makes "drag a panel onto another to tab
them together" an ordinary operation rather than a special case.

**2. Every layout operation is a pure function.**

`Dock`, `DockToEdge`, `Float`, `MoveFloating`, `Remove`, `SelectPanel`,
`SetWeights`, `SetPinned`, `SetCollapsed` each return a **new** tree,
normalised. The model cannot hold a half-applied drag, an empty tab group,
or a split whose weights disagree with its children. Two consequences
matter more than the elegance: the entire docking system is testable with
no UI in the process, and the renderer becomes a total function of the
model rather than a second place where arrangement decisions are made.

**3. Sizing is proportional, never pixels.**

Splits carry weights summing to one; the renderer maps them to star
sizing. A layout restored into a smaller window, or onto a different
monitor, keeps its proportions instead of pushing the working pane off the
edge. Normalisation is applied on construction — including when
deserialising — so a stored layout can never divide by zero or produce a
pane of infinite width.

**4. There is no privileged centre slot: the document area is a panel.**

This is the extensibility decision. A future surface — the Drawing Viewer
(`TD-80`), Materials, Calculations, a Tasks board — participates in
docking, tabbing, splitting, floating and persistence by registering one
`WorkspacePanelDescriptor`. There is no per-panel code in the model, the
renderer, or the drag logic, and no slot to compete for.

**5. Floating panels are real top-level windows.**

Not in-window overlays pretending to float. The operating system places
them, the user drags them onto whichever display they like, and their
**screen** coordinates are what persist — which is what makes
multi-monitor work restorable. A floating window hosts the same renderer
as the main window, so it can itself contain tabs and splits.

**6. Drop-zone geometry is a pure function, separate from the UI.**

`DockTargetResolver` maps a pointer position and a set of candidate
rectangles to one of five zones. Drag-to-dock is the gesture most likely
to be subtly wrong at the edges, and those cases are painful to click
through and trivial to enumerate as a function.

**7. Persistence is the serialised tree, on the settings substrate.**

A hand-written, versioned DTO rather than polymorphic serialisation of the
model types, so renaming a node type cannot orphan every saved layout.
Reading is **total**: malformed, truncated, foreign or structurally
impossible input returns "no saved layout" and the caller falls back to a
default. A corrupt layout costs a user their panel positions, never their
session. This is application session state (`ADR-0064`), deliberately not
the engineering persistence authority (`TD-85`) — where someone put their
panels is not engineering data.

**8. `IWorkspaceLayout` is retained as a projection, not frozen out.**

The `WP 8.0B` contract speaks in edges, sizes and visibility, and
Workspace-layer consumers still read it. Rather than leave it stale — a
second, disagreeing account of where the panels are — it is **derived from
the tree after every change**: `InferEdge` recovers Left/Right/Bottom by
finding the split that separates a panel from the document, and
`ShareOf` supplies the size. `WorkspaceDockPosition` gains no `Floating`
member; a floating or tabbed panel reports the nearest honest answer,
which is a real limitation of the old shape, disclosed rather than papered
over.

## Consequences

**Positive:**

- Every capability the brief and mock-ups require is now expressible, and
  implemented: drag-to-dock, tabbed groups, arbitrary splitting, floating
  windows, collapse, auto-hide, resize, persistence, responsive behaviour.
- The docking system is provable without a UI. The model, the drop-zone
  geometry, the presets and the preference migration are all pure and
  exhaustively tested; the renderer is tested separately as a function of
  the model.
- `TD-70`'s responsive guarantee is now expressed against the tree, so it
  holds for **any** arrangement a user builds rather than for three named
  docks — and it is wired to the running window's own resize for the first
  time, closing a real gap `TD-83` recorded (it previously existed but
  nothing except a test ever called it).
- Adding a surface is one registration. The reason
  `DigitalThreadGraphView` gave for not being a panel no longer exists.
- Existing preferences survive: `WorkspaceLayoutMigration` carries a
  returning user's widths, visibility, collapse and pin state into the
  equivalent tree on first launch after the upgrade.

**Negative:**

- A layout change re-renders the whole tree and reparents panel content.
  Content controls are long-lived singletons so selection and scroll state
  survive, but focus is not explicitly preserved across a re-render
  (`TD-90`).
- `IWorkspaceLayout` cannot express tabbing or floating. Its projection is
  therefore lossy for those arrangements, by construction (`TD-91`).
- Drag-to-dock has no live preview adorner following the pointer; the
  drop target is resolved and applied on release. The gesture is complete
  and correct, the feedback during it is minimal (`TD-92`).
- Three types were deleted — `DockingGrid`, `PanelHostControl`,
  `PredefinedLayouts` — along with their tests. Their guarantees were
  re-proven against the new host rather than carried, which is more work
  than adapting them but is the only way to be sure the behaviour survived
  rather than the assertions.

## Alternatives Considered

**Adding drag handles and a `Floating` enum member to the existing grid** —
rejected, and explicitly forbidden by the controlling instruction. The
fixed slot count is the actual constraint; a `Floating` value would not
have made tabbing or arbitrary splitting expressible.

**Adopting a third-party docking library** — rejected. It would have
imposed its own layout model and persistence format on a platform whose
whole architecture is built on owning its contracts, and would have made
the layout untestable without its runtime. The model here is ~400 lines of
pure data and functions.

**Mutable layout objects with events** — rejected. Docking edits are
multi-step (remove, insert, normalise); with mutation, an exception
partway leaves a corrupt arrangement on screen. Pure functions make a
failed operation a no-op by construction.

**Keeping pixel sizes** — rejected. A layout saved on a 4K monitor and
restored on a laptop would have starved the working pane, which is the
exact failure `TD-70` exists to prevent.

## Related Documents

- `ADR-0092` — reserved this ADR number for this question.
- `ADR-0064` — the settings substrate the arrangement persists through.
- `ADR-0103` — the Desktop composition-root pattern the controller follows.
- `TD-70`/`TD-71` — responsive workspace and splitter preferences, preserved.
- `TD-83` — the untested-resize gap this closes.
- `docs/architecture/Workspace Layout & Docking Architecture.md`
- `docs/academy/02 Runtime Architecture/36-workspace-layout-and-docking.md`

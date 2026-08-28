# Responsive Workspace & Ribbon Minimisation

**Work Package:** Product Compliance Audit, 2026-08-28 · **Debt closed:**
`TD-70`, `TD-71` · **Code:** `Tempest.Desktop.Docking.DockingGrid`,
`Tempest.Desktop.Views.RibbonView`

## The defect, measured rather than assumed

A compliance audit asked a simple question of the desktop shell: *does it
stay usable as the window gets smaller?* The honest way to answer that is
not to read the layout code — it is to boot the real window and measure
it. A headless probe did exactly that, arranging the real `MainWindow`
at seven widths from 1920 px down to 640 px.

The result:

```
WINDOW 960x600  (the application's own MinWidth/MinHeight)
   dockgrid: left=240  centre=472  right=240
   ribbon:   h=145 (fixed)
```

Both side docks were declared as `ColumnDefinition(240, GridUnitType.Pixel)`
— **fixed pixels**. Only the centre column was `Star`. So every pixel the
window lost came out of the Document Area, the one place the engineer
actually works. At the application's own minimum window size the two side
panels took **half the window**. The ribbon held a further 145 px of
height with no way to reclaim it.

A repository-wide search confirmed the cause was categorical, not local:
**no `SizeChanged` handler, no `Bounds` observation, no breakpoint and no
compact mode existed anywhere in `Tempest.Desktop`.** The window resized;
the layout did not adapt. Those are different things, and only measurement
tells them apart.

## The lesson: "it resizes" is not "it is responsive"

A `Grid` with a `Star` column technically reflows at any size. It will
also happily reflow into uselessness. The distinction that matters is
whether the layout has a **model of what must be preserved**. Ours now
does: the Document Area has a floor, and the docks are the flexible party.

## The fix, and the constraint that shaped it

`DockingGrid.ApplyResponsiveLayout(width, height)` runs whenever `Bounds`
changes and fits the docks into what is left after reserving
`MinDocumentAreaWidth` (420 px) and `MinDocumentAreaHeight` (220 px):

1. **Fits?** Leave every dock at its preferred size — on any ordinary
   display nothing changes at all. A responsive rule that alters the
   common case is a surprise, not an improvement.
2. **Doesn't fit?** Squeeze the docks proportionally.
3. **Squeezed below readable (`MinUsablePanelSize`, 140 px)?** Collapse
   that dock to its 32 px strip instead. A 60 px panel helps nobody; the
   strip keeps the expand affordance reachable and hands the rest back.

The constraint that shaped the design: **never overwrite the user's own
preference.** The preferred width is held separately (`_preferredLeftWidth`)
from the applied column width. Narrowing the window changes only what is
*applied*; widening it restores the preference in full. A responsive rule
that quietly rewrites user intent is a bug wearing a feature's clothes.

Two subtleties worth keeping:

- **Observe `Bounds`; do not clamp inside `ArrangeOverride`.** Mutating
  column definitions during the layout pass that produced them invites
  re-entrancy. Subscribing to `BoundsProperty` keeps the mutation outside
  the pass.
- **A splitter drag is a preference, not a transient** (`TD-71`). A real
  `GridSplitter` drag mutates the `ColumnDefinition` directly and never
  calls `SetLeftWidth`, so the preference field was never updated — hide
  and re-show a panel after dragging it and the drag was silently
  discarded. `NotifyLeftPanelResized` now records it. The pre-existing
  test suite missed this precisely because it drove `SetLeftWidth` rather
  than the drag path.

For the ribbon, `SetCollapsed`/`ToggleCollapsed` hide the tab *content*
while keeping every tab *header* — the convention every ribbon
application shares (double-click a tab, or **View ▸ Minimise Ribbon**),
persisted across restarts. The invariant that makes it safe: no command
becomes unreachable, because the tab strip survives.

## How we know the tests are worth having

Three mutations were run against the finished code:

| Mutation | Result |
|---|---|
| `ApplyResponsiveLayout` returns immediately | **5 tests fail** |
| Ribbon collapse becomes a no-op | **2 tests fail** |
| Drag no longer records the preference | **1 test fails** |

The third mutation is the instructive one: it **survived the first
version** of its own test, because that test called `SetLeftWidth(400)` —
which already sets the preference — instead of mutating the column the way
a real drag does. The test asserted the right outcome through the wrong
path, and only the mutation exposed it. A test you have not tried to break
is a test whose value you are guessing at.

A second lesson arrived from the full suite: the new tests passed in
isolation and **failed 15-way when run with everything else**, because
they were plain `[Fact]`s constructing Avalonia controls outside the
headless application context. Running the filtered subset would have
shipped that. Run the whole suite.

## What this deliberately did not do

This closed a *responsiveness* gap, not the *docking* gap. Panels still
cannot be dragged between docks, tabbed together, or floated into their
own window; the navigation and ribbon do not yet switch to compact,
icon-only presentations; and `MinWidth`/`MinHeight` of 960 × 600 still bars
genuinely small displays. Those are tracked honestly as `TD-72`/`TD-73`
and `FCR-0064` — not quietly folded into this change to make the
compliance matrix look greener than the product is.

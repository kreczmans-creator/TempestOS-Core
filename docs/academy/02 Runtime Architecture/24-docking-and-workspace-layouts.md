# Docking & Workspace Layouts

## 1. Introduction

`WP 10.2B`'s own concept guide — how TempestOS built a professional
dockable workspace (Bottom dock, Collapse, Auto-Hide, predefined
layout presets, an Output panel) entirely without touching any of the
twelve frozen `WP8.0B` Workspace contracts, and how Collapse and
Auto-Hide, though they share one visual affordance, remain two
genuinely distinct mechanisms underneath it.

## 2. Purpose

Explains the "extend additively at the Desktop layer, never reopen a
frozen shape" discipline this platform has applied at the contract
level since `ADR-0080`/`ADR-0082` (`WP 9.0A`) and `ADR-0096` (`WP
10.2A`), taken one step further here: not merely an *additive*
extension, but *no contract change of any kind* — every named scope
item realised using capability the contracts already exposed, or new
types placed entirely one layer up.

## 3. Background

`WorkspaceDockPosition` (`WP 8.0B`) has always declared three values —
`Left`, `Right`, `Bottom` — but `DockingGrid` (`WP 10.0B`) only ever
built a five-column Left/Centre/Right layout. `Bottom` sat unused for
three Work Packages (`WP 10.0B`, `WP 10.1A`, `WP 10.1B`, `WP 10.2A`)
before this Work Package finally wired it to a real dock surface — a
concrete instance of a contract designed slightly ahead of its own
first real consumer, a pattern this platform has seen before
(`IWorkspaceLayout` itself was specified, `WP 8.0B`, before any real
Engineering Discipline existed to exercise it, `WP 9.0A` onward).

## 4. The Problem

Three related problems, addressed together:

1. **No status/output surface existed.** `StatusBarView` (`WP 10.2A`)
   is a single-line strip — real, but not a place to browse module/
   hosted-service detail.
2. **Hide was the only "get it out of the way" affordance.** A user
   wanting Explorer/Inspector out of the way temporarily had one
   option — fully hide it, losing the panel's own presence entirely,
   with no lighter-weight "just make it smaller" or "keep it reachable
   but out of the way" option.
3. **No named, repeatable arrangement existed** beyond the one default
   — a user switching between "authoring" and "reviewing" had to
   manually resize/hide panels by hand every time, with no way to
   return to a known-good arrangement in one action beyond the single
   documented default.

## 5. The Design

**Bottom dock**: `DockingGrid` gained a third `RowDefinition` row
(mirroring its own existing column pattern exactly) and a horizontal
`GridSplitter` spanning all five columns — the identical shape its own
two vertical splitters already established, applied a third time, to
a different axis.

**Collapse vs. Auto-Hide — one visual affordance, two behaviours**:
both shrink a panel to a fixed-width `CollapsedStripSize` strip.
Collapse does this *in place*, inside the panel's own normal dock
column/row — clicking the strip expands it back, instantly, no
overlay. Auto-Hide additionally removes the panel from the reserved
layout *entirely*, handing that space to the Document Area — clicking
its own strip instead opens a temporary overlay (`DockingGrid.ShowFlyout`)
on top of the Document Area, by repositioning the *same*
`PanelHostControl` instance via `Grid` attached properties and
`ZIndex`, never constructing a second control and never opening a
second, floating OS window.

**Predefined layouts**: three named presets (`PredefinedLayouts`), each
nothing but a fixed combination of an existing `WorkspacePanelPlacement`
pair plus this Work Package's own Desktop-local Output/pin state.
Applying one calls `IWorkspaceLayout.SetPlacement` — the same member
every ordinary manual resize already calls.

**Desktop-local persistence**: Collapse/Auto-Hide/Output state has no
home in the frozen `WorkspacePanelPlacement` shape (Dock Position,
Size, Visibility only) — rather than extend that record, a sibling
class, `DesktopPanelUiState`, persists through a second
`ISettingsProvider` key, reusing `ADR-0064`'s own established pattern a
second time for a second, independent concern.

## 6. Alternatives Considered

- **Extend `WorkspacePanelPlacement` with `IsCollapsed`/`IsPinned`
  fields.** Rejected — would have made three new, Desktop-only
  concepts part of a contract every non-graphical future presentation
  layer would also have to carry, even though none of the three has
  any meaning outside a graphical shell (a terminal UI has no "thin
  strip" concept).
- **A real, hover-triggered flyout with a dwell timer**, matching VS
  Code/Visual Studio's own default Auto-Hide behaviour. Rejected for
  this first implementation — click-to-reveal/click-away-or-`Escape`-
  to-dismiss is deterministic, keyboard-accessible immediately, and
  testable headlessly without simulating pointer dwell time; disclosed
  as a real, honest trade-off (`WP10.2B UX Review.md` §3), not claimed
  as strictly superior.
- **A genuine floating/undocked panel window.** Explicitly out of
  scope (`ADR-0095` remains reserved); the Auto-Hide flyout achieves a
  similar "temporarily see it without it taking permanent space"
  outcome without ever opening a second top-level window.

## 7. Why This Solution Was Chosen

Every alternative considered above would have either reopened a
frozen contract for no functional gain, or introduced complexity
(hover-dwell timing, a second window) disproportionate to the concrete
need — resolving to the simplest design that satisfies every named
scope item using capability the platform already had, or a clean,
narrowly-scoped Desktop-local addition where it did not.

## 8. Architectural Principles

- **Separation of Concerns** — Workspace-layer contracts describe
  *what* is dockable/visible/sized; Desktop-layer classes decide *how*
  that gets drawn, including presentation-only states like Collapse.
- **Composition Over Inheritance** — `PanelHostControl` composes a
  header, a body, and a collapsed-strip child, swapping which is
  visible, rather than three different subclasses.
- **Fail Fast / Defensive Programming** — every new public method
  (`DockingGrid.ShowFlyout`, `SetBottomPanel`, etc.) null-checks its
  own `Control` parameter, mirroring every existing sibling method.
- **Single Responsibility** — `DesktopPanelUiState` does exactly one
  thing (persist Desktop-local panel UI flags); it does not also know
  how to apply them to a `DockingGrid` — that wiring lives in
  `MainWindow`, the composition root.

## 9. Benefits

- Zero contract change — the strongest compatibility result any real-
  discipline or presentation Work Package has achieved since `WP
  8.0B` froze the twelve.
- `Bottom` dock, dormant since `WP 8.0B`, finally has a real consumer.
- Collapse and Auto-Hide reuse one visual affordance, minimising new
  surface area, while remaining behaviourally distinct where it
  matters.
- Predefined layouts cost zero new Workspace-layer capability —
  purely a Desktop-layer convenience over already-legal state.

## 10. Trade-offs

- Click-to-dismiss over hover-to-peek (§6, above) — one or two extra
  clicks per peek, in exchange for determinism and immediate keyboard
  parity.
- `PanelHostControl`'s own overlay background is a fixed brush, not
  theme-variant-aware (`TD-39`) — a genuine, disclosed, cosmetic gap,
  shared with `CommandPaletteOverlay`'s own identical, previously-
  unregistered limitation.
- Click-away dismissal only fires from the Document Area specifically,
  not the toolbar/menu/status bar — `Escape` covers the gap for
  keyboard users.

## 11. Common Mistakes

- **Conflating Collapse and Auto-Hide** because they share one visual
  affordance — a reader of `PanelHostControl.cs` should notice
  `IsStripShowing = _collapsed || !_pinned` computes the *shared*
  visual trigger, while `_collapsed` and `_pinned` remain two
  independently-toggleable, independently-persisted flags underneath.
- **Assuming the Auto-Hide flyout is a second control.** It is the
  same `PanelHostControl` instance, repositioned — a common mistake
  when first reading `ShowFlyout` would be to look for a second
  `new PanelHostControl(...)` call that does not exist.
- **Assuming "predefined layouts" needed a new Workspace-layer
  concept.** It needed none — `PredefinedLayouts` is a plain static
  class returning already-legal `WorkspacePanelPlacement` values.

## 12. Future Evolution

- `FCR-0067` — Theme-Variant-Aware Overlay Backgrounds, fixing `TD-39`
  for both `CommandPaletteOverlay` and `PanelHostControl` together.
- A genuine hover-to-peek Auto-Hide mode, if real usage demonstrates
  the click-to-dismiss model's own extra clicks are a real friction
  point (no Future Capability entry raised yet — no demonstrated need
  exists).
- `ADR-0095` (floating/multi-monitor panel contract extension) remains
  reserved, unaffected by this Work Package's own in-window-only Auto-
  Hide flyout.

## 13. Key Takeaways

Docking, Collapse, Auto-Hide, predefined layouts, and a new dockable
Output panel were all realised without changing a single one of the
twelve frozen `WP8.0B` Workspace contracts — every genuinely new
concept lives one layer up, in `Tempest.Desktop`, reached either
through the contracts' own already-documented extensibility points
(`IWorkspacePanel`, implemented a fourth time) or through a clean,
narrowly-scoped sibling to an already-established pattern
(`DesktopPanelUiState`, alongside `WorkspaceState`).

## Related Documents

`WP10.2B Implementation Report.md`; `WP10.2B Architecture Review.md`;
`WP10.2B UX Review.md`; `docs/adr/ADR-0064-...md`; `23-workspace-modernisation.md`
(the prior concept guide this one extends).

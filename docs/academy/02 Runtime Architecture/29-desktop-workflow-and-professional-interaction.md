# Desktop Workflow & Professional Interaction

## 1. Introduction

`WP 10.5B`'s own concept guide — how TempestOS built its first
genuinely unified Dialog Framework, its first real window-geometry
persistence and graceful-shutdown gate, its first end-to-end
object-creation workflow, and the Notification Framework's own first
real Desktop consumer — while honestly naming, not hiding, every named
interaction pattern it deliberately did not build.

## 2. Purpose

Explains why four dialogs with no shared base class still count as "a
common framework," why exactly one discipline's Create/Duplicate flow
was wired end-to-end rather than eight shallow ones, and documents two
genuine implementation-time findings — one a real, pre-existing
reliability gap in shipped code, one a test-precision correction —
both found, both fixed, before commit.

## 3. Background

`WP 10.5A`'s own polish pass gave the platform its first real
theme-reactive brush infrastructure and its first reusable feedback
controls (`ToastHost`, `BusyOverlay`, `ConfirmationDialog`,
`EmptyStateView`). This Work Package's own controlling instruction asked
for the next, larger step — not more polish, but real *workflow*:
dialogs a user can act through, window behaviour that survives a
restart, a notification system with a real consumer, and settings
genuinely separate from Engineering data. The instruction named an
enormous surface (fourteen dialog kinds, a dozen window/workflow
behaviours, a full Notification Framework, User Settings, Recent
Activity, Professional Error Handling). Every prior large-scope Work
Package this session established the same discipline for a scope this
size: build a real, cohesive foundation, apply it to the highest-value
surfaces first, and disclose directly what received lighter treatment.
This guide follows that same discipline.

## 4. The Problem

Before this Work Package, `Tempest.Desktop` had exactly one dialog
(`ConfirmationDialog`, `WP 10.5A`), no window-geometry persistence at
all (every launch opened at a fixed default size and position), Delete
executed immediately with zero confirmation gate anywhere, no user-facing
settings surface existed outside the theme toggle, and
`IPlatformNotification` — a real, working, pre-existing event —
was published by `Tempest.Core`/sample modules but consumed by nothing
in the graphical Workspace, confirmed directly by a whole-repository
search.

## 5. The Design

**Four dialogs, one shared styling language, no shared base class.**
`InputDialog` and `MessageDialog` (both new) join `ConfirmationDialog`
(`WP 10.5A`) and the new `SettingsDialog`. A shared abstract base class
was considered and rejected — each dialog's own layout genuinely
differs enough (a single text field vs. a severity glyph plus
collapsible details vs. three labelled preference sections) that a
common base would need enough virtual seams to cost more than it saves.
Consistency instead comes from all four sharing `DesignTokens`/
`ApplicationPalette` resource keys directly.

**Delete Confirmation, wired once, consumed three times.** A single
`Func<string, Task<bool>>?` delegate (`ConfirmDeleteAsync`), added to
both `ProjectExplorerView` and `RibbonView`, set once by `MainWindow`,
covers the Ribbon Delete button, the Project Explorer context menu, and
the `Delete` key — one implementation, three call sites, gated by the
new `UserSettings.ConfirmBeforeDelete`.

**Real window persistence, with a real multi-monitor safety net.**
`WindowUiState` mirrors `DesktopPanelUiState`'s own established
`ISettingsProvider`-backed shape. `ClampToVisibleScreen` recentres a
restored window on the primary screen if its persisted position no
longer falls on any currently-connected screen — the one multi-monitor
scenario this Work Package could actually verify without a physical
rig.

**One real, complete, end-to-end object-creation workflow — not eight
shallow ones.** `RibbonView.ObjectCreationHandlers`, a new
`CommandDescriptor.Id`-keyed dictionary of real dispatch flows, wires
exactly two: `mechanical.create` (`InputDialog` name prompt →
`ICommandDispatcher.DispatchAsync(new CreateMechanicalObjectCommand(...))`)
and `mechanical.duplicate` (`ConfirmationDialog` only — the object is
already selected, no name prompt needed). Every other discipline's own
Create/Duplicate command still falls through to the pre-existing honest
"needs additional input" message.

**The Notification Framework's first real Desktop consumer.**
`PlatformNotificationToastBridge` implements
`IEventHandler<IPlatformNotification>`, subscribed via the
already-existing `IEventBus`, forwarding every publication to
`ToastHost` — zero new publisher-side knowledge required.

## 6. Alternatives Considered

- **A shared `DialogBase` abstract class** for the four dialogs —
  considered, rejected; see §5.
- **Auto-navigating to a newly created object** after
  `mechanical.create` succeeds — considered, rejected; `CommandResult`
  only carries `Succeeded`/`Message`, no structured new-object Id, and
  fabricating a lookup-by-name-and-recency heuristic was judged more
  likely to navigate to the wrong object than to genuinely help.
  Disclosed directly rather than built around.
- **A startup splash screen** — considered, deliberately not attempted;
  no way to visually verify a transient splash's own render/dismiss
  timing exists in this environment, a real risk of shipping something
  unverifiable (`FCR-0076`).
- **Wiring Create/Duplicate for all six disciplines** — considered,
  scoped down to Mechanical only; Requirements alone has three distinct
  Create command shapes, and a shallow, half-tested wiring across every
  discipline was judged worse than one genuinely complete, tested
  example other Work Packages can extend from (`FCR-0075`).

## 7. Why This Solution Was Chosen

Every alternative either risked shipping something unverifiable in this
environment (the splash screen, auto-navigation built on a guess) or
diluted a single Work Package's own effort across eight shallow
half-workflows instead of one real, complete, tested one. The chosen
scope gives the platform a genuine, proven pattern — Dialog Framework,
persistence, confirmation gating, notification consumption — that the
next Work Package can extend mechanically, rather than a thin layer
spread across every named bullet with none of it fully real.

## 8. Architectural Principles

- **Consistency comes from shared tokens, not a shared inheritance
  hierarchy** — four dialogs, zero common base class, uniform look
  because they all read the same `ApplicationPalette`/`DesignTokens`
  keys directly.
- **An optional, unwired delegate defaults to the pre-existing
  behaviour, never a silent behaviour change** — `ConfirmDeleteAsync`
  and `ObjectCreationHandlers` both leave every existing direct
  construction of `ProjectExplorerView`/`RibbonView` (every prior test,
  any future caller that does not wire them) working exactly as it did
  before this Work Package.
- **One genuinely complete workflow proves the pattern better than
  eight shallow ones** — `mechanical.create`/`mechanical.duplicate` are
  real, tested, end-to-end; every other discipline's own gap is named
  directly as real future work, not silently implied to already exist.

## 9. Benefits

Every future dialog need has a real, proven token-sharing pattern to
follow without inventing a base class. Every future destructive action
has a ready-made confirmation gate to opt into. Window state, panel
layout, and unsaved work are all handled through one consolidated,
real graceful-shutdown path instead of three uncoordinated ones. Any
module that already publishes `IPlatformNotification` now reaches a
real, visible Desktop surface with zero additional code.

## 10. Trade-offs

- Copy/Move/Export/Import remain unwired — no destination-picker dialog
  shape exists yet, and Export/Import have no underlying command to
  dispatch to at all (`FCR-0073`, `FCR-0074`).
- Only Mechanical's Create/Duplicate are wired (`FCR-0075`).
- No startup splash screen (`FCR-0076`).
- No keyboard-shortcut remapping or Ribbon/toolbar customisation —
  `UserSettings` cannot honestly persist a preference for a capability
  that does not exist (`FCR-0077`).
- Recent Searches is in-memory only, reset each launch — no
  cross-session persistence.
- `MessageDialog`/`ConfirmationDialog`/`SettingsDialog` do not move
  focus on open (`InputDialog` does) — a real, minor, disclosed gap.

## 11. Common Mistakes

- Assuming a Avalonia `TextBox`'s `TextChanged` routed event fires for
  a purely programmatic `.Text =` assignment — it does not reliably;
  `PropertyChanged`, filtered to `TextBox.TextProperty`, does. First
  found in `ObjectEditorView` (`WP 10.3A`), rediscovered independently
  in `ProjectExplorerView`'s own filter this Work Package, fixed the
  same way both times.
- Assuming `IDeletable.DeleteAsync` removes an object from its
  repository — it does not; this platform's delete is a deliberate soft
  delete (`IsDeleted` flag only), a correct, audit-preserving design,
  not a defect to work around.
- Writing a test-node-finder that returns "the first object found"
  against `InMemoryEngineeringObjectRepository` — its own
  `ConcurrentDictionary`-backed iteration order is unspecified
  (`TD-27`'s own named risk class), and a delete-path test needs a
  childless leaf node specifically, not merely *any* node.
- Assuming Avalonia awaits an async `Window.Closing` handler before
  proceeding — it does not; cancel synchronously first, do the real
  async work, then close again once it completes.

## 12. Future Evolution

- A Copy/Move destination-picker dialog and real dispatch wiring
  (`FCR-0073`).
- Export/Import commands, once a concrete interchange format is chosen
  (`FCR-0074`).
- Create/Duplicate wiring extended to the remaining five disciplines
  (`FCR-0075`).
- A startup splash screen, once visual verification is possible
  (`FCR-0076`).
- Keyboard-shortcut remapping and Ribbon/toolbar customisation
  (`FCR-0077`).

## 13. Key Takeaways

A Work Package instruction naming fourteen dialog kinds and dozens of
workflow/window/notification/settings behaviours is best served by
building one genuinely complete, reusable framework and one real,
end-to-end proof of the pattern — not a thin, half-working layer spread
across every named item. Two genuine findings (one a real,
pre-existing UI reliability gap, one a test-precision correction) were
found only because this Work Package's own required "repeat test
execution to identify flakes" discipline was followed seriously, and
both were fixed at their true source before commit, neither merely
patched over.

## Related Documents

- `WP10.5B Implementation Report.md`, `WP10.5B Engineering Review.md`,
  `WP10.5B Technical Debt Review.md`.
- Future Capability Register — `FCR-0073` through `FCR-0077` (all
  Identified here).
- `28-workspace-visual-polish.md` — the Work Package whose own
  `ConfirmationDialog`/`ApplicationPalette`/`DesignTokens` foundation
  this one built directly on top of.

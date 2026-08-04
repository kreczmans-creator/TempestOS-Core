# WP 8.0C — Engineering Workspace UX Specification — Workspace Behaviour Specification

## Purpose

The Workspace's own behaviour as a persistent, stateful application
across sessions, windows, and monitors — opening/switching projects,
saving/restoring layouts, selection synchronisation, filtering and
searching. Where a behaviour is already fully specified elsewhere
(project switching mechanics, `Navigation Maps.md` §5; Command Palette
search, `Interaction Specification.md` §1) this document cross-refers
rather than restates.

## 1. Opening Projects

**Today:** WP8.1A's shell starts against whatever project context the
running `ITempestHost` resolves; there is no in-Workspace "open
project" flow.

**Target:** the Engineering Cockpit (start-up default, `ADR-0069`)
offers "Open Project" alongside its own recent-projects list
(`Navigation Maps.md` §5). Opening:

1. Resolves the target project's own persistence root.
2. Loads its own last-saved layout via `IWorkspaceState`
   (`WorkspaceStateDto`, unchanged persistence mechanism, `ADR-0064`).
3. Navigates to the Engineering Cockpit scoped to that project.

No new persistence mechanism is introduced — this section names a UI
flow around `IWorkspaceState`'s own existing load/save, not a new
storage concept.

## 2. Switching Projects and Recent Projects

Fully specified in `Navigation Maps.md` §5 — cross-referenced here for
completeness, not restated.

## 3. Saving and Restoring Layouts

**Today:** `WorkspaceState` (WP8.1A, internal) already persists panel
placement and open-view state per session, restored automatically on
the next `WorkspaceManager.StartAsync` against the same persistence
root — proven by the shipped cross-restart session-restore test.

**Target, extending this:** **named layouts** — a user may save the
current panel arrangement, docked panel visibility, and open-tab set
under a name ("Review Layout," "Authoring Layout") and switch between
named layouts without losing either. This is an additive extension to
`WorkspaceStateDto`'s own existing shape (a keyed collection of saved
states instead of a single implicit one), not a new persistence
mechanism — still stored via `ISettingsProvider`, still JSON-serialised,
mirroring the same precedent WP8.1A already established.

**Autosave, unchanged in spirit:** layout changes (panel resize, tab
open/close) are persisted continuously, not on an explicit "save"
action — matching WP8.1A's own shipped behaviour; named layouts are a
deliberate snapshot on top of this continuous autosave, not a
replacement for it.

## 4. Docking Behaviour Details

Restates `WP8.0A Workspace Architecture Document.md` §8's own Docking
Strategy at the behaviour level:

- Project Explorer and Properties/Inspector dock to fixed edges (left
  and right respectively) — they do not float or dock to arbitrary
  positions, keeping the five-region layout (`UI Architecture.md` §1)
  structurally stable across sessions.
- Panels may be hidden (not merely collapsed to zero width) via
  `IWorkspacePanel.HideAsync` — hidden panels do not consume screen
  width, and reappear at their own last width when shown again.
- The Document Area's own tabs are the only element that reorders
  freely (`Interaction Specification.md` §3) — panel *positions* are
  fixed; panel *sizes* and tab *order* are the adjustable dimensions.

## 5. Multiple Windows

**Today:** a single running `WorkspaceShell` instance per
`ITempestHost` (itself single-use, per WP8.1A's own disclosed
finding) — no multi-window support exists.

**Target, disclosed tension (`UX Specification.md` §5):** this
specification's own user journeys (§ `User Journey Maps.md`) do not
strictly require multiple simultaneous windows — every journey's own
"jump opens a new tab" pattern satisfies the same underlying need
(working with two objects at once) within a single window. Multiple
windows would be a convenience (a Reviewer keeping the Cockpit visible
on one window while working a Document Area tab on another) rather than
a capability gap. This document does not specify multi-window
mechanics in detail, since doing so would presuppose a windowing
capability `ADR-0066`'s own terminal-based decision does not currently
offer — named as an open question for a future Work Package that
revisits `ADR-0066`, not designed here.

## 6. Multiple Monitors

**Today:** not applicable — a terminal-based single window has no
multi-monitor placement concept.

**Target, disclosed tension, elaborating `UX Specification.md` §5:**
true multi-monitor placement (dragging the Document Area to a second
physical display while Project Explorer remains on the first) is a
graphical-desktop-native behaviour with no natural terminal equivalent.
This specification names the *need* it would serve — the same
"keep the Cockpit visible while working elsewhere" need named in §5,
above, at greater scale — without specifying *how* it would work,
since that answer depends entirely on the still-open rendering
technology question. **This is the clearest single instance in the
entire specification where a rich UX ambition is bounded by, rather
than dictates, the platform's own current architectural decision** —
recorded here precisely so a future `ADR-0066` revisit starts from a
named requirement, not a vague aspiration.

## 7. Selection Synchronisation

A selection made in the Project Explorer updates the Properties panel
immediately (`WP8.0A Navigation Specification.md` §5, unchanged). This
document adds: **a selection made within an open Document Area tab
(e.g., selecting a linked object inside a calculation's own detail
view) also updates Properties/Inspector**, following the same
select-to-inspect rule uniformly — Properties/Inspector always reflects
the most recent selection *anywhere* in the Workspace, not only
Project Explorer selections. Opening (a new tab) never happens from a
plain selection, in the Document Area or the Project Explorer alike —
only the deliberate "open" verb does that (Principle 6, consistency).

## 8. Navigation History, Breadcrumbs, Filtering, Searching, Global Commands

Fully specified in `Navigation Maps.md` §3, §4, §7 and `Interaction
Specification.md` §1 — cross-referenced here for completeness, not
restated.

## Related Documents

`WP8.0C UX Specification.md` §5; `WP8.0A Workspace Architecture
Document.md` §8; `WP8.0C Navigation Maps.md`; `WP8.0C Interaction
Specification.md`; `ADR-0064`; `ADR-0066`.

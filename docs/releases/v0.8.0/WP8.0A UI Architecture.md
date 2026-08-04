# WP 8.0A — Engineering Workspace — UI Architecture

## Purpose

The detailed structural design for the Workspace's own presentation
layer — main window layout, docking strategy, view architecture, and
interaction patterns — expanding `WP8.0A Workspace Architecture
Document.md` §3, §7, §8, and §11. Architecture only; no code.

## 1. Main Window Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  Command Bar  (global navigation + command menu + search)        │
├───────────────┬────────────────────────────────┬─────────────────┤
│               │                                │                 │
│   Project     │        Document Area           │   Properties /  │
│   Explorer    │        (tabbed)                │   Digital       │
│   (dock:      │                                │   Thread panel  │
│    left)      │                                │   (dock: right) │
│               │                                │                 │
├───────────────┴────────────────────────────────┴─────────────────┤
│  Status Bar  (current selection, lifecycle status, Diagnostics)  │
└──────────────────────────────────────────────────────────────────┘
```

Five regions, a direct evolution of `TempestShell`'s own three:

| Region | Evolves From | Source |
|---|---|---|
| Command Bar | Navigation Region's own numbered list, plus nothing (commands were never surfaced in `TempestShell`) | `INavigationProvider.Items` (top-level) + `ICommandRegistry.GetDescriptors()` |
| Project Explorer | Navigation Region's own numbered list, specialised into a per-area tree | `INavigationProvider.Items` (hierarchy) + each Engineering Core service's own list/relationship reads |
| Document Area | Content Region, made tabbed (`TempestShell` renders exactly one page at a time) | Whatever object is currently open |
| Properties / Digital Thread panel | New — `TempestShell` has no equivalent | `FindAsync`/`GetRelationshipsAsync`/`GetEvidenceAsync` for the selected object |
| Status Bar | Finally populated — `TempestShell`'s own Status Bar is explicitly "reserved for future use" | `IDiagnosticsProvider` + current selection state |

The Command Bar carries global navigation (the top-level areas
`INavigationProvider` exposes) so that switching areas does not require
first closing whatever is open in the Document Area — consistent with
Workspace Philosophy Point 2 (no forced navigation away from context).

## 2. Docking Strategy

- **Dockable panels**: Project Explorer, Properties/Digital Thread.
  Each may be resized, collapsed, or closed independently; closing does
  not lose its own last state — reopening restores the same width/
  position it held before closing.
- **Fixed panels**: Command Bar (always top), Status Bar (always
  bottom), Document Area (always centre, always present — the one
  region that cannot be closed, since it is the Workspace's own primary
  work surface).
- **Default layout**: Project Explorer docked left at a fixed default
  width; Properties/Digital Thread docked right at a fixed default
  width; both resizable by the user from that default.
- **Not supported in this architecture's own first iteration**:
  undocking a panel into its own separate top-level window; splitting
  the Document Area into multiple side-by-side panes. Both are
  disclosed, deferred future capabilities (`WP8.0A Workspace
  Architecture Document.md` §"Deliberately Out of Scope") — no real
  demonstrated need for either exists yet, and both add real
  implementation complexity (independent window lifecycle, or a
  multi-pane layout model) this architecture does not commit to without
  evidence.

## 3. View Architecture

### 3.1 The View/Object Relationship

A View renders exactly one of:

1. **One engineering object** (a Requirement, a Material, a Calculation
   Record, a Verification Record, a Requirement Collection, a
   Requirement Group) — its own Identity, Revision History, Provenance,
   and discipline-specific facets (`WP8.0A Workspace Architecture
   Document.md` §6).
2. **One relationship list** — the Project Explorer's own per-node
   children, or a Properties panel's own "Relationships" facet.
3. **One composed digital-thread read** — the Digital Thread panel,
   backed by `GetEvidenceAsync` or a sibling framework's own equivalent.

No View renders more than one of these three at once — a Requirement's
own editor tab and its own Digital Thread panel are two separate Views,
open simultaneously, each independently reading its own data, never one
View internally branching on "am I showing the object or its thread."

### 3.2 Reads vs. Writes

| Operation | Path |
|---|---|
| Display a Requirement's current statement, status, revision history | `IRequirementsService.FindAsync` — direct read |
| Display a Requirement's relationships | `IRequirementsService.GetRelationshipsAsync` — direct read |
| Display a Requirement's digital thread | `IRequirementsService.GetEvidenceAsync` — direct read |
| Revise a Requirement's statement | A Command (`ReviseRequirementCommand`-shaped), dispatched via `ICommandDispatcher` |
| Change a Requirement's status | A Command, dispatched via `ICommandDispatcher` |
| Record a new relationship | A Command, dispatched via `ICommandDispatcher` |

This mirrors `ICommandDispatcher`'s own documented rationale for why it
is a separate contract from `ICommandRegistry`: a caller (a View) that
already holds a concrete, typed reference to what it wants to do
dispatches directly; nothing in this pattern is new to the Workspace,
only newly applied to presentation code specifically.

### 3.3 View Composition, Not Inheritance

Every View is built by composing existing service reads — no View
introduces its own query, cache, or index over engineering data. A
Requirement's own editor tab reads `FindAsync` once, on open; its
Properties panel independently reads whatever facet it displays; there
is no shared, Workspace-owned in-memory model duplicating what
`IEngineeringDocumentStore` (or a sibling framework) already holds
durably. This is a direct application of `FOUNDATION.md`'s own
Composition Over Inheritance principle to the presentation layer, and
of the reuse-of-existing-mechanism pattern every Engineering Core
framework has already independently reached (`WP7.4.0 Architecture
Baseline Summary.md`).

## 4. Interaction Patterns

| Pattern | Behaviour |
|---|---|
| Select (single click) | Project Explorer selection updates the Properties panel; no new tab opens |
| Open (double-click, or "Open" command) | Opens the object in a new Document Area tab, or focuses its existing tab if already open |
| Context menu (right-click) | Populated from `ICommandRegistry`, filtered to descriptors whose own applicability matches the selected object's `Kind` and current state |
| Jump-to-relationship (Digital Thread panel) | Opens the target object in a new Document Area tab, alongside the source, never replacing it |
| Tab close | Closes only that Document Area tab; does not affect Project Explorer selection or other open tabs |
| Keyboard navigation | Project Explorer and Document Area tabs are both keyboard-navigable; specific key bindings are an implementation-phase concern |

No drag-and-drop interaction is designed in this architecture's own
first iteration — no real demonstrated need for it has been identified,
and every interaction above already has a non-drag equivalent
(select-then-command, rather than drag-to-relate).

## 5. Workspace State Management

| State | Persisted Via | Scope |
|---|---|---|
| Panel layout (docked positions, sizes, open/closed) | `ISettingsProvider` | Per-user |
| Open Document Area tabs (which objects, in what order) | `ISettingsProvider` | Per-user |
| Last-selected Project Explorer node | `ISettingsProvider` | Per-user |
| Current object data (a Requirement's own statement, status, etc.) | Not Workspace state — read live from the owning service on every open | N/A (not cached) |

The distinction in the last row matters: the Workspace persists *how
the user left their own workspace arranged*, never a cached copy of
engineering data itself. Reopening a Requirement's own tab after a
restart re-reads `FindAsync` fresh — the Workspace introduces no
staleness risk, since it never holds its own copy of engineering data
across a session boundary.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0A Navigation
Specification.md`; `ADR-0062`; `ADR-0063`; `ADR-0064`;
`docs/architecture/Shell & Composition Framework Architecture.md`.

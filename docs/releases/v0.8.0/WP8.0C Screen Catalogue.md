# WP 8.0C — Engineering Workspace UX Specification — Screen Catalogue

## Purpose

Every screen and state the Engineering Workspace presents, each
answering `WP8.0C UX Specification.md` §1.9's own three questions
explicitly. "Today" marks what `WP 8.1A` already ships; "Target" marks
what this specification adds. No implementation detail, no rendering
technology.

## 1. Application Start-Up Experience

**What am I looking at?** A brief, informative loading sequence, not a
blank pause.
**What needs attention?** Nothing yet — start-up is not interactive.
**What should I do next?** Wait; start-up completes automatically into
either First-Run (§14) or the Engineering Cockpit (§2).

**Today:** `WorkspaceShell` prints a title banner, then the Host's own
lifecycle log lines, then the first rendered frame.
**Target:** A single, calm start-up screen — product name, version,
one-line status ("Loading workspace…") — the Host's own verbose
lifecycle logging remains available (diagnostics), but is not the
user-facing start-up experience by default.

## 2. Home Screen / Engineering Cockpit

**What am I looking at?** The engineer's own landing page — programme
health at a glance.
**What needs attention?** Surfaced directly in the Cockpit's own
"What Needs Attention" region (`WP8.0C Engineering Cockpit
Specification.md` §2) — never something the user must go looking for.
**What should I do next?** Open a project, open a flagged item, or
navigate to an area via the Command Bar.

**Today:** No Cockpit exists; `NavigationSampleModule`'s own
placeholder "Home" page is the closest analogue, and is not the
Workspace's own default view. **Target:** The Engineering Cockpit
(`ADR-0069`) is the default landing screen — see `WP8.0C Engineering
Cockpit Specification.md` for the complete definition.

## 3. Workspace Shell (Main Window Layout)

**What am I looking at?** The five-region layout
(`WP8.0A UI Architecture.md` §1) — Command Bar, Project Explorer,
Document Area, Properties/Digital Thread, Status Bar.
**What needs attention?** Whatever the Status Bar and Attention Centre
currently surface (§13).
**What should I do next?** Depends entirely on what is open/selected —
the shell itself has no single "next action," by design (it is a
frame, not a workflow).

**Today:** Implemented exactly as specified (`WP 8.1A`), terminal-
rendered. **Target:** Unchanged in structure; richer content within
each region as later Work Packages populate them.

## 4. Project Dashboard

**What am I looking at?** One project's own summary — distinct from
the Engineering Cockpit (which summarises across all open/recent
projects, or the one active project if only one is open).
**What needs attention?** This project's own open actions, risks, and
recently-changed items.
**What should I do next?** Drill into Requirements/Materials/
Calculations/Verification via the Project Explorer, or address a
flagged item directly from the dashboard.

**Today:** Does not exist. **Target:** A new screen, reachable from the
Engineering Cockpit (selecting a project) or directly via navigation
history/breadcrumbs (`WP8.0C Navigation Maps.md`).

## 5. Project Explorer

**What am I looking at?** The current area's own object tree
(`WP8.0A Navigation Specification.md` §3).
**What needs attention?** A status-indicator badge (§ Visual Language)
on any node whose own object needs attention, inherited from the
Attention Centre's own underlying data.
**What should I do next?** Select a node to inspect it; open it to
edit it; right-click for context-sensitive actions.

**Today:** Implemented, structurally empty (no `IProjectExplorerNodeProvider`
registered yet — `WP 8.1A`'s own explicit "no engineering functionality"
scope). **Target:** Populated once a real provider is registered
(`ADR-0067`); badges (above) are new, not yet specified at the contract
level — a disclosed gap for the next Contract Review.

## 6. Engineering Tree

Not a separate screen — the Project Explorer's own tree *is* the
Engineering Tree, named separately in this Work Package's own scope
because it deserves its own specification of what a node actually
shows: identity, a one-glyph status badge, and (on hover/focus) a
one-line summary — never more than that at the tree level; full detail
belongs to Properties (§9), never duplicated in the tree itself.

## 7. Navigation Behaviour

See `WP8.0C Navigation Maps.md` for the complete specification —
summarised: global areas (Command Bar) answer "what area," the Project
Explorer answers "what object within this area," and breadcrumbs/
history answer "how did I get here, and can I go back."

## 8. Workspace Layouts / Docking Behaviour

See `WP8.0C Workspace Behaviour Specification.md` §3-§4 for the
complete specification — summarised: panels dock left/right/bottom
(`WorkspaceDockPosition`, unchanged from `WP 8.0B`), are resizable and
collapsible, and a named layout can be saved and restored, extending
`IWorkspaceState`'s own existing single-layout persistence.

## 9. Properties Panel

**What am I looking at?** The selected object's own shared facets
(Identity, Revision, Provenance, Relationship) plus discipline-specific
facets — exactly `WP8.0A Navigation Specification.md` §4's own model.
**What needs attention?** A facet in an attention-worthy state (for
example, a Requirement in `Draft` status) is visually distinguished
from a routine one (§ Visual Language, Status indicators).
**What should I do next?** Edit (opens the object for editing) or act
via a context-sensitive command.

**Today:** Implemented; shows only Identity facets (Id, Kind), since no
engineering functionality exists yet to supply the rest (`WP 8.1A`'s
own disclosed scope limit). **Target:** Every facet kind populated once
a real object source exists.

## 10. Inspector Panel

Distinguished from Properties deliberately: **Properties** describes
*what an object is*; the **Inspector** is the Digital Thread view —
*what proves or relates to it* (`ADR-0065`'s own composed
`GetEvidenceAsync` read). Two panels, two questions, never merged into
one, per `WP8.0A Workspace Architecture Document.md` §8's own "a View
renders exactly one concern" discipline.

**What am I looking at?** Verification history and linked references
for the selected object, one flat, navigable list
(`WP8.0A Object Relationship Diagrams.md` §3).
**What needs attention?** An unresolved/failing verification entry,
visually distinguished.
**What should I do next?** "Jump to" a linked object (opens a new tab,
`WP8.0A UI Architecture.md` §4).

**Today:** Does not exist as its own panel (folded conceptually into
Properties' own "Relationship" facet kind in `WP 8.1A`'s minimal
shell). **Target:** A genuinely separate panel — a disclosed gap for
the next Contract Review (a thirteenth interface, `IDigitalThreadInspector`,
is a plausible name, not designed here since interface design is
explicitly out of this Work Package's own scope).

## 11. Status Bar

**What am I looking at?** A single line: current area/selection
summary, plus a global health indicator.
**What needs attention?** The health indicator itself, colour- and
glyph-coded (§ Visual Language).
**What should I do next?** Click/activate the health indicator to open
the Attention Centre (§13).

**Today:** Implemented; shows static "Ready."/selection text
(`WorkspaceStatusBar`, `WP 8.1A`). **Target:** Adds the global health
indicator and its own Attention Centre affordance.

## 12. Command Palette

**What am I looking at?** A single, global, keyboard-invoked search-and-
act surface — type a few characters, see every matching discoverable
action, invoke it directly.
**What needs attention?** N/A — the palette is user-invoked, not
ambient.
**What should I do next?** Type, narrow, select, done — the shortest
path in the entire Workspace (Principle 3).

**Today:** Does not exist. **Target:** `ADR-0070` — see
`WP8.0C Interaction Specification.md` §1 for the complete behaviour
specification.

## 13. Search Behaviour

Two distinct search surfaces, deliberately not merged:

- **Command Palette search** (§12) — searches *actions* (commands,
  navigation targets).
- **In-context search** — searches *objects* within the current area
  (a Project Explorer filter box) — narrows the visible tree, never
  navigates away from it.

## 14. Context Menus

**What am I looking at?** A short, relevant list of actions for exactly
the object right-clicked (or activated via a keyboard equivalent,
Principle 7) — populated from `ICommandRegistry`, filtered by
applicability (`WP8.0B Workspace Contracts.md` §12's own
`CanExecute`-closes-over-`IWorkspaceContext` design, unchanged).
**What needs attention?** Nothing — a context menu is itself the
"what should I do next" answer for its own target.

## 15. Toolbars

A small, fixed set of the *most common* actions for the current
screen, always visible without opening a menu — never a dumping ground
for every possible action (that is the Command Palette's own job, §12).
Context-sensitive: a toolbar's own buttons enable/disable exactly as a
context menu's own entries do.

## 16. Notifications

**What am I looking at?** A transient, dismissible message about
something that just happened (a command succeeded/failed, a background
operation completed).
**What needs attention?** Only if the notification itself signals a
problem — routine success notifications are brief and self-dismissing.
**What should I do next?** Usually nothing (routine); a failure
notification offers a direct action (retry, view detail) where one
exists.

Distinguished from the Attention Centre (§13, §17): a notification is
*ephemeral* (this just happened); the Attention Centre is *persistent*
(this is still true right now).

## 17. Attention / Action Centre

**What am I looking at?** Every currently-true "this needs attention"
fact across the open project(s) — blocked requirements, failing
verifications, overdue actions — in one place.
**What needs attention?** The whole screen *is* the answer to that
question.
**What should I do next?** Click through to the specific object and
act.

Full specification: `WP8.0C Engineering Cockpit Specification.md` §2
(the Attention Centre is the Cockpit's own central region, not a
separate screen reached a different way).

## 18. Digital Thread Visualisation

Covered by the Inspector Panel (§10) and `ADR-0065` (unchanged): a
flat, navigable list of verification history and linked references,
each with a "jump to" action. No graph visualisation (`ADR-0065`'s own
disclosed limitation, unchanged by this UX specification — a richer,
graphical thread view remains a plausible future capability, not
designed here).

## 19. Multi-Document Workflows

The Document Area is tabbed (`WP8.0A UI Architecture.md` §1); opening a
second object never closes the first (Workspace Philosophy Point 2,
unchanged). See `WP8.0C Workspace Behaviour Specification.md` §5 for
window/monitor-level multi-document behaviour.

## 20. Multi-Monitor Behaviour

See `WP8.0C Workspace Behaviour Specification.md` §6 — and the
Rendering Feasibility Disclosure (`WP8.0C UX Specification.md` §5): this
is the single named behaviour most in tension with `ADR-0066`'s own
current terminal-based decision, disclosed explicitly there, not
resolved here.

## 21. Keyboard Shortcuts

See `WP8.0C Interaction Specification.md` §2 for the complete
specification.

## 22. Mouse Interactions

See `WP8.0C Interaction Specification.md` §3 — select, open, right-click,
drag-to-resize-a-panel (never drag-to-relate, `WP8.0A UI
Architecture.md` §4's own unchanged "no drag-and-drop relate"
decision).

## 23. Empty States

**What am I looking at?** A screen with genuinely nothing to show yet
(an empty Project Explorer, no open documents).
**What needs attention?** Nothing — an empty state is not itself a
problem.
**What should I do next?** Always stated explicitly — "No documents
open. Open one from the Project Explorer," never a bare blank region.

**Today:** `WorkspaceShell`'s own literal rendered text
("no engineering module registered yet"; "(no documents open)";
"(nothing selected)") already follows this discipline exactly — the
one part of this specification `WP 8.1A` already satisfies precisely,
disclosed here as a positive finding, not assumed.

## 24. Loading States

**What am I looking at?** An operation in progress (opening a large
object, running a search).
**What needs attention?** Nothing, unless the operation is taking
unusually long (see Progress indicators, Visual Language).
**What should I do next?** Wait, or cancel if a cancel affordance
exists for that specific operation.

## 25. Error Presentation

**What am I looking at?** A clear statement of what failed and, where
knowable, why — never a raw exception message or stack trace surfaced
directly to the user. Mirrors this platform's own existing exception-
disclosure discipline (`WP7.3A Security Review Report.md`'s own
"Exception disclosure" dimension: only identifiers the caller already
knows, never internal state).
**What needs attention?** The error itself, plus any object it left in
an uncertain state.
**What should I do next?** Retry, undo (where meaningful), or
acknowledge and continue — always one of these three, never a dead end.

## 26. First-Run Experience

**What am I looking at?** The Engineering Cockpit (§2), empty, with an
explicit "Get Started" affordance rather than a bare empty dashboard.
**What needs attention?** Nothing — first run is not itself a problem
state, and must not be visually indistinguishable from an error.
**What should I do next?** Open or create a project — the one, clear,
primary action a first-run screen offers.

**Today:** `IWorkspaceState.LoadAsync`'s own existing "a missing value
yields defaults, never an exception" behaviour (`WP 8.1A`) already
makes first-run indistinguishable from any other run at the state
layer — this specification adds the *presentation* difference (the
explicit "Get Started" affordance) the state layer alone cannot supply.

## Related Documents

`WP8.0C UX Specification.md`; `WP8.0C Engineering Cockpit
Specification.md`; `WP8.0C Interaction Specification.md`;
`WP8.0C Navigation Maps.md`; `WP8.0C Workspace Behaviour
Specification.md`; `WP8.0A Workspace Architecture Document.md`;
`WP8.0B Workspace Contracts.md`.

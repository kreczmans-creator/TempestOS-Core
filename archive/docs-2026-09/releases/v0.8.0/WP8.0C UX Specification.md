# WP 8.0C — Engineering Workspace UX Specification

## Purpose

The complete specification of how the TempestOS Engineering Workspace
*feels* to use — a product and UX design exercise only. **No
implementation, no code, no UI framework selection, no rendering
technology.** This document is the master reference; nine companion
deliverables go deeper into one area each (`Screen Catalogue`,
`User Journey Maps`, `Interaction Specification`, `Navigation Maps`,
`Wireframe Sketches`, `Workspace Behaviour Specification`,
`Engineering Cockpit Specification`, plus Academy documentation).

## 0. Where This Sits, and a Disclosed Sequencing Finding

This Work Package specifies the experience `WP 8.0A` (architecture) and
`WP 8.0B` (contracts) made possible and `WP 8.1A` (shell
implementation) began building. **A genuine sequencing finding,
disclosed rather than silently absorbed:** `WP 8.1A` already shipped a
real, running, minimal shell — a numbered area list, three empty
panels, a one-line Status Bar — *before* this UX specification existed
to guide it. This is not a defect in `WP 8.1A`'s own work (it correctly
implemented exactly the twelve contracts `WP 8.0B` approved, with no
UX specification yet available to build toward), but it does mean a
gap now exists between what is running today and what this document
specifies as the target experience. That gap is named explicitly
throughout this document (each major section states its own "Today
vs. Target" delta) rather than presented as already satisfied.

**Recommendation carried forward to Future Capability tracking:** a
future Contract Review Work Package should reconcile `WP 8.0B`'s own
twelve contracts against this specification's own richer demands
(Command Palette, Engineering Cockpit, Attention Centre, navigation
history) before a second implementation Work Package builds them — the
identical two-stage discipline that served the Workspace well the
first time.

## 1. Design Principles

Nine principles, restated from this Work Package's own controlling
instruction and made concrete against the Workspace's own existing
architecture:

1. **Engineering first.** Every screen exists to help an engineer do
   engineering work — trace a requirement, check a verification status,
   review a calculation — never to showcase the platform itself.
2. **Information before decoration.** Density and clarity outrank
   visual novelty, directly answering this Work Package's own explicit
   priority ("prioritise engineering productivity, information
   density, clarity and minimal user effort over visual novelty").
3. **Minimise clicks (or keystrokes).** The shortest path between "I
   need to do X" and "X is done" is always preferred over a
   visually richer but longer one.
4. **Everything discoverable.** No capability is reachable only
   through a hidden gesture or memorised command — every action
   reachable from the Command Palette is also reachable from a menu or
   context menu, and vice versa (§ `Interaction Specification` §1).
5. **Progressive disclosure of complexity.** A new user sees the
   Engineering Cockpit and three empty panels; an expert user reaches
   the same screens but layers on filters, saved layouts, and keyboard-
   driven navigation without either mode blocking the other.
6. **Consistent interaction patterns.** Select-to-inspect, open-to-edit,
   right-click-for-context — the same three patterns
   `WP8.0A UI Architecture.md` §4 already established — apply
   identically whether the object is a Requirement, a Material, or a
   future discipline's own object.
7. **Keyboard-first operation where practical.** Every primary
   workflow (§ `User Journey Maps`) is completable without a mouse; a
   mouse remains a fully supported accelerant, never a requirement.
8. **Context-sensitive actions.** What a user can do next depends on
   what is selected and its own current state — mirroring
   `RequirementStatusTransitions`'s own existing discipline (only valid
   transitions are ever offered), extended here to every context menu
   and toolbar in the Workspace, not only status changes.
9. **Every screen answers three questions**, restated as this Work
   Package's own governing test, applied to every screen named in
   `Screen Catalogue.md`:
   - **What am I looking at?** (a clear, unambiguous title/breadcrumb)
   - **What needs attention?** (surfaced, not buried — the Attention
     Centre's own reason for existing, § `Engineering Cockpit
     Specification.md` §2)
   - **What should I do next?** (a clear primary action, never a blank
     screen with no next step)

## 2. Personas and Workflows

Five personas, matching this Work Package's own named workflows —
summarised here; full step-by-step journeys in
`WP8.0C User Journey Maps.md`:

| Persona | Primary Concern | Primary Screens |
|---|---|---|
| **Engineer** | Author and revise requirements, materials, calculations | Project Explorer, Document Area, Properties |
| **Project Manager** | Track programme health, risks, milestones | Engineering Cockpit, Project Dashboard |
| **Reviewer** | Verify claims, approve/reject status transitions | Digital Thread panel, Verification-adjacent context menus |
| **Calculation Author** | Produce and validate an engineering calculation | Document Area (calculation editor), Digital Thread |
| **Requirements Author** | Author, group, and trace requirements | Project Explorer (Requirements tree), Document Area |

`VISION.md`'s own stated target user ("an individual engineer or a
small professional engineering practice") plausibly occupies several of
these personas in one sitting — the Workspace does not assume separate
user accounts or role-gated screens (no such capability exists in
`Tempest.Core.Identity` beyond permissions already), only that each
persona's own *workflow* is a first-class, named path through the same
shared screens.

## 3. Visual Language (Intent, Not Implementation)

Per this Work Package's own explicit constraint ("do not specify
implementation technology"), every item below states *intent* — what a
future rendering technology (terminal or graphical, `ADR-0066`'s own
still-open narrower question) must honour, not how it renders it.

| Area | Intent |
|---|---|
| **Layout philosophy** | Five fixed regions (`WP8.0A UI Architecture.md` §1) — Command Bar, Project Explorer, Document Area, Properties/Digital Thread, Status Bar — never rearranged into a single scrolling column; information density over whitespace. |
| **Panel hierarchy** | Document Area is primary (largest, always present); Project Explorer and Properties are secondary (dockable, collapsible); Status Bar and Command Bar are tertiary (always visible, minimal height). |
| **Typography usage** | One primary weight for content, one heavier weight for titles/headers, one distinct treatment (monospace-equivalent) for identifiers/Ids — never more than three typographic levels on one screen. |
| **Iconography principles** | Symbolic keys only (`NavigationItem.Icon`'s own existing precedent, `WP 5.0A`) — every icon has a text label reachable on hover/focus; no icon-only affordance. |
| **Colour usage** | Meaning-bearing only, never decorative: red = blocking, amber = needs attention, green = healthy/verified, neutral = informational. Colour is always paired with a second signal (an icon or text label) for accessibility. |
| **Visual emphasis** | Bold/weight and position, not colour alone, distinguish primary from secondary actions. |
| **Status indicators** | A closed, small vocabulary reused everywhere: `Healthy`, `Attention`, `Blocked`, `Unknown` — never a screen-specific status vocabulary invented ad hoc. |
| **Progress indicators** | Determinate where the underlying operation reports real progress (a long-running command); indeterminate otherwise — never a fabricated percentage. |
| **Accessibility considerations** | Every interaction has a keyboard path (Principle 7); colour is never the sole signal (Colour usage, above); text scales independently of layout density. |
| **Light/Dark theme behaviour** | Both are first-class, not one "default" and one "alternate" — the status-indicator vocabulary and colour meanings (above) hold identically in both. |
| **Branding integration** | Minimal — a title and a wordmark in the Command Bar, nothing that competes with engineering content for visual priority (Principle 2). |

## 4. Summary of Companion Deliverables

| Deliverable | Covers |
|---|---|
| `WP8.0C Screen Catalogue.md` | Every named screen/state: start-up, Home/Cockpit, Workspace shell, Project Dashboard, Project Explorer, Engineering Tree, Properties, Inspector, Status Bar, Command Palette, Search, context menus, toolbars, Notifications, Attention Centre, Digital Thread, empty/loading/error states, first-run |
| `WP8.0C User Journey Maps.md` | The five personas' own typical workflows through a project lifecycle |
| `WP8.0C Interaction Specification.md` | Keyboard shortcuts, mouse interactions, docking behaviour, context-sensitive actions, Command Palette behaviour |
| `WP8.0C Navigation Maps.md` | Navigation behaviour, breadcrumbs, history, project switching/recent projects, global commands |
| `WP8.0C Wireframe Sketches.md` | Conceptual, technology-neutral sketches of the primary screens |
| `WP8.0C Workspace Behaviour Specification.md` | Opening/switching projects, saving/restoring layouts, multi-window/multi-monitor, filtering/searching |
| `WP8.0C Engineering Cockpit Specification.md` | The full landing-page dashboard |

## 5. Rendering Feasibility Disclosure

This specification is written to be renderable within `ADR-0066`'s own
current decision (terminal-based presentation) — every named screen
and interaction has a plausible terminal realisation (box-drawing
regions, ANSI colour, keyboard-driven menus). **Disclosed honestly:**
some behaviours this Work Package's own scope names — genuine
multi-window support, true multi-monitor placement, a floating,
overlay-style Command Palette — stretch what a terminal can express
elegantly relative to a graphical desktop framework. This specification
does not resolve that tension (resolving it would require choosing
implementation technology, explicitly out of this Work Package's own
scope) — it names the tension precisely, in `WP8.0C Workspace
Behaviour Specification.md` §5-§6, so the Work Package that eventually
revisits `ADR-0066` (if one ever does) inherits a precise account of
what is being traded off, not a vague sense that "the UX wants more."

## 6. ADR Summary

Two genuine, locked-in product decisions are written as full ADRs
(§ Related Documents) because both constrain future architecture, not
only visual presentation:

| ADR | Decision |
|---|---|
| `ADR-0069` | The Engineering Cockpit (not a placeholder Home page) is the Workspace's own default landing screen after start-up |
| `ADR-0070` | The Command Palette is a first-class, global entry point — every discoverable action is reachable from it, extending, not replacing, `ICommandRegistry`'s own existing discovery role |

No other item in this specification rises to an architectural boundary
decision — the remainder (colour meanings, panel hierarchy, keyboard
bindings) are product/UX decisions this document itself is the
authoritative record of, not decisions `docs/adr/` needs to duplicate.

## Related Documents

`WP8.0A Workspace Architecture Document.md` and its four companions;
`WP8.0B Workspace Contracts.md` and its three companions;
`WP8.1A Implementation Report.md`; `ADR-0062`–`ADR-0069`/`ADR-0070`;
`VISION.md`; `docs/academy/02 Runtime Architecture/
17-engineering-workspace.md`.

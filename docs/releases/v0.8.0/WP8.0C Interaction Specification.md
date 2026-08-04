# WP 8.0C — Engineering Workspace UX Specification — Interaction Specification

## Purpose

The complete interaction model — Command Palette behaviour, keyboard
shortcuts, mouse interactions, and context-sensitive actions. No
implementation detail, no specific key-binding table tied to a
rendering technology (a terminal and a graphical framework capture key
combinations differently) — this document specifies *what* every
interaction does, not the literal binding.

## 1. Command Palette (`ADR-0070`)

**Invocation.** One reserved, global activation gesture, available
from any screen, any selection state.

**Behaviour.**

1. Opens a single-line search input over (not replacing) the current
   screen.
2. As the user types, results narrow live — every match against a
   discoverable action's own display name, category, or description
   (`CommandDescriptor`'s own existing fields, unchanged).
3. Results are grouped by category, most-recently-used first within a
   category — never a flat, unordered list once more than a handful of
   matches exist.
4. Selecting a result invokes it immediately
   (`ICommandRegistry.InvokeAsync`, unchanged) and closes the palette.
5. Escaping closes the palette with no action taken.

**Completeness guarantee.** Every command registered in
`ICommandRegistry` is reachable from the palette — the palette is a
*view* over the existing registry, introducing no second registration
mechanism. A command not yet applicable to the current selection
appears but is shown disabled with a one-line reason (mirroring
`CommandDescriptor.CanExecute`'s own existing predicate, unchanged),
never hidden outright — Principle 4 ("everything discoverable")
applied literally: a user should be able to learn a command exists even
before they can use it.

**Also reaches navigation.** Typing an area name, a recently-viewed
object's own title, or a known identifier (a Requirement's own business
`Identifier`) surfaces a navigation result alongside command results —
one search surface for "do something" and "go somewhere," not two.

## 2. Keyboard Shortcuts

| Action Category | Behaviour |
|---|---|
| Open Command Palette | One reserved global gesture, never reassignable to avoid ambiguity across every other binding |
| Navigate between open tabs | A reserved next/previous gesture, wrapping at the ends |
| Close current tab | A reserved gesture, respecting `IWorkspaceView.CloseAsync`'s own unsaved-edit confirmation (`WP8.0B Workspace Contracts.md` §3) |
| Move focus between panels (Explorer/Document/Properties) | A reserved cycle gesture |
| In-context search (Project Explorer filter) | A reserved gesture, scoped to whichever panel currently has focus |
| Context menu (keyboard equivalent of right-click) | A reserved gesture, opens the same menu a right-click would for the currently focused item |
| Confirm / Cancel | Two reserved, universal gestures, consistent across every dialog and prompt in the Workspace |

No specific physical key is bound here — Principle 7 ("keyboard-first
operation where practical") is the requirement; the literal bindings
are an implementation-phase choice (per this Work Package's own "no
implementation" constraint), most naturally decided once a rendering
technology is chosen and its own platform conventions (if any) are
known.

## 3. Mouse Interactions

| Gesture | Behaviour |
|---|---|
| Single click (Project Explorer node) | Select — updates Properties, no navigation (`WP8.0A Navigation Specification.md` §5, unchanged) |
| Double click (Project Explorer node, or a closed tab's own title) | Open — new Document Area tab, or focus existing (`INavigationService.OpenAsync`, unchanged) |
| Right click | Context menu, filtered to the clicked object's own applicable commands |
| Click-drag (panel edge) | Resize a docked panel — updates `IWorkspaceLayout` (`WP8.0B Workspace Contracts.md` §5, unchanged) |
| Click-drag (tab) | Reorder open tabs within the Document Area — a new, small behaviour this specification adds; does not change which views are open, only their own order |
| Click (Digital Thread "jump to" entry) | Opens the target in a new tab, never replaces the source (`ADR-0065`, unchanged) |
| Hover (icon, status badge) | Reveals its own text label/tooltip (Principle 4, Visual Language "Iconography principles") |

**Deliberately not specified: drag-to-relate** (dragging one object
onto another to create a relationship). `WP8.0A UI Architecture.md` §4
already rejected this for the architecture stage ("no real demonstrated
need... every interaction already has a non-drag equivalent"); this
specification does not reopen that decision, since no new evidence for
it has emerged.

## 4. Context-Sensitive Actions (Principle 8)

Every context menu and toolbar in the Workspace follows one rule,
stated once here rather than repeated per screen: **an action is
offered if, and only if, `CommandDescriptor.CanExecute` (closing over
`IWorkspaceContext.CurrentSelection`, unchanged from `WP8.0B Workspace
Contracts.md` §12) returns true for the current selection and its own
current state.** This is not a new mechanism — it is this
specification's own explicit naming of a rule the approved contracts
already enable, so that a future implementation Work Package does not
need to re-derive it screen by screen.

**Worked example, unchanged from the shipped `RequirementStatusTransitions`
table:** a Requirement in `Draft` status offers "Mark Reviewed" and
"Mark Obsolete" in its context menu; it does not offer "Mark Verified,"
since that transition is not permitted from `Draft` — the menu itself
is never wrong, because it is never a second, independently-maintained
copy of the transition table.

## 5. Toolbars

A toolbar surfaces the two or three *most frequent* actions for the
current screen as always-visible buttons — never the complete action
set (that is the Command Palette's own job, §1, and the context menu's
own job, §4). Which actions qualify as "most frequent" is a product
decision for the owning screen's own specification (`Screen
Catalogue.md`), not a rule this document states generically.

## 6. Docking and Panel Interactions

See `WP8.0C Workspace Behaviour Specification.md` §3-§4 for the
complete docking/layout specification — summarised here for
completeness: show/hide (`IWorkspacePanel.ShowAsync`/`HideAsync`,
unchanged), resize (§3, above), and named-layout save/restore
(`IWorkspaceState`, extended).

## Related Documents

`WP8.0C UX Specification.md`; `WP8.0C Screen Catalogue.md`;
`WP8.0C Workspace Behaviour Specification.md`; `WP8.0B Workspace
Contracts.md` §3, §12; `ADR-0070`.

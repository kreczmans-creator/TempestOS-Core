# ADR-0070: The Command Palette Is a First-Class, Global Entry Point

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0C` (Engineering
Workspace UX Specification), 2026-08-04. Extends, and narrows the scope
of, `ADR-0067`'s own Kind-keyed extensibility decision into the
interaction layer.

## Context

`Tempest.Core.Commands.ICommandRegistry`/`ICommandDispatcher` already
exist, discovered and validated in earlier Work Packages (`WP 6.x`),
and `IWorkspaceCommand` (`WP 8.0B`/`WP 8.1A`) already lets Workspace
infrastructure auto-refresh a view after a dispatched command succeeds.
None of this, by itself, decides how a *user* reaches a command. Today
(`WP 8.1A`'s shipped shell), commands are reachable only through
whatever a given view chooses to expose directly — there is no single,
universal surface a user can rely on to find and invoke *any*
discoverable action. `WP 8.0C`'s own controlling instruction names
"Command palette" as one of the 28 areas to specify, and Principle 4
("everything discoverable") requires a concrete answer for how
discoverability is actually achieved, not only asserted.

## Decision

**The Command Palette is a first-class, global entry point, reachable
from any screen via one reserved activation gesture, and it is a
*view* over `ICommandRegistry` — not a second, independently maintained
registration or discovery mechanism.** Every command registered in
`ICommandRegistry` is reachable from the palette; a command whose
`CanExecute` currently returns false for the active selection still
appears, shown disabled with its own reason, rather than being hidden
(`WP8.0C Interaction Specification.md` §1). The palette additionally
surfaces navigation results (areas, recently-viewed objects, known
identifiers) alongside command results, as one combined "do something
or go somewhere" search surface, not two separate ones.

## Consequences

**Positive:**

- Gives Principle 4 ("everything discoverable") a concrete, testable
  mechanism instead of an unenforced aspiration — any command a future
  discipline module registers is automatically palette-reachable with
  zero palette-specific registration work, the same "introduce no
  second registration mechanism" discipline `ADR-0067` already applied
  to view/tree extensibility.
- Gives keyboard-first users (Principle 7) a single, memorisable path
  to every action in the product, including actions that have no
  dedicated toolbar button or menu entry.
- Unifies command and navigation search, avoiding a second, easily
  forgotten search surface a user would otherwise need to remember
  exists separately.

**Negative:**

- Requires every future command's own `CanExecute` predicate to
  produce a genuinely useful, user-facing disabled-reason string (not
  merely `false`) for the "disabled, not hidden" rule to actually serve
  discoverability rather than confuse it — a real, disclosed authoring
  obligation on every future command, not a cost of this decision that
  disappears after implementation.
- As named in `UX Specification.md` §5's own Rendering Feasibility
  Disclosure, a floating overlay palette is a more natural fit for a
  graphical framework than a terminal — this decision commits to the
  palette's own *behaviour*, not to how a terminal-based realisation of
  it should look, leaving that specific tension unresolved pending a
  possible future `ADR-0066` revisit.

## Alternatives Considered

**Per-view search only** (each view exposes its own local search/action
list, no global surface) — considered and rejected. This is what
`WP 8.1A`'s shipped shell effectively has today, and it directly fails
Principle 4: a user has no way to discover a command that exists
outside whatever view they currently happen to be looking at.

**A traditional menu bar as the primary discovery mechanism**,
palette as a secondary accelerator only — considered and rejected as
the *primary* mechanism specifically. A menu bar is not obviously a
better fit for a terminal-based presentation (`ADR-0066`) than a
keyboard-driven palette is, and a menu bar alone would not satisfy
Principle 3 (minimise keystrokes) as directly for an expert,
keyboard-first user. A menu-equivalent (context menus, per-screen
toolbars) remains part of the specification (`Interaction Specification.md`
§4-§5) — this alternative is rejected only as the *sole* or *primary*
discovery surface, not rejected outright.

## Related Documents

`ADR-0067`; `WP8.0B Workspace Contracts.md` (`IWorkspaceCommand`);
`WP8.0C UX Specification.md` §6; `WP8.0C Interaction Specification.md`
§1; `WP8.0C Screen Catalogue.md` §12.

# WP 8.1B — Navigation & Project Explorer — Implementation

## 1. Introduction

`WP 8.1B` is `v0.8.0`'s own fifth Work Package, and its second
implementation — following `WP 8.1A` (Workspace shell) and `WP 8.0C`
(UX specification) directly. It implements the Navigation system and
Project Explorer exactly as specified across all three prior Work
Packages: Navigation Service, navigation history, breadcrumbs, an Areas
panel, the Project Explorer, Kind-keyed node providers, selection
synchronisation, context menus, filtering, search, and recent items —
against representative engineering objects only, never Requirements,
Calculations, or Documents.

## 2. Purpose

To take the Project Explorer from `WP 8.1A`'s own deliberately empty
placeholder to a genuinely navigable tree, and to prove, for the first
time against real (if fictional) content, that `ADR-0067`'s own
Kind-keyed extensibility mechanism actually lets a user drill down,
select, open, and act on an object — before any real Engineering Core
discipline is wired to it.

## 3. Background

`WP 8.1A` shipped `IProjectExplorer`/`INavigationService` fully
compiled but functionally empty — no `IProjectExplorerNodeProvider` or
`IWorkspaceViewFactory` was ever registered in production, and
`WP8.0B Workspace Contracts.md` never anticipated navigation history,
breadcrumbs, filtering, or recent items at all (`WP8.0C UX
Specification.md` §0's own disclosed sequencing gap). `WP 8.0C` then
specified what all of this should feel like without building any of it.
`WP 8.1B` is where that gap starts closing — deliberately still without
any real engineering object, so the Navigation/Explorer behaviour
itself is proven independently of any one discipline's own data model.

## 4. The Problem

1. **Does the Project Explorer's own tree, breadcrumb, and filter
   behaviour actually work against a real, multi-level, Kind-keyed
   provider**, or did the architecture/contract stages miss an
   interaction only real drill-down surfaces?
2. **Where do navigation history and recent items — named in `WP8.0C
   Navigation Maps.md` but never in the twelve `WP 8.0B` contracts —
   actually live**, without reopening any of those twelve interfaces?
3. **How does a future Engineering Discipline Module actually call
   `IWorkspaceManager.RegisterView`/`RegisterExplorerArea`**, given
   `ADR-0067`'s own worked example assumed a module could reach
   `WorkspaceManager` directly?
4. **What does "context menu" mean for a terminal shell with no mouse
   and no right-click**, given `WP8.0C Interaction Specification.md`'s
   own richer model presupposes pointing devices and reserved key
   combinations neither of which a terminal REPL has?

## 5. The Design

`NavigationService`/`ProjectExplorer` (`WP 8.1A`) were extended, not
replaced: `History`/`RecentItems`/`GoBackAsync`/`GoForwardAsync` on
`NavigationService`, `CurrentPath`/`EnterAsync`/`ExitAsync`/`FilterAsync`
on `ProjectExplorer` — all declared directly on the concrete, internal
classes, never on the twelve public interfaces, reached by
`WorkspaceShell` through two new internal accessors on `Workspace`
(`NavigationServiceConcrete`/`ProjectExplorerConcrete`), mirroring
`WorkspaceManager.StatusBar`'s own `WP 8.1A` precedent exactly. A fixed,
in-memory Category → Group → Object tree
(`Tempest.App.Workspace.Samples.SampleExplorerContent` — Assemblies →
Primary/Secondary Structure → Longeron/Frame/Bracket) is presented
through the first real `IProjectExplorerNodeProvider`/
`IWorkspaceViewFactory` pair, registered by `Program.cs` itself
(`ADR-0071`) against a real, discovered `NavigationItem`
(`WorkspaceExplorerSampleModule`). `WorkspaceShell`'s own input loop
gained a small word-command vocabulary (`open`, `up`, `close`, `filter`,
`back`, `forward`, `menu`) — a disclosed, terminal-appropriate
realisation of `WP8.0C Interaction Specification.md`'s own richer model,
not a literal binding of it. See `WP8.1B Implementation Report.md` for
the complete file-by-file account.

## 6. Alternatives Considered

**Extending the twelve public `WP 8.0B` interfaces directly** (adding
`History`/`CurrentPath` etc. to `INavigationService`/`IProjectExplorer`
themselves) — considered and rejected. Neither capability was ever
reviewed at the contract stage; adding them as same-assembly-only
extensions to the concrete classes gives `WorkspaceShell` everything it
needs without reopening interfaces a Contract Review already froze.

**Giving a discovered module a direct reference to `IWorkspaceManager`**
— considered and rejected; see `ADR-0071`. Would require registering
`WorkspaceManager` into the Host's own DI container, directly
contradicting `ADR-0062`.

**A literal keyboard-shortcut/mouse-gesture implementation** — considered
and rejected as premature; `WP8.0C UX Specification.md` §5 already
defers the literal bindings to a future rendering-technology choice
neither `WP 8.1A` nor `WP 8.1B` was scoped to make.

## 7. Why This Solution Was Chosen

It proves the Project Explorer's own Kind-keyed extensibility mechanism
end to end, in real running code, with zero signature change to any of
the twelve `WP 8.0B` contracts — the two disclosed additions
(history/recent items; breadcrumbs/filtering) are both same-assembly,
non-breaking extensions, and the one genuine correction (`ADR-0071`)
fixes a worked example, not the underlying Kind-keyed decision itself.

## 8. Architectural Principles

- **Composition Over Inheritance** — `NavigationService`'s own
  back/forward replay reuses `OpenAsync`/`SwitchAreaAsync` themselves
  (via a suppression flag), rather than a second, parallel replay code
  path.
- **Single Responsibility Principle** — history/recent-items tracking
  lives entirely in `NavigationService`; breadcrumb/path tracking lives
  entirely in `ProjectExplorer`; neither reaches into the other's own
  state.
- **Fail Fast** — `ProjectExplorer.FilterAsync` rejects a null/empty/
  whitespace search term immediately (`ArgumentException`), matching
  every other Workspace contract's own established validation
  discipline.

## 9. Files Added

7 new production files (`NavigationHistoryEntry.cs`,
`RecentNavigationItem.cs`, 4 under `Workspace/Samples/`,
`WorkspaceExplorerSampleModule.cs`), 5 modified; 2 new test files under
`tests/Tempest.Core.Tests/Workspace/Samples/`, 4 modified. Full list:
`WP8.1B Implementation Report.md`.

## 10. Trade-offs

- Navigation history and recent items are Workspace-global, not
  per-tab/per-project as `WP8.0C Navigation Maps.md` §4 specifies — the
  terminal shell has no independent per-tab focus model to hang
  per-tab history off.
- The Project Explorer's own interaction vocabulary is a small set of
  terminal words, not the literal keyboard/mouse bindings `WP8.0C
  Interaction Specification.md` describes — deferred, unchanged, to a
  future rendering-technology choice (`ADR-0066`).
- Filtering serves only the Project Explorer; the Command Palette
  (a separate, out-of-scope search surface per `WP8.0C Screen
  Catalogue.md` §14) remains unimplemented.

## 11. Common Mistakes

The mistake most worth naming: trusting an Accepted ADR's own worked
example without verifying it against real, built code. `ADR-0067`
described a module calling `IWorkspaceManager.RegisterView` directly —
plausible-sounding, never actually built. Building the first real
registration is what surfaced that a discovered module has no path to
the one `WorkspaceManager` instance wrapping its own Host from the
outside (`ADR-0062`'s own boundary). `ADR-0071` corrects this openly,
in a new ADR, rather than working around it silently inside
`Program.cs` with no record of why.

## 12. Future Evolution

- **The first real, production `IWorkspaceViewFactory`/
  `IProjectExplorerNodeProvider` pair for an actual Engineering Core
  `Kind`**, most naturally for Requirements — this Work Package proved
  the mechanism only against fictional sample content.
- **The Engineering Cockpit and Command Palette** (`ADR-0069`/
  `ADR-0070`) — the next Workspace-experience gap, per `WP8.0C UX
  Specification.md`'s own Summary of Companion Deliverables.
- **Per-tab navigation history**, if a real multi-tab focus model is
  ever built — narrower than today's global history, not designed here.

## 13. Key Takeaways

1. A same-assembly-only extension to a concrete class is a legitimate
   way to add a real, needed capability the frozen public contracts
   never named — the discipline is keeping it internal until a genuine
   cross-boundary need actually forces a public contract change.
2. An ADR's own worked example is a claim, not a guarantee — the first
   Work Package that actually needs it is where that claim gets tested,
   and correcting it openly (a new ADR) is the right response when it
   does not hold, not a silent workaround.
3. A terminal shell can honestly realise a richer UX specification's
   own behavioural intent (drill-down, filter, history, context-
   sensitive actions) through a small word-command vocabulary, without
   needing to wait for a literal keyboard/mouse binding decision that
   depends on a rendering technology not yet chosen.

## Architectural Debt Assessment

**None.** Every scope limitation (global, not per-tab, history; no
Command Palette; no literal keyboard bindings) is either an
already-disclosed deferral from `WP 8.0C`'s own Rendering Feasibility
Disclosure, or a direct, expected consequence of this Work Package's
own explicit "no Requirements, no Calculations, no Documents" scope —
not a regression from a working state. See `WP8.1B Implementation
Report.md`'s own Technical Debt Assessment.

## Observations

This is the second implementation Work Package of `v0.8.0` — validated
by the same discipline every implementation Work Package before it has
used (clean Debug/Release builds, 1552/1552 tests, both configurations,
clean rebuild, stable across three runs, up from a 1497 baseline). It
is also the first Work Package in this project's history to correct an
Accepted ADR's own worked example with a new ADR, rather than either
silently working around it or leaving it uncorrected — a genuine test
of the project's own "disclose, don't hide" discipline applied to prior
governance output, not only to new implementation findings.

## Related Documents

`docs/releases/v0.8.0/WP8.1B Implementation Report.md`; `ADR-0071`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`;
`docs/releases/v0.8.0/WP8.0B Workspace Contracts.md`;
`docs/releases/v0.8.0/WP8.0C UX Specification.md`;
`docs/releases/v0.8.0/WP8.1A Implementation Report.md`.

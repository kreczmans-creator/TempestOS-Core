# WP 8.1B — Navigation & Project Explorer — Implementation Report

## Status

Complete. Implements the Workspace's own Navigation system and Project
Explorer exactly as specified in `WP 8.0A`, `WP 8.0B`, and `WP 8.0C` —
Navigation Service, navigation history, breadcrumbs, an Areas panel, the
Project Explorer, Kind-keyed node providers, selection synchronisation,
context menus, filtering, search, and recent items. The tree is
populated with representative engineering objects only, via a real
living reference provider (`Tempest.App.Workspace.Samples`) — no
Requirements logic, no Calculations, no Documents. No persistence
beyond the navigation/session state `WP 8.1A` already implemented.

## What Was Implemented

| Named in Scope | Implementation |
|---|---|
| Navigation Service | `NavigationService` (`WP 8.1A`), extended |
| Navigation history | `NavigationService.History`, `GoBackAsync`/`GoForwardAsync` — a genuine, disclosed addition (§ below) |
| Breadcrumbs | `ProjectExplorer.CurrentPath`, `EnterAsync`/`ExitAsync` — a genuine, disclosed addition |
| Areas panel | `WorkspaceShell`'s own "Areas" region (`WP 8.1A`), extended with a "current area" marker |
| Project Explorer | `ProjectExplorer` (`WP 8.1A`), extended |
| Node providers | `IProjectExplorerNodeProvider` (`WP 8.0B`, unchanged contract) — first real, non-test-double implementation: `SampleProjectExplorerNodeProvider` |
| Selection synchronisation | `ISelectionService`/`PropertyInspector` (`WP 8.1A`, unchanged) — now driven end to end by the Shell's own `open` command |
| Context menus | `WorkspaceShell`'s own `menu <N>` command — lists context-sensitive actions (Enter / Open / Focus / Close) for the selected node, computed against live Navigation/OpenViews state |
| Filtering / Search | `ProjectExplorer.FilterAsync` — one mechanism serves both, per `WP8.0C Screen Catalogue.md` §14's own distinction (Command Palette search is a separate, out-of-scope surface) |
| Recent items | `NavigationService.RecentItems` — a genuine, disclosed addition |

Two new production namespaces:

- `src/Tempest.App/Workspace/` (2 new files): `NavigationHistoryEntry.cs`,
  `RecentNavigationItem.cs`.
- `src/Tempest.App/Workspace/Samples/` (4 new files): `SampleExplorerContent.cs`
  (a fixed, in-memory Category → Group → Object tree — Assemblies →
  Primary/Secondary Structure → Longeron/Frame/Bracket), `SampleProjectExplorerNodeProvider.cs`,
  `SampleWorkspaceView.cs`, `SampleWorkspaceViewFactory.cs`.
- `src/Samples/Tempest.Samples/WorkspaceExplorerSampleModule.cs` (1 new
  file): a real, discovered `IModule` contributing only the
  `NavigationItem` ("Sample Objects") this content is presented under —
  see `ADR-0071` for why it registers nothing beyond that.

Five modified production files: `NavigationService.cs`, `ProjectExplorer.cs`,
`Workspace.cs` (new internal `ProjectExplorerConcrete`/
`NavigationServiceConcrete` accessors, mirroring `WorkspaceManager.StatusBar`'s
own precedent), `WorkspaceShell.cs` (rewritten input/render loop), and
`Program.cs` (registers the sample content against the real
`WorkspaceManager`, per `ADR-0071`).

## Interaction Model

A bare number still switches areas, unchanged from `WP 8.1A`. Project
Explorer interaction is reached through a small word-command vocabulary:
`open <N>` (enter a Category/Group, or select-and-open an Object),
`up` (exit one level), `close <N>` (close an open document), `filter
[text]` (filter/clear), `back`/`forward` (history), `menu <N>` (list
context-sensitive actions). This is a disclosed, terminal-appropriate
realisation of `WP8.0C Interaction Specification.md`'s own richer
keyboard-shortcut/mouse-gesture model — not a literal binding of it. The
literal bindings were always deferred to a future rendering-technology
choice (`WP8.0C UX Specification.md` §5); a terminal REPL has no mouse
and no reserved-key concept, so a small, discoverable word vocabulary is
this Work Package's own honest, minimal realisation of the same
underlying behaviours.

## Disclosed Implementation-Phase Findings

1. **Navigation history and recent items are Workspace-global, not
   per-tab or per-project.** `WP8.0C Navigation Maps.md` §4 specifies
   per-tab history and a separate global "recently viewed" surface. The
   terminal shell has no independent per-tab focus model to hang
   per-tab history off (`WP8.0C Workspace Behaviour Specification.md`
   §5's own disclosed multi-window tension is the same underlying
   limit) — one global back/forward stack and one global recent-items
   list serve the same underlying need honestly, without presupposing a
   capability this Work Package was never scoped to build. Not
   persisted (`WorkspaceStateDto` unchanged) — "no persistence beyond
   navigation/session state already implemented" is this Work Package's
   own explicit constraint.
2. **`ADR-0067`'s own worked registration example does not hold** — see
   `ADR-0071`. A discovered `IModule` cannot reach `IWorkspaceManager`
   (it is a composition-root component, `ADR-0062`, never DI-registered);
   Workspace-specific registration calls belong in `Program.cs`, not
   inside a module's own `InitialiseAsync`. Corrected in a new ADR
   rather than silently, since `ADR-0067` is an Accepted, authoritative
   record a future reader would otherwise be misled by.
3. **Filtering and Search are the same mechanism for the Project
   Explorer specifically.** `WP8.0C Screen Catalogue.md` §14 already
   drew this distinction at the specification stage (Project Explorer
   filter vs. Command Palette search, two separate surfaces); this Work
   Package implements only the former, since the Command Palette itself
   is not named in this Work Package's own scope.

Neither finding required revisiting `ADR-0062`–`ADR-0066`/`ADR-0068`–
`ADR-0070` — `ADR-0071` corrects `ADR-0067`'s own stated worked example
specifically, not its core Kind-keyed-registration decision.

## New ADR

`ADR-0071` — Workspace extensibility registrations (`RegisterView`/
`RegisterExplorerArea`) are made by the composition root (`Program.cs`),
never by a discovered module, correcting `ADR-0067`'s own worked
example against the real Host/Workspace boundary `ADR-0062` already
established.

## Testing

55 new tests (1497 → 1552), zero regressions, confirmed across two
clean-rebuild Debug runs and one clean-rebuild Release run. Every test
uses real, production collaborators — a real `TempestHost` (scoped via
the existing `internal TempestHostBuilder(IEnumerable<Type>?)`
constructor), the real `INavigationProvider`/`IEventBus`, and an
isolated `TempDirectory` persistence root per test. `NavigationService`/
`ProjectExplorer` are reached directly (not only through their public
interfaces) via `Tempest.App`'s own existing `InternalsVisibleTo`
grant to `Tempest.Core.Tests` (`WP 8.1A`), the same mechanism
`WorkspaceStateTests.cs` already established, needed here to test
`History`/`RecentItems`/`GoBackAsync`/`GoForwardAsync`/`CurrentPath`/
`EnterAsync`/`ExitAsync`/`FilterAsync` — none of which are part of the
twelve `WP8.0B Workspace Contracts.md` interfaces.

Coverage highlights: history recording and truncation-on-new-navigation
(browser-style back/forward semantics); back/forward replay never
recording a duplicate history or recent-item entry; recent-items
de-duplication and most-recent-first ordering; breadcrumb path
construction, reset-on-area-change; recursive, case-insensitive
filter/search across a multi-level tree; the full sample content
(`SampleProjectExplorerNodeProvider`/`SampleWorkspaceViewFactory`)
against its own fixed tree, proving Kind-keyed registration end to end
with zero Engineering Core dependency; and `WorkspaceShell`'s own full
drill-down/open/close/filter/back/forward/context-menu interaction loop,
mirroring `WorkspaceShellTests.cs`'s own established real-collaborator
discipline.

## Platform Integration

Zero new Platform Service, zero new persistence mechanism. The sample
content's own `NavigationItem` is registered exactly as every other
sample module's own area already is (`INavigationProvider`, unchanged);
no existing Platform Service or Engineering Core contract was modified.

## Governance Note: Interface/DI/Module Registers

`Interface Register.md`/`Dependency Injection Register.md` remain
correctly unchanged for the same reason `WP 8.1A` already established —
every new type lives in `Tempest.App`, is never DI-registered
(composition-root or same-assembly-internal, per `ADR-0062`), and falls
outside both registers' own explicit `Tempest.Core`/`TempestHost.cs`
scope. `Module Register.md` gains one row:
`WorkspaceExplorerSampleModule` (`tempest.samples.workspace-explorer`),
a real, discovered production sample module, registering one
`NavigationItem` and nothing else.

## Technical Debt Assessment

**No new Technical Debt item is raised by this Work Package.** The two
disclosed simplifications (global, not per-tab, history/recent items;
filtering scoped to Project Explorer only, not the Command Palette) are
both direct, expected consequences of this Work Package's own explicit
scope (no Command Palette named in its own Deliverables list) and the
terminal shell's own already-disclosed rendering ceiling
(`WP8.0C Workspace Behaviour Specification.md` §5-§6), not corners cut
under implementation pressure.

## Repository Metrics

- Production files: 7 new (`NavigationHistoryEntry.cs`,
  `RecentNavigationItem.cs`, 4 under `Workspace/Samples/`,
  `WorkspaceExplorerSampleModule.cs`), 5 modified.
- Tests: 55 new (1497 → 1552), 0 failures, both configurations, clean
  rebuild, stable across three consecutive runs (two Debug, one
  Release).
- ADRs: 1 new (`ADR-0071`, 70 → 71).
- Sample modules (discovered, production): 20 → 21
  (`WorkspaceExplorerSampleModule`) — `ClockModuleDiscoveryTests.cs`
  updated accordingly.
- Zero new Technical Debt items, zero new Future Capability Register
  entries.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0B Workspace
Contracts.md`; `WP8.0C UX Specification.md` and its eight companion
deliverables (`Navigation Maps.md`, `Screen Catalogue.md`, `Interaction
Specification.md` especially); `WP8.1A Implementation Report.md`;
`ADR-0062`, `ADR-0067`, `ADR-0071`; `docs/academy/02 Runtime
Architecture/17-engineering-workspace.md`; `docs/academy/03 Work
Packages/WP8.1B-navigation-and-project-explorer-implementation.md`.

# WP 8.1A — Workspace Shell — Implementation Report

## Status

Complete. Implements the Workspace infrastructure `WP 8.0A`/`WP 8.0B`
approved — the shell only, exactly as this Work Package's own
controlling instruction scopes it: `IWorkspace`, `IWorkspaceManager`, a
Navigation host, Workspace layout, a panel manager, a view manager,
session restore, an empty Project Explorer, an empty Properties panel,
an empty Content area, and a Status Bar. No engineering functionality,
no Requirements, no Calculations, no Documents.

## What Was Implemented

All twelve `WP8.0B Workspace Contracts.md` interfaces, compiled exactly
as specified, plus their supporting types, in a new `Tempest.App.Workspace`
namespace (`src/Tempest.App/Workspace/`, 26 new files):

| Public | Concrete (internal unless noted) |
|---|---|
| `IWorkspace` | `Workspace` |
| `IWorkspaceManager` | `WorkspaceManager` (**public** — `Tempest.App`'s own direct construction target, mirroring `TempestShell`) |
| `IWorkspaceView` | — (no concrete production implementation; no engineering `Kind` exists yet to construct one for) |
| `IWorkspacePanel` | (base only; `ProjectExplorer`/`PropertyInspector` implement it directly) |
| `IWorkspaceLayout` | `WorkspaceLayout` |
| `INavigationService` | `NavigationService` |
| `ISelectionService` | `SelectionService` |
| `IWorkspaceContext` | `WorkspaceContext` |
| `IWorkspaceState` | `WorkspaceState` |
| `IProjectExplorer` | `ProjectExplorer` |
| `IPropertyInspector` | `PropertyInspector` |
| `IWorkspaceCommand` | — (no concrete command; no engineering functionality to act on) |
| `IWorkspaceViewFactory` | (extensibility contract only — no concrete production factory) |
| `IProjectExplorerNodeProvider` | (extensibility contract only — no concrete production provider) |

Plus `WorkspaceStatusBar` (the Status Bar's own text, internal — no
public contract among the twelve named one) and `WorkspaceShell` (the
concrete terminal presentation layer, **public**, built on top of the
otherwise rendering-agnostic `IWorkspaceManager`/`IWorkspace` contracts).

## Named Scope Items, Mapped to What Was Built

| Named in Scope | Implementation |
|---|---|
| `IWorkspace` | `Workspace` — aggregate root, composed once per `StartAsync` |
| `IWorkspaceManager` | `WorkspaceManager` — composition root, lifecycle, extensibility registries |
| Navigation host | `NavigationService`, wrapping the existing `INavigationProvider` |
| Workspace layout | `WorkspaceLayout` |
| Panel manager | `WorkspaceManager`'s own composition of `ProjectExplorer`/`PropertyInspector` plus `WorkspaceLayout`'s own placement table — no separate public type, since none of the twelve approved contracts named one |
| View manager | `NavigationService`'s own `OpenViews`/`ActiveView` tracking — likewise no separate public type |
| Session restore | `WorkspaceState`, backed by the existing `ISettingsProvider` |
| Empty Project Explorer | `ProjectExplorer` — delegates to zero registered `IProjectExplorerNodeProvider`s in this Work Package's own scope |
| Empty Properties panel | `PropertyInspector` — shows only Identity facets (Id, Kind) derived from the selection tuple itself, no Engineering Core service consulted |
| Empty Content area | `NavigationService.OpenViews`, empty until a future Work Package registers a real `IWorkspaceViewFactory` |
| Status bar | `WorkspaceStatusBar` |

"Panel manager" and "View manager" are real, tested behaviours (see
Testing, below), not merely folded-in duty — they are simply not
separate public types, since `WP8.0B Workspace Contracts.md` named
twelve contracts, not fourteen, and inventing two more without a
genuine need would contradict this project's own "do not build ahead
of a demonstrated need" discipline.

## Disclosed Implementation-Phase Findings

1. **`ISettingsProvider` is `string`-only, not generic.**
   `WP8.0B Workspace Contracts.md` proposed
   `GetValueAsync<T>`/`SetValueAsync<T>` for `IWorkspaceState`; the real,
   shipped `ISettingsProvider` (`WP 6.4`) operates on `string` only.
   `WorkspaceState` serializes its own `WorkspaceStateDto` to JSON and
   stores that string directly — the identical pattern
   `Tempest.Core.Requirements.RequirementDto` already establishes for
   `IDocumentRevision.Content`, applied here to Settings instead. A
   minor, disclosed deviation from the contract document's own proposed
   signature, not from its own governing decision (`ADR-0064`).
2. **`ITempestHost` is explicitly single-use, not restart-tolerant.**
   `WP8.0B Lifecycle Definitions.md` §1 described `IWorkspaceManager` as
   restart-tolerant, by loose analogy to `ITempestHost`. The real,
   shipped `ITempestHost` contract states `RunAsync` "may be called at
   most once per instance." `WorkspaceManager.StartAsync` now throws
   `InvalidOperationException` on a second call against the same
   instance — a genuine, disclosed correction, confirmed directly
   against `ITempestHost`'s own XML documentation, not a defect.
3. **Terminal-based presentation realised as a hand-rolled console
   renderer** (`WorkspaceShell`), not a third-party TUI library —
   exactly one of the three options `ADR-0066` itself named, chosen
   because this Work Package's own scope is shell infrastructure, not a
   rendering-technology evaluation; introduces zero new dependency.

Neither finding required revisiting any approved architectural boundary
decision (`ADR-0062`–`ADR-0067`) — both are implementation-level
corrections to contract-stage assumptions, exactly the class of
disclosure `FOUNDATION.md`'s own "document every non-obvious decision"
principle exists for.

## New ADR

`ADR-0068` — `Tempest.App`'s own entry point now constructs and runs
the Workspace by default, satisfying this Work Package's own explicit
Definition of Done ("TempestOS launches directly into the Workspace").
`TempestShell` remains in the repository, fully intact, fully tested,
simply no longer the default launch target (`ADR-0062`'s own
"additive, not replacing" position, now made concrete).

## Testing

91 new tests (1406 → 1497), zero regressions, confirmed across four
full-suite runs (two Debug, two Release, both from a clean rebuild).
Every test uses real, production collaborators — a real `TempestHost`
(scoped via the existing `internal TempestHostBuilder(IEnumerable<Type>?)`
constructor, mirroring `TempestShellTests.cs`/
`RequirementsHostRegistrationTests.cs`'s own precedent exactly), the
real `INavigationProvider`/`IEventBus`/`ISettingsProvider`, and an
isolated `TempDirectory` persistence root per test. This project does
not use a mocking framework — the three test-local fakes
(`TestWorkspaceView`, `TestWorkspaceViewFactory`,
`TestProjectExplorerNodeProvider`) are real, minimal implementations of
the real public contracts, mirroring `PlaceholderPage`'s own role in
Shell tests.

Coverage highlights: `WorkspaceManager` lifecycle (start/shutdown,
double-start guard, `Current`); `RegisterView`/`RegisterExplorerArea`
extensibility (`ADR-0067`, including before- and after-`StartAsync`
registration); `NavigationService` (`Areas` delegation, `SwitchAreaAsync`,
`OpenAsync`'s own focus-existing-tab behaviour, `JumpToAsync`'s own
always-new-tab behaviour, `CloseAsync`'s own active-view promotion);
`SelectionService` (real `WorkspaceSelectionChangedEvent` publication
through the real `IEventBus`); `PropertyInspector`'s own automatic
reaction to selection changes; `ProjectExplorer`'s own zero-leak
per-area provider delegation; `WorkspaceState`'s own save/load round
trip, including a genuine cross-restart test (two separate `ITempestHost`
instances over the same persistence root, proving real, on-disk session
restore, not merely in-memory continuity); and `WorkspaceShell`'s own
full rendering and input-loop behaviour, mirroring `TempestShellTests.cs`
end to end.

## Platform Integration

Zero new Platform Service. `WorkspaceManager` consumes exactly four
existing Platform Services, all through the same `ITempestHost.Services`
resolution path `TempestShell` already established (`ADR-0034`):
`INavigationProvider`, `IEventBus`, `ISettingsProvider`. No existing
Platform Service or Engineering Core contract was modified.

## Governance Note: Interface/DI/Module Registers Unchanged

`Interface Register.md` is explicitly scoped to `src/Tempest.Core/`;
`Dependency Injection Register.md` to `TempestHost.cs`'s own Phase 6
registration block; `Module Register.md` to discovered production
modules. None of the twelve new public interfaces, `WorkspaceManager`,
or `WorkspaceShell` fall within any of these three scopes — they live
in `Tempest.App`, are never DI-registered (composition-root components,
per `ADR-0062`), and are not discovered modules. This mirrors
`TempestShell` itself, which has never appeared in any of the three
registers either. No update to any of the three was required or made.

## Technical Debt Assessment

**No new Technical Debt item is raised by this Work Package.** Every
scope limitation (`IPropertyInspector` showing only Identity facets, no
multi-window/floating-panel support, `IWorkspaceView`/`IWorkspaceCommand`
having no concrete production implementation) is either an already-
disclosed, deliberately deferred capability from `WP8.0A Workspace
Architecture Document.md`'s own "Deliberately Out of Scope" section, or
a direct, expected consequence of this Work Package's own explicit "no
engineering functionality" constraint — not a regression from a working
state, and not a corner cut under real implementation pressure.

## Repository Metrics

- Production files: 27 new (26 under `src/Tempest.App/Workspace/`, plus
  `src/Tempest.App/AssemblyInfo.cs`), 1 modified (`Program.cs`).
- Tests: 91 new (1406 → 1497), 0 failures, both configurations, clean
  rebuild, stable across four consecutive runs.
- ADRs: 1 new (`ADR-0068`, 67 → 68).
- Zero new Technical Debt items, zero new Future Capability Register
  entries (none genuinely new beyond what `WP8.0B`'s own retrospective
  already recommended).

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0B Workspace
Contracts.md` and its three companion deliverables; `ADR-0062`–`ADR-0068`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/academy/03 Work Packages/WP8.1A-workspace-shell-implementation.md`.

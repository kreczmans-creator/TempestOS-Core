# WP 8.1A — Workspace Shell — Implementation

## 1. Introduction

`WP 8.1A` is `v0.8.0`'s own third Work Package, and its first
implementation — following `WP 8.0A` (architecture) and `WP 8.0B`
(contracts) directly, mirroring the Requirements Engine's own
`WP 7.2B` → `WP 7.2C` → `WP 7.3A` sequence exactly. It implements the
Workspace *shell* only: the composition root, navigation host, layout,
panel and view tracking, session restore, and three deliberately empty
panels — no engineering functionality, no Requirements, no
Calculations, no Documents.

## 2. Purpose

To give TempestOS its first real, running, user-facing composition root
beyond the console `TempestShell` — proving every contract
`WP 8.0B` froze compiles, wires together, and behaves exactly as
specified, before any engineering-domain content is layered on top of
it.

## 3. Background

`WP 8.0B Workspace Contracts.md` froze twelve interfaces in proposed,
uncompiled C#. Nothing in the repository had yet proven those
signatures actually fit together — that `IWorkspaceManager` could
genuinely assemble an `IWorkspace` from real Platform Services, that
`INavigationService`'s own focus-existing-tab logic was expressible
against `IWorkspaceView`'s own shape, or that `ADR-0067`'s own
Kind-keyed registration idea actually worked as a real extension point.
`WP 8.1A` is where that proof happens — deliberately without any real
engineering object to complicate the proof, exactly as this Work
Package's own controlling instruction scopes it.

## 4. The Problem

1. **Do the twelve frozen contracts actually compile and compose**, or
   did the contract-review stage miss an interaction only real code
   surfaces?
2. **How does `Tempest.App` choose between two now-real composition
   roots** (`TempestShell`, the Workspace) — left genuinely undecided
   through `WP 8.0B`?
3. **Where does "Panel manager" and "View manager" — named in this Work
   Package's own scope but not among the twelve approved public
   contracts — actually live?**
4. **How is "session restore" proven**, given `ISettingsProvider` turned
   out to be `string`-only, not the generic contract `WP 8.0B` proposed?

## 5. The Design

Twelve interfaces compiled exactly as `WP8.0B Workspace Contracts.md`
specified, in a new `Tempest.App.Workspace` namespace — `WorkspaceManager`
(public, `Tempest.App`'s own direct construction target) assembles an
`IWorkspace` from four existing Platform Services
(`INavigationProvider`, `IEventBus`, `ISettingsProvider`), resolved
through the identical `ITempestHost.Services` path `TempestShell`
already established. "Panel manager" and "View manager" are real,
tested behaviour living inside `WorkspaceManager`'s own composition and
`NavigationService`'s own `OpenViews`/`ActiveView` tracking respectively
— no new public type, since neither was among the twelve approved.
`WorkspaceState` persists via `ISettingsProvider`, serializing its own
DTO to JSON to work within that service's own real, `string`-only
contract. `Program.cs` now constructs `WorkspaceManager`/`WorkspaceShell`
instead of `TempestShell` (`ADR-0068`) — `TempestShell` itself
untouched. See `WP8.1A Implementation Report.md` for the complete
file-by-file account.

## 6. Alternatives Considered

**Adding `IWorkspacePanelManager`/`IWorkspaceViewManager` as two further
public contracts** — considered and rejected. Neither was among the
twelve `WP8.0B Workspace Contracts.md` approved; inventing two more
mid-implementation, without a Contract Review having validated their
own shape, would repeat exactly the mistake this project's own
architecture-then-contracts discipline exists to prevent.

**A third-party TUI library** (a `Terminal.Gui`-shaped dependency) for
`WorkspaceShell`'s own rendering — considered and rejected; see
`ADR-0066`'s own reasoning, unchanged: no real, demonstrated need
justifies this platform's first-ever GUI/TUI dependency for a shell
whose own scope is proving contracts compose, not evaluating rendering
technology.

**A command-line launch-mode switch** between `TempestShell` and the
Workspace — considered and rejected; see `ADR-0068`'s own reasoning.

## 7. Why This Solution Was Chosen

It proves every one of `WP 8.0B`'s own twelve contracts in real,
running, tested code with zero signature change to any of them — the
two disclosed findings (`ISettingsProvider`'s own `string`-only shape,
`ITempestHost`'s own single-use constraint) were both implementation-
level corrections to contract-stage assumptions, never a reason to
revisit an approved interface.

## 8. Architectural Principles

- **Composition Over Inheritance** — `Workspace` composes six sub-
  services; no inheritance hierarchy was introduced anywhere in this
  Work Package.
- **Single Responsibility Principle** — `NavigationService` handles
  navigation and view tracking; `SelectionService` handles selection;
  neither reaches into the other's own concern.
- **Fail Fast** — `RegisterView`/`RegisterExplorerArea` reject a
  duplicate `Kind` immediately (`DuplicateWorkspaceRegistrationException`);
  `OpenAsync`/`JumpToAsync` reject an unregistered `Kind` immediately
  (`WorkspaceViewFactoryNotFoundException`) — never a silent no-op.

## 9. Files Added

27 new production files (26 under `src/Tempest.App/Workspace/`, plus
`src/Tempest.App/AssemblyInfo.cs`), 1 modified (`Program.cs`); 8 new
test files under `tests/Tempest.Core.Tests/Workspace/`. Full list:
`WP8.1A Implementation Report.md`.

## 10. Trade-offs

- `TempestShell` is no longer directly reachable by running
  `Tempest.App` — a future contributor wanting it must construct it
  manually (`ADR-0068`).
- `IPropertyInspector` shows only Identity facets (Id, Kind); Revision/
  Provenance/Relationship/DisciplineSpecific facets have no source yet
  — expected, not a defect, given "no engineering functionality."
- `IWorkspaceView`/`IWorkspaceCommand` have no concrete production
  implementation — proven only through test-local fakes, since no real
  engineering `Kind` exists yet to build one against.

## 11. Common Mistakes

The mistake most worth naming: assuming `IWorkspaceManager` could be
restarted freely, the way `WP8.0B Lifecycle Definitions.md` originally
described it. `ITempestHost` is explicitly single-use; a second
`StartAsync` call against the same `WorkspaceManager` now throws
`InvalidOperationException` rather than silently misbehaving against a
host that can never actually run twice.

## 12. Future Evolution

- **The first real `IWorkspaceViewFactory`/`IProjectExplorerNodeProvider`
  pair**, most naturally for Requirements (the only Implemented Systems
  Engineering Foundation capability) — the next implementation Work
  Package's own natural first proof of `ADR-0067` beyond test-local
  fakes.
- **A specific TUI library**, if `WorkspaceShell`'s own hand-rolled
  renderer ever proves insufficient — narrower than `ADR-0066`, needing
  no further ADR.
- **Multi-window/floating-panel support** — still deliberately deferred,
  unchanged from `WP 8.0A`.

## 13. Key Takeaways

1. Freezing contracts before implementation catches interface-shape
   mistakes; it does not catch every real-world constraint (`ISettingsProvider`'s
   own `string`-only contract, `ITempestHost`'s own single-use rule) —
   both classes of finding are expected, disclosed, and neither
   invalidates the contract-freezing discipline itself.
2. A named scope item ("Panel manager," "View manager") does not always
   need its own public interface — the test is whether a Contract
   Review already validated its own shape, not whether the Work Package
   instruction happened to name it.
3. A default launch-target decision is a genuine, disclosed
   architectural choice, not an implementation detail to leave
   implicit — `ADR-0068` exists because `Program.cs`'s own behaviour is
   externally observable the moment someone runs `dotnet run`.

## Architectural Debt Assessment

**None.** Every scope limitation is either already disclosed by
`WP 8.0A`'s own "Deliberately Out of Scope" section or a direct,
expected consequence of "no engineering functionality" — not a
regression from a working state, and not a corner cut under
implementation pressure. See `WP8.1A Implementation Report.md`'s own
Technical Debt Assessment.

## Observations

This is the first implementation Work Package of `v0.8.0` — validated
by the same discipline every implementation Work Package before it
has used (clean Debug/Release builds, 1497/1497 tests, both
configurations, clean rebuild, stable across four runs, up from a 1406
baseline). Every one of `WP 8.0B`'s own twelve contracts compiled and
composed with zero signature change — the strongest evidence yet, at
the presentation layer specifically, that this project's own
architecture-then-contracts-then-implementation discipline produces
implementable designs.

## Related Documents

`docs/releases/v0.8.0/WP8.1A Implementation Report.md`; `ADR-0068`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`;
`docs/releases/v0.8.0/WP8.0B Workspace Contracts.md`.

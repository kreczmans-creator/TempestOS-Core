# Architectural Dependency Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Architectural Dependency Register |
| **Purpose** | Records the dependency graph at two levels: (1) project-reference dependencies between compiled assemblies, and (2) the four-layer platform model (ADR-0023) every capability is classified against. |
| **Scope** | Every `.csproj` under `src/`/`tests/`, and the layer classification of every Platform Service/API in `Platform Services Register.md`/`Interface Register.md`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | The `.csproj` files themselves (project references); `docs/adr/ADR-0023-platform-layering-dependencies-flow-downward.md`; `docs/academy/02 Runtime Architecture/06-platform-layering.md`. |
| **Review Frequency** | Updated whenever a project reference changes, or a new capability is classified against the four-layer model. |
| **Last Reviewed** | 2026-08-30 (`TD-75` phase 2, Complete Samples Boundary) — **the Project Reference Graph re-derived in full and no longer partial**: all 8 projects present and each row verified against its own `.csproj`. Two rows changed on substance rather than presentation — `Tempest.Validation` loses its `Tempest.Samples` reference, and `Tempest.Samples` is now referenced by test projects only — which together make the sample harness deletable, verified by removing `src/Samples` and building the rest of the solution clean. Both executables are labelled with their actual `OutputType` (`Tempest.App` is the console harness, `Exe`; `Tempest.Desktop` the shell, `WinExe`) — checked against the project files, after a first draft of this row wrongly demoted `Tempest.App` to a library. The prior pass's standing caveat that a full re-derivation was "a separate, larger undertaking outside this Work Package's own scope" is discharged here, since `TD-75`'s own subject is this graph. Previously reviewed 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — narrow correction, same discipline as the prior pass below: added `Tempest.Validation` (new project, ADR-0102) — references `Tempest.Core` and `Tempest.Samples`, both downward, no cycle; referenced by `Tempest.Core.Tests` only, deliberately **not** by `Tempest.App`/`Tempest.Desktop` (the load-bearing fact this Work Package's own mechanism depends on — see `Fault Injection & Validation Architecture.md`). `Tempest.Samples`'s own "Referenced By" column gains `Tempest.Validation`. This register's own project-reference table is otherwise known to be stale beyond these two rows (does not yet list `Tempest.Desktop`/`Tempest.Desktop.Tests`/`Tempest.Templates.Module`, absent since before this table's own last full pass) — a full re-derivation remains a separate, larger undertaking outside this Work Package's own scope, named here rather than silently left implicit, matching this register's own established "narrow correction only" precedent. Previously reviewed 2026-08-11 (WP 11.3B, Presentation Strategy Implementation) — narrow correction: the "fifth position" paragraph's own named occupant updated to reflect `TempestShell`'s retirement and `ADR-0101`'s classification of `Tempest.Desktop`/`Tempest.App.Workspace` as its current occupants; the four-layer model itself and every other row unchanged. Previously reviewed 2026-07-27 (WP 5.0D). |
| **Related Documents** | `docs/architecture/Platform Service Map.md`; `docs/architecture/Fault Injection & Validation Architecture.md`; `Namespace Register.md`; `Interface Register.md`. |
| **Related ADRs** | ADR-0016, ADR-0023, ADR-0102. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/06-platform-layering.md`. |
| **Coverage Status** | Complete. |

---

## Project Reference Graph

| Project | Type | References | Referenced By |
|---|---|---|---|
| `Tempest.Core` | Library | (none) | `Tempest.App`, `Tempest.Samples`, `Tempest.Validation`, `Tempest.Templates.Module`, `Tempest.Core.Tests`, `Tempest.Desktop.Tests` |
| `Tempest.App` | Executable (console harness, `Exe`) | `Tempest.Core` | `Tempest.Desktop`, `Tempest.Core.Tests`, `Tempest.Desktop.Tests` |
| `Tempest.Desktop` | Executable (desktop shell, `WinExe`) | `Tempest.App` | `Tempest.Desktop.Tests` |
| `Tempest.Samples` | Library | `Tempest.Core` | `Tempest.Core.Tests`, `Tempest.Desktop.Tests` — **test projects only** (`TD-75`) |
| `Tempest.Validation` | Library | `Tempest.Core` | `Tempest.Core.Tests` — deliberately **not** `Tempest.App`/`Tempest.Desktop` (ADR-0102) |
| `Tempest.Templates.Module` | Library (template) | `Tempest.Core` | (none — a `dotnet new` template, not part of the product build) |
| `Tempest.Core.Tests` | Test project | `Tempest.Core`, `Tempest.Samples`, `Tempest.App`, `Tempest.Validation` | (test host — referenced by nothing) |
| `Tempest.Desktop.Tests` | Test project | `Tempest.Core`, `Tempest.Samples`, `Tempest.App`, `Tempest.Desktop` | (test host — referenced by nothing) |

**All 8 rows verified directly against each named `.csproj`'s own
`<ProjectReference>` elements**, re-derived in full by `TD-75` phase 2 —
the three projects the previous pass listed as outstanding
(`Tempest.Desktop`, `Tempest.Desktop.Tests`, `Tempest.Templates.Module`)
are now present, so the table is the whole graph rather than part of it.
`Tempest.Core` has zero outbound project references, confirming it is the
platform's own dependency root — nothing in this solution sits "below" it.

**The load-bearing property, and what `TD-75` was for: `Tempest.Samples` is
referenced only by the two test projects, so the demo harness can be deleted
from the repository and the product still builds.** That was verified the
blunt way rather than read off this table — `src/Samples` was moved out of
the tree and the remaining solution (`Tempest.Core`, `Tempest.App`,
`Tempest.Desktop`, `Tempest.Validation`, `Tempest.Templates.Module`) built
with 0 warnings and 0 errors. Two edges were removed to get there: phase 1
took `Tempest.App` → `Tempest.Samples` (the six discipline explorer modules
and the engineering calculation catalogue were declared in the sample
assembly), and phase 2 took `Tempest.Validation` → `Tempest.Samples` (the
fault-injection module read one navigation-id constant from a sample
module; it now collides with whatever `INavigationProvider.Items` already
holds, so it depends on no sample content and works with any partner
module).

This supersedes the `WP 5.0D` note that recorded `Tempest.App` as
referencing and loading `Tempest.Samples` at runtime through
`TempestShell`'s page mapping: `TempestShell` has since been retired, and
neither the reference nor the runtime load exists any more. `Tempest.Core.Tests`
keeps its reference to `Tempest.App`, to test `Tempest.App.Shell` under the
same single test project every other namespace is already tested from
(Engineering Governance §11); `Tempest.Core.Tests` and `Tempest.Desktop.Tests`
keep theirs to `Tempest.Samples` because a great many tests assert against
the fictional content the sample modules seed — a test rig asking for test
data, not a product depending on demo content. Guarded by
`SampleSeparationTests`, which fails if any project outside `src/Samples`
declares the dependency again.

## The Four-Layer Platform Model (ADR-0023)

**Modules → Platform APIs → Platform Services → Runtime Host**,
dependencies flowing downward only — a Module may depend on a Platform
API or Platform Service; a Platform Service may depend on another Platform
Service or the Runtime Host's own contracts; nothing depends upward.

| Layer | Examples | Verified By |
|---|---|---|
| Modules | `ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule` and companions | `Module Register.md` |
| Platform APIs (contracts) | `IEvent`, `IEventHandler<T>`, `ICommand`, `IHostedService`, `ICriticalBackgroundService` | `Interface Register.md`'s "Platform API" classification |
| Platform Services (implementations) | Configuration, Logging, Discovery, Registration, Lifecycle, Dependency Injection, Event Bus, Background Services infrastructure, Plugin Manifest infrastructure, Navigation Framework infrastructure | `Platform Services Register.md` |
| Runtime Host | `TempestHost`, `TempestHostBuilder` | `Architecture Document Register.md` |

**A fifth position, above all four, first implemented `WP 5.0D`:** the
Application layer — consumes the Runtime Host's own public surface
(`ITempestHost`/`ITempestHost.Services`) exactly as a human operator or
test harness already could, but is not itself one of ADR-0023's four
layers and carries no orchestration authority the Host does not already
grant any other external caller. See `ADR-0033` and `Shell & Composition
Framework Architecture.md`. **Occupant updated, `WP 11.3B`:** the
original occupant, `Tempest.App`'s own Shell (`TempestShell`,
`Tempest.App.Shell`), was retired as dead code (unreachable since
`ADR-0068`, `WP 8.1A`, `v0.8.0`) — this position is now held by
`Tempest.Desktop` (TempestOS's shipped application) and, for the shared
Workspace domain layer specifically, `Tempest.App.Workspace`
(`WorkspaceShell`, TempestOS's Internal Engineering Harness per
`ADR-0101`). Neither is one of ADR-0023's four layers either; the
position itself, and this register's own reasoning about it, is
unchanged.

## Layering Violations Found

**None.** No Module depends directly on another Module (every
cross-module interaction observed — `ClockLifecycleObserverModule`
subscribing to `ClockModule`'s event — passes through the Event Bus, a
Platform Service, never a direct reference). No Platform Service depends
upward on a Module. `NavigationService`'s own dependency on `IEventBus`
is Platform-Service-to-Platform-Service, confirmed downward-only and
introducing no cycle (`ADR-0032`). `Tempest.App`'s own new dependency on
`Tempest.Samples` (`WP 5.0D`) is Application-layer depending downward on
Modules — the same direction any consumer already depends in, never the
reverse. **Verified** by direct inspection of every Module Register/Event
Catalogue entry's own dependency list.

## Cross-Reference Check

Every layer classification above is consistent with
`docs/academy/02 Runtime Architecture/06-platform-layering.md`'s own
worked examples (Event Bus, Plugin infrastructure, Background Services,
all classified there before or during their own design phase).

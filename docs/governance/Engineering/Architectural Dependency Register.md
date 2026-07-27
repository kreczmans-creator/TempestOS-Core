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
| **Last Reviewed** | 2026-07-27 (WP 5.0B). |
| **Related Documents** | `docs/architecture/Platform Service Map.md`; `Namespace Register.md`; `Interface Register.md`. |
| **Related ADRs** | ADR-0016, ADR-0023. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/06-platform-layering.md`. |
| **Coverage Status** | Complete. |

---

## Project Reference Graph

| Project | Type | References | Referenced By |
|---|---|---|---|
| `Tempest.Core` | Library | (none) | `Tempest.App`, `Tempest.Samples`, `Tempest.Core.Tests` |
| `Tempest.Samples` | Library | `Tempest.Core` | `Tempest.App` (Unknown — not verified whether `Tempest.App` actually loads it at runtime vs. only via test project), `Tempest.Core.Tests` |
| `Tempest.App` | Executable | `Tempest.Core` | (entry point — referenced by nothing) |
| `Tempest.Core.Tests` | Test project | `Tempest.Core`, `Tempest.Samples` | (test host — referenced by nothing) |

**Total: 4 projects — Verified directly against each `.csproj`'s own
`<ProjectReference>` elements.** `Tempest.Core` has zero outbound project
references, confirming it is the platform's own dependency root — nothing
in this solution sits "below" it.

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

## Layering Violations Found

**None.** No Module depends directly on another Module (every
cross-module interaction observed — `ClockLifecycleObserverModule`
subscribing to `ClockModule`'s event — passes through the Event Bus, a
Platform Service, never a direct reference). No Platform Service depends
upward on a Module. `NavigationService`'s own dependency on `IEventBus`
is Platform-Service-to-Platform-Service, confirmed downward-only and
introducing no cycle (`ADR-0032`). **Verified** by direct inspection of
every Module Register/Event Catalogue entry's own dependency list.

## Cross-Reference Check

Every layer classification above is consistent with
`docs/academy/02 Runtime Architecture/06-platform-layering.md`'s own
worked examples (Event Bus, Plugin infrastructure, Background Services,
all classified there before or during their own design phase).

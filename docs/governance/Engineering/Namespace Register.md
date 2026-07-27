# Namespace Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Namespace Register |
| **Purpose** | The index of every namespace under `src/`, its owning project, file count, and purpose — so a reader can find "where does X live" without grepping the tree. |
| **Scope** | Every `namespace` declaration under `src/Tempest.Core/`, `src/Tempest.App/`, and `src/Samples/Tempest.Samples/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct source inspection (`grep -rhoP "^namespace" src/`). |
| **Review Frequency** | Updated whenever a new namespace is introduced under `src/`. |
| **Last Reviewed** | 2026-07-27 (WP 5.0A). |
| **Related Documents** | `docs/architecture/Engineering Glossary.md` (`Tempest.Core.Runtime` vs. `Tempest.Core.Hosting`, ADR-0016); `Interface Register.md`; `Exception Register.md`. |
| **Related ADRs** | ADR-0016, ADR-0024. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/06-platform-layering.md`. |
| **Coverage Status** | Complete. |

---

## Entries

| Namespace | Project | File Count | Purpose | Introduced |
|---|---|---|---|---|
| `Tempest.Core.Modules` | Tempest.Core | 23 | Discovery, Registration, Lifecycle, Module SDK, `ModuleMetadataAttribute` | WP 2.1–2.3, extended WP 4.1, WP 4.4B |
| `Tempest.Core.Plugins` | Tempest.Core | 13 | Plugin manifest, discovery, loading | WP 4.2 |
| `Tempest.Core.DependencyInjection` | Tempest.Core | 13 | Custom DI container | WP 2.4 |
| `Tempest.Core.Logging` | Tempest.Core | 9 | `ILogger`, sinks, factory | WP 2.6 |
| `Tempest.Core.Configuration` | Tempest.Core | 9 | Configuration sources, builder, provider | WP 2.5 |
| `Tempest.Core.BackgroundServices` | Tempest.Core | 9 | Hosted service contracts, discovery, orchestration | WP 4.0 (contracts), WP 4.5 (infrastructure) |
| `Tempest.Core.Runtime` | Tempest.Core | 7 | `TempestHost`, `TempestHostBuilder`, `HostState` | WP 2.7B; distinct from `Tempest.Core.Hosting` per ADR-0016 |
| `Tempest.Core.Events` | Tempest.Core | 4 | `IEvent`, `IEventHandler<T>`, `IEventBus`, `EventBus` | WP 4.0 (contracts), WP 4.4D (bus) |
| `Tempest.Samples` | Tempest.Samples | 3 | `ClockModule`, `ClockLifecycleObserverModule`, `ClockModuleLifecycleEvent` | WP 4.3, extended WP 4.4E |
| `Tempest.Core.Versioning` | Tempest.Core | 3 | `IPlatformVersionProvider`, `PlatformVersionProvider`, `PlatformVersion` | WP 4.2A |
| `Tempest.Core.Repositories` | Tempest.Core | 2 | Pre-module-pipeline project repository (`IProjectRepository`, `JsonProjectRepository`) | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Projects` | Tempest.Core | 1 | Pre-module-pipeline project service | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Hosting` | Tempest.Core | 1 | Pre-module-pipeline `HostingService` — environment/deployment adapters, reframed (not replaced) by ADR-0016 | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Commands` | Tempest.Core | 1 | `ICommand` contract only — no dispatcher yet | WP 4.0 |
| `Tempest.Core.Bootstrap` | Tempest.Core | 1 | Pre-module-pipeline `BootstrapService` | Pre-dates Claude-developed history (Unknown exact origin) |
| *(no namespace declared — global namespace)* | Tempest.Core, Tempest.App | 7 | `AssemblyInfo.cs`, `Program.cs`, `ApplicationConfiguration.cs`, `ConfigurationService.cs`, `LoggingService.cs`, `ProjectModel.cs`, `ProjectNumberGenerator.cs` — all pre-module-pipeline, bootstrap-era types | Pre-dates Claude-developed history (Unknown exact origin) |

**Total: 15 namespaces (14 declared + the global namespace) across 3
projects, 106 `.cs` files under `src/` excluding generated `obj`/`bin`
artifacts (Verified by direct count, corrected from an initial 100-file
count that had not excluded these 7 unnamespaced files).**

## A Note on the Four Pre-Claude Namespaces

`Tempest.Core.Repositories`, `Tempest.Core.Projects`, `Tempest.Core.Hosting`,
and `Tempest.Core.Bootstrap` are **Inferred** to predate this repository's
Claude-developed history (first Claude-authored commit `7514b9d`,
2026-07-21) — none is discussed as "newly created" by any Work Package
retrospective, and `Platform Service Map.md` itself describes the code
these namespaces contain as "bootstrap-era functionality that predates and
is currently independent of the module pipeline entirely." Their exact
original authorship and creation date are **Unknown** — the five
pre-Claude commits (`Engineering Evolution Register.md`) establish that
*some* code existed before Claude's involvement, but do not, by
themselves, prove which specific namespace originated in which commit
without a deeper `git log --follow` per file, which was out of scope for
this baseline.

## Note — `Tempest.Core.Navigation` (Designed, Not Yet Implemented)

`Tempest.Core.Navigation` (designed `WP 5.0A`, `ADR-0031`/`ADR-0032`,
`ADR-0024`'s capability-packaging pattern) is **deliberately not listed
in the Entries table above** — no `src/Tempest.Core/Navigation/`
directory exists yet (Verified — `grep -rhoP "^namespace"
src/Tempest.Core` finds no such declaration). It will be added as an
ordinary entry once `WP 5.0B` creates the namespace for real.

## Cross-Reference Check

Every namespace with an "Introduced" Work Package above is cross-checked
against `Architecture Document Register.md`'s "Primary Work Package(s)"
column and found consistent. The four pre-Claude namespaces are flagged
Unknown rather than assigned a fabricated Work Package, per this Work
Package's own governing rule.

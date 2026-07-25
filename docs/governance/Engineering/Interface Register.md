# Interface Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Interface Register |
| **Purpose** | The complete index of every public interface under `src/Tempest.Core/`, its namespace, and its DI-public/Host-owned classification. |
| **Scope** | Every `public interface` declaration under `src/Tempest.Core/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct source inspection (`grep -rhoP "^public interface" src/Tempest.Core`). |
| **Review Frequency** | Updated whenever a new public interface is introduced. |
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/architecture/Ownership Matrix.md`; `Dependency Injection Register.md`; `Namespace Register.md`. |
| **Related ADRs** | ADR-0006, ADR-0017, ADR-0020, ADR-0023, ADR-0024. |
| **Related Academy Articles** | `docs/architecture/Engineering Glossary.md` (Platform API vs. Platform Service). |
| **Coverage Status** | Complete. |

---

## Entries

| Interface | Namespace | Classification | Purpose |
|---|---|---|---|
| `ICommand` | `Tempest.Core.Commands` | Platform API (contract only) | Command Framework marker — no dispatcher yet |
| `IConfigurationProvider` | `Tempest.Core.Configuration` | DI-public (via `AddInstance`) | Read-only configuration access |
| `IConfigurationSource` | `Tempest.Core.Configuration` | Not DI-registered (input to `ConfigurationBuilder`) | A source `ConfigurationBuilder` reads |
| `ICriticalBackgroundService` | `Tempest.Core.BackgroundServices` | Platform API (marker) | Opt-in critical-failure escalation (ADR-0021) |
| `IEvent` | `Tempest.Core.Events` | Platform API (contract) | Marks a published fact |
| `IEventBus` | `Tempest.Core.Events` | DI-public | Publish/subscribe dispatch (ADR-0020) |
| `IEventHandler<T>` | `Tempest.Core.Events` | Platform API (contract) | Consumer-facing subscription contract |
| `IFrameworkDiscoveryService` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module Discovery |
| `IHostedService` | `Tempest.Core.BackgroundServices` | Platform API (contract) | Background service Start/Stop |
| `IHostedServiceDiscoveryService` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service discovery |
| `IHostedServiceManager` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service start/stop orchestration |
| `ILogSink` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Log entry destination |
| `ILogger` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Structured logging abstraction |
| `ILoggerFactory` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Produces `ILogger` instances |
| `IModule` | `Tempest.Core.Modules` | Discovered/registered, not DI-registered as an interface | Module identity contract |
| `IModuleLifecycle` | `Tempest.Core.Modules` | Discovered/registered | Module lifecycle contract |
| `IModuleLifecycleManager` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module lifecycle orchestration |
| `IPlatformVersionProvider` | `Tempest.Core.Versioning` | DI-public (via `AddInstance`) | Platform version query |
| `IPluginAssemblyLoader` | `Tempest.Core.Plugins` | Host-owned, never DI-public (ADR-0017 extended) | Plugin assembly loading |
| `IPluginManifestDiscoveryService` | `Tempest.Core.Plugins` | Host-owned, never DI-public (ADR-0017 extended) | Plugin manifest discovery |
| `IProjectRepository` | `Tempest.Core.Repositories` | Pre-module-pipeline, not part of the platform-service model | Project persistence (bootstrap-era) |
| `IRuntimeModuleManager` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module registration catalogue |
| `IServiceCollection` | `Tempest.Core.DependencyInjection` | Composition-time only (not itself registered) | DI registration accumulation |
| `ITempestHost` | `Tempest.Core.Runtime` | Not DI-registered (returned by the builder) | The running Host instance |
| `ITempestHostBuilder` | `Tempest.Core.Runtime` | Not DI-registered (the composition root's own entry point) | Assembles and produces a `TempestHost` |
| `ITempestServiceProvider` | `Tempest.Core.DependencyInjection` | The container itself | Constructs and resolves service instances |

**Total: 26 public interfaces under `src/Tempest.Core/` — Verified
directly.**

## Classification Summary

| Classification | Count |
|---|---|
| DI-public (`AddInstance` or container-constructed singleton) | 7 |
| Host-owned, never DI-public (ADR-0017 and its extensions) | 6 |
| Platform API / contract only (no dispatcher or orchestration yet, or consumer-facing marker) | 5 |
| Discovered/registered but not itself a DI registration target | 3 |
| Composition-time / not-DI-registered infrastructure | 4 |
| Pre-module-pipeline, outside the platform-service model | 1 |

## Cross-Reference Check

Every "Host-owned, never DI-public" interface above matches a row in
`docs/architecture/Ownership Matrix.md`; every "DI-public" interface
matches a registration row in `Dependency Injection Register.md`. No
discrepancy found between this register's classification and either
source document.

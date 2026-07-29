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
| **Last Reviewed** | 2026-07-29 (WP 6.7, Export/Import) — added this Work Package's own seven new interfaces (see Entries table, below); see the disclosed gap under Coverage Status. |
| **Related Documents** | `docs/architecture/Ownership Matrix.md`; `Dependency Injection Register.md`; `Namespace Register.md`. |
| **Related ADRs** | ADR-0006, ADR-0009, ADR-0017, ADR-0020, ADR-0023, ADR-0024, ADR-0034, ADR-0036, ADR-0037, ADR-0039, ADR-0051. |
| **Related Academy Articles** | `docs/architecture/Engineering Glossary.md` (Platform API vs. Platform Service). |
| **Coverage Status** | **Partial — a genuine, disclosed gap found during `WP 6.7`'s own repository review, not introduced by this Work Package.** This register's own `Last Reviewed` line read `WP 5.2` before this Work Package touched it — every public interface `WP 6.1` (Identity & Permissions), `WP 6.4` (Persistence, Settings), `WP 6.5` (Audit), `WP 6.2` (Notifications), `WP 6.0` (Reporting), and `WP 6.3` (REST API) each introduced was missing entirely (23 interfaces, confirmed by direct `grep` against the 31 previously listed). `WP 6.7` adds only its own seven new interfaces below, correctly classified, rather than attempting to retroactively backfill six unrelated Work Packages' worth of entries under this Work Package's own scope — a full backfill is recommended as `WP 6.8` (Platform Services Integration Review)'s own closing-audit task, exactly the kind of accumulated drift that Work Package exists to catch. |

---

## Entries

| Interface | Namespace | Classification | Purpose |
|---|---|---|---|
| `ICommand` | `Tempest.Core.Commands` | Platform API (contract only) | Command Framework marker — dispatched by concrete type (`ICommandDispatcher`, `WP 5.1B`) |
| `ICommandDispatcher` | `Tempest.Core.Commands` | DI-public | Type-keyed handler registration/dispatch (ADR-0036/ADR-0037) |
| `ICommandHandler<T>` | `Tempest.Core.Commands` | Platform API (contract) | Consumer-facing command handler contract |
| `ICommandRegistry` | `Tempest.Core.Commands` | DI-public | Id-keyed command catalogue/invocation (ADR-0036/ADR-0037) |
| `IConfigurationProvider` | `Tempest.Core.Configuration` | DI-public (via `AddInstance`) | Read-only configuration access |
| `IConfigurationSource` | `Tempest.Core.Configuration` | Not DI-registered (input to `ConfigurationBuilder`) | A source `ConfigurationBuilder` reads |
| `ICriticalBackgroundService` | `Tempest.Core.BackgroundServices` | Platform API (marker) | Opt-in critical-failure escalation (ADR-0021) |
| `IDiagnosticsProvider` | `Tempest.Core.Diagnostics` | DI-public (via `AddInstance`) | Read-only projection over Host/module/hosted-service lifecycle state (ADR-0039) |
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
| `INavigationProvider` | `Tempest.Core.Navigation` | DI-public | Navigation registry + `Navigate` (ADR-0031/ADR-0032) |
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
| `IExportable` | `Tempest.Core.ExportImport` | Platform API (contract) | Marks a source's data as exportable, round-trip-safe (ADR-0051) |
| `IExportService` | `Tempest.Core.ExportImport` | DI-public | Exports one or more `IExportable` sources into a single artifact |
| `IImportService` | `Tempest.Core.ExportImport` | DI-public (dual-registered under its own concrete type, mirroring `ICurrentPrincipalAccessor`) | Reads a previously exported artifact back into the owning service(s) |
| `IExportableKind` | `Tempest.Core.ExportImport` | Platform API (optional companion contract) | Supplies a source's own stable artifact-section identifier |
| `IImportable` | `Tempest.Core.ExportImport` | Registered via `ImportService.RegisterImportable`, not itself a DI service type | Read-back counterpart to `IExportable`, routed to by `Kind` |
| `IExportFormat` | `Tempest.Core.ExportImport` | DI-public (via `AddInstance`) | Frames/reads the multi-section artifact envelope |
| `IExportPayloadSerializer` | `Tempest.Core.ExportImport` | Not DI-registered (optional collaborator, mirroring `IReportTemplate<T>`) | Converts a key/value data set to/from raw bytes |

**Total: 61 public interfaces actually exist under `src/Tempest.Core/`
today — Verified directly by `WP 6.7` (`grep -rhoP "^public interface
\w+"`). Only 38 are listed above (the 31 previously listed + this Work
Package's own 7 new entries) — the remaining 23, introduced by `WP
6.1`/`WP 6.4`/`WP 6.5`/`WP 6.2`/`WP 6.0`/`WP 6.3`, are the disclosed gap
under Coverage Status, left for `WP 6.8`'s own backfill rather than
retrofitted here under a different Work Package's own scope.**

## Classification Summary

**Reflects only the 38 interfaces actually listed in the Entries table
above — not a true classification of all 61 that exist (see the
disclosed gap under Coverage Status).**

| Classification | Count |
|---|---|
| DI-public (`AddInstance` or container-constructed singleton) | 14 |
| Host-owned, never DI-public (ADR-0017 and its extensions) | 6 |
| Platform API / contract only (no dispatcher or orchestration yet, or consumer-facing marker) | 8 |
| Discovered/registered but not itself a DI registration target | 4 |
| Composition-time / not-DI-registered infrastructure | 5 |
| Pre-module-pipeline, outside the platform-service model | 1 |

## Cross-Reference Check

Every "Host-owned, never DI-public" interface above matches a row in
`docs/architecture/Ownership Matrix.md`; every "DI-public" interface
matches a registration row in `Dependency Injection Register.md`. No
discrepancy found between this register's classification and either
source document.

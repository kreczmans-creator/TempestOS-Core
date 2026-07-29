# Dependency Injection Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Dependency Injection Register |
| **Purpose** | The index of every DI-public service registration `TempestHost` performs, and every extension method the custom container exposes to perform one. |
| **Scope** | `TempestHost.ExecuteStartupPhasesAsync`'s own registration calls (Phase 6, Platform Services Registered), and `src/Tempest.Core/DependencyInjection/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `src/Tempest.Core/Runtime/TempestHost.cs` (the registration call sites); `src/Tempest.Core/DependencyInjection/`. |
| **Review Frequency** | Updated whenever `TempestHost`'s Platform Services Registered phase changes, or a new `IServiceCollection` extension method is added. |
| **Last Reviewed** | 2026-07-29 (WP 6.6, Licensing) — one new registration call site added (`ILicenseProvider`, via `AddInstance`; `ILicenseValidator` is deliberately never container-registered at all — see below); see the disclosed gap under Coverage Status. |
| **Related Documents** | `docs/architecture/Host Lifecycle.md` (Phase 6); `docs/architecture/Ownership Matrix.md`; `Interface Register.md`. |
| **Related ADRs** | ADR-0005 through ADR-0009, ADR-0011, ADR-0017, ADR-0020, ADR-0036, ADR-0039, ADR-0044, ADR-0050, ADR-0051. |
| **Related Academy Articles** | `docs/academy/01 Engineering Principles/05-dependency-injection.md`; `docs/academy/03 Work Packages/WP2.4-dependency-injection.md`. |
| **Coverage Status** | **Partial — a genuine, disclosed gap found during `WP 6.7`'s own repository review, not introduced by that Work Package or this one.** This register's own `Last Reviewed` line read `WP 5.2` before `WP 6.7` touched it — every Phase 6 registration `WP 6.1`, `WP 6.4`, `WP 6.5`, `WP 6.2`, `WP 6.0`, and `WP 6.3` each added was missing entirely. `WP 6.7` added only its own new registrations; `WP 6.6` adds only its own new registration below, correctly described, rather than retroactively backfilling the six unrelated Work Packages' worth of rows under either Work Package's own scope — a full backfill remains recommended as `WP 6.8` (Platform Services Integration Review)'s own closing-audit task. |

---

## Registration Surface (`ServiceCollectionExtensions`)

| Method | Lifetime | Purpose |
|---|---|---|
| `Singleton(Type, Type)` | Singleton | Type-based, non-generic registration — used by `AddDiscoveredModules`/`AddDiscoveredHostedServices` for reflection-discovered types |
| `Singleton<TService>()` | Singleton | Self-referential generic registration |
| `Singleton<TService, TImplementation>()` | Singleton | Interface-to-implementation generic registration (e.g. `IEventBus` → `EventBus`) |
| `Transient(Type, Type)` | Transient | Type-based, non-generic registration |
| `Transient<TService>()` | Transient | Self-referential generic registration |
| `Transient<TService, TImplementation>()` | Transient | Interface-to-implementation generic registration |
| `AddInstance<TService>(TService)` | Singleton (pre-constructed) | Registers an already-constructed instance — the Composition Root pattern (ADR-0009) |

**Total lifetimes supported: 2 (Singleton, Transient) — Verified directly
against `ServiceLifetime.cs`.** No Scoped lifetime exists; TempestOS has
no request/scope concept to justify one.

## What `TempestHost` Registers (Phase 6, Platform Services Registered)

| Registration | Mechanism | Registered As |
|---|---|---|
| `IConfigurationProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009) |
| `ILogSink` | `AddInstance` | Pre-built instance |
| `ILoggerFactory` | `AddInstance` | Pre-built instance |
| `ILogger` | `AddInstance` | Pre-built instance |
| `IPlatformVersionProvider` | `AddInstance` | Pre-built instance |
| `IEventBus` | `Singleton<IEventBus, EventBus>()` | Ordinary container-constructed singleton (ADR-0020) |
| `INavigationProvider` | `Singleton<INavigationProvider, NavigationService>()` | Ordinary container-constructed singleton (ADR-0032), registered immediately after `IEventBus` |
| `CommandHandlerTable` | `Singleton<CommandHandlerTable>()` | Ordinary container-constructed singleton — an implementation-supporting collaborator, not a documented Platform API, shared by `ICommandDispatcher` and `ICommandRegistry` so both operate against the identical handler set (see `Command Framework Architecture.md`'s Architecture Verification) |
| `ICommandDispatcher` | `Singleton<ICommandDispatcher, CommandDispatcher>()` | Ordinary container-constructed singleton (ADR-0036), registered immediately after `INavigationProvider` |
| `ICommandRegistry` | `Singleton<ICommandRegistry, CommandRegistry>()` | Ordinary container-constructed singleton (ADR-0036), registered immediately after `ICommandDispatcher` |
| `IDiagnosticsProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009), constructed with `Func<T>` accessors closing over `TempestHost`'s own `_lifecycleManager`/`_hostedServiceManager` private fields — neither manager exists yet at this phase (ADR-0039) |
| Every discovered module type | `AddDiscoveredModules` → `Singleton(type, type)` per type | Self-referential singleton |
| Every discovered hosted service type | `AddDiscoveredHostedServices` → `Singleton(type, type)` per type | Self-referential singleton |
| `IExportFormat` | `AddInstance` | Pre-built `JsonExportFormat` instance (Composition Root, ADR-0009), shared by both `ExportService` and `ImportService` |
| `IExportService` | `Singleton<IExportService, ExportService>()` | Ordinary container-constructed singleton, registered immediately after `IApiEndpointRegistry` |
| `IImportService` / `ImportService` | `AddInstance` (twice, under both keys) | The same already-built `ImportService` instance registered under both its own concrete type and `IImportService` — mirroring `ICurrentPrincipalAccessor`'s own dual-registration precedent (ADR-0044), so a module needing `RegisterImportable` resolves the concrete type while every ordinary consumer resolves only the interface (ADR-0051) |
| `ILicenseProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009), constructed from the already-validated `ILicense` a pre-container `ILicenseValidator` produced before Phase 1 — registered immediately after Identity & Permissions (ADR-0050) |

**Total distinct registration call sites in `TempestHost.cs`: 18 listed
above (adds `ILicenseProvider`, `WP 6.6`) — 28 actually exist; the
remaining 10, added by `WP 6.1`/`WP 6.4`/`WP 6.5`/`WP 6.2`/`WP 6.0`/`WP
6.3`, are the disclosed gap under Coverage Status, left for `WP 6.8`'s
own backfill. `ILicenseValidator` is deliberately never registered at
all — constructed directly by `TempestHost`, before the container
exists, since no container exists yet at its own construction point
(`ADR-0050`).**

**A new external consumption path, not a new registration (`WP 5.0D`).**
`ITempestHost.Services` (ADR-0034) exposes read-only resolution against
this exact same registration table to a caller that is not itself a
module — `Tempest.App`'s own Shell, first implemented `WP 5.0D`. No new
row belongs in the table above: `Services` changes *who can resolve*
what is already registered, never *what is registered*.

## Host-Owned Collaborators Deliberately Never Registered (ADR-0017)

| Collaborator | Reason |
|---|---|
| `IFrameworkDiscoveryService` / `ReflectionFrameworkDiscoveryService` | Would let a module reach back into Discovery |
| `IRuntimeModuleManager` / `RuntimeModuleManager` | Would let a module register/deregister other modules |
| `IModuleLifecycleManager` / `ModuleLifecycleManager` | Would let a module drive other modules' lifecycles |
| `IPluginManifestDiscoveryService`, `IPluginAssemblyLoader` | Mirror the same exclusion, extended to Plugins (WP 4.2) |
| `IHostedServiceDiscoveryService`, `IHostedServiceManager` | Mirror the same exclusion, extended to Background Services (WP 4.5) |

## Cross-Reference Check

Every registration above is cited in `Host Lifecycle.md`'s Phase 6
description and `Ownership Matrix.md`'s own object-by-object table. The
Host-owned exclusion list above matches `Ownership Matrix.md`'s own
"Owner: `TempestHost`, never registered in DI" rows exactly — no
discrepancy found.

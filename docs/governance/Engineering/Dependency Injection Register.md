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
| **Last Reviewed** | 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every registration `TempestHost.cs` performs is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
| **Related Documents** | `docs/architecture/Host Lifecycle.md` (Phase 6); `docs/architecture/Ownership Matrix.md`; `Interface Register.md`. |
| **Related ADRs** | ADR-0005 through ADR-0009, ADR-0011, ADR-0017, ADR-0020, ADR-0036, ADR-0039, ADR-0040–ADR-0052. |
| **Related Academy Articles** | `docs/academy/01 Engineering Principles/05-dependency-injection.md`; `docs/academy/03 Work Packages/WP2.4-dependency-injection.md`. |
| **Coverage Status** | **Complete.** Full backfill performed directly against `src/Tempest.Core/Runtime/TempestHost.cs`'s own Phase 6 registration block. |

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

In registration order, exactly as they appear in
`TempestHost.ExecuteStartupPhasesAsync`:

| Registration | Mechanism | Registered As |
|---|---|---|
| `IConfigurationProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009) |
| `ILogSink` | `AddInstance` | Pre-built instance |
| `ILoggerFactory` | `AddInstance` | Pre-built instance |
| `ILogger` | `AddInstance` | Pre-built instance |
| `IPlatformVersionProvider` | `AddInstance` | Pre-built instance |
| `IEventBus` | `Singleton<IEventBus, EventBus>()` | Ordinary container-constructed singleton (ADR-0020) |
| `IReportingService` | `Singleton<IReportingService, ReportingService>()` | Ordinary container-constructed singleton (`WP 6.0`), registered immediately after `IEventBus` |
| `INotificationDispatcher` | `Singleton<INotificationDispatcher, NotificationDispatcher>()` | Ordinary container-constructed singleton (`WP 6.2`), registered immediately after `IReportingService` |
| `INavigationProvider` | `Singleton<INavigationProvider, NavigationService>()` | Ordinary container-constructed singleton (ADR-0032) |
| `CommandHandlerTable` | `Singleton<CommandHandlerTable>()` | Ordinary container-constructed singleton — an implementation-supporting collaborator, not a documented Platform API, shared by `ICommandDispatcher` and `ICommandRegistry` so both operate against the identical handler set (see `Command Framework Architecture.md`'s Architecture Verification) |
| `ICommandDispatcher` | `Singleton<ICommandDispatcher, CommandDispatcher>()` | Ordinary container-constructed singleton (ADR-0036), registered immediately after `INavigationProvider` |
| `ICommandRegistry` | `Singleton<ICommandRegistry, CommandRegistry>()` | Ordinary container-constructed singleton (ADR-0036), registered immediately after `ICommandDispatcher` |
| `ICurrentPrincipalAccessor` / `CurrentPrincipalAccessor` | `AddInstance` (twice, under both keys) | The same already-built `CurrentPrincipalAccessor` instance registered under both its own concrete type and `ICurrentPrincipalAccessor` (`WP 6.1`, ADR-0044) — so `IdentityService` (which needs write access via the concrete type) and every ordinary consumer (read-only interface) share the exact same object |
| `IRoleProvider` | `Singleton<IRoleProvider, RoleProvider>()` | Ordinary container-constructed singleton (`WP 6.1`, additive) |
| `IPermissionEvaluator` | `Singleton<IPermissionEvaluator, PermissionEvaluator>()` | Ordinary container-constructed singleton (`WP 6.1`, ADR-0044) |
| `IIdentityService` | `Singleton<IIdentityService, IdentityService>()` | Ordinary container-constructed singleton (`WP 6.1`, additive), depends on the concrete `CurrentPrincipalAccessor`, `IRoleProvider` |
| `ILicenseProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009), constructed from the already-validated `ILicense` a pre-container `ILicenseValidator` produced before Phase 1 — registered immediately after Identity & Permissions (`WP 6.6`, ADR-0050) |
| `IPersistenceStore` | `Singleton<IPersistenceStore, PersistenceStore>()` | Ordinary container-constructed singleton (`WP 6.4`, ADR-0041), established as part of Settings' own scope, ahead of Settings' own registration |
| `ISettingsProvider` | `Singleton<ISettingsProvider, SettingsProvider>()` | Ordinary container-constructed singleton (`WP 6.4`), depends on `IPersistenceStore` |
| `IAuditRecorder` | `Singleton<IAuditRecorder, AuditRecorder>()` | Ordinary container-constructed singleton (`WP 6.5`, ADR-0045), depends on `IPersistenceStore` and Identity & Permissions — registered after both |
| `IAuditQuery` | `Singleton<IAuditQuery, AuditQuery>()` | Ordinary container-constructed singleton (`WP 6.5`), depends on `IPersistenceStore` and `IPermissionEvaluator` |
| `IApiEndpointRegistry` | `Singleton<IApiEndpointRegistry, ApiEndpointRegistry>()` | Ordinary container-constructed singleton (`WP 6.3`, ADR-0047) — the hosted-service scaffold itself (`RestApiHostedService`) is registered separately, via hosted service discovery, not part of this ordered list |
| `IExportFormat` | `AddInstance` | Pre-built `JsonExportFormat` instance (Composition Root, ADR-0009), shared by both `ExportService` and `ImportService` (`WP 6.7`) |
| `IExportService` | `Singleton<IExportService, ExportService>()` | Ordinary container-constructed singleton (`WP 6.7`), registered immediately after `IApiEndpointRegistry` |
| `IImportService` / `ImportService` | `AddInstance` (twice, under both keys) | The same already-built `ImportService` instance registered under both its own concrete type and `IImportService` — mirroring `ICurrentPrincipalAccessor`'s own dual-registration precedent (ADR-0044), so a module needing `RegisterImportable` resolves the concrete type while every ordinary consumer resolves only the interface (`WP 6.7`, ADR-0051) |
| `IDiagnosticsProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009), constructed with `Func<T>` accessors closing over `TempestHost`'s own `_lifecycleManager`/`_hostedServiceManager` private fields — neither manager exists yet at this phase (`WP 5.2`, ADR-0039) |
| Every discovered module type | `AddDiscoveredModules` → `Singleton(type, type)` per type | Self-referential singleton |
| Every discovered hosted service type | `AddDiscoveredHostedServices` → `Singleton(type, type)` per type | Self-referential singleton |

**Total: 26 individually-named registrations above (two of which —
`ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor` and
`IImportService`/`ImportService` — are each dual-registered via two
`AddInstance` calls under two keys), plus 2 further rows
(`AddDiscoveredModules`, `AddDiscoveredHostedServices`) each registering
a dynamic set of discovered types. Verified directly: 28 `Singleton`/
`AddInstance` call sites plus 2 `AddDiscovered*` call sites = 30 total
registration statements in `TempestHost.cs`'s own Phase 6 block
(`grep -n "services\.\(Singleton\|AddInstance\)" src/Tempest.Core/
Runtime/TempestHost.cs` returns 28; adding the 2 `AddDiscovered*` lines
gives 30), matching this table's own 26 named single/dual registrations
(26 rows, accounting for 28 raw `Singleton`/`AddInstance` calls) plus 2
discovered-type rows exactly.** `ILicenseValidator` is deliberately
never registered at all — constructed directly by `TempestHost`, before
the container exists, since no container exists yet at its own
construction point (`ADR-0050`).

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
| `ILicenseValidator` | Cannot be registered — runs before the container exists at all (`WP 6.6`, ADR-0050); distinct reason from the five above (timing, not a trust boundary) |

## Cross-Reference Check

Every registration above is cited in `Host Lifecycle.md`'s Phase 6
description and `Ownership Matrix.md`'s own object-by-object table. The
Host-owned exclusion list above matches `Ownership Matrix.md`'s own
"Owner: `TempestHost`, never registered in DI" rows, plus
`ILicenseValidator`'s own distinct pre-container exclusion — no
discrepancy found. Cross-checked directly against `Interface
Register.md`'s own full backfill (this same Work Package): every
interface marked "DI-public" there has a corresponding row here, and
every interface marked "Host-owned" or "not DI-registered" there has no
row here — consistent in both directions.

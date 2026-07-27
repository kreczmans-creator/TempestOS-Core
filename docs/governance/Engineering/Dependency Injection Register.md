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
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/architecture/Host Lifecycle.md` (Phase 6); `docs/architecture/Ownership Matrix.md`; `Interface Register.md`. |
| **Related ADRs** | ADR-0005 through ADR-0009, ADR-0011, ADR-0017, ADR-0020. |
| **Related Academy Articles** | `docs/academy/01 Engineering Principles/05-dependency-injection.md`; `docs/academy/03 Work Packages/WP2.4-dependency-injection.md`. |
| **Coverage Status** | Complete. |

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
| Every discovered module type | `AddDiscoveredModules` → `Singleton(type, type)` per type | Self-referential singleton |
| Every discovered hosted service type | `AddDiscoveredHostedServices` → `Singleton(type, type)` per type | Self-referential singleton |

**Total distinct registration call sites in `TempestHost.cs`: 8 (Verified
by direct line count of Phase 6's own registration block).**

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

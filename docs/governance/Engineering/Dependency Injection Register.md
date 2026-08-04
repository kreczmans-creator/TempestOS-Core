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
| **Last Reviewed** | 2026-08-04 (WP 8.2C, Engineering Domain Implementation) — ten new registrations added directly at implementation time, not backfilled later; 31 → 41. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — `IRequirementsService` added directly at implementation time, not backfilled later. Previously reviewed 2026-07-30 (WP 7.1F, Engineering Core Integration Review & Certification) — full backfill performed; four registrations added across the Engineering Foundation programme (`IEngineeringDocumentStore` `WP 7.1A`, `IMaterialCatalog` `WP 7.1C`, `ICalculationEngine` `WP 7.1D`, `IVerificationService` `WP 7.1E`) had never been recorded here — stale since `WP 6.8`, closed by this Work Package's own certification review. `Tempest.Core.UnitsAndQuantities` (`WP 7.1B`) confirmed to register nothing, exactly as its own approved design requires. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every registration `TempestHost.cs` performs is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
| **Related Documents** | `docs/architecture/Host Lifecycle.md` (Phase 6); `docs/architecture/Ownership Matrix.md`; `Interface Register.md`. |
| **Related ADRs** | ADR-0005 through ADR-0009, ADR-0011, ADR-0017, ADR-0020, ADR-0036, ADR-0039, ADR-0040–ADR-0057. |
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
| `IEngineeringDocumentStore` | `Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>()` | Ordinary container-constructed singleton (`WP 7.1A`, ADR-0053), built directly on `IPersistenceStore` — registered after Persistence and Identity & Permissions, both of which it depends on |
| `IEngineeringObjectRepository` | `Singleton<IEngineeringObjectRepository, InMemoryEngineeringObjectRepository>()` | Ordinary container-constructed singleton (`WP 8.2C`, `ADR-0077`), zero constructor dependencies — a new, purely in-memory index, registered immediately after `IEngineeringDocumentStore` |
| `IEngineeringRelationshipRepository` | `Singleton<IEngineeringRelationshipRepository, InMemoryEngineeringRelationshipRepository>()` | Ordinary container-constructed singleton (`WP 8.2C`, `ADR-0077`), zero constructor dependencies |
| `ILifecycleTransitionTable` | `Singleton<ILifecycleTransitionTable, LifecycleTransitionTable>()` | Ordinary container-constructed singleton (`WP 8.2C`), zero constructor dependencies — a static, canonical eight-state table |
| `IValidationRuleSet` | `Singleton<IValidationRuleSet, ValidationRuleSet>()` | Ordinary container-constructed singleton (`WP 8.2C`), zero constructor dependencies — zero rules registered by default |
| `IReferenceIntegrityChecker` | `Singleton<IReferenceIntegrityChecker, ReferenceIntegrityChecker>()` | Ordinary container-constructed singleton (`WP 8.2C`), depends on `IEngineeringObjectRepository` |
| `IRelationshipDiscovery` | `Singleton<IRelationshipDiscovery, RelationshipDiscoveryService>()` | Ordinary container-constructed singleton (`WP 8.2C`), depends on `IEngineeringRelationshipRepository`, `IEngineeringObjectRepository` |
| `IDependencyTraversal` | `Singleton<IDependencyTraversal, RelationshipDiscoveryService>()` | A second, independent `RelationshipDiscoveryService` singleton (`WP 8.2C`) — stateless, so a separate instance per interface costs nothing observable; not dual-registered via `AddInstance` |
| `IImpactAnalysis` | `Singleton<IImpactAnalysis, RelationshipDiscoveryService>()` | A third, independent `RelationshipDiscoveryService` singleton (`WP 8.2C`), same reasoning as `IDependencyTraversal` above |
| `IEvidenceComposer` | `Singleton<IEvidenceComposer, EvidenceComposer>()` | Ordinary container-constructed singleton (`WP 8.2C`), depends on `IRelationshipDiscovery`, `IEngineeringObjectRepository` |
| `EngineeringDomainContext` | `Singleton<EngineeringDomainContext>()` | Ordinary container-constructed singleton (`WP 8.2C`), depends on `IEngineeringDocumentStore` and all nine services immediately above plus `ICurrentPrincipalAccessor` — the shared collaborator bundle every `EngineeringObjectFactory<T>` needs |
| `IMaterialCatalog` | `Singleton<IMaterialCatalog, MaterialCatalog>()` | Ordinary container-constructed singleton (`WP 7.1C`, ADR-0055), a thin, typed index over `IEngineeringDocumentStore` plus a direct `IPersistenceStore` dependency of its own for its `materialId` index — registered after both |
| `ICalculationEngine` | `Singleton<ICalculationEngine, CalculationEngine>()` | Ordinary container-constructed singleton (`WP 7.1D`, ADR-0056), depends on `IEngineeringDocumentStore` only — registered immediately after `IMaterialCatalog` |
| `IVerificationService` | `Singleton<IVerificationService, VerificationService>()` | Ordinary container-constructed singleton (`WP 7.1E`, ADR-0057), depends on `IEngineeringDocumentStore` and Identity & Permissions — registered immediately after `ICalculationEngine` |
| `IRequirementsService` | `Singleton<IRequirementsService, RequirementsService>()` | Ordinary container-constructed singleton (`WP 7.3A`, ADR-0058), depends on `IEngineeringDocumentStore`, `IPersistenceStore`, Identity & Permissions, and `IVerificationService` — registered immediately after `IVerificationService` |
| `IDiagnosticsProvider` | `AddInstance` | Pre-built instance (Composition Root, ADR-0009), constructed with `Func<T>` accessors closing over `TempestHost`'s own `_lifecycleManager`/`_hostedServiceManager` private fields — neither manager exists yet at this phase (`WP 5.2`, ADR-0039) |
| Every discovered module type | `AddDiscoveredModules` → `Singleton(type, type)` per type | Self-referential singleton |
| Every discovered hosted service type | `AddDiscoveredHostedServices` → `Singleton(type, type)` per type | Self-referential singleton |

**Total: 41 individually-named registrations above (two of which —
`ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor` and
`IImportService`/`ImportService` — are each dual-registered via two
`AddInstance` calls under two keys), plus 2 further rows
(`AddDiscoveredModules`, `AddDiscoveredHostedServices`) each registering
a dynamic set of discovered types. Verified directly: 43 `Singleton`/
`AddInstance` call sites plus 2 `AddDiscovered*` call sites = 45 total
registration statements in `TempestHost.cs`'s own Phase 6 block
(`grep -n "services\.\(Singleton\|AddInstance\)" src/Tempest.Core/
Runtime/TempestHost.cs` returns 43; adding the 2 `AddDiscovered*` lines
gives 45), matching this table's own 41 named single/dual registrations
(41 rows, accounting for 43 raw `Singleton`/`AddInstance` calls) plus 2
discovered-type rows exactly. Ten new rows —
`IEngineeringObjectRepository`, `IEngineeringRelationshipRepository`,
`ILifecycleTransitionTable`, `IValidationRuleSet`,
`IReferenceIntegrityChecker`, `IRelationshipDiscovery`,
`IDependencyTraversal`, `IImpactAnalysis`, `IEvidenceComposer`,
`EngineeringDomainContext` — were added by `WP 8.2C` (Engineering
Domain Implementation) and recorded directly at implementation time,
not backfilled later; 31 → 41. Four of these —
`IEngineeringDocumentStore`, `IMaterialCatalog`, `ICalculationEngine`,
`IVerificationService` — were added by the Engineering Foundation
programme (`WP 7.1A`, `WP 7.1C`, `WP 7.1D`, `WP 7.1E` respectively) and
had never been recorded in this register before `WP 7.1F`'s own
backfill; `Tempest.Core.UnitsAndQuantities` (`WP 7.1B`) registers
nothing, by design (`FCR-0030`'s own "zero Platform Service dependency
and no DI registration of any kind"). `IRequirementsService` was added
by `WP 7.3A` (Requirements Engine) and recorded directly at
implementation time, not backfilled later.**
`ILicenseValidator` is deliberately never registered at all —
constructed directly by `TempestHost`, before the container exists,
since no container exists yet at its own construction point (`ADR-0050`).

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

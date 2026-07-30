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
| **Last Reviewed** | 2026-07-30 (WP 7.1F, Engineering Core Integration Review & Certification) — full backfill performed; 11 interfaces introduced across all five Engineering Foundation Work Packages (`WP 7.1A`–`WP 7.1E`) are now listed, none of which had ever been recorded here — this register had gone stale since `WP 6.8` (2026-07-29), the exact drift pattern `FCR-0005` exists to catch, now found and closed by this Work Package's own certification review, mirroring `WP 6.8`'s own identical finding for the `v0.6.0` release. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every interface introduced since `WP 5.2` (`WP 6.1`, `WP 6.4`, `WP 6.5`, `WP 6.2`, `WP 6.0`, `WP 6.3`, `WP 6.7`, `WP 6.6`) is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
| **Related Documents** | `docs/architecture/Ownership Matrix.md`; `Dependency Injection Register.md`; `Namespace Register.md`. |
| **Related ADRs** | ADR-0006, ADR-0009, ADR-0017, ADR-0020, ADR-0023, ADR-0024, ADR-0034, ADR-0036, ADR-0037, ADR-0039, ADR-0040–ADR-0057. |
| **Related Academy Articles** | `docs/architecture/Engineering Glossary.md` (Platform API vs. Platform Service); `docs/engineering/Engineering Principles.md`. |
| **Coverage Status** | **Complete.** Full backfill performed directly against `grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core` — 75 interfaces found, 75 listed below, zero omitted. A genuine, pre-existing arithmetic drift was also found and corrected during the `WP 6.8` backfill: the register's own Classification Summary read "Host-owned = 6" while its own Entries table already listed 7 Host-owned rows (`IFrameworkDiscoveryService`, `IHostedServiceDiscoveryService`, `IHostedServiceManager`, `IModuleLifecycleManager`, `IPluginAssemblyLoader`, `IPluginManifestDiscoveryService`, `IRuntimeModuleManager`) — an undercount that predates `WP 6.7`'s own first disclosure of the larger gap, corrected at that time. |

---

## Entries

| Interface | Namespace | Classification | Purpose |
|---|---|---|---|
| `IApiEndpointRegistry` | `Tempest.Core.Api` | DI-public | Maps HTTP method+path to a registered command Id (`WP 6.3`) |
| `IAuditQuery` | `Tempest.Core.Audit` | DI-public | Permission-gated, filtered query over recorded actions (`WP 6.5`) |
| `IAuditRecord` | `Tempest.Core.Audit` | Platform API (data contract) | The shape of one recorded action (`WP 6.5`) |
| `IAuditRecorder` | `Tempest.Core.Audit` | DI-public | Records an attributable action (`WP 6.5`) |
| `ICalculationDefinition<TInput, TResult>` | `Tempest.Core.Calculations` | Platform API (contract, registered by Id, not itself DI-registered) | A pure, registrable calculation's own input/output/formula contract (`WP 7.1D`, `ADR-0056`) |
| `ICalculationEngine` | `Tempest.Core.Calculations` | DI-public | Registration/dispatch of `ICalculationDefinition<TInput, TResult>` by Id, mirroring `ICommandRegistry`'s own shape (`WP 7.1D`, `ADR-0056`) |
| `ICommand` | `Tempest.Core.Commands` | Platform API (contract only) | Command Framework marker — dispatched by concrete type (`ICommandDispatcher`, `WP 5.1B`) |
| `ICommandDispatcher` | `Tempest.Core.Commands` | DI-public | Type-keyed handler registration/dispatch (ADR-0036/ADR-0037) |
| `ICommandHandler<T>` | `Tempest.Core.Commands` | Platform API (contract) | Consumer-facing command handler contract |
| `ICommandRegistry` | `Tempest.Core.Commands` | DI-public | Id-keyed command catalogue/invocation (ADR-0036/ADR-0037) |
| `IConfigurationProvider` | `Tempest.Core.Configuration` | DI-public (via `AddInstance`) | Read-only configuration access |
| `IConfigurationSource` | `Tempest.Core.Configuration` | Not DI-registered (input to `ConfigurationBuilder`) | A source `ConfigurationBuilder` reads |
| `ICriticalBackgroundService` | `Tempest.Core.BackgroundServices` | Platform API (marker) | Opt-in critical-failure escalation (ADR-0021) |
| `ICurrentPrincipalAccessor` | `Tempest.Core.Identity` | DI-public (via `AddInstance`, dual-registered under its own concrete type per ADR-0044) | Read-only view of the ambient current principal (`WP 6.1`) |
| `IDiagnosticsProvider` | `Tempest.Core.Diagnostics` | DI-public (via `AddInstance`) | Read-only projection over Host/module/hosted-service lifecycle state (ADR-0039) |
| `IDimension` | `Tempest.Core.UnitsAndQuantities` | Platform API (generic marker, no members) | Phantom-type dimension tag for `Quantity<TDimension>`/`Unit<TDimension>` — compile-time-only, never instantiated (`WP 7.1B`, `ADR-0054`) |
| `IDocumentRevision` | `Tempest.Core.EngineeringData` | Platform API (data contract) | One immutable, retrievable revision of an `IEngineeringDocument` (`WP 7.1A`, `ADR-0053`) |
| `IEngineeringDocument` | `Tempest.Core.EngineeringData` | Platform API (data contract) | Identity and current-revision pointer for a tracked engineering entity (`WP 7.1A`, `ADR-0053`) |
| `IEngineeringDocumentStore` | `Tempest.Core.EngineeringData` | DI-public | Create/find/revise/link/query engineering documents and their references (`WP 7.1A`, `ADR-0053`) |
| `IEvent` | `Tempest.Core.Events` | Platform API (contract) | Marks a published fact |
| `IEventBus` | `Tempest.Core.Events` | DI-public | Publish/subscribe dispatch (ADR-0020) |
| `IEventHandler<T>` | `Tempest.Core.Events` | Platform API (contract) | Consumer-facing subscription contract |
| `IExportFormat` | `Tempest.Core.ExportImport` | DI-public (via `AddInstance`) | Frames/reads the multi-section artifact envelope (`WP 6.7`) |
| `IExportPayloadSerializer` | `Tempest.Core.ExportImport` | Not DI-registered (optional collaborator, mirroring `IReportTemplate<T>`) | Converts a key/value data set to/from raw bytes |
| `IExportService` | `Tempest.Core.ExportImport` | DI-public | Exports one or more `IExportable` sources into a single artifact |
| `IExportable` | `Tempest.Core.ExportImport` | Platform API (contract) | Marks a source's data as exportable, round-trip-safe (ADR-0051) |
| `IExportableKind` | `Tempest.Core.ExportImport` | Platform API (optional companion contract) | Supplies a source's own stable artifact-section identifier |
| `IFrameworkDiscoveryService` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module Discovery |
| `IHostedService` | `Tempest.Core.BackgroundServices` | Platform API (contract) | Background service Start/Stop |
| `IHostedServiceDiscoveryService` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service discovery |
| `IHostedServiceManager` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service start/stop orchestration |
| `IIdentity` | `Tempest.Core.Identity` | Platform API (data contract) | The shape of a claimed identity (`WP 6.1`) |
| `IIdentityService` | `Tempest.Core.Identity` | DI-public | Establishes/resolves a principal; additive, not in the original catalogue (`WP 6.1`) |
| `IImportService` | `Tempest.Core.ExportImport` | DI-public (dual-registered under its own concrete type, mirroring `ICurrentPrincipalAccessor`) | Reads a previously exported artifact back into the owning service(s) |
| `IImportable` | `Tempest.Core.ExportImport` | Registered via `ImportService.RegisterImportable`, not itself a DI service type | Read-back counterpart to `IExportable`, routed to by `Kind` |
| `ILicense` | `Tempest.Core.Licensing` | Platform API (contract) | A single, validated, immutable license |
| `ILicenseProvider` | `Tempest.Core.Licensing` | DI-public (via `AddInstance`) | Read-only, post-validation view of the current license |
| `ILicenseValidator` | `Tempest.Core.Licensing` | Not DI-registered (Composition-Root-constructed, pre-container leaf, mirroring `IPlatformVersionProvider`) | Validates a license at Host startup, before the container exists |
| `ILogSink` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Log entry destination |
| `ILogger` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Structured logging abstraction |
| `ILoggerFactory` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Produces `ILogger` instances |
| `IMaterialCatalog` | `Tempest.Core.Materials` | DI-public | Register/find/revise/list named materials — a thin, typed index over `IEngineeringDocumentStore` (`WP 7.1C`, `ADR-0055`) |
| `IMaterialSpecification` | `Tempest.Core.Materials` | Platform API (data contract) | A registered material's own Id, name, category, and provenance-carrying properties (`WP 7.1C`, `ADR-0055`) |
| `IModule` | `Tempest.Core.Modules` | Discovered/registered, not DI-registered as an interface | Module identity contract |
| `IModuleLifecycle` | `Tempest.Core.Modules` | Discovered/registered | Module lifecycle contract |
| `IModuleLifecycleManager` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module lifecycle orchestration |
| `INavigationProvider` | `Tempest.Core.Navigation` | DI-public | Navigation registry + `Navigate` (ADR-0031/ADR-0032) |
| `INotification` | `Tempest.Core.Notifications` | Platform API (contract) | Marks a published notification (`WP 6.2`) |
| `INotificationDispatcher` | `Tempest.Core.Notifications` | DI-public | Subscribes and publishes notifications, isolating subscriber failures (`WP 6.2`) |
| `INotificationHandler<T>` | `Tempest.Core.Notifications` | Platform API (contract) | Consumer-facing subscription contract (`WP 6.2`) |
| `IPermissionEvaluator` | `Tempest.Core.Identity` | DI-public | The single authorization enforcement point (`WP 6.1`, ADR-0044) |
| `IPersistenceStore` | `Tempest.Core.Persistence` | DI-public | Internal, platform-owned key-value/document storage (`WP 6.4`, ADR-0041) |
| `IPlatformNotification` | `Tempest.Core.Notifications` | Platform API (additive general-purpose shape, extends `INotification` and `Events.IEvent`) | Severity/category-bearing general-purpose notification (`WP 6.2`) |
| `IPlatformVersionProvider` | `Tempest.Core.Versioning` | DI-public (via `AddInstance`) | Platform version query |
| `IPluginAssemblyLoader` | `Tempest.Core.Plugins` | Host-owned, never DI-public (ADR-0017 extended) | Plugin assembly loading |
| `IPluginManifestDiscoveryService` | `Tempest.Core.Plugins` | Host-owned, never DI-public (ADR-0017 extended) | Plugin manifest discovery |
| `IPrincipal` | `Tempest.Core.Identity` | Platform API (data contract) | The shape of an authenticated/established identity plus its roles (`WP 6.1`) |
| `IProjectRepository` | `Tempest.Core.Repositories` | Pre-module-pipeline, not part of the platform-service model | Project persistence (bootstrap-era) |
| `IReportDefinition` | `Tempest.Core.Reporting` | Platform API (contract) | Identifies a registrable report (`WP 6.0`) |
| `IReportRenderer<T>` | `Tempest.Core.Reporting` | Platform API (contract) | Produces a report definition's own output (`WP 6.0`) |
| `IReportTemplate<T>` | `Tempest.Core.Reporting` | Not DI-registered (optional collaborator, additive — `WP 6.0`) | Separates layout/rendering from a renderer's own data-gathering |
| `IReportingService` | `Tempest.Core.Reporting` | DI-public | Registers report definitions/renderers; dispatches generation by Id (`WP 6.0`) |
| `IRole` | `Tempest.Core.Identity` | Platform API (data contract, additive — `WP 6.1`) | A named grouping of permissions |
| `IRoleProvider` | `Tempest.Core.Identity` | DI-public (additive — `WP 6.1`) | Config-sourced role resolution |
| `IRuntimeModuleManager` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module registration catalogue |
| `IServiceCollection` | `Tempest.Core.DependencyInjection` | Composition-time only (not itself registered) | DI registration accumulation |
| `ISettingDefinition` | `Tempest.Core.Settings` | Platform API (data contract) | Identifies a registrable setting (`WP 6.4`) |
| `ISettingsChangedEvent` | `Tempest.Core.Settings` | Platform API (contract, an `IEvent`) | Published through the Event Bus on a setting value change (`WP 6.4`) |
| `ISettingsProvider` | `Tempest.Core.Settings` | DI-public | Reads/writes runtime-mutable setting values (`WP 6.4`) |
| `ITempestHost` | `Tempest.Core.Runtime` | Not DI-registered (returned by the builder) | The running Host instance |
| `ITempestHostBuilder` | `Tempest.Core.Runtime` | Not DI-registered (the composition root's own entry point) | Assembles and produces a `TempestHost` |
| `ITempestServiceProvider` | `Tempest.Core.DependencyInjection` | The container itself | Constructs and resolves service instances |
| `IUnitConverter` | `Tempest.Core.UnitsAndQuantities` | Not DI-registered (each `Unit<TDimension>` carries its own conversion factor; no registration/lookup service exists) | Reserved conversion-service contract; the framework's own actual conversion path is `Quantity<TDimension>.ConvertTo`, not this interface (`WP 7.1B`, `ADR-0054`) |
| `IVerificationRecord` | `Tempest.Core.Verification` | Platform API (data contract) | The complete, structured account of one recorded verification outcome (`WP 7.1E`, `ADR-0057`) |
| `IVerificationService` | `Tempest.Core.Verification` | DI-public | Records a verification outcome against a subject document; permission-gated history query (`WP 7.1E`, `ADR-0057`) |

**Total: 75 public interfaces under `src/Tempest.Core/` — Verified
directly (`grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core`
returns exactly 75 matches, matching the 75 rows above). 11 interfaces
introduced across the five Engineering Foundation Work Packages were
added in this pass, closing a gap that had persisted, undetected, since
each framework shipped: `WP 7.1A` (`IEngineeringDocument`,
`IDocumentRevision`, `IEngineeringDocumentStore` — 3), `WP 7.1B`
(`IDimension`, `IUnitConverter` — 2), `WP 7.1C` (`IMaterialCatalog`,
`IMaterialSpecification` — 2), `WP 7.1D` (`ICalculationDefinition<TInput,
TResult>`, `ICalculationEngine` — 2), `WP 7.1E` (`IVerificationRecord`,
`IVerificationService` — 2) — none of these five Work Packages' own
interfaces had ever been recorded here before this Work Package
(`WP 7.1F`), the same undetected-drift pattern `WP 6.8` found and closed
for `v0.6.0`'s own six Work Packages, now recurring and closed a second
time. Previously, `WP 6.8` fully backfilled: 23 interfaces introduced by
`WP 6.1` (`ICurrentPrincipalAccessor`, `IIdentity`, `IIdentityService`,
`IPermissionEvaluator`, `IPrincipal`, `IRole`, `IRoleProvider` — 7),
`WP 6.4` (`IPersistenceStore`, `ISettingDefinition`,
`ISettingsChangedEvent`, `ISettingsProvider` — 4), `WP 6.5`
(`IAuditQuery`, `IAuditRecord`, `IAuditRecorder` — 3), `WP 6.2`
(`INotification`, `INotificationDispatcher`, `INotificationHandler<T>`,
`IPlatformNotification` — 4), `WP 6.0` (`IReportDefinition`,
`IReportRenderer<T>`, `IReportTemplate<T>`, `IReportingService` — 4),
and `WP 6.3` (`IApiEndpointRegistry` — 1).**

## Classification Summary

**Reflects all 75 interfaces now listed above.**

| Classification | Count |
|---|---|
| DI-public (`AddInstance` or container-constructed singleton) | 29 |
| Host-owned, never DI-public (ADR-0017 and its extensions) | 7 |
| Platform API / contract only (no dispatcher or orchestration yet, consumer-facing marker, or data shape) | 26 |
| Discovered/registered but not itself a DI registration target | 3 |
| Composition-time / not-DI-registered infrastructure | 9 |
| Pre-module-pipeline, outside the platform-service model | 1 |

**Total: 29 + 7 + 26 + 3 + 9 + 1 = 75.** Four new DI-public rows
(`IEngineeringDocumentStore`, `IMaterialCatalog`, `ICalculationEngine`,
`IVerificationService`: 25 → 29); six new Platform API/contract rows
(`ICalculationDefinition<TInput, TResult>`, `IDimension`,
`IDocumentRevision`, `IEngineeringDocument`, `IMaterialSpecification`,
`IVerificationRecord`: 20 → 26); one new Composition-time/not-DI-registered
row (`IUnitConverter`, a reserved contract with no registration or lookup
service behind it: 8 → 9). Host-owned, Discovered/registered, and
Pre-module-pipeline counts are unchanged by the Engineering Foundation
programme — none of its five frameworks introduced a Host-owned
collaborator or a discovered-but-unregistered type.

## Cross-Reference Check

Every "Host-owned, never DI-public" interface above matches a row in
`docs/architecture/Ownership Matrix.md`; every "DI-public" interface
matches a registration row in `Dependency Injection Register.md`
(cross-checked directly against that register's own full backfill,
performed in this same Work Package). No discrepancy found between this
register's classification and either source document.

# Module Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Module Register |
| **Purpose** | The index of every real (non-test-fixture) module TempestOS ships — modules a consumer of the platform would actually encounter, as distinct from the many `IModule`/`ModuleBase` test fixtures that exist solely to exercise Discovery/Registration/Lifecycle in isolation. |
| **Scope** | Concrete classes implementing `IModule` (directly or via `ModuleBase`/`ModuleLifecycleBase`) under `src/`, excluding the SDK base classes themselves. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `src/Samples/Tempest.Samples/`; `docs/architecture/Sample Module Architecture.md`. |
| **Review Frequency** | Updated whenever a new production module is added anywhere under `src/`. |
| **Last Reviewed** | 2026-07-30 (WP 7.1F, Engineering Core Integration Review & Certification) — full backfill performed; four production modules added across the Engineering Foundation programme (`EngineeringDataSampleModule` `WP 7.1A`, `MaterialsSampleModule` `WP 7.1C`, `CalculationSampleModule` `WP 7.1D`, `VerificationSampleModule` `WP 7.1E`) had never been recorded here — stale since `WP 6.8`, closed by this Work Package's own certification review. `Tempest.Core.UnitsAndQuantities` (`WP 7.1B`) confirmed to have no sample module of its own, consistent with its own zero-DI-registration design. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every production module is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
| **Related Documents** | `docs/architecture/Sample Module Architecture.md`; `Platform Services Register.md`; `Event Catalogue.md`. |
| **Related ADRs** | ADR-0001 through ADR-0004 (module identity, lifecycle, disposal), ADR-0027 (`ModuleMetadataAttribute`), ADR-0036–ADR-0038, ADR-0040–ADR-0057. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/03-building-a-module.md`; `04-building-an-event-driven-module.md`; `docs/academy/03 Work Packages/WP4.3-sample-module-architecture.md`, `WP4.3-sample-module-implementation.md`, `WP4.4E-sample-module-event-integration.md`. |
| **Coverage Status** | **Complete.** Full backfill performed directly against `ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`, which enumerates every production module by direct assembly scan. |

---

## Entries

| Module | Namespace | Base Type | Uses `ModuleMetadataAttribute` | Constructor-Injects | Originating Work Package |
|---|---|---|---|---|---|
| `ClockModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IEventBus` | WP 4.3 (created), WP 4.4E (extended to publish events) |
| `ClockLifecycleObserverModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IEventBus` | WP 4.4E |
| `NavigationSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 5.0B |
| `SecondaryNavigationSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 5.0B |
| `DuplicateNavigationSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 5.0B |
| `CommandSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `ICommandDispatcher`, `ICommandRegistry`, `INavigationProvider` | WP 5.1B |
| `DiagnosticsSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IDiagnosticsProvider`, `ICommandDispatcher`, `ICommandRegistry` | WP 5.2 |
| `IdentitySampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `ICommandDispatcher`, `ICommandRegistry` | WP 6.1 |
| `SettingsSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `ISettingsProvider`, `IEventBus`, `ICommandDispatcher`, `ICommandRegistry` | WP 6.4 |
| `AuditSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `IAuditRecorder`, `IAuditQuery`, `ICommandDispatcher`, `ICommandRegistry` | WP 6.5 |
| `NotificationSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INotificationDispatcher`, `ICommandDispatcher`, `ICommandRegistry` | WP 6.2 |
| `ReportingSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `IReportingService`, `ISettingsProvider`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `IAuditRecorder`, `INotificationDispatcher`, `ICommandDispatcher`, `ICommandRegistry` | WP 6.0 |
| `ApiSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IApiEndpointRegistry` | WP 6.3 |
| `ExportImportSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `ISettingsProvider`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `IAuditRecorder`, `INotificationDispatcher`, `IExportService`, `ImportService` (concrete type), `ICommandDispatcher`, `ICommandRegistry` | WP 6.7 |
| `LicensingSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `ISettingsProvider`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `ILicenseProvider`, `IAuditRecorder`, `INotificationDispatcher`, `IApiEndpointRegistry`, `ICommandDispatcher`, `ICommandRegistry` | WP 6.6 |
| `EngineeringDataSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IEngineeringDocumentStore`, `ICommandDispatcher`, `ICommandRegistry` | WP 7.1A |
| `MaterialsSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IMaterialCatalog`, `ICommandDispatcher`, `ICommandRegistry` | WP 7.1C |
| `CalculationSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `ICalculationEngine`, `ICommandDispatcher`, `ICommandRegistry` | WP 7.1D |
| `VerificationSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IEngineeringDocumentStore`, `IVerificationService`, `ICommandDispatcher`, `ICommandRegistry` | WP 7.1E |

**Total: 19 production modules — Verified directly via
`ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`,
which asserts exactly 19 and names each by Id and type; all 19 are
listed above. Four modules were added in this pass, closing a gap that
had persisted, undetected, since each shipped: `EngineeringDataSampleModule`
(`WP 7.1A`), `MaterialsSampleModule` (`WP 7.1C`), `CalculationSampleModule`
(`WP 7.1D`), `VerificationSampleModule` (`WP 7.1E`) — none of these four
had ever been recorded here before this Work Package (`WP 7.1F`), the
same undetected-drift pattern `WP 6.8` found and closed for `v0.6.0`'s
own six modules, now recurring and closed a second time.
`Tempest.Core.UnitsAndQuantities` (`WP 7.1B`) has no sample module of its
own — consistent with its own zero-DI-registration design; nothing to
demonstrate through a module, since every consumer uses
`Quantity<TDimension>`/`Unit<TDimension>` directly as ordinary value
types, exercised instead through `PlatformIntegrationTests.cs`. Previously
fully backfilled by `WP 6.8`: `IdentitySampleModule` (`WP 6.1`),
`SettingsSampleModule` (`WP 6.4`), `AuditSampleModule` (`WP 6.5`),
`NotificationSampleModule` (`WP 6.2`), `ReportingSampleModule`
(`WP 6.0`), and `ApiSampleModule` (`WP 6.3`).**

## SDK Base Types (Not Modules Themselves)

| Type | Namespace | Role |
|---|---|---|
| `ModuleBase` | `Tempest.Core.Modules` | Identity only (`Id`/`Name`/`Version` via constructor) for a module with no lifecycle |
| `ModuleLifecycleBase` | `Tempest.Core.Modules` | Extends `ModuleBase` with four `virtual`, no-op-by-default lifecycle methods |

Both are abstract, introduced by WP 4.1 (Module SDK) — see
`Platform Services Register.md`'s "Module SDK" entry.

## Test-Only Module Fixtures (Out of Scope, Noted for Completeness)

Additional concrete `IModule`/`ModuleBase` implementations exist under
`tests/Tempest.Core.Tests/` — these are deliberately excluded from this
register's own count because they exist solely to exercise Discovery/
Registration/Lifecycle in isolation (healthy modules, a duplicate-ID
module, a blocking module, a disposal-tracking module, and similar),
never shipped or discoverable outside the test assembly. Full detail is
tracked by `Test Register.md`, not duplicated here.

## Cross-Reference Check

All 15 production modules are cited by name in `Platform Services
Register.md` and at least one Work Package retrospective each —
confirmed directly against `docs/academy/03 Work Packages/` for every
module above. `Platform Service Map.md`'s own per-service "Consumers"
section names each module as its own service's real contributor.
`ClockModule`/`ClockLifecycleObserverModule` publish/subscribe
`ClockModuleLifecycleEvent` (`Event Catalogue.md`); the three Navigation
sample modules are `NavigationRequestedEvent`'s real contributors —
`CommandSampleModule` also publishes it indirectly, via
`NavigateToSampleHomeCommandHandler`'s own call to
`INavigationProvider.Navigate`, not as a direct contributor of its own.
`DiagnosticsSampleModule` and `ApiSampleModule` each publish no event of
their own — they demonstrate their own named service and the Command
Framework interacting, not the Event Bus. No production module exists
that is not also covered by at least one test in `Test Register.md`.

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
| **Last Reviewed** | 2026-07-29 (WP 6.7, Export/Import) — added `ExportImportSampleModule`; see the disclosed gap under Coverage Status. |
| **Related Documents** | `docs/architecture/Sample Module Architecture.md`; `Platform Services Register.md`; `Event Catalogue.md`. |
| **Related ADRs** | ADR-0001 through ADR-0004 (module identity, lifecycle, disposal), ADR-0027 (`ModuleMetadataAttribute`), ADR-0036–ADR-0038, ADR-0051. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/03-building-a-module.md`; `04-building-an-event-driven-module.md`; `docs/academy/03 Work Packages/WP4.3-sample-module-architecture.md`, `WP4.3-sample-module-implementation.md`, `WP4.4E-sample-module-event-integration.md`. |
| **Coverage Status** | **Partial — a genuine, disclosed gap found during `WP 6.7`'s own repository review, not introduced by this Work Package.** This register's own `Last Reviewed` line read `WP 5.2` before this Work Package touched it — `IdentitySampleModule` (`WP 6.1`), `SettingsSampleModule`/`AuditSampleModule` (`WP 6.4`/`WP 6.5`), `NotificationSampleModule` (`WP 6.2`), `ReportingSampleModule` (`WP 6.0`), and `ApiSampleModule` (`WP 6.3`) were all missing entirely (`ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule` confirms 14 production modules actually exist; only 7 were previously listed, plus this Work Package's own 1 new one = 8, leaving 6 still undocumented). `WP 6.7` adds only its own new module, correctly described, rather than retroactively backfilling six unrelated Work Packages' worth of rows under its own scope — a full backfill is recommended as `WP 6.8` (Platform Services Integration Review)'s own closing-audit task. |

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
| `ExportImportSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `ISettingsProvider`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `IAuditRecorder`, `INotificationDispatcher`, `IExportService`, `ImportService` (concrete type), `ICommandDispatcher`, `ICommandRegistry` | WP 6.7 |

**Total: 14 production modules actually exist (Verified directly via
`ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`)
— only 8 are listed above (the 7 previously listed + this Work Package's
own new one); the remaining 6, added by `WP 6.1`/`WP 6.4`/`WP 6.5`/`WP
6.2`/`WP 6.0`/`WP 6.3`, are the disclosed gap under Coverage Status,
left for `WP 6.8`'s own backfill rather than retrofitted here under a
different Work Package's own scope.**

## SDK Base Types (Not Modules Themselves)

| Type | Namespace | Role |
|---|---|---|
| `ModuleBase` | `Tempest.Core.Modules` | Identity only (`Id`/`Name`/`Version` via constructor) for a module with no lifecycle |
| `ModuleLifecycleBase` | `Tempest.Core.Modules` | Extends `ModuleBase` with four `virtual`, no-op-by-default lifecycle methods |

Both are abstract, introduced by WP 4.1 (Module SDK) — see
`Platform Services Register.md`'s "Module SDK" entry.

## Test-Only Module Fixtures (Out of Scope, Noted for Completeness)

Six additional concrete `IModule`/`ModuleBase` implementations exist under
`tests/Tempest.Core.Tests/` (**Verified** by direct grep) — these are
deliberately excluded from this register's own count because they exist
solely to exercise Discovery/Registration/Lifecycle in isolation (healthy
modules, a duplicate-ID module, a blocking module, a disposal-tracking
module, and similar), never shipped or discoverable outside the test
assembly. Full detail is tracked by `Test Register.md`, not duplicated
here.

## Cross-Reference Check

All seven production modules are cited by name in `Platform Services
Register.md` (Event Bus's "first real consumer"; Navigation's real
contributors; Command Framework's real contributor; Diagnostics' real
consumer), `Event Catalogue.md`
(`ClockModule`/`ClockLifecycleObserverModule` as publisher/subscriber of
`ClockModuleLifecycleEvent`; the three Navigation sample modules as
`NavigationRequestedEvent`'s real contributors — `CommandSampleModule`
also publishes `NavigationRequestedEvent` indirectly, via
`NavigateToSampleHomeCommandHandler`'s own call to `INavigationProvider.
Navigate`, not as a direct contributor of its own), and at least one Work
Package retrospective each. `DiagnosticsSampleModule` publishes no event
of its own — it demonstrates `IDiagnosticsProvider` and the Command
Framework interacting, not the Event Bus. No production module exists
that is not also covered by at least one test in `Test Register.md`.

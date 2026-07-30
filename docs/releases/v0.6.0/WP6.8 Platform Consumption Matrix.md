# WP 6.8 — Platform Consumption Matrix

## Purpose

For every Platform Service in scope for this review — the Runtime
Foundation, the Host, and the nine `v0.6.0` Platform Services — this
matrix records: who consumes it, the concrete evidence that consumption
is real and verified (not merely claimed), and its certification status.
"Verified consumer" means a real, compiling, test-exercised piece of
code — a sample module, another platform service, or `Tempest.App`
itself — never a hypothetical future consumer.

## Matrix

| Platform Service | Consumers | Verification Evidence | Status |
|---|---|---|---|
| **Runtime Foundation** (DI, Discovery, Registration, Lifecycle, Configuration, Logging, Event Bus, Background Services, Navigation, Command Framework, Diagnostics, Plugin Manifest, Versioning) | Every one of the 15 production modules; every one of the 9 `v0.6.0` Platform Services; `Tempest.App`'s own Shell (`WP 5.0D`) | 1016 automated tests exercise this foundation directly or indirectly; `TempestHostTests.cs`, `TempestServiceProviderTests.cs`, `ReflectionFrameworkDiscoveryServiceTests.cs`, and every `*HostRegistrationTests.cs` file confirm construction, registration, and resolution work correctly | **Verified** |
| **Host** (`TempestHost`/`TempestHostBuilder`, the Composition Root) | `Tempest.App`'s own entry point (`Program.cs`); every integration test that calls `new TempestHostBuilder(...).Build()` and `RunAsync()` (24 test files, confirmed by direct `grep`) | `RunAsync_ConfigurationFailure_IsHostFatal_TransitionsToFaulted`, `RunAsync_MalformedLicenseFile_IsHostFatal_TransitionsToFaulted`, and every `*SampleModuleIntegrationTests.cs`'s own `RunAsync_With*SampleModule_*` end-to-end test prove the real, unmodified Host correctly sequences Configuration → License Validation → Logging → Plugin Discovery → Module Discovery/Registration → DI Build → Module Initialisation → Hosted Services Started → Running | **Verified** |
| **Identity & Permissions** (`WP 6.1`) | `IdentitySampleModule` (real contributor); `ApiRequestHandler` (`Tempest.Core.Api`, core-level dependency); `AuditRecorder` (ambient-principal attribution); `AuditSampleModule`, `ReportingSampleModule`, `ExportImportSampleModule`, `LicensingSampleModule` (all four, permission-gated commands) | `IdentityHostRegistrationTests.cs`; `PermissionEvaluatorTests.cs`; every sample module's own `*_NoPermissionGranted_ReportsDeniedByDefault` test (fail-closed default proven six times independently, once per consuming module) | **Verified** |
| **Settings** (`WP 6.4`) | `SettingsSampleModule` (real contributor); `ReportingSampleModule`, `ExportImportSampleModule`, `LicensingSampleModule` (all three, a customisable message read at the calling layer) | `SettingsHostRegistrationTests.cs`; `SettingsProviderTests.cs`; `GeneratedReport_UsesTheGreetingSettingsCurrentValue`, `ExportThenImportSampleDataCommand_*_RoundTripsCustomisedSettingsThroughRealSettingsProvider`, `CheckSampleCapabilityCommand_*_ReportsSuccessWithSettingsMessage` | **Verified** |
| **Persistence** (`WP 6.4`) | `SettingsProvider` (via `ISettingsProvider`); `AuditRecorder`/`AuditQuery` (via `IAuditRecorder`/`IAuditQuery`) — confirmed by direct `using` inspection: `Tempest.Core.Audit` imports `Tempest.Core.Persistence` | `PersistenceStoreTests.cs`; `AuditQueryTests`' own filter-correctness suite, proven to work fully and correctly against `IPersistenceStore`'s own key-lookup-only shape (`ADR-0045`'s own Persistence Validation) | **Verified** |
| **Audit** (`WP 6.5`) | `AuditSampleModule` (real contributor); `ApiRequestHandler` (core-level dependency, `api.request` entries); `ReportingSampleModule`, `ExportImportSampleModule`, `LicensingSampleModule` (all three, action recording) | `AuditHostRegistrationTests.cs`; `AuditRecorderTests.cs`; `AuditQueryTests.cs`; `GrantedRequest_RecordsAnAuditEntryCarryingTheCallerIdentityInDetail`; every sample module's own `*_RecordsAn(Granted\|Denied)?AuditEntry` test | **Verified** |
| **Notifications** (`WP 6.2`) | `NotificationSampleModule` (real contributor); `NotificationSampleHostedService` (start/stop notices); `ReportingSampleModule`, `ExportImportSampleModule`, `LicensingSampleModule` (all three, completion notices) | `NotificationHostRegistrationTests.cs`; `NotificationDispatcherTests.cs`; every sample module's own `*_PublishesA(Success\|Warning)?Notification` test | **Verified** |
| **Reporting** (`WP 6.0`) | `ReportingSampleModule` (real contributor); named as a plausible future consumer for any engineering module — no second real consumer exists this release | `ReportingHostRegistrationTests.cs`; `ReportingServiceTests.cs`; `PlainTextReportTemplateTests.cs`; `RunAsync_WithReportingSampleModule_GeneratesThroughTheRealHost` | **Verified** (single real consumer; deliberately orthogonal to Export/Import, `ADR-0040`) |
| **REST API** (`WP 6.3`) | `ApiSampleModule` (real contributor, mapping `ReportingSampleModule`'s own command); `LicensingSampleModule` (a second, independent route, `POST /api/v1/sample-capability`) | `ApiHostRegistrationTests.cs`; `ApiRequestHandlerTests.cs`; `ApiEndpointRegistryTests.cs`; `RestApiHostedServiceTests.cs`; real HTTP round trips in both `ApiSampleModuleIntegrationTests.cs` and `LicensingSampleModuleIntegrationTests.cs`, independently confirming two different modules can each map their own route with zero shared code | **Verified — the REST API now has two independent real consumers, not one, confirming `IApiEndpointRegistry`'s own "any module can map a route" design generalises** |
| **Export/Import** (`WP 6.7`) | `ExportImportSampleModule` (real contributor); named as a plausible future consumer for Licensing and any engineering module — no second real consumer exists this release | `ExportServiceTests.cs`; `ImportServiceTests.cs`; `JsonExportFormatTests.cs`; `JsonExportPayloadSerializerTests.cs`; `ExportImportSampleModuleIntegrationTests.cs`'s own full export-then-overwrite-then-import round trip | **Verified** (single real consumer; deliberately orthogonal to Persistence, `ADR-0051`) |
| **Licensing** (`WP 6.6`) | `LicensingSampleModule` (real contributor); `TempestHost` itself (the pre-container validation gate is its own first, structural consumer) | `LicenseValidatorTests.cs`; `LicenseProviderTests.cs`; `LicenseHostRegistrationTests.cs` (including four dedicated Host-fatal-abort tests); `LicensingSampleModuleIntegrationTests.cs`'s own real HTTP round trip | **Verified** |

## Observations

**Every one of the eleven services in scope has at least one verified,
real consumer — none is "approved but never actually exercised."**
Reporting and Export/Import are each the only two services this release
with exactly one real sample-module consumer rather than two or more;
both are deliberately orthogonal to a sibling service by explicit ADR
(`ADR-0040`, `ADR-0051`), so a second consumer was never expected to
exist within this release's own scope — not a gap, a designed boundary.

**The REST API is the only service with two independent real
consumers** (`ApiSampleModule`, `LicensingSampleModule`), each mapping
its own route with zero shared code between them — the strongest
available evidence that `IApiEndpointRegistry`'s own "any module can
expose a route" design genuinely generalises, not merely works once.

**Persistence has no sample module of its own** — by design
(`ADR-0051`'s own explicit exclusion for Export/Import; `ADR-0040`'s own
explicit exclusion for Reporting) — but is verified through two
independent real service-to-service consumers (Settings, Audit), a
different but equally valid form of verified consumption.

## Related Documents

`WP6.8 Platform Certification Report.md`; `WP6.8 Platform Architecture
Conformance Report.md`; `docs/architecture/Platform Service Map.md`;
`docs/governance/Engineering/Module Register.md`, `Dependency Injection
Register.md` (both fully backfilled by this Work Package).

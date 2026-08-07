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
| **Last Reviewed** | 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — Second Pass) — reviewed, zero new module added: 34 production modules re-verified directly a second time via `ClockModuleDiscoveryTests`, unchanged since the first pass — `WP 9.8B` (documentation-only) introduced no module. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — First Pass) — reviewed, zero new module added: verification-only Work Package. All 34 production modules re-verified directly via `ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule` (part of the 2026/2026 passing suite), 34 total unchanged — see `WP9.9.0 Release Readiness Report.md` §14 (Module Inventory). Previously reviewed 2026-08-07 (WP 9.5A, Manufacturing Workspace) — `ManufacturingWorkspaceExplorerModule`/`EngineeringManufacturingWorkspaceSampleModule` added directly at implementation time, not backfilled later; 32 → 34 production modules. The fifth sample module (`EngineeringManufacturingWorkspaceSampleModule`) to constructor-inject other sample modules' own concrete types — this time four at once (`MechanicalProductStructureSampleModule`, `RequirementsWorkspaceSampleModule`, `EngineeringCalculationsWorkspaceSampleModule`, `EngineeringDocumentsWorkspaceSampleModule`), disclosed directly; it additionally reads (never constructor-injects) the base `EngineeringDomainSampleModule`'s own already-live `"Supplier"` Domain object by Kind query, mirroring `WP 9.4A`'s/`WP 9.3A`'s own identical further edge — see `WP9.5A Architecture Conformance Review.md`. Deliberately does not depend on `EngineeringVerificationWorkspaceSampleModule` — checked directly, its own module id sorts after this module's own, so such a dependency would have been a genuine ordering defect, not merely unneeded. This Work Package's own controlling instruction skips `WP 9.6A`–`WP 9.8A` and moves directly to `WP 9.9.0` Release Preparation — recorded as a plain observation, per `PROJECT_STATUS.md`. Previously reviewed 2026-08-07 (WP 9.3A, Verification Management Workspace) — `VerificationWorkspaceExplorerModule`/`EngineeringVerificationWorkspaceSampleModule` added directly at implementation time, not backfilled later; 30 → 32 production modules. The fourth sample module (`EngineeringVerificationWorkspaceSampleModule`) to constructor-inject other sample modules' own concrete types — this time four at once (`MechanicalProductStructureSampleModule`, `RequirementsWorkspaceSampleModule`, `EngineeringCalculationsWorkspaceSampleModule`, `EngineeringDocumentsWorkspaceSampleModule`), disclosed directly; it additionally reads (never constructor-injects) the base `EngineeringDomainSampleModule`'s own already-live `"Risk"` Domain object by Kind query, mirroring `WP 9.4A`'s own identical fifth edge — see `WP9.3A Architecture Conformance Review.md`. Closes the disclosed `WP 9.3A` numbering gap; completed, in real time, after `WP 9.4A` despite its own earlier number. Previously reviewed 2026-08-06 (WP 9.4A, Engineering Documents Workspace) — `DocumentsWorkspaceExplorerModule`/`EngineeringDocumentsWorkspaceSampleModule` added directly at implementation time, not backfilled later; 28 → 30 production modules. The third sample module (`EngineeringDocumentsWorkspaceSampleModule`) to constructor-inject other sample modules' own concrete types — this time three at once (`MechanicalProductStructureSampleModule`, `RequirementsWorkspaceSampleModule`, `EngineeringCalculationsWorkspaceSampleModule`), disclosed directly; it additionally reads (never constructor-injects) the base `EngineeringDomainSampleModule`'s own already-live `"Risk"` Domain object by Kind query, a deliberately looser fourth edge — see `WP9.4A Architecture Conformance Review.md`. Previously reviewed 2026-08-05 (WP 9.2A, Engineering Calculations Workspace) — `CalculationsWorkspaceExplorerModule`/`EngineeringCalculationsWorkspaceSampleModule` added directly at implementation time, not backfilled later; 26 → 28 production modules. The second sample module (`EngineeringCalculationsWorkspaceSampleModule`) to constructor-inject other sample modules' own concrete types — this time two at once (`MechanicalProductStructureSampleModule`, `RequirementsWorkspaceSampleModule`), disclosed directly. Previously reviewed 2026-08-05 (WP 9.1A, Requirements Management Workspace) — `RequirementsWorkspaceExplorerModule`/`RequirementsWorkspaceSampleModule` added directly at implementation time, not backfilled later; 24 → 26 production modules. The first sample module (`RequirementsWorkspaceSampleModule`) ever to constructor-inject another sample module's own concrete type (`MechanicalProductStructureSampleModule`), disclosed directly. Previously reviewed 2026-08-05 (WP 9.0B, Product Configuration & BOM Management) — reviewed, no new module added: `MechanicalProductStructureSampleModule` (`WP 9.0A`) was extended in place with real BOM lines, a Baseline, a Release, and validation rule registration, rather than a second sample module being introduced; 24 production modules, unchanged. Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — `MechanicalWorkspaceExplorerModule`/`MechanicalProductStructureSampleModule` added directly at implementation time, not backfilled later; 22 → 24 production modules. Previously reviewed 2026-08-04 (WP 8.2C, Engineering Domain Implementation) — `EngineeringDomainSampleModule` added directly at implementation time, not backfilled later; 21 → 22 production modules. Previously reviewed 2026-08-04 (WP 8.1B, Navigation & Project Explorer) — `WorkspaceExplorerSampleModule` added directly at implementation time, not backfilled later; 20 → 21 production modules. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — `RequirementsSampleModule` added directly at implementation time, not backfilled later. Previously reviewed 2026-07-30 (WP 7.1F, Engineering Core Integration Review & Certification) — full backfill performed; four production modules added across the Engineering Foundation programme (`EngineeringDataSampleModule` `WP 7.1A`, `MaterialsSampleModule` `WP 7.1C`, `CalculationSampleModule` `WP 7.1D`, `VerificationSampleModule` `WP 7.1E`) had never been recorded here — stale since `WP 6.8`, closed by this Work Package's own certification review. `Tempest.Core.UnitsAndQuantities` (`WP 7.1B`) confirmed to have no sample module of its own, consistent with its own zero-DI-registration design. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every production module is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
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
| `RequirementsSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `IRequirementsService`, `IEngineeringDocumentStore`, `IVerificationService`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `IAuditRecorder`, `IReportingService`, `ImportService` (concrete type), `ICommandDispatcher`, `ICommandRegistry` | WP 7.3A |
| `WorkspaceExplorerSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 8.1B |
| `EngineeringDomainSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `EngineeringDomainContext`, `IMaterialCatalog`, `IDependencyTraversal`, `ICommandDispatcher`, `ICommandRegistry` | WP 8.2C |
| `MechanicalWorkspaceExplorerModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 9.0A |
| `MechanicalProductStructureSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `EngineeringDomainContext` | WP 9.0A |
| `RequirementsWorkspaceExplorerModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 9.1A |
| `RequirementsWorkspaceSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `IRequirementsService`, `IVerificationService`, `ImportService` (concrete type), `MechanicalProductStructureSampleModule` (concrete type — see Notes) | WP 9.1A |
| `CalculationsWorkspaceExplorerModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 9.2A |
| `EngineeringCalculationsWorkspaceSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `EngineeringDomainContext`, `ICalculationEngine`, `IRequirementsService`, `MechanicalProductStructureSampleModule` (concrete type), `RequirementsWorkspaceSampleModule` (concrete type — see Notes) | WP 9.2A |
| `DocumentsWorkspaceExplorerModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 9.4A |
| `EngineeringDocumentsWorkspaceSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `EngineeringDomainContext`, `MechanicalProductStructureSampleModule` (concrete type), `RequirementsWorkspaceSampleModule` (concrete type), `EngineeringCalculationsWorkspaceSampleModule` (concrete type — see Notes) | WP 9.4A |
| `VerificationWorkspaceExplorerModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 9.3A |
| `EngineeringVerificationWorkspaceSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `EngineeringDomainContext`, `IVerificationService`, `IRequirementsService`, `MechanicalProductStructureSampleModule` (concrete type), `RequirementsWorkspaceSampleModule` (concrete type), `EngineeringCalculationsWorkspaceSampleModule` (concrete type), `EngineeringDocumentsWorkspaceSampleModule` (concrete type — see Notes) | WP 9.3A |
| `ManufacturingWorkspaceExplorerModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `INavigationProvider` | WP 9.5A |
| `EngineeringManufacturingWorkspaceSampleModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IIdentityService`, `EngineeringDomainContext`, `IVerificationService`, `MechanicalProductStructureSampleModule` (concrete type), `RequirementsWorkspaceSampleModule` (concrete type), `EngineeringCalculationsWorkspaceSampleModule` (concrete type), `EngineeringDocumentsWorkspaceSampleModule` (concrete type — see Notes) | WP 9.5A |

**Total: 34 production modules — Verified directly via
`ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`,
which asserts exactly 34 and names each by Id and type; all 34 are
listed above. `MechanicalProductStructureSampleModule` (`WP 9.0A`) was
recorded directly at implementation time, not backfilled later — it
builds the Mechanical Product Structure's own representative object
graph (a Project, two Assemblies, a three-level-deep Sub-Assembly chain,
five Parts, one shared Component, one Configuration), and exercises
`RenameAsync`/`MoveAsync`/`DeleteAsync` directly during seeding.
`MechanicalWorkspaceExplorerModule` (`WP 9.0A`) was recorded directly at
implementation time, not backfilled later — it registers only a
`NavigationItem`, mirroring `WorkspaceExplorerSampleModule`'s own
identical shape exactly, for the identical reason (`ADR-0071`).
`RequirementsWorkspaceExplorerModule` (`WP 9.1A`) was recorded directly
at implementation time, not backfilled later — it registers only a
`NavigationItem`, the identical shape a third time. `RequirementsWorkspaceSampleModule`
(`WP 9.1A`) was recorded directly at implementation time, not backfilled
later — it builds a three-level Group hierarchy, ten Requirements across
six lifecycle statuses, two Collections, and real cross-discipline
allocations to `MechanicalProductStructureSampleModule`'s own live Wing
Assembly/Spar Web Plate. **A disclosed, genuine first for this register:**
its own constructor dependency on `MechanicalProductStructureSampleModule`
(the concrete sample module type, not an interface) is the first instance
of one sample module depending on another sample module's own instance —
safe because every discovered module type is registered as a DI singleton
(`ModuleServiceCollectionExtensions.AddDiscoveredModules`) and
`ModuleLifecycleManager` initialises modules in ordinal Id order
(`tempest.samples.mechanicalproductstructure` sorts before
`tempest.samples.requirementsworkspace`), confirmed directly, not
assumed. See `ADR-0084`'s Related Documents and `WP9.1A Architecture
Conformance Review.md` §2.
`CalculationsWorkspaceExplorerModule` (`WP 9.2A`) was recorded directly
at implementation time, not backfilled later — it registers only a
`NavigationItem`, the identical shape a fourth time.
`EngineeringCalculationsWorkspaceSampleModule` (`WP 9.2A`) was recorded
directly at implementation time, not backfilled later — it registers
five representative `ICalculationDefinition`s with `ICalculationEngine`,
builds five real `Calculation`/one `CalculationSet` Domain objects, and
exercises real cross-discipline Digital Thread links. **A second
instance of one sample module depending on another sample module's own
concrete instance**, extending `WP 9.1A`'s own first such precedent to
two dependencies at once (`MechanicalProductStructureSampleModule` and
`RequirementsWorkspaceSampleModule`) — safe for the identical reason,
confirmed directly: `tempest.samples.mechanicalproductstructure`, then
`tempest.samples.requirementsworkspace`, then this module's own
`tempest.samples.workspacecalculations` sort in exactly that ordinal
order. See `ADR-0086`'s Related Documents and `WP9.2A Architecture
Conformance Review.md` §2.
`EngineeringDomainSampleModule` (`WP 8.2C`) was recorded
directly at implementation time, not backfilled later — it builds a
sixteen-object representative Engineering Domain graph and registers one
command demonstrating `IDependencyTraversal`. `WorkspaceExplorerSampleModule` (`WP 8.1B`) was recorded
directly at implementation time, not backfilled later — it registers
only a `NavigationItem`; the Project Explorer/View content it names is
registered separately, by `Tempest.App`'s own composition root
(`Program.cs`), not by this module itself (`ADR-0071`, since a module
has no path to reach `IWorkspaceManager` directly). `RequirementsSampleModule`
(`WP 7.3A`) was likewise recorded directly at implementation time — the
first module added since `WP 7.1F` established the practice of keeping
this register current with implementation. Four modules were added in a
prior pass, closing a gap that had persisted, undetected, since each
shipped: `EngineeringDataSampleModule`
(`WP 7.1A`), `MaterialsSampleModule` (`WP 7.1C`), `CalculationSampleModule`
(`WP 7.1D`), `VerificationSampleModule` (`WP 7.1E`) — none of these four
had ever been recorded here before that Work Package (`WP 7.1F`), the
same undetected-drift pattern `WP 6.8` found and closed for `v0.6.0`'s
own six modules, recurring and closed a second time.
`Tempest.Core.UnitsAndQuantities` (`WP 7.1B`) has no sample module of its
own — consistent with its own zero-DI-registration design; nothing to
demonstrate through a module, since every consumer uses
`Quantity<TDimension>`/`Unit<TDimension>` directly as ordinary value
types, exercised instead through `PlatformIntegrationTests.cs`. Previously
fully backfilled by `WP 6.8`: `IdentitySampleModule` (`WP 6.1`),
`SettingsSampleModule` (`WP 6.4`), `AuditSampleModule` (`WP 6.5`),
`NotificationSampleModule` (`WP 6.2`), `ReportingSampleModule`
(`WP 6.0`), and `ApiSampleModule` (`WP 6.3`).**
`ManufacturingWorkspaceExplorerModule` (`WP 9.5A`) was recorded directly
at implementation time, not backfilled later — it registers only a
`NavigationItem`, the identical shape a sixth time.
`EngineeringManufacturingWorkspaceSampleModule` (`WP 9.5A`) was recorded
directly at implementation time, not backfilled later — it builds one
real Routing (three sequenced Operation steps), one Supplier Operation,
one Tooling and one Fixture Document, one Work Instruction, and one
recorded-`Pass` Inspection, and exercises real cross-discipline Digital
Thread links. **A fifth instance of one sample module depending on other
sample modules' own concrete instances**, constructor-injecting four at
once (`MechanicalProductStructureSampleModule`,
`RequirementsWorkspaceSampleModule`,
`EngineeringCalculationsWorkspaceSampleModule`,
`EngineeringDocumentsWorkspaceSampleModule`) — the same four
`EngineeringDocumentsWorkspaceSampleModule` itself already establishes,
extended by none. **Deliberately does not depend on
`EngineeringVerificationWorkspaceSampleModule`** — checked directly:
`tempest.samples.workspaceverification` sorts *after*
`tempest.samples.workspacemanufacturing` ordinally, so such a dependency
would have been a genuine `ModuleLifecycleManager` initialisation-order
defect, not merely an unneeded one. It additionally reads (never
constructor-injects) the base `EngineeringDomainSampleModule`'s own
already-live `"Supplier"` Domain object by Kind query, mirroring
`WP 9.4A`'s/`WP 9.3A`'s own identical further edge — see `WP9.5A
Architecture Conformance Review.md` §2.

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

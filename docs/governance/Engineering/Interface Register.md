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
| **Last Reviewed** | 2026-08-10 (WP 10.5C, Commercial User Experience & Application Completion) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop`/`Tempest.App.Workspace` (`DisciplineColors`, a new `internal static class`, not an interface; `ProjectExplorerNode`/`CockpitKpiCard`, existing records gaining only additive, defaulted trailing parameters; `CockpitCardControl`/`ProjectExplorerView`/`PropertyInspectorView`/`RibbonView`/`CockpitView`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly. `v0.10.0`'s own thirteenth Work Package by completion order. Previously reviewed 2026-08-10 (WP 10.6A, Command Execution & Productivity Experience) — 5 new interfaces added directly at implementation time, interleaved alphabetically into the existing table: `ICommandMacro`/`IMacroManager` (`Tempest.Core.Macros`, `ADR-0099`), `IInputBindingProvider`/`IExternalControllerProvider`/`IInputBindingRegistry` (`Tempest.Core.Input`, `ADR-0100`); 168 → 173 total. `IUndoRedoStack` (`Tempest.App.Workspace`) and `IBackgroundTaskRunner` (`Tempest.Desktop.Tasks`) are both out of this register's own scope (`src/Tempest.Core/` only) — the identical, already-established boundary `WP 9.0A`/`WP 10.2A`/`WP 10.3A` each already applied to `IPropertyFacetProvider`/`IWorkspaceManager`. `v0.10.0`'s own twelfth Work Package. Previously reviewed 2026-08-10 (WP 10.5B, Desktop Workflow & Professional Interaction) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop` (`InputDialog`/`MessageDialog`/`SettingsDialog`/`PlatformNotificationToastBridge`, new classes over already-existing patterns — `PlatformNotificationToastBridge` implements the already-existing `IEventHandler<IPlatformNotification>`, introducing no new interface of its own; `UserSettings`/`WindowUiState`, new top-level classes over the already-existing `ISettingsProvider` pattern; `RibbonView`/`ProjectExplorerView`/`DocumentAreaView`/`MainWindow`/`App`/`ToastHost`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly. `v0.10.0`'s own eleventh Work Package. Previously reviewed 2026-08-10 (WP 10.5A, Workspace Visual Polish & Engineering User Experience) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop` (`ApplicationPalette`/`ThemeReactiveBrush`/`SeverityColors`/`IconGeometry`/`ToastHost`/`BusyOverlay`/`ConfirmationDialog`/`EmptyStateView`, all new classes over already-existing patterns; `PanelHostControl`/`CommandPaletteOverlay`/`DigitalThreadGraphView`/`IconRegistry`/`DocumentAreaView`/`ObjectEditorView`/`MainWindow`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly. `v0.10.0`'s own tenth Work Package. Previously reviewed 2026-08-09 (WP 10.4A, Digital Thread Visualisation) — reviewed, zero new interfaces added: `DigitalThreadGraphView` *implements* the existing `IWorkspaceView` (unmodified, `WP 8.0B`) — a new concrete class over an already-frozen contract, not a new interface. Every change lives entirely in `Tempest.Desktop` (`DigitalThreadGraphModel`/`DigitalThreadGraphView`, new; `MainWindow`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly, matching `WP 10.3B`'s own cleanest result again. `v0.10.0`'s own ninth Work Package. Previously reviewed 2026-08-09 (WP 10.3B, Ribbon, Toolbar & Command Experience) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop` (`RibbonView`, new; `CommandPaletteOverlay`/`StatusBarView`/`MainWindow`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly, the cleanest result possible against this register's own `src/Tempest.Core/`-only scope. Previously reviewed 2026-08-09 (WP 10.3A, Engineering Object Editors) — reviewed, zero new interfaces added: `IWorkspaceManager`'s own three new members (`ADR-0097`) live in `Tempest.App.Workspace`, out of this register's own scope (`src/Tempest.Core/` only) — the identical, already-established disclosure `WP 10.2A` gave `ADR-0096`. `ObjectEditorView`/`ReviseMechanicalObjectCommand` are both new concrete classes, neither a new interface. Zero `src/Tempest.Core/` files touched, confirmed directly. Previously reviewed 2026-08-09 (WP 10.2B, Docking & Workspace Layouts) — reviewed, zero new interfaces added: every named scope item lives entirely in `Tempest.App.Workspace` (`OutputPanel`, a fourth `IWorkspacePanel` implementer, no new interface) and `Tempest.Desktop` (`DockingGrid`/`PanelHostControl`/`PredefinedLayouts`/`DesktopPanelUiState`) — both out of this register's own scope (`src/Tempest.Core/` only). Zero `src/Tempest.Core/` files touched, confirmed directly. Previously reviewed 2026-08-07 (WP 10.2A, Workspace Modernisation) — reviewed, zero new interfaces added: `IWorkspaceManager`'s own five new members (`ADR-0096`) live in `Tempest.App.Workspace`, out of this register's own scope (`src/Tempest.Core/` only) — the identical, already-established boundary `WP 9.0A` applied to `IPropertyFacetProvider`. Zero `Tempest.Core` file touched by this Work Package, confirmed by direct `git diff --stat`. 168 interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.1B, Runtime Host & Module Discovery Hardening) — reviewed, zero new interfaces added: this register's own scope is `public interface` declarations under `src/Tempest.Core/` only — zero `Tempest.Core` file touched by this Work Package (confirmed by direct `git diff --stat`); every fix lives in `Tempest.App.Workspace`, `Tempest.Desktop`, `Tempest.App.Composition`, or `Tempest.Samples`. 168 interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.1A, Engineering Cockpit Implementation) — reviewed, zero new interfaces added: this register's own scope is `public interface` declarations under `src/Tempest.Core/` only — zero `Tempest.Core` file touched by this Work Package, confirmed by direct `git status` review. 168 public interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.0B, Desktop Application Framework) — reviewed, zero new interfaces added: this register's own scope is `public interface` declarations under `src/Tempest.Core/` only — `Tempest.Desktop`'s own new types live entirely under `src/Tempest.Desktop/`, out of scope by definition, confirmed by direct `git status` review. Zero `Tempest.Core` file touched by this Work Package. 168 public interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.0A, User Experience Architecture) — reviewed, zero new interfaces added: this Work Package is architecture and specification only — every existing Workspace interface (`IWorkspaceView`, `IWorkspacePanel`, `IWorkspaceLayout`, `IProjectExplorer`, `IPropertyInspector`, `IPropertyFacetProvider`, `IProjectExplorerNodeProvider`) is independently re-confirmed rendering-agnostic and unmodified by direct read (`WP10.0A Engineering Review.md` §F2), 168 public interfaces unchanged, confirmed by direct `git status` check showing zero `src/`/`tests/` files touched. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — Second Pass) — reviewed, zero new interfaces added: 168 public interfaces re-verified directly a second time, unchanged since the first pass — `WP 9.8B` (documentation-only) introduced no interface. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — First Pass) — reviewed, zero new interfaces added: verification-only Work Package. All 168 public interfaces re-verified directly (`grep -rhoP "^public interface"`), 168 total unchanged — see `WP9.9.0 Release Readiness Report.md` §13 (Interface Inventory). Previously reviewed 2026-08-07 (WP 9.5A, Manufacturing Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `IManufacturingOperation`/`IWorkInstruction`/`IInspection` (all `WP 8.2C`, unchanged) already satisfy every scope item; 168 total, unchanged — the fourth real-discipline Work Package to leave this register's own total untouched, after `WP 9.2A`, `WP 9.4A`, and `WP 9.3A`. Previously reviewed 2026-08-07 (WP 9.3A, Verification Management Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `IVerificationActivity`/`IVerificationService` (`WP 8.2C`/`WP 7.1E`, unchanged) already satisfy every scope item; 168 total, unchanged — the third real-discipline Work Package to leave this register's own total untouched, after `WP 9.2A` and `WP 9.4A`. Previously reviewed 2026-08-06 (WP 9.4A, Engineering Documents Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `IDocument`/`IDrawing`/`ICadModel` (`WP 8.2C`, unchanged) already satisfy every scope item; 168 total, unchanged — the second real-discipline Work Package to leave this register's own total untouched, after `WP 9.2A`. Previously reviewed 2026-08-05 (WP 9.2A, Engineering Calculations Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `CalculationTemplateRegistry`'s own `ICalculationTemplateAdapter` is a `private` nested interface, never a `Tempest.Core` public contract, and is therefore out of this register's own scope (`src/Tempest.Core/` only), the identical disclosed boundary `WP 9.0A` already applied to `IPropertyFacetProvider`; 168 total, unchanged — the first real-discipline Work Package to leave this register's own total untouched. Previously reviewed 2026-08-05 (WP 9.1A, Requirements Management Workspace) — 1 new interface added directly at implementation time (`IRequirementValidationService`), interleaved alphabetically into the existing `Tempest.Core.Requirements` subsection; 167 → 168 total. `IRequirement`/`IRequirementCollection`/`IRequirementGroup`/`IRequirementsService` (all `WP 7.3A`) each extended additively, own row descriptions updated in place, no new row. `ISelectionService`/`IWorkspaceContext` (`Tempest.App.Workspace`, extended additively — `ADR-0085`) remain out of this register's own scope (`src/Tempest.Core/` only), same disclosed boundary `WP 9.0A` already applied to `IPropertyFacetProvider`. Previously reviewed 2026-08-05 (WP 9.0B, Product Configuration & BOM Management) — 1 new interface added directly at implementation time (`IHasBomLine`), interleaved alphabetically into the existing `Tempest.Core.EngineeringDomain` subsection; 166 → 167 total. Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — 3 new interfaces added directly at implementation time (`IRenamable`, `IHasParent`, `IDeletable`), interleaved alphabetically into the existing `Tempest.Core.EngineeringDomain` subsection (unlike `WP 8.2C`'s own disclosed bulk, non-interleaved addition — three new entries makes interleaving practical); 163 → 166 total. `IPropertyFacetProvider` (`Tempest.App.Workspace`, `WP 9.0A`) is out of this register's own scope (`src/Tempest.Core/` only) and is not listed here. Previously reviewed 2026-08-04 (WP 8.2C, Engineering Domain Implementation) — 83 new interfaces added directly at implementation time, under a new dedicated `Tempest.Core.EngineeringDomain` subsection (not interleaved into the main alphabetical table, disclosed as a pragmatic simplification for this one bulk addition); 80 → 163 total. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — 5 new interfaces added directly at implementation time (`IRequirement`, `IRequirementCollection`, `IRequirementEvidence`, `IRequirementGroup`, `IRequirementsService`), not backfilled later — the first Work Package to keep this register current with its own implementation since `WP 7.1F` established the practice. Previously reviewed 2026-07-30 (WP 7.1F, Engineering Core Integration Review & Certification) — full backfill performed; 11 interfaces introduced across all five Engineering Foundation Work Packages (`WP 7.1A`–`WP 7.1E`) are now listed, none of which had ever been recorded here — this register had gone stale since `WP 6.8` (2026-07-29), the exact drift pattern `FCR-0005` exists to catch, now found and closed by this Work Package's own certification review, mirroring `WP 6.8`'s own identical finding for the `v0.6.0` release. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every interface introduced since `WP 5.2` (`WP 6.1`, `WP 6.4`, `WP 6.5`, `WP 6.2`, `WP 6.0`, `WP 6.3`, `WP 6.7`, `WP 6.6`) is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
| **Related Documents** | `docs/architecture/Ownership Matrix.md`; `Dependency Injection Register.md`; `Namespace Register.md`. |
| **Related ADRs** | ADR-0006, ADR-0009, ADR-0017, ADR-0020, ADR-0023, ADR-0024, ADR-0034, ADR-0036, ADR-0037, ADR-0039, ADR-0040–ADR-0057. |
| **Related Academy Articles** | `docs/architecture/Engineering Glossary.md` (Platform API vs. Platform Service); `docs/engineering/Engineering Principles.md`. |
| **Coverage Status** | **Complete.** Verified directly against `grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core` — 80 interfaces found, 80 listed below, zero omitted. A genuine, pre-existing arithmetic drift was also found and corrected during the `WP 6.8` backfill: the register's own Classification Summary read "Host-owned = 6" while its own Entries table already listed 7 Host-owned rows (`IFrameworkDiscoveryService`, `IHostedServiceDiscoveryService`, `IHostedServiceManager`, `IModuleLifecycleManager`, `IPluginAssemblyLoader`, `IPluginManifestDiscoveryService`, `IRuntimeModuleManager`) — an undercount that predates `WP 6.7`'s own first disclosure of the larger gap, corrected at that time. |

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
| `ICommandMacro` | `Tempest.Core.Macros` | Platform API (data contract) | An ordered, named sequence of registered Command Ids (`WP 10.6A`, `ADR-0099`) |
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
| `IExternalControllerProvider` | `Tempest.Core.Input` | Platform API (contract, extends `IInputBindingProvider`) | An `IInputBindingProvider` backed by a physical external device — no production implementation ships this Work Package, only a test-only double (`WP 10.6A`, `ADR-0100`) |
| `IFrameworkDiscoveryService` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module Discovery |
| `IHostedService` | `Tempest.Core.BackgroundServices` | Platform API (contract) | Background service Start/Stop |
| `IHostedServiceDiscoveryService` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service discovery |
| `IHostedServiceManager` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service start/stop orchestration |
| `IIdentity` | `Tempest.Core.Identity` | Platform API (data contract) | The shape of a claimed identity (`WP 6.1`) |
| `IIdentityService` | `Tempest.Core.Identity` | DI-public | Establishes/resolves a principal; additive, not in the original catalogue (`WP 6.1`) |
| `IImportService` | `Tempest.Core.ExportImport` | DI-public (dual-registered under its own concrete type, mirroring `ICurrentPrincipalAccessor`) | Reads a previously exported artifact back into the owning service(s) |
| `IImportable` | `Tempest.Core.ExportImport` | Registered via `ImportService.RegisterImportable`, not itself a DI service type | Read-back counterpart to `IExportable`, routed to by `Kind` |
| `IInputBindingProvider` | `Tempest.Core.Input` | Platform API (contract) | A source of physical/virtual input that can request a registered Command Id be invoked (`WP 10.6A`, `ADR-0100`) |
| `IInputBindingRegistry` | `Tempest.Core.Input` | DI-public | Tracks every registered `IInputBindingProvider`, routing each one's own request to `ICommandRegistry.InvokeAsync` (`WP 10.6A`, `ADR-0100`) |
| `ILicense` | `Tempest.Core.Licensing` | Platform API (contract) | A single, validated, immutable license |
| `ILicenseProvider` | `Tempest.Core.Licensing` | DI-public (via `AddInstance`) | Read-only, post-validation view of the current license |
| `ILicenseValidator` | `Tempest.Core.Licensing` | Not DI-registered (Composition-Root-constructed, pre-container leaf, mirroring `IPlatformVersionProvider`) | Validates a license at Host startup, before the container exists |
| `ILogSink` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Log entry destination |
| `ILogger` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Structured logging abstraction |
| `ILoggerFactory` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Produces `ILogger` instances |
| `IMacroManager` | `Tempest.Core.Macros` | DI-public | Creates/lists/deletes `ICommandMacro`s, keeping each one's own `CommandDescriptor` registered against `ICommandRegistry` (`WP 10.6A`, `ADR-0099`) |
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
| `IRequirement` | `Tempest.Core.Requirements` | Platform API (data contract) | An `IEngineeringDocument`-backed engineering requirement — identifier, statement, category, status, plus (`WP 9.1A`, additive) Owner, Priority, IsDeleted, GroupId (`WP 7.3A`, `ADR-0058`/`ADR-0059`; `ADR-0084`) |
| `IRequirementCollection` | `Tempest.Core.Requirements` | Platform API (data contract) | A named, purpose-built set of requirements; membership derived via `GetReferencesAsync`, never stored directly; plus (`WP 9.1A`, additive) IsDeleted (`WP 7.3A`, `ADR-0058`; `ADR-0084`) |
| `IRequirementEvidence` | `Tempest.Core.Requirements` | Platform API (data contract) | A read-side aggregation of a requirement's own verification history and linked references — the digital thread, demonstrated (`WP 7.3A`) |
| `IRequirementGroup` | `Tempest.Core.Requirements` | Platform API (data contract) | A hierarchical requirement categorisation node; parent reference now the DTO's own live, current value (`WP 9.1A`, corrected from a `.FirstOrDefault()`-over-relationships resolution), plus (`WP 9.1A`, additive) IsDeleted (`WP 7.3A`, `ADR-0058`; `ADR-0084`) |
| `IRequirementValidationService` | `Tempest.Core.Requirements` | DI-public | Validates one requirement: duplicate identifier, orphan, missing verification, missing allocation, advisory relationship kind — reuses `IValidationResult`/`IValidationDiagnostic`'s own generic result shape, never `IValidationRule` (scoped to `IEngineeringObject`, structurally incompatible with `IRequirement`) (`WP 9.1A`, `ADR-0084`) |
| `IRequirementsService` | `Tempest.Core.Requirements` | DI-public | Create/find/revise/set-status/link/list requirements, collections, and groups; no internal permission gating; plus (`WP 9.1A`, additive) set-owner/set-priority/delete/move-to-group/move-group/delete-group/delete-collection/list-collections/list-groups (`WP 7.3A`, `ADR-0058`/`ADR-0061`; `ADR-0084`) |
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

**Total: 173 public interfaces under `src/Tempest.Core/` — Verified
directly (`grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core`
returns exactly 173 matches, `WP 10.6A`'s own five new `Tempest.Core.Macros`/
`Tempest.Core.Input` interfaces now included — 168 → 173 — matching the
rows above (interleaved alphabetically) plus the
`Tempest.Core.EngineeringDomain` rows in the dedicated subsection
below, three of them — `IRenamable`, `IHasParent`, `IDeletable` — added
by `WP 9.0A`, and one more — `IHasBomLine` — added by `WP 9.0B`). 83 new interfaces were added by `WP 8.2C` (Engineering Domain
Implementation) at the time of their own compilation — the largest
single addition this register has ever recorded, and the first time
`Tempest.Core.EngineeringDomain` compiled at all (`WP 8.2A`/`WP 8.2B`
proposed the same interfaces as uncompiled C#, never counted here,
consistent with this register's own "Verified directly against real
code" standard). 5 new interfaces were added by `WP 7.3A` (Requirements Engine) at the time of
their own implementation, not discovered as drift afterward:
`IRequirement`, `IRequirementCollection`, `IRequirementEvidence`,
`IRequirementGroup`, `IRequirementsService` — the first Work Package
since `WP 7.1F` itself established the practice of keeping this register
current with implementation, rather than backfilling it later. 11
interfaces introduced across the five Engineering Foundation Work
Packages were added in a prior pass (`WP 7.1F`), closing a gap that had
persisted, undetected, since each framework shipped: `WP 7.1A`
(`IEngineeringDocument`, `IDocumentRevision`, `IEngineeringDocumentStore`
— 3), `WP 7.1B` (`IDimension`, `IUnitConverter` — 2), `WP 7.1C`
(`IMaterialCatalog`, `IMaterialSpecification` — 2), `WP 7.1D`
(`ICalculationDefinition<TInput, TResult>`, `ICalculationEngine` — 2),
`WP 7.1E` (`IVerificationRecord`, `IVerificationService` — 2) — none of
these five Work Packages' own interfaces had ever been recorded here
before that Work Package (`WP 7.1F`), the same undetected-drift pattern
`WP 6.8` found and closed for `v0.6.0`'s own six Work Packages, recurring
and closed a second time. Previously, `WP 6.8` fully backfilled: 23 interfaces introduced by
`WP 6.1` (`ICurrentPrincipalAccessor`, `IIdentity`, `IIdentityService`,
`IPermissionEvaluator`, `IPrincipal`, `IRole`, `IRoleProvider` — 7),
`WP 6.4` (`IPersistenceStore`, `ISettingDefinition`,
`ISettingsChangedEvent`, `ISettingsProvider` — 4), `WP 6.5`
(`IAuditQuery`, `IAuditRecord`, `IAuditRecorder` — 3), `WP 6.2`
(`INotification`, `INotificationDispatcher`, `INotificationHandler<T>`,
`IPlatformNotification` — 4), `WP 6.0` (`IReportDefinition`,
`IReportRenderer<T>`, `IReportTemplate<T>`, `IReportingService` — 4),
and `WP 6.3` (`IApiEndpointRegistry` — 1).**

### `Tempest.Core.EngineeringDomain` (WP 8.2C — 83 interfaces)

Added as one bulk pass, not interleaved into the alphabetical table
above — a disclosed, pragmatic simplification for a single Work
Package's own 83-interface addition, not a change to this register's
own standing convention for future, smaller additions. Nine are
DI-public (`IEngineeringObjectRepository`,
`IEngineeringRelationshipRepository`, `ILifecycleTransitionTable`,
`IValidationRuleSet`, `IReferenceIntegrityChecker`,
`IRelationshipDiscovery`, `IDependencyTraversal`, `IImpactAnalysis`,
`IEvidenceComposer`), matching `TempestHost.cs`'s own ten new
registrations exactly (`EngineeringDomainContext`, a concrete class,
is the tenth, and is not itself an interface). Five carry a
`ADR-0078` classification note — `IRequirement`, `IRequirementSet`,
`IVerificationResult`, `ICalculationResult`, `IMaterial` — compiled
contracts with no concrete realisation in this namespace, by design.

| Interface | Namespace | Classification | Purpose |
|---|---|---|---|
| `IAction` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A single-level specialisation of `ITask`, raised by another object (`WP 8.2B`/`WP 8.2C`) |
| `IApproval` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A recorded approval event gating a lifecycle transition (`WP 8.2B`/`WP 8.2C`) |
| `IApprovalGate` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | Checks whether an object's own approval requirement is satisfied (`WP 8.2B`) |
| `IAssembly` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A composition parent of Sub-Assembly/Part; concrete `Assembly` non-sealed for `SubAssembly` (`WP 8.2B`/`WP 8.2C`) |
| `IAssumption` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A stated assumption, related to any object (`WP 8.2B`/`WP 8.2C`) |
| `IAttachment` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | File metadata carried by `IHasAttachments` (`WP 8.2B`/`WP 8.2C`) |
| `IBaseline` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A frozen `IConfiguration`; concrete `Baseline` non-sealed for `Release` (`WP 8.2B`/`WP 8.2C`) |
| `ICadModel` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IDocument` specialisation carrying a model format (`WP 8.2B`/`WP 8.2C`) |
| `ICalculation` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A registrable calculation definition, Domain-level (`WP 8.2B`/`WP 8.2C`) |
| `ICalculationResult` | `Tempest.Core.EngineeringDomain` | Platform API (contract; concrete realisation owned by `Tempest.Core.Calculations`, `ADR-0078`) | The Domain-level shape of a calculation execution record (`WP 8.2B`) |
| `ICalculationSet` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A named group of `ICalculation` members (`WP 8.2B`/`WP 8.2C`) |
| `IChangeRequest` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A proposal to change one or more objects (`WP 8.2B`/`WP 8.2C`) |
| `IComponent` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | The smallest identity/metadata-only Physical & Configuration object (`WP 8.2B`/`WP 8.2C`) |
| `IConfiguration` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | References specific revisions of member objects; concrete `Configuration` non-sealed for `Baseline` (`WP 8.2B`/`WP 8.2C`) |
| `IDecision` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A recorded decision, carrying its own rationale (`WP 8.2B`/`WP 8.2C`) |
| `IDeletable` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Soft-delete state and `DeleteAsync`, rejecting deletion while live children exist (`WP 9.0A`, `ADR-0080`) |
| `IDeliverable` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An object related to a `Milestone` (`WP 8.2B`/`WP 8.2C`) |
| `IDependencyTraversal` | `Tempest.Core.EngineeringDomain` | DI-public | Outward, category-filtered, depth-bounded object graph traversal (`WP 8.2B`/`WP 8.2C`) |
| `IDocument` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A general document; concrete `Document` non-sealed for `Drawing`/`CadModel`/`WorkInstruction` (`WP 8.2B`/`WP 8.2C`) |
| `IDrawing` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IDocument` specialisation carrying a drawing number (`WP 8.2B`/`WP 8.2C`) |
| `IEngineeringChange` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | Derived from an `IChangeRequest` (`WP 8.2B`/`WP 8.2C`) |
| `IEngineeringObject` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | The base contract every canonical Engineering Object satisfies, mirroring `IEngineeringDocument` (`WP 8.2B`/`WP 8.2C`, `ADR-0072`) |
| `IEngineeringObjectFactory` | `Tempest.Core.EngineeringDomain` | Platform API (contract; realised by `EngineeringObjectFactory<T>`, constructed by the composition root, not DI-registered — `WP8.2B Dependency Rules.md` §8) | Constructs one Kind of Engineering Object (`WP 8.2B`/`WP 8.2C`, `ADR-0079`) |
| `IEngineeringObjectRepository` | `Tempest.Core.EngineeringDomain` | DI-public | The new, in-memory, Kind-queryable object index (`WP 8.2C`, `ADR-0077`) |
| `IEngineeringRelationship` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | The one generic relationship shape, never a closed set of per-category types (`WP 8.2B`, `ADR-0076`) |
| `IEngineeringRelationshipFactory` | `Tempest.Core.EngineeringDomain` | Platform API (contract; realised by `EngineeringRelationshipFactory`, constructed by the composition root, not DI-registered) | Constructs one named relationship kind (`WP 8.2B`/`WP 8.2C`, `ADR-0079`) |
| `IEngineeringRelationshipRepository` | `Tempest.Core.EngineeringDomain` | DI-public | The new in-memory side index recording `Category`/`CreatedByPrincipalId`/`CreatedAt` for each relationship (`WP 8.2C`, `ADR-0077`) |
| `IEvidence` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A composed, read-side traceability result — never a stored relationship (`WP 8.2B`/`WP 8.2C`) |
| `IEvidenceComposer` | `Tempest.Core.EngineeringDomain` | DI-public | Composes `IEvidence` from outgoing Verification/Calculation-category relationships (`WP 8.2B`/`WP 8.2C`) |
| `IExternalSystemLink` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A reference to an object in an external system (`WP 8.2B`/`WP 8.2C`) |
| `IFamilySpecificState` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A named per-family lifecycle state mapped to its canonical equivalent (`WP 8.2B`) |
| `IHasAttachments` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Attach/list file attachments (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IHasBomLine` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Quantity/Unit of Measure/Find Number/Item Number/Reference Designator plus `SetBomLineAsync` (`WP 9.0B`, `ADR-0083`) |
| `IHasBusinessIdentifier` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | An optional caller-assigned identifier plus a display name (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IHasLifecycle` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Canonical `LifecycleState`, transition history, `TransitionAsync` (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IHasMetadata` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Category/discipline/owner/tags/classification/notes (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IHasParent` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | A live, current structural parent plus `MoveAsync`; the frozen `IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId` remain construction-time snapshots (`WP 9.0A`, `ADR-0080`/`ADR-0081`) |
| `IHasRelationships` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Link to another object; read outgoing relationships (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IHasRevisions` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Content, author, revise, revision history (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IHazard` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A safety specialisation of `IRisk`; concrete `Hazard : Risk` (`WP 8.2B`/`WP 8.2C`) |
| `IImpactAnalysis` | `Tempest.Core.EngineeringDomain` | DI-public | Incoming traversal over Dependency/Allocation/Verification categories only (`WP 8.2B`/`WP 8.2C`) |
| `IInspection` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IVerificationActivity` specialisation; concrete `Inspection : VerificationActivity` (`WP 8.2B`/`WP 8.2C`) |
| `IIssue` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A tracked issue, related to or blocking any object (`WP 8.2B`/`WP 8.2C`) |
| `ILifecycleTransitionRecord` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | One recorded lifecycle transition (`WP 8.2B`/`WP 8.2C`) |
| `ILifecycleTransitionTable` | `Tempest.Core.EngineeringDomain` | DI-public | The canonical eight-state permitted-transition table (`WP 8.2B`/`WP 8.2C`) |
| `ILifecycleValidationRule` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | Validates a proposed lifecycle transition (`WP 8.2B`) |
| `IManufacturingOperation` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An operation manufacturing a `Part` (`WP 8.2B`/`WP 8.2C`) |
| `IMaterial` | `Tempest.Core.EngineeringDomain` | Platform API (contract; concrete realisation owned by `Tempest.Core.Materials`, `ADR-0078`) | The Domain-level shape of a material specification (`WP 8.2B`) |
| `IMilestone` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A dated programme milestone (`WP 8.2B`/`WP 8.2C`) |
| `IPart` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A physical part, optionally referencing a `MaterialId` (`WP 8.2B`/`WP 8.2C`) |
| `IPortfolio` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | The top of the Programme Hierarchy (`WP 8.2B`/`WP 8.2C`) |
| `IProgramme` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A child of `IPortfolio`, parent of `IProject` (`WP 8.2B`/`WP 8.2C`) |
| `IProject` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A child of `IProgramme`; the root most other sample objects relate to (`WP 8.2B`/`WP 8.2C`) |
| `IPurchaseItem` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A Supply Chain item referencing an `ISupplier` (`WP 8.2B`/`WP 8.2C`) |
| `IRecommendedValidationRule` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | A non-structural, advisory validation rule carrying its own rationale (`WP 8.2B`) |
| `IReferenceIntegrityChecker` | `Tempest.Core.EngineeringDomain` | DI-public | Checks a relationship's/baseline's own referenced objects still exist (`WP 8.2B`/`WP 8.2C`) |
| `IRelationshipDescriptor` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | Category/direction/multiplicity metadata for a relationship shape (`WP 8.2B`) |
| `IRelationshipDiscovery` | `Tempest.Core.EngineeringDomain` | DI-public | Outgoing/incoming/by-category relationship lookup (`WP 8.2B`/`WP 8.2C`) |
| `IRelationshipValidator` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | Validates a proposed relationship (`WP 8.2B`) |
| `IRelease` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IBaseline` specialisation; concrete `Release : Baseline` (`WP 8.2B`/`WP 8.2C`) |
| `IReleaseGate` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | Checks whether a baseline is ready to release (`WP 8.2B`) |
| `IRenamable` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | `RenameAsync`, changing `IHasBusinessIdentifier.DisplayName`'s own backing value without changing that interface's own shape (`WP 9.0A`, `ADR-0080`) |
| `IRequirement` | `Tempest.Core.EngineeringDomain` | Platform API (contract; concrete realisation owned by `Tempest.Core.Requirements`, `ADR-0078`) | The Domain-level, facet-composed shape of a requirement — a deliberately loose reconciliation against the real, shipped `Requirements.IRequirement` (`WP 8.2B`) |
| `IRequirementSet` | `Tempest.Core.EngineeringDomain` | Platform API (contract; concrete realisation owned by `Tempest.Core.Requirements`, `ADR-0078`) | The Domain-level shape of a requirement collection/group (`WP 8.2B`) |
| `IReview` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A recorded review of any object (`WP 8.2B`/`WP 8.2C`) |
| `IReviewGate` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | Requests and reads reviews for an object (`WP 8.2B`) |
| `IRevisionRecord` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | One immutable content revision, scoped to one object's own history — referenced but never defined by `WP 8.2B`, closed here (`WP 8.2C`) |
| `IRisk` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A tracked risk carrying likelihood/severity; concrete `Risk` non-sealed for `Hazard` (`WP 8.2B`/`WP 8.2C`) |
| `ISavedQuery` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A named, saved `ISearchQuery` (`WP 8.2B`) |
| `ISearchQuery` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | Text/Kind/category/metadata search filters (`WP 8.2B`) |
| `ISearchResult` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | Matches plus a total count (`WP 8.2B`) |
| `ISearchable` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Exposes a computed searchable text projection (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `ISimulation` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `ICalculationResult` specialisation carrying a simulation type (`WP 8.2B`/`WP 8.2C`) |
| `ISubAssembly` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IAssembly` specialisation with a parent assembly reference; concrete `SubAssembly : Assembly` (`WP 8.2B`/`WP 8.2C`) |
| `ISupplier` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A Supply Chain supplier (`WP 8.2B`/`WP 8.2C`) |
| `ITask` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A process task; concrete `EngineeringTask`, not `Task`, to avoid colliding with `System.Threading.Tasks.Task` (`WP 8.2B`/`WP 8.2C`) |
| `ITest` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IVerificationActivity` specialisation; concrete `Test : VerificationActivity` (`WP 8.2B`/`WP 8.2C`) |
| `ITraceable` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Composes `IEvidence` for this object (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IValidatable` | `Tempest.Core.EngineeringDomain` | Platform API (facet contract) | Validates this object against registered rules (`WP 8.2B`/`WP 8.2C`, `ADR-0075`) |
| `IValidationDiagnostic` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | One validation error/warning, with its own code and message (`WP 8.2B`/`WP 8.2C`) |
| `IValidationResult` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | Errors (structural) and warnings (advisory) from a validation run (`WP 8.2B`/`WP 8.2C`) |
| `IValidationRule` | `Tempest.Core.EngineeringDomain` | Platform API (contract, not itself DI-registered) | Evaluates one rule against a subject object (`WP 8.2B`/`WP 8.2C`) |
| `IValidationRuleSet` | `Tempest.Core.EngineeringDomain` | DI-public | Registers and runs `IValidationRule`s per Kind — zero rules registered by default (`WP 8.2B`/`WP 8.2C`) |
| `IVerification` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | The umbrella Verification concept; concrete `Verification` (`WP 8.2B`/`WP 8.2C`) |
| `IVerificationActivity` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | A verification-in-progress, distinct from its own eventual result; concrete `VerificationActivity` non-sealed for `Test`/`Inspection` (`WP 8.2B`/`WP 8.2C`) |
| `IVerificationResult` | `Tempest.Core.EngineeringDomain` | Platform API (contract; concrete realisation owned by `Tempest.Core.Verification`, `ADR-0078`) | The Domain-level shape of a verification record (`WP 8.2B`) |
| `IWorkInstruction` | `Tempest.Core.EngineeringDomain` | Platform API (data contract) | An `IDocument` specialisation documenting a `IManufacturingOperation` (`WP 8.2B`/`WP 8.2C`) |

## Classification Summary

**Reflects all 163 interfaces now listed above.** This section's own
running total had already drifted five short of the main table's own
80 (`75` vs. `80`) before this Work Package began — a pre-existing gap
this Work Package found but did not cause, disclosed here rather than
silently carried forward uncorrected; not investigated further, since
resolving it is outside `WP 8.2C`'s own scope.

| Classification | Count |
|---|---|
| DI-public (`AddInstance` or container-constructed singleton) | 38 |
| Host-owned, never DI-public (ADR-0017 and its extensions) | 7 |
| Platform API / contract only (no dispatcher or orchestration yet, consumer-facing marker, or data shape) | 98 |
| Discovered/registered but not itself a DI registration target | 3 |
| Composition-time / not-DI-registered infrastructure | 11 |
| Pre-module-pipeline, outside the platform-service model | 1 |

**Total: 38 + 7 + 98 + 3 + 11 + 1 = 158** (against a 163-interface
register — the pre-existing five-row gap noted above, carried forward
unresolved). `WP 8.2C` added 83 rows to this summary: 9 new DI-public
(`IEngineeringObjectRepository`, `IEngineeringRelationshipRepository`,
`ILifecycleTransitionTable`, `IValidationRuleSet`,
`IReferenceIntegrityChecker`, `IRelationshipDiscovery`,
`IDependencyTraversal`, `IImpactAnalysis`, `IEvidenceComposer`); 2 new
Composition-time/not-DI-registered (`IEngineeringObjectFactory`,
`IEngineeringRelationshipFactory` — each realised by a generic type
constructed directly by a composition root, mirroring `IUnitConverter`'s
own already-established classification); the remaining 72 Platform
API/contract only. Host-owned and Pre-module-pipeline counts are
unchanged by `WP 8.2C` — it introduced no Host-owned collaborator.

## Cross-Reference Check

Every "Host-owned, never DI-public" interface above matches a row in
`docs/architecture/Ownership Matrix.md`; every "DI-public" interface
matches a registration row in `Dependency Injection Register.md`
(cross-checked directly against that register's own full backfill,
performed in this same Work Package). No discrepancy found between this
register's classification and either source document.

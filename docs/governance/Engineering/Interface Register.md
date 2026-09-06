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
| **Last Reviewed** | 2026-09-06 (`Group D`, P03 Commercial Intelligence) — twenty rows added for the `P03` catalogues, validation services and reasoning services; 252 → 272 interfaces, re-derived by the Interface Register check in `scripts/governance-healthcheck.ps1`. Two drifts disclosed rather than carried forward: this field was last stated at `WP 16.3B` and had not been revised by `Group B` or `Group C` even though both added rows and both revised the Total line beneath the table; and the check reported the `Group B`/`Group C` rows as present, so the row data itself was sound and only this narrative was stale. Corrected here. Previously 2026-09-04 (`WP 16.3B` integration) — three rows added for the schema-versioning interfaces `ADR-0120` introduced (`IStateMigration`, `IStateMigrationRegistry`, `ISettingsMigration<TDocument>`), caught by the `WP 16.1B` Interface check on the merged tree. Previously reviewed 2026-09-04 (WP 16.2A, Register and Status Currency) — full re-derivation against `grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core`, 188 matches; 174 → 188 total (net +14). 15 rows backfilled for two releases whose implementation never updated this register: `v0.13.0` Plugin Trust & Capability Enforcement (`ADR-0107`–`ADR-0112`) — `IPluginTrustStore`, `IPluginRegistry`, `IPluginRegistryRecorder`, `IPluginDeniedTypeRecorder`, `IPluginDeniedTypeRegistry`, `IPluginComponentPrincipalRecorder`, `IPluginComponentPrincipalRegistry`, `ICurrentComponentAccessor` (8); `v0.14.0` Durability/Rehydration/Attachment Content (`ADR-0113`, `ADR-0114`, `ADR-0116`, `TD-85`) — `IAttachmentContentStore`, `IBinaryPersistenceStore`, `IEngineeringObjectRehydrator`, `IEngineeringObjectRehydratorRegistry`, `IEngineeringObjectStateStore`, `IRehydratable<TSelf>`, `ISessionPrincipalSource` (7). 1 stale row removed: `IProjectRepository` (`Tempest.Core.Repositories`), whose concrete implementation no longer exists under `src/` — deleted by an earlier Work Package without this register being updated. `WP 16.3B`'s concurrent `IStateMigration`/`IStateMigrationRegistry`/`ISettingsMigration<T>` additions are not on this Work Package's own base commit and are explicitly out of scope here; they enter this register only at `WP 16.3B`'s own merge (disclosed in **Coverage Status**). See `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md` for the full derivation. Previously reviewed 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — 1 new interface added directly at implementation time, interleaved alphabetically into the existing `Tempest.Core.Modules` subsection: `IFaultInjectionModule` (`ADR-0102`); 173 → 174 total. `ITempestHostBuilder`'s own existing row updated in place (not a new row) to reflect its one new member, `EnableFaultInjectionModules()`. Previously reviewed 2026-08-10 (WP 10.5C, Commercial User Experience & Application Completion) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop`/`Tempest.App.Workspace` (`DisciplineColors`, a new `internal static class`, not an interface; `ProjectExplorerNode`/`CockpitKpiCard`, existing records gaining only additive, defaulted trailing parameters; `CockpitCardControl`/`ProjectExplorerView`/`PropertyInspectorView`/`RibbonView`/`CockpitView`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly. `v0.10.0`'s own thirteenth Work Package by completion order. Previously reviewed 2026-08-10 (WP 10.6A, Command Execution & Productivity Experience) — 5 new interfaces added directly at implementation time, interleaved alphabetically into the existing table: `ICommandMacro`/`IMacroManager` (`Tempest.Core.Macros`, `ADR-0099`), `IInputBindingProvider`/`IExternalControllerProvider`/`IInputBindingRegistry` (`Tempest.Core.Input`, `ADR-0100`); 168 → 173 total. `IUndoRedoStack` (`Tempest.App.Workspace`) and `IBackgroundTaskRunner` (`Tempest.Desktop.Tasks`) are both out of this register's own scope (`src/Tempest.Core/` only) — the identical, already-established boundary `WP 9.0A`/`WP 10.2A`/`WP 10.3A` each already applied to `IPropertyFacetProvider`/`IWorkspaceManager`. `v0.10.0`'s own twelfth Work Package. Previously reviewed 2026-08-10 (WP 10.5B, Desktop Workflow & Professional Interaction) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop` (`InputDialog`/`MessageDialog`/`SettingsDialog`/`PlatformNotificationToastBridge`, new classes over already-existing patterns — `PlatformNotificationToastBridge` implements the already-existing `IEventHandler<IPlatformNotification>`, introducing no new interface of its own; `UserSettings`/`WindowUiState`, new top-level classes over the already-existing `ISettingsProvider` pattern; `RibbonView`/`ProjectExplorerView`/`DocumentAreaView`/`MainWindow`/`App`/`ToastHost`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly. `v0.10.0`'s own eleventh Work Package. Previously reviewed 2026-08-10 (WP 10.5A, Workspace Visual Polish & Engineering User Experience) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop` (`ApplicationPalette`/`ThemeReactiveBrush`/`SeverityColors`/`IconGeometry`/`ToastHost`/`BusyOverlay`/`ConfirmationDialog`/`EmptyStateView`, all new classes over already-existing patterns; `PanelHostControl`/`CommandPaletteOverlay`/`DigitalThreadGraphView`/`IconRegistry`/`DocumentAreaView`/`ObjectEditorView`/`MainWindow`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly. `v0.10.0`'s own tenth Work Package. Previously reviewed 2026-08-09 (WP 10.4A, Digital Thread Visualisation) — reviewed, zero new interfaces added: `DigitalThreadGraphView` *implements* the existing `IWorkspaceView` (unmodified, `WP 8.0B`) — a new concrete class over an already-frozen contract, not a new interface. Every change lives entirely in `Tempest.Desktop` (`DigitalThreadGraphModel`/`DigitalThreadGraphView`, new; `MainWindow`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly, matching `WP 10.3B`'s own cleanest result again. `v0.10.0`'s own ninth Work Package. Previously reviewed 2026-08-09 (WP 10.3B, Ribbon, Toolbar & Command Experience) — reviewed, zero new interfaces added: every change lives entirely in `Tempest.Desktop` (`RibbonView`, new; `CommandPaletteOverlay`/`StatusBarView`/`MainWindow`, modified) — zero `src/Tempest.Core/` files touched, confirmed directly, the cleanest result possible against this register's own `src/Tempest.Core/`-only scope. Previously reviewed 2026-08-09 (WP 10.3A, Engineering Object Editors) — reviewed, zero new interfaces added: `IWorkspaceManager`'s own three new members (`ADR-0097`) live in `Tempest.App.Workspace`, out of this register's own scope (`src/Tempest.Core/` only) — the identical, already-established disclosure `WP 10.2A` gave `ADR-0096`. `ObjectEditorView`/`ReviseMechanicalObjectCommand` are both new concrete classes, neither a new interface. Zero `src/Tempest.Core/` files touched, confirmed directly. Previously reviewed 2026-08-09 (WP 10.2B, Docking & Workspace Layouts) — reviewed, zero new interfaces added: every named scope item lives entirely in `Tempest.App.Workspace` (`OutputPanel`, a fourth `IWorkspacePanel` implementer, no new interface) and `Tempest.Desktop` (`DockingGrid`/`PanelHostControl`/`PredefinedLayouts`/`DesktopPanelUiState`) — both out of this register's own scope (`src/Tempest.Core/` only). Zero `src/Tempest.Core/` files touched, confirmed directly. Previously reviewed 2026-08-07 (WP 10.2A, Workspace Modernisation) — reviewed, zero new interfaces added: `IWorkspaceManager`'s own five new members (`ADR-0096`) live in `Tempest.App.Workspace`, out of this register's own scope (`src/Tempest.Core/` only) — the identical, already-established boundary `WP 9.0A` applied to `IPropertyFacetProvider`. Zero `Tempest.Core` file touched by this Work Package, confirmed by direct `git diff --stat`. 168 interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.1B, Runtime Host & Module Discovery Hardening) — reviewed, zero new interfaces added: this register's own scope is `public interface` declarations under `src/Tempest.Core/` only — zero `Tempest.Core` file touched by this Work Package (confirmed by direct `git diff --stat`); every fix lives in `Tempest.App.Workspace`, `Tempest.Desktop`, `Tempest.App.Composition`, or `Tempest.Samples`. 168 interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.1A, Engineering Cockpit Implementation) — reviewed, zero new interfaces added: this register's own scope is `public interface` declarations under `src/Tempest.Core/` only — zero `Tempest.Core` file touched by this Work Package, confirmed by direct `git status` review. 168 public interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.0B, Desktop Application Framework) — reviewed, zero new interfaces added: this register's own scope is `public interface` declarations under `src/Tempest.Core/` only — `Tempest.Desktop`'s own new types live entirely under `src/Tempest.Desktop/`, out of scope by definition, confirmed by direct `git status` review. Zero `Tempest.Core` file touched by this Work Package. 168 public interfaces unchanged. Previously reviewed 2026-08-07 (WP 10.0A, User Experience Architecture) — reviewed, zero new interfaces added: this Work Package is architecture and specification only — every existing Workspace interface (`IWorkspaceView`, `IWorkspacePanel`, `IWorkspaceLayout`, `IProjectExplorer`, `IPropertyInspector`, `IPropertyFacetProvider`, `IProjectExplorerNodeProvider`) is independently re-confirmed rendering-agnostic and unmodified by direct read (`WP10.0A Engineering Review.md` §F2), 168 public interfaces unchanged, confirmed by direct `git status` check showing zero `src/`/`tests/` files touched. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — Second Pass) — reviewed, zero new interfaces added: 168 public interfaces re-verified directly a second time, unchanged since the first pass — `WP 9.8B` (documentation-only) introduced no interface. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — First Pass) — reviewed, zero new interfaces added: verification-only Work Package. All 168 public interfaces re-verified directly (`grep -rhoP "^public interface"`), 168 total unchanged — see `WP9.9.0 Release Readiness Report.md` §13 (Interface Inventory). Previously reviewed 2026-08-07 (WP 9.5A, Manufacturing Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `IManufacturingOperation`/`IWorkInstruction`/`IInspection` (all `WP 8.2C`, unchanged) already satisfy every scope item; 168 total, unchanged — the fourth real-discipline Work Package to leave this register's own total untouched, after `WP 9.2A`, `WP 9.4A`, and `WP 9.3A`. Previously reviewed 2026-08-07 (WP 9.3A, Verification Management Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `IVerificationActivity`/`IVerificationService` (`WP 8.2C`/`WP 7.1E`, unchanged) already satisfy every scope item; 168 total, unchanged — the third real-discipline Work Package to leave this register's own total untouched, after `WP 9.2A` and `WP 9.4A`. Previously reviewed 2026-08-06 (WP 9.4A, Engineering Documents Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `IDocument`/`IDrawing`/`ICadModel` (`WP 8.2C`, unchanged) already satisfy every scope item; 168 total, unchanged — the second real-discipline Work Package to leave this register's own total untouched, after `WP 9.2A`. Previously reviewed 2026-08-05 (WP 9.2A, Engineering Calculations Workspace) — reviewed, zero new `Tempest.Core` interfaces added: the entire Work Package is additive at the Workspace (`Tempest.App`) and representative-data (`Tempest.Samples`) layers only — `CalculationTemplateRegistry`'s own `ICalculationTemplateAdapter` is a `private` nested interface, never a `Tempest.Core` public contract, and is therefore out of this register's own scope (`src/Tempest.Core/` only), the identical disclosed boundary `WP 9.0A` already applied to `IPropertyFacetProvider`; 168 total, unchanged — the first real-discipline Work Package to leave this register's own total untouched. Previously reviewed 2026-08-05 (WP 9.1A, Requirements Management Workspace) — 1 new interface added directly at implementation time (`IRequirementValidationService`), interleaved alphabetically into the existing `Tempest.Core.Requirements` subsection; 167 → 168 total. `IRequirement`/`IRequirementCollection`/`IRequirementGroup`/`IRequirementsService` (all `WP 7.3A`) each extended additively, own row descriptions updated in place, no new row. `ISelectionService`/`IWorkspaceContext` (`Tempest.App.Workspace`, extended additively — `ADR-0085`) remain out of this register's own scope (`src/Tempest.Core/` only), same disclosed boundary `WP 9.0A` already applied to `IPropertyFacetProvider`. Previously reviewed 2026-08-05 (WP 9.0B, Product Configuration & BOM Management) — 1 new interface added directly at implementation time (`IHasBomLine`), interleaved alphabetically into the existing `Tempest.Core.EngineeringDomain` subsection; 166 → 167 total. Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — 3 new interfaces added directly at implementation time (`IRenamable`, `IHasParent`, `IDeletable`), interleaved alphabetically into the existing `Tempest.Core.EngineeringDomain` subsection (unlike `WP 8.2C`'s own disclosed bulk, non-interleaved addition — three new entries makes interleaving practical); 163 → 166 total. `IPropertyFacetProvider` (`Tempest.App.Workspace`, `WP 9.0A`) is out of this register's own scope (`src/Tempest.Core/` only) and is not listed here. Previously reviewed 2026-08-04 (WP 8.2C, Engineering Domain Implementation) — 83 new interfaces added directly at implementation time, under a new dedicated `Tempest.Core.EngineeringDomain` subsection (not interleaved into the main alphabetical table, disclosed as a pragmatic simplification for this one bulk addition); 80 → 163 total. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — 5 new interfaces added directly at implementation time (`IRequirement`, `IRequirementCollection`, `IRequirementEvidence`, `IRequirementGroup`, `IRequirementsService`), not backfilled later — the first Work Package to keep this register current with its own implementation since `WP 7.1F` established the practice. Previously reviewed 2026-07-30 (WP 7.1F, Engineering Core Integration Review & Certification) — full backfill performed; 11 interfaces introduced across all five Engineering Foundation Work Packages (`WP 7.1A`–`WP 7.1E`) are now listed, none of which had ever been recorded here — this register had gone stale since `WP 6.8` (2026-07-29), the exact drift pattern `FCR-0005` exists to catch, now found and closed by this Work Package's own certification review, mirroring `WP 6.8`'s own identical finding for the `v0.6.0` release. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — full backfill performed; every interface introduced since `WP 5.2` (`WP 6.1`, `WP 6.4`, `WP 6.5`, `WP 6.2`, `WP 6.0`, `WP 6.3`, `WP 6.7`, `WP 6.6`) is now listed, closing the gap `WP 6.7` first disclosed and `WP 6.6` left in place. |
| **Related Documents** | `docs/architecture/Ownership Matrix.md`; `Dependency Injection Register.md`; `Namespace Register.md`. |
| **Related ADRs** | ADR-0006, ADR-0009, ADR-0017, ADR-0020, ADR-0023, ADR-0024, ADR-0034, ADR-0036, ADR-0037, ADR-0039, ADR-0040–ADR-0057, ADR-0102. |
| **Related Academy Articles** | `docs/architecture/Engineering Glossary.md` (Platform API vs. Platform Service); `docs/engineering/Engineering Principles.md`. |
| **Coverage Status** | **Complete, re-verified `WP 16.2A`.** Verified directly against `grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core` — 188 interfaces found, 188 listed below (96 in the main table, 92 in the `Tempest.Core.EngineeringDomain` subsection), zero omitted, zero stale. The register's own stated grep also excludes `TempestHost.cs:564`'s "own public interface" prose, a non-anchored false hit the anchored pattern never matches. `WP 16.2A` found this register stale at 174 (undercounting by 14 net): 15 rows for `v0.13.0` Plugin Trust (`IPluginTrustStore`, `IPluginRegistry`, `IPluginRegistryRecorder`, `IPluginDeniedTypeRecorder`, `IPluginDeniedTypeRegistry`, `IPluginComponentPrincipalRecorder`, `IPluginComponentPrincipalRegistry`, `ICurrentComponentAccessor`) and `v0.14.0` Durability/Rehydration/Attachment Content (`IAttachmentContentStore`, `IBinaryPersistenceStore`, `IEngineeringObjectRehydrator`, `IEngineeringObjectRehydratorRegistry`, `IEngineeringObjectStateStore`, `IRehydratable<TSelf>`, `ISessionPrincipalSource`) were never added at implementation time; 1 stale row, `IProjectRepository` (`Tempest.Core.Repositories`), was removed — the concrete `ProjectRepository`/`IProjectRepository` pair no longer exists under `src/` (`grep -rn "IProjectRepository" src/` returns nothing), deleted by an earlier Work Package (`WP-C`) without this register being updated. The `Tempest.Core.EngineeringDomain` subsection's own header ("83 interfaces") is a second, independent, pre-existing drift found in the same pass — the subsection actually held 87 rows before this Work Package (`grep -c` on the subsection's own line range), now 92; not rewritten here beyond this disclosure, since re-titling every historical WP 8.2C reference is outside this Work Package's own scope. `WP 16.3B` (concurrent, `docs/releases/v0.16.0/v0.16.0 Release Plan.md` §`WP 16.3B`) is adding `IStateMigration`/`IStateMigrationRegistry`/`ISettingsMigration<T>` to `src/Tempest.Core/Persistence/` on its own branch — not present at this Work Package's own base commit, and therefore not counted or listed here; they are registered in this register only at `WP 16.3B`'s own merge. A genuine, pre-existing arithmetic drift was also found and corrected during the `WP 6.8` backfill: the register's own Classification Summary read "Host-owned = 6" while its own Entries table already listed 7 Host-owned rows (`IFrameworkDiscoveryService`, `IHostedServiceDiscoveryService`, `IHostedServiceManager`, `IModuleLifecycleManager`, `IPluginAssemblyLoader`, `IPluginManifestDiscoveryService`, `IRuntimeModuleManager`) — an undercount that predates `WP 6.7`'s own first disclosure of the larger gap, corrected at that time. |

---

## Entries

| Interface | Namespace | Classification | Purpose |
|---|---|---|---|
| `IApiEndpointRegistry` | `Tempest.Core.Api` | DI-public | Maps HTTP method+path to a registered command Id (`WP 6.3`) |
| `IAssessmentSubject` | `Tempest.Core.EngineeringIntelligence` | Platform API (data contract) | The narrow, typed bridge between a rule and a `P01` record — recorded properties with their availability, and applicability taken from the library's own traits table (`P02`, `ADR-0127`) |
| `IAuditQuery` | `Tempest.Core.Audit` | DI-public | Permission-gated, filtered query over recorded actions (`WP 6.5`) |
| `IAuditRecord` | `Tempest.Core.Audit` | Platform API (data contract) | The shape of one recorded action (`WP 6.5`) |
| `IAuditRecorder` | `Tempest.Core.Audit` | DI-public | Records an attributable action (`WP 6.5`) |
| `IBearingCatalog` | `Tempest.Core.Bearings` | DI-public | Register/retrieve/revise/govern/query bearing reference data — a typed index over `IEngineeringDocumentStore` (`A4`, `ADR-0124`) |
| `IBearingValidationService` | `Tempest.Core.Bearings` | DI-public | Bearing data-quality validation and the catalogue-wide data-quality report (`A4`, `ADR-0124`) |
| `IBinaryPersistenceStore` | `Tempest.Core.Persistence` | DI-public | The byte-valued counterpart of `IPersistenceStore`, same durable store/root/records, for values that are not text (`v0.14.0`, `ADR-0113`) |
| `ICalculationDefinition<TInput, TResult>` | `Tempest.Core.Calculations` | Platform API (contract, registered by Id, not itself DI-registered) | A pure, registrable calculation's own input/output/formula contract (`WP 7.1D`, `ADR-0056`) |
| `IBusinessRiskCatalog` | `Tempest.Core.BusinessGovernance.Risk` | DI-public | The organisation's own risk register — register/retrieve/revise/govern/query business risks (`C2`, `ADR-0129`) |
| `IBusinessRiskValidationService` | `Tempest.Core.BusinessGovernance.Risk` | DI-public | Governance of the risk register itself — `TEMPEST-BGR-001..013` (`C2`) |
| `ICalculationEngine` | `Tempest.Core.Calculations` | DI-public | Registration/dispatch of `ICalculationDefinition<TInput, TResult>` by Id, mirroring `ICommandRegistry`'s own shape (`WP 7.1D`, `ADR-0056`) |
| `ICommand` | `Tempest.Core.Commands` | Platform API (contract only) | Command Framework marker — dispatched by concrete type (`ICommandDispatcher`, `WP 5.1B`) |
| `ICommandDispatcher` | `Tempest.Core.Commands` | DI-public | Type-keyed handler registration/dispatch (ADR-0036/ADR-0037) |
| `ICommandHandler<T>` | `Tempest.Core.Commands` | Platform API (contract) | Consumer-facing command handler contract |
| `ICommandMacro` | `Tempest.Core.Macros` | Platform API (data contract) | An ordered, named sequence of registered Command Ids (`WP 10.6A`, `ADR-0099`) |
| `ICommandRegistry` | `Tempest.Core.Commands` | DI-public | Id-keyed command catalogue/invocation (ADR-0036/ADR-0037) |
| `IComponentCatalog` | `Tempest.Core.Components` | DI-public | Register/retrieve/revise/govern/query spring, gear, drive-element and standard-component reference data (`A5`, `ADR-0126`) |
| `IComponentValidationService` | `Tempest.Core.Components` | DI-public | Mechanical-component data-quality validation and the library-wide data-quality report (`A5`) |
| `IConfigurationProvider` | `Tempest.Core.Configuration` | DI-public (via `AddInstance`) | Read-only configuration access |
| `IConfigurationSource` | `Tempest.Core.Configuration` | Not DI-registered (input to `ConfigurationBuilder`) | A source `ConfigurationBuilder` reads |
| `IConstantCatalog` | `Tempest.Core.Constants` | DI-public | Register/retrieve/revise/govern/query engineering constants, and the released-only seam (`A6`, `ADR-0126`) |
| `IConstantValidationService` | `Tempest.Core.Constants` | DI-public | Engineering-constant data-quality validation and the library-wide data-quality report (`A6`) |
| `IContractService` | `Tempest.Core.BusinessGovernance.Contracts` | DI-public | Prepare a contract from a released template pinned to its revision, resolve that revision, and report obligations; executes nothing (`C1`, `ADR-0130`) |
| `IContractTemplateCatalog` | `Tempest.Core.BusinessGovernance.Contracts` | DI-public | The library of controlled contract templates (`C1`, `ADR-0129`) |
| `IContractTemplateValidationService` | `Tempest.Core.BusinessGovernance.Contracts` | DI-public | Governance of contract templates — completeness, never legality (`C1`) |
| `ICriticalBackgroundService` | `Tempest.Core.BackgroundServices` | Platform API (marker) | Opt-in critical-failure escalation (ADR-0021) |
| `ICurrentComponentAccessor` | `Tempest.Core.Identity` | DI-public (via `AddInstance`, dual-registered under its own concrete type, mirroring `ICurrentPrincipalAccessor`) | Resolves which loaded component's own code is currently executing — a second, independent identity axis alongside `ICurrentPrincipalAccessor` (`v0.13.0`, `ADR-0111`) |
| `ICurrentPrincipalAccessor` | `Tempest.Core.Identity` | DI-public (via `AddInstance`, dual-registered under its own concrete type per ADR-0044) | Read-only view of the ambient current principal (`WP 6.1`) |
| `IDataAssetCatalog` | `Tempest.Core.BusinessGovernance.Assets` | DI-public | The organisation's data-asset register (`C3`, `ADR-0129`) |
| `IDataAssetValidationService` | `Tempest.Core.BusinessGovernance.Assets` | DI-public | Governance of the data-asset register — records whose determination compliance is, never makes one (`C3`) |
| `IDecisionTreeCatalog` | `Tempest.Core.EngineeringIntelligence.Decisions` | DI-public | Register/retrieve/revise/govern/query manufacturing decision trees (`B2`, `ADR-0128`) |
| `IDecisionTreeValidationService` | `Tempest.Core.EngineeringIntelligence.Decisions` | DI-public | Governance of decision-tree definitions themselves — `TEMPEST-EID-001..014` (`B2`) |
| `IDesignRuleService` | `Tempest.Core.EngineeringIntelligence.DesignRules` | DI-public | Assess a subject against released design rules, stating what was and was not checked (`B3`, `ADR-0127`) |
| `IDiagnosticsProvider` | `Tempest.Core.Diagnostics` | DI-public (via `AddInstance`) | Read-only projection over Host/module/hosted-service lifecycle state (ADR-0039) |
| `IDimension` | `Tempest.Core.UnitsAndQuantities` | Platform API (generic marker, no members) | Phantom-type dimension tag for `Quantity<TDimension>`/`Unit<TDimension>` — compile-time-only, never instantiated (`WP 7.1B`, `ADR-0054`) |
| `IDocumentRevision` | `Tempest.Core.EngineeringData` | Platform API (data contract) | One immutable, retrievable revision of an `IEngineeringDocument` (`WP 7.1A`, `ADR-0053`) |
| `IEngineeringDocument` | `Tempest.Core.EngineeringData` | Platform API (data contract) | Identity and current-revision pointer for a tracked engineering entity (`WP 7.1A`, `ADR-0053`) |
| `IEngineeringDocumentStore` | `Tempest.Core.EngineeringData` | DI-public | Create/find/revise/link/query engineering documents and their references (`WP 7.1A`, `ADR-0053`) |
| `IEngineeringReviewService` | `Tempest.Core.EngineeringIntelligence.Reviews` | DI-public | Conduct an engineering review, answer what a rule can answer, and record an engineer's own findings (`B4`) |
| `IEvent` | `Tempest.Core.Events` | Platform API (contract) | Marks a published fact |
| `IEventBus` | `Tempest.Core.Events` | DI-public | Publish/subscribe dispatch (ADR-0020) |
| `IEventHandler<T>` | `Tempest.Core.Events` | Platform API (contract) | Consumer-facing subscription contract |
| `IExportFormat` | `Tempest.Core.ExportImport` | DI-public (via `AddInstance`) | Frames/reads the multi-section artifact envelope (`WP 6.7`) |
| `IExportPayloadSerializer` | `Tempest.Core.ExportImport` | Not DI-registered (optional collaborator, mirroring `IReportTemplate<T>`) | Converts a key/value data set to/from raw bytes |
| `IExportService` | `Tempest.Core.ExportImport` | DI-public | Exports one or more `IExportable` sources into a single artifact |
| `IExportable` | `Tempest.Core.ExportImport` | Platform API (contract) | Marks a source's data as exportable, round-trip-safe (ADR-0051) |
| `IExportableKind` | `Tempest.Core.ExportImport` | Platform API (optional companion contract) | Supplies a source's own stable artifact-section identifier |
| `IExternalControllerProvider` | `Tempest.Core.Input` | Platform API (contract, extends `IInputBindingProvider`) | An `IInputBindingProvider` backed by a physical external device — no production implementation ships this Work Package, only a test-only double (`WP 10.6A`, `ADR-0100`) |
| `IFastenerCatalog` | `Tempest.Core.Fasteners` | DI-public | Register/retrieve/revise/govern/query fastener reference data (`A3`, `ADR-0126`) |
| `IFastenerValidationService` | `Tempest.Core.Fasteners` | DI-public | Fastener data-quality validation and the library-wide data-quality report (`A3`) |
| `IFaultInjectionModule` | `Tempest.Core.Modules` | Platform API (marker, extends `IModule`) | Discovery-time fault-injection classification — a candidate implementing it is excluded from `ReflectionFrameworkDiscoveryService`'s discovery by default, unless the host's own builder called `EnableFaultInjectionModules()` (`WP 12.3B`, `ADR-0102`) |
| `IFinancialAssumptionCatalog` | `Tempest.Core.BusinessGovernance.Finance` | DI-public | The library of governed financial assumptions (`C5`, `ADR-0129`) |
| `IFinancialAssumptionValidationService` | `Tempest.Core.BusinessGovernance.Finance` | DI-public | Governance of financial assumptions — `TEMPEST-BGF-001..004` (`C5`) |
| `IFinancialControlService` | `Tempest.Core.BusinessGovernance.Finance` | DI-public | Compare expectation against actual, and one scenario against another; posts, recognises and computes no tax (`C5`, `ADR-0130`) |
| `IFinancialScenarioCatalog` | `Tempest.Core.BusinessGovernance.Finance` | DI-public | The library of financial scenarios (`C5`, `ADR-0129`) |
| `IFinancialScenarioValidationService` | `Tempest.Core.BusinessGovernance.Finance` | DI-public | Governance of financial scenarios — traceability, not whether the numbers are right (`C5`) |
| `IFrameworkDiscoveryService` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module Discovery |
| `IHostedService` | `Tempest.Core.BackgroundServices` | Platform API (contract) | Background service Start/Stop |
| `IHostedServiceDiscoveryService` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service discovery |
| `IHostedServiceManager` | `Tempest.Core.BackgroundServices` | Host-owned, never DI-public (ADR-0017) | Hosted service start/stop orchestration |
| `IIPAssetCatalog` | `Tempest.Core.BusinessGovernance.Assets` | DI-public | The organisation's intellectual property register (`C3`, `ADR-0129`) |
| `IIPAssetValidationService` | `Tempest.Core.BusinessGovernance.Assets` | DI-public | Governance of the IP register — reports an unevidenced ownership position, never determines ownership (`C3`) |
| `IIdentity` | `Tempest.Core.Identity` | Platform API (data contract) | The shape of a claimed identity (`WP 6.1`) |
| `IIdentityService` | `Tempest.Core.Identity` | DI-public | Establishes/resolves a principal; additive, not in the original catalogue (`WP 6.1`) |
| `IImportService` | `Tempest.Core.ExportImport` | DI-public (dual-registered under its own concrete type, mirroring `ICurrentPrincipalAccessor`) | Reads a previously exported artifact back into the owning service(s) |
| `IImportable` | `Tempest.Core.ExportImport` | Registered via `ImportService.RegisterImportable`, not itself a DI service type | Read-back counterpart to `IExportable`, routed to by `Kind` |
| `IInputBindingProvider` | `Tempest.Core.Input` | Platform API (contract) | A source of physical/virtual input that can request a registered Command Id be invoked (`WP 10.6A`, `ADR-0100`) |
| `IInputBindingRegistry` | `Tempest.Core.Input` | DI-public | Tracks every registered `IInputBindingProvider`, routing each one's own request to `ICommandRegistry.InvokeAsync` (`WP 10.6A`, `ADR-0100`) |
| `IInsurancePolicyCatalog` | `Tempest.Core.BusinessGovernance.Risk` | DI-public | The library of insurance policies the organisation holds (`C2`, `ADR-0129`) |
| `IInsurancePolicyValidationService` | `Tempest.Core.BusinessGovernance.Risk` | DI-public | Governance of policy records — whether cover could be demonstrated, never what the wording means (`C2`) |
| `IIssuedContractCatalog` | `Tempest.Core.BusinessGovernance.Contracts` | DI-public | The library of contracts the organisation has issued or entered into (`C1`, `ADR-0129`) |
| `IIssuedContractValidationService` | `Tempest.Core.BusinessGovernance.Contracts` | DI-public | Governance of issued contracts — `TEMPEST-BGC-007..022` (`C1`) |
| `ILicense` | `Tempest.Core.Licensing` | Platform API (contract) | A single, validated, immutable license |
| `ILicenseProvider` | `Tempest.Core.Licensing` | DI-public (via `AddInstance`) | Read-only, post-validation view of the current license |
| `ILicenseValidator` | `Tempest.Core.Licensing` | Not DI-registered (Composition-Root-constructed, pre-container leaf, mirroring `IPlatformVersionProvider`) | Validates a license at Host startup, before the container exists |
| `ILogSink` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Log entry destination |
| `ILogger` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Structured logging abstraction |
| `ILoggerFactory` | `Tempest.Core.Logging` | DI-public (via `AddInstance`) | Produces `ILogger` instances |
| `IMacroManager` | `Tempest.Core.Macros` | DI-public | Creates/lists/deletes `ICommandMacro`s, keeping each one's own `CommandDescriptor` registered against `ICommandRegistry` (`WP 10.6A`, `ADR-0099`) |
| `IManufacturingDecisionService` | `Tempest.Core.EngineeringIntelligence.Decisions` | DI-public | Screen processes against part requirements and walk released decision trees (`B2`) |
| `IMaterialCatalog` | `Tempest.Core.Materials` | DI-public | Register/find/revise/list named materials — a thin, typed index over `IEngineeringDocumentStore` (`WP 7.1C`, `ADR-0055`) |
| `IMaterialSelectionService` | `Tempest.Core.EngineeringIntelligence.MaterialSelection` | DI-public | Assess material candidates against an application's constraints and preferences; never selects (`B1`, `ADR-0127`) |
| `IMaterialValidationService` | `Tempest.Core.Materials` | DI-public | Material data-quality validation and the library-wide data-quality report (`A1`, `ADR-0126`) |
| `IModule` | `Tempest.Core.Modules` | Discovered/registered, not DI-registered as an interface | Module identity contract |
| `IModuleLifecycle` | `Tempest.Core.Modules` | Discovered/registered | Module lifecycle contract |
| `IModuleLifecycleManager` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module lifecycle orchestration |
| `INavigationProvider` | `Tempest.Core.Navigation` | DI-public | Navigation registry + `Navigate` (ADR-0031/ADR-0032) |
| `INotification` | `Tempest.Core.Notifications` | Platform API (contract) | Marks a published notification (`WP 6.2`) |
| `INotificationDispatcher` | `Tempest.Core.Notifications` | DI-public | Subscribes and publishes notifications, isolating subscriber failures (`WP 6.2`) |
| `INotificationHandler<T>` | `Tempest.Core.Notifications` | Platform API (contract) | Consumer-facing subscription contract (`WP 6.2`) |
| `IOperatingScenarioCatalog` | `Tempest.Core.BusinessGovernance.Operating` | DI-public | The library of operating models and scale scenarios (`C7`, `ADR-0129`) |
| `IOperatingScenarioValidationService` | `Tempest.Core.BusinessGovernance.Operating` | DI-public | Governance of operating models, including reporting a met decision gate as a finding for a person (`C7`, `ADR-0130`) |
| `ISupplierCatalog` | `Tempest.Core.CommercialIntelligence.Suppliers` | DI-public | The supplier database, with lookup by reference and search by capability, status and geography (`D1`, `ADR-0131`) |
| `ISupplierValidationService` | `Tempest.Core.CommercialIntelligence.Suppliers` | DI-public | Governance of the supplier database, including reporting a possible duplicate rather than merging it (`D1`, `ADR-0131`) |
| `ISupplierIdentityService` | `Tempest.Core.CommercialIntelligence.Suppliers` | DI-public | Compares two supplier identities and reports what it found; has no merge operation (`D1`, `ADR-0131`) |
| `IProcessCostCatalog` | `Tempest.Core.CommercialIntelligence.Costs` | DI-public | The process-cost library, with lookup by reference and applicability search by process, supplier, quantity and date (`D2`, `ADR-0132`) |
| `IProcessCostValidationService` | `Tempest.Core.CommercialIntelligence.Costs` | DI-public | Governance of the cost library, including contradicted figures and components that do not sum (`D2`, `ADR-0132`) |
| `ILeadTimeCatalog` | `Tempest.Core.CommercialIntelligence.LeadTimes` | DI-public | The lead-time library, returning applicable records strongest claim first (`D3`, `ADR-0133`) |
| `ILeadTimeValidationService` | `Tempest.Core.CommercialIntelligence.LeadTimes` | DI-public | Governance of the lead-time library, including a historical average drawn from too few orders (`D3`, `ADR-0133`) |
| `ICostEstimateCatalog` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | The organisation's own estimates, searchable by the record revisions they cite (`D4`, `ADR-0134`) |
| `ICostEstimateValidationService` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | Governance of estimates, including reporting a pinned source that has since been superseded without altering the estimate (`D4`, `ADR-0134`) |
| `IReferencePinResolver` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | Resolves a `ReferencePin` into one library, so an estimate's sources can be checked without `D4` depending on every library an estimate might cite (`D4`, `ADR-0134`) |
| `ISupplierQuoteCatalog` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | What suppliers have offered, searchable by supplier, firmness and whether the quote still binds (`D4`, `ADR-0134`) |
| `ISupplierQuoteValidationService` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | Governance of supplier quotes, including a firm quote with no period over which the price is held (`D4`, `ADR-0134`) |
| `ICustomerQuotationCatalog` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | What the organisation has offered, searchable by status and whether the offer still stands (`D4`, `ADR-0134`) |
| `ICustomerQuotationValidationService` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | Governance of customer quotations, including the error raised where an issued offer names nobody who issued it (`D4`, `ADR-0135`) |
| `IEstimatingService` | `Tempest.Core.CommercialIntelligence.Estimating` | DI-public | Builds an estimate from released cost and lead-time records, pinning each, and re-reads those pins to report where the libraries have moved (`D4`, `ADR-0134`) |
| `ISourcingRequirementCatalog` | `Tempest.Core.CommercialIntelligence.Procurement` | DI-public | What the organisation needs sourced and how it intends to judge candidates (`D5`, `ADR-0135`) |
| `ISourcingRequirementValidationService` | `Tempest.Core.CommercialIntelligence.Procurement` | DI-public | Governance of sourcing requirements, including a comparison dominated by one criterion (`D5`, `ADR-0135`) |
| `ISourcingComparisonCatalog` | `Tempest.Core.CommercialIntelligence.Procurement` | DI-public | What the organisation compared and what it decided, including the queue awaiting a person (`D5`, `ADR-0135`) |
| `ISourcingComparisonValidationService` | `Tempest.Core.CommercialIntelligence.Procurement` | DI-public | Governance of comparisons, including the error raised where a recorded procurement decision names nobody who took it (`D5`, `ADR-0135`) |
| `ISourcingComparisonService` | `Tempest.Core.CommercialIntelligence.Procurement` | DI-public | Ranks assessed candidates deterministically, never scoring absent information as zero, and recommends without deciding (`D5`, `ADR-0135`) |
| `IOpportunityCatalog` | `Tempest.Core.BusinessGovernance.Development` | DI-public | The organisation's opportunity pipeline (`C6`, `ADR-0129`) |
| `IOpportunityValidationService` | `Tempest.Core.BusinessGovernance.Development` | DI-public | Governance of the pipeline — `TEMPEST-BGD-001..011` (`C6`) |
| `IPermissionEvaluator` | `Tempest.Core.Identity` | DI-public | The single authorization enforcement point (`WP 6.1`, ADR-0044) |
| `IPersistenceStore` | `Tempest.Core.Persistence` | DI-public | Internal, platform-owned key-value/document storage (`WP 6.4`, ADR-0041) |
| `IPipelineService` | `Tempest.Core.BusinessGovernance.Development` | DI-public | Report the pipeline, keeping contracted and potential revenue apart and producing no weighted figure (`C6`, `ADR-0130`) |
| `IPlatformNotification` | `Tempest.Core.Notifications` | Platform API (additive general-purpose shape, extends `INotification` and `Events.IEvent`) | Severity/category-bearing general-purpose notification (`WP 6.2`) |
| `IPlatformVersionProvider` | `Tempest.Core.Versioning` | DI-public (via `AddInstance`) | Platform version query |
| `IPluginAssemblyLoader` | `Tempest.Core.Plugins` | Host-owned, never DI-public (ADR-0017 extended) | Plugin assembly loading |
| `IPluginComponentPrincipalRecorder` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, mirroring `IPluginRegistry`) | The write side of the small registry mapping a discovered `IModule`/`IHostedService` `Type` back to the plugin's own component principal (`v0.13.0`, `ADR-0111`) |
| `IPluginComponentPrincipalRegistry` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, mirroring `IPluginRegistry`) | The read side of the same registry (`v0.13.0`, `ADR-0111`) |
| `IPluginDeniedTypeRecorder` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, mirroring `IPluginRegistry`) | The write side of the small registry recording every discovered `IModule`/`IHostedService` denied at Discovery time and why (`v0.13.0`, `ADR-0110`) |
| `IPluginDeniedTypeRegistry` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, mirroring `IPluginRegistry`) | The read side of the same registry (`v0.13.0`, `ADR-0110`) |
| `IPluginManifestDiscoveryService` | `Tempest.Core.Plugins` | Host-owned, never DI-public (ADR-0017 extended) | Plugin manifest discovery |
| `IPluginRegistry` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, before the Phase 6 DI container exists) | The read side of the Plugin Registry — the queryable catalogue of every plugin candidate a run attempted, and its outcome (`v0.13.0`, `ADR-0107`) |
| `IPluginRegistryRecorder` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, mirroring `IPluginRegistry`) | The write side of the Plugin Registry, used only by Plugins-owned discovery/loading services (`v0.13.0`, `ADR-0107`) |
| `IPluginTrustStore` | `Tempest.Core.Plugins` | Host-owned, never DI-public (constructed directly during bootstrap, mirroring `IPluginRegistry`) | The local trust store of publisher certificates plugin signatures are verified against (`v0.13.0`, `ADR-0112`) |
| `IPricingService` | `Tempest.Core.BusinessGovernance.Pricing` | DI-public | Quote from a released, approved rate card, and reproduce a historical quotation from its pin (`C4`, `ADR-0130`) |
| `IPrincipal` | `Tempest.Core.Identity` | Platform API (data contract) | The shape of an authenticated/established identity plus its roles (`WP 6.1`) |
| `IProcessCatalog` | `Tempest.Core.Manufacturing` | DI-public | Register/retrieve/revise/govern/query manufacturing process reference data (`A7`, `ADR-0126`) |
| `IProcessValidationService` | `Tempest.Core.Manufacturing` | DI-public | Manufacturing-process data-quality validation and the library-wide data-quality report (`A7`) |
| `IReferenceDataCatalog<TDefinition>` | `Tempest.Core.ReferenceData` | Platform API (contract, implemented per library, not itself DI-registered) | The register/retrieve/revise/govern/supersede operations every `Group A` reference library shares (`ADR-0126`) |
| `IReferenceRecord<TDefinition>` | `Tempest.Core.ReferenceData` | Platform API (data contract) | One registered reference record: its domain engineering description plus its catalogue governance (`ADR-0126`) |
| `IReferenceValidationService<TDefinition>` | `Tempest.Core.ReferenceData` | Platform API (contract, implemented per library, not itself DI-registered) | The data-quality validation surface every `Group A` reference library offers (`ADR-0126`) |
| `IRateCardCatalog` | `Tempest.Core.BusinessGovernance.Pricing` | DI-public | The library of published, effective-dated rate cards (`C4`, `ADR-0129`) |
| `IRateCardValidationService` | `Tempest.Core.BusinessGovernance.Pricing` | DI-public | Governance of rate cards — usability and period collision, never whether a price is right (`C4`) |
| `IReleasedConstantSource` | `Tempest.Core.ReferenceData` | DI-public | The narrow seam a calculation consumes a constant through — hands back nothing until a record is Released (`A6`) |
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
| `IReviewDefinitionCatalog` | `Tempest.Core.EngineeringIntelligence.Reviews` | DI-public | Register/retrieve/revise/govern/query engineering review definitions (`B4`, `ADR-0128`) |
| `IReviewDefinitionValidationService` | `Tempest.Core.EngineeringIntelligence.Reviews` | DI-public | Governance of review definitions themselves — `TEMPEST-EIV-001..009` (`B4`) |
| `IRiskAndInsuranceService` | `Tempest.Core.BusinessGovernance.Risk` | DI-public | Report what the records support about a risk's insurance position and the register's own state; accepts no risk and asserts no cover (`C2`, `ADR-0130`) |
| `IRole` | `Tempest.Core.Identity` | Platform API (data contract, additive — `WP 6.1`) | A named grouping of permissions |
| `IRoleProvider` | `Tempest.Core.Identity` | DI-public (additive — `WP 6.1`) | Config-sourced role resolution |
| `IRuleCatalog` | `Tempest.Core.EngineeringIntelligence` | DI-public | Register/retrieve/revise/govern/query engineering design rules, and find the released rules applicable to a subject (`P02`, `ADR-0128`) |
| `IRuleValidationService` | `Tempest.Core.EngineeringIntelligence` | DI-public | Governance of rule definitions themselves — `TEMPEST-EIR-001..016` (`P02`) |
| `IRuntimeModuleManager` | `Tempest.Core.Modules` | Host-owned, never DI-public (ADR-0017) | Module registration catalogue |
| `IServiceCollection` | `Tempest.Core.DependencyInjection` | Composition-time only (not itself registered) | DI registration accumulation |
| `ISessionPrincipalSource` | `Tempest.Core.Identity` | Not DI-registered (consumed directly by the composition root, `Tempest.Desktop`) | The one seam a future Administration identity/roles authority implements; today satisfied by `LocalSessionPrincipalSource`, a single-user desktop stand-in — explicitly not authentication (`v0.14.0`) |
| `ISettingDefinition` | `Tempest.Core.Settings` | Platform API (data contract) | Identifies a registrable setting (`WP 6.4`) |
| `ISettingsChangedEvent` | `Tempest.Core.Settings` | Platform API (contract, an `IEvent`) | Published through the Event Bus on a setting value change (`WP 6.4`) |
| `ISettingsProvider` | `Tempest.Core.Settings` | DI-public | Reads/writes runtime-mutable setting values (`WP 6.4`) |
| `ISettingsMigration<TDocument>` | `Tempest.Core.Settings` | DI-public | One ordered step of a `SettingsDocument<TDocument>` schema-migration chain, applied on read only (`WP 16.3B`, `ADR-0120`) |
| `IStandardCatalog` | `Tempest.Core.Standards` | DI-public | Register/retrieve/revise/govern/query engineering standards (`A2`, `ADR-0126`) |
| `IStandardResolver` | `Tempest.Core.ReferenceData` | DI-public | The narrow seam a citing library confirms its own standard citations resolve through, without depending on `A2` (`A2`) |
| `IStandardValidationService` | `Tempest.Core.Standards` | DI-public | Standards-register data-quality validation and the register-wide data-quality report (`A2`) |
| `ITempestHost` | `Tempest.Core.Runtime` | Not DI-registered (returned by the builder) | The running Host instance |
| `ITempestHostBuilder` | `Tempest.Core.Runtime` | Not DI-registered (the composition root's own entry point) | Assembles and produces a `TempestHost`; also opts the resulting host's Discovery phase into `IFaultInjectionModule` candidates via the new `EnableFaultInjectionModules()` member (`WP 12.3B`, `ADR-0102`) — every other member unchanged |
| `ITempestServiceProvider` | `Tempest.Core.DependencyInjection` | The container itself | Constructs and resolves service instances |
| `ITradeStudyCatalog` | `Tempest.Core.EngineeringIntelligence.TradeStudies` | DI-public | Register/retrieve/revise/govern/query engineering trade-study definitions (`B5`, `ADR-0128`) |
| `ITradeStudyService` | `Tempest.Core.EngineeringIntelligence.TradeStudies` | DI-public | Run a trade study, record an engineer's judgement, and attach the decision a person took; nothing here chooses an option (`B5`, `ADR-0127`) |
| `ITradeStudyValidationService` | `Tempest.Core.EngineeringIntelligence.TradeStudies` | DI-public | Governance of trade-study definitions themselves — `TEMPEST-EIT-001..017` (`B5`) |
| `IUnitConverter` | `Tempest.Core.UnitsAndQuantities` | Not DI-registered (each `Unit<TDimension>` carries its own conversion factor; no registration/lookup service exists) | Reserved conversion-service contract; the framework's own actual conversion path is `Quantity<TDimension>.ConvertTo`, not this interface (`WP 7.1B`, `ADR-0054`) |
| `IVerificationRecord` | `Tempest.Core.Verification` | Platform API (data contract) | The complete, structured account of one recorded verification outcome (`WP 7.1E`, `ADR-0057`) |
| `IVerificationService` | `Tempest.Core.Verification` | DI-public | Records a verification outcome against a subject document; permission-gated history query (`WP 7.1E`, `ADR-0057`) |

**Total: 272 public interfaces under `src/Tempest.Core/` (271 distinct names — `IRequirement` is declared in two namespaces) — re-derived at `Group D` (2026-09-06) by the Interface Register check in `scripts/governance-healthcheck.ps1`, which compares every declared interface name against this table's own rows. `Group D` adds twenty: three for `D1`, two each for `D2` and `D3`, eight for `D4` and five for `D5`. No existing row changed. Previously 252 public interfaces (251 distinct names) — re-derived at `Group C` (2026-09-06) by the Interface Register check in `scripts/governance-healthcheck.ps1`, which reported 252 declared against 225 rows. `Group C` adds the 27 rows the gap named: the eleven `P07` catalogues, their eleven validation services and the five reasoning services (`IContractService`, `IRiskAndInsuranceService`, `IPricingService`, `IFinancialControlService`, `IPipelineService`) — the whole of `P07`'s public surface. Historic narrative, stated when the total was 225: re-derived at `Group B` (2026-09-06) against the repository itself, by the Interface Register check in `scripts/governance-healthcheck.ps1`, which reported 225 declared against 211 rows. `Group B` adds the 14 rows the gap named: `IRuleCatalog`, `IRuleValidationService`, `IAssessmentSubject`, `IDecisionTreeCatalog`, `IDecisionTreeValidationService`, `IManufacturingDecisionService`, `IMaterialSelectionService`, `IDesignRuleService`, `IReviewDefinitionCatalog`, `IReviewDefinitionValidationService`, `IEngineeringReviewService`, `ITradeStudyCatalog`, `ITradeStudyValidationService` and `ITradeStudyService` — the whole of `P02`'s public surface. Historic narrative, stated when the total was 195: corrected at the `WP 16.4B-R2` integration (2026-09-05): `IAttachmentWriteIntentStore` added (`TD-97`); corrected at the `WP 16.4B` integration (2026-09-05): `IAttachmentContentReconciliationService`, `IMaterialCatalogReconciliationService`, `IRequirementsReconciliationService` added (`TD-67`, `TD-97`); corrected at the `WP 16.3B` integration (2026-09-04): `IStateMigration`, `IStateMigrationRegistry`, `ISettingsMigration<TDocument>` added (`ADR-0120`); previously corrected
`WP 16.2A` (174 → 188: 15 rows backfilled for `v0.13.0` Plugin Trust and
`v0.14.0` Durability/Rehydration/Attachment Content, none added at their
own implementation time; 1 stale row, `IProjectRepository`, removed —
its concrete implementation no longer exists under `src/`; see this
register's own **Last Reviewed** field and
`docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`
for the full derivation). Previously corrected
`WP 12.3B` (173 → 174, `IFaultInjectionModule`, `Tempest.Core.Modules`,
new — `ADR-0102`; `ITempestHostBuilder` gained one new member,
`EnableFaultInjectionModules()`, own row updated in place, no new row).
Verified directly (`grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core`
returns exactly 174 matches). Previously 173, `WP 10.6A`'s own five new `Tempest.Core.Macros`/
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
| `IAttachmentContentStore` | `Tempest.Core.EngineeringDomain` | DI-public | The durable store of attachment *bytes* — what makes an attached file a file this platform holds rather than a description of one (`v0.14.0`, `ADR-0114`) |
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
| `IEngineeringObjectRehydrator` | `Tempest.Core.EngineeringDomain` | Discovered/registered but not itself a DI registration target (registered by Kind with `IEngineeringObjectRehydratorRegistry`, mirroring `ICalculationDefinition<TInput, TResult>`'s own pattern) | Reconstructs one Kind's canonical objects from persisted state — the rehydrating counterpart of `IEngineeringObjectFactory` (`v0.14.0`, `TD-85`, `ADR-0116`) |
| `IEngineeringObjectRehydratorRegistry` | `Tempest.Core.EngineeringDomain` | DI-public | The Kind-to-rehydrator map startup rehydration resolves through (`v0.14.0`, `TD-85`, `ADR-0116`) |
| `IStateMigration` | `Tempest.Core.EngineeringDomain` | DI-public | One ordered step of an `EngineeringObjectState` schema-migration chain: `Kind` (or null for every Kind), `FromVersion`, `Migrate` (`WP 16.3B`, `ADR-0120`) |
| `IStateMigrationRegistry` | `Tempest.Core.EngineeringDomain` | DI-public | Registry of `IStateMigration` steps, common chain and per-Kind chains, consulted by `EngineeringObjectStateStore`'s read path (`WP 16.3B`, `ADR-0120`) |
| `IAttachmentContentReconciliationService` | `Tempest.Core.EngineeringDomain` | DI-public | Detects, and on request collects, attachment content records that no live attachment Id references — the sweep `TD-97` named as the closing action (`WP 16.4B`, `ADR-0114` unchanged). Explicit `DetectAsync`/`SweepAsync` only; nothing invokes it automatically |
| `IAttachmentWriteIntentStore` | `Tempest.Core.EngineeringDomain` | DI-public | Records that an attachment's content write is in flight, so the reconciliation sweep never collects bytes whose owning object state has not landed yet (`WP 16.4B-R2`, `TD-97`). Marked before the content write, cleared after the state write; the sweep samples it **between** its content-key read and its object-state read, which is what makes the ordering airtight rather than merely narrow |
| `IMaterialCatalogReconciliationService` | `Tempest.Core.Materials` | DI-public | Detects, and on request repairs, backing documents with no catalogue index entry and index entries with no document — half of the reconcile/repair path `TD-67` named as absent (`WP 16.4B`) |
| `IRequirementsReconciliationService` | `Tempest.Core.Requirements` | DI-public | Detects, and on request repairs, orphaned requirement/collection/group documents against the identifier index and the collection and group registries — the other half of `TD-67`'s reconcile/repair path (`WP 16.4B`) |
| `IEngineeringObjectRepository` | `Tempest.Core.EngineeringDomain` | DI-public | The new, in-memory, Kind-queryable object index (`WP 8.2C`, `ADR-0077`) |
| `IEngineeringObjectStateStore` | `Tempest.Core.EngineeringDomain` | DI-public | The durable store of `EngineeringObjectState` — what makes an engineering object survive a process restart (`v0.14.0`, `TD-85`, `ADR-0113`) |
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
| `IRehydratable<TSelf>` | `Tempest.Core.EngineeringDomain` | Platform API (`static abstract` facet contract, not itself DI-registered) | A canonical object type's own reconstruction from its persisted `EngineeringObjectState` — the symmetric other half of `EngineeringObjectBase.CaptureTypeState` (`v0.14.0`, `TD-85`, `ADR-0116`) |
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

**`WP 16.2A` disclosure.** This section has not been updated since
`WP 8.2C` (2026-08-04) and still reads against a 163-interface register;
the main table above has since grown to 188 through eleven later
Work Packages' own additions, none of which updated this running
summary — a second, larger instance of the same pre-existing-drift
pattern this section's own next paragraph already discloses for `WP 6.8`.
`WP 16.2A`'s own scope (re-deriving the register's total and adding its
15 missing rows) did not extend to a full per-row re-classification of
all 188 entries; that remediation is left for a future review. For the
15 rows this Work Package added: 5 are DI-public (`IBinaryPersistenceStore`,
`ICurrentComponentAccessor`, `IAttachmentContentStore`,
`IEngineeringObjectRehydratorRegistry`, `IEngineeringObjectStateStore`),
7 are Host-owned, never DI-public (`IPluginTrustStore`, `IPluginRegistry`,
`IPluginRegistryRecorder`, `IPluginDeniedTypeRecorder`,
`IPluginDeniedTypeRegistry`, `IPluginComponentPrincipalRecorder`,
`IPluginComponentPrincipalRegistry`), 1 is Discovered/registered but not
itself a DI registration target (`IEngineeringObjectRehydrator`), 1 is
Platform API/contract only (`IRehydratable<TSelf>`), and 1 is
Composition-time/not-DI-registered infrastructure
(`ISessionPrincipalSource`) — see each row's own **Classification**
cell above for the source. The removed stale row (`IProjectRepository`)
had counted toward "Pre-module-pipeline, outside the platform-service
model", now 0 by removal, not by re-derivation of the bucket below.

**Reflects all 163 interfaces now listed above** *(as of `WP 8.2C`; not
current — see the `WP 16.2A` disclosure immediately above)*. This section's own
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

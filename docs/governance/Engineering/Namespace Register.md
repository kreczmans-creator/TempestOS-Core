# Namespace Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Namespace Register |
| **Purpose** | The index of every namespace under `src/`, its owning project, file count, and purpose — so a reader can find "where does X live" without grepping the tree. |
| **Scope** | Every `namespace` declaration under `src/Tempest.Core/`, `src/Tempest.App/`, `src/Samples/Tempest.Samples/`, and, since `WP 12.3B`, `src/Validation/Tempest.Validation/`. **`src/Tempest.Desktop/` is deliberately not in scope** — a pre-existing gap (not introduced by, and not deepened by, `WP 12.0B`'s own new `Tempest.Desktop.Composition` namespace), explicitly disclosed and left open rather than silently extended; see this field's own `WP 12.0B` (follow-up) entry below for the full reasoning. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct source inspection (`grep -rhoP "^namespace" src/`). |
| **Review Frequency** | Updated whenever a new namespace is introduced under `src/`. |
| **Last Reviewed** | 2026-09-04 (WP 16.2A, Register and Status Currency) — **full re-derivation, the first since `WP 6.6`.** Every row's file count re-derived directly (`grep -rl "^namespace X;" src/`); 12 wholly new namespace rows added (`v0.7.0`/`v0.8.0`/`v0.9.0`/`v0.10.0` disciplines and Workspace-layer namespaces never previously recorded); `Tempest.App.Shell` corrected from "0 — retired" to its real, revived 10-file count; `Tempest.Core.Modules` 23 → 24, `Tempest.Core.Plugins` 13 → 39 (`v0.13.0` Plugin Trust), `Tempest.Samples` 46 → 82, `Tempest.Core.Identity` 18 → 21, `Tempest.Core.Persistence` 4 → 5, `Tempest.Core.Commands` 14 → 20, and every other row re-verified. Total corrected 29 → 46 namespaces, 272 → 713 files (declared in-scope), with `src/Tempest.Desktop/` (88 files, 9 namespaces) and `src/`-wide totals (61 namespaces, 802 files) now stated explicitly in a new disclosure rather than left implicit. See `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md` for the full derivation. Previously reviewed 2026-08-12 (WP 12.0B follow-up, Desktop Composition Root Decomposition Implementation — Governance Reconciliation) — **narrow correction only, not a full re-derivation; no new row added**. Closes Finding 3 of `WP 12.0B`'s own architecture/code review: `Desktop Composition Architecture.md`'s own "Documentation Impact" section (written `WP 12.0A`) had stated this register was "`WP 12.0B`'s own obligation, once real collaborator types exist to record" — factually true real collaborator types now exist (`Tempest.Desktop.Composition`, nine files; new files added to the already-existing `Tempest.App.Workspace`/`.Mechanical`/`.Requirements`/`.Calculations`/`.Documents`/`.Verification`/`.Manufacturing` namespaces for the six `EngineeringCockpit` read-model collaborators plus `CockpitFormatting`) — but this register's own declared **Scope**, above, has never covered `src/Tempest.Desktop/` at all, for any namespace, at any prior Work Package; adding a row for only the newest `Tempest.Desktop` namespace while its eight sibling namespaces (`Docking`, `Editors`, `History`, `Input`, `Tasks`, `Theming`, `Views`, `DigitalThread`, and the root `Tempest.Desktop` namespace itself) remain untracked would not close the real gap, only paper over one small piece of it inconsistently. **Decision, made explicitly rather than silently**: `Tempest.Desktop`'s own namespaces remain out of this register's scope for now — extending scope to cover an entire second project's worth of namespaces is a separate, substantial undertaking (a full `src/Tempest.Desktop/` namespace audit), not a narrow correction a single implementation-follow-up Work Package should absorb as a side effect, mirroring this exact register's own established precedent immediately below (`WP 11.3B`'s own disclosed-not-fixed staleness) and `WP 11.4A`'s own precedent for scoping what is and is not a given Work Package's job to fix. **Separately confirmed, and also disclosed rather than silently accepted**: the six `Tempest.App.Workspace.*` discipline namespaces `WP 12.0B` added new files to are themselves already part of this register's own pre-existing, larger, undisclosed-until-now gap — `src/Tempest.App/` is declared in scope, above, yet no `Tempest.App.Workspace` row of any kind (Mechanical, Requirements, Calculations, Documents, Verification, Manufacturing, or the parent `Tempest.App.Workspace` namespace itself) has ever appeared in this register, confirmed by direct `grep`, predating `WP 12.0B` entirely (these namespaces were introduced `WP 9.0A`–`WP 9.5A`). `WP 12.0B`'s own new files land inside an already-untracked namespace and do not deepen this gap — recorded here as a real, standing, pre-existing omission this register should close in a future dedicated pass, not attempted here. **`Desktop Composition Architecture.md`'s own "Documentation Impact" section has been corrected in place** to reflect this reconciled outcome rather than its own prior, now-inaccurate "`WP 12.0B`'s own obligation" phrasing; `docs/releases/v0.12.0/WorkPackages.md`'s `WP 12.0B` row and the `WP12.0B` Academy retrospective are both updated to match, so all four documents now agree. Previously reviewed 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — **narrow correction only, not a full re-derivation**: added the `Tempest.Validation.FaultInjection` row (new project, ADR-0102); corrected the `Tempest.Samples` row's own file count (47 → 46, one file moved out) and member list (`DuplicateNavigationSampleModule` removed — moved to `Tempest.Validation.FaultInjection`, renamed `DuplicateNavigationModule`). Every other row carried forward unverified, same disclosed staleness as every prior pass below. Previously reviewed 2026-08-11 (WP 11.3B, Presentation Strategy Implementation) — **narrow correction only, not a full re-derivation**: the `Tempest.App.Shell` row updated to reflect `TempestShell`/`IPage`/`PlaceholderPage`'s retirement (dead code since `ADR-0068`, removed `WP 11.3B`, `ADR-0101`). Every other row is carried forward unverified from its own last review and is known to be stale for unrelated reasons — this register has not had a full pass since `WP 6.6` (2026-07-29), predating the entire Engineering Foundation, Engineering Workspace, Mechanical Foundation, and User Experience & Desktop Application phases; a full re-derivation is a separate, substantial undertaking outside this Work Package's own scope, named here rather than silently left implicit. Previously reviewed 2026-07-29 (WP 6.6, Licensing) — added `Tempest.Core.Licensing`; every row's own file count re-derived directly again (`grep -rl "^namespace X;"`), consistent with `WP 6.0`'s/`WP 6.2`'s/`WP 6.3`'s/`WP 6.7`'s own prior passes. |
| **Related Documents** | `docs/architecture/Engineering Glossary.md` (`Tempest.Core.Runtime` vs. `Tempest.Core.Hosting`, ADR-0016); `Interface Register.md`; `Exception Register.md`. |
| **Related ADRs** | ADR-0016, ADR-0024, ADR-0036, ADR-0037, ADR-0038, ADR-0039, ADR-0040, ADR-0046, ADR-0047, ADR-0048, ADR-0049, ADR-0050, ADR-0051, ADR-0102, ADR-0103 (`Tempest.Desktop.Composition`'s own governing decision — see this register's own `WP 12.0B` follow-up entry, above, for why no row was added despite it). |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/06-platform-layering.md`. |
| **Coverage Status** | **Complete for declared scope, re-verified `WP 16.2A`.** 46 of 46 in-use namespaces under `src/Tempest.Core/`, `src/Tempest.App/`, `src/Samples/Tempest.Samples/`, `src/Validation/Tempest.Validation/` are listed (713 files), zero omitted. `src/Tempest.Desktop/` (88 files, 9 namespaces) remains explicitly out of scope, per the Scope field and the disclosure below the Entries table. |

---

## Entries

**`WP 16.2A` re-derivation.** Every file count below is re-derived
directly, per namespace, via `grep -rl "^namespace X;" src/` at this
Work Package's own base commit — not incremented from any prior figure.
This register had not had a full pass since `WP 6.6` (2026-07-29); every
row's count is corrected, 12 wholly new namespace rows are added
(`Tempest.Core.Calculations`, `.Materials`, `.UnitsAndQuantities`,
`.EngineeringData`, `.EngineeringDomain`, `.Requirements`,
`.Verification`, `.Macros`, `.Input` — `v0.7.0`/`v0.8.0`;
`Tempest.App.Composition`, `.Projects` — `v0.8.0`/`v0.9.0`; and the six
`Tempest.App.Workspace`/`.Workspace.*` rows below, `v0.8.0`–`v0.9.0`),
and `Tempest.App.Shell` is corrected from "0 — retired" to its real,
revived 10-file count (`ADR-0103`, `Tempest.App`/`WorkspaceShell`'s own
Internal Engineering Harness re-purposing gave this namespace new,
unrelated members after its `WP 11.3B` retirement — `IShellNavigator`,
`ShellNavigator`, `ShellLocation`, `ProjectArea` and others — never
reflected here until now).

| Namespace | Project | File Count | Purpose | Introduced |
|---|---|---|---|---|
| `Tempest.Core.Modules` | Tempest.Core | 24 | Discovery, Registration, Lifecycle, Module SDK, `ModuleMetadataAttribute` | WP 2.1–2.3, extended WP 4.1, WP 4.4B |
| `Tempest.Core.Plugins` | Tempest.Core | 39 | Plugin manifest, discovery, loading (WP 4.2); Plugin Trust & Capability Enforcement — trust store, tiers, signature verification, dependency graph, denied-type/component-principal registries (`v0.13.0`, `ADR-0107`–`ADR-0112`) | WP 4.2, extended `v0.13.0` |
| `Tempest.Core.DependencyInjection` | Tempest.Core | 14 | Custom DI container | WP 2.4; `ServiceProviderExtensions` — the public `GetService<T>()` convenience — removed `WP-F` (`TD-114`, `F-15`): zero production callers, 32 test call sites, and production resolved through `GetService(typeof(T))` in 40 places regardless. A public API on a plugin-hosting assembly that only tests used; the tests were migrated to the form production already used rather than production migrated to it |
| `Tempest.Core.Logging` | Tempest.Core | 10 | `ILogger`, sinks, factory, `CompositeLogSink` | WP 2.6, extended WP 5.2; the legacy `LoggingService` removed `WP-C` (`TD-01`) |
| `Tempest.Core.Configuration` | Tempest.Core | 9 | Configuration sources, builder, provider | WP 2.5; the legacy `ConfigurationService` and the `ApplicationConfiguration` it returned both removed `WP-C` (`TD-110`) |
| `Tempest.Core.BackgroundServices` | Tempest.Core | 9 | Hosted service contracts, discovery, orchestration | WP 4.0 (contracts), WP 4.5 (infrastructure) |
| `Tempest.Core.Runtime` | Tempest.Core | 7 | `TempestHost`, `TempestHostBuilder`, `HostState` | WP 2.7B; distinct from `Tempest.Core.Hosting` per ADR-0016 |
| `Tempest.Core.Events` | Tempest.Core | 4 | `IEvent`, `IEventHandler<T>`, `IEventBus`, `EventBus` | WP 4.0 (contracts), WP 4.4D (bus) |
| `Tempest.Core.Navigation` | Tempest.Core | 7 | `NavigationItem`, `INavigationProvider`/`NavigationService`, `NavigationRequestedEvent`, `NavigationException` and two subtypes | WP 5.0A (design), WP 5.0B (implementation) |
| `Tempest.Samples` | Tempest.Samples | 82 | Representative-data sample modules exercising every Platform Service and Engineering discipline — one module family per `v0.4.0`–`v0.9.0` capability (Clock, Navigation, Command, Diagnostics, Identity, Settings, Audit, Notifications, Reporting, Api, ExportImport, Licensing) plus per-discipline sample data for Mechanical/Requirements/Calculations/Documents/Verification/Manufacturing (`v0.8.0`/`v0.9.0`) | WP 4.3 onward through `v0.9.0`; `DuplicateNavigationSampleModule` moved out WP 12.3B (ADR-0102, see `Tempest.Validation.FaultInjection`) |
| `Tempest.Validation.FaultInjection` | Tempest.Validation | 1 | `DuplicateNavigationModule` — fault-injection modules, excluded from default Discovery, never referenced by `Tempest.App`/`Tempest.Desktop` | WP 12.3B (moved from `Tempest.Samples`, renamed, ADR-0102) |
| `Tempest.Core.Versioning` | Tempest.Core | 3 | `IPlatformVersionProvider`, `PlatformVersionProvider`, `PlatformVersion` | WP 4.2A |
| `Tempest.Core.Repositories` | Tempest.Core | 0 — **retired, `WP-C`** | Formerly the pre-module-pipeline project repository (`IProjectRepository`, `JsonProjectRepository`) — unreferenced by any production or test code; deleted as genuinely dead (`TD-110`, discharging `TD-01`'s revisit trigger) | Pre-dates Claude-developed history (Unknown exact origin); retired `WP-C` |
| `Tempest.Core.Projects` | Tempest.Core | 0 — **retired, `WP-C`** | Formerly the pre-module-pipeline project service (`ProjectService`, `ProjectNumberGenerator`) — superseded by `Tempest.App.Projects`; deleted as genuinely dead (`TD-110`) | Pre-dates Claude-developed history (Unknown exact origin); retired `WP-C` |
| `Tempest.Core.Hosting` | Tempest.Core | 0 — **retired, `WP-C`** | Formerly the pre-module-pipeline `HostingService`, reframed (not replaced) by ADR-0016 — superseded in practice by `Tempest.Core.Runtime`; deleted as genuinely dead (`TD-110`) | Pre-dates Claude-developed history (Unknown exact origin); retired `WP-C` |
| `Tempest.Core.Commands` | Tempest.Core | 20 | `ICommand` (`WP 4.0`), `ICommandHandler<T>`, `ICommandDispatcher`/`CommandDispatcher`, `ICommandRegistry`/`CommandRegistry`, `CommandDescriptor`, `CommandResult`, `CommandHandlerTable`, `CommandException` and four subtypes; macro/input-binding dispatch support (`WP 10.6A`) | WP 4.0 (contract), WP 5.1A (design), WP 5.1B (implementation), extended WP 10.6A |
| `Tempest.Core.Bootstrap` | Tempest.Core | 0 — **retired, `WP-C`** | Formerly the pre-module-pipeline `BootstrapService`, the sole consumer of `ConfigurationService`/`HostingService`/`LoggingService` and itself referenced by nothing; deleted as genuinely dead (`TD-110`) | Pre-dates Claude-developed history (Unknown exact origin); retired `WP-C` |
| `Tempest.App.Shell` | Tempest.App | 10 — **revived, `v0.10.0`/`v0.11.0`, corrected `WP 16.2A`** | `IShellNavigator`/`ShellNavigator`, `ShellLocation`, `ShellLocationChangedEvent`, `ShellArea`, `NavigationAvailability`, `ProjectArea`/`ProjectAreas`, `IEngineeringScope`/`EngineeringScope` — the Internal Engineering Harness's own navigation/location model (`ADR-0101`, `ADR-0103`); unrelated to the pre-`WP 11.3B` `IPage`/`PlaceholderPage`/`TempestShell` members this namespace held before its own genuine retirement, corrected here after having gone uncounted through eleven `v0.10.0`/`v0.11.0` Work Packages | WP 5.0C/5.0D (original), retired WP 11.3B, revived `v0.10.0` onward as the Workspace Shell's own navigation model |
| `Tempest.Core.Diagnostics` | Tempest.Core | 2 | `IDiagnosticsProvider`/`DiagnosticsProvider` — read-only projection over Host/module/hosted-service lifecycle state | WP 5.2 |
| `Tempest.Core.Identity` | Tempest.Core | 21 | `IIdentity`/`PlatformIdentity`, `IPrincipal`/`PlatformPrincipal`, `Permission`, `IRole`/`Role`, `IRoleProvider`/`RoleProvider`, `ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor`, `IPermissionEvaluator`/`PermissionEvaluator`, `IIdentityService`/`IdentityService`, `IdentityException` and two subtypes; `ICurrentComponentAccessor`/`CurrentComponentAccessor` (`v0.13.0`, `ADR-0111`), `ISessionPrincipalSource`/`LocalSessionPrincipalSource`/`SessionPrincipal` (`v0.14.0`) | WP 6.1, extended `v0.13.0`/`v0.14.0` |
| `Tempest.Core.Persistence` | Tempest.Core | 5 | `IPersistenceStore`/`PersistenceStore`, `PersistenceException` and one subtype — established as part of `WP 6.4`'s own scope (ADR-0041); `IBinaryPersistenceStore` (`v0.14.0`, `ADR-0113`) added to the same `PersistenceStore` concrete type | WP 6.4, extended `v0.14.0` |
| `Tempest.Core.Settings` | Tempest.Core | 11 | `ISettingsMigration` (`WP 16.3B`, `ADR-0120`), `ISettingDefinition`/`SettingDefinition`, `ISettingsProvider`/`SettingsProvider`, `ISettingsChangedEvent`/`SettingsChangedEvent`, `SettingsException` and two subtypes | WP 6.4 |
| `Tempest.Core.Concurrency` | Tempest.Core | 1 | `AsyncKeyedLock` (internal) — a small, shared, per-key async lock used by both Persistence and Settings. Audit does not need it — every record's own key is unique (timestamp plus a random component), so no two writes ever target the same key | WP 6.4 |
| `Tempest.Core.Audit` | Tempest.Core | 9 | `IAuditRecord`/`AuditRecord`, `IAuditRecorder`/`AuditRecorder`, `IAuditQuery`/`AuditQuery`, `AuditQueryCriteria`, `AuditRecordDto` (internal), `AuditException` | WP 6.5 |
| `Tempest.Core.Notifications` | Tempest.Core | 8 | `INotification`, `INotificationHandler<T>`, `INotificationDispatcher`/`NotificationDispatcher`, `NotificationException`, `NotificationSeverity`, `IPlatformNotification`/`PlatformNotification` | WP 6.2 |
| `Tempest.Core.Reporting` | Tempest.Core | 11 | `IReportDefinition`, `IReportRenderer<T>`, `IReportingService`/`ReportingService`, `ReportRequest`, `ReportResult`, `ReportingException` and two subtypes, `IReportTemplate<T>`/`PlainTextReportTemplate<T>` | WP 6.0 |
| `Tempest.Core.Api` | Tempest.Core | 9 | `IApiEndpointRegistry`/`ApiEndpointRegistry`, `ApiRouteDescriptor`, `ApiResponse`, `ApiRequestHandler`, `RestApiHostedService`, `OpenApiDocumentGenerator`, `ApiException` and one subtype | WP 6.3 |
| `Tempest.Core.ExportImport` | Tempest.Core | 16 | `IExportable`/`IExportService`/`ExportService`, `IImportService`/`ImportService`, `ExportImportException` and one approved subtype (`IncompatibleExportSchemaException`), additive `IExportableKind`/`IImportable`/`ExportSection`, `IExportFormat`/`JsonExportFormat`, `IExportPayloadSerializer`/`JsonExportPayloadSerializer`, `CorruptedExportArtifactException`, `DuplicateImportableKindException` | WP 6.7 |
| `Tempest.Core.Licensing` | Tempest.Core | 10 | `ILicense`/`License`, `ILicenseValidator`/`LicenseValidator`, `LicenseValidationResult`, `ILicenseProvider`/`LicenseProvider`, `LicensingException` and one approved subtype (`LicenseValidationException`), `LicenseDto` | WP 6.6 |
| `Tempest.Core.EngineeringData` | Tempest.Core | 12 | `IEngineeringDocument`/`EngineeringDocument`, `IDocumentRevision`/`DocumentRevision`, `IEngineeringDocumentStore`/`EngineeringDocumentStore`, `DocumentReference`, `EngineeringDataException` and one subtype — the identity/revision foundation every canonical Engineering Object is built on | WP 7.1A, `ADR-0053` — backfilled `WP 16.2A` |
| `Tempest.Core.UnitsAndQuantities` | Tempest.Core | 20 | `Quantity<TDimension>`/`Unit<TDimension>`/`IDimension`, per-dimension unit families (`Length`, `Mass`, `Area`, `Volume`, `Force`, `Pressure`, `Duration`), `IncompatibleUnitsException` | WP 7.1B, `ADR-0054` — backfilled `WP 16.2A` |
| `Tempest.Core.Materials` | Tempest.Core | 17 | `IMaterialCatalog`/`MaterialCatalog`, `IMaterialSpecification`/`MaterialSpecification`, `MaterialProperty` and its provenance/confidence/validation-status types, `MaterialsException` and two subtypes | WP 7.1C, `ADR-0055` — backfilled `WP 16.2A` |
| `Tempest.Core.Calculations` | Tempest.Core | 18 | `ICalculationDefinition<TInput, TResult>`/`ICalculationEngine`/`CalculationEngine`, `CalculationRecord`/`CalculationContext`, `CalculationException` and three subtypes, `EngineeringCalculationDefinitions` | WP 7.1D, `ADR-0056` — backfilled `WP 16.2A` |
| `Tempest.Core.Verification` | Tempest.Core | 9 | `IVerificationRecord`/`VerificationRecord`, `IVerificationService`/`VerificationService`, `VerificationContext`/`VerificationCriterion`/`VerificationOutcome`/`VerificationEvidenceEntry` | WP 7.1E, `ADR-0057` — backfilled `WP 16.2A` |
| `Tempest.Core.Requirements` | Tempest.Core | 28 | `IRequirement`/`Requirement`, `IRequirementCollection`/`RequirementCollection`, `IRequirementGroup`/`RequirementGroup`, `IRequirementEvidence`, `IRequirementsService`/`RequirementsService`, `IRequirementValidationService`/`RequirementValidationService` (`WP 9.1A`), `RequirementsException` and four subtypes | WP 7.3A, `ADR-0058`, extended WP 9.1A — backfilled `WP 16.2A` |
| `Tempest.Core.EngineeringDomain` | Tempest.Core | 68 | The canonical Engineering Object model: `IEngineeringObject`/`EngineeringObjectBase`, every Physical/Configuration/Programme/Governance/Supply-Chain/Test-Manufacturing/Documentation-Design object and facet contract (`IHasLifecycle`, `IHasParent`, `IHasRelationships`, `IHasAttachments`, `IDeletable`, `IRenamable`, and others), `IEngineeringObjectRepository`/`IEngineeringRelationshipRepository`, `ILifecycleTransitionTable`, `IValidationRuleSet`, `IReferenceIntegrityChecker`, `IRelationshipDiscovery`/`IDependencyTraversal`/`IImpactAnalysis`, `IEvidenceComposer`, `EngineeringDomainContext`, `EngineeringDomainException` and its subtypes; durability/rehydration/attachment-content additions (`v0.14.0`): `EngineeringObjectState`/`IEngineeringObjectStateStore`, `IAttachmentContentStore`/`AttachmentContent`, `IRehydratable<TSelf>`/`IEngineeringObjectRehydrator`/`IEngineeringObjectRehydratorRegistry`/`EngineeringObjectRehydrationService` | WP 8.2C, `ADR-0075`–`ADR-0081`, extended `v0.9.0` (`IRenamable`/`IHasParent`/`IDeletable`/`IHasBomLine`) and `v0.14.0` (`TD-85`, `ADR-0113`/`ADR-0116`) — backfilled `WP 16.2A` |
| `Tempest.Core.Macros` | Tempest.Core | 5 | `ICommandMacro`/`CommandMacro`, `IMacroManager`/`MacroManager`, `RunMacroCommand` — an ordered, named sequence of registered Command Ids | WP 10.6A, `ADR-0099` — backfilled `WP 16.2A` |
| `Tempest.Core.Input` | Tempest.Core | 4 | `IInputBindingProvider`/`IExternalControllerProvider`, `IInputBindingRegistry`/`InputBindingRouter` | WP 10.6A, `ADR-0100` — backfilled `WP 16.2A` |
| `Tempest.App.Composition` | Tempest.App | 1 | `EngineeringWorkspaceComposer` — the shared collaborator-bundle composer every discipline's own `Program.cs`/`Tempest.Desktop` composition root calls | `v0.8.0` onward — backfilled `WP 16.2A` |
| `Tempest.App.Projects` | Tempest.App | 17 | `IProjectContext`/`ProjectContext`, `IProjectDirectory`/`ProjectDirectory`, `ProjectGovernanceRegister`/`Service`, `ProjectMilestoneRegister`/`Service`, `ProjectTaskRegister`/`Service`, `ProjectDocumentRegister`, `ProjectRequirementRegister`, `ProjectMembership`, `ProjectSummary`, `DuplicateProjectIdentifierException`, `ProjectNotFoundException` | `v0.9.0` onward — backfilled `WP 16.2A` |
| `Tempest.App.Workspace` | Tempest.App | 54 | The shared Workspace-layer contracts and infrastructure every discipline's own sub-namespace builds on: `IWorkspaceManager`/`WorkspaceManager`, `IWorkspaceView`/`IWorkspacePanel`, `IProjectExplorer`/`IPropertyInspector`/`IPropertyFacetProvider`, `ISelectionService`, `IUndoRedoStack`, `EngineeringObjectFactoryRegistry` pattern | WP 8.0B onward — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Mechanical` | Tempest.App | 18 | The Mechanical Product Structure discipline: node provider, view factory, facet provider, commands, factory registry | WP 9.0A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Requirements` | Tempest.App | 25 | The Requirements Management discipline: node provider, view factory, facet provider, 18 commands, registration entry point | WP 9.1A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Calculations` | Tempest.App | 20 | The Engineering Calculations discipline: node provider, view factory, facet provider, ten commands, `CalculationObjectFactoryRegistry`/`CalculationTemplateRegistry`/`CalculationRecordReader` | WP 9.2A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Documents` | Tempest.App | 17 | The Engineering Documents discipline: node provider, view factory, facet provider, nine commands, `DocumentObjectFactoryRegistry` | WP 9.4A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Verification` | Tempest.App | 18 | The Verification Management discipline: node provider, view factory, facet provider, nine commands, `VerificationActivityFactoryRegistry` | WP 9.3A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Manufacturing` | Tempest.App | 16 | The Manufacturing discipline: node provider, view factory, facet provider, eight commands, `ManufacturingObjectFactoryRegistry` | WP 9.5A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Layout` | Tempest.App | 9 | Docking/panel layout infrastructure (`DockingGrid`, `PanelHostControl`, `PredefinedLayouts`) shared across every discipline's own Workspace view | WP 10.2B onward — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Macros` | Tempest.App | 1 | Workspace-layer macro command-history collaborator | WP 10.6A — backfilled `WP 16.2A` |
| `Tempest.App.Workspace.Viewing` | Tempest.App | 3 | Digital Thread graph read-model collaborators shared with `Tempest.Desktop`'s own visualisation | `v0.10.0` — backfilled `WP 16.2A` |
| `Tempest.Core.Models` | Tempest.Core | 1 | `ProjectModel.cs` — the bootstrap-era project model (`Threat Model.md`'s dead-code trace; `TD-01`/`TD-110` scope). **Corrected `WP 16.1B` integration (2026-09-04):** the file begins with a UTF-8 byte-order mark, which makes `grep -rhoP "^namespace"` (this register's own documented derivation command) miss its `namespace Tempest.Core.Models;` line — `WP 16.2A`'s re-derivation therefore filed it under the global-namespace row below. The health check's new Namespace check reads files with the BOM stripped and found it; recorded here as its own row | Pre-dates Claude-developed history (Unknown exact origin) |
| *(no namespace declared — global namespace)* | Tempest.Core, Tempest.App | 3 | `AssemblyInfo.cs` (×2), `Program.cs` (rewritten `WP 5.0D` as the real entry point; still top-level statements, still global namespace) — `ProjectModel.cs` was listed here by `WP 16.2A` in error (see the `Tempest.Core.Models` row above); `ApplicationConfiguration.cs`, `ConfigurationService.cs`, `LoggingService.cs`, `ProjectNumberGenerator.cs` (present at this row's last review) have since been deleted as genuinely dead (`WP-C`, `TD-110`); `ProjectModel.cs` itself was not part of that deletion and remains unmigrated | Pre-dates Claude-developed history (Unknown exact origin); corrected `WP 16.2A` |

**Total: 47 namespaces** (corrected `WP 16.1B` integration, 2026-09-04: `Tempest.Core.Models` added — see its row) — the 47 rows above declaring a real
`namespace X;` with at least one file. The four **retired** rows
(`Tempest.Core.Repositories`/`.Projects`/`.Hosting`/`.Bootstrap`, zero
files) and the 1 *(no namespace declared — global namespace)* row are
both shown for continuity/completeness but are not namespaces and are
not counted toward the 47. Across 4 in-scope projects/areas (`Tempest.Core`, `Tempest.App`,
`Tempest.Samples`, `Tempest.Validation`) — **re-derived at the `WP 16.4B`
integration (2026-09-05), after `WP 16.3B`, `WP 16.4B` and `WP 16.5A`
landed:** 47 namespaces; summing every namespace's own file count above
(723) plus the 3 global-namespace files gives **726**, matching a direct
count of `.cs` files under the four in-scope roots excluding `bin/` and
`obj/` (432 + 211 + 82 + 1 = 726) exactly. `WP 16.4B` added nine files
across three existing namespaces — `Tempest.Core.Requirements` 24 → 28,
`Tempest.Core.Materials` 14 → 17, `Tempest.Core.EngineeringDomain`
66 → 68 and `Tempest.Core.DependencyInjection` 12 → 14 — and no new
namespace. Two derivation corrections are
recorded here rather than left implicit: the register's own documented
command `grep -rhoP "^namespace \K[\w.]+"` misses any file carrying a
UTF-8 byte-order mark (see the `Tempest.Core.Models` row) and a plain
`find src/... -name "*.cs"` counts build output under `bin/`/`obj/` in a
checkout that has been built locally — both derivations must strip the
BOM and prune those directories, as the health check's own Namespace
check does. The previously stated 46 namespaces / 713 files were correct
against the tree `WP 16.2A` measured; the movement since is `WP 16.3B`'s
`Contracts/StateMigration.cs` (`Tempest.Core.EngineeringDomain` 65 → 66)
and its settings-migration contract (`Tempest.Core.Settings` 10 → 11),
plus the `Tempest.Core.Models` row split out of the global-namespace row
at the `WP 16.1B` integration. The `Tempest.Templates.Module` sample-only project remains
out of this register's own declared scope; its own single `.cs` file
is not counted above.

**`src/Tempest.Desktop/` remains explicitly out of this register's own
declared Scope** (see the Scope field above) — **89 `.cs` files across 14
namespaces**, re-derived 2026-09-05: `Branding`, `Composition`,
`Diagnostics`, `DigitalThread`, `Docking`, `Editors`, `History`, `Icons`,
`Input`, `Tasks`, `Theming`, `Viewing`, `Views`, and the root
`Tempest.Desktop` namespace itself, plus one global-namespace file. (The
"9 namespaces" stated by every prior pass listed only nine of them and was
wrong on its own terms — corrected here, still out of scope.) None are
counted anywhere in this register, a decision reaffirmed rather than
revisited by `WP 16.2A` — extending scope to a full second
project's worth of namespaces remains a separate, substantial
undertaking, per the `WP 12.0B` follow-up entry below. Combined with
`src/Templates/` (1 file, 1 namespace, out of scope by declared
convention) and `src/Plugins/` (0 `.cs` files), `src/` holds **816 `.cs`
files across 62 distinct namespaces** in total (BOM-aware, `bin/`/`obj/`
pruned; 47 + 14 + 1 = 62) — of which this register's own declared scope
covers 726 files (89%) and 47 namespaces (76%).**

## A Note on the Four Pre-Claude Namespaces

**Update, `WP-C` (2026-08-31):** all four are now **retired**. `ApplicationConfiguration`,
the settings record `ConfigurationService` produced and `HostingService`/`BootstrapService`
consumed, became unreferenced as a direct consequence and was removed with them as
`WP-C`'s own completion. The full-repository
architecture and dead-code audit demonstrated that every type in them was
unreferenced by any production or test code, and that the only surviving
mentions were comments describing the code as retired. `TD-01`'s own recorded
revisit trigger — "the legacy bootstrap code is either genuinely revived or
deliberately deleted" — was discharged by deletion (`TD-110`). The rows above
are retained rather than removed, following `Tempest.App.Shell`'s own
established retirement convention. The historical note below stands unchanged.

`Tempest.Core.Repositories`, `Tempest.Core.Projects`, `Tempest.Core.Hosting`,
and `Tempest.Core.Bootstrap` are **Inferred** to predate this repository's
Claude-developed history (first Claude-authored commit `7514b9d`,
2026-07-21) — none is discussed as "newly created" by any Work Package
retrospective, and `Platform Service Map.md` itself describes the code
these namespaces contain as "bootstrap-era functionality that predates and
is currently independent of the module pipeline entirely." Their exact
original authorship and creation date are **Unknown** — the five
pre-Claude commits (`Engineering Evolution Register.md`) establish that
*some* code existed before Claude's involvement, but do not, by
themselves, prove which specific namespace originated in which commit
without a deeper `git log --follow` per file, which was out of scope for
this baseline.

## Cross-Reference Check

Every namespace with an "Introduced" Work Package above is cross-checked
against `Architecture Document Register.md`'s "Primary Work Package(s)"
column and found consistent. The four pre-Claude namespaces are flagged
Unknown rather than assigned a fabricated Work Package, per this Work
Package's own governing rule.

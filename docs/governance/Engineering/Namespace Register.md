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
| **Last Reviewed** | 2026-08-12 (WP 12.0B follow-up, Desktop Composition Root Decomposition Implementation — Governance Reconciliation) — **narrow correction only, not a full re-derivation; no new row added**. Closes Finding 3 of `WP 12.0B`'s own architecture/code review: `Desktop Composition Architecture.md`'s own "Documentation Impact" section (written `WP 12.0A`) had stated this register was "`WP 12.0B`'s own obligation, once real collaborator types exist to record" — factually true real collaborator types now exist (`Tempest.Desktop.Composition`, nine files; new files added to the already-existing `Tempest.App.Workspace`/`.Mechanical`/`.Requirements`/`.Calculations`/`.Documents`/`.Verification`/`.Manufacturing` namespaces for the six `EngineeringCockpit` read-model collaborators plus `CockpitFormatting`) — but this register's own declared **Scope**, above, has never covered `src/Tempest.Desktop/` at all, for any namespace, at any prior Work Package; adding a row for only the newest `Tempest.Desktop` namespace while its eight sibling namespaces (`Docking`, `Editors`, `History`, `Input`, `Tasks`, `Theming`, `Views`, `DigitalThread`, and the root `Tempest.Desktop` namespace itself) remain untracked would not close the real gap, only paper over one small piece of it inconsistently. **Decision, made explicitly rather than silently**: `Tempest.Desktop`'s own namespaces remain out of this register's scope for now — extending scope to cover an entire second project's worth of namespaces is a separate, substantial undertaking (a full `src/Tempest.Desktop/` namespace audit), not a narrow correction a single implementation-follow-up Work Package should absorb as a side effect, mirroring this exact register's own established precedent immediately below (`WP 11.3B`'s own disclosed-not-fixed staleness) and `WP 11.4A`'s own precedent for scoping what is and is not a given Work Package's job to fix. **Separately confirmed, and also disclosed rather than silently accepted**: the six `Tempest.App.Workspace.*` discipline namespaces `WP 12.0B` added new files to are themselves already part of this register's own pre-existing, larger, undisclosed-until-now gap — `src/Tempest.App/` is declared in scope, above, yet no `Tempest.App.Workspace` row of any kind (Mechanical, Requirements, Calculations, Documents, Verification, Manufacturing, or the parent `Tempest.App.Workspace` namespace itself) has ever appeared in this register, confirmed by direct `grep`, predating `WP 12.0B` entirely (these namespaces were introduced `WP 9.0A`–`WP 9.5A`). `WP 12.0B`'s own new files land inside an already-untracked namespace and do not deepen this gap — recorded here as a real, standing, pre-existing omission this register should close in a future dedicated pass, not attempted here. **`Desktop Composition Architecture.md`'s own "Documentation Impact" section has been corrected in place** to reflect this reconciled outcome rather than its own prior, now-inaccurate "`WP 12.0B`'s own obligation" phrasing; `docs/releases/v0.12.0/WorkPackages.md`'s `WP 12.0B` row and the `WP12.0B` Academy retrospective are both updated to match, so all four documents now agree. Previously reviewed 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — **narrow correction only, not a full re-derivation**: added the `Tempest.Validation.FaultInjection` row (new project, ADR-0102); corrected the `Tempest.Samples` row's own file count (47 → 46, one file moved out) and member list (`DuplicateNavigationSampleModule` removed — moved to `Tempest.Validation.FaultInjection`, renamed `DuplicateNavigationModule`). Every other row carried forward unverified, same disclosed staleness as every prior pass below. Previously reviewed 2026-08-11 (WP 11.3B, Presentation Strategy Implementation) — **narrow correction only, not a full re-derivation**: the `Tempest.App.Shell` row updated to reflect `TempestShell`/`IPage`/`PlaceholderPage`'s retirement (dead code since `ADR-0068`, removed `WP 11.3B`, `ADR-0101`). Every other row is carried forward unverified from its own last review and is known to be stale for unrelated reasons — this register has not had a full pass since `WP 6.6` (2026-07-29), predating the entire Engineering Foundation, Engineering Workspace, Mechanical Foundation, and User Experience & Desktop Application phases; a full re-derivation is a separate, substantial undertaking outside this Work Package's own scope, named here rather than silently left implicit. Previously reviewed 2026-07-29 (WP 6.6, Licensing) — added `Tempest.Core.Licensing`; every row's own file count re-derived directly again (`grep -rl "^namespace X;"`), consistent with `WP 6.0`'s/`WP 6.2`'s/`WP 6.3`'s/`WP 6.7`'s own prior passes. |
| **Related Documents** | `docs/architecture/Engineering Glossary.md` (`Tempest.Core.Runtime` vs. `Tempest.Core.Hosting`, ADR-0016); `Interface Register.md`; `Exception Register.md`. |
| **Related ADRs** | ADR-0016, ADR-0024, ADR-0036, ADR-0037, ADR-0038, ADR-0039, ADR-0040, ADR-0046, ADR-0047, ADR-0048, ADR-0049, ADR-0050, ADR-0051, ADR-0102, ADR-0103 (`Tempest.Desktop.Composition`'s own governing decision — see this register's own `WP 12.0B` follow-up entry, above, for why no row was added despite it). |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/06-platform-layering.md`. |
| **Coverage Status** | Complete. |

---

## Entries

| Namespace | Project | File Count | Purpose | Introduced |
|---|---|---|---|---|
| `Tempest.Core.Modules` | Tempest.Core | 23 | Discovery, Registration, Lifecycle, Module SDK, `ModuleMetadataAttribute` | WP 2.1–2.3, extended WP 4.1, WP 4.4B |
| `Tempest.Core.Plugins` | Tempest.Core | 13 | Plugin manifest, discovery, loading | WP 4.2 |
| `Tempest.Core.DependencyInjection` | Tempest.Core | 13 | Custom DI container | WP 2.4 |
| `Tempest.Core.Logging` | Tempest.Core | 10 | `ILogger`, sinks, factory, `CompositeLogSink` | WP 2.6, extended WP 5.2 |
| `Tempest.Core.Configuration` | Tempest.Core | 9 | Configuration sources, builder, provider | WP 2.5 |
| `Tempest.Core.BackgroundServices` | Tempest.Core | 9 | Hosted service contracts, discovery, orchestration | WP 4.0 (contracts), WP 4.5 (infrastructure) |
| `Tempest.Core.Runtime` | Tempest.Core | 7 | `TempestHost`, `TempestHostBuilder`, `HostState` | WP 2.7B; distinct from `Tempest.Core.Hosting` per ADR-0016 |
| `Tempest.Core.Events` | Tempest.Core | 4 | `IEvent`, `IEventHandler<T>`, `IEventBus`, `EventBus` | WP 4.0 (contracts), WP 4.4D (bus) |
| `Tempest.Core.Navigation` | Tempest.Core | 7 | `NavigationItem`, `INavigationProvider`/`NavigationService`, `NavigationRequestedEvent`, `NavigationException` and two subtypes | WP 5.0A (design), WP 5.0B (implementation) |
| `Tempest.Samples` | Tempest.Samples | 46 | `ClockModule`, `ClockLifecycleObserverModule`, `ClockModuleLifecycleEvent`, `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `CommandSampleModule`, `IncrementCounterCommand`/`Handler`, `NavigateToSampleHomeCommand`/`Handler`, `DiagnosticsSampleModule`, `GetDiagnosticsSummaryCommand`/`Handler`, `IdentitySampleModule`, `CheckSamplePermissionCommand`/`Handler`, `SettingsSampleModule`, `GetSampleSettingCommand`/`Handler`, `SetSampleSettingCommand`/`Handler`, `AuditSampleModule`, `RecordSampleAuditActionCommand`/`Handler`, `QuerySampleAuditRecordsCommand`/`Handler`, `NotificationSampleModule`, `NotificationSampleHostedService`, `PublishSampleNotificationCommand`/`Handler`, `ReportingSampleModule`, `SampleSummaryReportDefinition`, `SampleSummaryReportRenderer`, `GenerateSampleReportCommand`/`Handler`, `ApiSampleModule`, `ExportImportSampleModule`, `SettingExportImportAdapter`, `SampleExportArtifactStore`, `ExportSampleDataCommand`/`Handler`, `ImportSampleDataCommand`/`Handler`, `LicensingSampleModule`, `CheckSampleCapabilityCommand`/`Handler` | WP 4.3, extended WP 4.4E, WP 5.0B, WP 5.1B, WP 5.2, WP 6.1, WP 6.4, WP 6.5, WP 6.2, WP 6.0, WP 6.3, WP 6.7, WP 6.6; `DuplicateNavigationSampleModule` moved out WP 12.3B (ADR-0102, see `Tempest.Validation.FaultInjection`) |
| `Tempest.Validation.FaultInjection` | Tempest.Validation | 1 | `DuplicateNavigationModule` — fault-injection modules, excluded from default Discovery, never referenced by `Tempest.App`/`Tempest.Desktop` | WP 12.3B (moved from `Tempest.Samples`, renamed, ADR-0102) |
| `Tempest.Core.Versioning` | Tempest.Core | 3 | `IPlatformVersionProvider`, `PlatformVersionProvider`, `PlatformVersion` | WP 4.2A |
| `Tempest.Core.Repositories` | Tempest.Core | 2 | Pre-module-pipeline project repository (`IProjectRepository`, `JsonProjectRepository`) | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Projects` | Tempest.Core | 1 | Pre-module-pipeline project service | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Hosting` | Tempest.Core | 1 | Pre-module-pipeline `HostingService` — environment/deployment adapters, reframed (not replaced) by ADR-0016 | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Commands` | Tempest.Core | 14 | `ICommand` (`WP 4.0`), `ICommandHandler<T>`, `ICommandDispatcher`/`CommandDispatcher`, `ICommandRegistry`/`CommandRegistry`, `CommandDescriptor`, `CommandResult`, `CommandHandlerTable`, `CommandException` and four subtypes | WP 4.0 (contract), WP 5.1A (design), WP 5.1B (implementation) |
| `Tempest.Core.Bootstrap` | Tempest.Core | 1 | Pre-module-pipeline `BootstrapService` | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.App.Shell` | Tempest.App | 0 — **retired, `WP 11.3B`** | Formerly `IPage`, `PlaceholderPage`, `TempestShell` (`Tempest.App`'s own composition root at `v0.5.0`) — unreachable from any running entry point since `ADR-0068` (`WP 8.1A`, `v0.8.0`); retired as dead code and this namespace removed entirely once `ADR-0101` formally classified `Tempest.App`/`WorkspaceShell` as TempestOS's Internal Engineering Harness | WP 5.0C (design), WP 5.0D (implementation), retired WP 11.3B |
| `Tempest.Core.Diagnostics` | Tempest.Core | 2 | `IDiagnosticsProvider`/`DiagnosticsProvider` — read-only projection over Host/module/hosted-service lifecycle state | WP 5.2 |
| `Tempest.Core.Identity` | Tempest.Core | 18 | `IIdentity`/`PlatformIdentity`, `IPrincipal`/`PlatformPrincipal`, `Permission`, `IRole`/`Role`, `IRoleProvider`/`RoleProvider`, `ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor`, `IPermissionEvaluator`/`PermissionEvaluator`, `IIdentityService`/`IdentityService`, `IdentityException` and two subtypes | WP 6.1 |
| `Tempest.Core.Persistence` | Tempest.Core | 4 | `IPersistenceStore`/`PersistenceStore`, `PersistenceException` and one subtype — established as part of `WP 6.4`'s own scope (ADR-0041) | WP 6.4 |
| `Tempest.Core.Settings` | Tempest.Core | 9 | `ISettingDefinition`/`SettingDefinition`, `ISettingsProvider`/`SettingsProvider`, `ISettingsChangedEvent`/`SettingsChangedEvent`, `SettingsException` and two subtypes | WP 6.4 |
| `Tempest.Core.Concurrency` | Tempest.Core | 1 | `AsyncKeyedLock` (internal) — a small, shared, per-key async lock used by both Persistence and Settings. Audit does not need it — every record's own key is unique (timestamp plus a random component), so no two writes ever target the same key | WP 6.4 |
| `Tempest.Core.Audit` | Tempest.Core | 9 | `IAuditRecord`/`AuditRecord`, `IAuditRecorder`/`AuditRecorder`, `IAuditQuery`/`AuditQuery`, `AuditQueryCriteria`, `AuditRecordDto` (internal), `AuditException` | WP 6.5 |
| `Tempest.Core.Notifications` | Tempest.Core | 8 | `INotification`, `INotificationHandler<T>`, `INotificationDispatcher`/`NotificationDispatcher`, `NotificationException`, `NotificationSeverity`, `IPlatformNotification`/`PlatformNotification` | WP 6.2 |
| `Tempest.Core.Reporting` | Tempest.Core | 11 | `IReportDefinition`, `IReportRenderer<T>`, `IReportingService`/`ReportingService`, `ReportRequest`, `ReportResult`, `ReportingException` and two subtypes, `IReportTemplate<T>`/`PlainTextReportTemplate<T>` | WP 6.0 |
| `Tempest.Core.Api` | Tempest.Core | 9 | `IApiEndpointRegistry`/`ApiEndpointRegistry`, `ApiRouteDescriptor`, `ApiResponse`, `ApiRequestHandler`, `RestApiHostedService`, `OpenApiDocumentGenerator`, `ApiException` and one subtype | WP 6.3 |
| `Tempest.Core.ExportImport` | Tempest.Core | 16 | `IExportable`/`IExportService`/`ExportService`, `IImportService`/`ImportService`, `ExportImportException` and one approved subtype (`IncompatibleExportSchemaException`), additive `IExportableKind`/`IImportable`/`ExportSection`, `IExportFormat`/`JsonExportFormat`, `IExportPayloadSerializer`/`JsonExportPayloadSerializer`, `CorruptedExportArtifactException`, `DuplicateImportableKindException` | WP 6.7 |
| `Tempest.Core.Licensing` | Tempest.Core | 10 | `ILicense`/`License`, `ILicenseValidator`/`LicenseValidator`, `LicenseValidationResult`, `ILicenseProvider`/`LicenseProvider`, `LicensingException` and one approved subtype (`LicenseValidationException`), `LicenseDto` | WP 6.6 |
| *(no namespace declared — global namespace)* | Tempest.Core, Tempest.App | 7 | `AssemblyInfo.cs`, `Program.cs` (rewritten `WP 5.0D` as the real entry point; still top-level statements, still global namespace), `ApplicationConfiguration.cs`, `ConfigurationService.cs`, `LoggingService.cs`, `ProjectModel.cs`, `ProjectNumberGenerator.cs` — the latter five pre-module-pipeline, bootstrap-era types, now unreferenced by `Program.cs` but untouched and unmigrated (`WP 5.0C`'s own disclosed scope boundary; `WP 5.2` re-scoped `TD-01`'s own migration question forward again rather than touching these) | Pre-dates Claude-developed history (Unknown exact origin) |

**Total: 29 namespaces (28 declared + the global namespace) across 3
in-scope projects (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`) —
the `Tempest.Templates.Module` sample-only project `WP 5.3` added
remains out of this register's own declared scope (its own
`TempestSampleModule` namespace is not counted above), but its single
`.cs` file is still part of the `src/` file total below. 272 `.cs` files
under `src/` excluding generated `obj`/`bin` artifacts (271 across the
3 in-scope projects + 1 in `Tempest.Templates.Module`) — re-derived
directly by `WP 6.6`, per-namespace, via `grep -rl "^namespace X;"
src/` for every row above, not incremented from the prior figure. `WP
6.6` itself adds the new `Tempest.Core.Licensing` namespace (10 files)
and 3 new `Tempest.Samples` files (`LicensingSampleModule.cs`,
`CheckSampleCapabilityCommand.cs`, `CheckSampleCapabilityCommandHandler.cs`).**

## A Note on the Four Pre-Claude Namespaces

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

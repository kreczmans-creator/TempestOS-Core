# Namespace Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Namespace Register |
| **Purpose** | The index of every namespace under `src/`, its owning project, file count, and purpose — so a reader can find "where does X live" without grepping the tree. |
| **Scope** | Every `namespace` declaration under `src/Tempest.Core/`, `src/Tempest.App/`, and `src/Samples/Tempest.Samples/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct source inspection (`grep -rhoP "^namespace" src/`). |
| **Review Frequency** | Updated whenever a new namespace is introduced under `src/`. |
| **Last Reviewed** | 2026-07-29 (WP 6.0, Reporting Framework) — added `Tempest.Core.Reporting`; every row's own file count re-derived directly again (`grep -rl "^namespace X;"`), consistent with `WP 6.2`'s own prior pass. |
| **Related Documents** | `docs/architecture/Engineering Glossary.md` (`Tempest.Core.Runtime` vs. `Tempest.Core.Hosting`, ADR-0016); `Interface Register.md`; `Exception Register.md`. |
| **Related ADRs** | ADR-0016, ADR-0024, ADR-0036, ADR-0037, ADR-0038, ADR-0039, ADR-0040, ADR-0046. |
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
| `Tempest.Samples` | Tempest.Samples | 36 | `ClockModule`, `ClockLifecycleObserverModule`, `ClockModuleLifecycleEvent`, `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`, `CommandSampleModule`, `IncrementCounterCommand`/`Handler`, `NavigateToSampleHomeCommand`/`Handler`, `DiagnosticsSampleModule`, `GetDiagnosticsSummaryCommand`/`Handler`, `IdentitySampleModule`, `CheckSamplePermissionCommand`/`Handler`, `SettingsSampleModule`, `GetSampleSettingCommand`/`Handler`, `SetSampleSettingCommand`/`Handler`, `AuditSampleModule`, `RecordSampleAuditActionCommand`/`Handler`, `QuerySampleAuditRecordsCommand`/`Handler`, `NotificationSampleModule`, `NotificationSampleHostedService`, `PublishSampleNotificationCommand`/`Handler`, `ReportingSampleModule`, `SampleSummaryReportDefinition`, `SampleSummaryReportRenderer`, `GenerateSampleReportCommand`/`Handler` | WP 4.3, extended WP 4.4E, WP 5.0B, WP 5.1B, WP 5.2, WP 6.1, WP 6.4, WP 6.5, WP 6.2, WP 6.0 |
| `Tempest.Core.Versioning` | Tempest.Core | 3 | `IPlatformVersionProvider`, `PlatformVersionProvider`, `PlatformVersion` | WP 4.2A |
| `Tempest.Core.Repositories` | Tempest.Core | 2 | Pre-module-pipeline project repository (`IProjectRepository`, `JsonProjectRepository`) | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Projects` | Tempest.Core | 1 | Pre-module-pipeline project service | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Hosting` | Tempest.Core | 1 | Pre-module-pipeline `HostingService` — environment/deployment adapters, reframed (not replaced) by ADR-0016 | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.Core.Commands` | Tempest.Core | 14 | `ICommand` (`WP 4.0`), `ICommandHandler<T>`, `ICommandDispatcher`/`CommandDispatcher`, `ICommandRegistry`/`CommandRegistry`, `CommandDescriptor`, `CommandResult`, `CommandHandlerTable`, `CommandException` and four subtypes | WP 4.0 (contract), WP 5.1A (design), WP 5.1B (implementation) |
| `Tempest.Core.Bootstrap` | Tempest.Core | 1 | Pre-module-pipeline `BootstrapService` | Pre-dates Claude-developed history (Unknown exact origin) |
| `Tempest.App.Shell` | Tempest.App | 3 | `IPage`, `PlaceholderPage`, `TempestShell` — the application shell, `Tempest.App`'s own composition root | WP 5.0C (design), WP 5.0D (implementation) |
| `Tempest.Core.Diagnostics` | Tempest.Core | 2 | `IDiagnosticsProvider`/`DiagnosticsProvider` — read-only projection over Host/module/hosted-service lifecycle state | WP 5.2 |
| `Tempest.Core.Identity` | Tempest.Core | 18 | `IIdentity`/`PlatformIdentity`, `IPrincipal`/`PlatformPrincipal`, `Permission`, `IRole`/`Role`, `IRoleProvider`/`RoleProvider`, `ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor`, `IPermissionEvaluator`/`PermissionEvaluator`, `IIdentityService`/`IdentityService`, `IdentityException` and two subtypes | WP 6.1 |
| `Tempest.Core.Persistence` | Tempest.Core | 4 | `IPersistenceStore`/`PersistenceStore`, `PersistenceException` and one subtype — established as part of `WP 6.4`'s own scope (ADR-0041) | WP 6.4 |
| `Tempest.Core.Settings` | Tempest.Core | 9 | `ISettingDefinition`/`SettingDefinition`, `ISettingsProvider`/`SettingsProvider`, `ISettingsChangedEvent`/`SettingsChangedEvent`, `SettingsException` and two subtypes | WP 6.4 |
| `Tempest.Core.Concurrency` | Tempest.Core | 1 | `AsyncKeyedLock` (internal) — a small, shared, per-key async lock used by both Persistence and Settings. Audit does not need it — every record's own key is unique (timestamp plus a random component), so no two writes ever target the same key | WP 6.4 |
| `Tempest.Core.Audit` | Tempest.Core | 9 | `IAuditRecord`/`AuditRecord`, `IAuditRecorder`/`AuditRecorder`, `IAuditQuery`/`AuditQuery`, `AuditQueryCriteria`, `AuditRecordDto` (internal), `AuditException` | WP 6.5 |
| `Tempest.Core.Notifications` | Tempest.Core | 8 | `INotification`, `INotificationHandler<T>`, `INotificationDispatcher`/`NotificationDispatcher`, `NotificationException`, `NotificationSeverity`, `IPlatformNotification`/`PlatformNotification` | WP 6.2 |
| `Tempest.Core.Reporting` | Tempest.Core | 11 | `IReportDefinition`, `IReportRenderer<T>`, `IReportingService`/`ReportingService`, `ReportRequest`, `ReportResult`, `ReportingException` and two subtypes, `IReportTemplate<T>`/`PlainTextReportTemplate<T>` | WP 6.0 |
| *(no namespace declared — global namespace)* | Tempest.Core, Tempest.App | 7 | `AssemblyInfo.cs`, `Program.cs` (rewritten `WP 5.0D` as the real entry point; still top-level statements, still global namespace), `ApplicationConfiguration.cs`, `ConfigurationService.cs`, `LoggingService.cs`, `ProjectModel.cs`, `ProjectNumberGenerator.cs` — the latter five pre-module-pipeline, bootstrap-era types, now unreferenced by `Program.cs` but untouched and unmigrated (`WP 5.0C`'s own disclosed scope boundary; `WP 5.2` re-scoped `TD-01`'s own migration question forward again rather than touching these) | Pre-dates Claude-developed history (Unknown exact origin) |

**Total: 26 namespaces (25 declared + the global namespace) across 3
in-scope projects (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`) —
the `Tempest.Templates.Module` sample-only project `WP 5.3` added
remains out of this register's own declared scope (its own
`TempestSampleModule` namespace is not counted above), but its single
`.cs` file is still part of the `src/` file total below. 226 `.cs` files
under `src/` excluding generated `obj`/`bin` artifacts (225 across the
3 in-scope projects + 1 in `Tempest.Templates.Module`) — re-derived
directly by `WP 6.0`, per-namespace, via `grep -rl "^namespace X;"
src/` for every row above, not incremented from the prior figure. `WP
6.0` itself adds the new `Tempest.Core.Reporting` namespace (11 files)
and 5 new `Tempest.Samples` files (`ReportingSampleModule.cs`,
`SampleSummaryReportDefinition.cs`, `SampleSummaryReportRenderer.cs`,
`GenerateSampleReportCommand.cs`,
`GenerateSampleReportCommandHandler.cs`).**

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

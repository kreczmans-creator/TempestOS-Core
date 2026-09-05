# Exception Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Exception Register |
| **Purpose** | The complete index of every custom exception type under `src/Tempest.Core/`, its hierarchy, and which failure-classification rule (Host-fatal vs. isolated) governs it. |
| **Scope** | Every class deriving, directly or transitively, from `System.Exception` under `src/Tempest.Core/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct source inspection; `docs/architecture/Failure Behaviour.md`; `docs/academy/06 Engineering Standards/01-exception-design.md`. |
| **Review Frequency** | Updated whenever a new exception type is introduced. |
| **Last Reviewed** | 2026-09-05 (`WP 16.4B-R1`, Architecture remediation) — added `DuplicateStateMigrationException` and `ConflictingStateMigrationException`, both `EngineeringDomainException` subtypes, thrown by `StateMigrationRegistry.Register` (`src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs`) closing a review-board Architecture finding: two migrations could collide silently (a same-chain last-wins overwrite, and a common/Kind-specific ambiguity `Find` always resolved toward the common chain) with a record still advancing to its target schema version regardless. 87 → 89. See `docs/releases/v0.16.0/WP16.4B-R1 Migration Collision Guard and Platform Service Registration.md`. Previously reviewed 2026-09-04 (WP 16.2A, Register and Status Currency) — full re-derivation against `grep -rEn "^public (sealed \|abstract )?class \w+Exception\b" src/Tempest.Core --include=*.cs`, 84 matches. Backfilled all 29 rows missing since `WP 6.6` (`v0.7.0`/`v0.8.0` Calculations, Materials, Units & Quantities, Engineering Data, Engineering Domain, Engineering Workflow, Requirements; `v0.13.0` Plugin Trust & Dependencies), closing the staleness `WP 9.0A` first disclosed. Total corrected 52 → 84; Distribution by Root Category gained 8 new rows summing to 84. See `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`. Previously reviewed 2026-08-05 (WP 9.1A, Requirements Management Workspace) — added `RequirementGroupHasChildrenException` (see Entries table, below); confirmed the disclosed staleness below is still open, now also naming the `Tempest.Core.Requirements` exception family explicitly (previously only implied by "every other `v0.7.0`/`v0.8.0` exception type"). Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — added `CircularParentAssignmentException`, `EngineeringObjectHasChildrenException` (see Entries table, below); disclosed, not fixed, that this register's own Entries table and Total figure have gone stale since `WP 6.6` — every Engineering Domain and other `v0.7.0`/`v0.8.0` exception type is genuinely missing, a full backfill being out of this Work Package's own scope. Previously reviewed 2026-07-29 (WP 6.6, Licensing) — added `LicensingException`, `LicenseValidationException` (see Entries table, below); no other change to prior entries. |
| **Related Documents** | `docs/architecture/Failure Behaviour.md`; `Architectural Dependency Register.md`. |
| **Related ADRs** | ADR-0013, ADR-0021, ADR-0025, ADR-0038, ADR-0040, ADR-0046, ADR-0047, ADR-0048, ADR-0050, ADR-0051. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/01-exception-design.md`. |
| **Coverage Status** | **Complete, re-verified at `WP 16.4B-R1` (2026-09-05, Architecture remediation).** 89 of 89 classes matching `^public (sealed \|abstract )?class \w+Exception\b` under `src/Tempest.Core/` are listed below, zero omitted. `WP 16.4B`'s own pass, which stated 87/87, was correct against the tree it measured; `WP 16.4B-R1` added two — `DuplicateStateMigrationException` and `ConflictingStateMigrationException` — for the migration-collision guard `StateMigrationRegistry.Register` now enforces (a `v0.16.0` review board Architecture finding). |

---

## Entries

| Exception | Base | Root Category | Host-Fatal or Isolated |
|---|---|---|---|
| `ConfigurationException` | `Exception` | Configuration | Host-fatal (ADR-0013) |
| `ConfigurationKeyNotFoundException` | `ConfigurationException` | Configuration | Host-fatal |
| `DuplicateConfigurationKeyException` | `ConfigurationException` | Configuration | Host-fatal |
| `InvalidConfigurationEntryException` | `ConfigurationException` | Configuration | Host-fatal |
| `ServiceResolutionException` | `Exception` | Dependency Injection | Host-fatal (construction-time) or per-module isolated (resolution during Module Initialisation — see `Failure Behaviour.md`) |
| `AmbiguousConstructorException` | `ServiceResolutionException` | Dependency Injection | Context-dependent (as above) |
| `CircularServiceDependencyException` | `ServiceResolutionException` | Dependency Injection | Context-dependent (as above) |
| `ServiceNotRegisteredException` | `ServiceResolutionException` | Dependency Injection | Context-dependent (as above) |
| `ModuleDiscoveryException` | `Exception` | Module Discovery | Host-fatal (ADR-0013) |
| `DuplicateModuleIdException` | `ModuleDiscoveryException` | Module Discovery | Host-fatal |
| `ModuleRegistrationException` | `Exception` | Module Registration | Host-fatal |
| `DuplicateModuleRegistrationException` | `ModuleRegistrationException` | Module Registration | Host-fatal |
| `ModuleNotRegisteredException` | `ModuleRegistrationException` | Module Registration | Host-fatal (unreachable in normal operation) |
| `ModuleLifecycleException` | `Exception` | Module Lifecycle | Isolated per module (WP 2.3) |
| `InvalidModuleLifecycleTransitionException` | `ModuleLifecycleException` | Module Lifecycle | Isolated per module |
| `PluginException` | `Exception` | Plugin Manifest | Isolated per plugin (ADR-0025), except a Host-level orchestration defect |
| `DuplicatePluginIdException` | `PluginException` | Plugin Manifest | Isolated per plugin |
| `IncompatiblePluginVersionException` | `PluginException` | Plugin Manifest | Isolated per plugin |
| `InvalidPluginManifestException` | `PluginException` | Plugin Manifest | Isolated per plugin |
| `PluginAssemblyLoadException` | `PluginException` | Plugin Manifest | Isolated per plugin |
| `PluginAssemblyNotFoundException` | `PluginException` | Plugin Manifest | Isolated per plugin |
| `HostException` | `Exception` | Runtime Host | Host-fatal (base for Host-level defects) |
| `InvalidHostStateTransitionException` | `HostException` | Runtime Host | Host-fatal |
| `NavigationException` | `Exception` | Navigation | Isolated per module (registration happens inside a module's own lifecycle method) |
| `DuplicateNavigationItemException` | `NavigationException` | Navigation | Isolated per module — covered by `ModuleLifecycleException`'s existing isolation, no new Host policy (ADR-0032) |
| `NavigationItemNotFoundException` | `NavigationException` | Navigation | Application logic's own error (not Host-level); thrown by `Navigate`, not during module lifecycle |
| `CommandException` | `Exception` | Command Framework | Propagates to the caller — deliberately not isolated (ADR-0038); unlike every category above, this is neither Host-fatal nor per-module isolated |
| `DuplicateCommandHandlerException` | `CommandException` | Command Framework | Propagates to the caller — thrown by `RegisterHandler`, typically during a module's own `InitialiseAsync`, so also covered by `ModuleLifecycleException`'s existing per-module isolation in that context |
| `DuplicateCommandIdException` | `CommandException` | Command Framework | As above |
| `CommandHandlerNotRegisteredException` | `CommandException` | Command Framework | Application logic's own error (not Host-level); thrown by `DispatchAsync`/`InvokeAsync` |
| `CommandNotFoundException` | `CommandException` | Command Framework | Application logic's own error (not Host-level); thrown by `InvokeAsync` |
| `IdentityException` | `Exception` | Identity & Permissions | Application logic's own error (not Host-level); base type, never thrown directly |
| `PermissionDeniedException` | `IdentityException` | Identity & Permissions | Application logic's own error (not Host-level); thrown by `RequirePermission` — the single authorization enforcement point (ADR-0044) |
| `RoleNotFoundException` | `IdentityException` | Identity & Permissions | Application logic's own error (not Host-level); thrown by `IIdentityService.GetPrincipal`/`EstablishCurrentPrincipal` for a configuration defect (a principal referencing an undefined role), distinct from an ordinary denied-permission case |
| `PersistenceException` | `Exception` | Persistence | Application logic's own error (not Host-level); base type, never thrown directly |
| `PersistenceStoreUnavailableException` | `PersistenceException` | Persistence | Application logic's own error (not Host-level); thrown when the underlying storage backend fails (ADR-0041) |
| `SettingsException` | `Exception` | Settings | Application logic's own error (not Host-level); base type, never thrown directly |
| `DuplicateSettingDefinitionException` | `SettingsException` | Settings | Application logic's own error (not Host-level); thrown by `RegisterDefinition` — first registration wins |
| `SettingNotFoundException` | `SettingsException` | Settings | Application logic's own error (not Host-level); thrown by `GetValueAsync`/`SetValueAsync` for an unregistered key |
| `AuditException` | `Exception` | Audit | Application logic's own error (not Host-level); base type, never thrown directly — every current Audit failure mode is already covered by an existing exception from another namespace (`ArgumentException`, `PersistenceStoreUnavailableException`, `PermissionDeniedException`) |
| `NotificationException` | `Exception` | Notifications | Application logic's own error (not Host-level); base type, never thrown directly — every current Notification failure mode is already covered by an existing exception (`ArgumentException`, `ArgumentNullException`); see "A Note on Notifications" below |
| `ReportingException` | `Exception` | Reporting | Application logic's own error (not Host-level); base-plus-subtype (mirroring `SettingsException`/`IdentityException`/`CommandException`), never thrown directly itself |
| `DuplicateReportDefinitionException` | `ReportingException` | Reporting | Application logic's own error (not Host-level); thrown by `RegisterDefinition` — first registration wins |
| `ReportDefinitionNotFoundException` | `ReportingException` | Reporting | Application logic's own error (not Host-level); thrown by `GenerateAsync` for an unregistered Id |
| `ApiException` | `Exception` | REST API | Application logic's own error (not Host-level); base-plus-subtype (mirroring `ReportingException`/`SettingsException`), never thrown directly itself |
| `DuplicateApiRouteException` | `ApiException` | REST API | Application logic's own error (not Host-level); thrown by `MapCommand` — first registration wins |
| `ExportImportException` | `Exception` | Export/Import | Application logic's own error (not Host-level); base-plus-subtype (mirroring `ReportingException`/`ApiException`), never thrown directly itself |
| `IncompatibleExportSchemaException` | `ExportImportException` | Export/Import | Application logic's own error (not Host-level); thrown by `ImportAsync` for a schema-version mismatch or an unregistered section kind — approved by `Public Interface Catalogue.md` |
| `CorruptedExportArtifactException` | `ExportImportException` | Export/Import | Application logic's own error (not Host-level); thrown by `JsonExportFormat.ReadAsync`/`JsonExportPayloadSerializer.Deserialize` for a malformed or truncated artifact — additive, not in the original catalogue (see `ADR-0051`) |
| `DuplicateImportableKindException` | `ExportImportException` | Export/Import | Application logic's own error (not Host-level); thrown by `ImportService.RegisterImportable` — first registration wins, mirroring `DuplicateReportDefinitionException`/`DuplicateApiRouteException` |
| `LicensingException` | `Exception` | Licensing | Host-fatal (ADR-0013); base-plus-subtype (mirroring `ReportingException`/`ExportImportException`), never thrown directly itself |
| `LicenseValidationException` | `LicensingException` | Licensing | Host-fatal (ADR-0013, ADR-0050); thrown by the Host's own startup sequence when `ILicenseValidator.Validate()` reports an invalid result — never thrown by the validator itself, which always returns a `LicenseValidationResult` even for an expired, malformed, or unreadable license file |
| `CalculationException` | `Exception` | Calculations | Application logic's own error (not Host-level); base type, never thrown directly (`WP 7.1D`, `ADR-0056`) — backfilled `WP 16.2A` |
| `CalculationDefinitionNotFoundException` | `CalculationException` | Calculations | Application logic's own error (not Host-level); thrown for an unregistered calculation Id — backfilled `WP 16.2A` |
| `CalculationInputInvalidException` | `CalculationException` | Calculations | Application logic's own error (not Host-level); thrown when a calculation's own input fails validation — backfilled `WP 16.2A` |
| `DuplicateCalculationException` | `CalculationException` | Calculations | Application logic's own error (not Host-level); thrown by `RegisterDefinition` — first registration wins — backfilled `WP 16.2A` |
| `MaterialsException` | `Exception` | Materials | Application logic's own error (not Host-level); base type, never thrown directly (`WP 7.1C`) — backfilled `WP 16.2A` |
| `MaterialNotFoundException` | `MaterialsException` | Materials | Application logic's own error (not Host-level); thrown for an unregistered material specification — backfilled `WP 16.2A` |
| `DuplicateMaterialException` | `MaterialsException` | Materials | Application logic's own error (not Host-level); thrown by the material catalogue — first registration wins — backfilled `WP 16.2A` |
| `IncompatibleUnitsException` | `Exception` | Units & Quantities | Application logic's own error (not Host-level); thrown by `Quantity<TDimension>.ConvertTo` for a dimensionally incompatible conversion (`WP 7.1B`, `ADR-0054`) — backfilled `WP 16.2A` |
| `EngineeringDataException` | `Exception` | Engineering Data | Application logic's own error (not Host-level); base type, never thrown directly (`WP 7.1A`, `ADR-0053`) — backfilled `WP 16.2A` |
| `EngineeringDocumentNotFoundException` | `EngineeringDataException` | Engineering Data | Application logic's own error (not Host-level); thrown by `IEngineeringDocumentStore` for an unresolvable document Id — backfilled `WP 16.2A` |
| `EngineeringDomainException` | `Exception` | Engineering Domain | Application logic's own error (not Host-level); base type, never thrown directly (`WP 8.2C`, `ADR-0075`) — backfilled `WP 16.2A` |
| `EngineeringObjectNotFoundException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `IEngineeringObjectRepository` for an unresolvable Kind/Id — backfilled `WP 16.2A` |
| `InvalidLifecycleTransitionException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `IHasLifecycle.TransitionAsync` for a transition `ILifecycleTransitionTable` does not permit — backfilled `WP 16.2A` |
| `SelfReferentialRelationshipException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown when a relationship's source and target are the same object — backfilled `WP 16.2A` |
| `DuplicateRehydratorRegistrationException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `IEngineeringObjectRehydratorRegistry.Register` when a *different* type is already registered for the same Kind (`v0.14.0`, `TD-85`, `ADR-0116`) — backfilled `WP 16.2A` |
| `DuplicateStateMigrationException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `StateMigrationRegistry.Register` when a migration is already registered for the identical chain (common, or that same Kind) and `FromVersion` (`TD-87`, `ADR-0120`, `v0.16.0` review board Architecture finding, `WP 16.4B-R1`) — first registration wins, mirroring `DuplicateRehydratorRegistrationException`/`DuplicateServiceRegistrationException` |
| `ConflictingStateMigrationException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `StateMigrationRegistry.Register` when registering would leave a common (Kind-less) migration and a Kind-specific migration both targeting the same `FromVersion` — `Find` always prefers the common chain (`ADR-0120` Decision 2), so the Kind-specific one would silently never run while the record still advanced to the target version; rejected in either registration order (`TD-87`, `v0.16.0` review board Architecture finding, `WP 16.4B-R1`) |
| `CircularParentAssignmentException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `IHasParent.MoveAsync` when the candidate parent is the object itself or one of its own descendants (`WP 9.0A`, `ADR-0081`, `TEMPEST-VAL-006` — corrected from `-002` by `WP 9.0B`; see that register's own disclosure) |
| `EngineeringObjectHasChildrenException` | `EngineeringDomainException` | Engineering Domain | Application logic's own error (not Host-level); thrown by `IDeletable.DeleteAsync` when the object still has live (non-deleted) children (`WP 9.0A`, `ADR-0080`, `TEMPEST-VAL-007` — corrected from `-003` by `WP 9.0B`; see that register's own disclosure) |
| `InvalidDecisionStatusTransitionException` | `InvalidOperationException` | Engineering Workflow | Application logic's own error (not Host-level); thrown by the `Decision` governance workflow for an unpermitted status change (`EngineeringDomain/GovernanceWorkflow.cs`) — backfilled `WP 16.2A` |
| `InvalidIssueStatusTransitionException` | `InvalidOperationException` | Engineering Workflow | Application logic's own error (not Host-level); thrown by the `Issue` governance workflow for an unpermitted status change (`EngineeringDomain/GovernanceWorkflow.cs`) — backfilled `WP 16.2A` |
| `InvalidRiskStatusTransitionException` | `InvalidOperationException` | Engineering Workflow | Application logic's own error (not Host-level); thrown by the `Risk` governance workflow for an unpermitted status change (`EngineeringDomain/GovernanceWorkflow.cs`) — backfilled `WP 16.2A` |
| `InvalidTaskWorkStateTransitionException` | `InvalidOperationException` | Engineering Workflow | Application logic's own error (not Host-level); thrown by the `Task` work-state workflow for an unpermitted state change (`EngineeringDomain/TaskWorkflow.cs`) — backfilled `WP 16.2A` |
| `RequirementsException` | `Exception` | Requirements | Application logic's own error (not Host-level); base type, never thrown directly (`WP 7.3A`) — backfilled `WP 16.2A` |
| `RequirementNotFoundException` | `RequirementsException` | Requirements | Application logic's own error (not Host-level); thrown for an unresolvable requirement Id — backfilled `WP 16.2A` |
| `DuplicateRequirementIdentifierException` | `RequirementsException` | Requirements | Application logic's own error (not Host-level); thrown by `IRequirementsService` — first registration of a business identifier wins — backfilled `WP 16.2A` |
| `InvalidRequirementStatusTransitionException` | `RequirementsException` | Requirements | Application logic's own error (not Host-level); thrown for an unpermitted requirement status change — backfilled `WP 16.2A` |
| `RequirementGroupHasChildrenException` | `RequirementsException` | Requirements | Application logic's own error (not Host-level); thrown by `IRequirementsService.DeleteGroupAsync` when the group still has live (non-deleted) grouped requirements or live sub-groups (`WP 9.1A`, `ADR-0084`) — mirrors `EngineeringObjectHasChildrenException`'s own identical reasoning, for a genuinely different base exception type (`RequirementsException`, `WP 7.3A`, not `EngineeringDomainException`) |
| `RequirementGroupCycleException` | `RequirementsException` | Requirements | Application logic's own error (not Host-level); thrown by `IRequirementsService.MoveGroupAsync` when the requested new parent is the group itself or one of its own descendants, which would make the group hierarchy cyclic (`WP 16.4B`, `TD-67`). No existing type fitted: `RequirementGroupHasChildrenException` is about deletion, and `CircularParentAssignmentException` is scoped to `IEngineeringObject`, which no Requirements type implements (`ADR-0084`) |
| `ServiceRegistrationException` | `Exception` | Dependency Injection | Base type, never thrown directly (`WP 16.4B`, `TD-69`). Deliberately a separate root from `ServiceResolutionException`: that covers failures while an already-built `ITempestServiceProvider` resolves a service, this covers failures while an `IServiceCollection` is still being built and no provider exists — the same registration/resolution split `ModuleRegistrationException`/`ModuleDiscoveryException` already draw for modules |
| `DuplicateServiceRegistrationException` | `ServiceRegistrationException` | Dependency Injection | Application logic's own error (not Host-level); thrown by `IServiceCollection.Add`/`AddInstance` when the service type already has a registration (`WP 16.4B`, `TD-69`) — first registration wins, mirroring `DuplicateApiRouteException`/`DuplicateReportDefinitionException`. Replacing one deliberately requires `allowReplace: true`; before this, a mistaken re-registration silently swapped the platform implementation with no exception and no log |
| `CircularPluginDependencyException` | `PluginException` | Plugin Trust & Dependencies | Isolated per plugin (ADR-0025); thrown when a plugin's own declared dependency graph cycles (`v0.13.0`) — backfilled `WP 16.2A` |
| `IncompatiblePluginDependencyVersionException` | `PluginException` | Plugin Trust & Dependencies | Isolated per plugin (ADR-0025); thrown when a plugin's own declared dependency version constraint is not satisfied (`v0.13.0`) — backfilled `WP 16.2A` |
| `MissingPluginDependencyException` | `PluginException` | Plugin Trust & Dependencies | Isolated per plugin (ADR-0025); thrown when a plugin's own declared dependency is not present (`v0.13.0`) — backfilled `WP 16.2A` |
| `PluginSignatureVerificationFailedException` | `PluginException` | Plugin Trust & Dependencies | Isolated per plugin (ADR-0025, ADR-0112); thrown when a plugin's own signature does not verify against `IPluginTrustStore` (`v0.13.0`) — backfilled `WP 16.2A` |
| `PluginTrustDeniedException` | `PluginException` | Plugin Trust & Dependencies | Isolated per plugin (ADR-0025, ADR-0109/ADR-0112); thrown when a verified plugin's own trust tier does not authorize the capability it requests (`v0.13.0`) — backfilled `WP 16.2A` |
| `PluginUnsignedLoadNotAllowedException` | `PluginException` | Plugin Trust & Dependencies | Isolated per plugin (ADR-0025, ADR-0112); thrown when an unsigned plugin is loaded outside the policy that permits it (`v0.13.0`) — backfilled `WP 16.2A` |

**Staleness disclosed by `WP 9.0A`, resolved by `WP 16.2A`.** This
register's own Entries table had not been fully updated since `WP 6.6`
(2026-07-29) — every `Tempest.Core.EngineeringDomain`,
`Tempest.Core.Requirements`, `Tempest.Core.Calculations`,
`Tempest.Core.Materials`, `Tempest.Core.UnitsAndQuantities`,
`Tempest.Core.EngineeringData` exception type from `v0.7.0`/`v0.8.0`
onward, and every `v0.13.0` Plugin Trust exception, was genuinely
missing. `WP 16.2A` re-derived the register directly against
`src/Tempest.Core/` and backfilled all 29 missing rows (each marked
"backfilled `WP 16.2A`" above); see this register's own **Last
Reviewed** field and
`docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`
for the full derivation.

**Total: 89 custom exception types — Verified directly against
`src/Tempest.Core/` (`grep -rEn "^public (sealed |abstract )?class \w+Exception\b" src/Tempest.Core --include=*.cs`
returns exactly 89 matches, matching the 89 rows in the Entries table (87 at the `WP 16.4B` integration, plus `DuplicateStateMigrationException` and `ConflictingStateMigrationException` at `WP 16.4B-R1`, 2026-09-05; 84 at `WP 16.2A`, plus `RequirementGroupCycleException`, `ServiceRegistrationException` and `DuplicateServiceRegistrationException` at the `WP 16.4B` integration)
above, re-derived directly by `WP 16.4B-R1`). Corrected,
`WP 5.4`: this total previously read "30," undercounting
by one against this register's own Entries table and Distribution table
(both of which had, at that point, always summed to 31) — a genuine, internal
arithmetic drift found during `WP 5.4`'s own repository review, not a
change in the actual exception count.**

## A Note on Background Services

No dedicated exception type exists for Background Services (WP 4.5) —
**Verified**: `HostedServiceManager` isolates or rethrows the hosted
service's *own* exception directly (any `Exception`, including a plain
`InvalidOperationException` in the test fixtures), rather than wrapping it
in a TempestOS-specific type. This is a deliberate design choice, not a
gap — `Background Services Architecture.md` and the WP 4.5 implementation
retrospective describe isolation/escalation as a matter of *catching and
classifying* the service's own exception, never replacing it with a
platform-defined one. Contrast with `PluginException` and
`ModuleDiscoveryException`, which do wrap failures in a dedicated
hierarchy.

## A Note on Command Framework

`CommandException` and its four subtypes introduce a genuinely new
Host-Fatal/Isolated classification (Case 5 of *Failure Isolation Across
TempestOS*): **propagates to the caller** — neither Host-fatal (it does
not fault the Host) nor per-module isolated in the general case (a
handler's own exception, thrown from `DispatchAsync`/`InvokeAsync`, is
not automatically caught by any existing mechanism unless the call
happens to occur during a module's own lifecycle method, in which case
`ModuleLifecycleException`'s existing isolation applies incidentally, not
because the Command Framework itself isolates anything). This is a
deliberate, reasoned divergence from every prior exception category in
this register — see ADR-0038.

## A Note on Module Discovery (WP 5.3)

`ModuleDiscoveryException`'s existing role is unchanged — Host-fatal,
per ADR-0013, exactly as it has been since `WP 2.1`. What changed is
*when* it is thrown: a module type with no `[ModuleMetadataAttribute]`
and no public parameterless constructor previously fell through to
`Activator.CreateInstance`, which throws a raw `MissingMethodException`
with no actionable content. `ReflectionFrameworkDiscoveryService.
CreateDescriptor` now checks for this precondition explicitly first,
raising `ModuleDiscoveryException` with a message naming the actual fix
(add the attribute, or add a parameterless constructor) — closing a gap
`Building a Module.md` has documented in prose since `WP 4.1` but the
code itself never enforced. No new exception type; no new failure
category.

## A Note on Diagnostics

`WP 5.2` introduces no new exception type — confirmed directly, not by
omission. `DiagnosticsProvider`'s constructor throws only the ordinary
`ArgumentNullException` already used throughout this codebase for
constructor-parameter validation (`ArgumentNullException.ThrowIfNull`),
and `IDiagnosticsProvider`'s three properties (`HostState`, `Modules`,
`HostedServices`) have no failure mode of their own to raise — each
either returns a live value or an empty collection, never throws (see
`Diagnostics Architecture.md`'s own Failure Model). `CompositeLogSink`
likewise introduces no new exception type: its own constructor reuses
`ArgumentNullException`/`ArgumentException` for validation, and its
`Write` method deliberately catches and reports every child sink's own
exception rather than throwing a new, wrapping one.

## A Note on Notifications

`NotificationException` mirrors `AuditException`'s own base-only
precedent exactly: a concrete, single-constructor base type introduced
for the approved contract's own sake, never thrown directly this
release — every current Notification failure mode (a null handler, a
null notification, an invalid `Category`/`Message` on
`PlatformNotification`) is already fully covered by
`ArgumentNullException`/`ArgumentException`. Application logic's own
error (not Host-level); `NotificationDispatcher`'s own per-subscriber
isolation (mirroring `EventBus`, `ADR-0028`/`ADR-0046`) catches and logs
a subscriber's own exception at `Warning`, never rethrowing it, so no
Notification-specific exception type was needed for that path either.

## A Note on Reporting

`ReportingException`/`DuplicateReportDefinitionException`/
`ReportDefinitionNotFoundException` mirror
`SettingsException`/`DuplicateSettingDefinitionException`/
`SettingNotFoundException`'s own base-plus-subtype shape exactly —
`DuplicateReportDefinitionException` is thrown by `RegisterDefinition`
(first registration wins), `ReportDefinitionNotFoundException` by
`GenerateAsync` for an unregistered Id. Application logic's own error
(not Host-level); a renderer's own exception, thrown from
`RenderAsync`, propagates through `GenerateAsync` unmodified rather
than being wrapped in a Reporting-specific type — mirroring the Command
Framework's own dispatch failure model (`ADR-0038`), not the Event
Bus's or Notification Dispatcher's own per-subscriber isolation.

## A Note on the REST API

`ApiException`/`DuplicateApiRouteException` mirror `ReportingException`/
`DuplicateReportDefinitionException`'s own base-plus-subtype shape
exactly — thrown by `IApiEndpointRegistry.MapCommand` for a colliding
method + path, first registration wins. A request-time failure
(unmapped route, missing/unauthorized identity, a dispatched command's
own exception) is deliberately **not** modelled as a custom exception
type at all — `ApiRequestHandler` maps each case directly to an HTTP
status code (404/401/403/500) and returns it as an ordinary
`ApiResponse`, never throwing across its own public `HandleAsync`
boundary (`CommandNotFoundException`/`OperationCanceledException`
aside, both already-existing types it catches or lets propagate,
respectively). Application logic's own error (not Host-level); see
`ADR-0048`.

## Distribution by Root Category

| Root Category | Exception Count |
|---|---|
| Configuration | 4 |
| Dependency Injection | 6 |
| Module Discovery | 2 |
| Module Registration | 3 |
| Module Lifecycle | 2 |
| Plugin Manifest | 6 |
| Runtime Host | 2 |
| Background Services | 0 (by design — see note above) |
| Navigation | 3 |
| Command Framework | 5 |
| Identity & Permissions | 3 |
| Persistence | 2 |
| Settings | 3 |
| Audit | 1 |
| Notifications | 1 |
| Reporting | 3 |
| REST API | 2 |
| Export/Import | 4 |
| Licensing | 2 |
| Calculations | 4 |
| Materials | 3 |
| Units & Quantities | 1 |
| Engineering Data | 2 |
| Engineering Domain | 9 |
| Engineering Workflow | 4 |
| Requirements | 6 |
| Plugin Trust & Dependencies | 6 |

**Total: 4+6+2+3+2+6+2+0+3+5+3+2+3+1+1+3+2+4+2+4+3+1+2+9+4+6+6 = 89**,
matching the Entries table above and the direct `grep` count
(`WP 16.4B-R1`, re-derived row by row against the Entries table above,
2026-09-05). **Correction, `WP 16.4B-R1`:** this table had not been
updated when `ServiceRegistrationException`/`DuplicateServiceRegistrationException`
(Dependency Injection) and `RequirementGroupCycleException` (Requirements)
were added at the `WP 16.4B` integration — it still summed to 84 against
an 87-row Entries table. Dependency Injection corrected 4 → 6 and
Requirements 5 → 6 for that pre-existing gap, and both, plus Engineering
Domain, again for this Work Package's own two new rows (Engineering
Domain 7 → 9). The eight rows from "Calculations" through "Plugin Trust
& Dependencies" were added at `WP 16.2A`; every other row is
unchanged from `WP 6.6`, re-verified directly.

## Cross-Reference Check

Every exception's Host-fatal/isolated classification above matches
`docs/architecture/Failure Behaviour.md`'s own "Required Behaviour
Summary" table — no discrepancy found. Every exception type is exercised
by at least one test in `Test Register.md`.

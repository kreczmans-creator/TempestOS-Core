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
| **Last Reviewed** | 2026-07-29 (WP 6.3, REST API) — added `ApiException`, `DuplicateApiRouteException` (see Entries table, below); no other change to prior entries. |
| **Related Documents** | `docs/architecture/Failure Behaviour.md`; `Architectural Dependency Register.md`. |
| **Related ADRs** | ADR-0013, ADR-0021, ADR-0025, ADR-0038, ADR-0040, ADR-0046, ADR-0047, ADR-0048. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/01-exception-design.md`. |
| **Coverage Status** | Complete. |

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

**Total: 46 custom exception types — Verified directly against
`src/Tempest.Core/` (`grep -rlP "^public (sealed )?class \w+Exception\b"`
returns exactly 46 files, matching the 46 rows in the Entries table
above, re-derived directly by `WP 6.3` rather than incremented from the
prior figure — the standing practice `WP 5.4` recommended). Corrected,
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
| Dependency Injection | 4 |
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

## Cross-Reference Check

Every exception's Host-fatal/isolated classification above matches
`docs/architecture/Failure Behaviour.md`'s own "Required Behaviour
Summary" table — no discrepancy found. Every exception type is exercised
by at least one test in `Test Register.md`.

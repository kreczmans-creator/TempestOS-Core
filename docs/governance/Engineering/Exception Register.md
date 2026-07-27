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
| **Last Reviewed** | 2026-07-27 (WP 5.0B). |
| **Related Documents** | `docs/architecture/Failure Behaviour.md`; `Architectural Dependency Register.md`. |
| **Related ADRs** | ADR-0013, ADR-0021, ADR-0025. |
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

**Total: 25 custom exception types — Verified directly (adds
`NavigationException` and two subtypes, `WP 5.0B`).**

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

## Cross-Reference Check

Every exception's Host-fatal/isolated classification above matches
`docs/architecture/Failure Behaviour.md`'s own "Required Behaviour
Summary" table — no discrepancy found. Every exception type is exercised
by at least one test in `Test Register.md`.

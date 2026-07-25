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
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/architecture/Sample Module Architecture.md`; `Platform Services Register.md`; `Event Catalogue.md`. |
| **Related ADRs** | ADR-0001 through ADR-0004 (module identity, lifecycle, disposal), ADR-0027 (`ModuleMetadataAttribute`). |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/03-building-a-module.md`; `04-building-an-event-driven-module.md`; `docs/academy/03 Work Packages/WP4.3-sample-module-architecture.md`, `WP4.3-sample-module-implementation.md`, `WP4.4E-sample-module-event-integration.md`. |
| **Coverage Status** | Complete. |

---

## Entries

| Module | Namespace | Base Type | Uses `ModuleMetadataAttribute` | Constructor-Injects | Originating Work Package |
|---|---|---|---|---|---|
| `ClockModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IEventBus` | WP 4.3 (created), WP 4.4E (extended to publish events) |
| `ClockLifecycleObserverModule` | `Tempest.Samples` | `ModuleLifecycleBase` | Yes | `IEventBus` | WP 4.4E |

**Total: 2 production modules — Verified directly against
`src/Samples/Tempest.Samples/*.cs`.**

## SDK Base Types (Not Modules Themselves)

| Type | Namespace | Role |
|---|---|---|
| `ModuleBase` | `Tempest.Core.Modules` | Identity only (`Id`/`Name`/`Version` via constructor) for a module with no lifecycle |
| `ModuleLifecycleBase` | `Tempest.Core.Modules` | Extends `ModuleBase` with four `virtual`, no-op-by-default lifecycle methods |

Both are abstract, introduced by WP 4.1 (Module SDK) — see
`Platform Services Register.md`'s "Module SDK" entry.

## Test-Only Module Fixtures (Out of Scope, Noted for Completeness)

Six additional concrete `IModule`/`ModuleBase` implementations exist under
`tests/Tempest.Core.Tests/` (**Verified** by direct grep) — these are
deliberately excluded from this register's own count because they exist
solely to exercise Discovery/Registration/Lifecycle in isolation (healthy
modules, a duplicate-ID module, a blocking module, a disposal-tracking
module, and similar), never shipped or discoverable outside the test
assembly. Full detail is tracked by `Test Register.md`, not duplicated
here.

## Cross-Reference Check

Both production modules are cited by name in `Platform Services Register.md`
(Event Bus's "first real consumer"), `Event Catalogue.md` (as
publisher/subscriber of `ClockModuleLifecycleEvent`), and at least one
Work Package retrospective each. No production module exists that is not
also covered by at least one test in `Test Register.md`.

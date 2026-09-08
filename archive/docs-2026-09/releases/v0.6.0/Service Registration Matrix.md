# TempestOS v0.6.0 — Service Registration Matrix

## Purpose

A single, complete table of every proposed `v0.6.0` service's DI
lifetime, registration owner, dependencies, consumers, and Host
Lifecycle phase — the concrete registration-level companion to
`Platform Services Overview.md`'s prose and `Service Lifecycle.md`'s
per-service narrative. Exists so `WP 6.8` (and whoever wires
`TempestHost`'s own Platform Services Registered block during
implementation) has one row-per-service reference to check against,
rather than reconstructing it from eight separate documents each time.

## Matrix

| Service | DI Lifetime | Registration Owner | Dependencies | Consumers | Host Lifecycle Phase |
|---|---|---|---|---|---|
| Persistence (`IPersistenceStore`) | Singleton | `WP 6.4` (established as part of its own scope) | Dependency Injection | Settings, Audit | 6 (Platform Services Registered) |
| Reporting (`IReportingService`) | Singleton | `WP 6.0` | Dependency Injection; optionally invoked via Command Framework | Any module registering a report definition; REST API (optional, future) | 6 |
| Identity & Permissions (`ICurrentPrincipalAccessor`, `IPermissionEvaluator`) | Singleton | `WP 6.1` | Dependency Injection | REST API, Audit, any module performing an authorization check | 6 |
| Notifications (`INotificationDispatcher`) | Singleton | `WP 6.2` | Dependency Injection, Event Bus | Any module or service raising a notice | 6 |
| REST API — `IApiEndpointRegistry` | Singleton | `WP 6.3` | Dependency Injection | Any module registering a route | 6 |
| REST API — hosted service scaffold | Singleton (self-referential, per `IHostedService` convention) | `WP 6.3` | Background Services, Command Framework, Identity & Permissions | External HTTP clients | Start 8.1, Stop 10.1 |
| Settings (`ISettingsProvider`) | Singleton | `WP 6.4` | Dependency Injection, Persistence, Event Bus | Any module with runtime-mutable configuration; REST API (optional, future) | 6 |
| Audit (`IAuditRecorder`, `IAuditQuery`) | Singleton | `WP 6.5` | Dependency Injection, Persistence, Identity & Permissions | Any service recording an attributable action; REST API | 6 |
| Licensing — `ILicenseValidator` | Not container-registered — constructed directly, pre-container | `WP 6.6` | None (deliberately a leaf) | `TempestHostBuilder`/`TempestHost` (Composition Root, startup gate) | Before Phase 1 completes |
| Licensing — `ILicenseProvider` | Singleton (`AddInstance`) | `WP 6.6` | None (wraps the validated `ILicense`) | Any module checking entitlement | 6 |
| Export/Import (`IExportService`, `IImportService`) | Singleton | `WP 6.7` | Dependency Injection; reads from whatever service owns the exported data | Any module offering portable data exchange | 6 |

## Reading the Matrix

- **Every proposed service except the REST API's hosted-service scaffold
  and Licensing's validator is an ordinary `Singleton`, container-
  constructed registration** — consistent with the dominant,
  already-established pattern this platform uses for a service with no
  orchestration authority of its own (`Release Architecture.md`'s own
  observation about the Event Bus, Navigation, Command Framework, and
  Diagnostics all independently reaching this shape).
- **`WP 6.4` (Settings) is the only Work Package registering two
  distinct public contracts under one Work Package's ownership**
  (`ISettingsProvider` plus the co-established `IPersistenceStore`) —
  named explicitly so `WP 6.8`'s own review does not mistake this for
  two Work Packages' scope bleeding together; it is `ADR-0041`'s own
  deliberate, disclosed arrangement.
- **`WP 6.3` (REST API) is the only Work Package registering two
  differently-lifecycled contracts** — an ordinary Phase-6 singleton
  (`IApiEndpointRegistry`, for route registration during module
  initialisation) and a Hosted Service (the actual HTTP listener,
  Phase 8.1/10.1) — reflecting the genuine, deliberate split between
  "declaring what routes exist" (available as soon as any module wants
  to register one) and "actually listening for HTTP requests" (which
  must wait until Module Initialisation completes, per `IHostedService`'s
  own existing contract).
- **`WP 6.6` (Licensing) is the only Work Package with a
  non-container-registered contract** — `ILicenseValidator` is
  deliberately never resolved through the container, since it must run
  before the container exists at all (see `Service Lifecycle.md`).
- **No proposed service in this matrix depends on the REST API** — its
  own "Consumers" column (external HTTP clients) is the only one in this
  release that is not another platform service or module, confirming
  the REST API is a genuine leaf-consumer surface, not itself a
  dependency of anything else in `v0.6.0`.

## Registration-Order Requirement Within Phase 6

Because the container resolves a dependency graph rather than a
strictly ordered list, registration order within `TempestHost`'s own
Platform Services Registered method body does not need to match
dependency order for the container to function correctly — but for
human readability, the existing Phase 6 block's own top-to-bottom
convention (each registration appearing after whatever it depends on)
should be preserved. The recommended registration order within Phase 6,
consistent with `Platform Service Implementation Order.md`'s own
dependency analysis:

1. Persistence
2. Identity & Permissions
3. Licensing's `ILicenseProvider` (wraps the already-validated license
   from before Phase 1)
4. Reporting
5. Notifications
6. Settings (after Persistence)
7. Audit (after Persistence and Identity & Permissions)
8. Export/Import
9. REST API's `IApiEndpointRegistry` (the hosted-service scaffold itself
   is registered as a hosted service, discovered separately, not part of
   this ordered list)

## Related Documents

`Platform Services Overview.md`; `Platform Service Dependency Diagram.md`;
`Service Lifecycle.md`; `Platform Service Implementation Order.md`;
`Platform Service Contracts.md`; `docs/architecture/Host Lifecycle.md`;
`ADR-0009`, `ADR-0017`, `ADR-0029`, `ADR-0030`.

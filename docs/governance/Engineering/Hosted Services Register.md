# Hosted Services Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Hosted Services Register |
| **Purpose** | The index of every real (non-test-fixture) `IHostedService` implementation TempestOS ships, distinct from the Background Services *infrastructure* itself (discovery, orchestration), which is fully implemented and tracked by `Platform Services Register.md`. |
| **Scope** | Concrete classes implementing `IHostedService` under `src/`, excluding the infrastructure types (`HostedServiceDiscoveryService`, `HostedServiceManager` themselves are orchestrators, not hosted services). |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `src/` (direct source inspection); `docs/academy/03 Work Packages/WP4.5-background-services-implementation.md`. |
| **Review Frequency** | Updated whenever a new production hosted service is added anywhere under `src/`. |
| **Last Reviewed** | 2026-07-29 (WP 6.3, REST API) — populated for the first time since `WP 4.5A`; found and corrected a genuine, disclosed governance-documentation drift (see Coverage Note, below). |
| **Related Documents** | `docs/architecture/Background Services Architecture.md`; `Platform Services Register.md`; `Module Register.md` (the same "real consumer vs. infrastructure" distinction applies). |
| **Related ADRs** | ADR-0021, ADR-0029, ADR-0030, ADR-0047. |
| **Related Academy Articles** | `docs/academy/03 Work Packages/WP4.5-background-services-architecture.md`, `WP4.5-background-services-implementation.md`; `docs/academy/02 Runtime Architecture/08-failure-isolation.md` (Case 2); `WP6.2-notification-framework-implementation.md`; `WP6.3-rest-api-implementation.md`. |
| **Coverage Status** | Complete. |

---

## Coverage Note

The Background Services *infrastructure*
(`IHostedServiceDiscoveryService`/`HostedServiceDiscoveryService`,
`IHostedServiceManager`/`HostedServiceManager`) has been fully
implemented and tested since `WP 4.5` — see `Platform Services
Register.md`. **A disclosed governance-documentation finding:** this
register itself was never updated when `WP 6.2` added
`NotificationSampleHostedService` — the first real, non-infrastructure
`IHostedService` this codebase shipped — so its own "Zero production
hosted services exist" text survived, stale, through an entire Work
Package that directly contradicted it. Found and corrected here, during
`WP 6.3`'s own repository review, at the same time this register gains
its second real entry, `RestApiHostedService` — the Work Package
`AT-07`'s own revisit trigger explicitly named in advance (`Required
ADRs.md`'s `ADR-0047` entry). See `docs/governance/Quality/Technical
Debt Register.md`'s own `AT-07` entry, now Retired.

## Entries

| Hosted Service | Namespace | Introduced | Critical? | Purpose |
|---|---|---|---|---|
| `NotificationSampleHostedService` | `Tempest.Samples` | WP 6.2 | No (isolated by default, ADR-0021) | Publishes an `IPlatformNotification` on `StartAsync`/`StopAsync`, proving the Notification Framework's own "Background notifications" deliverable end-to-end; the codebase's first real, non-infrastructure hosted service, though this Work Package explicitly did not claim `AT-07`'s own retirement. |
| `RestApiHostedService` | `Tempest.Core.Api` | WP 6.3 | No (isolated by default, ADR-0021) | Hosts the REST API's own HTTP listener (ASP.NET Core/Kestrel, `ADR-0049`), started Phase 8.1, stopped Phase 10.1 (`ADR-0047`) — the Work Package `AT-07`'s own revisit trigger explicitly named. A start failure (for example, the configured port already in use) is isolated, proven directly by a dedicated test. |

## Test-Only Hosted Service Fixtures (Out of Scope, Noted for Completeness)

Ten concrete `IHostedService`/`ICriticalBackgroundService` fixtures exist
under `tests/Tempest.Core.Tests/BackgroundServices/HostedServiceFixtures.cs`
(**Verified** by direct file inspection) — `AlphaHostedService`,
`BetaHostedService`, `GammaHostedService`, `CancellingHostedService`,
`IsolatedThrowingHostedService`, `CriticalStartFailureHostedService`,
`CriticalStopFailureHostedService`, `ConstructorInjectedHostedService`,
`AbstractHostedService`, `GenericHostedService<T>`. These exist solely to
prove discovery, ordering, and failure-isolation behaviour in isolation;
full detail is tracked by `Test Register.md`, not duplicated here.

## Cross-Reference Check

This register's Partial status and its Reason/Review Trigger are directly
consistent with `Platform Services Register.md`'s own "Background
Services: Implemented" entry — the two are not contradictory: the
*infrastructure* is complete and Implemented; the *catalogue of hosted
services running on it* is empty by deliberate, documented choice.

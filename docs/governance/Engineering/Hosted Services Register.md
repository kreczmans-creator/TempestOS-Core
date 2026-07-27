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
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/architecture/Background Services Architecture.md`; `Platform Services Register.md`; `Module Register.md` (the same "real consumer vs. infrastructure" distinction applies). |
| **Related ADRs** | ADR-0021, ADR-0029, ADR-0030. |
| **Related Academy Articles** | `docs/academy/03 Work Packages/WP4.5-background-services-architecture.md`, `WP4.5-background-services-implementation.md`; `docs/academy/02 Runtime Architecture/08-failure-isolation.md` (Case 2). |
| **Coverage Status** | Partial. |

---

## Coverage Note

**Reason for Partial coverage.** The Background Services *infrastructure*
(`IHostedServiceDiscoveryService`/`HostedServiceDiscoveryService`,
`IHostedServiceManager`/`HostedServiceManager`) is fully implemented and
tested (WP 4.5) — see `Platform Services Register.md`. **Zero production
hosted services exist** (**Verified** by direct grep of `src/` for
concrete `IHostedService` implementations outside
`Tempest.Core.BackgroundServices`'s own infrastructure types) — this is a
deliberate scope decision recorded in the WP 4.5 implementation
retrospective ("do not yet build feature-rich Background Services; any
sample service should exist solely to validate the infrastructure"), not
an oversight or gap.

**Review Trigger.** The first Work Package that adds a real, shipped
`IHostedService` implementation (to `Tempest.Samples` or elsewhere) must
populate this register's Entries table with at least one row and update
Coverage Status to Complete or the appropriate ongoing status.

## Entries

*(none — see Coverage Note, above)*

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

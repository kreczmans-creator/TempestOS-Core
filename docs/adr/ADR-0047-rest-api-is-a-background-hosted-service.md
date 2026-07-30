# ADR-0047: The REST API Is a Background Hosted Service

## Status

Accepted — `WP 6.3` (REST API), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.3`'s own implementation
phase. `Required ADRs.md` named this Work Package's own required ADR:
the REST API is a long-running process that must start after modules
initialise and stop before they dispose — exactly the shape
`IHostedService` (`WP 4.5`) was built for, and the first Work Package
with a genuine reason to use it as a real, non-infrastructure consumer
(`AT-07`).

## Decision

**`RestApiHostedService` implements `IHostedService`, discovered and
orchestrated identically to any other hosted service** — started Phase
8.1, stopped Phase 10.1 (`ADR-0030`), exactly as anticipated. No new
Host Lifecycle phase; `Service Lifecycle.md`'s own five-point REST API
description is realised without modification.

**Isolated by default, not critical (`ADR-0021`).** `RestApiHostedService`
does not implement `ICriticalBackgroundService` — a start failure (for
example, the configured port already in use) is caught, logged, and
isolated, exactly like `NotificationSampleHostedService`'s own default,
proven directly by a dedicated test that occupies the configured port
before starting the Host and confirms it still reaches `Running`. This
was a live, disclosed judgment call: a REST API that silently fails to
start could look deceptively "working" to an operator who does not
check logs, which is a real argument for criticality — but no approved
contract or brief instruction names this as Host-fatal, and treating an
optional network-facing surface as able to abort the entire platform's
own startup was judged disproportionate, mirroring every other hosted
service's own default in this codebase.

**`IApiEndpointRegistry` is registered as an ordinary DI-public,
container-constructed Phase 6 singleton**, immediately after Audit, so
any module may map a route during its own `InitialiseAsync`, before the
hosted service itself ever starts listening — matching `Service
Registration Matrix.md`'s own recommended registration order and Service
Lifetime dimension.

## Consequences

**Positive:**

- `AT-07` ("Zero real hosted services exist beyond the infrastructure")
  is genuinely retired by this Work Package — the revisit trigger
  `Service Lifecycle.md` itself named. `NotificationSampleHostedService`
  (`WP 6.2`) was explicitly disclosed as *not* claiming this retirement;
  this Work Package is the one that does.
- No adaptation of `ADR-0029`/`ADR-0030` was required — proving those
  two ADRs' own design genuinely generalises to a real, substantial
  consumer, not merely the sample fixtures that originally exercised it.

**Negative:**

- A REST API failing to start (port conflict, misconfiguration) is
  logged but does not itself block the platform from reaching
  `Running` — an operator relying solely on the Host reaching `Running`
  as a health signal would miss this. Disclosed here, not silently
  accepted; a future Work Package could reconsider criticality if this
  proves a real operational problem.

## Alternatives Considered

**A bespoke Host-level phase dedicated to "network services."**
Rejected per `Required ADRs.md`'s own anticipated decision —
`IHostedService` already fully describes the start-after-Initialisation/
stop-before-Disposal lifecycle the REST API needs; inventing a parallel
mechanism would duplicate `WP 4.5` for no architectural benefit.

**Marking `RestApiHostedService` critical (`ICriticalBackgroundService`).**
Considered, given a REST API's own external visibility — rejected as
disproportionate for this release's own scope; no approved contract
names this requirement, and doing so would make an optional network
surface able to abort the entire platform's own startup, a change with
real operational consequences no Work Package brief authorised.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (the REST API's own 15-dimension
contract); `Service Lifecycle.md` (the REST API's own five-point hosted-
service description this ADR realises); `ADR-0021` (isolated-by-default
hosted-service failure); `ADR-0029`/`ADR-0030` (hosted-service discovery/
orchestration and lifecycle placement, reused without adaptation);
`docs/governance/Quality/Technical Debt Register.md` (`AT-07`, retired);
`docs/academy/03 Work Packages/WP6.3-rest-api-implementation.md`.

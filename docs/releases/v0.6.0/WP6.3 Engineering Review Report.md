# WP 6.3 — REST API — Engineering Review Report

## Purpose

A self-review of `WP 6.3`'s own implementation against the constraints
its own brief imposed and this project's standing Engineering
Governance, mirroring every prior Work Package's own Engineering Review
Report format.

## Constraint Checklist

| Constraint | Result | Evidence |
|---|---|---|
| Implement exactly as defined by approved architecture and contract documentation | **Met** | `IApiEndpointRegistry`, `ApiRouteDescriptor` implemented with zero signature deviation from `Public Interface Catalogue.md`. Registration matches `Service Registration Matrix.md`/`Service Lifecycle.md` — `IApiEndpointRegistry` Phase 6, `RestApiHostedService` started 8.1/stopped 10.1. |
| The REST API shall expose Platform Services only; remain a thin transport layer; no business logic in controllers/endpoints | **Met** | `ApiSampleModule` contains a single `MapCommand` call and nothing else — the purest possible proof. `ApiRequestHandler` itself contains route lookup, identity resolution, and permission enforcement, never a line of domain logic; every dispatched command's own handler is where business logic lives. |
| No architectural redesign absent a genuine implementation defect | **Met, with one disclosed, resolved exception** | `CurrentPrincipalAccessor` was considered for modification (per `Risk Register.md`'s own `R1`), tested directly, found to regress 17 tests, and left unchanged — the constraint was honoured by *not* redesigning it once the empirical evidence argued against doing so. |
| API Host, API registration, versioning infrastructure, endpoint discovery, authentication abstraction, authorisation integration, OpenAPI generation, DI registration, Hosted Service integration, logging, diagnostics | **Met, per the Implementation Report's own Scope Delivered table** | Every dimension delivered except Diagnostics, deliberately declined per this Work Package's own identical precedent to `WP 6.0`–`WP 6.2`. |
| Integrate with Identity, Settings, Audit, Notifications, Reporting; do not introduce unnecessary coupling | **Met** | Identity and Audit are genuine, justified, core-level dependencies of `ApiRequestHandler` itself (the approved contract requires both). Settings, Notifications, and Reporting are consumed only at the sample-module calling layer (`ApiSampleModule` exposing `ReportingSampleModule`'s own command) — see `WP6.3 Platform Integration Demonstration.md`. |
| Produce only implementation-driven ADRs | **Met** | `ADR-0047`/`ADR-0048`/`ADR-0049` — exactly the three `Required ADRs.md` named as originating from `WP 6.3`. `ADR-0052` is additionally produced, genuinely implementation-driven (documenting the empirically-tested identity-resolution decision), consistent with this Work Package's own brief ("if deviation is required... produce the appropriate ADR"). |
| Comprehensive testing across every named category | **Met** | 45 new tests across unit, integration, endpoint, authentication, failure-injection, hosted-service, regression, and concurrency categories. |
| Demonstrate each endpoint consuming Platform Services without introducing business logic | **Met** | `WP6.3 Platform Integration Demonstration.md` — a dedicated, per-service record. |
| Clean Debug/Release build, complete automated tests, static analysis, documentation validation, dependency validation | **Met** | 0 warnings/0 errors, both configurations, from a clean rebuild; 914/914 tests passing, both configurations, each re-run three times for stability; dependency validation performed directly (see below). |
| Stop after WP 6.3 | **Met** | No file under any other Work Package's own scope was created or modified. |

## Platform Impact Assessment

See `WP6.3 Platform Impact Assessment.md` for the complete, dedicated
assessment of whether this Work Package confirms, extends, or exposes a
weakness in the platform architecture.

## Four-Layer / Governance Confirmation (Re-Verified Against Real Code)

1. **Four-layer dependency rules.** `Tempest.Core.Api` depends only on
   Dependency Injection, `Tempest.Core.Audit`,
   `Tempest.Core.BackgroundServices`, `Tempest.Core.Commands`,
   `Tempest.Core.Configuration`, `Tempest.Core.Identity`, and
   `Tempest.Core.Logging` (all existing Platform Services/DI), plus
   ASP.NET Core — confirmed by direct inspection of every `using`
   directive in `src/Tempest.Core/Api/`. No dependency on any Module.
2. **No circular dependencies.** Confirmed directly:
   `grep -rl "Tempest.Core.Api" src/Tempest.Core --include=*.cs` finds
   only `TempestHost.cs` (the registration site itself) outside
   `src/Tempest.Core/Api/` — no platform service depends back on the
   REST API.
3. **No layering violations.** The REST API sits above Identity, Audit,
   Commands, and BackgroundServices (depends on all four; none depends
   on it) — confirmed by the same direct inspection.
4. **No public interface overlap.** `IApiEndpointRegistry`'s own
   `MapCommand`/`Routes` shape is distinct in purpose from
   `ICommandRegistry`'s own `RegisterDescriptor`/`Items`/`InvokeAsync` —
   the REST API's own registry maps HTTP routes to already-registered
   command Ids, never duplicating the Command Framework's own registry.
5. **No duplicated responsibilities.** The REST API is the only service
   in the shipped codebase with any "expose a command over HTTP"
   capability — confirmed directly.

## Findings Requiring Disclosure

1. **`CurrentPrincipalAccessor` was tested for migration to
   `AsyncLocal<T>` and found to regress 17 pre-existing tests** — built,
   run, measured, and reverted; disclosed fully in `ADR-0052`, this
   report, and the retrospective's own Observations, not silently
   discarded.
2. **No real authentication exists this release** — a bare, unverified
   `X-Identity-Id` header. Disclosed as `TD-13`, not built to look more
   secure than it is.
3. **A command handler relying on ambient-principal Audit attribution
   will record `"unknown"` when invoked via REST** — disclosed as
   `TD-15`, with the real caller identity preserved correctly in the
   REST API's own `Detail`-carried entry instead.
4. **`docs/governance/Engineering/Hosted Services Register.md` had
   never been updated since `WP 4.5A`**, despite `WP 6.2` shipping this
   codebase's first real hosted service — found during this Work
   Package's own repository review and corrected in the same commit.

## Verdict

`WP 6.3` meets every constraint its own brief imposed. Nothing approved
was redesigned; the REST API introduces zero business logic of its own,
proven by its own reference module containing exactly one method call;
the ASP.NET Core/Kestrel integration boundary was prototyped and
verified, not merely designed; and the one genuine architectural
question this Work Package was specifically positioned to answer
(`CurrentPrincipalAccessor`'s own request-scoping) was resolved with
real, measured evidence, not assumption.

## Related Documents

`WP6.3 Implementation Report.md`; `WP6.3 Platform Integration
Demonstration.md`; `WP6.3 Platform Impact Assessment.md`; `WP6.3
Lessons Learned.md`; `WP6.3 Technical Debt Assessment.md`; `WP6.3
Future Capability Recommendations.md`; `ADR-0047`; `ADR-0048`;
`ADR-0049`; `ADR-0052`; `docs/releases/v0.6.0/Governance
Confirmation.md` (the Contract Review's own design-time check this
report re-verifies against shipped code).

# WP 6.8 — Technical Debt Disposition

## Purpose

Classify every item in `docs/governance/Quality/Technical Debt
Register.md` — 16 tracked debt items, 13 disclosed trade-offs — into
exactly one of four dispositions for release certification purposes:
**Resolved** (fixed, no longer applicable), **Accepted** (a deliberate,
disclosed limitation this release ships with, not expected to change
without a real need), **Deferred** (a real, open item, not urgent, with
a named revisit trigger, not blocking this release), or **Release
Blocking** (must be fixed before `v0.6.0` can certify).

## Tracked Debt (TD)

| # | Item | Disposition | Rationale |
|---|---|---|---|
| TD-01 | Two logging mechanisms coexist | **Deferred** | Zero live callers of the legacy path since `WP 5.0D`; migrating dead code is pure risk with no behavioural benefit (`D-020`). Revisit trigger: legacy code revived or deleted. Does not affect `v0.6.0`'s own correctness. |
| TD-02 | Single-sink logging | **Resolved** | `CompositeLogSink` (`WP 5.2`). |
| TD-03 | No disposal tracking for `AddInstance`/reflection-constructed singletons | **Deferred** | No current platform service is disposable; genuinely no cost to leaving this alone until one is. |
| TD-04 | `IHostedService` naming proximity to ASP.NET Core's own type | **Deferred** | Trigger arguably met (`WP 6.3`), no confusion actually reported; a naming judgment call for a future Work Package, not a correctness defect. |
| TD-05 | Parameterless-constructor-only constraint on discovered modules | **Accepted** | Partially, deliberately lifted (`ADR-0027`); the remaining constraint is a considered design boundary, not an oversight. |
| TD-06 | Fixed Plugins directory/manifest name | **Accepted** | Disclosed as a purely additive future enhancement; zero current cost. |
| TD-07 | Navigation's `Tempest.Core` placement | **Resolved** | `ADR-0031` (`WP 5.0A`). |
| TD-08 | Background Services would need a second phase-table extension | **Resolved** | `WP 4.5` implemented Phases 8.1/10.1 exactly as anticipated, no renumbering. |
| TD-09 | No plugin/first-party trust isolation (widened to Commands, `WP 5.1A`) | **Deferred** | Enforcement mechanism exists (`IPermissionEvaluator`, `ADR-0044`); retrofit explicitly out of scope for every Work Package that has shipped so far. **Not release-blocking for `v0.6.0`**: `src/Plugins/` remains empty (`AT-06`) — no real third-party plugin exists this release to exploit the gap. Revisit trigger: real third-party plugin support. |
| TD-10 | `NavigationService.Unregister` has no ownership check | **Deferred** | Same reasoning as `TD-09` — mechanism exists, retrofit not yet scoped to any Work Package. Not release-blocking: no adversarial multi-tenant scenario exists in `v0.6.0`'s own approved scope. |
| TD-11 | Command/Navigation registration-order squatting | **Deferred** | Same reasoning as `TD-09`/`TD-10`. |
| TD-12 | `IPersistenceStore` has no native query/filter capability | **Accepted** | Deliberately not extended (`WP 6.5`'s own explicit Persistence Validation); revisit trigger is a real, measured performance problem, not speculative. `IAuditQuery` is proven fully correct against this shape by `AuditQueryTests`' own filter-correctness suite. |
| TD-13 | REST API has no real authentication | **Accepted** | Explicitly disclosed, this release's own first network-facing surface; mitigated (not fixed) by binding to loopback only by default (`ADR-0049`). A genuine, named future requirement (API keys/OAuth/mTLS), not a current-release defect — the approved contract never promised authentication this release. |
| TD-14 | No TLS for the REST API's Kestrel listener | **Accepted** | `Platform Service Contracts.md`'s own Security dimension names TLS as a "beyond local development" expectation, not a current requirement. |
| TD-15 | Ambient-principal Audit-attribution gap under REST invocation | **Accepted** | The real caller identity is not lost, only mis-routed — correctly carried in the REST API's own `api.request` entry's `Detail`. A narrow, disclosed, non-correctness-affecting gap for a *different* command's own separate `RecordAsync` call. |
| TD-16 | No cryptographic license file signature verification | **Accepted** | No concrete distribution channel or tamper-threat model exists yet in this release's own approved scope; mirrors `TD-13`'s own identical reasoning. |

**Disposition summary: 3 Resolved, 6 Accepted, 7 Deferred, 0 Release
Blocking.**

## Disclosed, Accepted Trade-offs (AT)

Every trade-off in this table is, by its own governing definition
(`Technical Debt Register.md`'s own "Governing Distinction"), already a
deliberate design exclusion — the disposition for all thirteen is
**Accepted** by construction, with one already **Resolved** (retired):

| # | Item | Disposition |
|---|---|---|
| AT-01 | No automatic Event Bus unsubscription | Accepted |
| AT-02 | Event Bus subscriber references held strongly | Accepted |
| AT-03 | Exact-event-type-only dispatch (Event Bus, Notifications) | Accepted |
| AT-04 | No hosted-service supervision after `StartAsync` | Accepted |
| AT-05 | No automatic hosted-service restart/backoff | Accepted |
| AT-06 | `src/Plugins/` remains empty | Accepted |
| AT-07 | Zero real hosted services beyond infrastructure | **Resolved** (Retired, `WP 6.3` — `RestApiHostedService` shipped) |
| AT-08 | No persistent notification model | Accepted |
| AT-09 | No Reporting delivery-channel abstraction or history | Accepted |
| AT-10 | No REST request-parameter binding | Accepted |
| AT-11 | No compression/encryption of exported artifact content | Accepted |
| AT-12 | No Export/Import schema-upgrade/migration path | Accepted |
| AT-13 | No remote validation/activation, floating licensing, or grace period | Accepted |

**Disposition summary: 1 Resolved, 12 Accepted, 0 Release Blocking.**

## Release-Blocking Assessment

**Zero items across both tables are classified Release Blocking.**
Every open (Deferred or Accepted) item was disclosed at the time its
owning Work Package shipped, approved by the same governance process
that approved that Work Package's own scope, and carries a named,
concrete revisit trigger rather than an open-ended "someday." None
represents an unannounced defect, a silently-abandoned requirement, or
a correctness gap in `v0.6.0`'s own approved contracts. The seven
Deferred security-adjacent items (`TD-09`, `TD-10`, `TD-11`, and the
REST API's own `TD-13`/`TD-14`/`TD-15`, plus `TD-16`) are each mitigated
by a concrete, verifiable fact specific to this release's own scope —
no real third-party plugin exists (`AT-06`), the REST API binds to
loopback only by default, and Licensing's own trust model mirrors
Identity's own already-accepted local-only posture (`ADR-0043`).

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (the complete,
authoritative source this disposition classifies); `WP6.8 Platform
Certification Report.md`; `WP6.8 Risk Register Disposition.md`;
`docs/security/Security Roadmap.md`.

# WP 7.1A — Engineering Data Model — Platform Consumption Assessment

## Purpose

For each `v0.6.0` Platform Service `WP7.0C Platform Integration
Matrix.md` named as a plausible consumer relationship, records whether
this Work Package's own real implementation actually used it, why, and
what the coupling looks like — mirroring `docs/releases/v0.6.0/WP6.0
Platform Integration Demonstration.md`'s own per-service assessment
format.

## Identity & Permissions

**Used. Confirmed.** `EngineeringDocumentStore` depends on
`ICurrentPrincipalAccessor` directly, resolving
`IDocumentRevision.AuthorPrincipalId` from
`Current?.Identity.Id ?? UnknownAuthorPrincipalId` — an exact mirror of
`AuditRecorder`'s own attribution pattern. No permission gating is
applied to any `IEngineeringDocumentStore` method — the approved
contract names no such requirement, unlike `IAuditQuery`'s own
`RequirePermission` gate.

## Persistence

**Used. Confirmed, and central to the design.** `EngineeringDocumentStore`
is built entirely on `IPersistenceStore`, resolved from the same
container-wide singleton Settings and Audit already share (proven
directly, `EngineeringDataHostRegistrationTests.
Host_EngineeringDataAndAudit_ShareTheSameIPersistenceStoreInstance`).
See `ADR-0053` for the full storage design.

## Settings

**Not used.** No configurable, user-changeable value exists in this
Work Package's own scope — consistent with `WP7.0C Platform Integration
Matrix.md`'s own prediction (Settings had no plausible consumer marked
for any Engineering Foundation framework).

## Audit

**Not used, directly.** `WP7.0C Platform Integration Matrix.md`'s own
Integration Note 1 marked this relationship "Plausible," not
"Confirmed," leaving whether document creation/revision should be
separately audited as an open question for the owning Work Package.
This Work Package's own decision: **no**, not automatically — mirroring
Reporting's own precedent (`WP 6.0`) that a Platform Service's own
internal operation is not itself an audited action; auditing, if
wanted, remains a calling module's own responsibility, exactly as every
`v0.6.0` sample module already demonstrates the pattern at the calling
layer, not inside the service.

## Reporting

**Not used.** No report definition or renderer was registered against
Engineering Data Model content in this Work Package's own scope —
correctly deferred, since no real consuming need exists yet.

## Export / Import

**Not used.** `WP7.0C Platform Integration Matrix.md` marked a document/
revision as a "Plausible export candidate" — this Work Package did not
implement `IExportable` for any Engineering Data Model type, since no
real export requirement has been named yet. A future Work Package
adding this integration would implement `IExportable`/`IExportableKind`
directly on a document-representing type, per `ADR-0044`'s own
dual-registration precedent — not attempted here.

## REST API

**Not used.** No route was mapped for any Engineering Data Model
operation. Consistent with this Work Package's own scope boundary — a
REST surface for the Engineering Data Model was never named as this
Work Package's own objective.

## Licensing

**Not used.** No capability gating was applied to any
`IEngineeringDocumentStore` method — consistent with `WP7.0C Platform
Integration Matrix.md`'s own prediction (no plausible consumer marked).

## Diagnostics

**Not used.** No Host-lifecycle-state concern exists in this Work
Package's own scope.

## Command Framework

**Used. Confirmed, at the sample-module layer only.**
`EngineeringDataSampleModule` registers
`CreateSampleDocumentCommand`/`ReviseSampleDocumentCommand` against the
real `ICommandDispatcher`/`ICommandRegistry` — the Engineering Data
Model's own public interface (`IEngineeringDocumentStore`) has no
Command Framework dependency itself; only the demonstrating sample
module does, exactly mirroring every other Platform Service's own
sample-module-vs-service-boundary.

## Summary Table

| Platform Service | Consumed? | Confirmed vs. Plausible (per `WP7.0C`) |
|---|---|---|
| Identity & Permissions | Yes | Confirmed |
| Persistence | Yes | Confirmed |
| Settings | No | Not predicted |
| Audit | No | Was Plausible; decided against, mirroring Reporting's own precedent |
| Reporting | No | Was Plausible; not yet needed |
| Export/Import | No | Was Plausible; not yet needed |
| REST API | No | Was Plausible; not yet needed |
| Licensing | No | Not predicted |
| Diagnostics | No | Not predicted |
| Command Framework | Yes (sample module only) | Not directly predicted for the service itself |

## Related Documents

`docs/releases/v0.6.0/WP6.0 Platform Integration Demonstration.md` (the
precedent this document's own format follows); `docs/releases/v0.7.0/
WP7.0C Platform Integration Matrix.md`; `WP7.1A Implementation
Report.md`.

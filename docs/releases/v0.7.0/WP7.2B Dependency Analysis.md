# WP 7.2B — Dependency Analysis

## Status

Architecture only. No production code accompanies this document.

## Purpose

Reviews the Systems Engineering Foundation's own integration with every
Platform Core and Engineering Core service this Work Package's own
controlling instruction names, confirming dependency direction,
ownership, lifetime, layering, and architectural responsibility for
each — mirroring `WP7.0C Cross-Framework Dependency Report.md`'s own
role for the original five Engineering Foundation frameworks.

## Analysis

| Service | Dependency Direction | Ownership | Lifetime | Layering | Architectural Responsibility |
|---|---|---|---|---|---|
| **Engineering Data** (`Tempest.Core.EngineeringData`) | Downward — Systems Engineering Foundation depends on it, never the reverse | Engineering Data Model owns storage, identity, revisioning; Systems Engineering Foundation owns interpretation of `Kind = "Requirement"`/`"RequirementCollection"`/`"RequirementGroup"` content | Singleton, container-constructed (already registered) | Engineering Core, consumed one layer up | Hard dependency — every requirement concept *is* an `IEngineeringDocument` |
| **Calculations** (`Tempest.Core.Calculations`) | Downward, optional | Calculation owns execution and recording; Systems Engineering Foundation owns only an unvalidated `Guid` reference to a calculation record | N/A — no direct service dependency, only a data reference | Engineering Core, referenced not consumed | Soft — a requirement may cite a calculation as supporting rationale, never requires one to exist |
| **Verification** (`Tempest.Core.Verification`) | Downward | Verification owns outcome recording and history; Systems Engineering Foundation owns nothing verification-related — pure consumption | Singleton, container-constructed (already registered) | Engineering Core, consumed one layer up | Hard dependency — `IVerificationService.RecordAsync` called directly against a requirement's own document Id |
| **Reporting** (`Tempest.Core.Reporting`) | Downward, optional, future | Reporting owns registration/dispatch/rendering; a future Requirements Traceability Report definition would be authored by whichever module registers it | Singleton, container-constructed (already registered) | Platform Core, referenced not consumed by the Foundation itself | Soft, future — no report is designed by this Work Package |
| **Export/Import** (`Tempest.Core.ExportImport`) | Downward, optional, future | Export/Import owns artifact framing; a Requirement Collection would author its own `IExportable`/`IImportable` implementation | Singleton, container-constructed (already registered) | Platform Core, referenced not consumed by the Foundation itself | Soft, future — no artifact format is designed by this Work Package |
| **REST API** (`Tempest.Core.Api`) | Downward, optional, future | The REST API owns route-to-command mapping; a future module choosing to expose Requirements operations owns the mapping call itself | Singleton, container-constructed (already registered) | Platform Core, referenced not consumed by the Foundation itself | Soft, future — no endpoint is designed by this Work Package |
| **Identity** (`Tempest.Core.Identity`) | Downward, calling-layer only | `IPermissionEvaluator`/`ICurrentPrincipalAccessor` are composed by whichever caller invokes the Systems Engineering Foundation — the Foundation itself never enforces authorization internally | Singleton (`IPermissionEvaluator`), `AddInstance` (`ICurrentPrincipalAccessor`) — both already registered | Platform Core, calling-layer composition, identical to every Engineering Core sibling | Hard dependency at the calling layer, mirroring `IReportingService`'s own explicit precedent |
| **Audit** (`Tempest.Core.Audit`) | Downward, calling-layer only | `IAuditRecorder` is composed by whichever caller performs a requirement create/revise/allocate action | Singleton, container-constructed (already registered) | Platform Core, calling-layer composition | Hard dependency at the calling layer, mirroring every existing sample module's permission-check-then-audit-record pattern |
| **Settings** (`Tempest.Core.Settings`) | None identified | N/A | N/A | N/A | No concrete need named at architecture time — not designed against speculatively, per Security Principle 7's own "do not build ahead of demonstrated need" discipline applied to product architecture generally |
| **Notifications** (`Tempest.Core.Notifications`) | None identified | N/A | N/A | N/A | No concrete need named at architecture time — a future consumer might want a status-change notification, but this is not required by the Foundation itself |
| **Licensing** (`Tempest.Core.Licensing`) | None identified | N/A | N/A | N/A | No relationship anticipated, mirroring how no existing Engineering Foundation framework has a Licensing dependency either |

## Layering Confirmation

**No violation of `ADR-0023`'s four-layer model, or of the three-layer
Systems Engineering extension (`WP7.2B Systems Engineering
Architecture.md`), is introduced by any dependency above.** Every real
dependency (Engineering Data, Verification, Identity, Audit) is
downward-only, to an already-certified Platform Core or Engineering Core
service. Every optional/future dependency (Calculations, Materials,
Reporting, Export/Import, REST API) is a plausible future consumer
relationship, not a present architectural commitment — none is designed
or implemented by this Work Package.

## No Circular Dependency Introduced

Confirmed directly against the same method `WP7.1F Engineering Core
Architecture Conformance Report.md` used: tracing every named dependency
above forward, none terminates back at the Systems Engineering
Foundation itself. In particular, `Tempest.Core.Verification`'s own
design — depending only on `EngineeringData`'s generic document concept,
never a concrete Requirements type — remains unmodified and confirmed
correct by this analysis: nothing in this dependency table proposes
adding a Requirements-specific type anywhere inside
`Tempest.Core.Verification`.

## What This Analysis Confirms Was Already True

Every Platform Core service this Work Package reviews was already
consumed, in the identical calling-layer pattern, by at least one
existing Engineering Foundation framework or sample module — this
analysis introduces no new integration *pattern*, only a new *consumer*
of patterns already proven correct (`Identity`/`Audit` calling-layer
composition, `Reporting`/`Export`/`Api` optional-future-consumer
shape). This is itself a confirming architectural finding: the Systems
Engineering Foundation requires no new platform capability whatsoever
to integrate cleanly — every integration point it needs already exists,
proven, and unmodified.

## Related Documents

`WP7.0C Cross-Framework Dependency Report.md` (the precedent this
document's own structure follows); `ADR-0023`; `WP7.2B Requirements
Platform Architecture.md` §4; `WP7.2B Systems Engineering
Architecture.md`; `WP7.1F Engineering Core Architecture Conformance
Report.md`.

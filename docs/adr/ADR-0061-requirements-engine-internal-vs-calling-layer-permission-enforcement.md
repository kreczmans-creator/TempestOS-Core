# ADR-0061: Requirements Engine — Internal vs. Calling-Layer Permission Enforcement

## Status

Accepted — `WP 7.3A` (Requirements Engine), 2026-07-30.

## Context

`WP7.2C Required ADR Catalogue.md` reserved this question: `Tempest.Core.
Verification.IVerificationService.GetVerificationHistoryAsync` gates its
own read internally (`RequirePermission`, mirroring `IAuditQuery`'s own
precedent), while `IReportingService`/`IMaterialCatalog`/
`ICalculationEngine` each leave every permission check to the calling
layer entirely. No existing rule decided which pattern a new service
should default to.

## Decision

**`IRequirementsService` performs no internal permission gating of its
own, for any method.** Every method is calling-layer-enforced only,
mirroring `IMaterialCatalog`/`ICalculationEngine`'s own majority
precedent — confirmed directly in the shipped implementation:
`RequirementsService`'s own constructor does not depend on
`IPermissionEvaluator` at all.

**The deciding test, stated explicitly for future reference:** gate
internally when the data exposed is itself evidentiary and permission-
sensitive on its own terms (`IVerificationService.
GetVerificationHistoryAsync`'s own audit-adjacent verification history);
leave to the calling layer when the data is ordinary operational
engineering content the calling layer's own context already governs (a
requirement's own statement, category, status, and relationships).
Requirement data falls in the second category — it is the artefact a
systems engineering practice works with directly, not a sensitive record
of what someone else did.

**`GetEvidenceAsync` still ends up permission-gated in practice —
transitively, not by this service's own design.** Its own call to
`IVerificationService.GetVerificationHistoryAsync` remains gated
unchanged; a caller lacking `VerificationService.ReadPermission`
receives `PermissionDeniedException` from that inherited call, proven
directly by `GetEvidenceAsync_InheritsVerificationReadPermissionGate`.

## Consequences

**Positive:**

- `RequirementsService`'s own constructor is simpler — one fewer
  dependency (`IPermissionEvaluator`) than it would otherwise carry,
  consistent with Materials' and Calculations' own identical shape.
- The deciding test (evidentiary/sensitive vs. ordinary operational
  content) is now stated explicitly, available to the next Work Package
  facing the identical question, rather than re-derived from scratch.
- No duplicate permission-enforcement point exists for verification
  history specifically — `GetEvidenceAsync` inherits the one gate that
  already exists, rather than adding a second, parallel one.

**Negative:**

- A caller wanting to restrict read access to requirement data itself
  (not merely its verification evidence) must implement that
  enforcement entirely at the calling layer — `RequirementsSampleModule`'s
  own `GetSampleRequirementEvidenceCommandHandler` demonstrates this
  explicitly, mirroring `ExportSampleDataCommandHandler`'s own identical
  convention.

## Alternatives Considered

**Gating every `IRequirementsService` read method internally**,
mirroring `GetVerificationHistoryAsync` — considered and rejected.
Requirement data does not share Verification's own evidentiary-history
character; gating it internally would be inconsistent with Materials'
and Calculations' own identical, already-shipped precedent for
comparable operational data.

**Gating only `GetRelationshipsAsync` and `GetEvidenceAsync`
internally**, as a middle option — considered and rejected. Both
concepts remain part of a requirement's own ordinary operational content
from this service's own perspective; `GetEvidenceAsync`'s own
verification-history component is already protected transitively,
without this service needing to duplicate that protection itself.

## Related Documents

`ADR-0044` (the platform-wide authorization enforcement point);
`ADR-0057` (Verification's own internal-gating precedent for
`GetVerificationHistoryAsync`); `ADR-0055` (Materials' own AT-15,
no-internal-gating precedent); `WP7.2C Platform Integration Matrix.md`;
`WP7.2C Security Review.md`; `WP7.2C Required ADR Catalogue.md`;
`docs/releases/v0.7.0/WP7.3A Security Review Report.md`.

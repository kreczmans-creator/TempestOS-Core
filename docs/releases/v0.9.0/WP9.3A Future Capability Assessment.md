# WP 9.3A — Verification Management Workspace — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build.

## `FCR-0057` — `VerificationService.RecordAsync` Additionally Linking Through `IHasRelationships` When the Subject Is a Real Domain Object

`TD-32` (this Work Package's own Technical Debt Assessment) discloses
that `VerificationService.RecordAsync` links its own subject to the new
record via the raw document store only, never visible to
`EngineeringDomainContext.RelationshipRepository`. A future
implementation could have `RecordAsync` detect whether
`subjectDocumentId` resolves to an `EngineeringObjectBase`-derived
object and, if so, additionally call its own `.LinkAsync()` — a
`Tempest.Core.Verification` change, deliberately out of this Work
Package's own "reuse, do not redesign execution" scope. **Recommended
once a real Workspace-layer consumer demonstrates a genuine need to
query `RelationshipRepository` directly for Verification result links**
— `VerificationRecordReader`'s own existing raw-store read already
serves every scope item this Work Package's own controlling instruction
names; no such consumer exists yet.

## `FCR-0058` — Concrete `IApprovalGate`/`IApproval`/`IReview` Implementation, Extended to Verification

`TD-30` (`WP 9.2A`, confirmed still open) discloses that no governed
Approval/Review workflow exists anywhere in the platform. This Work
Package's own "Verification Reviews"/"Verification Approval State" scope
items are satisfied by `LifecycleState` alone (`ADR-0090`), identically
to Calculation Management's own already-disclosed treatment. A real
implementation would give Verification (and every other discipline
naming "Review"/"Approval" in its own scope) a genuine, queryable
governance record — who reviewed what, when, against which recorded
result. **Recommended once a real, demonstrated need for auditable
review/approval provenance exists** — extends `FCR-0052` (`WP 9.2A`)
directly rather than duplicating it as a separate candidate.

## `FCR-0059` — A Dedicated `Witness` Field on `VerificationEvidenceEntry`

This Work Package's own scope names "Witness information" as a distinct
Engineering Behaviour item; `VerificationEvidenceEntry`
(`Description`/`Reference` only, `WP 7.1E`) has no dedicated field for
it — represented today as ordinary evidence text. A future capability
could extend `VerificationEvidenceEntry` with a genuine `WitnessedBy`
field (a `Tempest.Core.Verification` change, out of this Work Package's
own "do not redesign verification execution" scope). **Recommended once
a real consumer demonstrates that witness identity needs to be
queryable/reportable independently of free-text evidence** — today's
descriptive-text representation already satisfies every scope item this
Work Package's own controlling instruction names.

## Not Recommended: A `CalculationTemplateRegistry`-Equivalent `VerificationMethodRegistry`

Considered directly during implementation and rejected as this Work
Package's own delivered design (`ADR-0089`), not merely as a future
candidate — see that ADR's own Alternatives Considered section.
**Not recommended** unless `IVerificationService` itself grows a
generic, per-Method dispatch shape in the future — today it has none.

## Not Recommended: A Dedicated `VerificationPlan` Domain Kind

Considered directly and rejected as this Work Package's own delivered
design (`ADR-0090`) — see that ADR's own Alternatives Considered
section. **Not recommended** unless a future Work Package identifies a
genuine, demonstrated need for a Plan to carry its own structured fields
(a schedule, a resourcing assignment) beyond what `VerificationActivity`
already provides via `LifecycleState.Draft`.

## Verdict

Three new candidates recorded (`FCR-0057`–`FCR-0059`); none built
speculatively ahead of genuine need; two further candidates considered
and explicitly not recommended, with reasoning recorded rather than
silently dropped. `FCR-0058` extends `WP 9.2A`'s own `FCR-0052` directly,
disclosed as an extension rather than duplicated as a new, separate
candidate for the identical underlying capability.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0089`; `ADR-0090`;
`WP9.2A Future Capability Assessment.md` (`FCR-0052`); `WP9.4A Future
Capability Assessment.md` (`FCR-0054`–`FCR-0056`); `WP9.3A Technical
Debt Assessment.md` (`TD-30`, `TD-32`); `WP9.3A Engineering Review
Report.md`.

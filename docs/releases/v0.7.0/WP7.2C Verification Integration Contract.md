# WP 7.2C — Verification Integration Contract

## Status

Contract review only. No implementation.

## Purpose

Reviews the Requirements Platform's own interaction with
`Tempest.Core.Verification`, confirming ownership, responsibility,
dependency direction, and the explicit absence of duplicated behaviour
— per this Work Package's own controlling instruction.

## Ownership

**`Tempest.Core.Verification` owns everything about what "verified"
means: the outcome vocabulary (`VerificationOutcome`), the evidence
model (`VerificationContext`, `VerificationEvidenceEntry`), the
verification record itself (`IVerificationRecord`), and the permission
gate on reading verification history.** `Tempest.Core.Requirements`
owns nothing verification-related — it is purely a caller of
`IVerificationService`, in exactly the same relationship every future
Verification consumer is expected to have (`WP7.0C Engineering
Foundation Contracts.md` §5's own "Expected Future Consumers" naming
`FCR-0027` directly).

## Responsibility

**Recording.** A caller (a future module, a future command handler)
invokes `IVerificationService.RecordAsync(requirement.Id, outcome,
method, context)` directly — `Tempest.Core.Requirements` provides no
wrapper method, no convenience overload, and no intermediate type
between a caller and `IVerificationService` itself. The requirement's
own Id is passed as a bare `Guid subjectDocumentId`, exactly as
`IVerificationService`'s own real, existing signature already expects
— confirmed directly against the shipped contract (`Tempest.Core.
Verification.IVerificationService`, `WP 7.1E`), not a hypothetical
future signature.

**Reading.** `IVerificationService.GetVerificationHistoryAsync
(requirement.Id)` is called directly wherever verification history is
needed — including inside `IRequirementEvidence`'s own aggregation
(`WP7.2C Requirements Platform Contracts.md` §7). The existing
permission gate (`Tempest.Core.Verification.ReadPermission`) applies
unmodified; `Tempest.Core.Requirements` does not introduce a second,
parallel permission for the same read.

## Dependency Direction

**`Tempest.Core.Requirements` depends on `Tempest.Core.Verification`.
The reverse is false and must remain false.** This is the identical
structural decision `ADR-0057` and `WP7.0C Cross-Framework Dependency
Report.md` already made deliberately: `Tempest.Core.Verification`
depends only on `Tempest.Core.EngineeringData`'s generic document
concept, never on a concrete Requirements type. This contract review
re-confirms that decision remains unmodified — nothing proposed
anywhere in `WP7.2C Requirements Platform Contracts.md` adds a
Requirements-specific type, method, or dependency anywhere inside
`Tempest.Core.Verification`. Had this contract review instead proposed
`IVerificationService` gain a `RecordRequirementVerificationAsync`
overload, or `IVerificationRecord` gain a `Requirement`-typed property,
it would have reintroduced exactly the circular-dependency risk
`ADR-0057` was written to avoid — this review confirms no such proposal
exists anywhere in this Work Package's own deliverables.

## Confirmed: No Duplicated Behaviour

| Verification Concern | Owned By | Requirements Platform's Own Behaviour |
|---|---|---|
| Outcome vocabulary (Pass/Fail/Conditional) | `Tempest.Core.Verification` | None — reused directly, no parallel enum |
| Evidence recording (criteria, evidence entries) | `Tempest.Core.Verification` | None — `VerificationContext` used directly, unmodified |
| Verification record identity and history | `Tempest.Core.Verification` | None — `IVerificationRecord`/`GetVerificationHistoryAsync` used directly |
| Permission gating on verification reads | `Tempest.Core.Verification` | None — no second permission introduced |
| Linking a verification to its subject | `Tempest.Core.Verification` (`RecordAsync`'s own internal `verifiedBy` link) | None — `Tempest.Core.Requirements` never creates this link itself |
| **Requirement's own lifecycle status** (`RequirementStatus.Verified`) | `Tempest.Core.Requirements` | **This is Requirements' own concern, and deliberately not automated from a `VerificationRecord`'s own `Outcome`** (`WP7.2C Requirement Lifecycle Model.md`) — the one place a genuine, intentional separation exists between "verification happened" and "the requirement's own workflow reflects it" |

**Zero verification behaviour is duplicated.** The one place this
review found real, deliberate divergence — `RequirementStatus.Verified`
not being automatically derived from a recorded `VerificationOutcome` —
is not a duplication of Verification's own concern; it is a
confirmation that Requirements' own workflow-state concept and
Verification's own evidentiary-fact concept remain genuinely distinct
concerns, exactly as `Tempest.Core.Verification`'s own Principle 25
("Verification evidence is explicit") and this Platform's own Principle-
equivalent distinction (Status is judgement; Outcome is evidence,
`WP7.2B Requirements Domain Model.md` §9) both require.

## What a Future Implementation Must Not Do

- Must not add any Requirements-aware method, type, or property to
  `Tempest.Core.Verification`.
- Must not automatically transition a requirement's own `Status` to
  `Verified` inside the same call that records a `VerificationRecord` —
  the two remain two separate, caller-driven actions.
- Must not introduce a second evidence, criteria, or outcome
  vocabulary anywhere in `Tempest.Core.Requirements`.

## Related Documents

`Tempest.Core.Verification` (`IVerificationService`, unmodified);
`ADR-0057`; `WP7.0C Cross-Framework Dependency Report.md`; `WP7.1E
Future Capability Recommendations.md` Recommendation 1; `WP7.2C
Requirements Platform Contracts.md` §1, §6, §7; `WP7.2C Requirement
Lifecycle Model.md`.

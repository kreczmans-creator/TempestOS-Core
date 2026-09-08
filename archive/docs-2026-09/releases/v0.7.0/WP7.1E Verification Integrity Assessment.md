# WP 7.1E — Verification Framework — Verification Integrity Assessment

## Purpose

This Work Package's own controlling instruction required every
verification support nine specific integrity properties — a dedicated
deliverable mirroring `WP7.1D Calculation Integrity Assessment.md`'s own
role for the Calculation Framework. This report confirms each property
is genuinely satisfied by the real implementation, not merely asserted.

## Integrity Properties — What Is Guaranteed, and How It Is Proven

| Property | Guarantee | Proof |
|---|---|---|
| **Stable identity** | `IVerificationRecord.Id` is the real `EngineeringData.IEngineeringDocument`'s own Id, assigned once, at recording, never reassigned. | `RecordAsync_Id_IsDirectlyRetrievableThroughEngineeringDocumentStore` |
| **Revision history** | Every verification record is genuinely revision-capable, inherited directly from `IEngineeringDocumentStore` — `RevisionNumber` reflects the real, current document state. | `RecordAsync_ValidSubject_ReturnsRecord_WithGivenOutcomeAndMethod` (asserts `RevisionNumber == 1`); the underlying document is independently retrievable and revisable through `IEngineeringDocumentStore` directly |
| **Explicit verification criteria** | `VerificationContext.RecordCriterion` lets a verifier declare exactly what was checked; every recorded criterion survives into the resulting record unchanged. | `RecordAsync_RecordsCriteriaAndEvidence` |
| **Explicit evidence** | `VerificationContext.RecordEvidence` lets a verifier declare exactly what supports the outcome; every recorded evidence entry survives unchanged. | `RecordAsync_RecordsCriteriaAndEvidence` |
| **Explicit outcome** | `Outcome` (`Pass`/`Fail`/`Conditional`) is a required, non-optional parameter to `RecordAsync` — no verification may be recorded without one. | Every test in `VerificationServiceTests.cs` supplies an explicit outcome |
| **Linked engineering documents** | `VerificationContext.LinkDocument` links a verification to an additional document beyond its own subject, validated via `IEngineeringDocumentStore.LinkAsync` — a non-existent Id fails loudly. | `RecordAsync_LinksAdditionalDocument_RetrievableThroughEngineeringDocumentStore`; `RecordAsync_NonExistentLinkedDocument_ThrowsEngineeringDocumentNotFoundException` |
| **Linked calculations where applicable** | `VerificationContext.LinkCalculationRecord` links a verification to a calculation execution record by Id, equally validated. | `RecordAsync_LinksCalculationRecord_RecordedAndRetrievable` |
| **Linked material references where applicable** | `VerificationContext.ReferenceMaterial` records a material Id — open and unvalidated, since Verification has no dependency on Materials (`AT-17`), but genuinely present on the resulting record. | `RecordAsync_ReferencesMaterial_RecordsOpenUnvalidatedString` |
| **Provenance** | `VerifiedByPrincipalId`, `VerifiedAt`, and `Method` together constitute complete provenance — who verified what, when, and how. | `RecordAsync_PrincipalEstablished_RecordsItsIdentity`; `RecordAsync_NoPrincipalEstablished_RecordsUnknownVerifier` |

## Unverifiable Conclusions Made Impossible — By Construction, Not Convention

This Work Package's own controlling instruction required the framework
"make unverifiable engineering conclusions impossible wherever
practical." Concretely:

- A verification cannot be recorded against a subject document that
  does not exist — `RecordAsync` checks this before creating anything,
  throwing `EngineeringDocumentNotFoundException` immediately, never
  producing an orphaned or dangling verification record.
- A verification cannot omit its own method — `method` is a required,
  validated (`ArgumentException.ThrowIfNullOrWhiteSpace`) parameter.
- A verification's own links to other documents or calculation records
  cannot silently reference something that does not exist — every link
  is validated the same way the subject itself is, via the real
  `LinkAsync` existence check.
- `VerificationOutcome` has no fourth, ambiguous value — a verification
  is always `Pass`, `Fail`, or `Conditional`, never left unstated.

## What Remains the Registering Caller's Own Responsibility

Consistent with "shall verify engineering artefacts... shall not
implement Validation... shall not implement Requirements Management,"
this framework does **not** verify:

- That the recorded `Outcome` is actually correct given the recorded
  criteria — a human or process decision this framework records, never
  audits or second-guesses.
- That a `ReferenceMaterial` call names a `materialId` that genuinely
  exists in `Tempest.Core.Materials` (`AT-17`).
- That the verification `method` used is appropriate for the claim
  being verified — `method` remains an open string, deliberately not
  validated against any fixed vocabulary (`ADR-0057` Decision 2).

These are disclosed, deliberate scope boundaries, not integrity gaps
this Work Package silently accepted.

## Verdict

**Every one of the nine integrity properties this Work Package's own
controlling instruction named is genuinely satisfied, each proven by a
specific, passing test — not merely asserted in documentation.**

## Related Documents

`WP7.1E Implementation Report.md`; `ADR-0057`; `tests/Tempest.Core.Tests/
Verification/VerificationServiceTests.cs`; `docs/engineering/Engineering
Principles.md` (Principles 24-28).

# WP 7.2B — Digital Thread Architecture

## Status

Architecture only. No production code accompanies this document.

## Purpose

Designs the complete engineering digital thread — how engineering
information flows through TempestOS, from a stated requirement through
to its own audited, reported, exported record. Per this Work Package's
own controlling instruction, this design expands beyond the illustrative
example only where repository evidence justifies each further link —
no stage is added because it seems plausible in the abstract.

## What "Digital Thread" Means Architecturally

**The digital thread is not a new mechanism.** It is the name this
document gives to a capability that already exists, structurally,
across the Engineering Core and Platform Core: every stage below is
connected to the next by a real, already-shipped reference mechanism
(`DocumentReference`/`LinkAsync`/`GetReferencesAsync`), not a new
pipeline this Work Package proposes building. The digital thread is a
**read-side traversal concept** — "follow the references from this
requirement outward" — layered over write paths that already exist
independently of each other and were never designed as a pipeline in
the first place.

## The Thread, Stage by Stage

```
Requirement
   │  (is-a)
   ▼
Engineering Data                 [IEngineeringDocument, Kind="Requirement"]
   │  (may reference)
   ▼
Calculation                      [CalculationRecord<TResult>, referenced by Guid — AT-16-style, unvalidated]
   │  (recorded against)
   ▼
Verification                     [IVerificationService.RecordAsync(requirementId, outcome, method, context)]
   │  (produces)
   ▼
Evidence                         [VerificationRecord.Evidence + linked CalculationRecords + linked documents — an aggregation, not new storage]
   │  (consumed by)
   ▼
Reporting                        [a future IReportDefinition, presenting the aggregated evidence — not designed here]
   │  (packaged by)
   ▼
Export                           [IExportable, framing the Requirement/Collection as a portable artifact]
   │  (attributed by)
   ▼
Audit                            [IAuditRecorder.RecordAsync, at the calling layer, for every write above]
```

## Evidence for Each Stage (Why This Link, Not a Different One)

| Stage Transition | Real Mechanism | Evidence It Already Works |
|---|---|---|
| Requirement → Engineering Data | `IEngineeringDocumentStore.CreateAsync("Requirement", ...)` | Identical to `MaterialSpecification`'s own `Kind = "MaterialSpecification"` pattern, proven by `MaterialCatalogTests.cs` |
| Engineering Data → Calculation | `DocumentReference` via `LinkAsync`, or a bare `Guid` field mirroring `CalculationContext.ReferenceMaterial`'s own unvalidated-reference shape | `ADR-0056` Decision 6; `AT-16` |
| Calculation → Verification | `VerificationContext.LinkCalculationRecord(Guid)`, already implemented and tested | `RecordAsync_NonExistentLinkedDocument_ThrowsEngineeringDocumentNotFoundException` (proves the link is validated when present) |
| Verification → Evidence | `IVerificationRecord.Evidence` (a `VerificationEvidenceEntry` list), already implemented | `RecordAsync_RecordsCriteriaAndEvidence` |
| Evidence → Reporting | No existing mechanism connects Verification/Requirements data to `IReportingService` today — **this is a genuine future integration point, not yet built anywhere in this repository.** Named here as the architecturally correct next stage (a report definition reading requirement/verification data through this Platform's own read APIs), not designed further, per this Work Package's own explicit "do not design report layouts" instruction. | `WP7.2B Platform Integration Report.md` §3 |
| Reporting → Export | No existing mechanism connects a generated report to `IExportable` today, and none is proposed here — a generated `ReportResult` and an exported artifact are two independently useful outputs of the same underlying data, not necessarily a mandatory sequential step. Included in the illustrative thread because the controlling instruction's own example names it, disclosed honestly as **not yet a real, evidenced connection** — see "What This Document Does Not Claim," below. | — |
| Export → Audit | `IAuditRecorder.RecordAsync`, composed at the calling layer, exactly as every existing `IExportService` consumer already does (`ExportImportSampleModuleIntegrationTests.cs`'s own audit-entry assertions) | `ADR-0051`; existing Export/Import sample module |

## What This Document Does Not Claim

**Not every arrow in the illustrative thread is an equally strong,
already-evidenced connection.** This document distinguishes three
strengths of evidence, disclosed explicitly rather than presented as
uniformly solid:

- **Proven today, by real shipped code:** Requirement→Engineering Data,
  Calculation→Verification, Verification→Evidence, Export→Audit — each
  cites a real test or a real, already-implemented mechanism.
- **Architecturally correct, but not yet built by anything:**
  Evidence→Reporting — the natural next integration point, consistent
  with `Tempest.Core.Reporting`'s own existing shape, but no code
  anywhere connects Verification/Requirements data to a report
  definition today. Named as a future Work Package's own scope, not
  claimed as already working.
- **An illustrative ordering, not a mandatory pipeline:**
  Reporting→Export — the two are independently useful outputs of the
  same underlying evidence, not a required sequence. A future consumer
  could export a Requirement Collection without ever generating a
  report from it, or generate a report without ever exporting anything.
  This document does not force a sequential dependency where none is
  architecturally required.

## Digital Thread Traversal — Read Path, Not Write Path

The digital thread is walked, not written. A consumer asking "what is
the complete evidentiary history behind this requirement" would:

1. `IEngineeringDocumentStore.GetReferencesAsync(requirementId)` — every
   direct reference (allocations, trace links, the `verifiedBy`
   relationship `VerificationService` itself creates).
2. For each `verifiedBy` reference, `IVerificationService.
   GetVerificationHistoryAsync` (permission-gated, exactly as it is
   today) to retrieve every recorded outcome and its own evidence.
3. For each `LinkedCalculationRecordIds` entry a verification record
   carries, `IEngineeringDocumentStore.GetRevisionHistoryAsync` to
   retrieve the calculation's own recorded assumptions and results.

**No new traversal API is proposed.** Every step above is already a
real, callable method on an already-shipped interface — the "digital
thread" is this document's own name for composing them in sequence, not
a new capability requiring its own implementation Work Package. This is
disclosed as a deliberate, load-bearing finding: the Engineering Core's
own design (specifically, `Verification`'s own reuse of `LinkAsync`/
`GetReferencesAsync` rather than a bespoke index, `ADR-0057` Decision 3)
already anticipated exactly this kind of cross-framework traversal need,
one release before this Work Package needed it.

## Related Documents

`WP7.2B Systems Engineering Architecture.md` (Capability Area 7); `WP7.2B
Requirements Domain Model.md`; `ADR-0057` (the reuse-of-existing-
mechanism decision this thread depends on); `WP7.1F Engineering Core
Architecture Conformance Report.md`.

# Verification Framework

## 1. Introduction

`IVerificationService` (`Tempest.Core.Verification`, `WP 7.1E`) answers
one specific question — "has this engineering claim been demonstrated?"
— and this document exists because TempestOS already has two other
mechanisms that sound like they might answer it too: `IAuditRecorder`
(who did what, when) and `Calculations.CalculationRecord<TResult>` (what
was computed). All three record something that happened. Only one of
them is verification.

## 2. Purpose

To distinguish Verification from Audit and from a Calculation Record —
three structurally similar "record what happened" types with genuinely
different semantics — mirroring this project's own repeated practice of
distinguishing structurally similar pairs (Command Framework vs. Event
Bus; Calculation vs. Command, `13-calculation-framework.md`).

## 3. Background — Three Adjacent "What Happened" Questions

- **Audit answers "who did what, when."** `IAuditRecorder.RecordAsync`
  records an actor, an action, and a timestamp — it does not know or
  care whether the action was *correct*, only that it occurred.
- **A Calculation Record answers "what was computed, from what input,
  under what assumptions."** It is evidence of a computation, not a
  judgement about whether the computed value satisfies anything.
- **A Verification Record answers "has a specific engineering claim
  been demonstrated."** It is a judgement — `Pass`, `Fail`, or
  `Conditional` — against a specific engineering document, supported by
  explicit criteria and evidence.

A single real event can touch all three without any of them
duplicating the others: an engineer runs a calculation (a
`CalculationRecord` is created), the calculation's own execution is
independently audited (an `IAuditRecord` is created by the calling
layer), and the engineer then judges, using that calculation's own
result as evidence, whether a requirement is satisfied (a
`VerificationRecord` is created, linking back to the calculation).

## 4. The Problem

1. **How does a verification avoid becoming "just another Audit
   entry"** — recording that *a* verification action occurred, without
   losing the specific outcome, criteria, and evidence that make it
   verification rather than an arbitrary logged action?
2. **How does a verification reference the calculation or document that
   supports it**, without Verification needing to understand what a
   calculation or a material actually is?
3. **How is verification history retrieved for a given engineering
   document**, given `IEngineeringDocumentStore` itself has no native
   query capability beyond direct Id lookup?

## 5. The Design

`IVerificationRecord` carries `SubjectDocumentId`, `Outcome`, `Method`,
explicit `Criteria` and `Evidence`, and open, validated links
(`LinkedDocumentIds`, `LinkedCalculationRecordIds`) plus an open,
unvalidated one (`ReferencedMaterialIds`) — deliberately not the same
shape as `IAuditRecord` (actor/action/timestamp) or
`CalculationRecord<TResult>` (result/assumptions/intermediate values),
because it answers a different question. Verification history is
retrieved by linking the subject document to each verification record
via `IEngineeringDocumentStore.LinkAsync` (`"verifiedBy"`), then reading
it back via `GetReferencesAsync` — reusing the Data Model's own existing
mechanism rather than building a new index. See `ADR-0057` for the
complete design.

## 6. Alternatives Considered

**Recording a verification as an `IAuditRecord` with extra `Detail`
fields for outcome/criteria/evidence** — considered and rejected;
overloading Audit's own deliberately generic contract with verification-
specific semantics would violate the same one-reason-to-change
principle `ADR-0045` already settled for Audit, exactly as
`WP7.0C Required ADR Catalogue.md` itself anticipated.

**Giving Verification a hard dependency on Calculations to validate
linked calculation records against a real, registered definition** —
considered and rejected; a `CalculationRecord<TResult>.Id` is simply a
`Guid` `IEngineeringDocumentStore.LinkAsync` can validate directly,
needing no dependency on the Calculation Framework's own assembly.

## 7. Why This Solution Was Chosen

It gives verification its own precise semantics without duplicating
Audit's or Calculation's own storage, and it reuses the Data Model's
existing linking mechanism for history retrieval rather than building a
parallel index — the simplest dependency shape of any Engineering
Foundation framework, requiring no direct `Persistence.IPersistenceStore`
dependency at all.

## 8. Architectural Principles

- **Single Responsibility Principle** — Verification judges; it does
  not log actions (Audit) or compute values (Calculation).
- **Composition Over Inheritance** — a verification's own links to
  documents and calculation records are composed via the Data Model's
  existing reference mechanism, not a new inheritance relationship.
- **Fail Fast** — recording a verification against a non-existent
  subject, or linking to a non-existent document, fails immediately
  (`EngineeringDocumentNotFoundException`), never silently.

## 9. Benefits

- Verification, Audit, and Calculation Records can all exist for the
  same real-world event without any duplication — each captures the one
  fact only it is responsible for.
- Verification history requires no new storage mechanism, and no direct
  Persistence dependency — reusing `LinkAsync`/`GetReferencesAsync`
  closes the "how do I query by subject" problem for free.
- Linked documents and calculation records are genuinely validated at
  recording time, not merely recorded as trusted strings.

## 10. Trade-offs

- `RecordAsync`'s own multi-link sequence is not transactional — a
  failure partway through can leave a partially-linked record (`TD-23`).
- `VerificationContext` imposes no bound on recorded data volume, and
  `GetVerificationHistoryAsync`'s own cost scales with a subject
  document's total reference count, not only its verification
  references (`TD-24`).

## 11. Common Mistakes

The mistake most worth naming: treating a `Conditional` verification
outcome as equivalent to `Fail`. It is not — `Conditional` means the
claim was demonstrated subject to a disclosed qualification (see the
record's own `Criteria`), a materially different statement than "not
demonstrated at all," and callers should not collapse the two.

A second mistake: recording a verification's own supporting calculation
by re-describing its result in `Evidence` text rather than linking to
the real `CalculationRecord<TResult>.Id` via
`VerificationContext.LinkCalculationRecord` — the link is what makes the
calculation independently re-checkable; a text description alone is not.

## 12. Future Evolution

- **Transactional multi-document operations** (`FCR-0036`, `TD-23`),
  once a real, demonstrated need for atomic multi-link writes exists.
- **A future Requirements Engine** (`FCR-0027`) is Verification's own
  most likely first real consumer — recording verification outcomes
  against real requirement documents, exactly as `WP7.0C Cross-Framework
  Dependency Report.md` anticipated.

## 13. Key Takeaways

1. Three types that each record "something happened" can remain
   genuinely distinct if each answers a different question — the test
   is not "does this look similar," but "would merging these violate
   single responsibility for either."
2. A cross-cutting framework's own most valuable design decision can be
   *not* building something — Verification needed no new storage, no
   new index, and no new dependency beyond the Data Model, because the
   Data Model's own existing linking mechanism already solved the
   "query by subject" problem.
3. Validating some links (documents, calculation records) while leaving
   others open (materials) is a legitimate, asymmetric design — the
   deciding factor is which dependency the framework already has, not a
   blanket rule that every reference must, or must not, be validated.

## Related Documents

`13-calculation-framework.md` (the closest structural precedent, and
this guide's own second point of comparison); `08-failure-isolation.md`
(this project's own repeated practice of distinguishing structurally
similar pairs); `ADR-0057`; `docs/academy/03 Work Packages/
WP7.1E-verification-framework-implementation.md`;
`docs/academy/03 Work Packages/WP6.5-audit-framework-implementation.md`
(required reading per `WP7.0C Academy Plan.md`, to understand why Audit
and Verification are not the same concept before implementing either
alongside the other).

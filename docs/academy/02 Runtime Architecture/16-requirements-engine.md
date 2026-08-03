# Requirements Engine

## 1. Introduction

`IRequirementsService` (`Tempest.Core.Requirements`, `WP 7.3A`) is the
first Systems Engineering Foundation capability with a working
implementation, not only architecture and contract documentation
(`WP7.2B`, `WP7.2C`). This document exists because Requirements sits at
the intersection of three ideas this project has already carefully
separated — an Engineering Document (`15-engineering-data-model.md`), a
Verification judgement (`14-verification-framework.md`), and a
relationship (`DocumentReference`) — and understanding the Requirements
Engine means understanding that it introduces exactly zero new
mechanisms of its own.

## 2. Purpose

To explain the three-layer pattern the Requirements Engine is built
from (Requirement-as-Document, Relationship-as-Reference,
Status-independent-of-Verification), and to give the relationship-kind
and traceability vocabulary (`RequirementRelationshipKinds`) a single,
canonical reference point — mirroring `WP7.2C Academy Plan.md`'s own
recommendation for exactly these two sections.

## 3. Background — What a Requirement Actually Is, Structurally

A `Requirement` is not a new kind of storage — it is an
`IEngineeringDocument` whose `Kind` is `"Requirement"`, exactly the same
pattern `MaterialCatalog` and `ICalculationEngine` already established
for their own document kinds. Its `RequirementCollection` and
`RequirementGroup` siblings are the same pattern again, each simply a
different `Kind` string. This means every capability
`IEngineeringDocumentStore` already provides — immutable revision
history, provenance, `LinkAsync`/`GetReferencesAsync` — is inherited
for free, not reimplemented.

## 4. The Problem

1. **How does a requirement's own lifecycle status stay independent of
   whether it has actually been verified**, given both concepts sound
   related and a careless design could conflate them?
2. **How is a requirement's relationship to a group, a collection, an
   allocation target, or another requirement represented**, without
   inventing a new storage shape for each?
3. **How is a requirement's "proof" (its verification history plus its
   linked evidence) retrieved as a single, composed view**, without
   building a new traversal mechanism?

## 5. The Design

**Status and Verification Outcome are two independent, caller-driven
mechanisms with zero code path connecting them.** `RequirementStatus`
(`Draft`, `Reviewed`, `Approved`, `Allocated`, `Verified`, `Satisfied`,
`Obsolete`) is a closed enum, changed only by an explicit
`SetStatusAsync` call checked against `RequirementStatusTransitions`'s
own permitted-transition table. `VerificationOutcome` (`Pass`, `Fail`,
`Conditional`) belongs entirely to `Tempest.Core.Verification` and is
recorded independently via `IVerificationService.RecordAsync`. Nothing
in `RequirementsService` reads a `VerificationOutcome` to derive a
`RequirementStatus`, or vice versa — a caller who wants a requirement
marked `Verified` must call `SetStatusAsync` explicitly, informed by
whatever verification evidence it chooses to consult first.

**Every relationship is a `DocumentReference`, not a new concept.** A
requirement's parent group (`GroupedUnder`), its collection membership
(`CollectedIn`), its dependencies (`DependsOn`), its lineage
(`DerivesFrom`), its allocation target (`AllocatedTo`), and its
traceability links (`References`, `Satisfies`) are all recorded via the
identical `LinkAsync` call, distinguished only by which
`RequirementRelationshipKinds` constant is passed. `RequirementCollection`
and `RequirementGroup` store no membership/parent field of their own at
all — both are derived entirely by calling `GetReferencesAsync` and
filtering by relationship kind, exactly mirroring how a Verification
Record's own history is derived, not stored redundantly.

**`GetEvidenceAsync` is the digital thread, demonstrated.** It composes
`IVerificationService.GetVerificationHistoryAsync` with
`IEngineeringDocumentStore.GetReferencesAsync` into one read — the first
place in the codebase that actually builds the composed,
multi-source view `WP7.2B Digital Thread Architecture.md` argued would
require no new mechanism. See `ADR-0058` for the complete
classification decision.

## 6. Alternatives Considered

**Deriving `RequirementStatus.Verified` automatically whenever a
matching `VerificationRecord` with `Outcome = Pass` exists** —
considered and rejected. This would silently couple two concepts this
project has deliberately kept separate everywhere else, and would strip
the calling layer of its own judgement about when a requirement is
actually ready to be marked verified (a `Pass` outcome against one
criterion does not necessarily mean every aspect of the requirement is
satisfied).

**Storing collection membership and group parentage as fields on
`RequirementCollectionDto`/`RequirementGroupDto` directly**, rather than
deriving them via `GetReferencesAsync` — considered and rejected;
this would duplicate a mechanism the Data Model already provides,
exactly the anti-pattern every other Engineering Core framework has
consistently avoided.

## 7. Why This Solution Was Chosen

It is the fourth consecutive Engineering Core framework (after
Materials, Calculations, Verification) to introduce zero new storage
mechanism of its own, and it is the first to demonstrate that
relationships alone — with no additional index, no additional service,
no additional query capability — are sufficient to build a hierarchy, a
membership model, and a composed evidence view all at once.

## 8. Architectural Principles

- **Single Responsibility Principle** — a Requirement states an
  engineering need; it does not judge whether that need has been met
  (Verification's job) or log who changed it (Audit's job).
- **Composition Over Inheritance** — every requirement relationship is
  composed from `DocumentReference`, never a new relationship type.
- **Fail Fast** — an invalid status transition
  (`InvalidRequirementStatusTransitionException`), a duplicate
  identifier (`DuplicateRequirementIdentifierException`), or a link to a
  non-existent requirement or target fails immediately, never silently.

## 9. Benefits

- Requirements, Collections, Groups, and their relationships all reuse
  the Engineering Data Model's own existing mechanisms — zero new
  storage or traversal infrastructure anywhere in
  `Tempest.Core.Requirements`.
- The Status/Verification-Outcome separation is enforced structurally
  (no code path connects them), not merely by convention or comment.
- `GetEvidenceAsync` gives every future engineering discipline module a
  single, ready-made "what proves this requirement" view for free.

## 10. Trade-offs

- No compare-and-swap or expected-prior-revision check exists on
  `ReviseAsync`/`SetStatusAsync` — two concurrent editors can silently
  overwrite one another's intent (`TD-25`, `ADR-0060`).
- Allocation targets are Guid-only (an existing `IEngineeringDocument`);
  an open-string target for a not-yet-created design element, described
  in `WP7.2B`'s own broader architectural vision, was never carried into
  the approved contract (see `WP7.3A Future Capability
  Recommendations.md`).

## 11. Common Mistakes

The mistake most worth naming: assuming `SetStatusAsync(id,
RequirementStatus.Verified)` should be called automatically whenever a
verification record with a `Pass` outcome is recorded. It should not —
the two mechanisms are deliberately independent, and collapsing them
defeats the entire design point of `Principle 29` (`Engineering
Principles.md`).

A second mistake: treating `RequirementCollection`/`RequirementGroup` as
if they carry their own membership/parent data directly. They do not —
always call `GetRelationshipsAsync` (or the collection/group-specific
lookup methods) rather than assuming a field exists to read.

## 12. Future Evolution

- **String-based allocation targets**, closing the disclosed gap between
  `WP7.2B`'s own broader vision and the shipped, Guid-only contract
  (`WP7.3A Future Capability Recommendations.md`).
- **Requirement baselining and change impact analysis**, both plausible
  once a non-trivial requirement set with real relationship depth
  exists (`WP7.3A Future Capability Recommendations.md`).
- **The first discipline-specific engineering module** (Mechanical,
  HVAC, Structural, or Electrical) is the Requirements Engine's own most
  likely first real consumer beyond its own sample module.

## 13. Key Takeaways

1. A concept that sounds like it might need its own storage mechanism
   often does not — the Requirements Engine's own collections, groups,
   and traceability links are all ordinary `DocumentReference` entries,
   the fourth Engineering Core framework in a row to reach this
   conclusion.
2. Keeping two related-sounding concepts (lifecycle status, verification
   outcome) genuinely independent in code, not just in documentation, is
   what makes the separation trustworthy — a future maintainer cannot
   accidentally couple them if no code path exists to do so.
3. A "digital thread" foundation does not require inventing a
   traversal mechanism — composing two already-existing reads
   (`GetVerificationHistoryAsync` + `GetReferencesAsync`) is enough, and
   this Work Package is the first to actually prove it in running code.

## Related Documents

`15-engineering-data-model.md` (the storage foundation every Requirements
concept builds on); `14-verification-framework.md` (the framework whose
own outcome is deliberately kept independent of requirement status);
`ADR-0058`; `ADR-0059`; `ADR-0060`; `ADR-0061`;
`docs/academy/03 Work Packages/WP7.3A-requirements-engine-implementation.md`;
`docs/releases/v0.7.0/WP7.3A Digital Thread Assessment.md`.

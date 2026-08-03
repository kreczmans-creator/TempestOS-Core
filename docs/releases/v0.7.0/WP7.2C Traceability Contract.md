# WP 7.2C — Traceability Contract

## Status

Contract review only. No implementation.

## Purpose

Defines the contractual behaviour for the five traceability dimensions
this Work Package's own controlling instruction names — forward
traceability, backward traceability, allocation traceability,
verification traceability, and evidence traceability — reusing existing
Engineering Core capability wherever possible, per that same
instruction.

## Governing Contract: Traceability Is Traversal, Not Storage

**No traceability-specific storage or index is proposed anywhere in
this document.** Every dimension below is answered entirely by
`IEngineeringDocumentStore.GetReferencesAsync` (for a document's own
outgoing relationships) composed with `IRequirementsService.
GetRelationshipsAsync` (its own thin, typed wrapper) — confirmed already
in `WP7.2B Digital Thread Architecture.md`. This document's own
contribution is naming which relationship kind answers which
traceability question, not inventing a new traversal mechanism.

## 1. Forward Traceability

**Question answered:** "What does this requirement lead to?" — what is
it allocated to, and what satisfies it?

**Contract.** `IRequirementsService.GetRelationshipsAsync(requirementId)`,
filtered to `RequirementRelationshipKinds.AllocatedTo`, returns every
allocation target. The inverse direction — "what satisfies this
requirement" — is answered by `IEngineeringDocumentStore.
GetReferencesAsync` against the requirement's own Id, filtered to
`RequirementRelationshipKinds.Satisfies` relationships whose own target
is this requirement (i.e., a design/delivered-target document's own
outgoing `"satisfies"` relationship, pointed *at* this requirement —
see `WP7.2C Relationship Model.md`'s own direction column: Satisfies
flows *from* the satisfying target *to* the requirement, so forward
traceability's own "what satisfies me" question is answered by
inspecting *incoming* references, not outgoing ones).

**Contractual guarantee.** Every allocation and satisfaction link is
retrievable without traversing any other requirement's own data — a
single call per direction, mirroring `GetReferencesAsync`'s own existing
O(1)-relationship-count contract (not `O(n)` in the total requirement
count).

## 2. Backward Traceability

**Question answered:** "Where did this requirement come from?" — what
source (another requirement, a customer need, a standard clause) does it
derive from?

**Contract.** `IRequirementsService.GetRelationshipsAsync(requirementId)`,
filtered to `RequirementRelationshipKinds.DerivesFrom`, returns every
derivation source. Where the source is another `IRequirement`, the
returned `DocumentReference.TargetDocumentId` resolves via
`IRequirementsService.FindAsync`. Where the source is external (a
customer document, a standard clause), it is an open string reference
(`WP7.2B Requirements Domain Model.md` §11's own "Requirement
References" concept), not a resolvable document Id — disclosed
explicitly, not silently assumed to always resolve.

## 3. Allocation Traceability

**Question answered:** "What is this requirement allocated to, and (in
reverse) what requirements are allocated to a given target?"

**Contract.** Forward direction: identical to Forward Traceability §1,
above. Reverse direction ("which requirements allocate to this
target") is answered by `IEngineeringDocumentStore.GetReferencesAsync`
against the *target's own* document Id — **only if the target is
itself a real `IEngineeringDocument`.** Where the allocation target is
an open string (no target document exists yet, per `WP7.2B Requirements
Domain Model.md` §5's own discipline-neutrality design), **reverse
allocation traceability is not contractually available** — there is no
document Id to query `GetReferencesAsync` against. This is disclosed
explicitly as a real, structural limitation of the open-string
allocation-target design, not silently glossed over: discipline
neutrality (never requiring a target document to exist) and full
reverse-traceability (always being able to query "what points at this
target") are in tension, and this contract accepts the neutrality,
naming the traceability cost explicitly.

## 4. Verification Traceability

**Question answered:** "What verification evidence exists against this
requirement?"

**Contract.** `Tempest.Core.Verification.IVerificationService.
GetVerificationHistoryAsync(requirementId)`, called directly, unmodified
— see `WP7.2C Verification Integration Contract.md`. No traceability
logic of this Platform's own exists between a requirement and its own
verification history; the existing, permission-gated Verification
Framework contract answers this question completely on its own.

## 5. Evidence Traceability

**Question answered:** "What is the complete evidentiary basis behind
this requirement's own current status?"

**Contract.** `IRequirementEvidence` (`WP7.2C Requirements Platform
Contracts.md` §7) — composing Verification Traceability (§4, above)
with every linked `CalculationRecord` and supporting document
(Forward/Backward Traceability, §1–§2, above) into one aggregated read.
No new traversal mechanism; a composition of the four traceability
dimensions already defined.

## Cross-Dimension Confirmation

| Dimension | Reuses Existing Engineering Core Capability? | New Mechanism Introduced? |
|---|---|---|
| Forward | Yes — `GetReferencesAsync`/`GetRelationshipsAsync` | None |
| Backward | Yes — identical | None |
| Allocation | Yes, with a disclosed, structural limitation (§3) | None |
| Verification | Yes — `IVerificationService.GetVerificationHistoryAsync`, entirely | None |
| Evidence | Yes — a composition of the other four | None (a read-side aggregation only) |

**Every one of the five traceability dimensions this Work Package's own
controlling instruction names is answered entirely by capability the
Engineering Core already provides.** This Platform introduces zero new
traversal, indexing, or storage mechanism for traceability — the single
clearest confirmation that `WP7.2B Systems Engineering Architecture.md`'s
own Capability Area 2 boundary ("provides the mechanism, reusing
`LinkAsync`/`GetReferencesAsync` directly... never a discipline-specific
traceability policy") holds at the contract level, not merely the
architecture level.

## Related Documents

`WP7.2C Requirements Platform Contracts.md` §1, §5, §7; `WP7.2C
Relationship Model.md`; `WP7.2C Verification Integration Contract.md`;
`WP7.2B Digital Thread Architecture.md`; `WP7.2B Systems Engineering
Architecture.md` (Capability Area 2).

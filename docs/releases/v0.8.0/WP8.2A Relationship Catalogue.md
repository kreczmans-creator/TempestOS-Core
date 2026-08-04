# WP 8.2A — Engineering Domain Architecture — Relationship Catalogue

## Purpose

Every relationship kind between Engineering Objects, with direction,
ownership, and lifecycle implications, as `WP8.2A Engineering Domain
Architecture.md` §3 requires. Every relationship in this catalogue is
realised, uniformly, as a `DocumentReference(SourceDocumentId,
TargetDocumentId, RelationshipKind)` — an open, unvalidated string,
written via `LinkAsync`, read via `GetReferencesAsync` — never a closed
enum, never a second mechanism (`ADR-0073`). This catalogue is the
platform-wide vocabulary; a module remains free to mint a Kind-agnostic
`RelationshipKind` of its own the platform has never reserved
(`WP8.2A Engineering Domain Architecture.md` §7).

## 1. How to Read Every Entry

- **Direction** — which end is `SourceDocumentId` and which is
  `TargetDocumentId`. Every relationship is directed; a bidirectional
  concept ("Related To") is still written as one directed link,
  traversed from either end via `GetReferencesAsync` on either
  document's own Id.
- **Ownership** — which side's own lifecycle governs the relationship's
  own continued existence (§3, below).
- **Lifecycle** — whether the relationship survives its own source or
  target object being superseded, obsoleted, or archived (`Lifecycle
  Specification.md`).
- **Already Realised As** — the real, shipped `RelationshipKind`
  string, where one already exists, so this catalogue is reconciled
  against shipped code, not merely aspirational.

## 2. Relationship Categories

Every concrete relationship kind (§4) belongs to exactly one category:

| Category | Meaning | Realised Via |
|---|---|---|
| **Composition** | The target cannot exist independently of the source — deleting/obsoleting the source structurally obsoletes the target | `Parent`/`Child` between Assembly/Sub-Assembly/Part |
| **Aggregation** | The target exists independently; the source merely groups it | `Collected In`, `Grouped Under` |
| **Dependency** | The source requires the target to be in a particular state before its own state can advance | `Depends On`, `Blocks` |
| **Reference** | A non-owning cross-reference, no structural implication either way | `Reference`, `Related To` |
| **Allocation** | The source is assigned to be realised, satisfied, or implemented by the target | `Allocation`, `Derived From`, `Supersedes` |
| **Verification** | The target is the recorded proof the source's own claim holds | `Verified By`, `Calculated By` |
| **Evidence** | Not a stored relationship — a composed traversal over other categories (§5) | — |
| **Governance** | The target is a governance event concerning the source | `Documented By`, `Approved By`, `Duplicates` |

## 3. Ownership Rule

**The source owns the relationship, by default, for every category
except Composition, where the parent (whichever end represents the
whole) owns it regardless of which end is written as `Source`.** This
single rule resolves every relationship in this catalogue without a
per-relationship special case — consistent with `Engineering Principle
31`'s own "Kind-agnostic, uninspected target" discipline: the platform
never needs to know which family a `TargetDocumentId` belongs to in
order to apply this rule.

## 4. Full Relationship Table

| Relationship | Category | Direction (Source → Target) | Already Realised As | Lifecycle |
|---|---|---|---|---|
| Parent | Composition | Whole → part | `groupedUnder` inverted — shipped Requirement Group uses child→parent; this catalogue standardises **source = parent, target = child**, disclosed below | Child obsoleted if Parent obsoleted |
| Child | Composition | (the inverse read of Parent, not a second stored link) | — | — |
| Composition | *(category, not a concrete kind)* | — | — | — |
| Aggregation | *(category, not a concrete kind)* | — | — | — |
| Dependency | *(category, not a concrete kind — see Depends On)* | — | — | — |
| Reference | Reference | Referrer → referenced | `references` (`RequirementRelationshipKinds.References`; `VerificationService.ReferencesRelationshipKind`) | Survives either end's lifecycle change; a stale reference is a validation warning, never a structural error (`Validation Specification.md`) |
| Allocation | Allocation | Requirement/source → realising target (any Kind) | `allocatedTo` (`RequirementRelationshipKinds.AllocatedTo`) | Survives target supersession; re-allocation is a new link, the old one is not deleted (append-only, `Engineering Principle 4`) |
| Verification | Verification | Subject → Verification Result | `verifiedBy` (`VerificationService.VerifiedByRelationshipKind`) | Survives subject's own lifecycle change; history is never pruned |
| Evidence | Evidence | *(not a stored link — see §5)* | — | — |
| Derived From | Allocation | Derived object → source it was derived from | `derivesFrom` (`RequirementRelationshipKinds.DerivesFrom`) | Permanent — derivation history is never rewritten |
| Supersedes | Allocation | New object → object it replaces | Conceptual — no shipped constant yet; proposed `supersedes` | Target's own lifecycle transitions to `Superseded` (`Lifecycle Specification.md`) when this link is written |
| Duplicates | Governance | Duplicate → original | Conceptual; proposed `duplicates` | Duplicate typically transitions to `Obsolete`; original unaffected |
| Blocks | Dependency | Blocker → blocked | Conceptual; proposed `blocks` | Blocked object cannot advance its own lifecycle while an unresolved `Blocks` link targets it (`Validation Specification.md` §Lifecycle Constraints) |
| Depends On | Dependency | Dependent → dependency | Conceptual; proposed `dependsOn` (mirrors already-shipped `RequirementRelationshipKinds.DependsOn`) | Same as Blocks, inverse direction |
| Related To | Reference | Either → either | Conceptual; proposed `relatedTo` — the deliberately weakest, most general relationship, used when no more specific category applies | Survives any lifecycle change on either end |
| Manufactured By | Verification-adjacent | Part → Manufacturing Operation (or Supplier) | Conceptual; proposed `manufacturedBy` | Survives Part's own lifecycle change (manufacturing history is permanent record) |
| Verified By | Verification | Subject → Verification Result | `verifiedBy` (identical entry to "Verification," above — the brief names both the category and the concrete kind identically) | See Verification, above |
| Calculated By | Verification | Subject → Calculation Result | Conceptual; proposed `calculatedBy` (distinct from `VerificationService.BasedOnCalculationRelationshipKind`, which links a *Verification Result* to a Calculation Result, one level removed) | Survives subject's own lifecycle change |
| Documented By | Governance | Subject → Document/Drawing/CAD Model | Conceptual; proposed `documentedBy` | Survives subject's own lifecycle change |
| Approved By | Governance | Subject → Approval | Conceptual; proposed `approvedBy` | Written once per approval event; multiple `Approved By` links accumulate as a permanent approval history, never overwritten |

**Disclosed reconciliation note on Parent/Child:** the shipped
`RequirementGroup` hierarchy writes `groupedUnder` from **child to
parent** (`Requirement → Requirement Group`), the opposite direction
this catalogue's own Parent/Child convention states. This is not a
contradiction requiring a code change — `RequirementGroup`'s own
shipped direction remains correct and unchanged; this catalogue simply
records that a future Assembly/Part composition hierarchy should use
the **parent → child** direction instead, since a Part's own most
common query ("what are my children") is more frequent than a
Requirement Group's own most common query ("what is my parent") —
each object family's own dominant traversal direction decides which
way its own composition link points, a deliberate, disclosed
per-family choice, not a platform-wide rule violated.

## 5. Evidence Is Not a Relationship

`Evidence`, named in the controlling instruction alongside the other
nineteen relationship kinds, is **not** realised as a stored
`DocumentReference` at all — it is a composed, read-side traversal over
other relationships (`Verified By`, `Calculated By`, `Documented By`,
`Approved By`), mirroring `IRequirementEvidence`'s own shipped
precedent exactly ("never a new stored entity, only a composition of
already-recorded facts"). See `Digital Thread Specification.md` §3 for
the full traversal definition.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Canonical Object
Catalogue.md`; `WP8.2A Digital Thread Specification.md`;
`ADR-0073`; `Engineering Principle 31`.

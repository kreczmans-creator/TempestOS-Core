# WP 7.2B — Requirements Domain Model

## Status

Architecture only. No production code, no compiled interface — every
concept below is described by its own architectural responsibility, per
this Work Package's own explicit instruction, mirroring `WP7.0C
Engineering Foundation Contracts.md`'s own illustrative (not final)
contract shapes for the original five Engineering Foundation frameworks.

## Purpose

Defines the twelve domain concepts this Work Package's own controlling
instruction names, each as an architectural responsibility — what it
owns, what it delegates, and which existing Engineering Core mechanism
it reuses. No member signature, storage format, or C# type is finalised
here; that is the owning implementation Work Package's own task.

## 1. Requirement

**Responsibility:** the core entity — a single, stated engineering
requirement. **Is-a** relationship: a `Requirement` *is* an
`IEngineeringDocument` (`Kind = "Requirement"`), delegating identity and
revisioning entirely, exactly as `MaterialSpecification` and
`CalculationRecord<TResult>` already do. **Owns:** its own statement
text (the document's own `Content`, opaque, uninterpreted by this
Platform — mirroring Principle 3's "engineering data is independent of
calculations," applied here as "requirement text is independent of what
demonstrates it"), its business identifier (§11), its category (§12),
and its current lifecycle status (§9). **Does not own:** what satisfies
it, what verifies it, or what it is allocated to — all three are
references to something else, never contained data.

## 2. Requirement Collection

**Responsibility:** a named, purpose-built set of requirements — a
baseline, a release scope, a review package. **Is-a:** its own
`IEngineeringDocument` (`Kind = "RequirementCollection"`), linking to
each member requirement via `LinkAsync` with a dedicated relationship
kind (e.g., `"collects"`). **Owns:** membership (which requirements
belong to this collection) and the collection's own identity/name.
**Does not own:** any data about a member requirement itself — a
collection is a view over requirements that exist independently of it,
not a container that owns them; deleting a collection (were deletion
ever supported, which it is not — Principle 4) would never affect a
member requirement's own existence.

## 3. Requirement Group

**Responsibility:** a hierarchical categorisation node — the
"requirement hierarchy" this Work Package's own Architecture section
names explicitly, distinct from Requirement Collection's own
purpose-built, non-hierarchical grouping. **Is-a:** its own
`IEngineeringDocument` (`Kind = "RequirementGroup"`), forming a tree via
a dedicated relationship kind (e.g., `"groupedUnder"`) from a child
group or requirement to its own parent group. **Owns:** its own
position in the hierarchy and its own name. **Does not own:** a closed
depth limit or a fixed taxonomy — the hierarchy's own shape is entirely
caller-defined, mirroring how `Materials`' own `Category` field imposes
no closed set (§12).

## 4. Requirement Relationship

**Responsibility:** the general-purpose, typed, directed link between
two requirements — the mechanism every other relationship concept below
(Allocation, Trace Link) specialises. **Is-a:** a direct application of
`DocumentReference`/`LinkAsync`, with the relationship's own
`RelationshipKind` naming its semantic meaning (e.g., `"derivesFrom"`,
`"conflictsWith"`, `"duplicates"`, `"refines"`). **Owns:** nothing new —
this concept's own architectural contribution is the vocabulary of
relationship kinds this Platform defines as its own constants, mirroring
`VerificationService`'s own `VerifiedByRelationshipKind`/
`ReferencesRelationshipKind` constants (`WP7.1E`).

## 5. Requirement Allocation

**Responsibility:** a specialised relationship linking a requirement to
whatever it is allocated to — a design element, a subsystem, a
component. **This is the single most discipline-neutrality-critical
concept in the entire domain model.** The allocation target is modelled
as **either** a reference to any existing `IEngineeringDocument`
(regardless of `Kind` — the Systems Engineering Foundation does not
inspect or constrain what kind of document an allocation target is)
**or**, when no such document exists yet, an open, unvalidated string
identifier — mirroring `CalculationContext.ReferenceMaterial`'s and
`VerificationContext.ReferenceMaterial`'s own identical precedent
(`AT-16`, `AT-17`) for referencing something this Platform has no hard
dependency on. **Does not own:** any interpretation of what the
allocation target actually is engineeringly — that is entirely the
concern of whatever future discipline module the target belongs to.

## 6. Requirement Trace Link

**Responsibility:** a specialised relationship expressing derivation
(`"derivesFrom"` — this requirement originates from a source, another
requirement, or an external reference) and satisfaction (`"satisfiedBy"`
— this requirement is met by a design element, tracked the same way
Allocation targets are). **Is-a:** the same `DocumentReference`/
`LinkAsync` mechanism as Requirement Relationship, with its own reserved
relationship-kind vocabulary. **Owns:** nothing beyond the link itself
— traceability is a traversal property of the reference graph
(`WP7.2B Digital Thread Architecture.md`), never a separately computed
or cached value.

## 7. Requirement Verification Link

**Responsibility:** **this is not a new concept — it is
`Tempest.Core.Verification`'s own existing `verifiedBy` relationship,
named here only so the domain model's own vocabulary is complete.**
Recording a requirement's own verification calls
`IVerificationService.RecordAsync(requirementDocumentId, outcome,
method, context)` directly. This Platform introduces **zero** new types,
storage, or relationship kinds for verification — `VerificationService`
already creates the `verifiedBy` link as part of its own existing
`RecordAsync` implementation, exactly as it does for any other subject
document today.

## 8. Requirement Evidence

**Responsibility:** the complete evidentiary basis for a requirement's
own status — **an aggregation, not a new stored entity.** Drawn
entirely from: every `VerificationRecord.Criteria`/`Evidence` recorded
against the requirement (via `GetVerificationHistoryAsync`), every
linked `CalculationRecord`'s own assumptions and results, and every
linked supporting document's own content. **Owns:** the *presentation
shape* of this aggregation only (how the pieces are assembled for a
caller to read) — never the underlying facts themselves, each of which
remains owned by its own originating framework. This mirrors
`CalculationRecord<TResult>`'s own provenance design (Principle 20):
evidence is what the record already contains, assembled, not a
duplicate copy.

## 9. Requirement Status

**Responsibility:** the requirement's own lifecycle/workflow position
(e.g., Draft, Proposed, Approved, Verified, Rejected, Withdrawn) —
**deliberately distinct from `VerificationOutcome`.** A requirement can
be `Approved` without ever having been verified; a `Verified` status (if
this Platform chooses to define one) would be a status *set by a
caller* upon reviewing verification evidence, never a value the Engine
itself derives automatically from a `VerificationRecord`'s own
`Outcome`. This preserves the same "engineering evidence vs. engineering
judgement" separation `Tempest.Core.Verification`'s own design already
establishes (`Criteria`/`Evidence` vs. `Outcome`) — Status is judgement;
a `VerificationRecord`'s own `Outcome` is evidence. **Owns:** the
requirement's own current workflow state. **Does not own:** any
automatic transition logic — status changes are caller-driven, an
explicit architectural decision deferred to the owning implementation
Work Package (§ "What This Domain Model Does Not Decide," below).

## 10. Requirement Revision

**Responsibility:** **not a new concept — inherited directly from
`IDocumentRevision`.** Every change to a requirement's own statement
produces a new revision, exactly as every other `IEngineeringDocument`
already does (Principle 2). No second revision model, no "requirement-
specific" versioning scheme.

## 11. Requirement Identifier

**Responsibility:** a stable, human-facing business key (e.g.,
`"SYS-REQ-042"`), distinct from the underlying `IEngineeringDocument.Id`
(a `Guid`) — **mirroring `MaterialCatalog`'s own `materialId` index
precedent exactly** (`ADR-0055` Decision 3: a direct `IPersistenceStore`
dependency for lookup-by-arbitrary-string, since `IEngineeringDocumentStore`
itself has no such capability). **Owns:** the mapping from a
caller-chosen identifier string to the underlying document Guid.
**Does not own:** any enforced format or numbering scheme — the
identifier's own shape (a flat string, a hierarchical dotted numbering,
a standard-mandated format) is left open, consistent with this
architecture's own industry-neutral, discipline-neutral constraint.

## 12. Requirement Category

**Responsibility:** an open, extensible classification (e.g.,
functional, non-functional, performance, safety, interface,
regulatory). **Modelled as a plain, nullable string** — mirroring
`IMaterialSpecification.Category`'s own identical, already-proven
precedent (confirmed directly: `Category` is `string?`, not a closed
enum). **Owns:** nothing beyond the classification value itself. **Does
not own:** a fixed taxonomy — inventing one now, before a real
discipline module needs a specific set, would repeat exactly the
anti-pattern `WP7.0B Engineering Discipline Assessment.md`'s own
"cannot be sequenced from existing evidence" finding warns against, one
level down (inventing a category *taxonomy* instead of a category
*capability*).

## What This Domain Model Does Not Decide

Per this Work Package's own "architectural responsibilities only"
instruction, the following remain open, explicitly deferred to the
owning implementation Work Package's own Contract Review phase:

- The exact C# type shape of any of the twelve concepts above (record
  vs. class, exact member names, exact method signatures).
- Whether `Requirement Status` is a closed enum or an open string
  (mirroring the same `VerificationOutcome`-vs-`Category` tension this
  domain model itself resolves inconsistently by design — Status
  benefits from a closed, small set the way `VerificationOutcome` does,
  since lifecycle transitions are a workflow concern with real
  structure; Category benefits from an open string the way
  `Material.Category` does, since no closed taxonomy exists yet — both
  precedents exist in the Engineering Core already, and the Contract
  Review phase should choose per-concept, not uniformly).
- Any automatic status-transition logic.
- The exact relationship-kind string constants for Allocation, Trace
  Link, and Requirement Relationship (illustrative names only are given
  above).

## Related Documents

`ADR-0055` (Materials' own `materialId` index precedent); `ADR-0056`,
`ADR-0057` (Calculation's and Verification's own open-reference
precedents, `AT-16`/`AT-17`); `docs/engineering/Engineering
Principles.md` (Principles 2-6, 13, 16, 20); `WP7.2B Requirements
Platform Architecture.md`; `WP7.2B Systems Engineering Architecture.md`;
`WP7.2B Digital Thread Architecture.md`.

# WP 8.2A — Engineering Domain Architecture — Validation Specification

## Purpose

The architectural validation rules every Engineering Object must
satisfy — required/optional fields, relationship constraints, lifecycle
constraints, approval constraints, deletion rules, and reference
integrity. Every rule here is either already enforced by shipped code
(cited directly) or named as architecture for a future implementation
Work Package to enforce — this Work Package defines the rule, not the
enforcing code (no implementation, per its own explicit constraint).

## 1. Required Fields (Every Engineering Object)

| Field | Required? | Enforcement Precedent |
|---|---|---|
| `Id` (`Guid`) | Always, assigned once at creation, never optional | `IEngineeringDocument.Id` — structural, cannot be constructed without one |
| `Kind` (`string`) | Always, assigned once at creation | `IEngineeringDocument.Kind` — structural |
| First revision `Content` | Always — an object with zero revisions does not exist | `IEngineeringDocumentStore.CreateAsync` requires `initialContent` |
| `CreatedAt`/`AuthorPrincipalId` | Always, resolved automatically, never caller-supplied | Falls back to `"unknown"` rather than failing the write (`EngineeringDocumentStore.UnknownAuthorPrincipalId`) — the field is always populated, never blank |
| Lifecycle `Status` | Always, defaults to `Draft` at creation (`Lifecycle Specification.md` §1) | `RequirementStatus` precedent — every `Requirement` is created `Draft`, never statusless |

## 2. Optional Fields (Every Engineering Object)

Every field in `Metadata Specification.md` §1–§4 **except** the five
listed as required in §1, above, is optional by default — a family may
promote one to required for its own Kind (a Drawing might require
`Units`; a generic `Document` might not), but the canonical baseline
never mandates more than identity, one revision, and a lifecycle state.
Human-readable business identifiers (`Requirement.Identifier`,
`MaterialSpecification.MaterialId`) are a **family-level** requirement,
not a canonical one — most families will want one; the canonical shape
does not mandate it, since `Id` alone is always sufficient for
correctness.

## 3. Relationship Constraints

1. **No self-reference** — `SourceDocumentId != TargetDocumentId`,
   structurally enforced today by `IEngineeringDocumentStore.LinkAsync`
   (`Digital Thread Specification.md` §5).
2. **No relationship-kind validation at write time** — any string is
   accepted; a "wrong-looking" relationship (§ `Digital Thread
   Specification.md` §5) is a **validation warning** a future
   reporting/linting capability could surface, never a rejected write
   — consistent with `Kind` itself carrying no closed vocabulary.
3. **Composition implies cascading lifecycle** — a `Parent`
   relationship's own target (the child) should transition toward
   `Obsolete`/`Archived` when its own parent does (`Relationship
   Catalogue.md` §4) — a **recommended**, not structurally enforced,
   rule; enforcing it would require a closed Kind registry this
   platform does not have.

## 4. Lifecycle Constraints

1. **Only table-permitted transitions succeed** — `Lifecycle
   Specification.md` §2/§4; violation raises a family-scoped exception,
   mirroring `InvalidRequirementStatusTransitionException`'s own
   shipped precedent.
2. **`Draft → InReview → Approved` is monotonic within one
   revision** — an object cannot be simultaneously `Approved` on one
   revision and silently reset to `Draft` by a later revision without
   an explicit status transition recording that fact; status and
   revision advance independently but both are always explicit,
   auditable, caller-driven actions (`Engineering Principle 29`/`30`).
3. **A `Blocks`/`Depends On` relationship prevents lifecycle
   advancement** — an object with an unresolved incoming `Blocks` link
   should not be permitted to transition to `Approved`/`Released`
   (`Relationship Catalogue.md` §4) — named as architecture; not
   structurally enforced by any shipped mechanism today (no cross-object
   lifecycle-gating code exists yet in `Tempest.Core.Requirements` or
   any other framework).

## 5. Approval Constraints

`InReview → Approved` requires a resolvable `Approved By` relationship
to a real `Approval` object (`Lifecycle Specification.md` §3) —
**named as the canonical rule; not yet structurally enforced by any
shipped code.** `RequirementStatusTransitions` today permits
`Reviewed → Approved` with no linked-evidence check at all — a
disclosed gap between this Work Package's own canonical rule and
`WP 7.3A`'s own shipped, narrower behaviour, left open for a future
Requirements Contract Review to close, not corrected here.

## 6. Deletion Rules

**No Engineering Object is ever physically deleted** (`Lifecycle
Specification.md` §5, `Engineering Principle 4`). "Deleting" an object
is always one of:

- Transitioning it to `Cancelled` (work never completed).
- Transitioning it to `Obsolete` (no longer valid, no replacement).
- Transitioning it to `Archived` (retained for record only).

A UI or API surface that appears to "delete" an Engineering Object must
realise that action as one of the three transitions above — never a
store-level remove operation. This is already structurally true today:
no method on `IEngineeringDocumentStore` removes a document or a
revision.

## 7. Reference Integrity

1. **A `DocumentReference`'s own `TargetDocumentId` is never validated
   against an existing document at write time** — `LinkAsync` accepts
   any `Guid`, by design (`Engineering Principle 31`'s own
   Kind-agnostic, "never inspected/constrained" discipline, extended to
   existence itself, not only Kind). This permits linking to an object
   that legitimately does not exist yet in the calling context (a
   forward reference to a not-yet-created Purchase Item, for instance).
2. **A dangling reference is a read-time concern, not a write-time
   one** — `GetReferencesAsync` returns the reference regardless of
   whether the target still resolves via `FindAsync`; a caller (or a
   future validation/reporting capability) decides what to do with an
   unresolvable target, the platform does not decide for it.
3. **Referential integrity across a Baseline is stronger** — because a
   Baseline freezes specific *(object, revision-number)* pairs
   (`Configuration Management Specification.md` §3), a Baseline's own
   member list should be validated, at Baseline-creation time, against
   real, currently-resolvable objects — the one point in this
   specification where reference integrity is a create-time concern,
   not merely a read-time one, since a Baseline's own entire purpose is
   to be a trustworthy frozen record.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Lifecycle
Specification.md`; `WP8.2A Relationship Catalogue.md`; `WP8.2A
Configuration Management Specification.md`; `WP8.2A Digital Thread
Specification.md`; `Engineering Principle 4`, `13`, `29`, `30`, `31`;
`InvalidRequirementStatusTransitionException` (`src/Tempest.Core/
Requirements/`).

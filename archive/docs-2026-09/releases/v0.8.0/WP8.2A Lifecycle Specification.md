# WP 8.2A — Engineering Domain Architecture — Lifecycle Specification

## Purpose

The canonical lifecycle vocabulary every Engineering Object family
specialises from, its own default transition table, approval gates,
and validation rules — resolving `ADR-0074` (lifecycle status is a
common canonical vocabulary, specialised per object family, never one
rigid global enum, never fully ad hoc per module).

## 1. The Canonical States

Eight states, named directly from the controlling instruction:

| State | Meaning | Terminal? |
|---|---|---|
| `Draft` | Being authored; not yet ready for review | No |
| `InReview` | Under active review | No |
| `Approved` | Reviewed and accepted; not yet in force | No |
| `Released` | In force — the object's own current, authoritative state | No |
| `Superseded` | Replaced by a newer object or revision (see `Supersedes`, `Relationship Catalogue.md`) | No (advances to `Archived`) |
| `Obsolete` | No longer valid, not replaced by anything specific | No (advances to `Archived`) |
| `Archived` | Retained for record only; no further transition | **Yes** |
| `Cancelled` | Work stopped before reaching `Released` | **Yes** |

## 2. The Canonical Transition Table

```
Draft      -> InReview, Cancelled
InReview   -> Draft, Approved, Cancelled
Approved   -> Draft, Released, Cancelled
Released   -> Superseded, Obsolete, Archived
Superseded -> Archived
Obsolete   -> Archived
Archived   -> (terminal)
Cancelled  -> (terminal)
```

Same-to-same transitions are never permitted (mirrors
`RequirementStatusTransitions`'s own shipped discipline, which likewise
never special-cases them). This table is the **default** every object
family inherits; §4 below defines how a family specialises it.

## 3. Approval Gates

`InReview → Approved` is the one canonical transition that structurally
requires evidence: an `Approved By` relationship (`Relationship
Catalogue.md` §4) to a real `Approval` Engineering Object must exist
before this transition is permitted. Every other transition in §2
requires no linked evidence by default — a family may add its own
stricter gate (§4).

## 4. Per-Family Specialisation

A family specialises the canonical table in exactly three ways, never
a fourth:

1. **Insert additional states** between `Approved` and the family's own
   terminal states, for domain-specific workflow the canonical eight
   do not capture.
2. **Omit states** the family has no use for (a family with no
   configuration-managed release step may never reach `Released` at
   all).
3. **Add a stricter approval gate** to any transition the canonical
   table leaves ungated.

A family may **never** invent a transition the canonical table
forbids outright (e.g. `Archived → Draft`) — reopening archived work
is always a new object, `Derived From` the archived one, never a
reverse transition.

### 4.1 Worked Reconciliation: `RequirementStatus` (Shipped)

`RequirementStatus`'s own seven values (`Draft`, `Reviewed`, `Approved`,
`Allocated`, `Verified`, `Satisfied`, `Obsolete`) are a real,
already-shipped specialisation, reconciled against the canonical table
as follows — **no code change implied or required**:

| `RequirementStatus` | Canonical Mapping |
|---|---|
| `Draft` | `Draft` |
| `Reviewed` | `InReview` (named differently — a disclosed, historical naming divergence, not a contradiction) |
| `Approved` | `Approved` |
| `Allocated` | *(inserted)* — domain-specific, between `Approved` and `Verified` |
| `Verified` | *(inserted)* — domain-specific |
| `Satisfied` | *(inserted, functions as the family's own `Released`)* |
| `Obsolete` | `Obsolete` |

Requirements omits `Released`, `Superseded`, `Archived`, and
`Cancelled` entirely (rule 2, above) — `Satisfied` already functions as
the family's own "in force" terminal-ish state, and nothing in
`WP 7.3A`'s own shipped scope needed the remaining three. This is named
here as a disclosed gap, not corrected: a future Requirements Contract
Review may choose to adopt `Archived`/`Cancelled` explicitly; this
Work Package does not mandate it (architecture only, no implementation
impact).

## 5. Deletion

**No Engineering Object is ever physically deleted.** "Deletion" is
always realised as a transition to `Obsolete`, `Cancelled`, or
`Archived` — never a removal from the store. This is not a new rule;
it is `Engineering Principle 4`'s own immutability discipline, restated
as canonical lifecycle architecture and now made binding on every
object family, not only `Tempest.Core.EngineeringData`'s own revision
history.

## 6. Validation Rules Summary

Full detail in `Validation Specification.md` §Lifecycle Constraints;
summarised here for completeness:

- A transition not present in the effective table (canonical plus
  family specialisation) is rejected, mirroring
  `InvalidRequirementStatusTransitionException`'s own shipped
  precedent — every family gets an equivalent, family-scoped exception.
- An `Approved` transition without a resolvable `Approved By` link is
  rejected (§3).
- A `Released`/`Approved`/terminal object's own `Content` cannot be
  revised in place — a new revision is always permitted (the existing
  `IDocumentRevision` mechanism already allows this unconditionally),
  but a family may require a fresh `Draft`-equivalent state before
  further revision, mirroring `Approved → Draft`'s own presence in the
  canonical table specifically to support this.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Canonical Object
Catalogue.md`; `WP8.2A Relationship Catalogue.md`; `WP8.2A Validation
Specification.md`; `ADR-0074`; `RequirementStatus`/
`RequirementStatusTransitions` (`src/Tempest.Core/Requirements/`).

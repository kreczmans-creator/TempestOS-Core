# ADR-0076: Relationship Contracts Are Realised as One Generic `IEngineeringRelationship` Interface, Not a Closed Set of Per-Category Types

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2B` (Engineering
Domain Contracts), 2026-08-04. Resolves a direct tension between this
Work Package's own controlling instruction and `ADR-0073`'s own
already-locked decision.

## Context

`WP 8.2B`'s own controlling instruction names seventeen relationship
categories (Parent, Child, Composition, Aggregation, Reference,
Dependency, Verification, Evidence, Allocation, Derivation,
Supersession, Manufacturing, Calculation, Documentation, Risk, Change,
Decision) and states "define contracts governing" each, with every
relationship exposing "Direction, Multiplicity, Ownership, Lifecycle,
Validation." Read literally, this could mean seventeen separate
interface types (`IParentRelationship`, `IVerificationRelationship`,
and so on).

`ADR-0073` (`WP 8.2A`) already decided, as binding platform-wide
architecture, that relationships between Engineering Objects are
open-string, unvalidated `DocumentReference`s — explicitly **never a
closed enum**, precisely because a closed relationship-type set cannot
scale to fifty object families and their own future extensions without
becoming a review bottleneck. Seventeen separate interface types would
be a closed set by construction — a direct, if unintentional,
contradiction of a decision made one Work Package earlier in the same
release.

## Decision

**Every relationship between Engineering Objects is realised as one
interface, `IEngineeringRelationship` (`WP8.2B Relationship Contract
Specification.md` §1) — `SourceId`, `TargetId`, an open
`RelationshipKind` string (unchanged from `ADR-0073`), and a
`RelationshipCategory` enum carrying the seventeen named categories as
*descriptive metadata only*, never validated against `RelationshipKind`
at write time.** Direction, Multiplicity, Ownership, Lifecycle, and
Validation are each resolved as a documented rule or a small
descriptor type (`IRelationshipDescriptor`) layered over this one
interface, never as members of seventeen separate types.

## Consequences

**Positive:**

- Fully honours the controlling instruction's own request (every named
  category is addressable, queryable, and documented) without
  reopening `ADR-0073`'s own settled decision — the two requirements
  turn out to be compatible once "governing a relationship category"
  is understood as "documenting a convention," not "defining a closed
  type."
- A relationship between two object families with no category that
  cleanly fits any of the seventeen (a future `Hazard` referencing a
  future `Supplier`, say) is still fully expressible — `Category` can
  be the closest approximate value, or omitted from strict
  significance, while `RelationshipKind` (the only field ever actually
  interpreted by `LinkAsync`) remains exactly as open as `ADR-0073`
  already requires.
- One interface, one factory contract
  (`IEngineeringRelationshipFactory`), one validator
  (`IRelationshipValidator`) — not seventeen of each — keeps the
  Engineering Domain's own contract surface proportional to its real
  complexity, not to the number of categories a product brief happened
  to enumerate.

**Negative:**

- `RelationshipCategory` carries no structural guarantee it matches
  `RelationshipKind` — a caller can construct an
  `IEngineeringRelationship` with `Category = Verification` and
  `RelationshipKind = "manufacturedBy"`, and nothing in this contract
  layer prevents it. This is the same trade-off `ADR-0073` already
  accepted for `RelationshipKind` alone, now extended to `Category` —
  disclosed, not new.
- A consumer wanting compile-time guarantees about a specific
  relationship category (a method that only accepts a `Verification`
  relationship, say) cannot express that constraint in the type system
  — it must be a runtime check against `Category`, mirroring how
  `Kind`-specific logic already requires a runtime check against
  `Kind` itself, platform-wide.

## Alternatives Considered

**Seventeen separate relationship interface types**, one per named
category — considered and rejected; see Context, above. Directly
contradicts `ADR-0073`.

**Dropping `RelationshipCategory` entirely, keeping only
`RelationshipKind`** — considered and rejected. Would satisfy
`ADR-0073` cleanly but would silently drop the controlling
instruction's own explicit request that every relationship "govern" a
named category — `Category` as descriptive metadata is the minimum
addition that honours both this Work Package's own instruction and the
prior, binding decision it must not contradict.

## Related Documents

`ADR-0073`; `WP8.2A Relationship Catalogue.md`; `WP8.2B Relationship
Contract Specification.md`; `WP8.2B Engineering Domain Contracts.md`
§4.

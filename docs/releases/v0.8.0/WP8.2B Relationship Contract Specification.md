# WP 8.2B — Engineering Domain Contracts — Relationship Contract Specification

## Purpose

The contract shape for every relationship between Engineering Objects
— one generic interface, not seventeen (`ADR-0076`), realising
`WP8.2A Relationship Catalogue.md` at the contract level. Proposed,
uncompiled C#, `Tempest.Core.EngineeringDomain` namespace throughout.

## 1. `IEngineeringRelationship`

```csharp
public interface IEngineeringRelationship
{
    Guid SourceId { get; }
    Guid TargetId { get; }

    /// <summary>The open, unvalidated relationship vocabulary string (ADR-0073) — for example "verifiedBy."</summary>
    string RelationshipKind { get; }

    /// <summary>Descriptive metadata only — never used to validate or restrict RelationshipKind (§2).</summary>
    RelationshipCategory Category { get; }

    string CreatedByPrincipalId { get; }
    DateTimeOffset CreatedAt { get; }
}
```

This is the **only** relationship interface this catalogue proposes.
Every one of the seventeen named categories in the controlling
instruction is a `RelationshipCategory` value on this one interface,
never a distinct type — resolving the tension between the controlling
instruction's own per-category framing and `ADR-0073`'s already-locked
"open string, never a closed enum" decision (`ADR-0076`).

## 2. `RelationshipCategory`

```csharp
/// <summary>Descriptive grouping only (WP8.2A Relationship Catalogue.md §2) — never validated against RelationshipKind at write time.</summary>
public enum RelationshipCategory
{
    Parent,
    Child,
    Composition,
    Aggregation,
    Reference,
    Dependency,
    Verification,
    Evidence,
    Allocation,
    Derivation,
    Supersession,
    Manufacturing,
    Calculation,
    Documentation,
    Risk,
    Change,
    Decision,
}
```

**Why descriptive, not enforced:** `ADR-0073` already decided
`RelationshipKind` is Kind-agnostic and unvalidated; adding an enforced
`RelationshipCategory` would silently reopen that decision through a
side door. `Category` exists so tooling (search, filtering, the
Workspace's own future Command Palette) can group relationships
sensibly for a human reader — it carries no structural authority. A
`RelationshipKind` of `"verifiedBy"` is conventionally
`RelationshipCategory.Verification`, but nothing in this contract
prevents a caller from constructing an `IEngineeringRelationship` with
a mismatched `Category` — exactly as nothing in `LinkAsync` today
validates that a `"verifiedBy"` string actually targets a
`VerificationRecord` (`WP8.2A Validation Specification.md` §3.2).

## 3. Category-to-Kind Mapping (Reference Table)

The conventional mapping every implementation should follow, restating
`WP8.2A Relationship Catalogue.md` §4 as a contract-facing lookup:

| `RelationshipCategory` | Conventional `RelationshipKind` string(s) |
|---|---|
| `Parent`/`Child` | *(family-defined — e.g. composition child→parent for Assembly/Part)* |
| `Composition` | *(structural — realised via Parent/Child)* |
| `Aggregation` | `collects`, `groupedUnder` |
| `Reference` | `references`, `relatedTo` |
| `Dependency` | `dependsOn`, `blocks` |
| `Verification` | `verifiedBy` |
| `Evidence` | *(not a stored kind — composed, §5 below)* |
| `Allocation` | `allocatedTo` |
| `Derivation` | `derivesFrom` |
| `Supersession` | `supersedes` |
| `Manufacturing` | `manufacturedBy` |
| `Calculation` | `calculatedBy`, `basedOnCalculation` |
| `Documentation` | `documentedBy` |
| `Risk` | `relatedTo` (Risk has no dedicated kind — a Reference by convention) |
| `Change` | `derivesFrom` (an Engineering Change from its own Change Request) |
| `Decision` | `approvedBy`, `relatedTo` |

## 4. Direction, Multiplicity, Ownership, Lifecycle, Validation

The controlling instruction's own five required facets, each resolved
as a member or documented rule on `IEngineeringRelationship`:

```csharp
public interface IRelationshipDescriptor
{
    RelationshipCategory Category { get; }

    /// <summary>Always Source -> Target; a bidirectional concept (Related To) is still one directed link, traversable from either end.</summary>
    RelationshipDirection Direction { get; }

    RelationshipMultiplicity Multiplicity { get; }
}

public enum RelationshipDirection
{
    SourceToTarget,
}

public enum RelationshipMultiplicity
{
    OneToOne,
    OneToMany,
    ManyToMany,
}
```

- **Direction** — always `SourceToTarget`, structurally (§1); the enum
  exists only to make the fact explicit and queryable, not because a
  second value is anticipated.
- **Multiplicity** — descriptive metadata per `RelationshipCategory`
  (an `Allocation` is typically `OneToMany`; a `Parent`/`Child` link is
  typically `OneToOne` per child), looked up via
  `IRelationshipDescriptor`, never structurally enforced (mirroring
  `Category`'s own descriptive-only status, §2).
- **Ownership** — resolved by `WP8.2A Relationship Catalogue.md` §3's
  own rule: the source owns the relationship, except Composition, where
  the parent owns it regardless of which end is `Source`. No new
  contract member — `Ownership` is a documented rule over `SourceId`/
  `Category`, not stored data.
- **Lifecycle** — whether the relationship survives its own source/
  target's lifecycle change; documented per category in `WP8.2A
  Relationship Catalogue.md` §4's own table, not a stored field (every
  real relationship survives every lifecycle change today — no shipped
  mechanism prunes `DocumentReference`s).
- **Validation** — `IValidatable` (`Interface Catalogue.md` §1), applied
  to a relationship exactly as to any other object; a `Validate`
  implementation checks structural rules only (no self-reference,
  §5, below), never `RelationshipKind`/`Category` consistency (§2).

## 5. Structural Validation

```csharp
public interface IRelationshipValidator
{
    /// <summary>The one structural rule this platform enforces (WP8.2A Digital Thread Specification.md §5): SourceId != TargetId.</summary>
    Task<IValidationResult> ValidateAsync(IEngineeringRelationship relationship, CancellationToken cancellationToken = default);
}
```

## 6. Evidence Is Not a Relationship (Restated)

`WP8.2A Relationship Catalogue.md` §5 already established this;
restated at the contract level: `IEvidence` (`Interface Catalogue.md`
§13) is composed by a traversal implementation, never constructed via
`IEngineeringRelationshipFactory`. No `RelationshipCategory.Evidence`
value corresponds to a real, storable `RelationshipKind` — it exists in
the enum (§2) only because the controlling instruction named it
alongside the other sixteen; its own row in §3's mapping table says so
explicitly.

## Related Documents

`WP8.2B Engineering Domain Contracts.md`; `WP8.2B Interface
Catalogue.md`; `WP8.2A Relationship Catalogue.md`; `ADR-0073`;
`ADR-0076`; `DocumentReference`/`LinkAsync`
(`src/Tempest.Core/EngineeringData/`).

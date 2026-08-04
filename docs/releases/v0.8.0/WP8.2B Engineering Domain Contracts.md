# WP 8.2B — Engineering Domain Contracts

## Purpose

The complete public contract for the Engineering Domain Architecture
`WP 8.2A` established — proposed, uncompiled C#, exactly as `WP7.0C
Engineering Foundation Contracts.md`/`WP7.2C Requirements Platform
Contracts.md`/`WP8.0B Workspace Contracts.md` each already established
the precedent for. No implementation, no concrete classes, no
repositories, no persistence, no serialization, no storage technology,
no UI. Every interface named below becomes the permanent API every
current and future TempestOS module implements against or consumes —
contracts only, frozen, technology-independent.

This document is the master reference; seven companion deliverables go
deeper into one area each (`Interface Catalogue`, `Lifecycle Contract
Specification`, `Relationship Contract Specification`, `Validation
Contract Specification`, `Digital Thread Contract Specification`,
`Sequence Diagrams`, `Dependency Rules`), plus Academy documentation
and two new ADRs.

## 0. Grounding: Contracts Over an Existing Store, Not a New One

Per `ADR-0072` (`WP 8.2A`), every canonical Engineering Object is
realised as an `IEngineeringDocumentStore`-backed `Kind`. This Work
Package's own contracts are therefore **views and behaviour over that
existing store**, never a competing persistence abstraction — the same
relationship `IRequirementsService`/`IVerificationService` already have
to `IEngineeringDocumentStore` today, generalised to every canonical
object. A future implementation Work Package realises `IRequirement`,
`IAssembly`, `IPart`, and every other interface in this catalogue
exactly the way `Requirement`/`RequirementDto` already realise
`IRequirement` today: a thin typed wrapper over `IEngineeringDocument`/
`IDocumentRevision`/`DocumentReference`, resolved through
`IEngineeringDocumentStore`. No interface in this Work Package proposes
a repository, a query language, or a storage technology of any kind.

## 1. General Principles, Made Concrete

The controlling instruction's own five general principles, each
resolved into a specific contract-design rule:

| Principle | Resolution |
|---|---|
| Immutable where practical | Every read-side interface exposes only getters; every mutation is a method returning a new/updated read (mirrors `IRequirementsService.ReviseAsync` returning the new `IRequirement`, never mutating in place) |
| Composition over inheritance | Every canonical object interface composes a small set of **facet interfaces** (§3) via multiple interface implementation — never a deep single-inheritance chain (`ADR-0075`) |
| Expose behaviour, not storage | No interface exposes a `Guid` foreign key naming another interface's own storage location, a table name, or a query string — every reference is a typed member (`Guid`, another interface, or a `DocumentReference`-shaped read) |
| Support future persistence/networking/plugins/cloud sync/collaboration | Every interface is a plain C# contract with no dependency on any concrete technology — the same discipline that let `IEngineeringDocumentStore` itself be backed by `IPersistenceStore` without any consuming framework knowing or caring |
| No contract leaks implementation details | No interface exposes `EngineeringDocumentDto`, `IPersistenceStore`, or any other internal type; every member type is either a primitive, a `Quantity<TDimension>`, or another public contract interface |

## 2. `IEngineeringObject` — The Base Contract

```csharp
namespace Tempest.Core.EngineeringDomain;

/// <summary>The base contract every canonical Engineering Object satisfies.</summary>
public interface IEngineeringObject
{
    /// <summary>The object's own permanent identity, assigned once at creation.</summary>
    Guid Id { get; }

    /// <summary>The object's own Kind — for example "Requirement" or "Part".</summary>
    string Kind { get; }

    /// <summary>The object's own current revision number.</summary>
    int CurrentRevisionNumber { get; }

    /// <summary>When the object's own first revision was created.</summary>
    DateTimeOffset CreatedAt { get; }
}
```

Directly mirrors the real, shipped `IEngineeringDocument`
(`Tempest.Core.EngineeringData`) — deliberately identical in shape,
since `ADR-0072` requires every canonical object to be backed by
exactly that type. `IEngineeringObject` is this Work Package's own
domain-facing name for the same contract; a future implementation Work
Package may realise it as a direct alias, or as a thin wrapper — both
are conforming, since this Work Package proposes no implementation.

## 3. Facet Interfaces (§`Interface Catalogue.md` §1 for full signatures)

`ADR-0075` resolves "composition over inheritance" into ten small,
focused facet interfaces — `IHasBusinessIdentifier`, `IHasMetadata`,
`IHasLifecycle`, `IHasRevisions`, `IHasRelationships`, `ITraceable`,
`IValidatable`, `IHasAttachments`, `ISearchable` — each covering one or
more of the twenty named "Common Behaviour" concerns
(`WP8.2A Metadata Specification.md`'s own consolidation precedent,
extended to contracts). Every one of the ~49 canonical object
interfaces composes `IEngineeringObject` plus whichever facets are
relevant to it — never all ten unconditionally, and never a class
inheritance chain.

## 4. Relationship Contracts — One Generic Interface, Not Seventeen

`ADR-0076` resolves the controlling instruction's own seventeen named
relationship categories (Parent, Child, Composition, ..., Decision)
against `ADR-0073`'s own already-locked "open-string, never a closed
enum" decision: **one generic `IEngineeringRelationship` interface**
(`Relationship Contract Specification.md` §1), carrying a
`RelationshipCategory` enum for the seventeen named categories as
*descriptive metadata*, and an open `string RelationshipKind` for the
actual link vocabulary — never seventeen separate relationship types.
Every relationship exposes Direction, Multiplicity, Ownership,
Lifecycle, and Validation as members of this one interface
(`Relationship Contract Specification.md` §2).

## 5. Search Contracts

Named at the contract level only, consuming the common metadata
envelope every `IHasMetadata` implementer already carries:

```csharp
public interface ISearchQuery
{
    string? Text { get; }
    string? Kind { get; }
    string? Category { get; }
    IReadOnlyDictionary<string, string>? MetadataFilters { get; }
}

public interface ISearchResult
{
    IReadOnlyList<IEngineeringObject> Matches { get; }
    int TotalCount { get; }
}

public interface ISavedQuery
{
    Guid Id { get; }
    string Name { get; }
    ISearchQuery Query { get; }
}
```

No `ISearchService`/`ISearchIndex` interface is proposed here — search
execution is a future Platform Service's own concern
(`WP8.2A Engineering Domain Architecture.md` §6), not this Work
Package's; these three types exist only so a future search capability
has a stable request/result shape to build against, satisfying "future
global search compatibility" without designing the service itself.

## 6. Extensibility Contracts

```csharp
public interface IEngineeringObjectFactory
{
    /// <summary>The Kind this factory constructs objects for.</summary>
    string Kind { get; }

    Task<IEngineeringObject> CreateAsync(string initialContent, CancellationToken cancellationToken = default);
}

public interface IEngineeringRelationshipFactory
{
    /// <summary>The RelationshipKind this factory constructs relationships for.</summary>
    string RelationshipKind { get; }

    Task<IEngineeringRelationship> CreateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);
}
```

Mirrors `ADR-0067`'s own Kind-keyed registration precedent
(`IWorkspaceViewFactory`/`IProjectExplorerNodeProvider`) directly,
applied here to the Engineering Domain itself rather than the
Workspace — a module registers a factory for its own custom object or
relationship Kind, never modifying a platform registry to do so. Full
extensibility rules (module-defined metadata/behaviour/validation,
compatibility guarantees) are named in `WP8.2A Engineering Domain
Architecture.md` §7, unchanged and not repeated here — this Work
Package adds only the two factory contracts that name needed.

## 7. ADR Summary

| ADR | Decision |
|---|---|
| `ADR-0075` | Engineering Object contracts are composed from small facet interfaces, never one monolithic interface or a class-inheritance chain |
| `ADR-0076` | Relationship contracts are realised as one generic `IEngineeringRelationship` interface with an open `RelationshipKind` string, never a closed set of seventeen per-category types |

## 8. Summary of Companion Deliverables

| Deliverable | Covers |
|---|---|
| `WP8.2B Interface Catalogue.md` | All ~55 named interfaces — facets, all ~49 canonical object interfaces, factories |
| `WP8.2B Lifecycle Contract Specification.md` | `ILifecycleState`, transitions, approval/review/release gates |
| `WP8.2B Relationship Contract Specification.md` | `IEngineeringRelationship`, `RelationshipCategory`, Direction/Multiplicity/Ownership/Lifecycle/Validation |
| `WP8.2B Validation Contract Specification.md` | `IValidationResult`, rule evaluation, constraint/diagnostic/error/warning collection |
| `WP8.2B Digital Thread Contract Specification.md` | Traceability, evidence, navigation, dependency traversal, impact analysis |
| `WP8.2B Sequence Diagrams.md` | Eight named scenarios — creation, relationship, transition, approval, revision, baseline, traceability, validation |
| `WP8.2B Dependency Rules.md` | Allowed/forbidden dependencies, ownership, layering, composition, factory/registration responsibilities |

## Related Documents

`WP8.2A Engineering Domain Architecture.md` and its eight companion
deliverables; `ADR-0072`–`ADR-0076`; `WP7.0C Engineering Foundation
Contracts.md`; `WP7.2C Requirements Platform Contracts.md`; `WP8.0B
Workspace Contracts.md`; `IEngineeringDocument`/`IEngineeringDocumentStore`
(`src/Tempest.Core/EngineeringData/`).

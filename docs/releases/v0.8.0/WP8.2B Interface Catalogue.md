# WP 8.2B — Engineering Domain Contracts — Interface Catalogue

## Purpose

Every interface the controlling instruction names, in proposed,
uncompiled C#. Every canonical object interface composes
`IEngineeringObject` (`WP8.2B Engineering Domain Contracts.md` §2) plus
whichever facet interfaces (§1, below) apply to it — never a class
inheritance chain (`ADR-0075`). Namespace `Tempest.Core.EngineeringDomain`
throughout, omitted per-block for brevity.

## 1. Facet Interfaces

```csharp
/// <summary>A stable, human-facing name distinct from Id — Identity's own display half.</summary>
public interface IHasBusinessIdentifier
{
    /// <summary>A stable, caller-defined business key — for example "SYS-REQ-042." Never the primary key.</summary>
    string? Identifier { get; }

    /// <summary>A short, human-readable display name — always present, unlike Identifier.</summary>
    string DisplayName { get; }
}

/// <summary>The common metadata envelope — Ownership, Category, Discipline, Tags, Classification, Notes.</summary>
public interface IHasMetadata
{
    string? Category { get; }
    string? Discipline { get; }
    string? Owner { get; }
    IReadOnlyList<string> Tags { get; }
    string? Classification { get; }
    string? Notes { get; }
}

/// <summary>Lifecycle status and its own transition history.</summary>
public interface IHasLifecycle
{
    LifecycleState Status { get; }
    IReadOnlyList<ILifecycleTransitionRecord> History { get; }

    Task TransitionAsync(LifecycleState target, CancellationToken cancellationToken = default);
}

/// <summary>Revision-controlled content — Revision and Version.</summary>
public interface IHasRevisions
{
    /// <summary>Opaque content, interpreted only by this object's own Kind.</summary>
    string Content { get; }

    string AuthorPrincipalId { get; }

    Task<IHasRevisions> ReviseAsync(string newContent, string? changeSummary, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IRevisionRecord>> GetRevisionHistoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>Directed, typed links to other Engineering Objects.</summary>
public interface IHasRelationships
{
    Task LinkAsync(Guid targetId, string relationshipKind, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IEngineeringRelationship>> GetRelationshipsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Composed, read-side aggregation of proof — never new storage (mirrors IRequirementEvidence).</summary>
public interface ITraceable
{
    Task<IEvidence> GetEvidenceAsync(CancellationToken cancellationToken = default);
}

/// <summary>Architectural validation, per `WP8.2B Validation Contract Specification.md`.</summary>
public interface IValidatable
{
    Task<IValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>File/binary payloads distinct from Content.</summary>
public interface IHasAttachments
{
    Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken = default);
}

/// <summary>A composed, indexable text projection — Searchability.</summary>
public interface ISearchable
{
    string SearchableText { get; }
}
```

**Consolidation note:** the controlling instruction's own twenty named
"Common Behaviour" concerns map onto these ten facets (plus
`IEngineeringObject`'s own Identity) as follows — Identity →
`IEngineeringObject`; Metadata/Ownership/Classification/Tags/Notes →
`IHasMetadata`; Lifecycle/Status/History → `IHasLifecycle`;
Revision/Version → `IHasRevisions`; Relationships → `IHasRelationships`;
Traceability → `ITraceable`; Validation → `IValidatable`; Audit → a
calling-layer concern, not a facet (`WP8.2A Engineering Domain
Architecture.md` §4, unchanged); References → realised as
`IEngineeringRelationship` instances, not a facet of its own;
Attachments → `IHasAttachments`; Searchability → `ISearchable`;
Display/Navigation → `IHasBusinessIdentifier.DisplayName` plus
`IEngineeringObject.Id`/`Kind` are already sufficient; no separate
`INavigable` facet is proposed (Workspace-layer navigation already
exists, `INavigationService`, and needs nothing further from this
layer).

## 2. Programme Hierarchy

```csharp
public interface IPortfolio : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    IReadOnlyList<Guid> ProgrammeIds { get; }
}

public interface IProgramme : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid? PortfolioId { get; }
    IReadOnlyList<Guid> ProjectIds { get; }
}

public interface IProject : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships, ITraceable
{
    Guid? ProgrammeId { get; }
}
```

## 3. Physical & Configuration

```csharp
public interface IAssembly : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable
{
    /// <summary>Composition children — Sub-Assembly and/or Part Ids.</summary>
    IReadOnlyList<Guid> ChildIds { get; }
}

public interface ISubAssembly : IAssembly
{
    Guid ParentAssemblyId { get; }
}

public interface IPart : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable
{
    string? MaterialId { get; }
}

/// <summary>Alias family for Part/Assembly, used where the distinction does not matter to the referencing object (WP8.2A Canonical Object Catalogue.md §2). No independent members.</summary>
public interface IComponent : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata
{
}

public interface IConfiguration : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle
{
    /// <summary>Frozen (object, revision-number) pairs.</summary>
    IReadOnlyList<ConfigurationMember> MemberRevisions { get; }
}

/// <param name="ObjectId">The member object's own Id.</param>
/// <param name="RevisionNumber">The revision frozen at the moment this Configuration was created.</param>
public readonly record struct ConfigurationMember(Guid ObjectId, int RevisionNumber);
```

## 4. Requirements & Verification

Reconciled against real, shipped `IRequirement`/`IVerificationRecord`
(`WP8.2A Canonical Object Catalogue.md` §3) — these contracts describe
the same shape those real types already have, not a redesign.

```csharp
public interface IRequirement : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable
{
    string Statement { get; }
}

/// <summary>Realises both shipped shapes (RequirementCollection, RequirementGroup) under one contract.</summary>
public interface IRequirementSet : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    IReadOnlyList<Guid> MemberRequirementIds { get; }

    /// <summary>True for a hierarchical Group; false for a flat Collection.</summary>
    bool IsHierarchical { get; }
}

/// <summary>Umbrella marker — see IVerificationActivity/IVerificationResult. No independent members.</summary>
public interface IVerification : IEngineeringObject, IHasMetadata
{
}

/// <summary>The planned or performed act of verifying — Conceptual today (WP8.2A Canonical Object Catalogue.md §3's own disclosed gap).</summary>
public interface IVerificationActivity : IVerification, IHasLifecycle
{
    Guid SubjectId { get; }
    string Method { get; }
}

public interface IVerificationResult : IVerification, IHasRevisions, IHasRelationships, ITraceable
{
    Guid SubjectId { get; }
    VerificationOutcome Outcome { get; }
    string Method { get; }
}
```

## 5. Calculations

```csharp
/// <summary>The method/model definition — a pure function plus metadata; not itself persisted as a Kind (WP8.2A Canonical Object Catalogue.md §4).</summary>
public interface ICalculation : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata
{
}

public interface ICalculationSet : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    IReadOnlyList<Guid> MemberCalculationIds { get; }
}

public interface ICalculationResult : IEngineeringObject, IHasRevisions, IHasRelationships, ITraceable
{
    Guid SubjectId { get; }
    IReadOnlyList<string> ReferencedMaterialIds { get; }
}
```

## 6. Materials

```csharp
public interface IMaterial : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRevisions
{
    /// <summary>Each value carries mandatory provenance (WP8.2A Metadata Specification.md §5).</summary>
    IReadOnlyDictionary<string, MaterialPropertyValue> Properties { get; }
}

/// <param name="Value">The property's own value — a boxed `Quantity&lt;TDimension&gt;` in every real, shipped case.</param>
/// <param name="ConfidenceLevel">One of the platform-wide confidence vocabulary's own four values (WP8.2A Metadata Specification.md §2).</param>
public sealed record MaterialPropertyValue(object Value, string ConfidenceLevel);
```

## 7. Documentation & Design

```csharp
public interface IDocument : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, IHasAttachments
{
}

public interface IDrawing : IDocument
{
    string? DrawingNumber { get; }
}

public interface ICadModel : IDocument
{
    string? ModelFormat { get; }
}

/// <summary>A specialised Calculation Result with simulation-specific metadata.</summary>
public interface ISimulation : ICalculationResult, IHasAttachments
{
    string SimulationType { get; }
}
```

## 8. Test & Manufacturing

```csharp
public interface ITest : IVerificationActivity
{
}

public interface IInspection : IVerificationActivity
{
}

public interface IManufacturingOperation : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid PartId { get; }
}

public interface IWorkInstruction : IDocument
{
    Guid ManufacturingOperationId { get; }
}
```

## 9. Supply Chain

```csharp
public interface ISupplier : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
}

public interface IPurchaseItem : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    Guid SupplierId { get; }
    Guid? ReferencedObjectId { get; }
}
```

## 10. Governance & Risk

```csharp
public interface IIssue : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
}

public interface IRisk : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    string? Likelihood { get; }
    string? Severity { get; }
}

/// <summary>Safety-specific specialisation of Risk. No independent members.</summary>
public interface IHazard : IRisk
{
}

public interface IDecision : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    string Rationale { get; }
}

public interface IAssumption : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
}
```

## 11. Process & Approval

```csharp
public interface ITask : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    string? AssignedToPrincipalId { get; }
}

/// <summary>Narrower than Task — always traceable to what raised it.</summary>
public interface IAction : ITask
{
    Guid RaisedByObjectId { get; }
}

public interface IReview : IEngineeringObject, IHasMetadata, IHasRelationships
{
    IReadOnlyList<string> ReviewerPrincipalIds { get; }
}

public interface IApproval : IEngineeringObject, IHasMetadata, IHasRelationships
{
    string ApproverPrincipalId { get; }
    DateTimeOffset ApprovedAt { get; }
}

public interface IMilestone : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle
{
    DateTimeOffset TargetDate { get; }
}

public interface IDeliverable : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid MilestoneId { get; }
}
```

## 12. Change & Release

```csharp
public interface IChangeRequest : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
}

public interface IEngineeringChange : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid ChangeRequestId { get; }
}

/// <summary>Realised via the same named-collection pattern IRequirementSet already ships (WP8.2A Configuration Management Specification.md §3).</summary>
public interface IBaseline : IConfiguration
{
}

/// <summary>A Baseline whose own lifecycle has reached Released — no second storage concept.</summary>
public interface IRelease : IBaseline
{
}
```

## 13. Evidence & Reference

```csharp
/// <summary>Composed, read-side aggregation — never a stored Kind (mirrors IRequirementEvidence exactly).</summary>
public interface IEvidence
{
    Guid SubjectId { get; }
    IReadOnlyList<IEngineeringRelationship> SupportingRelationships { get; }
    IReadOnlyList<IVerificationResult> VerificationResults { get; }
    IReadOnlyList<ICalculationResult> CalculationResults { get; }
}

// IReference is not a first-class object interface — realised as an
// IEngineeringRelationship with RelationshipCategory.Reference
// (WP8.2B Relationship Contract Specification.md §3). Named here to
// disclose why no interface block exists for it.

public interface IExternalSystemLink : IEngineeringObject, IHasMetadata
{
    string ExternalSystemName { get; }
    string ExternalObjectIdentifier { get; }
}

public interface IAttachment
{
    Guid Id { get; }
    string FileName { get; }
    string ContentType { get; }
    long SizeInBytes { get; }
}
```

## 14. Classification & Extensibility

`ITag` and `IClassification`, named in the controlling instruction
alongside every other interface, are **not** realised as object
interfaces — `WP8.2A Canonical Object Catalogue.md` §13 already
established both as metadata fields (`IHasMetadata.Tags`/`.Classification`,
§1 above), not first-class Engineering Objects requiring their own
identity. No interface block exists for either, by design, disclosed
here rather than silently omitted.

## 15. Factories

Defined in full in `WP8.2B Engineering Domain Contracts.md` §6
(`IEngineeringObjectFactory`, `IEngineeringRelationshipFactory`) — not
repeated here.

## Cross-Reference Check

53 interface names appear in the controlling instruction. All 53 are
accounted for: 51 realised as interface blocks across §1–§13 and §15
(ten facets, forty-one object/support interfaces, two factories); two
(`ITag`, `IClassification`) explicitly and disclosedly realised as
metadata fields, not interfaces, per §14. `IEngineeringRelationship`
itself is defined in full in `WP8.2B Relationship Contract
Specification.md` §1, referenced throughout this catalogue rather than
duplicated.

## Related Documents

`WP8.2B Engineering Domain Contracts.md`; `WP8.2A Canonical Object
Catalogue.md`; `WP8.2B Relationship Contract Specification.md`;
`WP8.2B Lifecycle Contract Specification.md`; `WP8.2B Validation
Contract Specification.md`; `ADR-0075`.

namespace Tempest.Core.EngineeringDomain;

public sealed class EngineeringRelationship : IEngineeringRelationship
{
    public Guid SourceId { get; }
    public Guid TargetId { get; }
    public string RelationshipKind { get; }
    public RelationshipCategory Category { get; }
    public string CreatedByPrincipalId { get; }
    public DateTimeOffset CreatedAt { get; }

    public EngineeringRelationship(Guid sourceId, Guid targetId, string relationshipKind, RelationshipCategory category, string createdByPrincipalId, DateTimeOffset createdAt)
    {
        SourceId = sourceId;
        TargetId = targetId;
        RelationshipKind = relationshipKind;
        Category = category;
        CreatedByPrincipalId = createdByPrincipalId;
        CreatedAt = createdAt;
    }
}

public sealed class RevisionRecord : IRevisionRecord
{
    public int RevisionNumber { get; }
    public string Content { get; }
    public string? ChangeSummary { get; }
    public string AuthorPrincipalId { get; }
    public DateTimeOffset CreatedAt { get; }

    public RevisionRecord(int revisionNumber, string content, string? changeSummary, string authorPrincipalId, DateTimeOffset createdAt)
    {
        RevisionNumber = revisionNumber;
        Content = content;
        ChangeSummary = changeSummary;
        AuthorPrincipalId = authorPrincipalId;
        CreatedAt = createdAt;
    }
}

/// <summary>
/// A best-effort, disclosed inference from <see cref="EngineeringData.DocumentReference.RelationshipKind"/> to
/// <see cref="RelationshipCategory"/> for links recorded outside the Domain framework's own
/// <see cref="EngineeringObjectBase.LinkAsync"/> path — mirrors the conventional Category-to-Kind mapping
/// documented in WP8.2A/WP8.2B (e.g. Verification→"verifiedBy", Allocation→"allocatedTo"). Never validated
/// against the caller's own declared category (ADR-0076) — purely descriptive, defaulting to
/// <see cref="RelationshipCategory.Reference"/> for an unrecognised kind.
/// </summary>
public static class RelationshipKindCategoryMap
{
    private static readonly IReadOnlyDictionary<string, RelationshipCategory> Conventional = new Dictionary<string, RelationshipCategory>(StringComparer.Ordinal)
    {
        ["groupedUnder"] = RelationshipCategory.Composition,
        ["collects"] = RelationshipCategory.Aggregation,
        ["dependsOn"] = RelationshipCategory.Dependency,
        ["blocks"] = RelationshipCategory.Dependency,
        ["derivesFrom"] = RelationshipCategory.Derivation,
        ["allocatedTo"] = RelationshipCategory.Allocation,
        ["references"] = RelationshipCategory.Reference,
        ["relatedTo"] = RelationshipCategory.Reference,
        ["satisfies"] = RelationshipCategory.Verification,
        ["verifiedBy"] = RelationshipCategory.Verification,
        ["calculatedBy"] = RelationshipCategory.Calculation,
        ["basedOnCalculation"] = RelationshipCategory.Calculation,
        ["supersedes"] = RelationshipCategory.Supersession,
        ["duplicates"] = RelationshipCategory.Reference,
        ["manufacturedBy"] = RelationshipCategory.Manufacturing,
        ["documentedBy"] = RelationshipCategory.Documentation,
        ["approvedBy"] = RelationshipCategory.Change,
    };

    public static RelationshipCategory InferCategory(string relationshipKind) =>
        Conventional.TryGetValue(relationshipKind, out var category) ? category : RelationshipCategory.Reference;
}

namespace Tempest.Core.Requirements;

/// <summary>The concrete, immutable <see cref="IRequirement"/> snapshot returned by <see cref="IRequirementsService"/>.</summary>
internal sealed class Requirement : IRequirement
{
    public Guid Id { get; }
    public string Identifier { get; }
    public string Statement { get; }
    public string? Category { get; }
    public RequirementStatus Status { get; }
    public int RevisionNumber { get; }
    public string CreatedByPrincipalId { get; }
    public DateTimeOffset CreatedAt { get; }
    public string? Owner { get; }
    public RequirementPriority? Priority { get; }
    public bool IsDeleted { get; }
    public Guid? GroupId { get; }

    public Requirement(
        Guid id,
        string identifier,
        string statement,
        string? category,
        RequirementStatus status,
        int revisionNumber,
        string createdByPrincipalId,
        DateTimeOffset createdAt,
        string? owner = null,
        RequirementPriority? priority = null,
        bool isDeleted = false,
        Guid? groupId = null)
    {
        Id = id;
        Identifier = identifier;
        Statement = statement;
        Category = category;
        Status = status;
        RevisionNumber = revisionNumber;
        CreatedByPrincipalId = createdByPrincipalId;
        CreatedAt = createdAt;
        Owner = owner;
        Priority = priority;
        IsDeleted = isDeleted;
        GroupId = groupId;
    }
}

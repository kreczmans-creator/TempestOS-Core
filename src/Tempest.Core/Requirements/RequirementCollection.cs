namespace Tempest.Core.Requirements;

/// <summary>The concrete, immutable <see cref="IRequirementCollection"/> snapshot returned by <see cref="IRequirementsService"/>.</summary>
internal sealed class RequirementCollection : IRequirementCollection
{
    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<Guid> MemberRequirementIds { get; }
    public bool IsDeleted { get; }

    public RequirementCollection(Guid id, string name, IReadOnlyList<Guid> memberRequirementIds, bool isDeleted = false)
    {
        Id = id;
        Name = name;
        MemberRequirementIds = memberRequirementIds;
        IsDeleted = isDeleted;
    }
}

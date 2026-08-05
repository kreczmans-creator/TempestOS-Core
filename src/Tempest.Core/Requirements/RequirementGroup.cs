namespace Tempest.Core.Requirements;

/// <summary>The concrete, immutable <see cref="IRequirementGroup"/> snapshot returned by <see cref="IRequirementsService"/>.</summary>
internal sealed class RequirementGroup : IRequirementGroup
{
    public Guid Id { get; }
    public string Name { get; }
    public Guid? ParentGroupId { get; }
    public bool IsDeleted { get; }

    public RequirementGroup(Guid id, string name, Guid? parentGroupId, bool isDeleted = false)
    {
        Id = id;
        Name = name;
        ParentGroupId = parentGroupId;
        IsDeleted = isDeleted;
    }
}

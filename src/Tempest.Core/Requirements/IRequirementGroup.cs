namespace Tempest.Core.Requirements;

/// <summary>
/// A hierarchical categorisation node — the requirement hierarchy,
/// distinct from <see cref="IRequirementCollection"/>'s own
/// non-hierarchical, purpose-built grouping (<c>WP7.2C Requirements
/// Platform Contracts.md</c> §4).
/// </summary>
public interface IRequirementGroup
{
    Guid Id { get; }
    string Name { get; }

    /// <summary><see langword="null"/> if this is a root group. The live, current value; see <see cref="IRequirementsService.MoveGroupAsync"/> (`WP 9.1A`).</summary>
    Guid? ParentGroupId { get; }

    /// <summary>Whether this group has been soft-deleted (`WP 9.1A`).</summary>
    bool IsDeleted { get; }
}

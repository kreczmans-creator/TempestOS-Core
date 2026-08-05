namespace Tempest.Core.Requirements;

/// <summary>
/// A named, purpose-built set of requirements — a baseline, a release
/// scope, a review package. Owns membership and its own name; owns no
/// data about any member requirement itself (<c>WP7.2C Requirements
/// Platform Contracts.md</c> §3).
/// </summary>
public interface IRequirementCollection
{
    Guid Id { get; }
    string Name { get; }

    /// <summary>Every requirement Id this collection currently contains. Never <see langword="null"/>.</summary>
    IReadOnlyList<Guid> MemberRequirementIds { get; }

    /// <summary>Whether this collection has been soft-deleted (`WP 9.1A`).</summary>
    bool IsDeleted { get; }
}

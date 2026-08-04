namespace Tempest.App.Workspace;

/// <summary>What kind of thing a <see cref="ProjectExplorerNode"/> represents.</summary>
public enum ProjectExplorerNodeType
{
    /// <summary>
    /// A structural label with no backing engineering object — for example
    /// "Groups" or "Collections" (`WP8.0A Navigation Specification.md` §3.1).
    /// </summary>
    Category,

    /// <summary>A hierarchical grouping node — for example a <c>RequirementGroup</c>.</summary>
    Group,

    /// <summary>A cross-cutting membership node — for example a <c>RequirementCollection</c>.</summary>
    Collection,

    /// <summary>A real engineering object — a Requirement, a Material, and so on.</summary>
    Object,
}

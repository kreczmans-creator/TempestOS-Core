namespace Tempest.Core.Requirements;

/// <summary>
/// A requirement's own relative importance — an open, ordered scale,
/// distinct from <see cref="RequirementStatus"/>'s own workflow position.
/// A genuine, disclosed `WP 9.1A` addition: no prior Work Package's own
/// controlling instruction named priority anywhere in the Requirements
/// Platform's own scope.
/// </summary>
public enum RequirementPriority
{
    Low,
    Medium,
    High,
    Critical,
}

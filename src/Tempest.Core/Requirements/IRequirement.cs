namespace Tempest.Core.Requirements;

/// <summary>
/// A single, stated engineering requirement — a first-class Engineering
/// Document. Owns identity, statement, category, and lifecycle status;
/// does not own what satisfies it, verifies it, or allocates it — each
/// is a relationship, never a field here (<c>WP7.2C Requirements
/// Platform Contracts.md</c> §2).
/// </summary>
public interface IRequirement
{
    /// <summary>The underlying <see cref="EngineeringData.IEngineeringDocument"/>'s own stable identity.</summary>
    Guid Id { get; }

    /// <summary>The stable, human-facing business identifier (e.g., <c>"SYS-REQ-042"</c>).</summary>
    string Identifier { get; }

    /// <summary>The requirement's own current statement text — opaque to this framework, uninterpreted.</summary>
    string Statement { get; }

    /// <summary>An open, caller-defined classification. <see langword="null"/> if uncategorised.</summary>
    string? Category { get; }

    /// <summary>The requirement's own current lifecycle status.</summary>
    RequirementStatus Status { get; }

    /// <summary>The current revision number.</summary>
    int RevisionNumber { get; }

    /// <summary>The principal who originally created this requirement — carried forward unchanged across every later revision.</summary>
    string CreatedByPrincipalId { get; }

    /// <summary>When this requirement was originally created — carried forward unchanged across every later revision.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>The principal currently accountable for this requirement, or <see langword="null"/> if unset. Distinct from <see cref="CreatedByPrincipalId"/> — ownership may change; authorship never does (`WP 9.1A`).</summary>
    string? Owner { get; }

    /// <summary>This requirement's own relative importance, or <see langword="null"/> if unset (`WP 9.1A`).</summary>
    RequirementPriority? Priority { get; }

    /// <summary>Whether this requirement has been soft-deleted — never erased, per this platform's own append-only ethos (`WP 9.1A`).</summary>
    bool IsDeleted { get; }

    /// <summary>The requirement group this requirement currently belongs to, or <see langword="null"/> if ungrouped — the live, current value; see <see cref="IRequirementsService.MoveToGroupAsync"/> (`WP 9.1A`).</summary>
    Guid? GroupId { get; }
}

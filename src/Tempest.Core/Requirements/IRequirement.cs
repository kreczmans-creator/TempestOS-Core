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
}

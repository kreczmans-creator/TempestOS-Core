using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace;

/// <summary>
/// One live Project's own health, as the Engineering Cockpit computes it
/// (`ADR-0155`): the Cockpit's own five-discipline rollup
/// (<see cref="EngineeringCockpit.Health"/>) applied to only the objects
/// that Project owns, plus the Project's own blocked-item and
/// overdue-action counts under the same definitions
/// <see cref="EngineeringCockpit.BlockedItems"/> and
/// <see cref="EngineeringCockpit.OverdueActions"/> use workspace-wide.
/// RAG is <see cref="EngineeringHealthStatus"/> — no second vocabulary.
/// </summary>
/// <param name="ProjectId">The Project's own engineering-object Id.</param>
/// <param name="Identifier">The Project's own business identifier (for example <c>P-0027</c>), or <see langword="null"/> if unset.</param>
/// <param name="DisplayName">The Project's own display name.</param>
/// <param name="Status">The Project's own current lifecycle state.</param>
/// <param name="Health">The overall rollup — Blocked if any discipline is; else Attention if any is; else Unknown if every one is; else Healthy.</param>
/// <param name="RequirementsStatus">The Requirements discipline's own status over this Project's requirements.</param>
/// <param name="CalculationStatus">The Calculations discipline's own status over this Project's calculations.</param>
/// <param name="VerificationStatus">The Verification discipline's own status over this Project's activities.</param>
/// <param name="DocumentationStatus">The Documentation discipline's own status over this Project's documents.</param>
/// <param name="ManufacturingStatus">The Manufacturing discipline's own status over this Project's manufacturing objects.</param>
/// <param name="HealthScoreDisplay">The score text, worded exactly as <see cref="EngineeringCockpit.HealthScoreDisplay"/>.</param>
/// <param name="BlockedItemCount">How many of <see cref="EngineeringCockpit.BlockedItems"/> belong to this Project.</param>
/// <param name="OverdueActionCount">How many of <see cref="EngineeringCockpit.OverdueActions"/> belong to this Project.</param>
public sealed record CockpitProjectHealth(
    Guid ProjectId,
    string? Identifier,
    string DisplayName,
    LifecycleState Status,
    EngineeringHealthStatus Health,
    EngineeringHealthStatus RequirementsStatus,
    EngineeringHealthStatus CalculationStatus,
    EngineeringHealthStatus VerificationStatus,
    EngineeringHealthStatus DocumentationStatus,
    EngineeringHealthStatus ManufacturingStatus,
    string HealthScoreDisplay,
    int BlockedItemCount,
    int OverdueActionCount)
{
    /// <summary>The label a shell surface shows for this Project — identifier and name together when both exist, the same form <see cref="Projects.ProjectSummary.Label"/> uses.</summary>
    public string Label => string.IsNullOrWhiteSpace(Identifier) ? DisplayName : $"{Identifier} {DisplayName}";
}

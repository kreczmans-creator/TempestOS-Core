using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Requirements;

/// <summary>
/// Requirements-specific validation — `WP 9.1A`'s own "Requirement
/// validation" scope. Reuses <see cref="IValidationResult"/>/
/// <see cref="IValidationDiagnostic"/> (`Tempest.Core.EngineeringDomain`,
/// `WP8.2B`) for its own result *shape* only — genuinely type-agnostic,
/// unlike <see cref="IValidationRule"/>, which is scoped to
/// <see cref="IEngineeringObject"/> and therefore cannot itself validate
/// an <see cref="IRequirement"/> (confirmed directly: <c>IRequirement</c>
/// carries no such base). A new, small, Requirements-scoped contract is
/// the correct, reuse-respecting answer here — the same "reuse the result
/// shape, not a rule interface that structurally does not fit" reasoning
/// this Work Package's own Search decision already applies (see
/// `WP9.1A Implementation Report.md`).
/// </summary>
public interface IRequirementValidationService
{
    /// <summary>
    /// Validates one requirement: duplicate-identifier confirmation
    /// (defence-in-depth — <see cref="IRequirementsService.CreateAsync"/>
    /// already prevents this at write time), orphan detection (no
    /// outgoing relationship of any kind — disclosed: incoming references
    /// are not discoverable, see remarks), missing-verification
    /// (<c>Allocated</c>/<c>Verified</c>/<c>Satisfied</c> status with no
    /// recorded <c>verifiedBy</c> relationship), missing-allocation (same
    /// three statuses, zero <see cref="RequirementRelationshipKinds.AllocatedTo"/>
    /// link), and relationship-kind advisory (a kind outside this
    /// Platform's own named set — a warning, never an error, since
    /// relationship kinds remain platform-wide open by design, `ADR-0073`).
    /// Every check reads only <see cref="IRequirementsService.GetRelationshipsAsync"/>
    /// (never <see cref="IRequirementsService.GetEvidenceAsync"/>), so this
    /// method is never permission-gated — a validation read must stay
    /// available to any principal that can reach a requirement at all.
    /// </summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task<IValidationResult> ValidateAsync(Guid requirementId, CancellationToken cancellationToken = default);
}

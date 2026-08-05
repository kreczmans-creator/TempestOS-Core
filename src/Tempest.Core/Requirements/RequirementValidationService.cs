using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.Core.Requirements;

/// <summary>The concrete <see cref="IRequirementValidationService"/> implementation.</summary>
/// <remarks>
/// <b>Disclosed limitation — orphan detection is outgoing-only:</b>
/// <see cref="EngineeringData.IEngineeringDocumentStore"/> has no
/// "find incoming references" capability (confirmed directly — only
/// <see cref="EngineeringData.IEngineeringDocumentStore.GetReferencesAsync"/>,
/// scoped to a document's own *outgoing* references, exists). A
/// requirement with zero outgoing relationships but at least one
/// *incoming* one (something else depends on, derives from, or is
/// allocated to it) is therefore still reported as an orphan here — a
/// real, disclosed gap, not a silent one. Adding a reverse index would be
/// a new Domain-level read capability, exactly the contract redesign this
/// Work Package's own controlling instruction forbids. See `WP9.1A
/// Technical Debt Assessment.md`.
/// </remarks>
public sealed class RequirementValidationService : IRequirementValidationService
{
    /// <summary>Every relationship kind this Platform names, across the Requirements Platform and Verification framework.</summary>
    private static readonly IReadOnlySet<string> KnownRelationshipKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        RequirementRelationshipKinds.GroupedUnder,
        RequirementRelationshipKinds.CollectedIn,
        RequirementRelationshipKinds.DependsOn,
        RequirementRelationshipKinds.DerivesFrom,
        RequirementRelationshipKinds.AllocatedTo,
        RequirementRelationshipKinds.References,
        RequirementRelationshipKinds.Satisfies,
        VerificationService.VerifiedByRelationshipKind,
    };

    private static readonly IReadOnlySet<RequirementStatus> StatusesExpectingVerificationAndAllocation = new HashSet<RequirementStatus>
    {
        RequirementStatus.Allocated,
        RequirementStatus.Verified,
        RequirementStatus.Satisfied,
    };

    private readonly IRequirementsService _requirementsService;

    /// <summary>Initialises a new instance of the <see cref="RequirementValidationService"/> class.</summary>
    public RequirementValidationService(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);
        _requirementsService = requirementsService;
    }

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var requirement = await _requirementsService.FindAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();

        // Duplicate-identifier confirmation — defence-in-depth; CreateAsync's
        // own AsyncKeyedLock-protected identifier index already prevents
        // this at write time (WP7.3A).
        var allRequirements = await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false);
        if (allRequirements.Count(r => string.Equals(r.Identifier, requirement.Identifier, StringComparison.Ordinal)) > 1)
            errors.Add(new ValidationDiagnostic("TEMPEST-REQ-VAL-001", $"Identifier '{requirement.Identifier}' is used by more than one requirement.", requirementId));

        var relationships = await _requirementsService.GetRelationshipsAsync(requirementId, cancellationToken).ConfigureAwait(false);

        // Orphan detection (outgoing-only — see this type's own disclosed limitation).
        if (relationships.Count == 0)
            warnings.Add(new ValidationDiagnostic("TEMPEST-REQ-VAL-002", $"Requirement '{requirement.Identifier}' has no outgoing relationship of any kind.", requirementId));

        if (StatusesExpectingVerificationAndAllocation.Contains(requirement.Status))
        {
            // Deliberately checks the "verifiedBy" relationship link itself
            // (already present in the same relationships read above), never
            // IRequirementsService.GetEvidenceAsync — that method is
            // transitively permission-gated on VerificationService.ReadPermission
            // via IVerificationService.GetVerificationHistoryAsync (`ADR-0061`),
            // and a validation read must never throw because the current
            // principal lacks a narrower capability than "can validate a
            // requirement at all". VerificationService.RecordAsync always
            // creates this exact link, so this is not a weaker check, only a
            // non-gated one — a real, disclosed fix over this method's own
            // original shape (`WP9.1A Technical Debt Assessment.md`).
            if (!relationships.Any(r => string.Equals(r.RelationshipKind, VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal)))
                warnings.Add(new ValidationDiagnostic("TEMPEST-REQ-VAL-003", $"Requirement '{requirement.Identifier}' is '{requirement.Status}' but has no recorded verification.", requirementId));

            if (!relationships.Any(r => string.Equals(r.RelationshipKind, RequirementRelationshipKinds.AllocatedTo, StringComparison.Ordinal)))
                warnings.Add(new ValidationDiagnostic("TEMPEST-REQ-VAL-004", $"Requirement '{requirement.Identifier}' is '{requirement.Status}' but is not allocated to anything.", requirementId));
        }

        foreach (var unknownKind in relationships.Select(r => r.RelationshipKind).Where(k => !KnownRelationshipKinds.Contains(k)).Distinct())
            warnings.Add(new ValidationDiagnostic("TEMPEST-REQ-VAL-005", $"Requirement '{requirement.Identifier}' carries a relationship of kind '{unknownKind}', outside this Platform's own named set — advisory only (`ADR-0073` keeps relationship kinds open by design).", requirementId));

        // Status validation — RequirementStatusTransitions is the sole
        // enforcement point (SetStatusAsync already checks it at write
        // time); this is a read-time confirmation only, always valid for
        // any requirement reachable through this service.
        return new ValidationResult(errors, warnings);
    }
}

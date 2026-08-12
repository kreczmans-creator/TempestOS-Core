using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;
using IRequirement = Tempest.Core.Requirements.IRequirement;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// The Requirements discipline's own Engineering Cockpit read-model —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s
/// own previous Requirements-specific members, unmodified in behaviour.
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), declaring
/// only the two dependencies it actually needs, never DI-registered,
/// never referencing <see cref="EngineeringCockpit"/> or any sibling
/// discipline collaborator back.
/// </summary>
internal sealed class RequirementsCockpitReadModel
{
    private readonly IRequirementsService _requirementsService;
    private readonly IRequirementValidationService _requirementValidationService;

    /// <summary>Initialises a new instance of the <see cref="RequirementsCockpitReadModel"/> class.</summary>
    /// <param name="requirementsService">The Requirements Framework's own service this read-model queries directly.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service this read-model queries directly.</param>
    public RequirementsCockpitReadModel(IRequirementsService requirementsService, IRequirementValidationService requirementValidationService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);

        _requirementsService = requirementsService;
        _requirementValidationService = requirementValidationService;
    }

    /// <summary>Gets every live (non-deleted) Requirement — a real read.</summary>
    public IReadOnlyList<IRequirement> LiveRequirements =>
        _requirementsService.ListAsync().GetAwaiter().GetResult().Where(r => !r.IsDeleted).ToList();

    /// <summary>Gets the number of live Requirements — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveRequirements.Count;

    /// <summary>Gets the number of live Requirements that are <see cref="RequirementStatus.Reviewed"/>.</summary>
    public int InReviewCount => LiveRequirements.Count(r => r.Status == RequirementStatus.Reviewed);

    /// <summary>
    /// Gets every live requirement's own <see cref="IRequirementValidationService"/>
    /// result — the shared basis for <see cref="Status"/> and
    /// <see cref="OutstandingActions"/>, computed once per read so both
    /// stay consistent with each other.
    /// </summary>
    /// <remarks>
    /// <b>Defensive, not currently load-bearing:</b> the concrete
    /// <see cref="RequirementValidationService"/> reads only
    /// <see cref="IRequirementsService.GetRelationshipsAsync"/> today,
    /// never the permission-gated <see cref="IRequirementsService.GetEvidenceAsync"/>,
    /// so <see cref="PermissionDeniedException"/> is not expected here in
    /// practice. The guard remains because <see cref="IRequirementValidationService"/>
    /// is an interface, not a sealed contract to this one implementation —
    /// a passive status dashboard must never throw because some future
    /// implementation's own validation needs a narrower capability than
    /// "can view the Cockpit at all"; a requirement whose own validation
    /// cannot be evaluated for that reason is silently excluded from this
    /// read (never counted as a false "no findings"), rather than
    /// crashing every other card this property feeds.
    /// </remarks>
    private IReadOnlyList<IValidationResult> LiveRequirementValidationResults
    {
        get
        {
            var results = new List<IValidationResult>();

            foreach (var requirement in LiveRequirements)
            {
                try
                {
                    results.Add(_requirementValidationService.ValidateAsync(requirement.Id).GetAwaiter().GetResult());
                }
                catch (PermissionDeniedException)
                {
                    // See this property's own remarks.
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Gets the number of live requirements with at least one recorded
    /// verification — a real read via <see cref="IRequirementsService.GetRelationshipsAsync"/>
    /// for a <see cref="Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind"/>
    /// relationship, the existing Digital Thread read, never a new
    /// traversal.
    /// </summary>
    private int VerifiedRequirementCount =>
        LiveRequirements.Count(r => _requirementsService.GetRelationshipsAsync(r.Id).GetAwaiter().GetResult()
            .Any(reference => string.Equals(reference.RelationshipKind, Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal)));

    /// <summary>Gets the number of live requirements with at least one <see cref="RequirementRelationshipKinds.AllocatedTo"/> relationship.</summary>
    private int AllocatedRequirementCount =>
        LiveRequirements.Count(r => _requirementsService.GetRelationshipsAsync(r.Id).GetAwaiter().GetResult()
            .Any(reference => string.Equals(reference.RelationshipKind, RequirementRelationshipKinds.AllocatedTo, StringComparison.Ordinal)));

    /// <summary>Gets the total count of Requirements validation findings (errors plus warnings) across every live requirement — the Cockpit's own "Outstanding Actions" KPI.</summary>
    public int OutstandingActions => LiveRequirementValidationResults.Sum(r => r.Errors.Count + r.Warnings.Count);

    /// <summary>
    /// Gets the Requirements discipline's own status: <see cref="EngineeringHealthStatus.Unknown"/>
    /// if no live Requirement exists yet; <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any live requirement's own validation result carries an error;
    /// <see cref="EngineeringHealthStatus.Attention"/> if any carries a
    /// warning with no error present; <see cref="EngineeringHealthStatus.Healthy"/>
    /// otherwise.
    /// </summary>
    public EngineeringHealthStatus Status
    {
        get
        {
            var live = LiveRequirements;
            if (live.Count == 0)
                return EngineeringHealthStatus.Unknown;

            var results = LiveRequirementValidationResults;

            if (results.Any(r => r.Errors.Count > 0))
                return EngineeringHealthStatus.Blocked;

            return results.Any(r => r.Warnings.Count > 0)
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Requirements discipline's own dedicated KPI card set:
    /// Total, Draft, Review, Approved, Released, Verification Coverage,
    /// Allocation Coverage, Requirement Health, and Outstanding Actions.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed status-name mapping:</b> this platform's own
    /// <see cref="RequirementStatus"/> has no <c>"Released"</c> value —
    /// this card set's own "Released" card reports the
    /// <see cref="RequirementStatus.Satisfied"/> count, the closest
    /// existing terminal-success status.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var live = LiveRequirements;
            var total = live.Count;
            var counts = live.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

            int CountOf(RequirementStatus status) => counts.TryGetValue(status, out var count) ? count : 0;

            return
            [
                new("Total Requirements", total.ToString(), IsPlaceholder: false),
                new("Draft", CountOf(RequirementStatus.Draft).ToString(), IsPlaceholder: false),
                new("Review", InReviewCount.ToString(), IsPlaceholder: false),
                new("Approved", CountOf(RequirementStatus.Approved).ToString(), IsPlaceholder: false),
                new("Released", CountOf(RequirementStatus.Satisfied).ToString(), IsPlaceholder: false),
                new("Verification Coverage", CockpitFormatting.FormatCoverage(VerifiedRequirementCount, total), IsPlaceholder: false, CockpitFormatting.PercentOf(VerifiedRequirementCount, total)),
                new("Allocation Coverage", CockpitFormatting.FormatCoverage(AllocatedRequirementCount, total), IsPlaceholder: false, CockpitFormatting.PercentOf(AllocatedRequirementCount, total)),
                new("Requirement Health", Status.ToString(), IsPlaceholder: false),
                new("Outstanding Actions", OutstandingActions.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>Gets this discipline's own "What Needs Attention" contribution — a base entry, plus a conditional second entry when <see cref="OutstandingActions"/> is non-zero.</summary>
    public IReadOnlyList<CockpitAttentionItem> GetAttentionItems()
    {
        var items = new List<CockpitAttentionItem>
        {
            LiveRequirements.Count > 0
                ? new("Requirements Management is live", $"{LiveRequirements.Count} Requirement(s) registered - the Project Explorer's own Requirements area and the Engineering Cockpit's own Requirements KPIs reflect real Requirements Framework data (WP 9.1A).")
                : new("No Requirements registered yet", "The Requirements Management area has no live Requirement yet - this is expected, not a defect."),
        };

        if (OutstandingActions > 0)
        {
            items.Add(new(
                "Requirements need attention",
                $"{OutstandingActions} outstanding Requirements validation finding(s) across {LiveRequirements.Count} live requirement(s) - duplicate identifiers, orphans, missing verification/allocation, or advisory relationship kinds. See the Requirements area's own Property Inspector for detail."));
        }

        return items;
    }

    /// <summary>Gets this discipline's own "Open Actions" triage entry, or <see langword="null"/> if nothing is currently outstanding.</summary>
    public CockpitActionItem? GetOpenActionItem() =>
        OutstandingActions > 0
            ? new($"Triage {OutstandingActions} outstanding Requirements validation finding(s)", "Systems Engineer")
            : null;

    /// <summary>Gets this discipline's own "Blocked Items" contribution — one message per live requirement with a validation error.</summary>
    public IReadOnlyList<string> GetBlockedMessages()
    {
        var items = new List<string>();

        // Re-validates per requirement directly (mirroring
        // LiveRequirementValidationResults's own identical try/catch-per-
        // item shape) rather than correlating back from that property's
        // own pre-aggregated list — IValidationResult carries no
        // ObjectId of its own, and a skipped PermissionDeniedException
        // would otherwise misalign a positional correlation.
        foreach (var requirement in LiveRequirements)
        {
            try
            {
                var result = _requirementValidationService.ValidateAsync(requirement.Id).GetAwaiter().GetResult();
                if (result.Errors.Count > 0)
                    items.Add($"Requirement '{requirement.Identifier}' has a validation error blocking approval.");
            }
            catch (PermissionDeniedException)
            {
                // See LiveRequirementValidationResults's own remarks — silently excluded, never counted as a false "not blocked."
            }
        }

        return items;
    }
}

using Tempest.Workspace;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;
using IRequirement = Tempest.Core.Requirements.IRequirement;

namespace Tempest.Workspace.Requirements;

/// <summary>
/// The Requirements discipline's own Engineering Cockpit read-model —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s
/// own previous Requirements-specific members, unmodified in behaviour.
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), declaring
/// only the dependencies it actually needs, never DI-registered,
/// never referencing <see cref="EngineeringCockpit"/> or any sibling
/// discipline collaborator back.
/// </summary>
/// <remarks>
/// <b>`WP 18.1A-R1`.</b> Supersedes `WP-E`'s own <see cref="CockpitReadScope"/>-backed
/// memoisation (a lazy cell, computed once per open <c>Begin()</c> pass
/// but still blocked on synchronously outside one — the exact shape
/// `TD-108`/`TD-118` found) with an eager <see cref="LoadAsync"/>: every
/// persistence-backed read this discipline needs — the live requirement
/// listing, one validation pass per requirement, and one relationships
/// read per requirement — happens there, awaited once per Cockpit render
/// (<see cref="EngineeringCockpit.PrimeAsync"/>). Every property below is
/// now a pure, in-memory read of what <see cref="LoadAsync"/> last
/// loaded — still computed once per render, never per property, but
/// without a single blocking call left in this file's own source.
/// </remarks>
internal sealed class RequirementsCockpitReadModel
{
    private readonly IRequirementsService _requirementsService;
    private readonly IRequirementValidationService _requirementValidationService;
    private IReadOnlyList<IRequirement> _liveRequirements = [];
    private IReadOnlyList<(Guid RequirementId, IValidationResult Result)> _validationByRequirement = [];
    private IReadOnlyDictionary<Guid, IReadOnlyList<DocumentReference>> _relationshipsByRequirement = new Dictionary<Guid, IReadOnlyList<DocumentReference>>();

    /// <summary>Initialises a new instance of the <see cref="RequirementsCockpitReadModel"/> class.</summary>
    /// <param name="requirementsService">The Requirements Framework's own service this read-model queries directly.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service this read-model queries directly.</param>
    public RequirementsCockpitReadModel(
        IRequirementsService requirementsService,
        IRequirementValidationService requirementValidationService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);

        _requirementsService = requirementsService;
        _requirementValidationService = requirementValidationService;
    }

    /// <summary>Loads every live requirement, its own validation result, and its own outgoing relationships — the three reads every property below is derived from.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var requirements = await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false);
        var live = requirements.Where(r => !r.IsDeleted).ToList();
        _liveRequirements = live;

        _validationByRequirement = await ReadValidationResultsAsync(live, cancellationToken).ConfigureAwait(false);
        _relationshipsByRequirement = await ReadRelationshipsAsync(live, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A view of this read-model over only the requirements that belong
    /// to the engineering objects <paramref name="includesObject"/> admits
    /// (`ADR-0151`, project health). A requirement is not itself a
    /// parented engineering object, so its membership is the rule
    /// <see cref="Projects.ProjectRequirementRegister"/> already states:
    /// it is in a project when at least one of its own recorded
    /// relationships targets an object that is. Applied here to the
    /// relationships <see cref="LoadAsync"/> already read — the same
    /// class over a filtered copy, no second load, no second rule.
    /// </summary>
    public RequirementsCockpitReadModel ScopedTo(Func<Guid, bool> includesObject)
    {
        ArgumentNullException.ThrowIfNull(includesObject);

        var included = _liveRequirements
            .Where(r => _relationshipsByRequirement.TryGetValue(r.Id, out var references) && references.Any(reference => includesObject(reference.TargetDocumentId)))
            .Select(r => r.Id)
            .ToHashSet();

        return new RequirementsCockpitReadModel(_requirementsService, _requirementValidationService)
        {
            _liveRequirements = _liveRequirements.Where(r => included.Contains(r.Id)).ToList(),
            _validationByRequirement = _validationByRequirement.Where(v => included.Contains(v.RequirementId)).ToList(),
            _relationshipsByRequirement = _relationshipsByRequirement.Where(kv => included.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
        };
    }

    /// <summary>Gets every live (non-deleted) Requirement — loaded by <see cref="LoadAsync"/>.</summary>
    public IReadOnlyList<IRequirement> LiveRequirements => _liveRequirements;

    /// <summary>Gets every distinct engineering object any live requirement's own recorded relationships target — the ids a project-scoping caller resolves membership for before calling <see cref="ScopedTo"/>.</summary>
    public IReadOnlyCollection<Guid> RelatedObjectIds =>
        _relationshipsByRequirement.Values.SelectMany(references => references.Select(reference => reference.TargetDocumentId)).ToHashSet();

    /// <summary>Gets the number of live Requirements — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveRequirements.Count;

    /// <summary>Gets the number of live Requirements that are <see cref="RequirementStatus.Reviewed"/>.</summary>
    public int InReviewCount => LiveRequirements.Count(r => r.Status == RequirementStatus.Reviewed);

    /// <summary>
    /// Gets every live requirement's own <see cref="IRequirementValidationService"/>
    /// result — the shared basis for <see cref="Status"/> and
    /// <see cref="OutstandingActions"/>, loaded once so both stay
    /// consistent with each other.
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
    private IReadOnlyList<(Guid RequirementId, IValidationResult Result)> LiveRequirementValidationResults =>
        _validationByRequirement;

    /// <summary>
    /// The one validation pass behind <see cref="LiveRequirementValidationResults"/>
    /// — keyed by requirement so <see cref="GetBlockedMessages"/> can name
    /// the requirement a finding belongs to without re-validating it, and
    /// without the positional correlation that property's own remarks
    /// correctly refused (`WP-E`).
    /// </summary>
    private async Task<IReadOnlyList<(Guid RequirementId, IValidationResult Result)>> ReadValidationResultsAsync(
        IReadOnlyList<IRequirement> requirements, CancellationToken cancellationToken)
    {
        var results = new List<(Guid, IValidationResult)>();

        foreach (var requirement in requirements)
        {
            try
            {
                results.Add((requirement.Id, await _requirementValidationService.ValidateAsync(requirement.Id, cancellationToken).ConfigureAwait(false)));
            }
            catch (PermissionDeniedException)
            {
                // See LiveRequirementValidationResults's own remarks.
            }
        }

        return results;
    }

    /// <summary>
    /// Every live requirement's own outgoing relationships, loaded once
    /// (`WP-E`) — <see cref="VerifiedRequirementCount"/> and
    /// <see cref="AllocatedRequirementCount"/> ask two different questions
    /// of the same read, and <c>KpiCards</c> asks each of them twice.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentReference>>> ReadRelationshipsAsync(
        IReadOnlyList<IRequirement> requirements, CancellationToken cancellationToken)
    {
        var relationships = new Dictionary<Guid, IReadOnlyList<DocumentReference>>();

        foreach (var requirement in requirements)
            relationships[requirement.Id] = await _requirementsService.GetRelationshipsAsync(requirement.Id, cancellationToken).ConfigureAwait(false);

        return relationships;
    }

    /// <summary>
    /// Gets the number of live requirements with at least one recorded
    /// verification — a real read via <see cref="IRequirementsService.GetRelationshipsAsync"/>
    /// for a <see cref="Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind"/>
    /// relationship, the existing Digital Thread read, never a new
    /// traversal.
    /// </summary>
    private int VerifiedRequirementCount =>
        _relationshipsByRequirement.Values.Count(references => references
            .Any(reference => string.Equals(reference.RelationshipKind, Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal)));

    /// <summary>Gets the number of live requirements with at least one <see cref="RequirementRelationshipKinds.AllocatedTo"/> relationship.</summary>
    private int AllocatedRequirementCount =>
        _relationshipsByRequirement.Values.Count(references => references
            .Any(reference => string.Equals(reference.RelationshipKind, RequirementRelationshipKinds.AllocatedTo, StringComparison.Ordinal)));

    /// <summary>Gets the total count of Requirements validation findings (errors plus warnings) across every live requirement — the Cockpit's own "Outstanding Actions" KPI.</summary>
    public int OutstandingActions => LiveRequirementValidationResults.Sum(r => r.Result.Errors.Count + r.Result.Warnings.Count);

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

            if (results.Any(r => r.Result.Errors.Count > 0))
                return EngineeringHealthStatus.Blocked;

            return results.Any(r => r.Result.Warnings.Count > 0)
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets every live requirement's own count, grouped by
    /// <see cref="RequirementStatus"/> — the exact grouping
    /// <see cref="KpiCards"/> already computes internally, exposed here so
    /// a consumer that needs every bucket (not just the four
    /// <see cref="KpiCards"/> names as its own dedicated card) reads the
    /// identical figures the Cockpit itself shows, rather than
    /// recomputing its own count from <see cref="LiveRequirements"/> and
    /// risking the two silently drifting apart (`WP 18.3A` Dashboard
    /// Export).
    /// </summary>
    public IReadOnlyDictionary<RequirementStatus, int> StatusCounts =>
        LiveRequirements.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

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
            var counts = StatusCounts;

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

        // `WP-E`: consumes the one keyed validation pass rather than
        // re-validating every requirement a second time. The correlation
        // hazard that previously justified re-validating — IValidationResult
        // carries no ObjectId, so a PermissionDeniedException skipping an
        // entry would misalign a positional match — is gone, because the
        // pass is now keyed by requirement Id rather than by position. A
        // requirement whose validation was skipped is simply absent from
        // the map, and is silently excluded here exactly as before, never
        // counted as a false "not blocked."
        var validation = LiveRequirementValidationResults.ToDictionary(r => r.RequirementId, r => r.Result);

        foreach (var requirement in LiveRequirements)
        {
            if (validation.TryGetValue(requirement.Id, out var result) && result.Errors.Count > 0)
                items.Add($"Requirement '{requirement.Identifier}' has a validation error blocking approval.");
        }

        return items;
    }
}

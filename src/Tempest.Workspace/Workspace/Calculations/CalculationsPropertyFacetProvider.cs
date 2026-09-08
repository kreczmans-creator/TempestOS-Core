using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Supplies the Property Inspector's own real facets for one Calculations
/// Kind (<c>"Calculation"</c>/<c>"CalculationSet"</c>/<c>"CalculationTemplate"</c>)
/// — the third <see cref="IPropertyFacetProvider"/> backed by a real
/// Engineering discipline, after Mechanical's (`WP 9.0A`) and Requirements'
/// (`WP 9.1A`). For a <c>"Calculation"</c>: every generic facet
/// <see cref="Mechanical.MechanicalPropertyFacetProvider"/> already reads
/// via the same casts (Identity/Metadata/Lifecycle/Revisions/Parent/
/// Deletable), plus this Work Package's own additions — Assumptions/
/// Constraints (from the executing Template's own registered
/// <see cref="CalculationMetadata"/>), Safety Factor and Latest Result
/// (from <see cref="CalculationRecordReader"/>), Result History count, and
/// Digital Thread links, read via <see cref="IHasRelationships.GetRelationshipsAsync"/>/
/// <see cref="EngineeringDomainContext.RelationshipRepository"/> directly —
/// never <see cref="ITraceable.GetEvidenceAsync"/>, which honestly resolves
/// empty for every Calculation today (no concrete <see cref="ICalculationResult"/>
/// implementation exists anywhere in the platform — a disclosed,
/// pre-existing gap, not introduced here, mirroring
/// <see cref="Requirements.RequirementsPropertyFacetProvider"/>'s own
/// identical, already-disclosed treatment of the same gap for Verification).
/// </summary>
public sealed class CalculationsPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;
    private readonly CalculationTemplateRegistry _templateRegistry;

    /// <summary>Initialises a new instance of the <see cref="CalculationsPropertyFacetProvider"/> class.</summary>
    /// <param name="kind">The single object Kind this provider supplies facets for.</param>
    public CalculationsPropertyFacetProvider(string kind, EngineeringDomainContext context, CalculationTemplateRegistry templateRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(templateRegistry);

        Kind = kind;
        _context = context;
        _templateRegistry = templateRegistry;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this provider's own <see cref="Kind"/>.</exception>
    public Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(Kind, "CalculationTemplate", StringComparison.Ordinal))
            return GetTemplateFacetsAsync(objectId);

        return GetDomainObjectFacetsAsync(objectId, cancellationToken);
    }

    private Task<IReadOnlyList<PropertyFacet>> GetTemplateFacetsAsync(Guid nodeId)
    {
        var template = _templateRegistry.FindByNodeId(nodeId)
            ?? throw new ArgumentException($"'{nodeId}' is not a known Calculation Template.", nameof(nodeId));

        var facets = new List<PropertyFacet>
        {
            new("Id", nodeId.ToString(), PropertyFacetKind.Identity),
            new("Kind", "CalculationTemplate", PropertyFacetKind.Identity),
            new("Name", template.Metadata.Name, PropertyFacetKind.Identity),
            new("Calculation Id", template.CalculationId, PropertyFacetKind.Identity),
        };

        if (template.Metadata.Category is { } category)
            facets.Add(new("Category", category, PropertyFacetKind.DisciplineSpecific));

        if (template.Metadata.Description is { } description)
            facets.Add(new("Description", description, PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Assumptions",
            template.Metadata.Assumptions.Count > 0 ? string.Join("; ", template.Metadata.Assumptions.Select(a => a.Description)) : "(none)",
            PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Constraints",
            template.Metadata.Constraints.Count > 0 ? string.Join("; ", template.Metadata.Constraints.Select(c => c.Description)) : "(none)",
            PropertyFacetKind.DisciplineSpecific));

        IReadOnlyList<PropertyFacet> result = facets;
        return Task.FromResult(result);
    }

    private async Task<IReadOnlyList<PropertyFacet>> GetDomainObjectFacetsAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var facets = new List<PropertyFacet>
        {
            new("Id", target.Id.ToString(), PropertyFacetKind.Identity),
            new("Kind", target.Kind, PropertyFacetKind.Identity),
        };

        if (target is IHasBusinessIdentifier identity)
        {
            facets.Add(new("Name", identity.DisplayName, PropertyFacetKind.Identity));

            if (identity.Identifier is { } engineeringIdentifier)
                facets.Add(new("Engineering Identifier", engineeringIdentifier, PropertyFacetKind.Identity));
        }

        if (target is IHasMetadata metadata)
        {
            if (metadata.Owner is { } owner)
                facets.Add(new("Owner", owner, PropertyFacetKind.Provenance));

            if (metadata.Discipline is { } discipline)
                facets.Add(new("Discipline", discipline, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Category is { } category)
                facets.Add(new("Category", category, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Tags.Count > 0)
                facets.Add(new("Tags", string.Join(", ", metadata.Tags), PropertyFacetKind.DisciplineSpecific));

            if (metadata.Notes is { } notes)
                facets.Add(new("Description / Notes", notes, PropertyFacetKind.DisciplineSpecific));
        }

        // Status/Approval — IHasLifecycle, via the existing LifecycleTransitionTable.
        // "Approval State" reuses this same facet — no IApproval/IApprovalGate
        // implementation exists anywhere in the platform (a disclosed,
        // pre-existing gap; see this class's own remarks).
        if (target is IHasLifecycle lifecycle)
        {
            facets.Add(new("Status", lifecycle.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
            facets.Add(new(
                "Approved",
                lifecycle.Status is LifecycleState.Approved or LifecycleState.Released ? "Yes" : "No",
                PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IHasRevisions revisions)
        {
            facets.Add(new("Revision", target.CurrentRevisionNumber.ToString(), PropertyFacetKind.Revision));
            facets.Add(new("Last Revised By", revisions.AuthorPrincipalId, PropertyFacetKind.Provenance));
        }

        if (target is IHasParent hasParent)
            facets.Add(new("Parent", hasParent.ParentId?.ToString() ?? "(top level)", PropertyFacetKind.Relationship));

        if (target is IDeletable { IsDeleted: true })
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        if (target is ICalculationSet set)
            facets.Add(new("Members", set.MemberCalculationIds.Count.ToString(), PropertyFacetKind.DisciplineSpecific));

        if (target is ICalculation)
            await AddCalculationEvidenceFacetsAsync(objectId, facets, cancellationToken).ConfigureAwait(false);

        return facets;
    }

    /// <summary>
    /// Adds this Work Package's own Calculation-specific facets: Assumptions/
    /// Constraints (from the last-executed Template's own registered
    /// <see cref="CalculationMetadata"/>), Safety Factor and Latest Result
    /// (from <see cref="CalculationRecordReader"/>), Result History count,
    /// Referenced Materials, and Digital Thread links.
    /// </summary>
    private async Task AddCalculationEvidenceFacetsAsync(Guid objectId, List<PropertyFacet> facets, CancellationToken cancellationToken)
    {
        var history = await CalculationRecordReader.GetResultHistoryAsync(_context, objectId, cancellationToken).ConfigureAwait(false);

        facets.Add(new("Result History", history.Count > 0 ? $"{history.Count} execution(s)" : "Never executed", PropertyFacetKind.DisciplineSpecific));

        if (history.Count > 0)
        {
            var latest = history[^1];

            if (_templateRegistry.FindByCalculationId(latest.CalculationId) is { } template)
            {
                facets.Add(new(
                    "Assumptions",
                    template.Metadata.Assumptions.Count > 0 ? string.Join("; ", template.Metadata.Assumptions.Select(a => a.Description)) : "(none)",
                    PropertyFacetKind.DisciplineSpecific));

                facets.Add(new(
                    "Constraints",
                    template.Metadata.Constraints.Count > 0 ? string.Join("; ", template.Metadata.Constraints.Select(c => c.Description)) : "(none)",
                    PropertyFacetKind.DisciplineSpecific));
            }

            facets.Add(new("Latest Result", latest.ResultDisplay, PropertyFacetKind.DisciplineSpecific));
            facets.Add(new("Latest Result Outcome", latest.Outcome.ToString(), PropertyFacetKind.DisciplineSpecific));
            facets.Add(new("Latest Executed At", latest.ExecutedAt.ToString("u"), PropertyFacetKind.Provenance));
            facets.Add(new("Latest Executed By", latest.ExecutedByPrincipalId, PropertyFacetKind.Provenance));

            var safetyFactor = latest.IntermediateResults.FirstOrDefault(i => i.Name.Contains("Safety Factor", StringComparison.Ordinal));
            if (safetyFactor.Name is not null)
                facets.Add(new(safetyFactor.Name, safetyFactor.Display, PropertyFacetKind.DisciplineSpecific));

            facets.Add(new(
                "Referenced Materials",
                latest.ReferencedMaterialIds.Count > 0 ? string.Join(", ", latest.ReferencedMaterialIds) : "(none)",
                PropertyFacetKind.Relationship));
        }

        // Digital Thread — the existing relationship read, never a new
        // traversal or ITraceable.GetEvidenceAsync (see this class's own
        // remarks).
        var outgoing = await _context.RelationshipRepository.GetOutgoingAsync(objectId, cancellationToken).ConfigureAwait(false);
        var basedOn = outgoing.Where(r => string.Equals(r.RelationshipKind, "basedOnCalculation", StringComparison.Ordinal)).ToList();
        if (basedOn.Count > 0)
            facets.Add(new("Based On Calculation(s)", string.Join(", ", basedOn.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));

        var incoming = await _context.RelationshipRepository.GetIncomingAsync(objectId, cancellationToken).ConfigureAwait(false);
        var usedBy = incoming.Where(r => string.Equals(r.RelationshipKind, CalculationTemplateRegistry.CalculatedByRelationshipKind, StringComparison.Ordinal)
            || string.Equals(r.RelationshipKind, "basedOnCalculation", StringComparison.Ordinal)).ToList();
        if (usedBy.Count > 0)
            facets.Add(new("Used By (Digital Thread)", string.Join(", ", usedBy.Select(r => r.SourceId.ToString())), PropertyFacetKind.Relationship));
    }
}

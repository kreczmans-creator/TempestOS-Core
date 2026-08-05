using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Supplies the Property Inspector's own real facets for one Requirements
/// Kind (<c>"Requirement"</c>/<c>"RequirementCollection"</c>/
/// <c>"RequirementGroup"</c>) — the second <see cref="IPropertyFacetProvider"/>
/// backed by a real Engineering discipline, after Mechanical's own
/// (`WP 9.0A`). For a Requirement: Identifier/Statement/Category/Status/
/// Owner/Priority/Revision/Group/Deleted, plus Verification Coverage and
/// Allocation targets — both read directly from <see cref="IRequirementsService.GetRelationshipsAsync"/>,
/// the existing Digital Thread read, never a new traversal (`WP 9.1A`'s
/// own controlling instruction: "Do not implement new traceability
/// mechanisms") and never <see cref="IRequirementsService.GetEvidenceAsync"/>
/// (permission-gated; see this class's own remarks on <c>GetFacetsAsync</c>).
/// </summary>
public sealed class RequirementsPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly IRequirementsService _requirementsService;

    /// <summary>Initialises a new instance of the <see cref="RequirementsPropertyFacetProvider"/> class.</summary>
    /// <param name="kind">The single Requirements Kind this provider supplies facets for.</param>
    public RequirementsPropertyFacetProvider(string kind, IRequirementsService requirementsService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(requirementsService);

        Kind = kind;
        _requirementsService = requirementsService;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this provider's own <see cref="Kind"/>.</exception>
    public async Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(Kind, RequirementsService.RequirementDocumentKind, StringComparison.Ordinal))
            return await GetRequirementFacetsAsync(objectId, cancellationToken).ConfigureAwait(false);

        if (string.Equals(Kind, RequirementsService.RequirementCollectionDocumentKind, StringComparison.Ordinal))
            return await GetCollectionFacetsAsync(objectId, cancellationToken).ConfigureAwait(false);

        if (string.Equals(Kind, RequirementsService.RequirementGroupDocumentKind, StringComparison.Ordinal))
            return await GetGroupFacetsAsync(objectId, cancellationToken).ConfigureAwait(false);

        throw new InvalidOperationException($"'{Kind}' is not a Requirements Kind this provider can supply facets for.");
    }

    private async Task<IReadOnlyList<PropertyFacet>> GetRequirementFacetsAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var requirement = await _requirementsService.FindAsync(objectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{objectId}' is not a known Requirement.", nameof(objectId));

        var facets = new List<PropertyFacet>
        {
            new("Id", requirement.Id.ToString(), PropertyFacetKind.Identity),
            new("Kind", RequirementsService.RequirementDocumentKind, PropertyFacetKind.Identity),
            new("Identifier", requirement.Identifier, PropertyFacetKind.Identity),
            new("Statement", requirement.Statement, PropertyFacetKind.DisciplineSpecific),
        };

        if (requirement.Category is { } category)
            facets.Add(new("Category", category, PropertyFacetKind.DisciplineSpecific));

        facets.Add(new("Status", requirement.Status.ToString(), PropertyFacetKind.DisciplineSpecific));

        if (requirement.Owner is { } owner)
            facets.Add(new("Owner", owner, PropertyFacetKind.Provenance));

        if (requirement.Priority is { } priority)
            facets.Add(new("Priority", priority.ToString(), PropertyFacetKind.DisciplineSpecific));

        facets.Add(new("Revision", requirement.RevisionNumber.ToString(), PropertyFacetKind.Revision));
        facets.Add(new("Created By", requirement.CreatedByPrincipalId, PropertyFacetKind.Provenance));
        facets.Add(new("Created At", requirement.CreatedAt.ToString("u"), PropertyFacetKind.Provenance));

        if (requirement.GroupId is { } groupId)
            facets.Add(new("Group", groupId.ToString(), PropertyFacetKind.Relationship));

        if (requirement.IsDeleted)
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        // WP 9.1A — Verification Coverage / Allocation, both composed
        // purely from already-existing Digital Thread reads.
        // Deliberately reads GetRelationshipsAsync, never GetEvidenceAsync
        // (unlike this facet's own earlier shape) - GetEvidenceAsync is
        // transitively permission-gated on VerificationService.ReadPermission
        // (`ADR-0061`), and the Property Inspector must stay available to
        // any principal that can select a requirement at all, mirroring
        // RequirementValidationService's own identical, disclosed fix
        // (`WP9.1A Technical Debt Assessment.md`).
        var requirementRelationships = await _requirementsService.GetRelationshipsAsync(objectId, cancellationToken).ConfigureAwait(false);
        var verifications = requirementRelationships
            .Count(r => string.Equals(r.RelationshipKind, VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal));
        facets.Add(new(
            "Verification Coverage",
            verifications > 0 ? $"Verified ({verifications} record(s))" : "Not Verified",
            PropertyFacetKind.DisciplineSpecific));

        var allocations = requirementRelationships
            .Where(r => string.Equals(r.RelationshipKind, RequirementRelationshipKinds.AllocatedTo, StringComparison.Ordinal))
            .ToList();
        facets.Add(new(
            "Allocated To",
            allocations.Count > 0 ? string.Join(", ", allocations.Select(a => a.TargetDocumentId.ToString())) : "(none)",
            PropertyFacetKind.Relationship));

        return facets;
    }

    private async Task<IReadOnlyList<PropertyFacet>> GetCollectionFacetsAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var collection = await _requirementsService.FindCollectionAsync(objectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{objectId}' is not a known Requirement Collection.", nameof(objectId));

        var facets = new List<PropertyFacet>
        {
            new("Id", collection.Id.ToString(), PropertyFacetKind.Identity),
            new("Kind", RequirementsService.RequirementCollectionDocumentKind, PropertyFacetKind.Identity),
            new("Name", collection.Name, PropertyFacetKind.Identity),
            new("Members", collection.MemberRequirementIds.Count.ToString(), PropertyFacetKind.DisciplineSpecific),
        };

        if (collection.IsDeleted)
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        return facets;
    }

    private async Task<IReadOnlyList<PropertyFacet>> GetGroupFacetsAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var group = await _requirementsService.FindGroupAsync(objectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{objectId}' is not a known Requirement Group.", nameof(objectId));

        var facets = new List<PropertyFacet>
        {
            new("Id", group.Id.ToString(), PropertyFacetKind.Identity),
            new("Kind", RequirementsService.RequirementGroupDocumentKind, PropertyFacetKind.Identity),
            new("Name", group.Name, PropertyFacetKind.Identity),
            new("Parent Group", group.ParentGroupId?.ToString() ?? "(top level)", PropertyFacetKind.Relationship),
        };

        if (group.IsDeleted)
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        return facets;
    }
}

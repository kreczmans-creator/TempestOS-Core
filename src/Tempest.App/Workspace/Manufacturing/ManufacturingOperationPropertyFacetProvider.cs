using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <c>"ManufacturingOperation"</c> — the only Manufacturing Kind without an
/// existing facet provider to reuse (<c>"WorkInstruction"</c>/
/// <c>"Inspection"</c> instead reuse
/// <see cref="Documents.DocumentsPropertyFacetProvider"/>/
/// <see cref="Verification.VerificationActivityPropertyFacetProvider"/>
/// directly — see <see cref="ManufacturingWorkspaceRegistration"/>'s own
/// remarks). Every generic facet every prior discipline's own provider
/// already reads via the same casts (Identity/Metadata/Lifecycle/
/// Revisions/Parent/Deletable/BOM line), plus this Work Package's own
/// addition — Part (from <see cref="IManufacturingOperation.PartId"/>
/// directly) and Digital Thread links, read via
/// <see cref="IHasRelationships.GetRelationshipsAsync"/>/
/// <see cref="EngineeringDomainContext.RelationshipRepository"/> directly.
/// </summary>
public sealed class ManufacturingOperationPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ManufacturingOperationPropertyFacetProvider"/> class.</summary>
    public ManufacturingOperationPropertyFacetProvider(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this provider's own <see cref="Kind"/>.</exception>
    public async Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default)
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

        if (target is IManufacturingOperation operation)
            facets.Add(new("Part", operation.PartId.ToString(), PropertyFacetKind.Relationship));

        if (target is IHasMetadata metadata)
        {
            facets.Add(new("Classification", metadata.Classification ?? "(none)", PropertyFacetKind.DisciplineSpecific));

            if (metadata.Owner is { } owner)
                facets.Add(new("Owner", owner, PropertyFacetKind.Provenance));

            if (metadata.Discipline is { } discipline)
                facets.Add(new("Discipline", discipline, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Notes is { } notes)
                facets.Add(new("Description / Notes", notes, PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IHasLifecycle lifecycle)
        {
            facets.Add(new("Status", lifecycle.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
            facets.Add(new(
                "Released",
                lifecycle.Status is LifecycleState.Released ? "Yes" : "No",
                PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IHasRevisions revisions)
        {
            facets.Add(new("Revision", target.CurrentRevisionNumber.ToString(), PropertyFacetKind.Revision));
            facets.Add(new("Last Revised By", revisions.AuthorPrincipalId, PropertyFacetKind.Provenance));
        }

        if (target is IHasParent hasParent)
            facets.Add(new("Parent", hasParent.ParentId?.ToString() ?? "(top level)", PropertyFacetKind.Relationship));

        if (target is IHasBomLine bomLine)
        {
            facets.Add(new("BOM Sequence (ItemNumber)", bomLine.ItemNumber ?? "(unset)", PropertyFacetKind.DisciplineSpecific));
            facets.Add(new("BOM Quantity", $"{bomLine.Quantity} {bomLine.UnitOfMeasure}".Trim(), PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IDeletable { IsDeleted: true })
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        await AddDigitalThreadFacetsAsync(objectId, facets, cancellationToken).ConfigureAwait(false);

        return facets;
    }

    /// <summary>Adds this Work Package's own Digital Thread facets — every <c>"references"</c>/<c>"manufacturedBy"</c>/<c>"documentedBy"</c>/<c>"verifiedBy"</c> relationship in either direction, the existing relationship read, never a new traversal.</summary>
    private async Task AddDigitalThreadFacetsAsync(Guid objectId, List<PropertyFacet> facets, CancellationToken cancellationToken)
    {
        var outgoing = await _context.RelationshipRepository.GetOutgoingAsync(objectId, cancellationToken).ConfigureAwait(false);

        var references = outgoing.Where(r => string.Equals(r.RelationshipKind, "references", StringComparison.Ordinal)).ToList();
        if (references.Count > 0)
            facets.Add(new("References (Digital Thread)", string.Join(", ", references.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));

        var manufacturedBy = outgoing.Where(r => string.Equals(r.RelationshipKind, "manufacturedBy", StringComparison.Ordinal)).ToList();
        if (manufacturedBy.Count > 0)
            facets.Add(new("Manufactured By (Digital Thread)", string.Join(", ", manufacturedBy.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));

        // "documentedBy"/"verifiedBy" are recorded subject-as-source (mirrors
        // the base sample's own assembly--documentedBy-->drawing precedent,
        // and WP 9.3A's own requirement--verifiedBy-->activity precedent) —
        // this Operation is itself the subject, so both are read outgoing.
        var documentedBy = outgoing.Where(r => string.Equals(r.RelationshipKind, "documentedBy", StringComparison.Ordinal)).ToList();
        if (documentedBy.Count > 0)
            facets.Add(new("Documented By (Digital Thread)", string.Join(", ", documentedBy.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));

        var verifiedBy = outgoing.Where(r => string.Equals(r.RelationshipKind, "verifiedBy", StringComparison.Ordinal)).ToList();
        if (verifiedBy.Count > 0)
            facets.Add(new("Verified By (Digital Thread)", string.Join(", ", verifiedBy.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));
    }
}

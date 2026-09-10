using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Workspace.Deliverables;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="DeliverableCompletion.CanonicalKind"/>: the deliverable
/// (named), principal (named, via <see cref="IPrincipalDirectory.Describe"/>),
/// completion date, cited evidence and documents, fixed-price value and
/// the invoiced link (`WP 19.0A`, `ADR-0150`).
/// </summary>
public sealed class DeliverableCompletionPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;
    private readonly IPrincipalDirectory _principals;

    /// <summary>Initialises a new instance of the <see cref="DeliverableCompletionPropertyFacetProvider"/> class.</summary>
    public DeliverableCompletionPropertyFacetProvider(string kind, EngineeringDomainContext context, IPrincipalDirectory principals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(principals);

        Kind = kind;
        _context = context;
        _principals = principals;
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

        if (target is not DeliverableCompletion completion)
            return facets;

        var deliverable = await _context.Repository.FindAsync(completion.DeliverableId, cancellationToken).ConfigureAwait(false);
        var deliverableName = deliverable is IHasBusinessIdentifier deliverableIdentity ? deliverableIdentity.DisplayName : $"{completion.DeliverableId} (not found)";
        facets.Add(new("Deliverable", deliverableName, PropertyFacetKind.ObjectReference));

        facets.Add(new("Completed On", completion.CompletedOn.ToString("yyyy-MM-dd"), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Principal", _principals.Describe(completion.PrincipalIdentityId), PropertyFacetKind.Principal));

        facets.Add(new(
            "Issued Evidence",
            completion.IssuedEvidenceIds.Count == 0 ? "(none)" : string.Join("; ", completion.IssuedEvidenceIds),
            PropertyFacetKind.Relationship));

        facets.Add(new(
            "Documents",
            completion.DocumentIds.Count == 0 ? "(none)" : string.Join("; ", completion.DocumentIds),
            PropertyFacetKind.Relationship));

        facets.Add(new("Fixed Price", completion.FixedPriceValue?.ToString() ?? "(time-billed)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Invoiced By", completion.InvoicedBy?.ToString() ?? "(not invoiced)", PropertyFacetKind.DisciplineSpecific));

        if (target is IHasRevisions revisions)
        {
            facets.Add(new("Revision", target.CurrentRevisionNumber.ToString(), PropertyFacetKind.Revision));
            facets.Add(new("Last Revised By", revisions.AuthorPrincipalId, PropertyFacetKind.Principal));
        }

        if (target is IDeletable { IsDeleted: true })
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        return facets;
    }
}

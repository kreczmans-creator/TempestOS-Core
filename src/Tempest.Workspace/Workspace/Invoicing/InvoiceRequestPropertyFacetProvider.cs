using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;

namespace Tempest.Workspace.Invoicing;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="InvoiceRequest.CanonicalKind"/>: client, purchase order,
/// currency, lines, total, status, and every external field the connector
/// has reported (`WP 19.1A`, `ADR-0151`). Mirrors
/// <c>Evidence.EvidencePropertyFacetProvider</c>'s own shape.
/// </summary>
public sealed class InvoiceRequestPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="InvoiceRequestPropertyFacetProvider"/> class.</summary>
    public InvoiceRequestPropertyFacetProvider(string kind, EngineeringDomainContext context)
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
            facets.Add(new("Name", identity.DisplayName, PropertyFacetKind.Identity));

        if (target is not InvoiceRequest request)
            return facets;

        // A tag, never validated (ADR-0150's own rule for
        // Project.ClientOrganisationId, carried through unchanged) — shown
        // as the id itself rather than pretending a lookup always
        // resolves.
        facets.Add(new("Client", request.ClientOrganisationId, PropertyFacetKind.ObjectReference));
        facets.Add(new("Purchase Order Reference", request.PurchaseOrderReference ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Currency", request.Currency.ToString(), PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Lines",
            request.Lines.Count == 0
                ? "(none)"
                : string.Join("; ", request.Lines.Select(l => $"{l.Description} — {l.Quantity} x {l.UnitRate} = {l.Amount}")),
            PropertyFacetKind.Relationship));

        facets.Add(new("Total", request.Total.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Status", request.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Connector", request.Connector ?? "(not yet sent)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("External Id", request.ExternalId ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("External Invoice Number", request.ExternalInvoiceNumber ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("External Status", request.ExternalStatus ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Issued Date", request.IssuedDate?.ToString("O") ?? "(none)", PropertyFacetKind.DisciplineSpecific));

        // Read from the connector only — nothing in TempestOS ever sets
        // this (Product Owner guard, `WP 19.1A`'s own row).
        facets.Add(new("Paid Date", request.PaidDate?.ToString("O") ?? "(not paid)", PropertyFacetKind.DisciplineSpecific));

        facets.Add(new("Last Error", request.LastError ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Sent At (UTC)", request.SentAtUtc?.ToString("u") ?? "(not yet sent)", PropertyFacetKind.DisciplineSpecific));

        if (target is IHasParent hasParent)
            facets.Add(new("Parent", hasParent.ParentId?.ToString() ?? "(top level)", PropertyFacetKind.ObjectReference));

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

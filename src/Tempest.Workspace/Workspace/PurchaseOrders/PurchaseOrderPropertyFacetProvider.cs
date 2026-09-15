using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.PurchaseOrders;

namespace Tempest.Workspace.PurchaseOrders;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="PurchaseOrder.CanonicalKind"/>: reference, supplier,
/// currency, lines, net/VAT/gross totals, status and dates (`WP 21.3B`).
/// Mirrors <c>Quotations.QuotationPropertyFacetProvider</c>'s own shape.
/// </summary>
public sealed class PurchaseOrderPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderPropertyFacetProvider"/> class.</summary>
    public PurchaseOrderPropertyFacetProvider(string kind, EngineeringDomainContext context)
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

        if (target is not PurchaseOrder order)
            return facets;

        facets.Add(new("Reference", order.Reference, PropertyFacetKind.DisciplineSpecific));

        // A tag, never validated (the identical rule `Quotation.ClientOrganisationId` follows).
        facets.Add(new("Supplier", order.SupplierOrganisationId ?? "(none)", PropertyFacetKind.ObjectReference));
        facets.Add(new("Currency", order.Currency.ToString(), PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Lines",
            order.Lines.Count == 0
                ? "(none)"
                : string.Join("; ", order.Lines.Select(DescribeLine)),
            PropertyFacetKind.Relationship));

        facets.Add(new("Total (net)", order.Total.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("VAT", order.VatTotal.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Total (gross)", order.GrossTotal.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Status", order.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Issued Date", order.IssuedDate?.ToString("O") ?? "(not yet issued)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Expected Delivery", order.ExpectedDelivery?.ToString("O") ?? "(not stated)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Notes", order.Notes ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Expenses Recorded", order.ExpensesRecorded.ToString(), PropertyFacetKind.DisciplineSpecific));

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

    private static string DescribeLine(PurchaseOrderLine line) =>
        $"{line.Description} — {line.Quantity} x {line.UnitPrice} = {line.Net} (+ VAT {line.VatAmount}, {line.VatRate.DisplayName()})";
}

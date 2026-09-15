using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;

namespace Tempest.Workspace.Quotations;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="Quotation.CanonicalKind"/>: reference, date, client,
/// currency, validity, terms, lines, total and status (`WP 19.5A`,
/// `ADR-0152`). Mirrors <c>Invoicing.InvoiceRequestPropertyFacetProvider</c>'s
/// own shape.
/// </summary>
public sealed class QuotationPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="QuotationPropertyFacetProvider"/> class.</summary>
    public QuotationPropertyFacetProvider(string kind, EngineeringDomainContext context)
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

        if (target is not Quotation quotation)
            return facets;

        facets.Add(new("Reference", quotation.Reference, PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Quote Date", quotation.QuoteDate.ToString("O"), PropertyFacetKind.DisciplineSpecific));

        // A tag, never validated (ADR-0150's own rule for
        // Project.ClientOrganisationId, carried through unchanged) — shown
        // as the id itself rather than pretending a lookup always
        // resolves.
        facets.Add(new("Client", quotation.ClientOrganisationId ?? "(none)", PropertyFacetKind.ObjectReference));
        facets.Add(new("Currency", quotation.Currency.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Validity (days)", quotation.ValidityDays.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Terms", quotation.Terms ?? "(none)", PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Lines",
            quotation.Lines.Count == 0
                ? "(none)"
                : string.Join("; ", quotation.Lines.Select(DescribeLine)),
            PropertyFacetKind.Relationship));

        facets.Add(new("Total (net)", quotation.Total.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("VAT", quotation.VatTotal.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Total (gross)", quotation.GrossTotal.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Status", quotation.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Sent On", quotation.SentOn?.ToString("O") ?? "(not yet sent)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Decided On", quotation.DecidedOn?.ToString("O") ?? "(not yet decided)", PropertyFacetKind.DisciplineSpecific));

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

    private static string DescribeLine(QuotationLine line) => line.Basis switch
    {
        QuotationLineBasis.Hourly => $"{line.Description} — {line.Hours} x {line.Rate} = {line.Amount} (+ VAT {line.VatAmount}, {line.VatRate.DisplayName()})",
        _ => $"{line.Description} — {line.Amount} (fixed, + VAT {line.VatAmount}, {line.VatRate.DisplayName()})",
    };
}

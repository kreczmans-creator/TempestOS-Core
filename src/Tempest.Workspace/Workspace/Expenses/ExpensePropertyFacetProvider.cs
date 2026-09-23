using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;

namespace Tempest.Workspace.Expenses;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="ProjectExpense.CanonicalKind"/>: project, date, description,
/// category, net/VAT/gross, billable and the invoiced link (`WP 21.3B`).
/// Mirrors <c>Timesheets.TimesheetEntryPropertyFacetProvider</c>'s own
/// shape.
/// </summary>
public sealed class ExpensePropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ExpensePropertyFacetProvider"/> class.</summary>
    public ExpensePropertyFacetProvider(string kind, EngineeringDomainContext context)
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

        if (target is not ProjectExpense expense)
            return facets;

        var project = await _context.Repository.FindAsync(expense.ProjectId, cancellationToken).ConfigureAwait(false);
        var projectName = project is IHasBusinessIdentifier projectIdentity ? projectIdentity.DisplayName : $"{expense.ProjectId} (not found)";
        facets.Add(new("Project", projectName, PropertyFacetKind.ObjectReference));

        facets.Add(new("Date", expense.Date.ToString("yyyy-MM-dd"), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Description", expense.Description, PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Category", expense.Category.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Net Amount", expense.NetAmount.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("VAT Amount", expense.VatAmount.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Gross Amount", expense.GrossAmount.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Billable", expense.Billable.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Invoiced By", expense.InvoicedBy?.ToString() ?? "(not invoiced)", PropertyFacetKind.DisciplineSpecific));

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

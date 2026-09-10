using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Timesheets;

namespace Tempest.Workspace.Timesheets;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="TimesheetEntry.CanonicalKind"/>: principal (named, via
/// <see cref="IPrincipalDirectory.Describe"/>, not a bare identity id),
/// project, date, hours, billable, grade, billing and cost rate, and the
/// invoiced link (`WP 19.0A`, `ADR-0150`). Mirrors
/// <c>Evidence.EvidencePropertyFacetProvider</c>'s own shape.
/// </summary>
public sealed class TimesheetEntryPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;
    private readonly IPrincipalDirectory _principals;

    /// <summary>Initialises a new instance of the <see cref="TimesheetEntryPropertyFacetProvider"/> class.</summary>
    public TimesheetEntryPropertyFacetProvider(string kind, EngineeringDomainContext context, IPrincipalDirectory principals)
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

        if (target is not TimesheetEntry entry)
            return facets;

        facets.Add(new("Principal", _principals.Describe(entry.PrincipalIdentityId), PropertyFacetKind.Principal));

        var project = await _context.Repository.FindAsync(entry.ProjectId, cancellationToken).ConfigureAwait(false);
        var projectName = project is IHasBusinessIdentifier projectIdentity ? projectIdentity.DisplayName : $"{entry.ProjectId} (not found)";
        facets.Add(new("Project", projectName, PropertyFacetKind.ObjectReference));

        facets.Add(new("Date", entry.Date.ToString("yyyy-MM-dd"), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Task", entry.TaskDescription, PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Hours", entry.Hours.ToString("0.##"), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Billable", entry.Billable.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Grade", entry.Grade, PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Billing Rate", entry.BillingRate.ToString(), PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Cost Rate", entry.CostRate?.ToString() ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Invoiced By", entry.InvoicedBy?.ToString() ?? "(not invoiced)", PropertyFacetKind.DisciplineSpecific));

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

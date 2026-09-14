using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tasks;

namespace Tempest.Workspace.Tasks;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="ManualTask.CanonicalKind"/>: due date, done, project
/// (`WP 19.5C`). Mirrors <c>Quotations.QuotationPropertyFacetProvider</c>'s
/// own shape.
/// </summary>
public sealed class TaskPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="TaskPropertyFacetProvider"/> class.</summary>
    public TaskPropertyFacetProvider(string kind, EngineeringDomainContext context)
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

        if (target is not ManualTask task)
            return facets;

        facets.Add(new("Due Date", task.DueDate?.ToString("O") ?? "(none)", PropertyFacetKind.DisciplineSpecific));
        facets.Add(new("Done", task.Done ? $"Yes ({task.CompletedOn:O})" : "No", PropertyFacetKind.DisciplineSpecific));

        if (target is IHasParent hasParent)
            facets.Add(new("Project", hasParent.ParentId?.ToString() ?? "(none)", PropertyFacetKind.ObjectReference));

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

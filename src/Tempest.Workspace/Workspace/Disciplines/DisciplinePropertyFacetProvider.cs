using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Disciplines;

/// <summary>
/// The Properties panel for one discipline Kind (`ADR-0163`): identity,
/// every attribute the Kind declares (recorded or not), then lifecycle,
/// revision and parent.
/// </summary>
public sealed class DisciplinePropertyFacetProvider : IPropertyFacetProvider
{
    private readonly DisciplineKind _kind;
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DisciplinePropertyFacetProvider"/> class.</summary>
    public DisciplinePropertyFacetProvider(DisciplineKind kind, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(context);

        _kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => _kind.Kind;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var target = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var facets = new List<PropertyFacet>
        {
            new("Id", target.Id.ToString(), PropertyFacetKind.Identity),
            new("Kind", _kind.Label, PropertyFacetKind.Identity),
        };

        if (target is IHasBusinessIdentifier identity)
        {
            facets.Add(new("Name", identity.DisplayName, PropertyFacetKind.Identity));
            if (identity.Identifier is { } identifier)
                facets.Add(new("Engineering Identifier", identifier, PropertyFacetKind.Identity));
        }

        var recorded = (target as IDisciplineObject)?.Attributes ?? new Dictionary<string, string>();
        foreach (var attribute in _kind.Attributes)
            facets.Add(new(attribute, recorded.TryGetValue(attribute, out var value) ? value : "(not recorded)", PropertyFacetKind.DisciplineSpecific));

        if (target is IHasLifecycle lifecycle)
            facets.Add(new("Status", lifecycle.Status.ToString(), PropertyFacetKind.DisciplineSpecific));

        if (target is IHasRevisions revisions)
        {
            facets.Add(new("Revision", target.CurrentRevisionNumber.ToString(), PropertyFacetKind.Revision));
            facets.Add(new("Last Revised By", revisions.AuthorPrincipalId, PropertyFacetKind.Principal));
        }

        if (target is IHasParent hasParent)
            facets.Add(new("Parent", hasParent.ParentId?.ToString() ?? "(top level)", PropertyFacetKind.ObjectReference));

        if (target is IDeletable { IsDeleted: true })
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        return facets;
    }
}

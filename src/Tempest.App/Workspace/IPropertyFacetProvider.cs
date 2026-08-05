namespace Tempest.App.Workspace;

/// <summary>
/// Supplies the <see cref="IPropertyInspector"/>'s own real facets for one
/// object <c>Kind</c> — a genuine, disclosed `WP 9.0A` extension: the
/// Kind-keyed provider architecture `ADR-0067` already establishes for
/// <see cref="IProjectExplorerNodeProvider"/> and <see cref="IWorkspaceViewFactory"/>,
/// applied a third time, exactly as `WP8.1C Implementation Report.md`
/// anticipated ("every displayed facet is derived purely from the
/// selection tuple itself... no Engineering Core service is ever
/// consulted" — true only until a real discipline existed to consult).
/// </summary>
public interface IPropertyFacetProvider
{
    /// <summary>Gets the single object <c>Kind</c> this provider supplies facets for.</summary>
    string Kind { get; }

    /// <summary>Gets <paramref name="objectId"/>'s own current facets.</summary>
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this provider's own <see cref="Kind"/>.</exception>
    Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default);
}

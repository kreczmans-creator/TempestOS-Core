using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace;

// A real, minimal IPropertyFacetProvider — proves WP 9.0A's own third
// Kind-keyed provider category end to end without any Engineering Core
// dependency, mirroring TestWorkspaceViewFactory/TestProjectExplorerNodeProvider's
// own identical precedent.
public sealed class TestPropertyFacetProvider(string kind, IReadOnlyList<PropertyFacet> facets) : IPropertyFacetProvider
{
    public string Kind { get; } = kind;

    public int GetFacetsCallCount { get; private set; }

    public Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        GetFacetsCallCount++;
        return Task.FromResult(facets);
    }
}

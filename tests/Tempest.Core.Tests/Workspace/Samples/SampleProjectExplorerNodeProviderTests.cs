using Tempest.App.Workspace;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace.Samples;

// Proves SampleProjectExplorerNodeProvider (Tempest.Core.Tests.Workspace.Samples)
// — the Project Explorer's own living reference content, WP 8.1B — against
// its own fixed, fictional tree, with no Engineering Core dependency of any
// kind.
public class SampleProjectExplorerNodeProviderTests
{
    // The real area id, read from the module that registers it, so this
    // file adds no second spelling of the literal to the repository.
    private const string AreaId = WorkspaceExplorerSampleModule.NavigationItemId;

    [Fact]
    public void Kind_ReturnsTheConstructorArgument()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);

        Assert.Equal(AreaId, provider.Kind);
    }

    [Fact]
    public void Constructor_NullOrWhitespaceKind_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new SampleProjectExplorerNodeProvider("  "));

    [Fact]
    public async Task GetRootNodesAsync_ReturnsTheAssembliesCategory()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);

        var roots = await provider.GetRootNodesAsync();

        Assert.Single(roots);
        Assert.Equal("Assemblies", roots[0].Title);
        Assert.Null(roots[0].Kind);
        Assert.Equal(ProjectExplorerNodeType.Category, roots[0].NodeType);
        Assert.True(roots[0].HasChildren);
    }

    [Fact]
    public async Task GetRootNodesAsync_IsStable_AcrossCalls()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);

        var first = await provider.GetRootNodesAsync();
        var second = await provider.GetRootNodesAsync();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetChildrenAsync_AssembliesCategory_ReturnsTwoGroups()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);
        var category = (await provider.GetRootNodesAsync())[0];

        var children = await provider.GetChildrenAsync(category.Id);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, n => n.Title == "Primary Structure" && n.NodeType == ProjectExplorerNodeType.Group);
        Assert.Contains(children, n => n.Title == "Secondary Structure" && n.NodeType == ProjectExplorerNodeType.Group);
        Assert.All(children, n => Assert.Null(n.Kind));
    }

    [Fact]
    public async Task GetChildrenAsync_PrimaryStructure_ReturnsTwoComponents()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);
        var category = (await provider.GetRootNodesAsync())[0];
        var primaryStructure = (await provider.GetChildrenAsync(category.Id)).Single(n => n.Title == "Primary Structure");

        var children = await provider.GetChildrenAsync(primaryStructure.Id);

        Assert.Equal(2, children.Count);
        Assert.All(children, n => Assert.Equal(SampleExplorerContent.ComponentKind, n.Kind));
        Assert.Contains(children, n => n.Title == "Longeron");
        Assert.Contains(children, n => n.Title == "Frame");
        Assert.All(children, n => Assert.False(n.HasChildren));
    }

    [Fact]
    public async Task GetChildrenAsync_LeafComponent_ReturnsEmpty()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);
        var category = (await provider.GetRootNodesAsync())[0];
        var primaryStructure = (await provider.GetChildrenAsync(category.Id)).Single(n => n.Title == "Primary Structure");
        var longeron = (await provider.GetChildrenAsync(primaryStructure.Id)).Single(n => n.Title == "Longeron");

        var children = await provider.GetChildrenAsync(longeron.Id);

        Assert.Empty(children);
    }

    [Fact]
    public async Task GetChildrenAsync_UnknownNodeId_ThrowsArgumentException()
    {
        var provider = new SampleProjectExplorerNodeProvider(AreaId);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetChildrenAsync(Guid.NewGuid()));
    }
}

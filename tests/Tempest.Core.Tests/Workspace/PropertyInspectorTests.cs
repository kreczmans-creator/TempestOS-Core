using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

// Proves IPropertyInspector (Tempest.App.Workspace) reacts automatically to
// WorkspaceSelectionChangedEvent - the Property Inspector never subscribes
// to ISelectionService directly (WP8.0B Workspace Contracts.md §11) - and
// that every displayed facet in this Work Package's own shell is derived
// purely from the selection tuple itself, no Engineering Core service ever
// consulted.
[Collection("Console output capture")]
public class PropertyInspectorTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        IWorkspace workspace;
        try
        {
            Console.SetOut(new StringWriter());
            workspace = await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return (workspace, manager);
    }

    [Fact]
    public async Task CurrentFacets_BeforeAnySelection_IsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path);

        Assert.Empty(workspace.PropertyInspector.CurrentFacets);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InspectAsync_PopulatesIdentityFacets_FromTheSelectionTupleAlone()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path);
        var objectId = Guid.NewGuid();

        await workspace.PropertyInspector.InspectAsync(objectId, "Requirement");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Id" && f.Value == objectId.ToString());
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Kind" && f.Value == "Requirement");
        Assert.All(workspace.PropertyInspector.CurrentFacets, f => Assert.Equal(PropertyFacetKind.Identity, f.FacetKind));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ClearAsync_EmptiesCurrentFacets()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path);
        await workspace.PropertyInspector.InspectAsync(Guid.NewGuid(), "Requirement");

        await workspace.PropertyInspector.ClearAsync();

        Assert.Empty(workspace.PropertyInspector.CurrentFacets);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_OnSelectionService_AutomaticallyUpdatesPropertyInspector_ViaTheEventBus()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path);
        var objectId = Guid.NewGuid();

        await workspace.Selection.SelectAsync(objectId, "Material");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Id" && f.Value == objectId.ToString());
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Kind" && f.Value == "Material");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ClearAsync_OnSelectionService_AutomaticallyClearsPropertyInspector_ViaTheEventBus()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path);
        await workspace.Selection.SelectAsync(Guid.NewGuid(), "Material");

        await workspace.Selection.ClearAsync();

        Assert.Empty(workspace.PropertyInspector.CurrentFacets);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task DockPosition_IsRight()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path);

        Assert.Equal(WorkspaceDockPosition.Right, workspace.PropertyInspector.DockPosition);

        await manager.ShutdownAsync();
    }
}

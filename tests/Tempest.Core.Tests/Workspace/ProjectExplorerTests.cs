using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

// Proves IProjectExplorer (Tempest.App.Workspace) never calls any
// Engineering Core service directly - every read delegates to whichever
// IProjectExplorerNodeProvider is registered for the current area
// (ADR-0067). This Work Package registers none in production; these tests
// use real, minimal test-double providers to prove the delegation itself.
[Collection("Console output capture")]
public class ProjectExplorerTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager)> StartAsync(string rootPath, params Type[] moduleTypes)
    {
        var host = new TempestHostBuilder(moduleTypes)
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
    public async Task GetRootNodesAsync_NoAreaSelectedYet_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        Assert.Empty(await workspace.ProjectExplorer.GetRootNodesAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GetRootNodesAsync_AreaSelectedButNoProviderRegistered_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        Assert.Empty(await workspace.ProjectExplorer.GetRootNodesAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GetRootNodesAsync_ProviderRegisteredForCurrentArea_DelegatesToIt()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var node = new ProjectExplorerNode(Guid.NewGuid(), "REQ-0001", "Requirement", false, ProjectExplorerNodeType.Object);
        manager.RegisterExplorerArea(NavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(NavigationSampleModule.NavigationItemId, [node]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        var nodes = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Single(nodes);
        Assert.Equal(node, nodes[0]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GetRootNodesAsync_ProviderRegisteredForADifferentArea_DoesNotLeakIntoCurrentArea()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var node = new ProjectExplorerNode(Guid.NewGuid(), "REQ-0001", "Requirement", false, ProjectExplorerNodeType.Object);
        manager.RegisterExplorerArea("some-other-area", new TestProjectExplorerNodeProvider("some-other-area", [node]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        Assert.Empty(await workspace.ProjectExplorer.GetRootNodesAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GetChildrenAsync_NoProviderForCurrentArea_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        await Assert.ThrowsAsync<ArgumentException>(() => workspace.ProjectExplorer.GetChildrenAsync(Guid.NewGuid()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task IsVisible_DefaultsTrue_ShowHideToggleIt()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        Assert.True(workspace.ProjectExplorer.IsVisible);

        await workspace.ProjectExplorer.HideAsync();
        Assert.False(workspace.ProjectExplorer.IsVisible);

        await workspace.ProjectExplorer.ShowAsync();
        Assert.True(workspace.ProjectExplorer.IsVisible);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task DockPosition_IsLeft()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        Assert.Equal(WorkspaceDockPosition.Left, workspace.ProjectExplorer.DockPosition);

        await manager.ShutdownAsync();
    }
}

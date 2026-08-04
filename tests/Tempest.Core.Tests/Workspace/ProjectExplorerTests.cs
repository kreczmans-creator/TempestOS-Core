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

    // ----------------------------------------------------------------
    // CurrentPath / EnterAsync / ExitAsync (WP 8.1B — a genuine, disclosed
    // implementation-phase addition, not part of the twelve WP8.0B
    // contracts; ProjectExplorer is internal, reached here via
    // Tempest.App's own InternalsVisibleTo grant, WP 8.1A)
    // ----------------------------------------------------------------

    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ProjectExplorer Explorer)> StartWithCategoryAsync(string rootPath)
    {
        var (workspace, manager) = await StartAsync(rootPath, typeof(NavigationSampleModule));
        var category = new ProjectExplorerNode(Guid.NewGuid(), "Assemblies", null, true, ProjectExplorerNodeType.Category);
        manager.RegisterExplorerArea(NavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(NavigationSampleModule.NavigationItemId, [category]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        var explorer = (ProjectExplorer)workspace.ProjectExplorer;
        return (workspace, manager, explorer);
    }

    [Fact]
    public async Task CurrentPath_InitiallyEmpty()
    {
        using var temp = new TempDirectory();
        var (_, manager, explorer) = await StartWithCategoryAsync(temp.Path);

        Assert.Empty(explorer.CurrentPath);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task EnterAsync_ExtendsCurrentPath()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, explorer) = await StartWithCategoryAsync(temp.Path);
        var rootNodes = await workspace.ProjectExplorer.GetRootNodesAsync();
        var category = rootNodes[0];

        await explorer.EnterAsync(category);

        Assert.Single(explorer.CurrentPath);
        Assert.Equal(category, explorer.CurrentPath[0]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task EnterAsync_NodeWithNoChildren_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var (_, manager, explorer) = await StartWithCategoryAsync(temp.Path);
        var leaf = new ProjectExplorerNode(Guid.NewGuid(), "Bracket", "SampleComponent", false, ProjectExplorerNodeType.Object);

        var children = await explorer.EnterAsync(leaf);

        Assert.Empty(children);
        Assert.Single(explorer.CurrentPath);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExitAsync_AtRoot_IsANoOp_ReturnsRootNodes()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, explorer) = await StartWithCategoryAsync(temp.Path);

        var result = await explorer.ExitAsync();

        Assert.Empty(explorer.CurrentPath);
        Assert.Equal(await workspace.ProjectExplorer.GetRootNodesAsync(), result);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExitAsync_AfterEnter_RemovesLastPathSegment()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, explorer) = await StartWithCategoryAsync(temp.Path);
        var category = (await workspace.ProjectExplorer.GetRootNodesAsync())[0];
        await explorer.EnterAsync(category);

        await explorer.ExitAsync();

        Assert.Empty(explorer.CurrentPath);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SwitchAreaAsync_ResetsCurrentPath()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var category = new ProjectExplorerNode(Guid.NewGuid(), "Assemblies", null, true, ProjectExplorerNodeType.Category);
        manager.RegisterExplorerArea(NavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(NavigationSampleModule.NavigationItemId, [category]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        var explorer = (ProjectExplorer)workspace.ProjectExplorer;
        await explorer.EnterAsync(category);
        Assert.Single(explorer.CurrentPath);

        manager.RegisterExplorerArea(SecondaryNavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(SecondaryNavigationSampleModule.NavigationItemId, []));
        await workspace.Navigation.SwitchAreaAsync(SecondaryNavigationSampleModule.NavigationItemId);

        Assert.Empty(explorer.CurrentPath);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // FilterAsync (WP 8.1B)
    // ----------------------------------------------------------------

    [Fact]
    public async Task FilterAsync_MatchesRootNode()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var node = new ProjectExplorerNode(Guid.NewGuid(), "Bracket", "SampleComponent", false, ProjectExplorerNodeType.Object);
        manager.RegisterExplorerArea(NavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(NavigationSampleModule.NavigationItemId, [node]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        var explorer = (ProjectExplorer)workspace.ProjectExplorer;

        var matches = await explorer.FilterAsync("brack");

        Assert.Single(matches);
        Assert.Equal(node, matches[0]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FilterAsync_IsCaseInsensitive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var node = new ProjectExplorerNode(Guid.NewGuid(), "Bracket", "SampleComponent", false, ProjectExplorerNodeType.Object);
        manager.RegisterExplorerArea(NavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(NavigationSampleModule.NavigationItemId, [node]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        var explorer = (ProjectExplorer)workspace.ProjectExplorer;

        var matches = await explorer.FilterAsync("BRACKET");

        Assert.Single(matches);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FilterAsync_NoMatch_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var node = new ProjectExplorerNode(Guid.NewGuid(), "Bracket", "SampleComponent", false, ProjectExplorerNodeType.Object);
        manager.RegisterExplorerArea(NavigationSampleModule.NavigationItemId, new TestProjectExplorerNodeProvider(NavigationSampleModule.NavigationItemId, [node]));
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        var explorer = (ProjectExplorer)workspace.ProjectExplorer;

        var matches = await explorer.FilterAsync("no-such-object");

        Assert.Empty(matches);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FilterAsync_NullOrWhitespace_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        var explorer = (ProjectExplorer)workspace.ProjectExplorer;

        await Assert.ThrowsAsync<ArgumentException>(() => explorer.FilterAsync("  "));

        await manager.ShutdownAsync();
    }
}

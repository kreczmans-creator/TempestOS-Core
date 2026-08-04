using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

// Proves INavigationService (Tempest.App.Workspace) against real,
// production collaborators: the real INavigationProvider (resolved through
// a real, running TempestHost) for Areas/SwitchAreaAsync, and real,
// minimal test-double IWorkspaceViewFactory instances (this project does
// not use a mocking framework) for Open/JumpTo/Close.
[Collection("Console output capture")]
public class NavigationServiceTests
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

    // ----------------------------------------------------------------
    // Areas / SwitchAreaAsync — delegates to the real INavigationProvider
    // ----------------------------------------------------------------

    [Fact]
    public async Task Areas_DelegatesDirectly_ToTheRealNavigationProvider()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));

        Assert.Contains(workspace.Navigation.Areas, item => item.Id == NavigationSampleModule.NavigationItemId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SwitchAreaAsync_KnownArea_Succeeds()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));

        var exception = await Record.ExceptionAsync(() => workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId));

        Assert.Null(exception);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SwitchAreaAsync_UnknownArea_ThrowsNavigationItemNotFoundException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        await Assert.ThrowsAsync<Tempest.Core.Navigation.NavigationItemNotFoundException>(() =>
            workspace.Navigation.SwitchAreaAsync("not-a-real-area"));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // OpenAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task OpenAsync_UnregisteredKind_ThrowsWorkspaceViewFactoryNotFoundException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        await Assert.ThrowsAsync<WorkspaceViewFactoryNotFoundException>(() =>
            workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement"));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenAsync_RegisteredKind_ConstructsViaTheFactory_AddsToOpenViews()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var objectId = Guid.NewGuid();

        var view = await workspace.Navigation.OpenAsync(objectId, "Requirement");

        Assert.Contains(view, workspace.OpenViews);
        Assert.Equal(objectId, view.ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenAsync_SetsActiveView()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));

        var view = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        Assert.Same(view, workspace.ActiveView);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenAsync_SameObjectTwice_FocusesExistingTab_DoesNotCreateASecondView()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        var factory = new TestWorkspaceViewFactory("Requirement");
        manager.RegisterView("Requirement", factory);
        var objectId = Guid.NewGuid();

        var first = await workspace.Navigation.OpenAsync(objectId, "Requirement");
        var second = await workspace.Navigation.OpenAsync(objectId, "Requirement");

        Assert.Same(first, second);
        Assert.Single(workspace.OpenViews);
        Assert.Equal(1, factory.CreateCallCount);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // JumpToAsync — always a new tab
    // ----------------------------------------------------------------

    [Fact]
    public async Task JumpToAsync_UnregisteredKind_ThrowsWorkspaceViewFactoryNotFoundException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        await Assert.ThrowsAsync<WorkspaceViewFactoryNotFoundException>(() =>
            workspace.Navigation.JumpToAsync(Guid.NewGuid(), "CalculationRecord"));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task JumpToAsync_SameObjectAlreadyOpen_StillOpensANewTab()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var objectId = Guid.NewGuid();
        var original = await workspace.Navigation.OpenAsync(objectId, "Requirement");

        var jumped = await workspace.Navigation.JumpToAsync(objectId, "Requirement");

        Assert.NotSame(original, jumped);
        Assert.Equal(2, workspace.OpenViews.Count);
        Assert.Contains(original, workspace.OpenViews);
        Assert.Contains(jumped, workspace.OpenViews);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // CloseAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task CloseAsync_OpenView_RemovesItFromOpenViews()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var view = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        await workspace.Navigation.CloseAsync(view.Id);

        Assert.DoesNotContain(view, workspace.OpenViews);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CloseAsync_UnknownViewId_IsANoOp()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);

        var exception = await Record.ExceptionAsync(() => workspace.Navigation.CloseAsync(Guid.NewGuid()));

        Assert.Null(exception);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CloseAsync_ActiveView_PromotesTheMostRecentRemainingViewToActive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var first = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");
        var second = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        await workspace.Navigation.CloseAsync(second.Id);

        Assert.Same(first, workspace.ActiveView);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CloseAsync_LastRemainingView_ActiveViewBecomesNull()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var view = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        await workspace.Navigation.CloseAsync(view.Id);

        Assert.Null(workspace.ActiveView);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // History / GoBackAsync / GoForwardAsync (WP 8.1B — a genuine,
    // disclosed implementation-phase addition, not part of the twelve
    // WP8.0B contracts; NavigationService is internal, reached here via
    // Tempest.App's own InternalsVisibleTo grant, WP 8.1A)
    // ----------------------------------------------------------------

    [Fact]
    public async Task SwitchAreaAsync_RecordsAHistoryEntry()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;

        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        Assert.Single(navigationService.History);
        Assert.Equal(NavigationSampleModule.NavigationItemId, navigationService.History[0].AreaId);
        Assert.Null(navigationService.History[0].ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenAsync_RecordsAHistoryEntry()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var navigationService = (NavigationService)workspace.Navigation;
        var objectId = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(objectId, "Requirement");

        Assert.Single(navigationService.History);
        Assert.Equal(objectId, navigationService.History[0].ObjectId);
        Assert.Equal("Requirement", navigationService.History[0].ObjectKind);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GoBackAsync_NoHistoryYet_ReturnsFalse()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        var navigationService = (NavigationService)workspace.Navigation;

        Assert.False(await navigationService.GoBackAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GoBackAsync_OnlyOneEntry_ReturnsFalse()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        Assert.False(await navigationService.GoBackAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GoBackAsync_TwoAreaSwitches_ReturnsToTheFirst()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        await workspace.Navigation.SwitchAreaAsync(SecondaryNavigationSampleModule.NavigationItemId);

        var moved = await navigationService.GoBackAsync();

        Assert.True(moved);
        Assert.Equal(NavigationSampleModule.NavigationItemId, navigationService.CurrentAreaId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GoBackAsync_DoesNotRecordANewHistoryEntry()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        await workspace.Navigation.SwitchAreaAsync(SecondaryNavigationSampleModule.NavigationItemId);

        await navigationService.GoBackAsync();

        Assert.Equal(2, navigationService.History.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GoForwardAsync_AfterGoBack_ReturnsToTheSecond()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        await workspace.Navigation.SwitchAreaAsync(SecondaryNavigationSampleModule.NavigationItemId);
        await navigationService.GoBackAsync();

        var moved = await navigationService.GoForwardAsync();

        Assert.True(moved);
        Assert.Equal(SecondaryNavigationSampleModule.NavigationItemId, navigationService.CurrentAreaId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task GoForwardAsync_AtNewestEntry_ReturnsFalse()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        Assert.False(await navigationService.GoForwardAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SwitchAreaAsync_AfterGoBack_TruncatesForwardHistory()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);
        await workspace.Navigation.SwitchAreaAsync(SecondaryNavigationSampleModule.NavigationItemId);
        await navigationService.GoBackAsync();

        await workspace.Navigation.SwitchAreaAsync(NavigationSampleModule.NavigationItemId);

        Assert.False(await navigationService.GoForwardAsync());
        Assert.Equal(2, navigationService.History.Count);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // RecentItems (WP 8.1B — a genuine, disclosed implementation-phase
    // addition, not part of the twelve WP8.0B contracts)
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecentItems_OpenAsync_AddsAnEntry_MostRecentFirst()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var navigationService = (NavigationService)workspace.Navigation;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(first, "Requirement");
        await workspace.Navigation.OpenAsync(second, "Requirement");

        Assert.Equal(2, navigationService.RecentItems.Count);
        Assert.Equal(second, navigationService.RecentItems[0].ObjectId);
        Assert.Equal(first, navigationService.RecentItems[1].ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RecentItems_ReOpeningSameObject_DoesNotDuplicate_MovesToFront()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var navigationService = (NavigationService)workspace.Navigation;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(first, "Requirement");
        await workspace.Navigation.OpenAsync(second, "Requirement");
        await workspace.Navigation.OpenAsync(first, "Requirement");

        Assert.Equal(2, navigationService.RecentItems.Count);
        Assert.Equal(first, navigationService.RecentItems[0].ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RecentItems_GoBackReplay_DoesNotAddAnEntry()
    {
        using var temp = new TempDirectory();
        var (workspace, manager) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var navigationService = (NavigationService)workspace.Navigation;
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");
        var jumped = await workspace.Navigation.JumpToAsync(Guid.NewGuid(), "Requirement");

        await navigationService.GoBackAsync();

        Assert.Equal(2, navigationService.RecentItems.Count);
        Assert.Equal(jumped.ObjectId, navigationService.RecentItems[0].ObjectId);

        await manager.ShutdownAsync();
    }
}

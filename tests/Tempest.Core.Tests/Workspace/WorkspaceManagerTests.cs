using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

// Proves WP 8.1A end to end: WorkspaceManager (Tempest.App.Workspace) is a
// real composition root layered above a real, unmodified TempestHost,
// exactly as ADR-0062 designs - constructs and runs the real Host, resolves
// the real INavigationProvider/IEventBus/ISettingsProvider through the real
// ITempestHost.Services, and assembles a real IWorkspace. Every collaborator
// here is the real production type; only the persistence root is
// test-isolated (a TempDirectory), mirroring RequirementsHostRegistrationTests'
// own precedent exactly.
[Collection("Console output capture")]
public class WorkspaceManagerTests
{
    private static ITempestHost BuildHost(string rootPath, params Type[] moduleTypes) =>
        new TempestHostBuilder(moduleTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

    private static async Task<T> WithSuppressedConsoleAsync<T>(Func<Task<T>> body)
    {
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            return await body().ConfigureAwait(false);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // ----------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullHost_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new WorkspaceManager(null!));

    [Fact]
    public void Constructor_RealHostInCreatedState_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var exception = Record.Exception(() => new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes)));

        Assert.Null(exception);
    }

    [Fact]
    public void Current_BeforeStartAsync_IsNull()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        Assert.Null(manager.Current);
    }

    // ----------------------------------------------------------------
    // StartAsync / lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_StartsTheRealHost_ReachesRunning()
    {
        using var temp = new TempDirectory();
        var host = BuildHost(temp.Path, Type.EmptyTypes);
        var manager = new WorkspaceManager(host);

        await WithSuppressedConsoleAsync(async () =>
        {
            await manager.StartAsync();
            return 0;
        });

        Assert.Equal(HostState.Running, host.State);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task StartAsync_ReturnsAssembledWorkspace_WithEverySubServicePopulated()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        Assert.NotNull(workspace.Layout);
        Assert.NotNull(workspace.State);
        Assert.NotNull(workspace.Navigation);
        Assert.NotNull(workspace.Selection);
        Assert.NotNull(workspace.ProjectExplorer);
        Assert.NotNull(workspace.PropertyInspector);
        Assert.Empty(workspace.OpenViews);
        Assert.Null(workspace.ActiveView);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task StartAsync_SetsCurrent_ToTheAssembledWorkspace()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        Assert.Same(workspace, manager.Current);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task StartAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        await WithSuppressedConsoleAsync(() => manager.StartAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ShutdownAsync_BeforeStartAsync_IsANoOp()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        var exception = await Record.ExceptionAsync(() => manager.ShutdownAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_StopsTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = BuildHost(temp.Path, Type.EmptyTypes);
        var manager = new WorkspaceManager(host);
        await WithSuppressedConsoleAsync(() => manager.StartAsync());

        await manager.ShutdownAsync();

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task ShutdownAsync_ClearsCurrent()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        await WithSuppressedConsoleAsync(() => manager.StartAsync());

        await manager.ShutdownAsync();

        Assert.Null(manager.Current);
    }

    // ----------------------------------------------------------------
    // RegisterView / RegisterExplorerArea (ADR-0067)
    // ----------------------------------------------------------------

    [Fact]
    public void RegisterView_NullKind_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException
        // specifically for a null argument (still an ArgumentException by
        // inheritance) — the same distinction WP 7.3A's own tests already
        // disclosed for this exact BCL method.
        Assert.ThrowsAny<ArgumentException>(() => manager.RegisterView(null!, new TestWorkspaceViewFactory("Requirement")));
    }

    [Fact]
    public void RegisterView_NullFactory_ThrowsArgumentNullException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        Assert.Throws<ArgumentNullException>(() => manager.RegisterView("Requirement", null!));
    }

    [Fact]
    public void RegisterView_DuplicateKind_ThrowsDuplicateWorkspaceRegistrationException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));

        var exception = Assert.Throws<DuplicateWorkspaceRegistrationException>(() =>
            manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement")));
        Assert.Equal("Requirement", exception.Kind);
    }

    [Fact]
    public void RegisterExplorerArea_DuplicateKind_ThrowsDuplicateWorkspaceRegistrationException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        manager.RegisterExplorerArea("Requirement", new TestProjectExplorerNodeProvider("Requirement", []));

        Assert.Throws<DuplicateWorkspaceRegistrationException>(() =>
            manager.RegisterExplorerArea("Requirement", new TestProjectExplorerNodeProvider("Requirement", [])));
    }

    [Fact]
    public void RegisterFacetProvider_NullKind_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        Assert.ThrowsAny<ArgumentException>(() => manager.RegisterFacetProvider(null!, new TestPropertyFacetProvider("Requirement", [])));
    }

    [Fact]
    public void RegisterFacetProvider_NullProvider_ThrowsArgumentNullException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        Assert.Throws<ArgumentNullException>(() => manager.RegisterFacetProvider("Requirement", null!));
    }

    [Fact]
    public void RegisterFacetProvider_DuplicateKind_ThrowsDuplicateWorkspaceRegistrationException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        manager.RegisterFacetProvider("Requirement", new TestPropertyFacetProvider("Requirement", []));

        var exception = Assert.Throws<DuplicateWorkspaceRegistrationException>(() =>
            manager.RegisterFacetProvider("Requirement", new TestPropertyFacetProvider("Requirement", [])));
        Assert.Equal("Requirement", exception.Kind);
    }

    [Fact]
    public async Task RegisterFacetProvider_BeforeStartAsync_IsHonouredByThePropertyInspector()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        var facets = new List<PropertyFacet> { new("Name", "Real Facet", PropertyFacetKind.Identity) };
        manager.RegisterFacetProvider("Requirement", new TestPropertyFacetProvider("Requirement", facets));

        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());
        var objectId = Guid.NewGuid();
        await workspace.PropertyInspector.InspectAsync(objectId, "Requirement");

        Assert.Equal(facets, workspace.PropertyInspector.CurrentFacets);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RegisterFacetProvider_AfterStartAsync_IsStillHonoured()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        var facets = new List<PropertyFacet> { new("Name", "Real Facet", PropertyFacetKind.Identity) };
        manager.RegisterFacetProvider("Requirement", new TestPropertyFacetProvider("Requirement", facets));
        await workspace.PropertyInspector.InspectAsync(Guid.NewGuid(), "Requirement");

        Assert.Equal(facets, workspace.PropertyInspector.CurrentFacets);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task PropertyInspector_NoFacetProviderRegisteredForKind_FallsBackToIdKindOnly()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        var objectId = Guid.NewGuid();
        await workspace.PropertyInspector.InspectAsync(objectId, "UnregisteredKind");

        Assert.Equal(2, workspace.PropertyInspector.CurrentFacets.Count);
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Id" && f.Value == objectId.ToString());
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Kind" && f.Value == "UnregisteredKind");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RegisterView_BeforeStartAsync_IsHonouredByTheAssembledWorkspace()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));

        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());
        var objectId = Guid.NewGuid();
        var view = await workspace.Navigation.OpenAsync(objectId, "Requirement");

        Assert.Equal(objectId, view.ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RegisterView_AfterStartAsync_IsStillHonoured()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));
        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var view = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        Assert.Equal("Requirement", view.ObjectKind);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Composition correctness — zero engineering functionality
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_NoEngineeringViewOrExplorerRegistration_ProjectExplorerRootNodesIsEmpty()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        Assert.Empty(await workspace.ProjectExplorer.GetRootNodesAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task StartAsync_NoEngineeringFunctionality_OpenAsyncForUnregisteredKind_ThrowsWorkspaceViewFactoryNotFoundException()
    {
        using var temp = new TempDirectory();
        var manager = new WorkspaceManager(BuildHost(temp.Path, Type.EmptyTypes));

        var workspace = await WithSuppressedConsoleAsync(() => manager.StartAsync());

        var exception = await Assert.ThrowsAsync<WorkspaceViewFactoryNotFoundException>(() =>
            workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement"));
        Assert.Equal("Requirement", exception.Kind);

        await manager.ShutdownAsync();
    }
}

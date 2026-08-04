using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

// Proves EngineeringCockpit (Tempest.App.Workspace) - the Workspace's own
// default landing screen (ADR-0069), WP 8.1C - against real, production
// collaborators: the real ICommandRegistry (resolved through a real,
// running TempestHost) for the Cockpit's own Command Palette integration
// (ADR-0070), and NavigationService (WP 8.1A/8.1B) for every real,
// non-placeholder status indicator. EngineeringCockpit is internal, reached
// here via Tempest.App's own InternalsVisibleTo grant (WP 8.1A).
[Collection("Console output capture")]
public class EngineeringCockpitTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath, params Type[] moduleTypes)
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

        return (workspace, manager, host);
    }

    // ----------------------------------------------------------------
    // Placeholder content
    // ----------------------------------------------------------------

    [Fact]
    public async Task ProjectName_IsFixedRepresentativePlaceholderText()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal("Sample Engineering Project", cockpit.ProjectName);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Health_IsUnknown_NoSignalSourceExistsYet()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.Health);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AttentionItems_HasFixedRepresentativeEntries()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.NotEmpty(cockpit.AttentionItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenActions_HasFixedRepresentativeEntries()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.NotEmpty(cockpit.OpenActions);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task KpiCards_AreAllMarkedPlaceholder()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.NotEmpty(cockpit.KpiCards);
        Assert.All(cockpit.KpiCards, kpi => Assert.True(kpi.IsPlaceholder));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Real Workspace service consumption (no placeholder)
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecentActivity_DelegatesDirectly_ToNavigationServiceRecentItems()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;
        var objectId = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(objectId, "Requirement");

        Assert.Single(cockpit.RecentActivity);
        Assert.Equal(objectId, cockpit.RecentActivity[0].ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AreaCount_ReflectsTheRealNavigationProvider()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(2, cockpit.AreaCount);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenDocumentCount_ReflectsRealOpenViews()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        Assert.Equal(2, cockpit.OpenDocumentCount);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // AvailableCommands / InvokeCommandAsync - Command Palette integration
    // (ADR-0070), against the real ICommandRegistry
    // ----------------------------------------------------------------

    [Fact]
    public async Task AvailableCommands_NoModulesLoaded_IsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Empty(cockpit.AvailableCommands);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AvailableCommands_RealCommandModuleLoaded_ListsItsDescriptors()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.AvailableCommands, d => d.Id == CommandSampleModule.IncrementCounterCommandId);
        Assert.Contains(cockpit.AvailableCommands, d => d.Id == CommandSampleModule.NavigateHomeCommandId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AvailableCommands_ExcludesADescriptorWhoseCanExecuteReturnsFalse()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "test.never-available",
            displayName: "Never Available",
            canExecute: () => false));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "test.always-available",
            displayName: "Always Available",
            canExecute: () => true));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.DoesNotContain(cockpit.AvailableCommands, d => d.Id == "test.never-available");
        Assert.Contains(cockpit.AvailableCommands, d => d.Id == "test.always-available");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InvokeCommandAsync_ValidIndex_DispatchesTheRealCommand()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;
        var index = cockpit.AvailableCommands
            .Select((descriptor, i) => (descriptor, i))
            .Single(x => x.descriptor.Id == CommandSampleModule.IncrementCounterCommandId).i + 1;

        var result = await cockpit.InvokeCommandAsync(index);

        Assert.True(result.Succeeded);
        Assert.Equal("Counter is now 1.", result.Message);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InvokeCommandAsync_IndexTooLow_ThrowsArgumentOutOfRangeException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cockpit.InvokeCommandAsync(0));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InvokeCommandAsync_IndexTooHigh_ThrowsArgumentOutOfRangeException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cockpit.InvokeCommandAsync(cockpit.AvailableCommands.Count + 1));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Continue Where I Left Off / Recent Activity navigation (WP 8.1C)
    // ----------------------------------------------------------------

    [Fact]
    public async Task ContinueWhereILeftOff_NothingOpenedYet_IsNull()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Null(cockpit.ContinueWhereILeftOff);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ContinueWhereILeftOff_ReflectsTheMostRecentlyOpenedObject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(first, "Requirement");
        await workspace.Navigation.OpenAsync(second, "Requirement");

        Assert.Equal(second, cockpit.ContinueWhereILeftOff!.ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ContinueAsync_NothingOpenedYet_ThrowsInvalidOperationException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        await Assert.ThrowsAsync<InvalidOperationException>(() => cockpit.ContinueAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ContinueAsync_ReopensOrFocusesTheMostRecentObject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;
        var objectId = Guid.NewGuid();
        var original = await workspace.Navigation.OpenAsync(objectId, "Requirement");

        var continued = await cockpit.ContinueAsync();

        Assert.Same(original, continued);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenRecentAsync_ValidIndex_ReopensOrFocusesThatObject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;
        var first = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        var reopened = await cockpit.OpenRecentAsync(2);

        Assert.Same(first, reopened);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenRecentAsync_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cockpit.OpenRecentAsync(1));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Project Health Dashboard / Risk / Digital Thread / Milestones
    // (WP 8.1C) - all disclosed placeholder content
    // ----------------------------------------------------------------

    [Fact]
    public async Task HealthDashboardStatuses_AreAllUnknown_NoRealSignalSourceExistsYet()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.RequirementsStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.VerificationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.CalculationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.DocumentationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.ReviewStatus);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RecentProjects_HasFixedRepresentativeEntry()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.NotEmpty(cockpit.RecentProjects);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FavouriteProjects_IsHonestlyEmpty_FavouritingNotImplemented()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Empty(cockpit.FavouriteProjects);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // QuickActions (WP 8.1C) - computed from real state, never fixed text
    // ----------------------------------------------------------------

    [Fact]
    public async Task QuickActions_NothingOpenNoAreasNoCommands_IsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Empty(cockpit.QuickActions);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task QuickActions_AreaRegistered_IncludesABrowseHint()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.QuickActions, hint => hint.Contains("Browse an Area", StringComparison.Ordinal));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task QuickActions_SomethingOpened_IncludesAContinueHint()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        Assert.Contains(cockpit.QuickActions, hint => hint.StartsWith("Continue:", StringComparison.Ordinal));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task QuickActions_CommandsAvailable_IncludesARunHint()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.QuickActions, hint => hint.Contains("Run a Global Command", StringComparison.Ordinal));

        await manager.ShutdownAsync();
    }
}

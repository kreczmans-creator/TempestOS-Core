using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

// Proves IWorkspaceState (Tempest.App.Workspace) persists via the real,
// unmodified ISettingsProvider (ADR-0064) - "Session restore" as this Work
// Package's own controlling instruction names it - with no new persistence
// mechanism. Tests both WorkspaceState directly (a same-assembly internal
// type, reachable here via Tempest.App's own InternalsVisibleTo grant,
// added by this Work Package) and the full cross-restart round trip through
// WorkspaceManager.
[Collection("Console output capture")]
public class WorkspaceStateTests
{
    private static ITempestHost BuildHost(string rootPath) =>
        new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

    private static async Task<T> RunAgainstRunningHostAsync<T>(ITempestHost host, Func<ITempestHost, Task<T>> body)
    {
        var runTask = host.RunAsync();
        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        var originalOut = Console.Out;
        T result;
        try
        {
            Console.SetOut(new StringWriter());
            result = await body(host);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        await host.StopAsync();
        await runTask;
        return result;
    }

    private static IReadOnlyList<WorkspacePanelPlacement> Defaults(Guid explorerId, Guid propertiesId) =>
    [
        new(explorerId, WorkspaceDockPosition.Left, 30, true),
        new(propertiesId, WorkspaceDockPosition.Right, 30, true),
    ];

    // ----------------------------------------------------------------
    // Direct unit tests
    // ----------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_FirstRun_YieldsDefaultLayoutAndNoOpenTabs()
    {
        using var temp = new TempDirectory();
        var host = BuildHost(temp.Path);
        var explorerId = Guid.NewGuid();
        var propertiesId = Guid.NewGuid();

        await RunAgainstRunningHostAsync(host, async h =>
        {
            var settingsProvider = (ISettingsProvider)h.Services!.GetService(typeof(ISettingsProvider));
            var state = new WorkspaceState(settingsProvider, Defaults(explorerId, propertiesId));

            await state.LoadAsync();

            Assert.Empty(state.OpenViewIds);
            Assert.Null(state.LastSelection);
            Assert.Equal(2, state.Layout.PanelPlacements.Count);
            return true;
        });
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_OnASecondInstance_RoundTripsLayoutOpenTabsAndSelection()
    {
        using var temp = new TempDirectory();
        var host = BuildHost(temp.Path);
        var explorerId = Guid.NewGuid();
        var propertiesId = Guid.NewGuid();
        var viewId = Guid.NewGuid();
        var selection = new WorkspaceSelection(Guid.NewGuid(), "Requirement");

        await RunAgainstRunningHostAsync(host, async h =>
        {
            var settingsProvider = (ISettingsProvider)h.Services!.GetService(typeof(ISettingsProvider));

            var first = new WorkspaceState(settingsProvider, Defaults(explorerId, propertiesId));
            await first.LoadAsync();
            first.SetOpenViewIds([viewId]);
            first.SetLastSelection(selection);
            first.Layout.SetPlacement(explorerId, new WorkspacePanelPlacement(explorerId, WorkspaceDockPosition.Bottom, 50, false));
            await first.SaveAsync();

            var second = new WorkspaceState(settingsProvider, Defaults(explorerId, propertiesId));
            await second.LoadAsync();

            Assert.Equal([viewId], second.OpenViewIds);
            Assert.Equal(selection, second.LastSelection);
            Assert.Equal(WorkspaceDockPosition.Bottom, second.Layout.GetPlacement(explorerId).DockPosition);
            return true;
        });
    }

    // ----------------------------------------------------------------
    // Cross-restart round trip through WorkspaceManager itself
    // ----------------------------------------------------------------

    [Fact]
    public async Task WorkspaceManager_ShutdownThenStart_AcrossTwoRealHosts_RestoresLastSelection()
    {
        using var temp = new TempDirectory();
        var objectId = Guid.NewGuid();

        // First run: select something, then shut down (persists via SaveAsync).
        var firstManager = new WorkspaceManager(BuildHost(temp.Path));
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            var firstWorkspace = await firstManager.StartAsync();
            await firstWorkspace.Selection.SelectAsync(objectId, "Requirement");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        await firstManager.ShutdownAsync();

        // Second run: a brand-new ITempestHost (single-use) over the same
        // persistence root - proves this is a genuine restart, not reuse of
        // in-memory state.
        var secondManager = new WorkspaceManager(BuildHost(temp.Path));
        IWorkspace secondWorkspace;
        try
        {
            Console.SetOut(new StringWriter());
            secondWorkspace = await secondManager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(new WorkspaceSelection(objectId, "Requirement"), secondWorkspace.State.LastSelection);

        await secondManager.ShutdownAsync();
    }
}

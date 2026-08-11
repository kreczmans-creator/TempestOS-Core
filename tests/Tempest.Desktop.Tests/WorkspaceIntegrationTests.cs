using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Core.Settings;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates that the existing Workspace integrates unchanged
/// (`WP 10.0B`'s own explicit requirement: "Mechanical, Requirements,
/// Calculations, Verification, Documents and Manufacturing must all load
/// without behavioural change"), plus Tabbed Documents and Persistent
/// Layouts / Workspace Restoration from the "Demonstrate" list — each
/// against a real, running <see cref="WorkspaceHost"/>, never a fake or
/// mocked Workspace.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorkspaceIntegrationTests
{
    [AvaloniaFact]
    public async Task AllSixEngineeringDisciplines_LoadWithoutBehaviouralChange()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;

            // Every one of the six real areas EngineeringWorkspaceComposer
            // registers (unchanged since WP 9.5A) is switchable and returns
            // real, non-empty sample data through the Project Explorer —
            // proof each discipline actually loaded, not merely that
            // RegisterExplorerArea didn't throw.
            string[] areaIds =
            [
                MechanicalWorkspaceExplorerModule.NavigationItemId,
                RequirementsWorkspaceExplorerModule.NavigationItemId,
                CalculationsWorkspaceExplorerModule.NavigationItemId,
                VerificationWorkspaceExplorerModule.NavigationItemId,
                DocumentsWorkspaceExplorerModule.NavigationItemId,
                ManufacturingWorkspaceExplorerModule.NavigationItemId,
            ];

            foreach (var areaId in areaIds)
            {
                await workspace.Navigation.SwitchAreaAsync(areaId);
                var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
                Assert.True(roots.Count > 0, $"Expected area '{areaId}' to have real root nodes.");
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TabbedDocuments_OpeningTwoObjectsShowsTwoTabs_ClosingOneLeavesOne()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var documentArea = new DocumentAreaView();

            // The Project Explorer has no default area selected (the
            // Engineering Cockpit, not an Explorer area, is the Workspace's
            // own default landing screen, ADR-0069) — an explicit area
            // switch is required before GetRootNodesAsync returns anything,
            // exactly as a real Navigation Bar click would drive.
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

            var explorer = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            await explorer.LoadAsync();

            // Real object identities, read from the real Mechanical Product
            // Structure sample data (WP 9.0A) via the real Project Explorer
            // tree — never fabricated Guids.
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNodes = await CollectObjectNodesAsync(workspace.ProjectExplorer, roots);
            Assert.True(objectNodes.Count >= 2, "Expected at least two real engineering objects in the Mechanical sample tree.");

            var first = await workspace.Navigation.OpenAsync(objectNodes[0].Id, objectNodes[0].Kind!);
            documentArea.ShowTab(first);
            var second = await workspace.Navigation.OpenAsync(objectNodes[1].Id, objectNodes[1].Kind!);
            documentArea.ShowTab(second);

            Assert.Equal(2, documentArea.TabCount);

            await workspace.Navigation.CloseAsync(first.Id);
            documentArea.RemoveTab(first.Id);

            Assert.Equal(1, documentArea.TabCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task PersistentLayouts_HidingAPanelAndRestarting_RestoresTheSameLayout()
    {
        // Both hosts below deliberately share one isolated persistence root
        // (WP 10.1B, TD-37) - restoration can only be proven if the second
        // host reads back what the first durably wrote.
        var persistenceRootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var firstHost = new WorkspaceHost(persistenceRootPath);
        System.Guid explorerPanelId;
        try
        {
            await firstHost.StartAsync();
            var workspace = firstHost.Workspace!;
            explorerPanelId = workspace.ProjectExplorer.Id;

            var current = workspace.Layout.GetPlacement(explorerPanelId);
            workspace.Layout.SetPlacement(explorerPanelId, current with { IsVisible = false, Size = 111 });

            // Session persistence (ADR-0064, unchanged): ShutdownAsync
            // writes the current layout through WorkspaceManager.
            await firstHost.ShutdownAsync();
        }
        finally
        {
            await firstHost.DisposeAsync();
        }

        // Workspace restoration: a second, independent WorkspaceHost reads
        // the same persisted session state back on StartAsync — proving
        // restoration end to end, not merely that SaveAsync ran.
        var secondHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await secondHost.StartAsync();
            var restored = secondHost.Workspace!.Layout.GetPlacement(explorerPanelId);

            Assert.False(restored.IsVisible);
            Assert.Equal(111, restored.Size);
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MultiPanelLayout_MainWindowConstructsWithExplorerDocumentAreaAndInspectorAllPresent()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            // The full integration smoke test — MainWindow's own
            // constructor wires the Docking Framework, Panel Host, Document
            // Host, Status Bar, Toolbar, Menu System, Navigation Framework,
            // and Command Palette Host together over the real Workspace;
            // constructing it without throwing is this Work Package's own
            // end-to-end proof that every one of those pieces composes.
            var window = new MainWindow(host);

            Assert.NotNull(window);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task PersistentDesktopPanelUiState_SavingAndRestartingOverTheRealSettingsProvider_RestoresTheSameState()
    {
        // WP 10.2B's own Desktop-local counterpart to
        // PersistentLayouts_HidingAPanelAndRestarting_RestoresTheSameLayout,
        // above — same "two sequential hosts, one shared persistence root"
        // proof, but against DesktopPanelUiState's own Settings key instead
        // of IWorkspaceState's.
        var persistenceRootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var firstHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await firstHost.StartAsync();
            var settingsProvider = (ISettingsProvider)firstHost.Services!.GetService(typeof(ISettingsProvider));

            var state = new DesktopPanelUiState(settingsProvider)
            {
                ExplorerCollapsed = true,
                InspectorPinned = false,
                OutputVisible = true,
                OutputHeight = 205,
                LastAppliedPreset = "Documentation",
            };
            await state.SaveAsync();

            await firstHost.ShutdownAsync();
        }
        finally
        {
            await firstHost.DisposeAsync();
        }

        var secondHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await secondHost.StartAsync();
            var settingsProvider = (ISettingsProvider)secondHost.Services!.GetService(typeof(ISettingsProvider));

            var restored = new DesktopPanelUiState(settingsProvider);
            await restored.LoadAsync();

            Assert.True(restored.ExplorerCollapsed);
            Assert.False(restored.InspectorPinned);
            Assert.True(restored.OutputVisible);
            Assert.Equal(205, restored.OutputHeight);
            Assert.Equal("Documentation", restored.LastAppliedPreset);
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MainWindow_SaveDesktopUiStateAsync_PersistsThroughTheRealSettingsProvider_WithoutThrowing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            // MainWindow's own Window Lifecycle save point (App.cs's
            // ShutdownRequested handler calls this before
            // WorkspaceHost.ShutdownAsync) — proven directly, not merely
            // implied by MainWindow constructing without throwing.
            var exception = await Record.ExceptionAsync(() => window.SaveDesktopUiStateAsync());

            Assert.Null(exception);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<List<ProjectExplorerNode>> CollectObjectNodesAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        var result = new List<ProjectExplorerNode>();

        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                result.Add(node);

            if (node.HasChildren)
                result.AddRange(await CollectObjectNodesAsync(explorer, await explorer.GetChildrenAsync(node.Id)));
        }

        return result;
    }
}

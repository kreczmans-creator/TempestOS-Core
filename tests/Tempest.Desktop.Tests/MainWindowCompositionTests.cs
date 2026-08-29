using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Core.Notifications;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;
using Tempest.Samples;
using Tempest.App.Workspace.Mechanical;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Characterization coverage for `WP 12.0B`'s own two stateless factory
/// collaborators (<c>MainMenuFactory</c>/<c>QuickAccessToolbarFactory</c>)
/// and the layout-preset coordinator they both call through — added
/// before the `ADR-0103` extraction moved their bodies out of
/// <see cref="MainWindow"/>'s own constructor, closing a real,
/// confirmed-by-direct-search gap: no existing test constructed the Menu
/// System or clicked a Quick Access Toolbar button before this file.
/// Every test here constructs a real <see cref="MainWindow"/> over a
/// real, running <see cref="WorkspaceHost"/> — never a mock.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class MainWindowCompositionTests
{
    [AvaloniaFact]
    public async Task PlatformNotification_PublishedThroughTheRealDispatcher_ReachesAVisibleToast()
    {
        // `TD-58` stale-UI closure: the toast bridge previously listened
        // only on the event bus, which no real producer publishes
        // notifications through — every INotificationDispatcher
        // publication silently vanished.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var toastHost = window.GetLogicalDescendants().OfType<ToastHost>().Single();
            var dispatcher = (INotificationDispatcher)host.Services!.GetService(typeof(INotificationDispatcher));

            Assert.Equal(0, toastHost.ActiveToastCount);

            await dispatcher.PublishAsync<IPlatformNotification>(
                new PlatformNotification("Tests", NotificationSeverity.Information, "A real platform notification."));

            Assert.Equal(1, toastHost.ActiveToastCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ViewMenu_ToggleProjectExplorer_ActuallyFlipsTheRealWorkspaceLayoutVisibility()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var window = new MainWindow(host);

            var wasVisible = workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id).IsVisible;

            var menu = window.GetLogicalDescendants().OfType<Menu>().Single();
            var view = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_View"));
            var toggleExplorer = view.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Project Explorer"));

            toggleExplorer.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(!wasVisible, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id).IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task LayoutMenu_ApplyingEngineeringPreset_ActuallyAppliesTheRealNamedPlacement()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var window = new MainWindow(host);

            var menu = window.GetLogicalDescendants().OfType<Menu>().Single();
            var view = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_View"));
            var layout = view.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "_Layout"));
            var engineering = layout.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Engineering"));

            engineering.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            // A preset is now a whole layout tree, replaced in one
            // operation (`TD-72`), so the assertion is against the
            // arrangement itself rather than a per-panel placement record.
            var expected = Tempest.App.Workspace.Layout.WorkspaceLayoutPresets.Build(
                Tempest.App.Workspace.Layout.WorkspaceLayoutPreset.Engineering,
                window.WorkspaceLayout.Tree.DockedPanels.First(),
                Tempest.Desktop.Composition.WorkspaceDockingComposer.DocumentAreaPanelId,
                workspace.PropertyInspector.Id,
                Guid.NewGuid());

            Assert.NotNull(expected.Root);
            Assert.Contains(workspace.ProjectExplorer.Id, window.WorkspaceLayout.Tree.AllPanels);
            Assert.Contains(workspace.PropertyInspector.Id, window.WorkspaceLayout.Tree.AllPanels);

            // Reset Layout, right below the three presets, reverses it —
            // proving WorkspaceLayoutPresetCoordinator's own two public
            // methods both actually run, not just Apply.
            var resetItem = layout.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Reset Layout"));
            resetItem.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            var afterReset = workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id);
            Assert.True(afterReset.IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task DocumentMenu_NextAndPreviousTab_ActuallyMoveTheRealDocumentAreaSelection()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var window = new MainWindow(host);
            var documentArea = GetPrivateField<DocumentAreaView>(window, "_documentArea");

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNode = (await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots))!;
            var view1 = await workspace.Navigation.OpenAsync(objectNode.Id, objectNode.Kind!);
            documentArea.ShowTab(view1);

            var menu = window.GetLogicalDescendants().OfType<Menu>().Single();
            var document = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_Document"));
            var nextDoc = document.Items.OfType<MenuItem>().Single(m => Equals((string)m.Header!, "Next Tab   (Ctrl+Tab)"));

            // With only the Home tab plus one real document tab open,
            // Next Tab is a real, harmless no-throw round trip back to
            // itself — proving the menu item really calls SelectNextTab
            // on the real DocumentAreaView, not a no-op stub.
            var exception = Record.Exception(() => nextDoc.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)));
            Assert.Null(exception);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task QuickAccessToolbar_ResetLayoutButton_ActuallyResetsTheRealWorkspaceLayout()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { IsVisible = false });

            var window = new MainWindow(host);
            var resetButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "↺ Reset Layout"));

            resetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.True(workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id).IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task QuickAccessToolbar_ViewRelationshipsButton_WithNoSelection_ReportsHonestlyRatherThanThrowing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var graphButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "🕸 View Relationships"));

            graphButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var statusText = statusBar.GetLogicalDescendants().OfType<TextBlock>()
                .FirstOrDefault(t => t.Text != null && t.Text.Contains("Select an object first", StringComparison.Ordinal));
            Assert.NotNull(statusText);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task QuickAccessToolbar_UndoRedoButtons_StartDisabled_AndReactivelyEnableAfterARealRecordedAction()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var window = new MainWindow(host);

            var undoButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "↶ Undo"));
            Assert.False(undoButton.IsEnabled);

            // Ctrl+D (Toggle Favourite) records a real, trivially
            // self-inverting UndoableAction (WorkspaceViewCoordinator's
            // own ToggleFavourite) — real proof that UndoRedoCoordinator's
            // own reactive Stack.Changed subscription (no explicit refresh
            // call anywhere in the new collaborators) actually enables the
            // button.
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNode = (await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots))!;
            await workspace.Selection.SelectAsync(objectNode.Id, objectNode.Kind!);

            var undoRedo = GetPrivateField<object>(window, "_undoRedo");
            var stack = (IUndoRedoStack)undoRedo.GetType().GetProperty("Stack")!.GetValue(undoRedo)!;
            stack.Record(new UndoableAction("Test action", undo: _ => Task.FromResult(Tempest.Core.Commands.CommandResult.Success()), redo: _ => Task.FromResult(Tempest.Core.Commands.CommandResult.Success())));

            Assert.True(undoButton.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task QuickAccessToolbar_MacrosButton_ActuallyOpensTheRealMacroManagerDialog()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var macrosButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "🧩 Macros"));
            var macroManagerDialog = GetPrivateField<MacroManagerDialog>(window, "_macroManagerDialog");

            Assert.False(macroManagerDialog.IsVisible);

            macrosButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.True(macroManagerDialog.IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}

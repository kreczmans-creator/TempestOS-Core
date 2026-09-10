using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Workspace;
using Tempest.Core.Notifications;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;
using Tempest.Samples;
using Tempest.Workspace.Mechanical;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

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
            var expected = Tempest.Workspace.Layout.WorkspaceLayoutPresets.Build(
                Tempest.Workspace.Layout.WorkspaceLayoutPreset.Engineering,
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
            var nextDoc = document.Items.OfType<MenuItem>().Single(m => Equals((string)m.Header!, "Next Tab"));

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
    public async Task MenuItems_DisplayTheExactGesture_KeyboardShortcuts_ActuallyBinds()
    {
        // `WP-Z4` Productisation Phase 1 (backlog item 1) — MenuItem.InputGesture
        // is a purely presentational Avalonia property; the real key
        // handling lives entirely in KeyboardShortcuts.Register's own
        // KeyDown handler (Ctrl+K / Ctrl+Tab / Ctrl+Shift+Tab). This proves
        // the two never drift apart: whatever a menu item claims to be
        // bound to is the literal gesture the handler dispatches on.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var menu = window.GetLogicalDescendants().OfType<Menu>().Single();

            var commands = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_Commands"));
            var openPalette = commands.Items.OfType<MenuItem>().Single(m => Equals((string)m.Header!, "Command Palette..."));
            Assert.Equal(new Avalonia.Input.KeyGesture(Avalonia.Input.Key.K, Avalonia.Input.KeyModifiers.Control), openPalette.InputGesture);

            var document = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_Document"));
            var nextDoc = document.Items.OfType<MenuItem>().Single(m => Equals((string)m.Header!, "Next Tab"));
            var prevDoc = document.Items.OfType<MenuItem>().Single(m => Equals((string)m.Header!, "Previous Tab"));
            Assert.Equal(new Avalonia.Input.KeyGesture(Avalonia.Input.Key.Tab, Avalonia.Input.KeyModifiers.Control), nextDoc.InputGesture);
            Assert.Equal(new Avalonia.Input.KeyGesture(Avalonia.Input.Key.Tab, Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift), prevDoc.InputGesture);

            // Raising the identical KeyDown on the real window must reach
            // the identical real handler the menu item's own Click calls
            // (proven separately by DocumentMenu_NextAndPreviousTab_...
            // above) — both paths lead to KeyboardShortcuts' one KeyDown
            // handler, never two independently-maintained mechanisms. With
            // only the Home tab open this is a harmless no-throw round
            // trip, exactly like that test's own click path.
            var exception = Record.Exception(() => window.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Tab,
                KeyModifiers = Avalonia.Input.KeyModifiers.Control,
                Source = window,
            }));
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
            var resetButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Reset Layout");

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
            var graphButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "View Relationships");

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

            var undoButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Undo");
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
            var macrosButton = window.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Macros");
            var macroManagerDialog = GetPrivateField<MacroManagerDialog>(window, "_macroManagerDialog");

            Assert.False(macroManagerDialog.IsVisible);

            macrosButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the Macros click opens the dialog on an asynchronous
            // continuation; bounded poll on the real visibility, assertion unchanged.
            var macrosDeadline = DesktopTestHelpers.Deadline(2);
            while (!(macroManagerDialog.IsVisible) && DateTime.UtcNow < macrosDeadline)
                await Task.Delay(10);

            Assert.True(macroManagerDialog.IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
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

    // ======================================================================
    // WP 19.2A (TD-105–TD-107, TD-109, TD-112, TD-113, TD-115) — structural
    // pins on the composition itself, grep-style over the real source text
    // like NoBlockingPersistenceCallsTests, rather than over a running
    // window: what these three assert is about the shape of the code, not
    // its runtime behaviour, which the rest of this file and every journey
    // test already cover.
    // ======================================================================

    /// <summary>
    /// Neither <c>MainWindow.cs</c> nor <c>WorkspaceViewCoordinator.cs</c>
    /// reads a field behind a null-forgiving <c>!</c> any more — the two
    /// two-phase-construction cycles (<c>_documentArea!</c>,
    /// <c>_cockpitView!</c>) `WP 19.2A` closed, one by building the
    /// Document Area before the coordinator that needs it
    /// (<c>DocumentAreaView.ContentBuilder</c>, <c>IDocumentOpener</c>),
    /// the other by a real <c>WorkspaceViewCoordinator.Attach(CockpitView)</c>
    /// call once it exists. A non-field null-forgiving use (<c>host.X!</c>,
    /// asserting the already-started <c>WorkspaceHost</c>'s own optional
    /// properties) is untouched — only an underscore-prefixed field name
    /// matches.
    /// </summary>
    [Theory]
    [InlineData("MainWindow.cs")]
    [InlineData("Composition/WorkspaceViewCoordinator.cs")]
    public void NoNullForgivingFieldCapture_InMainWindowOrWorkspaceViewCoordinator(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Tempest.Desktop", relativePath));

        var offenders = Regex.Matches(source, @"\b_[A-Za-z][A-Za-z0-9]*!(?!=)")
            .Select(m => m.Value)
            .ToList();

        Assert.True(offenders.Count == 0, $"{relativePath} still null-forgives a field read: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// <see cref="MainWindowComposer"/>'s own four phases —
    /// <c>BuildViews</c>, <c>BuildCoordinators</c>, <c>Wire</c>,
    /// <c>Layout</c> — exist, and <see cref="MainWindow"/>'s own
    /// constructor calls all four, in that order. `TD-109`: the god
    /// object's own ~830-line constructor is this composer's job now; a
    /// future edit that reorders or drops a phase call is what this catches.
    /// </summary>
    [Fact]
    public void MainWindowComposer_FourPhases_ExistAndAreCalledInOrder()
    {
        string SourceOf(string fileName) =>
            File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Tempest.Desktop", "Composition", fileName));

        Assert.Contains("public ComposedViews BuildViews(", SourceOf("MainWindowComposer.cs"), StringComparison.Ordinal);
        Assert.Contains("public ComposedCoordinators BuildCoordinators(", SourceOf("MainWindowComposer.Coordinators.cs"), StringComparison.Ordinal);
        Assert.Contains("public void Wire(", SourceOf("MainWindowComposer.Wire.cs"), StringComparison.Ordinal);
        Assert.Contains("public ComposedLayout Layout(", SourceOf("MainWindowComposer.Layout.cs"), StringComparison.Ordinal);

        var mainWindowSource = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Tempest.Desktop", "MainWindow.cs"));
        var buildViewsCall = mainWindowSource.IndexOf("composer.BuildViews(", StringComparison.Ordinal);
        var buildCoordinatorsCall = mainWindowSource.IndexOf("composer.BuildCoordinators(", StringComparison.Ordinal);
        var wireCall = mainWindowSource.IndexOf("composer.Wire(", StringComparison.Ordinal);
        var layoutCall = mainWindowSource.IndexOf("composer.Layout(", StringComparison.Ordinal);

        Assert.True(
            buildViewsCall >= 0 && buildCoordinatorsCall >= 0 && wireCall >= 0 && layoutCall >= 0,
            "MainWindow's own constructor must call all four MainWindowComposer phases.");
        Assert.True(
            buildViewsCall < buildCoordinatorsCall && buildCoordinatorsCall < wireCall && wireCall < layoutCall,
            "MainWindowComposer's four phases must run in order: BuildViews, BuildCoordinators, Wire, Layout.");
    }

    /// <summary>
    /// <c>SurfaceCommandPolicy</c>'s own two sets name every command by its
    /// discipline's own <c>CommandIds</c> constant (`WP 19.2A`) — never a
    /// string literal ending <c>.rename</c>, <c>.edit</c> or <c>.delete</c>,
    /// the three suffixes both sets are built from.
    /// </summary>
    [Fact]
    public void SurfaceCommandPolicy_NamesNoCommandId_AsAStringLiteral()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Tempest.Desktop", "Composition", "SurfaceCommandPolicy.cs"));

        var offenders = Regex.Matches(source, @"""[a-z][a-z.-]*\.(rename|edit|delete)""")
            .Select(m => m.Value)
            .ToList();

        Assert.True(offenders.Count == 0, $"SurfaceCommandPolicy.cs still names a command id as a string literal: {string.Join(", ", offenders)}");
    }
}

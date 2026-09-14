using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.Settings;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.10O` — the Product Owner's own request ("when I'm not using
/// those menus I get the maximum real estate on the screens for working
/// in"): the rail and each area tree column collapse on demand, at any
/// window width, and stay collapsed across a restart. Mirrors
/// <c>ResponsiveWorkspaceTests</c>' own established ribbon-minimise
/// pattern (`TD-70`) — a real <see cref="WorkspaceHost"/>, a real
/// <see cref="MainWindow"/>, never the panel under test built directly.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ShellCollapseTests
{
    private const double WideWidth = 1600;
    private const double NarrowWidth = 900;

    // ----------------------------------------------------------------
    // The rail
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task Rail_Collapse_FoldsToIconWidth_KeepsToolTipsAndNames_AndPersistsTheFlag()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            LayOut(window);

            Assert.False(rail.IsCollapsed);
            Assert.Equal(DesignTokens.RailWidth, rail.Bounds.Width, precision: 1);

            rail.ToggleCollapsed();
            LayOut(window);

            Assert.True(rail.IsCollapsed);
            Assert.Equal(DesignTokens.RailCompactWidth, rail.Bounds.Width, precision: 1);

            // Every module button — folded exactly as the responsive
            // compact mode already folds it — keeps its own tooltip and
            // automation name (the brief's "the icons keep their
            // tooltips and automation names, exactly as compact mode
            // does today").
            var home = rail.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Home");
            Assert.NotNull(ToolTip.GetTip(home));

            // The collapse chevron itself names its own next action.
            var chevron = rail.GetLogicalDescendants().OfType<Button>().Single(b => (AutomationProperties.GetName(b) ?? string.Empty) == "Expand navigation");
            Assert.NotNull(ToolTip.GetTip(chevron));

            // Persisted for the next session, the identical `TD-70`
            // mechanism the ribbon's own minimise state already uses.
            var uiState = new DesktopPanelUiState((ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider)));
            await window.SaveDesktopUiStateAsync();
            await uiState.LoadAsync();
            Assert.True(uiState.RailCollapsed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Rail_CollapsedState_IsRestoredOnRelaunch_BeforeFirstRender()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);
            window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single().ToggleCollapsed();
            await window.SaveDesktopUiStateAsync();
            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            // Restored at construction, before any layout pass runs —
            // nothing jumps once the window is shown.
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            Assert.True(rail.IsCollapsed);

            LayOut(window);
            Assert.Equal(DesignTokens.RailCompactWidth, rail.Bounds.Width, precision: 1);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Rail_WidenAndNarrowAcrossCompactThreshold_ResponsiveRuleWinsBelow_ToggleStateWinsAbove()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host) { Width = WideWidth, Height = 900 };
            window.Show();
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();

            // Wide, expanded (the toggle's own default): full width.
            Resize(window, WideWidth, 900);
            Assert.False(rail.IsCompact);
            Assert.Equal(DesignTokens.RailWidth, rail.Bounds.Width, precision: 1);

            // Narrow: the responsive rule folds the rail even though the
            // manual toggle still says expanded.
            Resize(window, NarrowWidth, 700);
            Assert.True(rail.IsCompact);
            Assert.False(rail.IsCollapsed);
            Assert.Equal(DesignTokens.RailCompactWidth, rail.Bounds.Width, precision: 1);

            // Wide again: the rail returns to the toggle's own (expanded) state.
            Resize(window, WideWidth, 900);
            Assert.False(rail.IsCompact);
            Assert.Equal(DesignTokens.RailWidth, rail.Bounds.Width, precision: 1);

            // Now the manual toggle says collapsed.
            rail.SetCollapsed(true);
            LayOut(window);
            Assert.Equal(DesignTokens.RailCompactWidth, rail.Bounds.Width, precision: 1);

            // Narrow: still folded (the responsive rule agrees).
            Resize(window, NarrowWidth, 700);
            Assert.True(rail.IsCompact);
            Assert.Equal(DesignTokens.RailCompactWidth, rail.Bounds.Width, precision: 1);

            // Wide again: the rail stays folded — the toggle's own state
            // wins now that the responsive rule no longer forces it.
            Resize(window, WideWidth, 900);
            Assert.False(rail.IsCompact);
            Assert.True(rail.IsCollapsed);
            Assert.Equal(DesignTokens.RailCompactWidth, rail.Bounds.Width, precision: 1);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ----------------------------------------------------------------
    // The Command Palette route + Ctrl+B
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task CommandPalette_ListsCollapseNavigation_AndInvokingItTogglesTheRail()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            Assert.Contains(registry.Items, d => d.DisplayName == "Collapse navigation");
            Assert.False(rail.IsCollapsed);

            var invocation = await registry.InvokeAsync("shell.toggleNavigationRail", CommandContext.Empty, prompt: null, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            Assert.True(rail.IsCollapsed);

            var again = await registry.InvokeAsync("shell.toggleNavigationRail", CommandContext.Empty, prompt: null, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(CommandOutcome.Executed, again.Outcome);
            Assert.False(rail.IsCollapsed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ----------------------------------------------------------------
    // Each area's own tree column
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task ProjectsTree_Collapse_FoldsToTheStrip_GrowsTheRightPane_AndPersistsPerArea()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host) { Width = WideWidth, Height = 900 };
            window.Show();
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var area = window.GetLogicalDescendants().OfType<ProjectsAreaView>().Single();
            var (columnWidth, detailWidth) = AssertCollapsed(window, area, area.SetTreeCollapsed, () => area.IsTreeCollapsed);

            var uiState = new DesktopPanelUiState((ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider)));
            await window.SaveDesktopUiStateAsync();
            await uiState.LoadAsync();
            Assert.True(uiState.ProjectsTreeCollapsed);

            AssertExpanded(window, area, area.SetTreeCollapsed, () => area.IsTreeCollapsed, columnWidth, detailWidth);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task EngineeringTree_Collapse_FoldsToTheStrip_GrowsTheRightPane_AndPersistsPerArea()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host) { Width = WideWidth, Height = 900 };
            window.Show();
            var navigator = host.ShellNavigator!;

            await navigator.GoToModuleAsync(Tempest.Workspace.Shell.ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            var (columnWidth, detailWidth) = AssertCollapsed(window, area, area.SetTreeCollapsed, () => area.IsTreeCollapsed);

            var uiState = new DesktopPanelUiState((ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider)));
            await window.SaveDesktopUiStateAsync();
            await uiState.LoadAsync();
            Assert.True(uiState.EngineeringTreeCollapsed);

            AssertExpanded(window, area, area.SetTreeCollapsed, () => area.IsTreeCollapsed, columnWidth, detailWidth);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task BusinessTree_Collapse_FoldsToTheStrip_GrowsTheRightPane_AndPersistsPerArea()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host) { Width = WideWidth, Height = 900 };
            window.Show();
            var navigator = host.ShellNavigator!;

            await navigator.GoToModuleAsync(Tempest.Workspace.Shell.ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var area = window.GetLogicalDescendants().OfType<BusinessAreaView>().Single();
            var (columnWidth, detailWidth) = AssertCollapsed(window, area, area.SetTreeCollapsed, () => area.IsTreeCollapsed);

            var uiState = new DesktopPanelUiState((ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider)));
            await window.SaveDesktopUiStateAsync();
            await uiState.LoadAsync();
            Assert.True(uiState.BusinessTreeCollapsed);

            AssertExpanded(window, area, area.SetTreeCollapsed, () => area.IsTreeCollapsed, columnWidth, detailWidth);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The shared "collapse" half every area's own tree-collapse test above
    /// makes: collapsing shrinks the tree column to
    /// <see cref="CollapsibleColumn.CollapsedWidth"/> and the right pane's
    /// own width grows by exactly the width freed. Returns the column's and
    /// the right pane's own pre-collapse widths, so the caller can check
    /// persistence while still collapsed and then assert
    /// <see cref="AssertExpanded"/> restores exactly those widths. Reflects
    /// into each area view's own private <c>_treeColumn</c>/<c>_detail</c>
    /// fields — every area view names them identically (`WP 19.10O`'s own
    /// "one shared control" design) — rather than adding a test-only public
    /// surface neither production code nor the brief asks for.
    /// </summary>
    private static (double ColumnWidth, double DetailWidth) AssertCollapsed(Window window, Control areaView, Action<bool> setTreeCollapsed, Func<bool> isCollapsed)
    {
        LayOut(window);

        var column = GetPrivateField<CollapsibleColumn>(areaView, "_treeColumn");
        var detail = GetPrivateField<ContentControl>(areaView, "_detail");

        Assert.False(isCollapsed());
        var widthBefore = column.Bounds.Width;
        var detailWidthBefore = detail.Bounds.Width;
        Assert.Equal(CollapsibleColumn.ExpandedWidth, widthBefore, precision: 1);

        setTreeCollapsed(true);
        LayOut(window);

        Assert.True(isCollapsed());
        var widthAfter = column.Bounds.Width;
        var detailWidthAfter = detail.Bounds.Width;
        Assert.Equal(CollapsibleColumn.CollapsedWidth, widthAfter, precision: 1);
        Assert.Equal(detailWidthBefore + (widthBefore - widthAfter), detailWidthAfter, precision: 1);

        return (widthBefore, detailWidthBefore);
    }

    /// <summary>The "expand" half — the previous width comes back, and the right pane shrinks back by the identical amount.</summary>
    private static void AssertExpanded(Window window, Control areaView, Action<bool> setTreeCollapsed, Func<bool> isCollapsed, double expectedColumnWidth, double expectedDetailWidth)
    {
        setTreeCollapsed(false);
        LayOut(window);

        Assert.False(isCollapsed());
        var column = GetPrivateField<CollapsibleColumn>(areaView, "_treeColumn");
        var detail = GetPrivateField<ContentControl>(areaView, "_detail");
        Assert.Equal(expectedColumnWidth, column.Bounds.Width, precision: 1);
        Assert.Equal(expectedDetailWidth, detail.Bounds.Width, precision: 1);
    }

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(window.Width, window.Height));
            window.Arrange(new Rect(0, 0, window.Width, window.Height));
        }
    }

    /// <summary>Resizes the real window and runs a real layout pass, mirroring <c>MainWindowResizeTests.Resize</c>.</summary>
    private static void Resize(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
        }
    }
}

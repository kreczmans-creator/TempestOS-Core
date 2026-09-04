using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-70` closure, ribbon half — the ribbon must be minimisable so it
/// does not eat vertical space on a laptop, and that choice must persist.
/// </summary>
/// <remarks>
/// The side-dock half of `TD-70` moved to
/// <c>WorkspaceLayoutHostTests</c> when `TD-72` replaced the fixed
/// docking geometry. The guarantee is unchanged — the working pane is
/// never starved by side panels — but it is now expressed against the
/// layout tree, so it holds for any arrangement a user builds rather than
/// only for three named docks, and it is for the first time actually
/// wired to the running window's own resize rather than reachable only
/// from a test (`TD-83`).
/// </remarks>
/// <remarks>
/// <b>Found and fixed, `WP 15.2A`:</b> this class builds three real
/// <see cref="WorkspaceHost"/> instances (via
/// <see cref="WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath"/>)
/// but, unlike every other class that does so, carried no
/// <see cref="CollectionAttribute"/> — so xUnit ran it in its own,
/// unserialised default collection, exposed to exactly the process-wide
/// headless-dispatcher hazard <see cref="WorkspacePersistenceCollection"/>'s
/// own <c>DisableParallelization</c> exists to prevent. Joining that
/// collection also brings this class under
/// <see cref="PersistenceRootCleanupFixture"/>'s own cleanup (`TD-120`).
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ResponsiveWorkspaceTests
{
    // ----------------------------------------------------------------
    // Ribbon minimise
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task Ribbon_Minimise_HidesCommandContent_ButKeepsEveryTabReachable()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (Tempest.Core.Commands.ICommandRegistry)host.Services!.GetService(typeof(Tempest.Core.Commands.ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });
            var tabs = (TabControl)ribbon.Content!;
            var tabCount = tabs.Items.Count;

            Assert.False(ribbon.IsCollapsed);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.True(((Control)t.Content!).IsVisible));

            ribbon.SetCollapsed(true);

            Assert.True(ribbon.IsCollapsed);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.False(((Control)t.Content!).IsVisible));

            // No command becomes unreachable: every tab header survives.
            Assert.Equal(tabCount, tabs.Items.Count);

            ribbon.SetCollapsed(false);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.True(((Control)t.Content!).IsVisible));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Ribbon_MinimisedState_SurvivesARebuild()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (Tempest.Core.Commands.ICommandRegistry)host.Services!.GetService(typeof(Tempest.Core.Commands.ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            ribbon.SetCollapsed(true);
            ribbon.Rebuild();

            var tabs = (TabControl)ribbon.Content!;
            Assert.True(ribbon.IsCollapsed);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.False(((Control)t.Content!).IsVisible));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Ribbon_MinimiseIsReachableFromTheViewMenu_AndIsPersisted()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var ribbon = window.GetLogicalDescendants().OfType<RibbonView>().Single();

            var menu = window.GetLogicalDescendants().OfType<Menu>().Single();
            var view = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_View"));
            var minimise = view.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Minimise Ribbon"));

            Assert.False(ribbon.IsCollapsed);
            minimise.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(ribbon.IsCollapsed);

            // Persisted for the next session.
            var uiState = new DesktopPanelUiState((Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider)));
            await window.SaveDesktopUiStateAsync();
            await uiState.LoadAsync();
            Assert.True(uiState.RibbonCollapsed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}

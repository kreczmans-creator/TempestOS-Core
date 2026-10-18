using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `v0.24.0` Product Owner review's navigation change: disciplines are
/// picked from the Engineering tree (standalone) or the Structure tab's
/// discipline list (in a project), never from a tab strip inside the
/// command bar — and the tree stays on screen, so there is always a way
/// back out.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringDisciplineNavigationTests
{
    [AvaloniaFact]
    public async Task PickingADiscipline_KeepsTheTreeBesideTheSurface_AndTheDashboardNodeLeadsBackOut()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            await navigator.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();

            var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            var ribbon = window.GetLogicalDescendants().OfType<RibbonView>().Single();
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();

            area.SelectNode(EngineeringDisciplines.NavigatorName("Requirements"));
            await WaitUntilAsync(() => navigator.Current.Area == ShellArea.Engineering && ribbon.CurrentCategory == "Requirements");
            await window.RenderCurrentModuleAsync();

            // The surface sits inside the Engineering view, beside its tree.
            Assert.True(navigator.Current.IsStandaloneEngineering);
            Assert.Same(area, ribbon.FindLogicalAncestorOfType<EngineeringAreaView>());
            Assert.Equal("Requirements", ribbon.CurrentCategory);
            Assert.Equal(ShellArea.EngineeringDepartment, rail.HighlightedArea);

            // Another discipline switches the command bar without leaving.
            area.SelectNode(EngineeringDisciplines.NavigatorName("Calculations"));
            await WaitUntilAsync(() => ribbon.CurrentCategory == "Calculations");
            Assert.True(navigator.Current.IsStandaloneEngineering);

            // The way back out: the dashboard node, right there in the tree.
            area.SelectNode("Dashboard + Reports");
            await WaitUntilAsync(() => navigator.Current.Area == ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            Assert.Null(ribbon.FindLogicalAncestorOfType<EngineeringAreaView>());
            Assert.Equal(ShellArea.EngineeringDepartment, rail.HighlightedArea);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheStructureTab_ListsTheDisciplines_AndPickingOneSwitchesTheCommandBar()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var project = await host.ProjectDirectory!.CreateAsync("P-0124", "Discipline list");

            await navigator.OpenProjectAsync(project.Id);
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            var ribbon = window.GetLogicalDescendants().OfType<RibbonView>().Single();
            var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Single();
            var list = workspace.GetLogicalDescendants().OfType<ListBox>()
                .Single(l => AutomationProperties.GetName(l) == "Disciplines");

            Assert.Equal(
                EngineeringDisciplines.All.Select(EngineeringDisciplines.NavigatorName),
                list.Items.OfType<ListBoxItem>().Select(i => AutomationProperties.GetName(i)));

            list.SelectedItem = list.Items.OfType<ListBoxItem>().Single(i => Equals(i.Tag, "Verification"));
            await WaitUntilAsync(() => ribbon.CurrentCategory == "Verification");

            Assert.Equal(project.Id, navigator.Current.ProjectId);
            Assert.Equal(ShellArea.Projects, window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single().HighlightedArea);

            // The bar following a change made elsewhere moves the list too.
            ribbon.SelectTabForArea("Mechanical Product Structure");
            Assert.Equal("Mechanical", (list.SelectedItem as ListBoxItem)?.Tag);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheCommandBar_HidesItsDisciplineTabStrip_AndItsOwnToggleRestoresAMinimisedBar()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            await navigator.GoToStandaloneEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ribbon = window.GetLogicalDescendants().OfType<RibbonView>().Single();
            Assert.DoesNotContain(
                ribbon.Tabs.GetVisualDescendants().OfType<TabItem>(),
                t => t.IsEffectivelyVisible);

            var toggle = ribbon.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Hide commands");
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.True(ribbon.IsCollapsed);
            Assert.Equal("Show commands", AutomationProperties.GetName(toggle));

            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.False(ribbon.IsCollapsed);
            window.Close();
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "The condition never became true.");
    }
}

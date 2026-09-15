using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 20.1B` (`TD-181`) Desktop acceptance: a Calculation created
/// under a project shows up, from creation, as its own heading with its
/// own row on both surfaces the Product Owner's own sketch named —
/// <see cref="EngineeringAreaView"/>'s Tasks node and
/// <see cref="EngineeringDashboardView"/> — each row opening the
/// calculation right up and offering Complete, which removes it from
/// both.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CalculationTaskJourneyTests
{
    [AvaloniaFact]
    public async Task CalculationUnderAProject_ShowsOnBothSurfaces_OpensRightUp_CompleteRemovesItFromBoth()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var domain = Resolve<EngineeringDomainContext>(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-CALC-TASK", "Calculation Task Project");
            var calculation = await CreateCalculationAsync(domain, project.Id, "Bracket capacity check");

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            // ---------------------------------------------------------
            // Engineering -> Tasks: the Calculations heading, a row,
            // Open (right up) and Complete.
            // ---------------------------------------------------------
            await navigator.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            var engineeringArea = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            engineeringArea.SelectNode("Tasks");
            await RenderUntilAsync(window, () =>
                engineeringArea.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Calculations (1)"));
            LayOut(window);

            var tasksText = string.Join(" | ", engineeringArea.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("Bracket capacity check", tasksText, StringComparison.Ordinal);

            var openInTasks = engineeringArea.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationName(b) == "Open Bracket capacity check");
            openInTasks.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => window.LastOpenPhase.StartsWith("opened (", StringComparison.Ordinal));
            Assert.Contains(calculation.Id.ToString("N"), window.LastOpenPhase, StringComparison.OrdinalIgnoreCase);

            // ---------------------------------------------------------
            // Opening right up switches the document area to the
            // Calculation's own editor tab — back into the Engineering
            // rail area to reach the dashboard's own Calculations section.
            // ---------------------------------------------------------
            await navigator.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            engineeringArea = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            engineeringArea.SelectNode("Dashboard + Reports");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<EngineeringDashboardView>().Any());
            LayOut(window);

            var dashboard = window.GetLogicalDescendants().OfType<EngineeringDashboardView>().Single();
            var dashboardText = string.Join(" | ", dashboard.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("Bracket capacity check", dashboardText, StringComparison.Ordinal);

            var completeOnDashboard = dashboard.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationName(b) == "Complete Bracket capacity check");
            completeOnDashboard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var domainForWait = domain;
            await RenderUntilAsync(window, () =>
                ((Calculation)domainForWait.Repository.FindAsync(calculation.Id).GetAwaiter().GetResult()!).Completed);
            LayOut(window);

            Assert.DoesNotContain(
                dashboard.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Bracket capacity check");

            // ---------------------------------------------------------
            // Completing it there also clears it from the Tasks node.
            // ---------------------------------------------------------
            engineeringArea.SelectNode("Tasks");
            await RenderUntilAsync(window, () =>
                engineeringArea.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Calculations (0)"));
            LayOut(window);

            Assert.DoesNotContain(
                engineeringArea.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Bracket capacity check");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<Calculation> CreateCalculationAsync(EngineeringDomainContext domain, Guid projectId, string title)
    {
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", domain, (doc, rev) => new Calculation(doc, rev, domain, identifier: null, title, EngineeringObjectMetadata.Empty));
        var created = (Calculation)await factory.CreateAsync($"{title} — for test purposes.");

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId);

        return created;
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));

    private static string AutomationName(Control control) => AutomationProperties.GetName(control) ?? string.Empty;

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }
}

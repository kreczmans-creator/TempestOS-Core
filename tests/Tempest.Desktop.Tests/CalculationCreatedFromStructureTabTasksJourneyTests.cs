using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.10B` (T2). Reproduces the Product Owner's own path exactly:
/// open a project, land on its Structure tab (`ProjectArea.Engineering`,
/// the project's own embedded ribbon-and-docking surface), create a
/// Calculation through the Ribbon's own <c>calculations.create</c>
/// command — never a test-only factory call, the way
/// <c>CalculationTaskJourneyTests</c> builds its fixture — then follow
/// the rail to Engineering's own Tasks node
/// (<see cref="EngineeringAreaView"/>, `WP 20.1B`/`TD-181`'s Calculations
/// bucket) and assert the new Calculation is listed there.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CalculationCreatedFromStructureTabTasksJourneyTests
{
    [AvaloniaFact]
    public async Task CalculationCreatedFromTheStructureTab_ShowsInEngineeringTasksCalculationsBucket()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-T2-REPRO", "T2 Repro Project");

            // Exactly the Product Owner's own path: open the project
            // straight onto its Structure tab — never
            // `GoToEngineeringAsync`'s own standalone-capable overload.
            await navigator.OpenProjectAsync(project.Id, ProjectArea.Engineering);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var ribbon = window.FindUnique<RibbonView>();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            ribbon.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["kind"] = "Calculation", ["displayName"] = "T2 Repro Calculation", ["dueOn"] = "2026-10-01" });

            Click(ribbon, registry, "calculations.create");

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            IEngineeringObject? created = null;
            await RenderUntilAsync(window, () =>
            {
                var entry = domain.Repository.ListByKindAsync("Calculation").GetAwaiter().GetResult()
                    .FirstOrDefault(o => o.DisplayName == "T2 Repro Calculation");
                created = entry is null ? null : domain.Repository.FindAsync(entry.Id).GetAwaiter().GetResult();
                return created is not null;
            });

            Assert.NotNull(created);
            Assert.Equal(project.Id, ((IHasParent)created!).ParentId);

            // Follow the rail to Engineering's own Tasks node — the
            // Product Owner's own second step.
            await navigator.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            var engineeringArea = window.FindUnique<EngineeringAreaView>();
            engineeringArea.SelectNode("Tasks");

            await RenderUntilAsync(window, () =>
                engineeringArea.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Calculations (1)"));
            LayOut(window);

            var tasksText = string.Join(" | ", engineeringArea.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("T2 Repro Calculation", tasksText, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.10B` (T2). The real cause behind the Product Owner's finding: a
/// selection left over from a <em>different</em> project — the single,
/// global <see cref="ISelectionService"/> is never cleared by a project
/// switch (<see cref="CreatedObjectOpensRightUpTests"/> already found this
/// once, `WP 17.9.4`, and worked around it locally rather than fixing the
/// service) — silently hijacked <see cref="CreationPlacement.ParentFor"/>'s
/// own placement for a Calculation created afterwards in the project the
/// user had actually navigated to. Fixed by
/// <c>CalculationsWorkspaceRegistration.ResolveCreateParent</c>, which
/// distrusts the placement once it disagrees with the open project.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CalculationCreationCrossProjectSelectionTests
{
    [AvaloniaFact]
    public async Task CreatingACalculation_WithASelectionLeftOverFromAnotherProject_StillParentsUnderTheOpenProject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var workspace = host.Workspace!;
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();

            var projectA = await host.ProjectDirectory!.CreateAsync("P-STALE-A", "Stale Selection Project A");
            var staleCalc = await CreateCalculationAsync(domain, projectA.Id, "Stale Calc In Project A");

            // Select the stale calculation — exactly what a click on it in
            // Project A's own Structure tab would have left behind in the
            // single, global ISelectionService.
            await workspace.Selection.SelectAsync(staleCalc.Id, "Calculation");

            var projectB = await host.ProjectDirectory!.CreateAsync("P-STALE-B", "Stale Selection Project B");

            // Behave exactly like the Product Owner's own T2 path, but in
            // Project B — never touching selection again, exactly as a
            // real user would not, having selected nothing new since
            // Project A.
            await navigator.OpenProjectAsync(projectB.Id, ProjectArea.Engineering);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var ribbon = window.FindUnique<RibbonView>();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            ribbon.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["kind"] = "Calculation", ["displayName"] = "New Calc In Project B", ["dueOn"] = "2026-10-01" });

            Click(ribbon, registry, "calculations.create");

            IEngineeringObject? created = null;
            await RenderUntilAsync(window, () =>
            {
                created = domain.Repository.ListByKindAsync("Calculation").GetAwaiter().GetResult()
                    .FirstOrDefault(o => ((IHasBusinessIdentifier)o).DisplayName == "New Calc In Project B");
                return created is not null;
            });

            Assert.NotNull(created);
            Assert.Equal(projectB.Id, ((IHasParent)created!).ParentId);
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

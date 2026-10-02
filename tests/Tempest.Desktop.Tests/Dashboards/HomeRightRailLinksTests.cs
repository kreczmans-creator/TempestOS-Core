using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.Dashboards;

/// <summary>
/// RC: Home's right rail entries are working links, not read-outs —
/// Continue opens its project, and Recent opens its object in
/// Engineering rather than into a Document Area hidden behind Home.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class HomeRightRailLinksTests
{
    [AvaloniaFact]
    public async Task ContinueAndRecent_AreClickable_AndNavigateAwayFromHome()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);
            var navigator = host.ShellNavigator!;

            var project = await host.ProjectDirectory!.CreateAsync("RAIL-1", "Rail Links Project");
            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher))!;
            var created = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "Rail Links Doc", parentId: project.Id, initialContent: "x"), default);
            await window.OpenObjectAsync(created.SubjectId!.Value, "Document");

            // Continue → the project itself.
            var home = await ShowHomeAsync(window, navigator);
            var continueButton = RailButton(home, "Rail Links Project");
            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => navigator.Current.ProjectId == project.Id);
            Assert.Equal(project.Id, navigator.Current.ProjectId);

            // Recent → Engineering, with the object opened. `LastOpenPhase`
            // carries a time-stamp prefix, so the phase is matched with
            // Contains (a StartsWith here never held and only passed while
            // the wait timed out silently — v0.23.0 board B9).
            home = await ShowHomeAsync(window, navigator);
            var phaseBefore = window.LastOpenPhase;
            RailButton(home, "Rail Links Doc").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => window.LastOpenPhase != phaseBefore && window.LastOpenPhase.Contains(" opened (", StringComparison.Ordinal));
            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.Contains(created.SubjectId!.Value.ToString("N"), window.LastOpenPhase, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// v0.23.0 board M1: Continue lists the most recent Open projects,
    /// newest first — never the three oldest in insertion order, and never
    /// a Closed (signed-off) one.
    /// </summary>
    [AvaloniaFact]
    public async Task Continue_ListsTheNewestOpenProjects_NewestFirst_AndSkipsClosedOnes()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        MainWindow? window = null;
        try
        {
            await host.StartAsync();
            window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);
            var navigator = host.ShellNavigator!;

            var created = new List<Guid>();
            for (var i = 1; i <= 5; i++)
            {
                created.Add((await host.ProjectDirectory!.CreateAsync($"CONT-{i}", $"Continue Project {i}")).Id);
                await Task.Delay(5); // distinct CreatedAt ticks
            }

            // The newest project is signed off: Closed, so not "continued".
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext))!;
            var closed = await new ProjectLifecycleService(domain).SignOffAsync(created[4], "Closed for the Continue ordering test.");
            Assert.True(closed.Succeeded, closed.Reason);

            var home = await ShowHomeAsync(window, navigator);
            var continueEntries = home.GetLogicalDescendants().OfType<Button>()
                .Select(b => b.Content as string)
                .Where(t => t is not null && System.Text.RegularExpressions.Regex.IsMatch(t, "^Continue Project [0-9]$"))
                .ToList();

            Assert.Equal(["Continue Project 4", "Continue Project 3", "Continue Project 2"], continueEntries);
        }
        finally
        {
            window?.Close();
            Dispatcher.UIThread.RunJobs();
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<HomeDashboardView> ShowHomeAsync(MainWindow window, IShellNavigator navigator)
    {
        await navigator.GoToModuleAsync(ShellArea.Home);
        await window.RenderCurrentModuleAsync();
        LayOut(window);
        var home = window.GetLogicalDescendants().OfType<HomeDashboardView>().Single();
        await home.RefreshAsync();
        LayOut(window);
        return home;
    }

    private static Task RenderUntilAsync(MainWindow window, Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null) =>
        DesktopTestHelpers.WaitUntilAsync(condition, 20, () => LayOut(window), DesktopTestHelpers.OpenPhaseOf(window), what);

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        window.Width = 1900;
        window.Height = 1200;

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1200));
            window.Arrange(new Rect(0, 0, 1900, 1200));
        }
    }

    private static Button RailButton(HomeDashboardView home, string text) =>
        home.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, text));
}

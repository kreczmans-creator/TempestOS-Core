using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.10B` (T2), Scope item 4: the generic Object Editor shows a
/// Calculation's own Due date and lets the user change it, dispatching
/// <c>SetCalculationDueDateCommand</c> — the same row pattern as the BOM
/// and Owner/Priority sections beside it.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CalculationDueDateEditorTests
{
    [AvaloniaFact]
    public async Task TheEditor_ShowsTheDueDate_AndSavingAChangeUpdatesIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var project = await host.ProjectDirectory!.CreateAsync("P-DUE-EDIT", "Due Date Editor Project");
            var calculation = await CreateCalculationAsync(domain, project.Id, "Pressure vessel check", new DateOnly(2026, 10, 1));

            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Engineering);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await window.OpenCreatedObjectAsync(calculation.Id, "Calculation");
            LayOut(window);

            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().Distinct()
                .First(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == "Pressure vessel check"));

            var dueSection = editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Due"));
            Assert.True(dueSection.IsVisible, "The Due section is not shown for a Calculation.");

            var dueBox = dueSection.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.Equal("2026-10-01", dueBox.Text);

            dueBox.Text = "2026-11-15";
            var saveButton = dueSection.GetLogicalDescendants().OfType<Button>().Single(b => (b.Content as string) == "Save Due Date");
            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                ((Calculation)domain.Repository.FindAsync(calculation.Id).GetAwaiter().GetResult()!).DueOn == new DateOnly(2026, 11, 15));
            LayOut(window);

            var reloaded = (Calculation)(await domain.Repository.FindAsync(calculation.Id))!;
            Assert.Equal(new DateOnly(2026, 11, 15), reloaded.DueOn);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<Calculation> CreateCalculationAsync(EngineeringDomainContext domain, Guid projectId, string title, DateOnly dueOn)
    {
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", domain, (doc, rev) => new Calculation(doc, rev, domain, identifier: null, title, EngineeringObjectMetadata.Empty));
        var created = (Calculation)await factory.CreateAsync($"{title} — for test purposes.");

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId);

        await created.SetDueOnAsync(dueOn);

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

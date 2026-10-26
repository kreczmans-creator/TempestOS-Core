using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// C-7h on the worksheet screen: "Save as calculation" for a calculation too large for the store (past 4 MB) says so in one sentence that names it by
/// its title, makes nothing, and leaves the screen as it was; it does not end in "Could not save that calculation" with an exception's text, or leave an
/// empty Evidence record behind. The form cannot be filled with 4 MB (a text input takes 200 characters, a table has its row caps), so the test hands the
/// coordinator the inputs the way the form's last recalculation does and presses Save as the button would.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetCreateSizeCapTests
{
    private const string Template = "struct.base-plate";

    private static string ExampleText() =>
        File.ReadAllText(Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples", "structural", Template, "portal-column-base-uc254", "inputs.json"));

    private static async Task<int> ObjectCountAsync(WorkspaceHost host) =>
        (await ((EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext))).Repository.ListAllAsync()).Count;

    [AvaloniaFact]
    public async Task Saving_A_Calculation_Past_The_Store_Cap_Names_It_By_Title_And_Makes_Nothing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-CAP", "Size cap project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();

            view.SelectTemplate(Template);
            await Settle();
            Assert.True(coordinator.ImportInputsText(ExampleText()));
            await Settle();
            view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Calculation name").Text = "Portal frame base plate";

            // The inputs of the last recalculation, with a free text past the store's cap in one of them.
            var field = typeof(CalcSheetCoordinator).GetField("_latest", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var latest = (CalcInputSet)field.GetValue(coordinator)!;
            var oversize = new CalcInputSet(latest.Values.Select(v => v.Symbol == "alpha_basis" ? v with { Text = new string('x', CalcInstanceStore.MaxDocumentBytes + 100_000) } : v));
            Assert.Contains(oversize.Values, v => v.Text?.Length > CalcInstanceStore.MaxDocumentBytes);
            field.SetValue(coordinator, oversize);
            var objectsBefore = await ObjectCountAsync(host);

            await Save(coordinator);

            Assert.Contains("'Portal frame base plate'", view.StatusText, StringComparison.Ordinal);
            Assert.Contains("larger than 4 MB", view.StatusText, StringComparison.Ordinal);
            Assert.DoesNotContain("Could not", view.StatusText, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", view.StatusText, StringComparison.Ordinal);
            Assert.Null(view.CurrentInstance);
            Assert.Empty(await service.Store.ListInstancesAsync(project.Id));
            Assert.Equal(objectsBefore, await ObjectCountAsync(host));

            // The person trims it and saves again: it is saved, as before.
            field.SetValue(coordinator, new CalcInputSet(latest.Values.Select(v => v.Symbol == "alpha_basis" ? v with { Text = "Footing as drawing S-101." } : v)));
            await Save(coordinator);

            Assert.NotNull(view.CurrentInstance);
            Assert.Contains("Saved as 'Portal frame base plate'", view.StatusText, StringComparison.Ordinal);
            Assert.Single(await service.Store.ListInstancesAsync(project.Id));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task Save(CalcSheetCoordinator coordinator)
    {
        var save = typeof(CalcSheetCoordinator).GetMethod("SaveAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)save.Invoke(coordinator, [])!;
        await Settle();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }
}

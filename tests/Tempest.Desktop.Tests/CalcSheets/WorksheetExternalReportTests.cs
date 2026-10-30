using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum A, S6 in the worksheet form: the PROFIS report is attached to the calculation's Evidence and cited by `profis_ref`.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetExternalReportTests
{
    [AvaloniaFact]
    public async Task A_Base_Plate_Draft_Attaches_Its_Profis_Report_And_Cites_It()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-S6", "Base plate project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var plate = await service.CreateAsync("struct.base-plate", project.Id, "Portal column base", service.Catalogue.Get("struct.base-plate").Template.TestCases[0].Inputs);
            var bearing = await service.CreateAsync("mech.bearing-life", project.Id, "Pump DE bearing");

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();

            // No `_ref` input, no button.
            await coordinator.OpenAsync(bearing.Id);
            await Settle();
            Assert.False(AttachButton(view).IsEffectivelyVisible);

            await coordinator.OpenAsync(plate.Id);
            await Settle();
            Assert.Equal("profis_ref", view.ExternalReportInput);
            Assert.True(AttachButton(view).IsEffectivelyVisible);

            await coordinator.AttachExternalAsync("PROFIS-CB-01.pdf", "%PDF-1.4\n% PROFIS anchorage report placeholder\n"u8.ToArray());
            await Settle();

            var cited = (await service.Store.FindInstanceAsync(plate.Id))!.Inputs.Single(i => i.Symbol == "profis_ref");
            Assert.Equal("PROFIS-CB-01.pdf", cited.Text);
            Assert.Equal(CalcProvenanceKind.Evidence, cited.Provenance!.Kind);

            var run = await service.RunAsync(plate.Id);
            Assert.Contains("Anchorage designed externally", run!.SheetText, StringComparison.Ordinal);
            Assert.DoesNotContain(run.Result.Warnings, w => w.RuleId == "anchorage");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static Button AttachButton(CalcSheetView view) =>
        view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == CalcSheetView.AttachEvidenceCaption);

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }
}

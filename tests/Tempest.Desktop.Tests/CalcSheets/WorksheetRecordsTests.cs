using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum A, §A3, in the worksheet form: save tabulated inputs as a reference record, apply it to another draft, and keep a licensed record's values out of exports.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetRecordsTests
{
    [AvaloniaFact]
    public async Task Inputs_Saved_As_A_Record_Are_Cited_And_Applied_To_Another_Draft_And_A_Licensed_Record_Is_Withheld()
    {
        // The save-a-record flow starts from a kind with no record, as a database from before the bundled records (owner comment 5) would.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath()) { SeedBundledRecords = false };

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-REC", "Records project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var first = await service.CreateAsync("mech.bearing-life", project.Id, "Pump DE bearing", [CalcInputValue.Quantity("C", 20.3, "kN"), CalcInputValue.Quantity("C_0", 11.2, "kN")]);
            var second = await service.CreateAsync("mech.bearing-life", project.Id, "Pump NDE bearing");

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(first.Id);
            await Settle();
            Assert.True(view.IsRecordPanelShown);
            Assert.Empty(view.RecordLabels);

            view.FillRecordDraft(0, "6206 ratings", "SKF Rolling bearings catalogue", "PUB BU/P1 17000 (2018)", "6206 product table", "S. Kreczman (own copy)", licensed: true);
            Click(view, CalcSheetView.SaveRecordCaption);
            await Settle();

            var cited = (await service.Store.FindInstanceAsync(first.Id))!;
            var c = cited.Inputs.Single(i => i.Symbol == "C");
            Assert.Equal(20.3, c.Number);
            Assert.NotNull(CalcDataRecord.PinOf(c.Provenance));
            Assert.Contains("6206 product table", c.Provenance!.Reference, StringComparison.Ordinal);
            Assert.Single(view.RecordLabels);

            // The licensed values never leave in an export; the pin stays so the receiver knows which record to supply.
            var exported = await service.WithholdLicensedAsync(new CalcInputSet(cited.Inputs));
            Assert.Null(exported.Find("C")!.Number);
            Assert.Contains("licensed value withheld", exported.Find("C")!.Provenance!.Reference, StringComparison.Ordinal);
            var (csv, refusal) = await service.ExportRecordAsync(CalcDataRecord.PinOf(c.Provenance)!.Value.Id);
            Assert.Null(csv);
            Assert.Contains("never exported", refusal, StringComparison.Ordinal);

            // The second draft lists the record and takes its values with one click.
            await coordinator.OpenAsync(second.Id);
            await Settle();
            var label = Assert.Single(view.RecordLabels);
            view.ChooseRecord(label);
            Click(view, CalcSheetView.ApplyRecordCaption);
            await Settle();

            var applied = (await service.Store.FindInstanceAsync(second.Id))!;
            Assert.Equal(11.2, applied.Inputs.Single(i => i.Symbol == "C_0").Number);
            Assert.DoesNotContain(CalcInputValidator.Validate(service.Catalogue.Get("mech.bearing-life").Template, new CalcInputSet(applied.Inputs)), n => n.RuleId is "record.C" or "record.C_0");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void Click(CalcSheetView view, string name)
    {
        var button = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible, $"'{name}' is not available.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }
}

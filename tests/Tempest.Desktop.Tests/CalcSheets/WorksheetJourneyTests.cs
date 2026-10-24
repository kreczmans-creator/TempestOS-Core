using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Core.Identity;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// One worked example through the real window, as the showcase journey ran it: the person switched in Settings is the
/// one the header names and the sheet records as author and checker; the reference pickers keep their records after
/// an import; the issued calculation is in the Calculation register and opens from there; the PDF reads its table rows.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetJourneyTests
{
    [AvaloniaFact]
    public async Task A_Worked_Example_Is_Issued_Under_The_Switched_Person_Listed_In_The_Register_And_Printed_Whole()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            window.Show();
            host.SwitchPrincipal(new SessionPrincipal("s.kreczman", "Steven Kreczman", SessionRole.Engineer));
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Steven Kreczman · Engineer");

            var project = await host.ProjectDirectory!.CreateAsync("P-JOURNEY", "Journey project");
            await host.ProjectContext!.OpenAsync(project.Id);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            await Settle();

            var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            area.SelectNode("Worksheets");
            await Settle();
            var view = window.GetLogicalDescendants().OfType<CalcSheetView>().Single();
            var coordinator = (CalcSheetCoordinator)typeof(MainWindow).GetField("_calcSheetCoordinator", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

            view.SelectTemplate("elec.thermal-chain");
            await Settle();
            coordinator.ImportInputsText(File.ReadAllText(Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples", "electrical", "elec.thermal-chain", "three-stage-25W", "inputs.json")));
            await Settle();
            view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Calculation name").Text = "CALC-E01 Heat sink";

            await Click(view, CalcSheetView.SaveCaption);
            await Click(view, CalcSheetView.CheckCaption);
            await Click(view, CalcSheetView.IssueCaption);

            var issued = (await host.CalcInstances!.Store.FindInstanceAsync(view.CurrentInstance!.Id))!;
            Assert.Equal(Tempest.Calc.Library.CalcInstanceStatus.Issued, issued.Status);
            Assert.Equal("s.kreczman", issued.CreatedBy);
            Assert.Equal("s.kreczman", issued.CheckedBy);

            var text = PdfTextExtractor.ExtractText(host.CalcSheetRenderer!.Render(await host.CalcInstances.SheetAsync(issued)).ToArray());
            Assert.Contains("name = junction to case; R = 0.5 K/W", text, StringComparison.Ordinal);
            Assert.Contains("Figure 1 — ", text, StringComparison.Ordinal);
            Assert.Contains("P = 25 W", text, StringComparison.Ordinal);

            area.SelectNode("Calculation register");
            await Settle();
            var register = window.GetLogicalDescendants().OfType<CalcRegisterView>().Single();
            Assert.Single(window.GetLogicalDescendants().OfType<EngineeringCalculationView>());
            var row = Assert.Single(register.Shown);
            Assert.Equal(issued.Id, row.Id);
            Assert.Contains(register.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Heat sink thermal resistance chain");

            register.Open("CALC-E01 Heat sink");
            await Settle();
            Assert.Same(view, window.GetLogicalDescendants().OfType<CalcSheetView>().Single());
            Assert.Equal(issued.Id, view.CurrentInstance!.Id);
            Assert.True(view.IsReadOnly);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task Click(CalcSheetView view, string caption)
    {
        var button = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == caption);
        Assert.True(button.IsVisible && button.IsEnabled, $"'{caption}' is not available: {view.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => AutomationProperties.GetName(t) == "Worksheet status")?.Text}");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Settle();
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

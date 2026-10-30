using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Workspace.Shell;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Shot-fix A, item 7: the status bar's area read "Building Services" on every worksheet; it now names the open worksheet's discipline.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetStatusAreaTests
{
    private static async Task Settle()
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task The_Status_Bar_Area_Names_The_Discipline_Of_The_Open_Worksheet()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            window.Show();
            var project = await host.ProjectDirectory!.CreateAsync("P-AREA", "Area project");
            await host.ProjectContext!.OpenAsync(project.Id);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            await Settle();
            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Worksheets");
            await Settle();

            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var area = statusBar.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Active area");
            var view = GetPrivateField<CalcSheetView>(GetPrivateField<CalcSheetCoordinator>(window, "_calcSheetCoordinator"), "_view");

            foreach (var (template, discipline) in new[] { ("mech.spring", "Mechanical"), ("fp.accumulator", "Fluid Power"), ("elec.containment", "Electrical"), ("struct.base-plate", "Structural"), ("bs.glaser", "Building Services") })
            {
                view.SelectTemplate(template);
                await Settle();
                Assert.Equal(template, view.SelectedTemplate?.Template.Id);
                Assert.Equal(discipline, area.Text);
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}

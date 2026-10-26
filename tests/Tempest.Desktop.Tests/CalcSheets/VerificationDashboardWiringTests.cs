using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Core.Identity;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using DashboardOutcome = Tempest.Calc.Library.Dashboard.DashboardManualTestOutcome;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Addendum C integration, in the composed app: Calculations → Verification (C-4) shows the guided test's result (C-5) once a run is finished,
/// and counts the records a calculator is missing from C-3's required kinds.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class VerificationDashboardWiringTests
{
    private const string BearingId = "mech.bearing-life-basic";
    private const string SpringId = "mech.spring";

    private static async Task Settle()
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task The_Dashboard_Shows_A_Finished_Guided_Run_And_Counts_Missing_Records_From_C3s_Requirements()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            window.Show();
            host.SwitchPrincipal(new SessionPrincipal("test.engineer", "Test Engineer", SessionRole.Engineer));
            var project = await host.ProjectDirectory!.CreateAsync("P-VERIFY-WIRING", "Verification wiring project");
            await host.ProjectContext!.OpenAsync(project.Id);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            await Settle();

            // Every worked example of the bearing life calculator passes in one run, which is then finished; an unfinished run counts for nothing.
            var runs = host.ManualTests!;
            var examples = runs.Script.Steps.Where(s => s.Kind == GuidedStepKind.Example && s.TemplateId == BearingId).ToList();
            Assert.NotEmpty(examples);
            var run = await runs.StartAsync();

            foreach (var step in examples)
                await runs.RecordAsync(run.Id, step.Id, Tempest.Calc.Library.ManualTesting.ManualTestOutcome.Pass, "Matches the worked example.", null);

            var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            area.SelectNode("Verification dashboard");
            await Settle();
            var dashboard = window.GetLogicalDescendants().OfType<VerificationDashboardView>().Single().Model;
            Assert.Equal("Not tested", dashboard.Rows.Single(r => r.Id == BearingId).ManualTest.Label);

            await runs.FinishAsync(run.Id);
            await dashboard.RefreshAsync();

            var bearing = dashboard.Rows.Single(r => r.Id == BearingId);
            Assert.Equal(DashboardOutcome.Passed, bearing.ManualTest.Outcome);
            Assert.Equal("Passed", bearing.ManualTest.Label);
            Assert.Contains($"{examples.Count} of {examples.Count} worked examples recorded", bearing.ManualTest.Note, StringComparison.Ordinal);
            Assert.Equal("Not tested", dashboard.Rows.Single(r => r.Id == SpringId).ManualTest.Label);

            // mech.spring: C-3 marks the wire strength and the endurance strength Required, so those two are what the row says is missing
            // (not the permissible-stress fractions or the end-support coefficient, which the calculator runs on a default for).
            var spring = dashboard.Rows.Single(r => r.Id == SpringId);
            Assert.Equal(2, spring.MissingRecordKinds.Count);
            Assert.Equal(["Wire tensile strength R_m(d)", "Endurance strength"], spring.MissingRecordKinds);
            Assert.True(spring.Matches(VerificationStatusFilter.MissingRecords));
            var c3 = await host.CalcInstances!.RecordImport.MissingRequiredAsync(SpringId);
            Assert.Equal(c3.Select(r => r.Title), spring.MissingRecordKinds);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}

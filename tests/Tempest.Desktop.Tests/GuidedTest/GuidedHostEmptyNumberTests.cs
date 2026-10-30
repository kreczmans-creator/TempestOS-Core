using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.GuidedTest;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>
/// Shot-fix A, item 1: a worksheet whose defaults leave a required number empty (no default declared) crashed the app while the Guided test listened
/// to the form, because the form held the empty number as NaN and <see cref="CalcInputJson.Write"/> threw. The form now holds it as empty.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class GuidedHostEmptyNumberTests
{
    /// <summary>The templates the screenshots crashed on, and the empty required number of each.</summary>
    public static TheoryData<string, string> Crashed => new()
    {
        { "elec.containment", "f_max" },
        { "struct.base-plate", "l_eff" },
        { "struct.base-plate", "m" },
        { "mech.spring", "S_e" },
        { "mech.snap-fit", "mu" },
    };

    private static async Task Settle()
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Crashed))]
    public async Task A_Template_Opened_On_Its_Defaults_And_Then_Edited_Never_Throws_And_Holds_The_Empty_Number_As_Empty(string templateId, string symbol)
    {
        var workspace = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await workspace.StartAsync();

        try
        {
            var project = await workspace.ProjectDirectory!.CreateAsync("P-EMPTY", "Empty number");
            await workspace.ProjectContext!.OpenAsync(project.Id);
            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(workspace.CalcInstances!, view, workspace.ProjectContext, () => "s.kreczman", (IEventBus)workspace.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            var host = new CalcGuidedWorksheetHost(view, coordinator, workspace.CalcCatalogue!);
            var changes = 0;
            host.FormChanged += () => changes++;

            // Open on its defaults: the host writes the form on every change and run.
            view.SelectTemplate(templateId);
            await Settle();
            Assert.Equal(templateId, view.SelectedTemplate!.Template.Id);

            var empty = view.ReadForm().Find(symbol);
            Assert.NotNull(empty);
            Assert.Null(empty!.Number);
            Assert.DoesNotContain($"\"{symbol}\"", CalcInputJson.Write(view.ReadForm()), StringComparison.Ordinal);

            // The run says what to enter, not "enter a finite number".
            Assert.True(view.LastRun!.IsBlocked);
            Assert.DoesNotContain(view.LastRun.Blocks, b => b.Message.Contains("finite", StringComparison.Ordinal));
            Assert.Contains(view.LastRun.Blocks, b => b.RuleId == "input." + symbol);

            // A form change: a word typed into the number (NaN on the form) and then a number.
            view.SetField(symbol, text: "not a number");
            await Settle();
            Assert.True(double.IsNaN(view.ReadForm().Find(symbol)!.Number!.Value));
            _ = CalcInputJson.Write(view.ReadForm());

            view.SetField(symbol, text: "0.3");
            await Settle();
            Assert.Contains($"\"{symbol}\"", CalcInputJson.Write(view.ReadForm()), StringComparison.Ordinal);
            Assert.True(changes > 0);
        }
        finally
        {
            await workspace.ShutdownAsync();
            await workspace.DisposeAsync();
        }
    }
}

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

/// <summary>Addendum A, L1, in the worksheet form: link an input, see it go stale when the upstream changes, update it.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetLinkingTests
{
    [AvaloniaFact]
    public async Task An_Input_Linked_In_The_Form_Goes_Stale_When_Its_Upstream_Changes_And_Updates_Back()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-LINK", "Linking project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var section = await service.CreateAsync("struct.section-properties", project.Id, "Floor beam section");
            var beam = await service.CreateAsync("struct.beam-formulas", project.Id, "Floor beam");

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(beam.Id);

            var label = $"Floor beam section · rev A · {CalcInstanceStatus.Draft}";
            view.ChooseLink("I", label, "I_y");
            Click(view, CalcSheetView.LinkCaption);
            await Settle();

            var linked = (await service.Store.FindInstanceAsync(beam.Id))!;
            var link = Assert.Single(linked.Links);
            Assert.Equal(new CalcLink(section.Id, "I_y", "A"), link.Link);
            Assert.Equal(section.Outputs.Single(o => o.Symbol == "I_y").Si!.Value * 1e8, link.Input.Number!.Value, 6);
            Assert.Equal(string.Empty, view.StaleText);

            // The section changes: the event marks the open beam stale and offers the update.
            var changed = await service.UpdateInputsAsync(section.Id, [.. section.Inputs.Select(i => i.Number is { } n && i.Unit == "mm" ? i with { Number = n * 1.5 } : i)]);
            Assert.True(changed.Succeeded, changed.Reason);
            await Settle();
            Assert.Contains("now gives a different 'I_y'", view.StaleText, StringComparison.Ordinal);
            Assert.Equal("Update linked inputs", view.UpdateLinksCaption);

            Click(view, CalcSheetView.UpdateLinksName);
            await Settle();
            Assert.Equal(string.Empty, view.StaleText);
            Assert.False((await service.StalenessAsync((await service.Store.FindInstanceAsync(beam.Id))!)).IsStale);
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

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

/// <summary>Addendum A (A8-UI), in the worksheet form: reopen a saved calculation, see and clear its stale mark, and create, check and issue a pack.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetSavedAndPacksTests
{
    private sealed record Fixture(WorkspaceHost Host, CalcInstanceService Service, CalcSheetView View, CalcSheetCoordinator Coordinator, Guid ProjectId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Saved_Sheet_Is_Reopened_From_The_List_With_Its_Inputs_And_Revision()
    {
        await using var fx = await StartAsync("P-OPEN");
        var view = fx.View;
        Assert.False(view.CanOpenSaved);

        view.SelectTemplate("struct.section-properties");
        await Settle();
        view.SetField("h", "500");
        await Settle();
        Click(view, CalcSheetView.SaveCaption);
        await WaitFor(() => view.SavedCalculations.Count == 1);

        var first = (await fx.Service.Store.ListInstancesAsync(fx.ProjectId)).Single();
        var row = Assert.Single(view.SavedCalculations);
        Assert.Equal(first.Id, row.Id);
        Assert.Equal("Draft", row.Status);
        Assert.Equal("A", row.Revision);
        Assert.False(row.IsStale);

        // Start a different, new sheet: the form no longer shows the saved one.
        view.SelectTemplate("struct.beam-formulas");
        await Settle();
        Assert.Null(view.CurrentInstance);
        Assert.False(view.CanOpenSaved);

        view.ChooseSaved(first.Id);
        Assert.True(view.CanOpenSaved);
        Click(view, CalcSheetView.OpenSavedCaption);
        await WaitFor(() => view.CurrentInstance?.Id == first.Id);

        Assert.Equal("struct.section-properties", view.SelectedTemplate!.Template.Id);
        Assert.Equal("500", ((TextBox)view.FieldControl("h")!).Text);
        Assert.Equal("A", view.CurrentInstance!.Revision);
        Assert.Contains("rev A", view.StatusText, StringComparison.Ordinal);
        Assert.Contains(first.Name, view.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_Linked_Downstream_Shows_The_Stale_Mark_In_The_List_Until_Its_Links_Are_Updated()
    {
        await using var fx = await StartAsync("P-STALE");
        var service = fx.Service;
        var view = fx.View;
        var section = await service.CreateAsync("struct.section-properties", fx.ProjectId, "Floor beam section");
        var beam = await service.CreateAsync("struct.beam-formulas", fx.ProjectId, "Floor beam");
        var bound = await service.BindAsync(beam.Id, "I", section.Id, "I_xx");
        Assert.True(bound.Succeeded, bound.Reason);

        await fx.Coordinator.RefreshListsAsync();
        Assert.Equal(2, view.SavedCalculations.Count);
        Assert.All(view.SavedCalculations, r => Assert.False(r.IsStale));

        var changed = await service.UpdateInputsAsync(section.Id, [.. section.Inputs.Select(i => i.Number is { } n && i.Unit == "mm" ? i with { Number = n * 1.5 } : i)]);
        Assert.True(changed.Succeeded, changed.Reason);
        await WaitFor(() => view.SavedCalculations.Any(r => r.IsStale));

        var stale = Assert.Single(view.SavedCalculations, r => r.IsStale);
        Assert.Equal(beam.Id, stale.Id);
        Assert.Contains(CalcSheetView.StaleListMark, stale.Label, StringComparison.Ordinal);
        Assert.DoesNotContain(CalcSheetView.StaleListMark, view.SavedCalculations.Single(r => r.Id == section.Id).Label, StringComparison.Ordinal);

        view.ChooseSaved(beam.Id);
        Click(view, CalcSheetView.OpenSavedCaption);
        await WaitFor(() => view.CurrentInstance?.Id == beam.Id);
        await WaitFor(() => view.StaleText.Length > 0);
        Assert.Contains("now gives a different 'I_xx'", view.StaleText, StringComparison.Ordinal);

        Click(view, CalcSheetView.UpdateLinksName);
        await WaitFor(() => view.SavedCalculations.All(r => !r.IsStale));
        Assert.Equal(string.Empty, view.StaleText);
    }

    [AvaloniaFact]
    public async Task The_Drive_Train_Pack_Is_Created_From_Its_Template_Lists_Its_Calculations_And_One_Is_Reopened()
    {
        await using var fx = await StartAsync("P-DRIVE");
        var view = fx.View;
        Assert.Contains(view.PackTemplates, t => t.Id == "pack.drive-train");
        Assert.Contains(view.PackTemplates, t => t.Id == "pack.motor-circuit");
        Assert.False(view.CanCreatePack);

        view.ChoosePackTemplate("pack.drive-train");
        Assert.Equal("Drive train", view.PackTitle);
        view.SetPackTitle("Conveyor drive");
        Assert.True(view.CanCreatePack);
        Click(view, CalcSheetView.CreatePackCaption);
        await WaitFor(() => view.Packs.Count == 1 && view.PackMembers.Count == 4);

        var pack = Assert.Single(view.Packs);
        Assert.Equal("Conveyor drive", pack.Title);
        Assert.Equal(CalcInstanceStatus.Draft, pack.Status);
        Assert.Equal(pack.Id, view.SelectedPackId);
        Assert.Contains("created from 'Drive train'", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(["Motor sizing", "Shaft", "Bearing A life", "Bearing A seat fit"], view.PackMembers.Select(m => m.Name).ToArray());

        // The members are also in the project's list, and the downstream ones are linked and current.
        Assert.Equal(4, view.SavedCalculations.Count);
        Assert.All(view.SavedCalculations, r => Assert.False(r.IsStale));

        var shaft = view.PackMembers.Single(m => m.Name == "Shaft");
        view.ChoosePackMember(shaft.Id);
        Click(view, CalcSheetView.OpenPackMemberCaption);
        await WaitFor(() => view.CurrentInstance?.Id == shaft.Id);
        Assert.Equal("mech.shaft", view.SelectedTemplate!.Template.Id);
    }

    [AvaloniaFact]
    public async Task The_Motor_Circuit_Pack_Without_The_Owners_Tables_Shows_The_Refusal_Text()
    {
        await using var fx = await StartAsync("P-MOTOR");
        var view = fx.View;

        view.ChoosePackTemplate("pack.motor-circuit");
        Click(view, CalcSheetView.CreatePackCaption);
        await WaitFor(() => view.StatusText.StartsWith("Step 'e4' is blocked", StringComparison.Ordinal));

        Assert.Contains("ampacity:", view.StatusText, StringComparison.Ordinal);
        Assert.Empty(view.Packs);
        Assert.Empty(await fx.Service.Store.ListPacksAsync(fx.ProjectId));
    }

    [AvaloniaFact]
    public async Task A_Pack_Is_Refused_While_A_Member_Is_Stale_Then_Checked_And_Issued()
    {
        await using var fx = await StartAsync("P-ISSUE");
        var service = fx.Service;
        var view = fx.View;

        view.ChoosePackTemplate("pack.drive-train");
        Click(view, CalcSheetView.CreatePackCaption);
        await WaitFor(() => view.Packs.Count == 1 && view.PackMembers.Count == 4);
        var packId = view.SelectedPackId!.Value;

        // Check is offered on a draft pack with the checker pre-filled; issue is not until it is checked.
        Assert.True(view.CanCheckPack);
        Assert.False(view.CanIssuePack);

        // Members whose inputs still say "manual" cannot be checked: the refusal is shown as the service words it.
        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.StatusText.Contains("before the pack can be checked", StringComparison.Ordinal));
        Assert.Equal(CalcInstanceStatus.Draft, view.Packs.Single().Status);

        var spec = CalcProvenance.Specification("SPEC-CONV-01 §4");

        foreach (var member in view.PackMembers)
        {
            var stored = (await service.Store.FindInstanceAsync(member.Id))!;
            var updated = await service.UpdateInputsAsync(stored.Id, [.. stored.Inputs.Select(i => i.Provenance?.Kind == CalcProvenanceKind.Link ? i : i with { Provenance = spec })]);
            Assert.True(updated.Succeeded, updated.Reason);
        }

        // The motor changes: everything downstream is stale and the pack cannot be checked.
        var motor = (await service.Store.FindInstanceAsync(view.PackMembers.Single(m => m.Name == "Motor sizing").Id))!;
        var changed = await service.UpdateInputsAsync(motor.Id, [.. motor.Inputs.Select(i => i.Number is { } n && i.Symbol == "m" ? i with { Number = n * 2 } : i)]);
        Assert.True(changed.Succeeded, changed.Reason);
        await WaitFor(() => view.PackMembers.Any(m => m.IsStale));

        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.StatusText.Contains("is stale", StringComparison.Ordinal));
        Assert.Contains("Update it before checking the pack", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, view.Packs.Single().Status);

        // Update each downstream member in chain order, from its own sheet.
        foreach (var name in new[] { "Shaft", "Bearing A life", "Bearing A seat fit" })
        {
            var member = view.PackMembers.Single(m => m.Name == name);

            if (!member.IsStale)
                continue;

            view.ChoosePackMember(member.Id);
            Click(view, CalcSheetView.OpenPackMemberCaption);
            await WaitFor(() => view.CurrentInstance?.Id == member.Id);
            await WaitFor(() => view.UpdateLinksCaption is not null);
            Click(view, CalcSheetView.UpdateLinksName);
            await WaitFor(() => !view.PackMembers.Single(m => m.Id == member.Id).IsStale);
        }

        Assert.All(view.PackMembers, m => Assert.False(m.IsStale));

        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Checked);
        Assert.Equal("Pack checked. Issue it when ready.", view.StatusText);
        Assert.False(view.CanCheckPack);
        Assert.True(view.CanIssuePack);

        view.FillPackIssue("CONV-DRV-001", "Client Co");
        Click(view, CalcSheetView.IssuePackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Issued);

        Assert.StartsWith("Pack issued as 'CONV-DRV-001'", view.StatusText, StringComparison.Ordinal);
        Assert.False(view.CanIssuePack);
        Assert.All(view.PackMembers, m => Assert.Equal("Issued", m.Status));
        Assert.All((await service.Store.ListInstancesAsync(fx.ProjectId)).Where(i => i.PackId == packId), i => Assert.Equal(CalcInstanceStatus.Issued, i.Status));
        Assert.Equal(CalcInstanceStatus.Issued, (await service.Store.FindPackAsync(packId))!.Status);
    }

    [AvaloniaFact]
    public async Task Without_A_Project_Nothing_Is_Listed_And_Nothing_Can_Be_Created_Or_Opened()
    {
        await using var fx = await StartAsync("P-NONE");
        await fx.Host.ProjectContext!.CloseAsync();
        await WaitFor(() => !fx.Host.ProjectContext!.HasProject);
        await fx.Coordinator.RefreshListsAsync();

        fx.View.ChoosePackTemplate("pack.drive-train");
        Assert.Empty(fx.View.SavedCalculations);
        Assert.Empty(fx.View.Packs);
        Assert.False(fx.View.CanCreatePack);
        Assert.False(fx.View.CanOpenSaved);
        Assert.False(fx.View.CanCheckPack);
        Assert.False(fx.View.CanIssuePack);
    }

    private static async Task<Fixture> StartAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, code + " project");
        await host.ProjectContext!.OpenAsync(project.Id);
        var view = new CalcSheetView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var coordinator = new CalcSheetCoordinator(host.CalcInstances!, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
        await coordinator.RefreshAsync();
        return new Fixture(host, host.CalcInstances!, view, coordinator, project.Id);
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

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 400; i++)
        {
            Dispatcher.UIThread.RunJobs();

            if (condition())
                return;

            await Task.Delay(25);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition(), "The form did not reach the expected state in time.");
    }
}

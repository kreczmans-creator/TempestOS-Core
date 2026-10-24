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
    /// <summary>The signed-in person the coordinator is given; a test changes it as "Switch person" would.</summary>
    private sealed class Who
    {
        public string Name { get; set; } = "s.kreczman";
    }

    private sealed record Fixture(WorkspaceHost Host, CalcInstanceService Service, CalcSheetView View, CalcSheetCoordinator Coordinator, Guid ProjectId, Who Principal) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }

    private static readonly IReadOnlyList<CalcPackStep> DriveSteps = CalcPackTemplates.DriveTrain.Steps;

    private static string StepName(string templateId) => DriveSteps.First(s => s.TemplateId == templateId).Name;

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
        Assert.False(view.HasUnsavedEdits());

        // Start a different, new sheet: the form no longer shows the saved one, and an untouched new sheet is nothing to lose.
        view.SelectTemplate("struct.beam-formulas");
        await Settle();
        Assert.Null(view.CurrentInstance);
        Assert.False(view.CanOpenSaved);
        Assert.False(view.HasUnsavedEdits());

        view.ChooseSaved(first.Id);
        Assert.True(view.CanOpenSaved);
        Click(view, CalcSheetView.OpenSavedCaption);
        await WaitFor(() => view.CurrentInstance?.Id == first.Id);

        Assert.Equal("struct.section-properties", view.SelectedTemplate!.Template.Id);
        Assert.Equal("500", ((TextBox)view.FieldControl("h")!).Text);
        Assert.Equal("A", view.CurrentInstance!.Revision);
        Assert.Contains("rev A", view.StatusText, StringComparison.Ordinal);
        Assert.Contains(first.Name, view.StatusText, StringComparison.Ordinal);
        Assert.False(view.HasUnsavedEdits());
    }

    [AvaloniaFact]
    public async Task Opening_Another_Calculation_Over_Unsaved_Edits_Asks_Once_Then_Discards()
    {
        await using var fx = await StartAsync("P-DIRTY");
        var service = fx.Service;
        var view = fx.View;
        var one = await service.CreateAsync("struct.section-properties", fx.ProjectId, "Section one");
        var two = await service.CreateAsync("struct.beam-formulas", fx.ProjectId, "Beam two");
        await fx.Coordinator.RefreshListsAsync();

        view.ChooseSaved(one.Id);
        Click(view, CalcSheetView.OpenSavedCaption);
        await WaitFor(() => view.CurrentInstance?.Id == one.Id);
        Assert.False(view.HasUnsavedEdits());

        view.SetField("h", "612");
        await Settle();
        Assert.True(view.HasUnsavedEdits());

        view.ChooseSaved(two.Id);
        Click(view, CalcSheetView.OpenSavedCaption);
        await WaitFor(() => view.StatusText.Contains("unsaved changes", StringComparison.Ordinal));
        Assert.Contains("Beam two", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(one.Id, view.CurrentInstance!.Id);
        Assert.Equal("612", ((TextBox)view.FieldControl("h")!).Text);

        // Pressing Open again for the same calculation is the confirmation.
        Click(view, CalcSheetView.OpenSavedCaption);
        await WaitFor(() => view.CurrentInstance?.Id == two.Id);
        Assert.False(view.HasUnsavedEdits());
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
    public async Task A_Burst_Of_Staleness_Events_Relists_Once()
    {
        await using var fx = await StartAsync("P-BURST");
        var bus = (IEventBus)fx.Host.Services!.GetService(typeof(IEventBus))!;
        await Settle();
        var before = fx.Coordinator.RelistCount;

        for (var i = 0; i < 6; i++)
            await bus.PublishAsync(new CalcStalenessChanged(Guid.NewGuid(), "upstream changed", [Guid.NewGuid()]), CancellationToken.None);

        await Settle();
        Assert.Equal(before + 1, fx.Coordinator.RelistCount);
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
        await WaitFor(() => view.Packs.Count == 1 && view.PackMembers.Count == DriveSteps.Count);

        var pack = Assert.Single(view.Packs);
        Assert.Equal("Conveyor drive", pack.Title);
        Assert.Equal(CalcInstanceStatus.Draft, pack.Status);
        Assert.Equal(pack.Id, view.SelectedPackId);
        Assert.Contains("created from 'Drive train'", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(DriveSteps.Select(s => s.Name).ToArray(), view.PackMembers.Select(m => m.Name).ToArray());

        // The members are also in the project's list, and the downstream ones are linked and current.
        Assert.Equal(DriveSteps.Count, view.SavedCalculations.Count);
        Assert.All(view.SavedCalculations, r => Assert.False(r.IsStale));

        var shaft = view.PackMembers.Single(m => m.Name == StepName("mech.shaft"));
        view.ChoosePackMember(shaft.Id);
        Click(view, CalcSheetView.OpenPackMemberCaption);
        await WaitFor(() => view.CurrentInstance?.Id == shaft.Id);
        Assert.Equal("mech.shaft", view.SelectedTemplate!.Template.Id);
    }

    [AvaloniaFact]
    public async Task Two_Quick_Create_Requests_Make_One_Pack()
    {
        await using var fx = await StartAsync("P-TWICE");
        var view = fx.View;
        view.ChoosePackTemplate("pack.drive-train");

        // The stores answer at once here, so the second click is made to land while the first request is still running: choosing
        // the new pack in the list (part of creating it) is where the form calls back into the view.
        var seenBusy = false;
        var seenCreateEnabled = true;
        var fired = false;
        view.PackSelected += _ =>
        {
            if (fired)
                return;

            fired = true;
            seenBusy = view.IsBusy;
            seenCreateEnabled = view.CanCreatePack;
            RawClick(view, CalcSheetView.CreatePackCaption);
        };

        RawClick(view, CalcSheetView.CreatePackCaption);
        await WaitFor(() => view.Packs.Count == 1 && !view.IsBusy);
        await Settle();

        Assert.True(fired);
        Assert.True(seenBusy);
        Assert.False(seenCreateEnabled);

        Assert.Single(await fx.Service.Store.ListPacksAsync(fx.ProjectId));
        Assert.Equal(DriveSteps.Count, (await fx.Service.Store.ListInstancesAsync(fx.ProjectId)).Count);
        Assert.True(view.CanCreatePack);
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
    public async Task A_Pack_Is_Refused_While_A_Member_Is_Stale_Then_Checked_By_The_Signed_In_Person_And_Issued()
    {
        await using var fx = await StartAsync("P-ISSUE");
        var service = fx.Service;
        var view = fx.View;
        fx.Principal.Name = "alice";
        await fx.Coordinator.RefreshListsAsync();
        Assert.Equal("alice", view.PackCheckerText);

        await CreateDriveTrainAsync(fx);
        var packId = view.SelectedPackId!.Value;

        // Check is offered on a draft pack; issue is not until it is checked.
        Assert.True(view.CanCheckPack);
        Assert.False(view.CanIssuePack);

        // Built-in packs start from example inputs whose provenance is an assumption; give them project provenance.
        await SetSpecProvenanceAsync(fx);

        // The motor changes: everything downstream is stale and the pack cannot be checked.
        var motorId = view.PackMembers.Single(m => m.Name == StepName("elec.motor-sizing")).Id;
        var motor = (await service.Store.FindInstanceAsync(motorId))!;
        var changed = await service.UpdateInputsAsync(motor.Id, [.. motor.Inputs.Select(i => i.Number is { } n && i.Symbol == "T_L" ? i with { Number = n * 1.2 } : i)]);
        Assert.True(changed.Succeeded, changed.Reason);
        await WaitFor(() => view.PackMembers.Any(m => m.IsStale));

        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.StatusText.Contains("is stale", StringComparison.Ordinal));
        Assert.Contains("Update it before checking the pack", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, view.Packs.Single().Status);

        // Update each downstream member in chain order, from its own sheet.
        foreach (var step in DriveSteps.Skip(1))
        {
            var member = view.PackMembers.Single(m => m.Name == step.Name);

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

        // Someone else signs in before checking: the check is recorded under them, whatever the panel showed before.
        fx.Principal.Name = "bob";
        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Checked);
        Assert.Equal("Pack checked by bob. Issue it when ready.", view.StatusText);
        Assert.Equal("bob", view.PackCheckerText);
        Assert.False(view.CanCheckPack);
        Assert.True(view.CanIssuePack);

        view.FillPackIssue("CONV-DRV-001", "Client Co");
        Click(view, CalcSheetView.IssuePackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Issued);

        Assert.StartsWith("Pack issued as 'CONV-DRV-001'", view.StatusText, StringComparison.Ordinal);
        Assert.False(view.CanIssuePack);
        Assert.All(view.PackMembers, m => Assert.Equal("Issued", m.Status));
        var members = (await service.Store.ListInstancesAsync(fx.ProjectId)).Where(i => i.PackId == packId).ToList();
        Assert.Equal(DriveSteps.Count, members.Count);
        Assert.All(members, i => Assert.Equal(CalcInstanceStatus.Issued, i.Status));
        Assert.All(members, i => Assert.Contains("Checker: bob", i.Issue!.SheetText, StringComparison.Ordinal));
        Assert.All(members, i => Assert.DoesNotContain("Checker: alice", i.Issue!.SheetText, StringComparison.Ordinal));
        Assert.Equal(CalcInstanceStatus.Issued, (await service.Store.FindPackAsync(packId))!.Status);
    }

    [AvaloniaFact]
    public async Task A_Checked_Pack_Is_Reopened_As_A_Draft_Under_The_Next_Revision()
    {
        await using var fx = await StartAsync("P-REOPEN");
        var view = fx.View;
        await CreateDriveTrainAsync(fx);
        await SetSpecProvenanceAsync(fx);
        Assert.False(view.CanReopenPack);

        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Checked);
        Assert.True(view.CanReopenPack);

        Click(view, CalcSheetView.ReopenPackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Draft);

        Assert.Equal("B", view.Packs.Single().Revision);
        Assert.True(view.CanCheckPack);
        Assert.False(view.CanReopenPack);
        Assert.StartsWith("Pack reopened as a new revision", view.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Issuing_A_Pack_That_Changed_After_It_Was_Checked_Shows_The_Refusal_And_A_Reselected_Pack_Refills_Before_Issue_Is_Offered()
    {
        await using var fx = await StartAsync("P-REFUSE");
        var service = fx.Service;
        var view = fx.View;
        await CreateDriveTrainAsync(fx);
        var packId = view.SelectedPackId!.Value;
        await SetSpecProvenanceAsync(fx);

        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.Packs.Single().Status == CalcInstanceStatus.Checked);

        // A second pack, so the checked one can be chosen again.
        var other = await service.CreatePackAsync(fx.ProjectId, "Other pack", [view.PackMembers.First().Id]);
        await fx.Coordinator.RefreshListsAsync();
        view.ChoosePack(other.Id);
        await WaitFor(() => view.PackMembers.Count == 1);
        Assert.False(view.CanIssuePack);

        // Choosing the checked pack again refills the reference and client for it (over whatever was typed) and offers Issue.
        view.FillPackIssue("typed for another", "someone else");
        view.ChoosePack(packId);
        await WaitFor(() => view.CanIssuePack && view.PackMembers.Count == DriveSteps.Count);
        var checkedPack = (await service.Store.FindPackAsync(packId))!;
        Assert.Equal(($"{CalcInstanceService.Safe(checkedPack.Title)}-{checkedPack.Revision}", fx.Host.ProjectContext!.Current!.DisplayName), view.PackIssueText);

        var first = (await service.Store.FindInstanceAsync(view.PackMembers[0].Id))!;
        var edited = await service.UpdateInputsAsync(first.Id, [.. first.Inputs.Select(i => i.Number is { } n && i.Symbol == "m" ? i with { Number = n * 2 } : i)]);
        Assert.True(edited.Succeeded, edited.Reason);

        Click(view, CalcSheetView.IssuePackCaption);
        await WaitFor(() => view.StatusText.Contains("changed after the pack was checked", StringComparison.Ordinal));
        Assert.Contains("check the pack again", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await service.Store.FindPackAsync(packId))!.Status);
    }

    [AvaloniaFact]
    public async Task A_Pack_With_A_Calculation_That_Needs_An_Independent_Check_Is_Refused_For_Its_Author()
    {
        await using var fx = await StartAsync("P-INDEP");
        var service = fx.Service;
        var view = fx.View;
        var spec = CalcProvenance.Specification("SPEC-FP-01 §2");
        var template = service.Catalogue.Get("elec.fault-protection").Template;
        Assert.True(template.IndependentCheck);

        var fault = await service.CreateAsync("elec.fault-protection", fx.ProjectId, "Final circuit fault protection", [.. CalcTemplateHasher.NewSheetInputs(template).Values.Select(v => v with { Provenance = spec })]);
        Assert.Empty(fault.Blocks);
        var pack = await service.CreatePackAsync(fx.ProjectId, "Fault pack", [fault.Id]);
        await fx.Coordinator.RefreshListsAsync();

        view.ChoosePack(pack.Id);
        await WaitFor(() => view.PackMembers.Count == 1 && view.CanCheckPack);
        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.StatusText.Contains("needs an independent check", StringComparison.Ordinal));

        Assert.Contains("Final circuit fault protection", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("someone other than its author", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, view.Packs.Single().Status);
    }

    [AvaloniaFact]
    public async Task The_Lists_Follow_The_Open_Project_Through_The_Project_Changed_Event()
    {
        await using var fx = await StartAsync("P-ONE");
        var view = fx.View;
        await fx.Service.CreateAsync("struct.section-properties", fx.ProjectId, "Section in project one");
        await fx.Coordinator.RefreshListsAsync();
        Assert.Single(view.SavedCalculations);

        // Only the event drives these refreshes.
        var second = await fx.Host.ProjectDirectory!.CreateAsync("P-TWO", "P-TWO project");
        await fx.Host.ProjectContext!.OpenAsync(second.Id);
        await WaitFor(() => view.SavedCalculations.Count == 0);

        await fx.Host.ProjectContext.OpenAsync(fx.ProjectId);
        await WaitFor(() => view.SavedCalculations.Count == 1);
        view.ChoosePackTemplate("pack.drive-train");
        Assert.True(view.CanCreatePack);

        await fx.Host.ProjectContext.CloseAsync();
        await WaitFor(() => view.SavedCalculations.Count == 0 && !view.CanCreatePack);
        Assert.Empty(view.Packs);
        Assert.False(view.CanOpenSaved);
        Assert.False(view.CanCheckPack);
        Assert.False(view.CanIssuePack);
    }

    [AvaloniaFact]
    public async Task Lists_That_Stop_At_Their_Cap_Say_So_And_The_Checker_Is_Read_Only()
    {
        await using var fx = await StartAsync("P-CAP");
        var view = fx.View;
        view.SetSavedCalculations([new CalcSheetView.SavedCalculation(Guid.NewGuid(), "n", "t", "A", "Draft", false)], truncated: true);
        view.SetPacks([], truncated: true);
        Assert.Contains("Only the 1 most recently updated calculations are listed", view.SavedCapNote, StringComparison.Ordinal);
        Assert.Contains("Only the 0 most recently updated packs are listed", view.PacksCapNote, StringComparison.Ordinal);
        view.SetSavedCalculations([], truncated: false);
        Assert.Equal(string.Empty, view.SavedCapNote);

        var checker = view.GetLogicalDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Pack checker (signed-in person)");
        Assert.True(checker.IsReadOnly);
        Assert.Equal("s.kreczman", checker.Text);
        Assert.NotEqual(AutomationProperties.GetName(view.GetLogicalDescendants().OfType<StackPanel>().First(p => AutomationProperties.GetName(p) == CalcSheetView.SavedPanelName)),
            AutomationProperties.GetName(view.GetLogicalDescendants().OfType<ListBox>().First(l => AutomationProperties.GetName(l) == CalcSheetView.SavedListName)));
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

    private static async Task CreateDriveTrainAsync(Fixture fx)
    {
        var view = fx.View;
        view.ChoosePackTemplate("pack.drive-train");
        Click(view, CalcSheetView.CreatePackCaption);
        await WaitFor(() => view.Packs.Count == 1 && view.PackMembers.Count == DriveSteps.Count && !view.IsBusy);
    }

    /// <summary>Every unlinked input of every shown member cites a source, as issuing needs.</summary>
    private static async Task SetSpecProvenanceAsync(Fixture fx)
    {
        var spec = CalcProvenance.Specification("SPEC-CONV-01 §4");

        foreach (var member in fx.View.PackMembers)
        {
            var stored = (await fx.Service.Store.FindInstanceAsync(member.Id))!;
            var updated = await fx.Service.UpdateInputsAsync(stored.Id, [.. stored.Inputs.Select(i => i.Provenance?.Kind == CalcProvenanceKind.Link ? i : i with { Provenance = spec })]);
            Assert.True(updated.Succeeded, updated.Reason);
        }

        await Settle();
    }

    private static async Task<Fixture> StartAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, code + " project");
        await host.ProjectContext!.OpenAsync(project.Id);
        var who = new Who();
        var view = new CalcSheetView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var coordinator = new CalcSheetCoordinator(host.CalcInstances!, view, host.ProjectContext, () => who.Name, (IEventBus)host.Services!.GetService(typeof(IEventBus)));
        await coordinator.RefreshAsync();
        return new Fixture(host, host.CalcInstances!, view, coordinator, project.Id, who);
    }

    private static Button ButtonNamed(CalcSheetView view, string name) =>
        view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    private static void Click(CalcSheetView view, string name)
    {
        var button = ButtonNamed(view, name);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible, $"'{name}' is not available.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>Raises the click whether or not the button is enabled (a double click lands before the screen has caught up).</summary>
    private static void RawClick(CalcSheetView view, string name) => ButtonNamed(view, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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

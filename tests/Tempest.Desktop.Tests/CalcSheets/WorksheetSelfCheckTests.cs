using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Desktop.Tests.Xero;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// DA-12 on the worksheet form and the packs panel: "Self-check (no second person available)" is offered only for a draft whose template
/// demands an independent check (E4, E5, S6), the preparer's check is refused until the box is ticked and a statement of at least 20
/// characters written, and a self-check is recorded on the calculation (or pack), printed on the issued sheet, text and PDF, and written to
/// the audit trail. S8 lifting offers no self-check (DB-13).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetSelfCheckTests
{
    private const string Preparer = "S-1-5-21-1111111111-2222222222-3333333333-2001";
    private const string FaultProtection = "elec.fault-protection";
    private const string Statement = "No second engineer is available this week; I re-derived Zs and the disconnection time by hand and they agree.";

    private sealed record Fixture(WorkspaceHost Host, CalcInstanceService Service, CalcSheetView View, CalcSheetCoordinator Coordinator, Guid ProjectId, RecordingAudit Audit) : IAsyncDisposable
    {
        public void SignInAs(string id, string name) => Host.SwitchPrincipal(new SessionPrincipal(id, name, SessionRole.Engineer));

        public async ValueTask DisposeAsync()
        {
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Self_Check_Is_Offered_Only_For_A_Draft_Whose_Template_Demands_An_Independent_Check()
    {
        await using var fx = await StartAsync("P-SELF-OFFER");
        var view = fx.View;
        fx.SignInAs(Preparer, "Pat Preparer");

        // A template with no independence rule never offers it.
        view.SelectTemplate("elec.thermal-chain");
        await Settle();
        Assert.False(view.IsSelfCheckOffered);
        Assert.True(fx.Coordinator.ImportInputsText(File.ReadAllText(Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples", "electrical", "elec.thermal-chain", "three-stage-25W", "inputs.json"))));
        await Settle();
        Click(view, CalcSheetView.SaveCaption);
        await WaitFor(() => view.CurrentInstance is not null);
        Assert.False(view.IsSelfCheckOffered);
        Assert.Null(view.SelfCheckStatement);

        // E5 offers it once there is a saved draft to check, and stops once it is checked.
        await OpenFaultProtectionAsync(fx, "Offered");
        Assert.True(view.IsSelfCheckOffered);
        Assert.Contains(view.GetLogicalDescendants().OfType<CheckBox>(), c => AutomationProperties.GetName(c) == CalcInstanceService.SelfCheckCaption && c.IsEffectivelyVisible);
        Assert.Null(view.SelfCheckStatement);

        view.FillSelfCheck(true, Statement);
        Assert.Equal(Statement, view.SelfCheckStatement);
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Checked, () => view.StatusText);
        Assert.False(view.IsSelfCheckOffered);
        Assert.Null(view.SelfCheckStatement);
    }

    [AvaloniaFact]
    public async Task S8_Lifting_Offers_No_Self_Check()
    {
        await using var fx = await StartAsync("P-SELF-S8", liftingOn: true);
        var view = fx.View;
        fx.SignInAs(Preparer, "Pat Preparer");

        view.SelectTemplate("struct.lifting");
        await Settle();
        view.SetField("mode", choice: "EN 13155");

        foreach (var input in view.SelectedTemplate!.Template.Inputs)
            view.SetField(input.Symbol, provenance: "drawing", reference: "DRG-100 rev A");

        await Settle();
        Click(view, CalcSheetView.SaveCaption);
        await WaitFor(() => view.CurrentInstance is not null);
        Assert.True(view.IsGovernancePanelVisible);
        Assert.False(view.IsSelfCheckOffered);
    }

    [AvaloniaFact]
    public async Task The_Preparer_Checks_Their_Own_E5_Calculation_Through_The_Form_And_The_Issued_Sheet_Prints_The_Statement()
    {
        await using var fx = await StartAsync("P-SELF-CHECK");
        var view = fx.View;
        fx.SignInAs(Preparer, "Pat Preparer");
        await OpenFaultProtectionAsync(fx, "IND self");
        var id = view.CurrentInstance!.Id;

        // Unticked: refused as before, now with the self-check named as the way out.
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.StatusText.Contains("needs an independent check", StringComparison.Ordinal), () => view.StatusText);
        Assert.Contains($"tick \"{CalcInstanceService.SelfCheckCaption}\"", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await fx.Service.Store.FindInstanceAsync(id))!.Status);

        // Ticked with too little said: refused, in a sentence naming the calculation.
        view.FillSelfCheck(true, "Looks fine.");
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.StatusText.Contains("at least 20 characters", StringComparison.Ordinal), () => view.StatusText);
        Assert.StartsWith("'IND self'", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await fx.Service.Store.FindInstanceAsync(id))!.Status);
        Assert.True(view.IsSelfCheckOffered);

        // The statement box takes no more than the cap.
        Assert.Equal(CalcInstanceService.MaxSelfCheckStatementLength, view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Self-check statement").MaxLength);

        // Ticked with a statement: checked by the preparer, recorded, audited.
        view.FillSelfCheck(true, Statement);
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Checked, () => view.StatusText);
        Assert.Equal("Checked by the preparer (self-check recorded). Issue when ready.", view.StatusText);

        var checkedInstance = (await fx.Service.Store.FindInstanceAsync(id))!;
        Assert.Equal(Preparer, checkedInstance.SelfCheck!.Principal);
        Assert.Equal(Statement, checkedInstance.SelfCheck.Statement);
        Assert.Equal(Preparer, checkedInstance.CheckedBy);

        var row = Assert.Single(fx.Audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);
        Assert.Equal(Statement, row.Detail!["Statement"]);
        Assert.Equal(Preparer, row.Detail["CheckerPrincipal"]);
        Assert.Equal("IND self", row.Detail["Calculation"]);

        // Issued: the sheet prints it, as text and in the PDF.
        Click(view, CalcSheetView.IssueCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Issued, () => view.StatusText);
        var issued = (await fx.Service.Store.FindInstanceAsync(id))!;
        Assert.Contains("Self-checked by the preparer: " + Statement, issued.Issue!.SheetText, StringComparison.Ordinal);

        var sheet = await fx.Service.SheetAsync(issued);
        Assert.Equal(Statement, sheet.Header.SelfCheck);
        // The extractor joins the PDF's wrapped lines without a space, so the line is checked by its head and its tail.
        var pdf = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(sheet).ToArray());
        Assert.Contains("Self-checked by the preparer: No second engineer", pdf, StringComparison.Ordinal);
        Assert.Contains("by hand and they agree.", pdf, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_Different_Person_Checks_As_Before_With_No_Self_Check_Recorded()
    {
        await using var fx = await StartAsync("P-SELF-OTHER");
        var view = fx.View;
        fx.SignInAs(Preparer, "Pat Preparer");
        await OpenFaultProtectionAsync(fx, "IND other");
        var id = view.CurrentInstance!.Id;

        fx.SignInAs("S-1-5-21-1111111111-2222222222-3333333333-2002", "Chris Checker");
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Checked, () => view.StatusText);
        Assert.Equal("Checked. Issue when ready.", view.StatusText);

        var checkedInstance = (await fx.Service.Store.FindInstanceAsync(id))!;
        Assert.Null(checkedInstance.SelfCheck);
        Assert.DoesNotContain(fx.Audit.Rows, r => r.Action == CalcInstanceService.SelfCheckedActionName);
        Assert.DoesNotContain("Self-checked", CalcSheetTextRenderer.Render(await fx.Service.SheetAsync(checkedInstance)), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_Pack_Holding_The_E5_Calculation_Is_Self_Checked_From_The_Packs_Panel()
    {
        await using var fx = await StartAsync("P-SELF-PACK");
        var view = fx.View;
        fx.SignInAs(Preparer, "Pat Preparer");
        await OpenFaultProtectionAsync(fx, "IND pack member");
        var memberId = view.CurrentInstance!.Id;

        var pack = await fx.Service.CreatePackAsync(fx.ProjectId, "Self-checked pack", [memberId]);
        await fx.Coordinator.RefreshListsAsync();
        await WaitFor(() => view.Packs.Count == 1 && !view.IsBusy);
        view.ChoosePack(pack.Id);
        await Settle();
        Assert.Equal(pack.Id, view.SelectedPackId);
        view.FillPackCheck("Tempest Engineering", "Checked the pack in the panel.");
        Assert.True(view.CanCheckPack);
        Assert.Null(view.PackSelfCheckStatement);

        // Unticked: the pack is refused and the self-check named.
        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.StatusText.Contains("needs an independent check", StringComparison.Ordinal), () => view.StatusText);
        Assert.Contains(CalcInstanceService.SelfCheckCaption, view.StatusText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await fx.Service.Store.FindPackAsync(pack.Id))!.Status);

        // Ticked with a statement: the pack is checked by the preparer, the statement kept on the pack and audited, the box cleared.
        view.FillPackSelfCheck(true, Statement);
        Assert.Equal(Statement, view.PackSelfCheckStatement);
        Click(view, CalcSheetView.CheckPackCaption);
        await WaitFor(() => view.Packs.SingleOrDefault()?.Status == CalcInstanceStatus.Checked, () => view.StatusText);
        Assert.StartsWith("Pack checked by", view.StatusText, StringComparison.Ordinal);
        Assert.Null(view.PackSelfCheckStatement);

        var checkedPack = (await fx.Service.Store.FindPackAsync(pack.Id))!;
        Assert.Equal(Statement, checkedPack.SelfCheck!.Statement);
        Assert.Equal(Preparer, checkedPack.SelfCheck.Principal);
        var row = Assert.Single(fx.Audit.Rows, r => r.Action == CalcInstanceService.PackSelfCheckedActionName);
        Assert.Equal("Self-checked pack", row.Detail!["Pack"]);
        Assert.Equal(Statement, row.Detail["Statement"]);

        // The member's sheet carries the pack's self-check.
        var member = (await fx.Service.Store.FindInstanceAsync(memberId))!;
        Assert.Null(member.SelfCheck);
        Assert.Equal(Statement, (await fx.Service.SheetAsync(member)).Header.SelfCheck);
    }

    // ------------------------------------------------------------------
    // Fixture
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_Self_Checked_Calculation_And_Pack_Carry_The_Mark_In_The_Lists()
    {
        await using var fx = await StartAsync("P-SELF-MARK");
        var view = fx.View;
        fx.SignInAs(Preparer, "Pat Preparer");
        await OpenFaultProtectionAsync(fx, "IND marked");
        var id = view.CurrentInstance!.Id;

        // A draft carries no mark.
        await fx.Coordinator.RefreshListsAsync();
        await WaitFor(() => view.SavedCalculations.Any(r => r.Id == id), () => view.StatusText);
        Assert.DoesNotContain(CalcSheetView.SelfCheckedListMark, view.SavedCalculations.Single(r => r.Id == id).Label, StringComparison.Ordinal);

        // A pack the preparer self-checks carries it: a second draft of the same inputs (the form holds the first), in a pack checked through the service.
        var member = await fx.Service.CreateAsync(FaultProtection, fx.ProjectId, "IND pack marked", view.CurrentInstance!.Inputs);
        var pack = await fx.Service.CreatePackAsync(fx.ProjectId, "Marked pack", [member.Id]);
        var packCheck = await fx.Service.CheckPackAsync(pack.Id, "Pat Preparer", "Tempest Engineering", "Checked the pack.", CheckOutcome.Accepted, selfCheckStatement: Statement);
        Assert.True(packCheck.Succeeded, packCheck.Reason);
        await fx.Coordinator.RefreshListsAsync();
        await WaitFor(() => view.Packs.Any(p => p.Id == pack.Id && p.SelfChecked), () => string.Join("; ", view.Packs.Select(p => p.Label)));
        Assert.Contains(" · Checked · " + CalcSheetView.SelfCheckedListMark + " · 1 calculation(s)", view.Packs.Single(p => p.Id == pack.Id).Label, StringComparison.Ordinal);

        // The calculation the preparer self-checks in the form carries it too, like STALE: after the status, in the same list row.
        view.FillSelfCheck(true, Statement);
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Checked, () => view.StatusText);
        await fx.Coordinator.RefreshListsAsync();
        await WaitFor(() => view.SavedCalculations.Any(r => r.Id == id && r.SelfChecked), () => string.Join("; ", view.SavedCalculations.Select(r => r.Label)));

        var row = view.SavedCalculations.Single(r => r.Id == id);
        Assert.Contains(" · Checked · " + CalcSheetView.SelfCheckedListMark, row.Label, StringComparison.Ordinal);
        Assert.Equal("SELF-CHECKED", CalcSheetView.SelfCheckedListMark);
        Assert.DoesNotContain(CalcSheetView.SelfCheckedListMark, view.SavedCalculations.Single(r => r.Id == member.Id).Label, StringComparison.Ordinal);
    }

    private static CalcInstanceService BuildService(WorkspaceHost host, bool liftingOn, RecordingAudit audit)
    {
        var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var features = new CalcFeatureFlags(liftingOn ? [CalcFeatures.Lifting] : [], "the test's install file");
        return new CalcInstanceService(
            new CalcCatalogue(CalcCalculatorComposition.All(), features),
            new CalcInstanceStore(domain.PersistenceStore),
            host.EvidenceService!,
            domain,
            new CalcSheetPdfRenderer(),
            applicationVersion: "TempestOS test",
            events: (IEventBus)host.Services!.GetService(typeof(IEventBus)),
            principals: (IPrincipalDirectory)host.Services!.GetService(typeof(IPrincipalDirectory)),
            audit: audit);
    }

    private static async Task<Fixture> StartAsync(string code, bool liftingOn = false)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, code + " project");
        await host.ProjectContext!.OpenAsync(project.Id);
        var audit = new RecordingAudit();
        var service = BuildService(host, liftingOn, audit);
        var view = new CalcSheetView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => host.SessionPrincipal?.IdentityId, (IEventBus)host.Services!.GetService(typeof(IEventBus)));
        await coordinator.RefreshAsync();
        return new Fixture(host, service, view, coordinator, project.Id, audit);
    }

    /// <summary>Opens E5 with its worked example (a source on every input, so nothing but independence stands in the way of a check) and saves it under <paramref name="name"/>.</summary>
    private static async Task OpenFaultProtectionAsync(Fixture fx, string name)
    {
        var view = fx.View;
        view.SelectTemplate(FaultProtection);
        await Settle();
        Assert.True(fx.Coordinator.ImportInputsText(File.ReadAllText(Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples", "electrical", FaultProtection, "workshop-radial-c16", "inputs.json"))));
        await Settle();
        view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Calculation name").Text = name;
        Click(view, CalcSheetView.SaveCaption);
        await WaitFor(() => view.CurrentInstance is { } i && i.Name == name, () => view.StatusText);
        Assert.True(view.LastRun is { IsBlocked: false }, string.Join("; ", view.LastRun?.Blocks.Select(b => b.Message) ?? []));
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

    private static async Task WaitFor(Func<bool> condition, Func<string>? why = null)
    {
        for (var i = 0; i < 400; i++)
        {
            Dispatcher.UIThread.RunJobs();

            if (condition())
                return;

            await Task.Delay(25);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition(), "The form did not reach the expected state in time. " + why?.Invoke());
    }
}

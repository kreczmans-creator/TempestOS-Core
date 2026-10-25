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
using CoreEvidence = Tempest.Core.Evidence.Evidence;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Editors.Sections;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// `ADR-0173` (S8 review H2, M9, Low 13) on the worksheet form: Attest and Countersign are offered only for a template that demands them, each is
/// recorded under the signed-in person, and an S8 calculation can be taken from a draft to an issued sheet through the form alone. The Evidence of
/// a calculation is checked, issued and revised in its worksheet, so its editor offers "Open worksheet" instead.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetGovernanceTests
{
    private const string Author = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string Attester = "S-1-5-21-1111111111-2222222222-3333333333-1002";
    private const string FirstChecker = "S-1-5-21-1111111111-2222222222-3333333333-1003";
    private const string SecondChecker = "S-1-5-21-1111111111-2222222222-3333333333-1004";

    private sealed record Fixture(WorkspaceHost Host, CalcInstanceService Service, CalcSheetView View, CalcSheetCoordinator Coordinator, Guid ProjectId) : IAsyncDisposable
    {
        public void SignInAs(string id, string name) => Host.SwitchPrincipal(new SessionPrincipal(id, name, SessionRole.Engineer));

        public async ValueTask DisposeAsync()
        {
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Lifting_Calculation_Is_Attested_Checked_Countersigned_And_Issued_Through_The_Form_By_Four_People()
    {
        await using var fx = await StartAsync("P-GOV", liftingOn: true);
        var view = fx.View;

        fx.SignInAs(Author, "Alex Author");
        view.SelectTemplate("struct.lifting");
        await Settle();
        view.SetField("mode", choice: "EN 13155");
        CiteEverything(view);
        await Settle();
        Assert.True(view.IsGovernancePanelVisible);
        Assert.Contains("Signatures are not authenticated by TempestOS", view.GovernanceNoticeText, StringComparison.Ordinal);
        Assert.False(view.CanAttest);
        Assert.True(view.LastRun is { IsBlocked: false }, string.Join("; ", view.LastRun?.Blocks.Select(b => b.Message) ?? []));
        Click(view, CalcSheetView.SaveCaption);
        await WaitFor(() => view.CurrentInstance is not null);
        var id = view.CurrentInstance!.Id;
        Assert.Contains("Not attested", view.AttestStateText, StringComparison.Ordinal);

        // The author cannot attest their own calculation.
        view.FillAttest("Alex Author", "CEng MIStructE", "I am competent to attest this lifting calculation and have reviewed it.");
        Assert.True(view.CanAttest);
        Click(view, CalcSheetView.AttestCaption);
        await WaitFor(() => view.StatusText.Contains("author", StringComparison.OrdinalIgnoreCase));
        Assert.Null((await fx.Service.Store.FindInstanceAsync(id))!.Attestation);

        // A different signed-in person can; the record names that principal.
        fx.SignInAs(Attester, "Alice Attester");
        view.FillAttest("Alice Attester", "CEng MIStructE", "I am competent to attest this lifting calculation and have reviewed it.");
        Click(view, CalcSheetView.AttestCaption);
        await WaitFor(() => view.CurrentInstance?.Attestation is not null);
        var attested = (await fx.Service.Store.FindInstanceAsync(id))!;
        Assert.Equal(Attester, attested.Attestation!.Principal);
        Assert.Equal("Alice Attester", attested.Attestation.Name);
        Assert.Contains("Attested by Alice Attester", view.AttestStateText, StringComparison.Ordinal);

        // The attester cannot also be the first checker; a different person is.
        Assert.False(view.CanCountersign);
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.StatusText.Length > 0 && !view.StatusText.StartsWith("Attested", StringComparison.Ordinal));
        Assert.Equal(CalcInstanceStatus.Draft, (await fx.Service.Store.FindInstanceAsync(id))!.Status);

        fx.SignInAs(FirstChecker, "Chris Checker");
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Checked, () => view.StatusText);

        // Issue is refused until a second, independent person countersigns; Countersign is now offered, Attest is not.
        Assert.False(view.CanAttest);
        Assert.False(view.CanCountersign);
        view.FillCountersign("Second Co", "Independently checked the lug and spreader capacities against the standard.");
        Assert.True(view.CanCountersign);
        Click(view, CalcSheetView.IssueCaption);
        await WaitFor(() => view.StatusText.Contains("second", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(CalcInstanceStatus.Checked, (await fx.Service.Store.FindInstanceAsync(id))!.Status);

        // The first checker cannot be the second.
        Click(view, CalcSheetView.CountersignCaption);
        await WaitFor(() => view.StatusText.Contains("first", StringComparison.OrdinalIgnoreCase) || view.StatusText.Contains("same", StringComparison.OrdinalIgnoreCase));
        Assert.Null((await fx.Service.Store.FindInstanceAsync(id))!.SecondCheck);

        fx.SignInAs(SecondChecker, "Sam Second");
        Click(view, CalcSheetView.CountersignCaption);
        await WaitFor(() => view.CurrentInstance?.SecondCheck is not null);
        Assert.Equal(SecondChecker, view.CurrentInstance!.SecondCheck!.Principal);
        Assert.False(view.CanCountersign);

        Click(view, CalcSheetView.IssueCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Issued);
        var issued = (await fx.Service.Store.FindInstanceAsync(id))!;
        Assert.Equal(CalcInstanceStatus.Issued, issued.Status);
        Assert.Contains("lifting feature enabled", issued.Issue!.FeatureNote, StringComparison.Ordinal);
        Assert.False(view.CanAttest);
    }

    [AvaloniaFact]
    public async Task A_Rejected_Second_Check_Is_Recorded_And_Blocks_Issue()
    {
        await using var fx = await StartAsync("P-GOV-REJ", liftingOn: true);
        var view = fx.View;
        fx.SignInAs(Author, "Alex Author");
        view.SelectTemplate("struct.lifting");
        await Settle();
        view.SetField("mode", choice: "EN 13155");
        CiteEverything(view);
        await Settle();
        Click(view, CalcSheetView.SaveCaption);
        await WaitFor(() => view.CurrentInstance is not null);

        fx.SignInAs(Attester, "Alice Attester");
        view.FillAttest("Alice Attester", "CEng MIStructE", "I am competent to attest this lifting calculation and have reviewed it.");
        Click(view, CalcSheetView.AttestCaption);
        await WaitFor(() => view.CurrentInstance?.Attestation is not null);
        fx.SignInAs(FirstChecker, "Chris Checker");
        Click(view, CalcSheetView.CheckCaption);
        await WaitFor(() => view.CurrentInstance?.Status == CalcInstanceStatus.Checked, () => view.StatusText);

        fx.SignInAs(SecondChecker, "Sam Second");
        view.FillCountersign("Second Co", "The spreader capacity is not demonstrated for the stated sling angle.", CheckOutcome.Rejected);
        Click(view, CalcSheetView.CountersignCaption);
        await WaitFor(() => view.CurrentInstance?.SecondCheck is not null);
        Assert.False(view.CurrentInstance!.SecondCheck!.Accepted);

        Click(view, CalcSheetView.IssueCaption);
        await WaitFor(() => view.StatusText.Contains("reject", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(CalcInstanceStatus.Checked, (await fx.Service.Store.FindInstanceAsync(view.CurrentInstance.Id))!.Status);
    }

    [AvaloniaFact]
    public async Task A_Template_With_No_Extra_Governance_Shows_No_Sign_Off_Panel()
    {
        await using var fx = await StartAsync("P-GOV-NONE", liftingOn: true);
        fx.View.SelectTemplate("struct.section-properties");
        await Settle();
        Assert.False(fx.View.IsGovernancePanelVisible);
        Assert.False(fx.View.CanAttest);
        Assert.False(fx.View.CanCountersign);
    }

    [AvaloniaFact]
    public async Task Opening_A_Calculation_Whose_Calculator_Is_Switched_Off_Names_The_Feature_Not_A_Missing_Build()
    {
        await using var fx = await StartAsync("P-GOV-OFF", liftingOn: true);
        fx.SignInAs(Author, "Alex Author");
        var made = await fx.Service.CreateAsync("struct.lifting", fx.ProjectId, "Lug");

        // The same store, with the lifting feature switched off.
        var off = BuildService(fx.Host, liftingOn: false);
        var view = new CalcSheetView();
        var coordinator = new CalcSheetCoordinator(off, view, fx.Host.ProjectContext!, () => fx.Host.SessionPrincipal?.IdentityId);
        await coordinator.OpenAsync(made.Id);

        Assert.Contains("switched off", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("lifting", view.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("not registered in this build", view.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Issued_Pdf_Prints_How_The_Optional_Feature_Was_Switched_On()
    {
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags([CalcFeatures.Lifting], "machine policy HKLM\\SOFTWARE\\Policies\\TempestOS\\Features"));
        var entry = catalogue.Find("struct.lifting")!;
        var inputs = CalcTemplateHasher.DefaultInputs(entry.Template);
        var result = CalcRunner.Run(entry.Calculator, new CalcInputSet(inputs.Values.Select(v => v.Symbol == "mode" ? CalcInputValue.Choice("mode", "EN 13155") : v)));
        var at = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P", "C", entry.Template.Title, "A", CalcSheetStatus.Issued, "author", "checker", at, at, at);
        var footer = new CalcSheetFooter(entry.Reference, entry.ShortHash, "e", 1, 1, at, "TempestOS", "lifting feature enabled (machine policy HKLM\\SOFTWARE\\Policies\\TempestOS\\Features)");
        var sheet = CalcSheetBuilder.Build(entry.Template, new CalcInputSet(inputs.Values.Select(v => v.Symbol == "mode" ? CalcInputValue.Choice("mode", "EN 13155") : v)), result, header, footer);

        var text = Quotations.PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(sheet).ToArray());
        Assert.Contains("Optional feature", text, StringComparison.Ordinal);
        Assert.Contains("lifting feature enabled", text, StringComparison.Ordinal);
        Assert.Contains("Optional feature: lifting feature enabled", CalcSheetTextRenderer.Render(sheet), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Starting_The_Host_Marks_Evidence_A_Worksheet_Made_Before_The_Marker_As_Managed()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid oldEvidenceId;

        var first = new WorkspaceHost(root);

        try
        {
            await first.StartAsync();
            var project = await first.ProjectDirectory!.CreateAsync("P-SWEEP", "Sweep project");
            await first.ProjectContext!.OpenAsync(project.Id);
            var instance = await first.CalcInstances!.CreateAsync("struct.section-properties", project.Id, "Old section");

            // Evidence made the way it was before the marker existed: unmanaged, then pointed at by the instance.
            var old = await first.EvidenceService!.CreateAsync(project.Id, "Old evidence", EvidenceClassification.Calculation);
            await first.CalcInstances.Store.SaveInstanceAsync(instance with { EvidenceId = old.Id, UpdatedAt = instance.UpdatedAt.AddSeconds(1) });
            oldEvidenceId = old.Id;
            Assert.Null(old.ManagedBy);
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);

        try
        {
            await second.StartAsync();
            var domain = (EngineeringDomainContext)second.Services!.GetService(typeof(EngineeringDomainContext));
            var evidence = (CoreEvidence)(await domain.Repository.FindAsync(oldEvidenceId))!;

            Assert.Equal(CoreEvidence.CalcWorksheetOwner, evidence.ManagedBy);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await second.EvidenceService!.RecordCheckAsync(oldEvidenceId, "Chris Checker", "Org", "ok", CheckOutcome.Accepted)).Refusal);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Evidence_Of_A_Calculation_Offers_Open_Worksheet_Not_Check_Issue_Or_Revise()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-GOV-EVD", "Managed evidence");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var instance = await host.CalcInstances!.CreateAsync("struct.section-properties", project.Id, "Floor section");
            var calcEvidence = (CoreEvidence)(await ((EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext))).Repository.FindAsync(instance.EvidenceId!.Value))!;
            Assert.Equal(CoreEvidence.CalcWorksheetOwner, calcEvidence.ManagedBy);

            var editor = await OpenEvidenceEditorAsync(window, calcEvidence.Id);
            var buttons = editor.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string).ToList();
            Assert.DoesNotContain("Check", buttons);
            Assert.DoesNotContain("Issue", buttons);
            Assert.DoesNotContain("Revise", buttons);
            Assert.Contains(EvidenceLifecycleSection.OpenWorksheetCaption, buttons);
            Assert.Contains(editor.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).Contains("belongs to a calculation worksheet", StringComparison.Ordinal));

            // "Open worksheet" lands on that calculation in the Worksheets surface.
            var open = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, EvidenceLifecycleSection.OpenWorksheetCaption));
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var sheets = GetPrivateField<CalcSheetView>(GetPrivateField<CalcSheetCoordinator>(window, "_calcSheetCoordinator"), "_view");
            await RenderUntilAsync(window, () => sheets.CurrentInstance?.Id == instance.Id);

            // Plain Evidence is unchanged: Check is offered and there is no worksheet to open.
            var plain = await host.EvidenceService!.CreateAsync(project.Id, "Plain report", EvidenceClassification.Report);
            var plainEditor = await OpenEvidenceEditorAsync(window, plain.Id);
            var plainButtons = plainEditor.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string).ToList();
            Assert.Contains("Check", plainButtons);
            Assert.DoesNotContain(EvidenceLifecycleSection.OpenWorksheetCaption, plainButtons);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Shipped_Host_Engine_Refuses_The_Lifting_Definitions_Run_By_Id_While_The_Flag_Is_Off()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var engine = (Tempest.Core.Calculations.ICalculationEngine)host.Services!.GetService(typeof(Tempest.Core.Calculations.ICalculationEngine));

            // The refusal comes before the lookup and before the input is read, so the input type is immaterial.
            foreach (var id in new[] { "calc.lifting-lug-pin-joint", "struct.lifting" })
            {
                var refused = await Assert.ThrowsAsync<Tempest.Core.Calculations.CalculationFeatureDisabledException>(() => engine.ExecuteAsync<int, int>(id, 1));
                Assert.Contains("switched off", refused.Message, StringComparison.Ordinal);
            }

            // The catalogue of the same host withholds the worksheet in the same words.
            var viaEngine = await Assert.ThrowsAsync<Tempest.Core.Calculations.CalculationFeatureDisabledException>(() => engine.ExecuteAsync<int, int>("struct.lifting", 1));
            Assert.Equal(host.CalcCatalogue!.FeatureRefusal("struct.lifting"), viaEngine.Message);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Shipped_Host_Ignores_The_Environment_The_Command_Line_The_Current_Directory_And_Every_Appsettings_File()
    {
        // Only the machine's administrator source counts; if this machine has one that switches lifting on, the test has nothing to say.
        if (CalcFeatureFlags.FromSource(CalcFeatureSources.ForThisMachine(), out _).IsEnabled(CalcFeatures.Lifting))
            return;

        var elsewhere = Path.Combine(Path.GetTempPath(), "tempest-host-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(elsewhere);
        const string on = "{ \"Calc\": { \"Features\": { \"Lifting\": true } }, \"Lifting\": true }";
        File.WriteAllText(Path.Combine(elsewhere, "appsettings.json"), on);
        var beside = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var hadBeside = File.Exists(beside);
        var besideBefore = hadBeside ? File.ReadAllText(beside) : null;
        var oldCwd = Directory.GetCurrentDirectory();
        var variables = new[] { "TEMPEST_Calc__Features__Lifting", "Calc__Features__Lifting", "Calc:Features:Lifting" };
        var oldValues = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(), commandLineArgs: ["--Calc:Features:Lifting=true", "Calc:Features:Lifting=true"]);

        try
        {
            Directory.SetCurrentDirectory(elsewhere);

            foreach (var variable in variables)
                Environment.SetEnvironmentVariable(variable, "true");

            File.WriteAllText(beside, on);
            await host.StartAsync();

            Assert.Null(host.CalcCatalogue!.Find("struct.lifting"));
            Assert.False(host.CalcCatalogue.Features.IsEnabled(CalcFeatures.Lifting));
            Assert.True(host.CalcCatalogue.IsWithheld("struct.lifting"));
            var engine = (Tempest.Core.Calculations.ICalculationEngine)host.Services!.GetService(typeof(Tempest.Core.Calculations.ICalculationEngine));
            await Assert.ThrowsAsync<Tempest.Core.Calculations.CalculationFeatureDisabledException>(() => engine.ExecuteAsync<int, int>("calc.lifting-lug-pin-joint", 1));
        }
        finally
        {
            Directory.SetCurrentDirectory(oldCwd);

            for (var i = 0; i < variables.Length; i++)
                Environment.SetEnvironmentVariable(variables[i], oldValues[i]);

            if (hadBeside)
                File.WriteAllText(beside, besideBefore!);
            else
                File.Delete(beside);

            try { Directory.Delete(elsewhere, true); } catch (IOException) { }

            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Host_Given_An_Administrator_Source_That_Switches_Lifting_On_Offers_It_And_Names_The_Source()
    {
        var source = new WindowsPolicyFeatureSource((_, name) => name == "Lifting" ? new PolicyValue(true, true, 1) : default);
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(), calcFeatureSource: source);

        try
        {
            await host.StartAsync();

            Assert.NotNull(host.CalcCatalogue!.Find("struct.lifting"));
            Assert.Contains("machine policy", host.CalcCatalogue.Features.SourceOf(CalcFeatures.Lifting), StringComparison.Ordinal);
            var engine = (Tempest.Core.Calculations.ICalculationEngine)host.Services!.GetService(typeof(Tempest.Core.Calculations.ICalculationEngine));

            // Not refused as switched off: the registered definition simply is not an int -> int one.
            await Assert.ThrowsAsync<Tempest.Core.Calculations.CalculationDefinitionNotFoundException>(() => engine.ExecuteAsync<int, int>("calc.lifting-lug-pin-joint", 1));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------

    private static CalcInstanceService BuildService(WorkspaceHost host, bool liftingOn)
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
            principals: (IPrincipalDirectory)host.Services!.GetService(typeof(IPrincipalDirectory)));
    }

    private static async Task<Fixture> StartAsync(string code, bool liftingOn)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, code + " project");
        await host.ProjectContext!.OpenAsync(project.Id);
        var service = BuildService(host, liftingOn);
        var view = new CalcSheetView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => host.SessionPrincipal?.IdentityId, (IEventBus)host.Services!.GetService(typeof(IEventBus)));
        await coordinator.RefreshAsync();
        return new Fixture(host, service, view, coordinator, project.Id);
    }

    private static Task RenderUntilAsync(MainWindow window, Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null) =>
        DesktopTestHelpers.WaitUntilAsync(condition, 5, null, DesktopTestHelpers.OpenPhaseOf(window), what);

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string name) =>
        instance.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{name}' not found on {instance.GetType().Name}.");

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1600, 1000));
            window.Arrange(new Avalonia.Rect(0, 0, 1600, 1000));
        }
    }

    private static async Task<ObjectEditorView> OpenEvidenceEditorAsync(MainWindow window, Guid id)
    {
        var openEvidenceRecord = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
        await (Task)openEvidenceRecord.Invoke(window, [id, CoreEvidence.CanonicalKind])!;
        LayOut(window);
        var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault(e => GetPrivateField<Guid>(e, "_objectId") == id);
        Assert.NotNull(editor);
        return editor!;
    }

    /// <summary>A check refuses an input with no source, so every input of the shown template is given one.</summary>
    private static void CiteEverything(CalcSheetView view)
    {
        foreach (var input in view.SelectedTemplate!.Template.Inputs)
            view.SetField(input.Symbol, provenance: "drawing", reference: "DRG-100 rev A");
    }

    private static Button ButtonNamed(CalcSheetView view, string name) =>
        view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    private static void Click(CalcSheetView view, string name)
    {
        var button = ButtonNamed(view, name);
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

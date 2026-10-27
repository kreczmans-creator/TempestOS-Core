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
using Tempest.Desktop.Composition;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// ADR-0177 (DC-14) on the worksheet form: a calculation made on a template version whose formulas are not installed. An issued one opens as
/// the sheet it stored, read-only, with one sentence saying what became of its version and whether a re-run on the installed version reproduces
/// it exactly; Revise carries its inputs to the current version. A draft opens as a new sheet on the current version with its inputs (renamed
/// inputs carried under their new names, C-2). A new sheet is made on the current version even when an older one is kept beside it.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetSupersededVersionTests
{
    private const string Id = "test.desktop-versioned";

    /// <summary>
    /// The Desktop stress worksheet at a version. 1.0.0 calls the load <c>F_old</c>; 1.1.0 renames it <c>F</c> with the alias <c>F_old</c> (C-2) and
    /// keeps the formulas, so a 1.0.0 record is reproduced; 1.2.0 changes the formula, so it is not.
    /// </summary>
    private sealed class VersionedStress : ICalcCalculator
    {
        private readonly string _load;
        private readonly string _sigma;

        public VersionedStress(string version, string load, string? alias, string sigma)
        {
            _load = load;
            _sigma = sigma;
            var original = new TestWorksheetCalculator().Template;
            Template = original with
            {
                Id = Id,
                Version = version,
                Inputs = [.. original.Inputs.Select(i => i.Symbol == "F" ? i with { Symbol = load, Aliases = alias is null ? null : [alias] } : i)],
                TestCases = [],
            };
        }

        public static VersionedStress V100 => new("1.0.0", "F_old", null, "F_old / A");

        public static VersionedStress V110 => new("1.1.0", "F", "F_old", "F / A");

        public static VersionedStress V120 => new("1.2.0", "F", "F_old", "1.1 * F / A");

        public CalcTemplate Template { get; }

        /// <summary>Whether the last run of any version was made on the UI thread: the re-run a stored record's opening makes must not be.</summary>
        public static bool? RanOnUiThread { get; set; }

        public void Run(CalcRunContext c)
        {
            RanOnUiThread = Dispatcher.UIThread.CheckAccess();
            c.Input(_load);
            c.Input("A");
            c.Input("sigma_allow");
            c.Input("grade");
            c.Assign("sigma", _sigma, "MPa", "Direct stress", "EN 1993-1-1 §6.2.3");
            c.Assign("u", "sigma / sigma_allow", null, "Utilisation");
            c.Output("sigma", "Direct stress");
            c.Output("u", "Utilisation");
            c.CheckUtilisation("Direct stress", "u", "EN 1993-1-1 §6.2.3");
        }
    }

    /// <summary>A margin p in bar on 1.0.0 and in kPa on 2.0.0, which says so (ADR-0177 review, UnitChangedAt).</summary>
    private sealed class Margin(string version, string unit, double @default, bool changed) : ICalcCalculator
    {
        public const string MarginId = "test.desktop-margin";

        public static Margin V100 => new("1.0.0", "bar", 0.5, false);

        public static Margin V200 => new("2.0.0", "kPa", 50, true);

        public CalcTemplate Template { get; } = new(MarginId, version, CalcDiscipline.Mechanical, "Margin", "", "",
            [new CalcInputSpec("p", "Pre-charge margin", CalcInputKind.Quantity, unit, Default: @default, HardMin: 0, HardMax: changed ? 200 : 2, UnitChangedAt: changed ? "2.0.0" : null, UnitWas: changed ? "bar" : null)],
            [], [], [], []);

        public void Run(CalcRunContext c)
        {
            c.Input("p");
            c.Assign("p_out", "p", "Pa", "Margin");
            c.Output("p_out", "Margin");
        }
    }

    private sealed record Fixture(WorkspaceHost Host, Guid ProjectId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }

    private static IReadOnlyList<CalcInputValue> InputsOn100(double load = 12) =>
    [
        CalcInputValue.Quantity("F_old", load, "kN", CalcProvenance.Drawing("DRG-001", "B")),
        CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("DRG-001", "B")),
        CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("SPEC-7 §4")),
        CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed("Not stated.")),
    ];

    [AvaloniaFact]
    public async Task An_Issued_Calculation_On_A_Superseded_Version_Opens_As_Its_Stored_Sheet_Read_Only_And_Revise_Carries_It_To_The_Current_Version()
    {
        await using var fx = await StartAsync("P-VSN-ISSUED");
        var issued = await IssuedOn100Async(fx, "Column stress");

        // The next build has 1.1.0, which renamed the load input and kept the formulas.
        var (view, coordinator) = Open(fx, [VersionedStress.V110]);
        await coordinator.OpenAsync(issued.Id);

        Assert.True(view.IsReadOnly);
        Assert.Null(view.SelectedTemplate);
        Assert.Equal(issued.Id, view.CurrentInstance?.Id);
        var text = view.GetLogicalDescendants().OfType<SelectableTextBlock>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.StoredRecordName);
        Assert.Equal(issued.Issue!.SheetText, text.Text);
        Assert.Contains("Column stress", text.Text, StringComparison.Ordinal);
        Assert.Contains($"was made on {Id}@1.0.0, which has been revised to {Id}@1.1.0", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("sheet as it was issued, read-only", view.StatusText, StringComparison.Ordinal);
        Assert.Contains($"re-ran on {Id}@1.1.0, which has replaced {Id}@1.0.0: all 2 stored outputs and 1 stored check come out exactly as stored", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("Revise it to carry its inputs", view.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("not registered", view.StatusText, StringComparison.Ordinal);

        foreach (var caption in new[] { CalcSheetView.SaveCaption, CalcSheetView.CheckCaption, CalcSheetView.IssueCaption, CalcSheetView.ImportCaption })
            Assert.False(Available(view, caption), $"{caption} is available on a stored record");

        // Nothing was written by opening it.
        var store = new CalcInstanceStore(Domain(fx).PersistenceStore);
        AssertUnchanged(issued, await store.FindInstanceAsync(issued.Id));

        // Revise starts revision B on the current version with the inputs carried, the load under its new name; A is superseded, its sheet intact.
        Click(view, CalcSheetView.ReviseCaption);
        await WaitFor(() => view.CurrentInstance is { Status: CalcInstanceStatus.Draft }, () => view.StatusText);
        var next = view.CurrentInstance!;
        Assert.Equal($"{Id}@1.1.0", next.TemplateReference);
        Assert.Equal("B", next.Revision);
        Assert.Equal($"{Id}@1.1.0", view.SelectedTemplate?.Reference);
        Assert.False(view.IsReadOnly);
        Assert.Equal(12, view.ReadForm().Find("F")?.Number);
        var old = (await store.FindInstanceAsync(issued.Id))!;
        Assert.Equal(CalcInstanceStatus.Superseded, old.Status);
        Assert.Equal(issued.Issue, old.Issue);
        Assert.Equal(issued.Outputs, old.Outputs);
    }

    [AvaloniaFact]
    public async Task The_Re_Run_That_Opening_A_Stored_Record_On_A_Superseded_Version_Makes_Runs_Off_The_UI_Thread()
    {
        await using var fx = await StartAsync("P-VSN-THREAD");
        var issued = await IssuedOn100Async(fx, "Threaded stress");
        var (view, coordinator) = Open(fx, [VersionedStress.V110]);

        // The re-run is a whole calculation under the engine's limits; the form must not run it on the UI thread (ADR-0177 review).
        VersionedStress.RanOnUiThread = null;
        await coordinator.OpenAsync(issued.Id);

        Assert.True(view.IsReadOnly);
        Assert.Contains($"re-ran on {Id}@1.1.0", view.StatusText, StringComparison.Ordinal);
        Assert.False(VersionedStress.RanOnUiThread, "the re-run that opening a stored record makes ran on the UI thread");
    }

    [AvaloniaFact]
    public async Task An_Issued_Calculation_The_Installed_Version_Does_Not_Reproduce_Opens_Read_Only_With_A_Sentence_Saying_Why()
    {
        await using var fx = await StartAsync("P-VSN-DIFFERS");
        var issued = await IssuedOn100Async(fx, "Bracket stress");

        // 1.2.0 changed the stress formula: the issued numbers are not what it gives.
        var (view, coordinator) = Open(fx, [VersionedStress.V120]);
        await coordinator.OpenAsync(issued.Id);

        Assert.True(view.IsReadOnly);
        Assert.Null(view.SelectedTemplate);
        Assert.Contains($"cannot be re-run exactly on {Id}@1.2.0, which has replaced {Id}@1.0.0: sigma is ", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("where it was ", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("It stays as stored, read-only.", view.StatusText, StringComparison.Ordinal);
        Assert.False(Available(view, CalcSheetView.SaveCaption));
        Assert.True(Available(view, CalcSheetView.ReviseCaption));
        AssertUnchanged(issued, await new CalcInstanceStore(Domain(fx).PersistenceStore).FindInstanceAsync(issued.Id));
    }

    [AvaloniaFact]
    public async Task A_Checked_Calculation_On_A_Superseded_Version_Shows_Its_Stored_Results_And_Offers_Revise_Not_Issue()
    {
        await using var fx = await StartAsync("P-VSN-CHECKED");
        var first = BuildService(fx.Host, [VersionedStress.V100]);
        var created = await first.CreateAsync(Id, fx.ProjectId, "Checked stress", InputsOn100());
        Assert.True((await first.CheckAsync(created.Id, "J. Reviewer", "Client Co", "Checked.", CheckOutcome.Accepted)).Succeeded);

        var (view, coordinator) = Open(fx, [VersionedStress.V110]);
        await coordinator.OpenAsync(created.Id);

        Assert.True(view.IsReadOnly);
        var text = view.GetLogicalDescendants().OfType<SelectableTextBlock>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.StoredRecordName);
        Assert.Contains("Checked stress — rev A — Checked", text.Text, StringComparison.Ordinal);
        Assert.Contains("sigma — Direct stress = ", text.Text, StringComparison.Ordinal);
        Assert.Contains("Checked by J. Reviewer", text.Text, StringComparison.Ordinal);
        Assert.Contains("its stored results are shown read-only", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("then check and issue it again", view.StatusText, StringComparison.Ordinal);
        Assert.False(Available(view, CalcSheetView.IssueCaption));
        Assert.True(Available(view, CalcSheetView.ReviseCaption));

        Click(view, CalcSheetView.ReviseCaption);
        await WaitFor(() => view.CurrentInstance is { Status: CalcInstanceStatus.Draft, Revision: "B" }, () => view.StatusText);
        Assert.Equal($"{Id}@1.1.0", view.CurrentInstance!.TemplateReference);
        Assert.Equal(12, view.ReadForm().Find("F")?.Number);
    }

    [AvaloniaFact]
    public async Task An_Old_Draft_Opens_As_A_New_Sheet_On_The_Current_Version_With_Its_Inputs_Under_Their_New_Names_And_Is_Left_Unchanged()
    {
        await using var fx = await StartAsync("P-VSN-DRAFT");
        var first = BuildService(fx.Host, [VersionedStress.V100]);
        var draft = await first.CreateAsync(Id, fx.ProjectId, "Draft stress", InputsOn100(15));
        var store = new CalcInstanceStore(Domain(fx).PersistenceStore);
        var before = await store.FindInstanceAsync(draft.Id);

        var (view, coordinator) = Open(fx, [VersionedStress.V110]);
        await coordinator.OpenAsync(draft.Id);

        // A new, unsaved sheet on 1.1.0: the load carried under its new name F (C-2 alias), nothing dropped, the old draft untouched.
        Assert.Null(view.CurrentInstance);
        Assert.False(view.IsReadOnly);
        Assert.Equal($"{Id}@1.1.0", view.SelectedTemplate?.Reference);
        Assert.Equal(15, view.ReadForm().Find("F")?.Number);
        Assert.Equal("Draft stress (1.1.0)", view.CalculationName);
        Assert.Contains($"was made on {Id}@1.0.0, which has been revised to {Id}@1.1.0", view.StatusText, StringComparison.Ordinal);
        Assert.Contains("The old draft is left unchanged.", view.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("left out", view.StatusText, StringComparison.Ordinal);
        AssertUnchanged(before, await store.FindInstanceAsync(draft.Id));
    }

    [AvaloniaFact]
    public async Task A_New_Sheet_Is_Offered_On_The_Current_Version_Only_And_A_Kept_Older_Version_Still_Opens_Its_Own_Calculations()
    {
        await using var fx = await StartAsync("P-VSN-CURRENT");
        var issued = await IssuedOn100Async(fx, "Kept stress");

        // A build that keeps 1.0.0 beside 1.1.0 (a frozen copy): the catalogue offers 1.1.0 for new sheets, and the issued sheet opens on its own version.
        var (view, coordinator) = Open(fx, [VersionedStress.V100, VersionedStress.V110]);
        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        var leaves = tree.Items.OfType<TreeViewItem>().SelectMany(d => d.Items.OfType<TreeViewItem>()).Select(l => l.Header?.ToString() ?? string.Empty).ToList();
        Assert.Contains(leaves, l => l.Contains("v1.1.0", StringComparison.Ordinal));
        Assert.DoesNotContain(leaves, l => l.Contains("v1.0.0", StringComparison.Ordinal));

        await coordinator.OpenAsync(issued.Id);
        Assert.Equal($"{Id}@1.0.0", view.SelectedTemplate?.Reference);
        Assert.True(view.IsReadOnly);
        Assert.Contains("Opened 'Kept stress'", view.StatusText, StringComparison.Ordinal);
        Assert.Equal(12, view.ReadForm().Find("F_old")?.Number);
    }

    [AvaloniaFact]
    public async Task A_Bare_Stored_Number_Opens_In_The_Unit_It_Was_Recorded_In_And_A_New_Sheet_In_The_New_One()
    {
        await using var fx = await StartAsync("P-VSN-UNIT");
        var draft = await BuildService(fx.Host, [Margin.V100]).CreateAsync(Margin.MarginId, fx.ProjectId, "Bare margin", [CalcInputValue.Of("p", 0.3, CalcProvenance.Assumed("Test value."))]);
        Assert.Null(draft.Inputs.Single().Unit);

        // The next build reads p in kPa. The picker seeds from the record's own version, so the bare 0.3 shows as 0.3 bar, never 0.3 kPa.
        var (view, coordinator) = Open(fx, [Margin.V200]);
        var current = BuildService(fx.Host, [Margin.V200]).Catalogue.Get(Margin.MarginId);
        view.ShowTemplate(current, draft, null);
        var unit = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "p unit");
        Assert.Equal("bar", unit.SelectedItem);
        Assert.Equal(("bar", 0.3), (view.ReadForm().Find("p")!.Unit, view.ReadForm().Find("p")!.Number));

        // Opened through the coordinator, the old draft is carried onto a new sheet on 2.0.0 with the unit made explicit, and runs as 30 kPa.
        await coordinator.OpenAsync(draft.Id);
        await WaitFor(() => view.SelectedTemplate?.Reference == $"{Margin.MarginId}@2.0.0", () => view.StatusText);
        unit = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "p unit");
        Assert.Equal("bar", unit.SelectedItem);
        Assert.Equal("bar", view.ReadForm().Find("p")!.Unit);
        var run = CalcRunner.Run(Margin.V200, view.ReadForm());
        Assert.False(run.IsBlocked, string.Join(" ", run.Blocks.Select(b => b.Message)));
        Assert.Equal(30_000, run.OutputSi("p_out"), 6);

        // A new sheet starts in the new unit with the new default.
        view.ShowTemplate(current, null, null);
        unit = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "p unit");
        Assert.Equal("kPa", unit.SelectedItem);
        Assert.Equal(50, view.ReadForm().Find("p")?.Number);
    }

    // ---------------------------------------------------------------

    /// <summary>The stored record is what it was: the same document, byte for byte, as the store writes it.</summary>
    private static void AssertUnchanged(CalcInstance? expected, CalcInstance? actual) =>
        Assert.Equal(CalcInstanceStore.Serialise(expected), CalcInstanceStore.Serialise(actual));

    private static EngineeringDomainContext Domain(Fixture fx) => (EngineeringDomainContext)fx.Host.Services!.GetService(typeof(EngineeringDomainContext));

    private static CalcInstanceService BuildService(WorkspaceHost host, IReadOnlyList<ICalcCalculator> calculators)
    {
        var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        return new CalcInstanceService(
            new CalcCatalogue(calculators),
            new CalcInstanceStore(domain.PersistenceStore),
            host.EvidenceService!,
            domain,
            renderer: null,
            applicationVersion: "TempestOS test",
            events: (IEventBus)host.Services!.GetService(typeof(IEventBus)),
            principals: (IPrincipalDirectory)host.Services!.GetService(typeof(IPrincipalDirectory)));
    }

    private static async Task<Fixture> StartAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        host.SwitchPrincipal(new SessionPrincipal("S-1-5-21-1111111111-2222222222-3333333333-1001", "Alex Author", SessionRole.Engineer));
        var project = await host.ProjectDirectory!.CreateAsync(code, code + " project");
        await host.ProjectContext!.OpenAsync(project.Id);
        return new Fixture(host, project.Id);
    }

    private static async Task<CalcInstance> IssuedOn100Async(Fixture fx, string name)
    {
        var service = BuildService(fx.Host, [VersionedStress.V100]);
        var created = await service.CreateAsync(Id, fx.ProjectId, name, InputsOn100());
        Assert.True((await service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "Checked.", CheckOutcome.Accepted)).Succeeded);
        var issued = await service.IssueAsync(created.Id, "ISS-1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        return (await service.Store.FindInstanceAsync(created.Id))!;
    }

    /// <summary>A worksheet form over a later build of the Library: the same database, another catalogue.</summary>
    private static (CalcSheetView View, CalcSheetCoordinator Coordinator) Open(Fixture fx, IReadOnlyList<ICalcCalculator> calculators)
    {
        var service = BuildService(fx.Host, calculators);
        var view = new CalcSheetView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var coordinator = new CalcSheetCoordinator(service, view, fx.Host.ProjectContext!, () => fx.Host.SessionPrincipal?.IdentityId, (IEventBus)fx.Host.Services!.GetService(typeof(IEventBus)));
        view.ShowCatalogue(service.Catalogue.Current());
        return (view, coordinator);
    }

    private static Button ButtonNamed(CalcSheetView view, string name) =>
        view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    private static bool Available(CalcSheetView view, string name)
    {
        var button = ButtonNamed(view, name);
        return button.IsVisible && button.IsEnabled;
    }

    private static void Click(CalcSheetView view, string name)
    {
        var button = ButtonNamed(view, name);
        Assert.True(button.IsEnabled && button.IsVisible, $"'{name}' is not available.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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

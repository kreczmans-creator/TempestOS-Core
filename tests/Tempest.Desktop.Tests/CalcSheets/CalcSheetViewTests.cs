using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>§3.8a: the worksheet form, headless. Live recalculation, unit conversion, validation, governing bar, show working, read-only issued.</summary>
public sealed class CalcSheetViewTests
{
    private static (CalcSheetView View, CalcCatalogue Catalogue) NewView()
    {
        var catalogue = new CalcCatalogue([new TestWorksheetCalculator()]);
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(catalogue.Get("test.desktop-stress").Calculator, inputs));
        view.ShowCatalogue(catalogue.Current());
        return (view, catalogue);
    }

    [AvaloniaFact]
    public void Catalogue_ListsTemplates_ByDiscipline_WithVersionAndHash()
    {
        var (view, catalogue) = NewView();
        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        var discipline = Assert.Single(tree.Items.OfType<TreeViewItem>());
        Assert.Equal("Structural", AutomationProperties.GetName(discipline));
        var leaf = Assert.Single(discipline.Items.OfType<TreeViewItem>());
        Assert.Contains("v1.0.0", leaf.Header?.ToString(), StringComparison.Ordinal);
        Assert.Contains(catalogue.Current()[0].ShortHash[..8], leaf.Header?.ToString(), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ReferencePicker_IsShownPerLibrary_AndAFillSetsValuesWithThePinAsTheSource()
    {
        // ADR-0167 §3: a template that declares reference-fillable inputs gets one picker per library; a fill writes value, unit and a 'reference' source.
        var catalogue = new CalcCatalogue([new Tempest.Calc.Structural.MemberChecksCalculator()]);
        var view = new CalcSheetView();
        string? picked = null;
        view.ReferencePicked += (library, index) => picked = $"{library}#{index}";
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate("struct.en1993-member");

        Assert.Equal(["Materials"], view.ReferenceLibraries);
        var picker = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == CalcSheetView.ReferencePickerName("Materials"));
        Assert.False(picker.IsEnabled);

        view.SetReferenceOptions("Materials", ["S355 — Structural steel"]);
        Assert.True(picker.IsEnabled);
        picker.SelectedIndex = 0;
        Assert.Equal("Materials#0", picked);

        view.ApplyReferenceFill(
        [
            CalcInputValue.Quantity("f_y", 345, "MPa", CalcProvenance.FromReference("Materials/mat-s355@3")),
            CalcInputValue.Quantity("E", 210, "GPa", CalcProvenance.FromReference("Materials/mat-s355@3")),
        ]);
        var read = view.ReadForm();
        Assert.Equal(345, read.Find("f_y")!.Number);
        Assert.Equal(CalcProvenanceKind.Reference, read.Find("f_y")!.Provenance!.Kind);
        Assert.Equal("Materials/mat-s355@3", read.Find("f_y")!.Provenance!.Reference);
        Assert.Equal(210, read.Find("E")!.Number);
    }

    [AvaloniaFact]
    public void ALegacyArchive_IsShownReadOnly_WithItsSheet_AndNoLifecycleActions()
    {
        // ADR-0167: a migrated legacy record opens as its archive sheet; it cannot be saved, checked, issued or revised.
        var (view, _) = NewView();
        var at = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        var archive = new CalcInstance(
            Guid.NewGuid(), CalcInstance.LegacyReferencePrefix + "calc.bolted-joint-preload@1", "legacy", null, "calc.bolted-joint-preload 2026-03-01 09:00", "A",
            [CalcInputValue.Quantity("Preload", 37935, "N", CalcProvenance.Manual)], [new CalcOutputSnapshot("Bolt load", "result", "42935 N", 42935, "N")], [], [], [],
            CalcInstanceStatus.Draft, null, null, "steven", at, at, Notes: ["Migrated from calculation record."]);
        var sheet = LegacyArchiveSheet.Build(archive, "—", "steven", "engine", null, at);

        view.ShowArchive(archive, CalcSheetTextRenderer.Render(sheet));

        Assert.True(view.IsReadOnly);
        Assert.Null(view.SelectedTemplate);
        var text = view.GetLogicalDescendants().OfType<SelectableTextBlock>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.ArchiveSheetName);
        Assert.Contains("Bolt load = 42935 N", text.Text, StringComparison.Ordinal);
        foreach (var caption in new[] { CalcSheetView.SaveCaption, CalcSheetView.CheckCaption, CalcSheetView.IssueCaption, CalcSheetView.ReviseCaption, CalcSheetView.ImportCaption })
        {
            var button = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == caption);
            Assert.False(button.IsVisible && button.IsEnabled, $"{caption} is available on an archive");
        }
    }

    [AvaloniaFact]
    public void ATemplateWithoutReferenceInputs_ShowsNoPicker()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        Assert.Empty(view.ReferenceLibraries);
    }

    [AvaloniaFact]
    public void ImportedInputs_SeedTheForm_AndRoundTripThroughReadForm()
    {
        // §3.8 / Black B-24: a worked example's inputs.json (or the exported CSV) re-seeds the form; the form reads the same set back.
        var (view, catalogue) = NewView();
        view.SelectTemplate("test.desktop-stress");
        var template = catalogue.Get("test.desktop-stress").Template;
        var imported = new CalcInputSet(
        [
            CalcInputValue.Quantity("F", 30, "kN", CalcProvenance.Drawing("DRG-1", "B")),
            CalcInputValue.Quantity("A", 150, "mm^2", CalcProvenance.Assumed("plate")),
        ]);
        var json = CalcInputJson.Write(imported);

        var (parsed, notices) = CalcSheetCoordinator.ParseInputs(template, json);
        Assert.Empty(notices);
        view.ApplyImportedInputs(parsed);

        Assert.Equal("30", ((TextBox)view.FieldControl("F")!).Text);
        var read = view.ReadForm();
        Assert.Equal(30, read.Find("F")!.Number);
        Assert.Equal("kN", read.Find("F")!.Unit);
        Assert.Equal(CalcProvenanceKind.Drawing, read.Find("F")!.Provenance?.Kind);
        Assert.Equal(150, read.Find("A")!.Number);
        Assert.StartsWith("FAIL — Direct stress", view.GoverningText, StringComparison.Ordinal);

        var csv = CalcCsv.ExportInputs(template, read);
        var (fromCsv, csvNotices) = CalcSheetCoordinator.ParseInputs(template, csv);
        Assert.DoesNotContain(csvNotices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Equal(30, fromCsv.Find("F")!.Number);
    }

    [AvaloniaFact]
    public void WithheldLicensedValue_IsRefusedOnImport_AndTheFormIsNotReseededOnDefaults()
    {
        // Purple P-A-01: a licensed value withheld on export must not come back as the template default.
        var (view, catalogue) = NewView();
        view.SelectTemplate("test.desktop-stress");
        var template = catalogue.Get("test.desktop-stress").Template;
        var withheld = new CalcInputSet([new CalcInputValue("F", Provenance: new CalcProvenance(CalcProvenanceKind.Reference, $"record:{Guid.NewGuid():N}@1 \u00b7 {CalcCsv.WithheldMarker}", "2018"))]);

        foreach (var text in new[] { CalcCsv.ExportInputs(template, withheld), CalcInputJson.Write(withheld) })
        {
            var (_, notices) = CalcSheetCoordinator.ParseInputs(template, text);
            Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("apply the record again", StringComparison.Ordinal));
        }
    }

    [AvaloniaFact]
    public void An_Optional_Input_An_Import_Leaves_Out_Stays_Blank_So_The_Inputs_Remain_Sourced()
    {
        // Found by the screenshot journey: the beam's optional w came back at its default with no source, so Check refused.
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate(Tempest.Calc.Structural.BeamFormulasCalculator.Id);
        Assert.NotEqual(string.Empty, ((TextBox)view.FieldControl("w")!).Text);

        view.ApplyImportedInputs(new CalcInputSet(
        [
            CalcInputValue.Choice("case", Tempest.Calc.Structural.BeamFormulasCalculator.SsPoint, CalcProvenance.Drawing("D1")),
            CalcInputValue.Quantity("L", 6, "m", CalcProvenance.Drawing("D1")),
            CalcInputValue.Quantity("P", 50, "kN", CalcProvenance.Specification("S1")),
        ]));

        Assert.Equal(string.Empty, ((TextBox)view.FieldControl("w")!).Text);
        var read = view.ReadForm();
        Assert.Null(read.Find("w"));
        Assert.Equal(CalcProvenanceKind.Specification, read.Find("P")!.Provenance?.Kind);
    }

    [AvaloniaFact]
    public void Released_Record_Options_Survive_An_Import_And_Are_Dropped_For_Another_Template()
    {
        // Found by the screenshot journey: an import rebuilt the form and left the pickers on "Loading released records…".
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate("struct.en1993-member");
        view.SetReferenceOptions("Materials", ["S275 — Structural steel", "S355 — Structural steel"]);

        view.ApplyImportedInputs(view.ReadForm());

        var picker = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == CalcSheetView.ReferencePickerName("Materials"));
        Assert.True(picker.IsEnabled);
        Assert.Equal(2, picker.ItemCount);
        Assert.Equal("Pick a released record (2)", picker.PlaceholderText);

        view.SelectTemplate(Tempest.Calc.Structural.BeamFormulasCalculator.Id);
        picker = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == CalcSheetView.ReferencePickerName("Materials"));
        Assert.Equal(0, picker.ItemCount);
        Assert.False(picker.IsEnabled);
    }

    [AvaloniaFact]
    public void Results_Fill_The_Column_And_A_Check_Reads_Across_It()
    {
        // Found by the screenshot journey: Results shrank to its values and each check wrapped in a 220 px column.
        var (view, _) = NewView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();

        try
        {
            view.SelectTemplate("test.desktop-stress");
            window.UpdateLayout();
            var sections = view.GetLogicalDescendants().OfType<Expander>().ToDictionary(e => (string)e.Header!);
            Assert.Equal(sections["Inputs"].Bounds.Width, sections["Results"].Bounds.Width, 1);

            var check = view.GetLogicalDescendants().OfType<TextBlock>().First(t => AutomationProperties.GetName(t) == "Result PASS");
            Assert.True(check.Bounds.Width > 600, $"the check text is {check.Bounds.Width:0} px wide");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MalformedImport_IsReported_AndTheFormIsUnchanged()
    {
        var (view, catalogue) = NewView();
        view.SelectTemplate("test.desktop-stress");
        var template = catalogue.Get("test.desktop-stress").Template;
        var (_, notices) = CalcSheetCoordinator.ParseInputs(template, "{");
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("JSON list of inputs", StringComparison.Ordinal));
        Assert.Equal("12", ((TextBox)view.FieldControl("F")!).Text);
    }

    [AvaloniaFact]
    public void SelectingATemplate_BuildsTheForm_AndRecalculatesAtOnce()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        Assert.NotNull(view.SelectedTemplate);
        Assert.NotNull(view.FieldControl("F"));
        Assert.NotNull(view.UnitPicker("F"));
        Assert.NotNull(view.LastRun);
        Assert.False(view.IsStale);
        Assert.StartsWith("PASS — Direct stress: utilisation 0.375", view.GoverningText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ChangingAnInput_RecalculatesLive_AndTheGoverningBarFollows()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        view.SetField("F", text: "40");
        Assert.StartsWith("FAIL — Direct stress: utilisation 1.25", view.GoverningText, StringComparison.Ordinal);
        Assert.Equal(1.25, view.LastRun!.OutputSi("u"), 9);
    }

    [AvaloniaFact]
    public void ChangingTheUnit_ConvertsTheValue_SoTheQuantityIsUnchanged()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        var before = view.LastRun!.OutputSi("sigma");
        view.SetField("F", unit: "N");
        Assert.Equal("12000", ((TextBox)view.FieldControl("F")!).Text);
        Assert.Equal(before, view.LastRun!.OutputSi("sigma"), 6);
    }

    [AvaloniaFact]
    public void OutOfRange_ShowsASoftWarning_AndHardLimit_Blocks_WithTheSymbolNamed()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        view.SetField("F", text: "20000");
        Assert.Contains("F (Axial load) is outside its usual range", view.FieldProblem("F"), StringComparison.Ordinal);
        Assert.NotNull(view.LastRun);
        Assert.False(view.LastRun!.IsBlocked);

        view.SetField("A", text: "0");
        Assert.Contains("A (Cross-section area) must be at least", view.FieldProblem("A"), StringComparison.Ordinal);
        Assert.True(view.LastRun!.IsBlocked);
        Assert.StartsWith("Blocked —", view.GoverningText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ANonNumber_IsReportedPlainly_NotThrown()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        view.SetField("F", text: "twelve");
        Assert.Contains("F must be a finite number", view.FieldProblem("F"), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ShowWorking_RevealsFormulaSubstitutionResult_MatchingTheSheet()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        Assert.False(view.IsWorkingShown);
        view.ToggleWorking(true);
        Assert.True(view.IsWorkingShown);
        var lines = view.WorkingLines;
        Assert.Contains("[5] sigma = F / A    « EN 1993-1-1 §6.2.3", lines);
        Assert.Contains("      = 12 kN / 200 mm²", lines);
        Assert.Contains("      = 60 MPa", lines);
    }

    private static CalcInstance Archive(string name, DateTimeOffset at) => new(
        Guid.NewGuid(), CalcInstance.LegacyReferencePrefix + "calc.bolted-joint-preload@1", "legacy", null, name, "A",
        [CalcInputValue.Quantity("Preload", 37935, "N", CalcProvenance.Manual)], [], [], [], [], CalcInstanceStatus.Draft, null, null, "steven", at, at);

    private static CalcInstance Revision(CalcCatalogueEntry entry, string revision, double force, string sigma, CalcInstanceStatus status, Guid? predecessor) => new(
        Guid.NewGuid(), entry.Reference, entry.ContentHash, null, "CALC-12", revision,
        [CalcInputValue.Quantity("F", force, "kN", CalcProvenance.Drawing("D1")), CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("D1")), CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("S1")), CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed())],
        [new CalcOutputSnapshot("sigma", "Direct stress", sigma, null, "MPa")], [], [], [], status, Guid.NewGuid(), null, "s.k", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, PredecessorId: predecessor);

    [AvaloniaFact]
    public void Compare_IsOfferedOnlyOnARevision_AndShowsWhatDiffers_UntilAnotherCalculationOpens()
    {
        // ADR-0167 §4: Compare is reimplemented over calc instances before the legacy surface goes.
        var (view, catalogue) = NewView();
        var entry = catalogue.Get("test.desktop-stress");
        var a = Revision(entry, "A", 12, "60 MPa", CalcInstanceStatus.Superseded, null);
        var b = Revision(entry, "B", 16, "80 MPa", CalcInstanceStatus.Draft, a.Id);
        Button Compare() => view.GetLogicalDescendants().OfType<Button>().Single(x => AutomationProperties.GetName(x) == CalcSheetView.CompareCaption);

        view.ShowTemplate(entry, a);
        Assert.False(Compare().IsVisible);

        view.ShowTemplate(entry, b);
        Assert.True(Compare().IsVisible);
        var raised = 0;
        view.CompareRequested += () => raised++;
        Compare().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, raised);

        view.ShowComparison(CalcInstanceComparer.Compare(a, b));
        Assert.Contains("F: 12 kN → 16 kN  (+33.3 %)", view.ComparisonText, StringComparison.Ordinal);
        Assert.Contains("sigma: 60 MPa → 80 MPa", view.ComparisonText, StringComparison.Ordinal);
        Assert.NotNull(view.LastComparison);

        view.ShowTemplate(entry, a);
        Assert.Equal(string.Empty, view.ComparisonText);
        Assert.Null(view.LastComparison);
    }

    [AvaloniaFact]
    public void Archives_AreListedUnderTheirOwnNode_NewestFirst_AndPickingOneAsksToOpenIt()
    {
        // Y8-06: a migrated record has no project or register entry, so the catalogue lists it.
        var (view, catalogue) = NewView();
        var older = Archive("legacy run 1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = Archive("legacy run 2", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var live = Revision(catalogue.Get("test.desktop-stress"), "A", 12, "60 MPa", CalcInstanceStatus.Draft, null);
        view.ShowArchives([older, live, newer]);

        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        var node = tree.Items.OfType<TreeViewItem>().Single(n => AutomationProperties.GetName(n) == CalcSheetView.ArchivesNodeName);
        Assert.Equal(["legacy run 2", "legacy run 1"], node.Items.OfType<TreeViewItem>().Select(l => AutomationProperties.GetName(l)));
        Assert.Contains(tree.Items.OfType<TreeViewItem>(), n => AutomationProperties.GetName(n) == "Structural");

        Guid? opened = null;
        view.ArchiveSelected += id => opened = id;
        tree.SelectedItem = node.Items.OfType<TreeViewItem>().Last();
        Assert.Equal(older.Id, opened);
    }

    [AvaloniaFact]
    public void Archives_BeyondTheCap_AreCounted_NotListed()
    {
        var (view, _) = NewView();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        view.ShowArchives([.. Enumerable.Range(0, CalcSheetView.MaxArchivesListed + 3).Select(i => Archive($"run {i}", start.AddMinutes(i)))]);

        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        var node = tree.Items.OfType<TreeViewItem>().Single(n => AutomationProperties.GetName(n) == CalcSheetView.ArchivesNodeName);
        Assert.Equal(CalcSheetView.MaxArchivesListed + 1, node.Items.Count);
        Assert.Equal("… and 3 more", node.Items.OfType<TreeViewItem>().Last().Header);
    }

    [AvaloniaFact]
    public void AnIssuedInstance_IsReadOnly_UnderAnIssuedWatermark()
    {
        var (view, catalogue) = NewView();
        var entry = catalogue.Get("test.desktop-stress");
        var instance = new CalcInstance(
            Guid.NewGuid(), entry.Reference, entry.ContentHash, null, "CALC-9", "A",
            [CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("D1")), CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("D1")), CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("S1")), CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed())],
            [], [], [], [], CalcInstanceStatus.Issued, Guid.NewGuid(), null, "s.k", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, IssuedAt: DateTimeOffset.UnixEpoch);

        view.ShowTemplate(entry, instance);
        Assert.True(view.IsReadOnly);
        Assert.True(((TextBox)view.FieldControl("F")!).IsReadOnly);
        var watermark = view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Status watermark");
        Assert.True(watermark.IsVisible);
        Assert.Equal("ISSUED", watermark.Text);
        Assert.Contains(view.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == CalcSheetView.ReviseCaption && b.IsVisible);
        Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == CalcSheetView.IssueCaption && b.IsVisible);
    }

    [AvaloniaFact]
    public void ReadForm_CarriesUnitsAndProvenance()
    {
        var (view, _) = NewView();
        view.SelectTemplate("test.desktop-stress");
        view.SetField("F", provenance: "drawing", reference: "DRG-7 rev C");
        var inputs = view.ReadForm();
        var f = inputs.Find("F")!;
        Assert.Equal(12, f.Number);
        Assert.Equal("kN", f.Unit);
        Assert.Equal(CalcProvenanceKind.Drawing, f.Provenance!.Kind);
        Assert.Equal("DRG-7 rev C", f.Provenance.Reference);
        Assert.Equal("S275", inputs.Find("grade")!.Text);
    }
}

public sealed class CalcSheetDisciplineFilterTests
{
    [AvaloniaFact]
    public void Filter_Shows_Only_One_Discipline_And_Clears()
    {
        var catalogue = new CalcCatalogue([new TestWorksheetCalculator(), .. Tempest.Calc.Mechanical.MechanicalCalculators.All]);
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        Assert.Equal(2, tree.Items.Count);

        view.FilterDiscipline(CalcDiscipline.Mechanical);
        var only = Assert.Single(tree.Items.OfType<TreeViewItem>());
        Assert.Equal("Mechanical", AutomationProperties.GetName(only));
        // The gated lifting-lug worksheet (ADR-0173) is withheld from the catalogue with the feature off.
        Assert.Equal(Tempest.Calc.Mechanical.MechanicalCalculators.All.Count(c => c.Template.FeatureFlag is not { Length: > 0 }), only.Items.Count);

        view.FilterDiscipline(null);
        Assert.Equal(2, tree.Items.Count);
    }

    [AvaloniaFact]
    public void Discipline_Mapping_Covers_The_Five_Disciplines()
    {
        Assert.Equal(CalcDiscipline.Mechanical, CalcSheetView.DisciplineFor("Mechanical"));
        Assert.Equal(CalcDiscipline.BuildingServices, CalcSheetView.DisciplineFor("Building Services"));
        Assert.Equal(CalcDiscipline.FluidPower, CalcSheetView.DisciplineFor("Fluid Power"));
        Assert.Null(CalcSheetView.DisciplineFor("Manufacturing"));
    }

    [AvaloniaFact]
    public void Every_Discipline_Files_Under_A_Category_That_Maps_Back_To_It()
    {
        // The Ctrl+K "Open worksheet" entries file under CategoryFor; the Disciplines tree resolves the same word with DisciplineFor.
        foreach (var discipline in Enum.GetValues<CalcDiscipline>())
            Assert.Equal(discipline, CalcSheetView.DisciplineFor(CalcSheetView.CategoryFor(discipline)));

        Assert.Equal("Fluid Power", CalcSheetView.CategoryFor(CalcDiscipline.FluidPower));
        Assert.Equal("Building Services", CalcSheetView.CategoryFor(CalcDiscipline.BuildingServices));
        Assert.Contains("Fluid Power", Tempest.Desktop.Views.EngineeringDisciplines.Disciplines);
    }

    [AvaloniaFact]
    public void A_Fluid_Power_Worksheet_Lists_Under_Its_Own_Labelled_Discipline_Node()
    {
        var catalogue = new CalcCatalogue([new TestWorksheetCalculator(), new FluidPowerStub()]);
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);

        Assert.Equal(["Structural", "Fluid Power"], tree.Items.OfType<TreeViewItem>().Select(AutomationProperties.GetName));

        view.FilterDiscipline(CalcDiscipline.FluidPower);
        var only = Assert.Single(tree.Items.OfType<TreeViewItem>());
        Assert.Equal("Fluid Power", AutomationProperties.GetName(only));
        Assert.Single(only.Items);
    }

    /// <summary>The desktop test worksheet filed under Fluid Power: no shipped Fluid Power calculator exists until B-2.</summary>
    private sealed class FluidPowerStub : ICalcCalculator
    {
        private readonly TestWorksheetCalculator _inner = new();

        public CalcTemplate Template => _inner.Template with { Id = "test.desktop-fluid-power", Discipline = CalcDiscipline.FluidPower, Title = "Desktop fluid power check" };

        public void Run(CalcRunContext context) => _inner.Run(context);
    }
}

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
        Assert.Equal(Tempest.Calc.Mechanical.MechanicalCalculators.All.Count, only.Items.Count);

        view.FilterDiscipline(null);
        Assert.Equal(2, tree.Items.Count);
    }

    [AvaloniaFact]
    public void Discipline_Mapping_Covers_The_Four_Disciplines()
    {
        Assert.Equal(CalcDiscipline.Mechanical, CalcSheetView.DisciplineFor("Mechanical"));
        Assert.Equal(CalcDiscipline.BuildingServices, CalcSheetView.DisciplineFor("Building Services"));
        Assert.Null(CalcSheetView.DisciplineFor("Manufacturing"));
    }
}

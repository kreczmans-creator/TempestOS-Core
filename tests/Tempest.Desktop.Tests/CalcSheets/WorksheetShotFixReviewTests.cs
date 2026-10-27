using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Shot-fix A, the independent review: each problem it found in the worksheet screens, fixed and held here.</summary>
public sealed class WorksheetShotFixReviewTests
{
    private static CalcSheetView Live(string templateId, out CalcCatalogueEntry entry)
    {
        var all = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(CalcFeatures.All));
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(all.Get(templateId).Calculator, inputs));
        view.ShowCatalogue(all.Current());
        view.SelectTemplate(templateId);
        entry = all.Get(templateId);
        return view;
    }

    private static CalcInstance Draft(CalcCatalogueEntry entry, IReadOnlyList<CalcInputValue> inputs) => new(
        Guid.NewGuid(), entry.Reference, entry.ContentHash, null, "CALC-R1", "A", inputs, [], [], [], [], CalcInstanceStatus.Draft, null, null, "s.k",
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static CalcInputValue Sections(params CalcRow[] rows) => CalcInputValue.Table("sections", rows, CalcProvenance.Drawing("DWG-7", "C"));

    private static CalcRow Section(double q, string qUnit, double l, CalcProvenance? qSource = null) =>
        CalcRow.Of(("Q", new CalcCell(q, qUnit, Provenance: qSource)), ("L", new CalcCell(l, "m")));

    [AvaloniaFact]
    public void Item1_An_Edited_Table_Is_No_Longer_Unsaved_Once_Saved()
    {
        var view = Live("bs.duct-sizing", out var entry);
        var draft = Draft(entry, [.. CalcTemplateHasher.NewSheetInputs(entry.Template).Values, Sections(Section(3600, "m^3/h", 10))]);
        view.ShowTemplate(entry, draft);
        Assert.False(view.HasUnsavedEdits());

        view.AddTableRow("sections");
        view.SetTableCell("sections", 1, "Q", text: "1800");
        view.SetTableCell("sections", 1, "L", text: "4");
        Assert.True(view.HasUnsavedEdits());

        // Save: the coordinator stores the form and shows the saved calculation; the form is not rebuilt.
        view.ShowInstance(draft with { Inputs = [.. view.ReadForm().Values] });
        Assert.False(view.HasUnsavedEdits());

        // A later edit is an edit again.
        view.SetTableCell("sections", 1, "L", text: "5");
        Assert.True(view.HasUnsavedEdits());
    }

    [AvaloniaFact]
    public void Item2_A_New_Sheet_Lists_Its_Missing_Records_Although_Its_Defaults_Fill_The_Form()
    {
        var view = Live("bs.insulation", out var entry);
        var kinds = entry.Template.RecordKinds!;
        view.SetRecordRequirements(entry.Reference, [.. kinds.Select(k =>
        {
            var (need, reason) = RecordRequirements.For(entry.Template.Id, k.Kind);
            return new RecordRequirement(k.Kind, k.Title, k.Symbols, need, reason, Present: false, Count: 0, []);
        })]);

        Assert.Equal("No record is required; the defaults apply until you enter the ones below.", view.RequirementsSummary);
        var fills = view.GetLogicalDescendants().OfType<Button>().Count(b => AutomationProperties.GetName(b)?.StartsWith("Fill record: ", StringComparison.Ordinal) == true);
        Assert.Equal(kinds.Count, fills);

        // A value given with a source takes its kind off the list.
        view.SetField("eps", text: "0.3", provenance: "spec", reference: "Jacket maker's data sheet");
        var after = view.GetLogicalDescendants().OfType<Button>().Count(b => AutomationProperties.GetName(b)?.StartsWith("Fill record: ", StringComparison.Ordinal) == true);
        Assert.Equal(kinds.Count - 1, after);
    }

    [AvaloniaFact]
    public void Item3_Changing_A_Columns_Unit_Keeps_Each_Cells_Link_And_Value()
    {
        var view = Live("bs.duct-sizing", out var entry);
        var link = CalcProvenance.Link(Guid.NewGuid(), "Q_out", "B");
        var linked = Section(1, "m^3/s", 10, link);
        view.ShowTemplate(entry, null, new CalcInputSet([Sections(linked, Section(1800, "m^3/h", 4))]));

        view.SetTableColumnUnit("sections", "Q", "L/s");
        Assert.Equal("1000", view.TableCellText("sections", 0, "Q"));
        var cell = view.ReadForm().Find("sections")!.Rows![0].Get("Q")!;
        Assert.Equal(link, cell.Provenance);
        Assert.Equal(1, cell.Number);
        Assert.Equal("m^3/s", cell.Unit);

        // Edited after the change, a cell takes the column's unit and is the user's own value.
        view.SetTableCell("sections", 0, "Q", text: "500");
        var edited = view.ReadForm().Find("sections")!.Rows![0].Get("Q")!;
        Assert.Equal(500, edited.Number);
        Assert.Equal("L/s", edited.Unit);
        Assert.Null(edited.Provenance);
    }

    [AvaloniaFact]
    public void Item3_A_Column_Unit_Change_Is_Refused_While_A_Cell_Is_Not_A_Number()
    {
        var view = Live("bs.duct-sizing", out var entry);
        view.ShowTemplate(entry, null, new CalcInputSet([Sections(Section(1000, "m^3/h", 10), Section(2000, "m^3/h", 4))]));
        view.SetTableCell("sections", 1, "Q", text: "2OOO");

        view.SetTableColumnUnit("sections", "Q", "m^3/s");
        Assert.Contains("not a number", view.TableProblem("sections") ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("1000", view.TableCellText("sections", 0, "Q"));
        view.SetTableCell("sections", 1, "Q", text: "2000");
        Assert.Equal("m^3/h", view.ReadForm().Find("sections")!.Rows![1].Get("Q")!.Unit);
    }

    [AvaloniaFact]
    public void Item4_A_Vector_Unit_Change_Is_Refused_While_An_Entry_Is_Not_A_Number()
    {
        var view = Live("bs.pipe-pump", out _);
        view.SetVector("Q_pump", "1000 2000 3OOO", unit: "L/s");

        var picker = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Q_pump unit");
        picker.SelectedItem = "m^3/h";
        Assert.Equal("L/s", picker.SelectedItem);
        Assert.Contains("not a number", view.TableProblem("Q_pump") ?? string.Empty, StringComparison.Ordinal);

        // Corrected, the numbers are still in L/s, never silently read in another unit.
        view.SetVector("Q_pump", "1000 2000 3000");
        var value = view.ReadForm().Find("Q_pump")!;
        Assert.Equal("L/s", value.Unit);
        Assert.Equal([1000d, 2000d, 3000d], value.Vector!);

        // With every entry a number, the change converts them.
        picker.SelectedItem = "m^3/h";
        var converted = view.ReadForm().Find("Q_pump")!;
        Assert.Equal("m^3/h", converted.Unit);
        Assert.Equal([3600d, 7200d, 10800d], converted.Vector!);
    }

    [Theory]
    [InlineData("1, 2, 3", new[] { 1d, 2d, 3d })]
    [InlineData("1 2;3", new[] { 1d, 2d, 3d })]
    [InlineData("1.5, 2.5", new[] { 1.5, 2.5 })]
    public void Item5_A_Vector_Reads_Its_Numbers(string text, double[] expected) => Assert.Equal(expected, CalcSheetView.ParseVector(text)!);

    [Theory]
    [InlineData("1,5 2,5")]
    [InlineData("1,2,3")]
    public void Item5_A_Decimal_Comma_Is_Refused_Not_Read_As_Two_Values(string text)
    {
        var values = CalcSheetView.ParseVector(text)!;
        Assert.Contains(values, double.IsNaN);
        Assert.True(values.Count < 4);
    }

    [AvaloniaFact]
    public void Item6_Rows_Beyond_The_Cap_Are_Kept_After_An_Edit_Until_Removed()
    {
        var view = Live("bs.duct-sizing", out var entry);
        var max = entry.Template.FindInput("sections")!.MaxRows;
        var rows = Enumerable.Range(0, max + 3).Select(i => Section(100 + i, "m^3/h", 1)).ToArray();
        view.ShowTemplate(entry, null, new CalcInputSet([Sections(rows)]));

        Assert.Equal(max, view.TableRowCount("sections"));
        Assert.Equal(3, view.TableOverflowCount("sections"));
        view.SetTableCell("sections", 0, "L", text: "2");
        Assert.Equal(max + 3, view.ReadForm().Find("sections")!.Rows!.Count);
        Assert.Contains(view.LastRun!.Blocks, b => b.Message.Contains($"at most {max} rows", StringComparison.Ordinal));

        view.RemoveTableOverflow("sections");
        Assert.Equal(max, view.ReadForm().Find("sections")!.Rows!.Count);
        Assert.Equal(0, view.TableOverflowCount("sections"));
    }

    [AvaloniaFact]
    public void Item9_A_Drawing_Sources_Revision_Is_Kept_And_A_Change_To_It_Is_An_Edit()
    {
        var catalogue = new CalcCatalogue([new TestWorksheetCalculator()]);
        var entry2 = catalogue.Get("test.desktop-stress");
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(entry2.Calculator, inputs));
        view.ShowCatalogue(catalogue.Current());
        var draft = Draft(entry2,
        [
            CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("D-1", "C")), CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Specification("S-1", "2")),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa"), CalcInputValue.Choice("grade", "S275"),
        ]);
        view.ShowTemplate(entry2, draft);

        Assert.Equal("C", view.ReadForm().Find("F")!.Provenance!.Revision);
        Assert.Equal("2", view.ReadForm().Find("A")!.Provenance!.Revision);
        Assert.False(view.HasUnsavedEdits());

        // A stored revision that differs from the one shown is an edit.
        view.ShowInstance(draft with { Inputs = [CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("D-1", "D")), .. draft.Inputs.Skip(1)] });
        Assert.True(view.HasUnsavedEdits());

        // Changing the reference makes a new source, without the old revision.
        view.SetField("F", reference: "D-2");
        Assert.Null(view.ReadForm().Find("F")!.Provenance!.Revision);
    }

    [Fact]
    public void Item8_A_Bolt_Group_At_The_Edge_Of_The_Number_Range_Is_Drawn_With_Finite_Points_Or_Not_Redrawn()
    {
        var template = CalcCalculatorComposition.All().Single(c => c.Template.Id == Tempest.Calc.Mechanical.BoltGroupCalculator.Id).Template;

        foreach (var (x1, x2, y2) in new[] { (1.7e308, 1.7e308, 100.0), (-1.7e308, 1.7e308, 0.0), (0.0, 1e-310, 0.0), (0.0, 1e-310, 1e-310) })
        {
            var bolts = CalcInputValue.Table("bolts", [CalcRow.Of(("x", new CalcCell(x1, "mm")), ("y", new CalcCell(0, "mm"))), CalcRow.Of(("x", new CalcCell(x2, "mm")), ("y", new CalcCell(y2, "mm")))]);
            var reading = CalcDiagramReader.Read(template, new CalcInputSet([bolts]))!;
            var points = reading.Shapes.Select(s => s.Element).SelectMany(Points).ToList();
            Assert.NotEmpty(points);
            Assert.All(points, p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y), $"({p.X}, {p.Y}) for bolts at x = {x1}, {x2}"));
        }
    }

    private static IEnumerable<DiagramPoint> Points(DiagramElement element) => element switch
    {
        DiagramCircle c => [c.Centre, new DiagramPoint(c.Radius, 0)],
        DiagramPlate p => [new DiagramPoint(p.X, p.Y), new DiagramPoint(p.Width, p.Height)],
        DiagramMember m => [m.From, m.To],
        DiagramPolyline l => l.Points,
        _ => [],
    };
}

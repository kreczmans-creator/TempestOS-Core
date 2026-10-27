using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Shot-fix A, item 12: table and vector inputs have an editor on the worksheet form. A new sheet of a template with a table no longer opens blocked
/// with nowhere to enter a row; rows are added, edited and deleted up to the cap, with units, choices and validation, and feed the run.
/// </summary>
public sealed class WorksheetTableEditorTests
{
    private static CalcSheetView Live(string templateId, out CalcCatalogue catalogue)
    {
        var all = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(CalcFeatures.All));
        catalogue = all;
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(all.Get(templateId).Calculator, inputs));
        view.ShowCatalogue(all.Current());
        view.SelectTemplate(templateId);
        return view;
    }

    [AvaloniaFact]
    public void A_New_Duct_Sizing_Sheet_Takes_Its_Sections_On_The_Form_And_Runs()
    {
        var view = Live("bs.duct-sizing", out _);
        Assert.Equal(0, view.TableRowCount("sections"));
        Assert.Contains(view.LastRun!.Blocks, b => b.RuleId == "input.sections");
        Assert.Contains("enter at least one row", view.TableProblem("sections") ?? string.Empty, StringComparison.Ordinal);

        view.AddTableRow("sections");
        Assert.Equal(1, view.TableRowCount("sections"));
        view.SetTableCell("sections", 0, "name", text: "Main");
        view.SetTableCell("sections", 0, "Q", text: "3600");
        view.SetTableCell("sections", 0, "L", text: "10");
        view.SetTableCell("sections", 0, "index", flag: true);

        var rows = view.ReadForm().Find("sections")!.Rows!;
        var row = Assert.Single(rows);
        Assert.Equal(3600, row.Get("Q")!.Number);
        Assert.Equal("m^3/h", row.Get("Q")!.Unit);
        Assert.Equal(10, row.Get("L")!.Number);
        Assert.Equal("Main", row.Get("name")!.Text);
        Assert.True(row.Get("index")!.Flag);
        Assert.DoesNotContain(view.LastRun!.Blocks, b => b.RuleId == "input.sections");
        Assert.True(view.HasUnsavedEdits());

        // A cell that is not a number is named by validation, on the table's editor.
        view.SetTableCell("sections", 0, "L", text: "ten");
        Assert.Contains("must be a finite number", view.TableProblem("sections") ?? string.Empty, StringComparison.Ordinal);
        Assert.True(view.LastRun!.IsBlocked);

        // A column's unit converts its numbers, as for a scalar.
        view.SetTableCell("sections", 0, "L", text: "10");
        view.SetTableColumnUnit("sections", "L", "mm");
        Assert.Equal("10000", view.TableCellText("sections", 0, "L"));
        Assert.Equal("mm", view.ReadForm().Find("sections")!.Rows![0].Get("L")!.Unit);

        // Deleted, the table is empty again and the run asks for a row.
        view.DeleteTableRow("sections", 0);
        Assert.Equal(0, view.TableRowCount("sections"));
        Assert.Null(view.ReadForm().Find("sections"));
        Assert.Contains(view.LastRun!.Blocks, b => b.RuleId == "input.sections");
    }

    [AvaloniaTheory]
    [InlineData("bs.duct-sizing")]
    [InlineData("elec.containment")]
    [InlineData("elec.thermal-chain")]
    [InlineData("struct.frame-2d")]
    [InlineData("elec.conductor-sizing")]
    [InlineData("mech.fatigue-miner")]
    [InlineData("bs.glaser")]
    [InlineData("struct.load-combinations")]
    public void Every_Table_Input_Of_A_Template_Has_An_Editor_That_Adds_Rows(string templateId)
    {
        var view = Live(templateId, out _);
        var tables = view.SelectedTemplate!.Template.Inputs.Where(i => i.Kind == CalcInputKind.Table && i.Columns is { Count: > 0 }).ToList();
        Assert.NotEmpty(tables);

        foreach (var spec in tables)
        {
            Assert.Equal(0, view.TableRowCount(spec.Symbol));
            Assert.True(view.CanAddTableRow(spec.Symbol));
            view.AddTableRow(spec.Symbol);
            Assert.Equal(1, view.TableRowCount(spec.Symbol));
            var row = Assert.Single(view.ReadForm().Find(spec.Symbol)!.Rows!);
            Assert.NotNull(row);
        }
    }

    [AvaloniaFact]
    public void An_Imported_Table_Reads_Back_Exactly_Until_Changed_And_An_Untouched_Row_Keeps_Its_Cells()
    {
        var view = Live("bs.duct-sizing", out var catalogue);
        var entry = catalogue.Get("bs.duct-sizing");
        var link = new CalcProvenance(CalcProvenanceKind.Specification, "SPEC-1");
        var first = CalcRow.Of(("name", new CalcCell(Text: "A")), ("Q", new CalcCell(1, "m^3/s", Provenance: link)), ("L", new CalcCell(12, "m")));
        var second = CalcRow.Of(("name", new CalcCell(Text: "B")), ("Q", new CalcCell(1800, "m^3/h")), ("L", new CalcCell(4, "m")));
        var table = CalcInputValue.Table("sections", [first, second], CalcProvenance.Drawing("DWG-7", "C"));
        var seed = new CalcInputSet(CalcTemplateHasher.NewSheetInputs(entry.Template).Values.Append(table));

        view.ShowTemplate(entry, null, seed);
        Assert.Equal(2, view.TableRowCount("sections"));
        Assert.Same(table, view.ReadForm().Find("sections"));
        Assert.False(view.HasUnsavedEdits());

        // The column shows the first row's unit; the second row's 1800 m³/h is shown in it.
        Assert.Equal("0.5", view.TableCellText("sections", 1, "Q"));

        view.SetTableCell("sections", 1, "L", text: "5");
        var read = view.ReadForm().Find("sections")!;
        Assert.Same(first, read.Rows![0]);
        Assert.Equal(5, read.Rows[1].Get("L")!.Number);
        Assert.Equal(1800, read.Rows[1].Get("Q")!.Number); // an unchanged cell keeps its own unit and number
        Assert.Equal("m^3/h", read.Rows[1].Get("Q")!.Unit);
        Assert.Equal(table.Provenance, read.Provenance);
    }

    [AvaloniaFact]
    public void An_Issued_Table_Is_Read_Only()
    {
        var view = Live("bs.duct-sizing", out var catalogue);
        var entry = catalogue.Get("bs.duct-sizing");
        var table = CalcInputValue.Table("sections", [CalcRow.Of(("Q", new CalcCell(3600, "m^3/h")), ("L", new CalcCell(10, "m")))], CalcProvenance.Drawing("DWG-7"));
        var issued = new CalcInstance(
            Guid.NewGuid(), entry.Reference, entry.ContentHash, null, "CALC-D1", "A", [table], [], [], [], [], CalcInstanceStatus.Issued, Guid.NewGuid(), null, "s.k",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, IssuedAt: DateTimeOffset.UnixEpoch);

        view.ShowTemplate(entry, issued);
        Assert.Equal(1, view.TableRowCount("sections"));
        Assert.False(view.IsTableEditable("sections"));
        Assert.False(view.CanAddTableRow("sections"));
        view.AddTableRow("sections");
        view.DeleteTableRow("sections", 0);
        view.SetTableCell("sections", 0, "L", text: "99");
        Assert.Equal(1, view.TableRowCount("sections"));
        Assert.Same(table.Rows, view.ReadForm().Find("sections")!.Rows);
    }

    [AvaloniaFact]
    public void Add_Row_Stops_At_The_Tables_Row_Cap()
    {
        var catalogue = new CalcCatalogue([new CappedTable()]);
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate(CappedTable.Id);

        view.AddTableRow("rows");
        view.AddTableRow("rows");
        Assert.False(view.CanAddTableRow("rows"));
        view.AddTableRow("rows");
        Assert.Equal(2, view.TableRowCount("rows"));
        view.SetTableCell("rows", 0, "kind", choice: "b");
        Assert.Equal("b", view.ReadForm().Find("rows")!.Rows![0].Get("kind")!.Text);
    }

    [AvaloniaFact]
    public void A_Vector_Input_Is_A_Line_Of_Numbers_With_Its_Unit()
    {
        var view = Live("bs.pipe-pump", out _);
        Assert.Null(view.ReadForm().Find("Q_pump"));

        view.SetVector("Q_pump", "0, 10, 20");
        var value = view.ReadForm().Find("Q_pump")!;
        Assert.Equal([0d, 10d, 20d], value.Vector!);
        Assert.Equal("m^3/h", value.Unit);

        view.SetVector("Q_pump", "0, x");
        Assert.True(double.IsNaN(view.ReadForm().Find("Q_pump")!.Vector![1]));
        _ = CalcInputJson.Write(view.ReadForm()); // never throws on the NaN

        view.SetVector("Q_pump", string.Empty);
        Assert.Null(view.ReadForm().Find("Q_pump"));
    }

    /// <summary>The desktop test worksheet with a small table: two rows at most.</summary>
    private sealed class CappedTable : ICalcCalculator
    {
        public const string Id = "test.desktop-capped-table";

        private readonly TestWorksheetCalculator _inner = new();

        public CalcTemplate Template => _inner.Template with
        {
            Id = Id,
            Inputs =
            [
                .. _inner.Template.Inputs,
                new CalcInputSpec("rows", "A small table", CalcInputKind.Table, IsOptional: true, MaxRows: 2, Columns:
                [
                    new CalcColumnSpec("l", "Length", CalcInputKind.Quantity, "mm", IsOptional: true),
                    new CalcColumnSpec("kind", "Kind", CalcInputKind.Choice, Choices: ["a", "b"], IsOptional: true),
                ]),
            ],
        };

        public void Run(CalcRunContext context) => _inner.Run(context);
    }
}

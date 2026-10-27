using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// C-2 phase B renamed the composite-parts column <c>t</c> of <c>struct.section-properties</c> to <c>t_w</c>. A table column has the same alias mechanism
/// an input has (<see cref="CalcColumnSpec.Aliases"/>, C-2 integration): a saved table, a CSV or an inputs file whose rows still hold the old key is read as
/// the current column by the validator (so the cell is range-checked), the binder and the sheet (through <see cref="CalcInputAliases"/>), and by the CSV
/// importer, so an export of such a table followed by an import keeps the thickness instead of dropping it as an unknown column.
/// </summary>
public sealed class ColumnAliasTests
{
    private static readonly SectionPropertiesCalculator Section = new();

    private static CalcInputValue Parts(string thicknessKey, double thickness = 10) =>
        CalcInputValue.Table("parts",
        [
            CalcRow.Of(("shape", new CalcCell(Text: "rectangle")), ("b", new CalcCell(100, "mm")), ("h", new CalcCell(200, "mm")), ("x0", new CalcCell(0, "mm")), ("y0", new CalcCell(0, "mm"))),
            CalcRow.Of(("shape", new CalcCell(Text: "tube")), ("h", new CalcCell(80, "mm")), (thicknessKey, new CalcCell(thickness, "mm")), ("x0", new CalcCell(0, "mm")), ("y0", new CalcCell(300, "mm"))),
        ]);

    private static CalcInputSet Composite(string thicknessKey, double thickness = 10) =>
        CalcTemplateHasher.NewSheetInputs(Section.Template).With(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeComposite)).With(Parts(thicknessKey, thickness));

    [Fact]
    public void The_Parts_Column_t_w_Declares_Its_Old_Key_And_Resolves_By_It()
    {
        var parts = Section.Template.FindInput("parts")!;
        var column = parts.ResolveColumn("t")!;

        Assert.Equal("t_w", column.Key);
        Assert.Same(column, parts.ResolveColumn("t_w"));
        Assert.True(column.Matches("t"));
        Assert.Null(parts.ResolveColumn("thickness"));
        Assert.True(parts.HasColumnAliases);
        Assert.True(CalcInputAliases.Any(Section.Template));
    }

    [Fact]
    public void A_Table_Saved_With_The_Old_Column_Key_Is_Canonicalised_Run_And_Shown_Under_The_New_Key()
    {
        var old = Composite("t");
        var current = Composite("t_w");

        var canonical = CalcInputAliases.Canonicalise(Section.Template, old);
        var tube = canonical.Find("parts")!.Rows![1];
        Assert.Equal(10, tube.Get("t_w")!.Number);
        Assert.Null(tube.Get("t"));

        // The same section either way, and the sheet prints the thickness under the current key.
        var before = CalcRunner.Run(Section, old);
        var after = CalcRunner.Run(Section, current);
        Assert.False(before.IsBlocked, string.Join("; ", before.Blocks.Select(b => b.Message)));
        Assert.Equal(after.OutputSi("A"), before.OutputSi("A"), 12);
        Assert.Equal(after.OutputSi("I_y"), before.OutputSi("I_y"), 12);
        var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(Section.Template, old, before, GoldenFiles.FixedHeader("x", "x"), GoldenFiles.FixedFooter(Section.Template, 1, 1)));
        Assert.Contains("t_w = 10 mm", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("; t = ", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Cell_Under_The_Old_Key_Is_Range_Checked_By_The_Validator()
    {
        // A negative wall under the old key: before the alias, the validator saw no column and let it through to the calculator.
        var notices = CalcInputValidator.Validate(Section.Template, Composite("t", -5));
        var block = Assert.Single(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("parts[2]", StringComparison.Ordinal));
        Assert.Contains("t_w", block.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(CalcInputValidator.Validate(Section.Template, Composite("t", 10)), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void The_Same_Cell_Under_Both_Keys_Is_Kept_Once_And_Different_Values_Are_Refused_Naming_Both()
    {
        var parts = Section.Template.FindInput("parts")!;
        var rows = Composite("t").Find("parts")!.Rows!;
        var twice = CalcInputValue.Table("parts", [rows[0], new CalcRow(new Dictionary<string, CalcCell>(rows[1].Cells, StringComparer.Ordinal) { ["t_w"] = new CalcCell(1, "cm") })]);
        var agreed = CalcInputAliases.Canonicalise(Section.Template, [twice]);
        Assert.Equal((1.0, "cm"), (agreed[0].Rows![1].Get("t_w")!.Number, agreed[0].Rows![1].Get("t_w")!.Unit));
        Assert.Null(agreed[0].Rows![1].Get("t"));

        var conflicting = CalcInputValue.Table("parts", [rows[0], new CalcRow(new Dictionary<string, CalcCell>(rows[1].Cells, StringComparer.Ordinal) { ["t_w"] = new CalcCell(12, "mm") })]);
        var refused = Assert.Throws<CalcInputAliasConflictException>(() => CalcInputAliases.Canonicalise(Section.Template, [conflicting]));
        Assert.Contains("parts[2].t", refused.Message, StringComparison.Ordinal);
        Assert.Contains("parts[2].t_w", refused.Message, StringComparison.Ordinal);
        Assert.Contains("with different values", refused.Message, StringComparison.Ordinal);
        Assert.NotNull(parts.ResolveColumn("t"));
    }

    [Fact]
    public void A_CSV_Export_Of_A_Table_With_The_Old_Key_Imports_With_The_Thickness_Under_The_New_Key()
    {
        var old = Composite("t");

        var csv = CalcCsv.ExportInputs(Section.Template, old);
        Assert.Contains("parts[2].t,", csv, StringComparison.Ordinal);
        var (back, notices) = CalcCsv.ImportInputs(Section.Template, csv);

        Assert.Empty(notices);
        var tube = back.Find("parts")!.Rows![1];
        Assert.Equal((10.0, "mm"), (tube.Get("t_w")!.Number, tube.Get("t_w")!.Unit));
        Assert.Null(tube.Get("t"));
        Assert.Equal(CalcRunner.Run(Section, Composite("t_w")).OutputSi("A"), CalcRunner.Run(Section, back).OutputSi("A"), 12);

        // A column that was never one of the table's is still skipped with a warning, not stored.
        var (_, unknown) = CalcCsv.ImportInputs(Section.Template, csv.Replace("parts[2].t,", "parts[2].thickness,", StringComparison.Ordinal));
        Assert.Contains(unknown, n => n.Message.Contains("has no column 'thickness'", StringComparison.Ordinal));
    }
}

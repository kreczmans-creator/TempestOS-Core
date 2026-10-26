using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>A calculator with a table of segments, a plain length and a choice, to prove what the CSV exchange keeps.</summary>
internal sealed class SegmentTableCalculator : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.segments", "1.0.0", CalcDiscipline.Mechanical, "Segments",
        "Sums a table of segments.",
        "Total: L = Σ l.",
        [
            new CalcInputSpec("segments", "Segments", CalcInputKind.Table, MaxRows: 10, Columns:
            [
                new CalcColumnSpec("name", "Name", CalcInputKind.Text, IsOptional: true),
                new CalcColumnSpec("l", "Length", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e6),
                new CalcColumnSpec("kind", "Kind", CalcInputKind.Choice, Choices: ["a", "b"], IsOptional: true),
                new CalcColumnSpec("on", "Active", CalcInputKind.Boolean, IsOptional: true),
            ]),
            new CalcInputSpec("gap", "Gap", CalcInputKind.Quantity, "mm", Default: 2, HardMin: 0, HardMax: 1e6),
            new CalcInputSpec("optional_rows", "Optional rows", CalcInputKind.Table, MaxRows: 5, IsOptional: true, Columns: [new CalcColumnSpec("w", "Weight", CalcInputKind.Number)]),
        ],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "two segments", new CalcReferenceSource("Hand", null, "10 + 20 + 2"), [Segments((10, "a"), (20, "b")), CalcInputValue.Quantity("gap", 2, "mm")], [new CalcExpectedValue("L", 32, "mm", 1e-9)]),
            new CalcTestCase("ka-2", "one segment", new CalcReferenceSource("Hand", null, "5 + 2"), [Segments((5, "a")), CalcInputValue.Quantity("gap", 2, "mm")], [new CalcExpectedValue("L", 7, "mm", 1e-9)]),
        ]);

    public static CalcInputValue Segments(params (double L, string Kind)[] rows) =>
        CalcInputValue.Table("segments", rows.Select((r, i) => CalcRow.Of(("name", new CalcCell(Text: $"s{i + 1}")), ("l", new CalcCell(r.L, "mm")), ("kind", new CalcCell(Text: r.Kind)), ("on", new CalcCell(Flag: i % 2 == 0)))).ToList());

    public void Run(CalcRunContext c)
    {
        c.Input("gap");
        var total = 2 * 0.0 + c.Rows("segments").Sum(r => r.Get("l")?.Number ?? 0);
        c.Record("l_sum", "Σ l", $"{c.Rows("segments").Count} row(s)", Engine.Values.ScalarValue.Of(total, "mm"), "Sum of the segments");
        c.Assign("L", "l_sum + gap", "mm", "Total length");
        c.Output("L", "Total length");
    }
}

/// <summary>Blue round (Addendum A): the CSV exchange keeps tables; sheets collapse repeated record warnings; new sheets open on declared defaults.</summary>
public sealed class BlueRoundTests
{
    private static readonly SegmentTableCalculator Calculator = new();

    private static CalcTemplate Template => Calculator.Template;

    private static CalcInputSet Inputs() => new([SegmentTableCalculator.Segments((10, "a"), (20, "b"), (30.5, "a")) with { Provenance = CalcProvenance.Drawing("DRG-1", "B") }, CalcInputValue.Quantity("gap", 2, "mm", CalcProvenance.Specification("SPEC-1"))]);

    [Fact]
    public void B_A_05_A_Table_Survives_The_CSV_Round_Trip_With_Its_Units_Kinds_Flags_And_Provenance()
    {
        var csv = CalcCsv.ExportInputs(Template, Inputs());
        var (back, notices) = CalcCsv.ImportInputs(Template, csv);

        Assert.Empty(notices);
        var table = back.Find("segments")!;
        Assert.Equal(3, table.Rows!.Count);
        Assert.Equal(("Drawing", "DRG-1", "B"), (table.Provenance!.Kind.ToString(), table.Provenance.Reference, table.Provenance.Revision));
        Assert.Equal(30.5, table.Rows[2].Get("l")!.Number);
        Assert.Equal("mm", table.Rows[2].Get("l")!.Unit);
        Assert.Equal("b", table.Rows[1].Get("kind")!.Text);
        Assert.Equal([true, false, true], table.Rows.Select(r => r.Get("on")!.Flag!.Value));
        Assert.Equal("s2", table.Rows[1].Get("name")!.Text);
        Assert.Equal(CalcInputJson.Write(Inputs()), CalcInputJson.Write(back));

        // The run agrees: nothing was dropped on the way.
        Assert.Equal(CalcRunner.Run(Calculator, Inputs()).Outputs.Select(o => o.ValueText), CalcRunner.Run(Calculator, back).Outputs.Select(o => o.ValueText));
    }

    [Fact]
    public void B_A_05_A_Table_Cell_Provenance_Such_As_A_Link_Round_Trips()
    {
        var link = CalcProvenance.Link(Guid.NewGuid(), "L_out", "A");
        var rows = new[] { CalcRow.Of(("l", new CalcCell(12, "mm", Provenance: link))) };
        var (back, notices) = CalcCsv.ImportInputs(Template, CalcCsv.ExportInputs(Template, new CalcInputSet([CalcInputValue.Table("segments", rows)])));

        Assert.Empty(notices);
        Assert.Equal(link, back.Find("segments")!.Rows![0].Get("l")!.Provenance);
    }

    [Fact]
    public void B_A_05_An_Empty_Table_And_A_Table_Without_A_Value_Are_Kept_As_They_Were()
    {
        var (back, notices) = CalcCsv.ImportInputs(Template, CalcCsv.ExportInputs(Template, new CalcInputSet([CalcInputValue.Table("optional_rows", [])])));

        Assert.Empty(notices);
        Assert.Empty(back.Find("optional_rows")!.Rows!);
        Assert.Null(back.Find("segments"));
    }

    private static CalcProvenance WithheldPin() => new(CalcProvenanceKind.Reference, $"record:{Guid.NewGuid():N}@1 · {CalcCsv.WithheldMarker}", "2018");

    [Fact]
    public void P_A_01_A_Withheld_Scalar_Is_Refused_On_Import_Not_Replaced_By_The_Default()
    {
        var csv = CalcCsv.ExportInputs(Template, new CalcInputSet([new CalcInputValue("gap", Provenance: WithheldPin())]));
        var (back, notices) = CalcCsv.ImportInputs(Template, csv);

        Assert.Null(back.Find("gap"));
        var block = Assert.Single(notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Contains("gap", block.Message, StringComparison.Ordinal);
        Assert.Contains("apply the record again", block.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void P_A_01_A_Withheld_Table_Stays_Withheld_And_The_Calculation_Is_Blocked_Not_Run_On_Defaults()
    {
        var csv = CalcCsv.ExportInputs(Template, new CalcInputSet([new CalcInputValue("segments", Provenance: WithheldPin())]));
        var (back, notices) = CalcCsv.ImportInputs(Template, csv);

        // The table comes back as an empty value carrying its pin, so a run (or the form's carried table) cannot fall back on rows nobody entered.
        Assert.Null(back.Find("segments")!.Rows);
        Assert.True(CalcCsv.IsWithheld(back.Find("segments")!.Provenance));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Warning && n.Message.Contains("withheld", StringComparison.Ordinal));
        Assert.DoesNotContain(notices, n => n.Severity == CalcNoticeSeverity.Block);

        var result = CalcRunner.Run(Calculator, back);
        Assert.True(result.IsBlocked);
        Assert.Contains(result.Blocks, b => b.Message.Contains("segments", StringComparison.Ordinal) && b.Message.Contains("apply the record again", StringComparison.Ordinal));
    }

    [Fact]
    public void P_A_01_A_Withheld_Scalar_In_A_Set_That_Is_Run_Directly_Is_Blocked_Not_Defaulted()
    {
        var inputs = new CalcInputSet([SegmentTableCalculator.Segments((10, "a")), new CalcInputValue("gap", Provenance: WithheldPin())]);
        var result = CalcRunner.Run(Calculator, inputs);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Blocks, b => b.Message.StartsWith("gap:", StringComparison.Ordinal) && b.Message.Contains("apply the record again", StringComparison.Ordinal));
    }

    [Fact]
    public void P_A_01_A_Withheld_Scalar_In_A_JSON_Import_Is_Refused_Too()
    {
        var json = CalcInputJson.Write(new CalcInputSet([new CalcInputValue("gap", Provenance: WithheldPin())]));
        var (back, notices) = CalcInputJson.Read(json, Template);

        Assert.Empty(back.Values);
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.StartsWith("gap:", StringComparison.Ordinal) && n.Message.Contains("apply the record again", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("No", false)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    public void B_A_20_A_Yes_No_Given_As_Text_Is_Accepted_In_JSON_And_CSV(string text, bool expected)
    {
        var json = "[{\"Symbol\":\"slender\",\"Flag\":\"" + text + "\"},{\"Symbol\":\"rows\",\"Rows\":[{\"Cells\":{\"on\":{\"Flag\":\"" + text + "\"}}}]}]";
        var (back, notices) = CalcInputJson.Read(json);
        Assert.Empty(notices);
        Assert.Equal(expected, back.Find("slender")!.Flag);
        Assert.Equal(expected, back.Find("rows")!.Rows![0].Get("on")!.Flag);

        var csv = $"symbol,value,unit,provenance,reference,revision\nsegments\nsegments[1]\nsegments[1].l,5,mm\nsegments[1].on,{text}\n";
        var (fromCsv, csvNotices) = CalcCsv.ImportInputs(Template, csv);
        Assert.Empty(csvNotices);
        Assert.Equal(expected, fromCsv.Find("segments")!.Rows![0].Get("on")!.Flag);
    }

    [Fact]
    public void B_A_20_Something_That_Is_Not_Yes_Or_No_Is_Refused_Rather_Than_Read_As_No()
    {
        var (_, notices) = CalcCsv.ImportInputs(Template, "symbol,value\nsegments[1].on,maybe\n");
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("yes or no", StringComparison.Ordinal));

        var (_, jsonNotices) = CalcInputJson.Read("""[{"Symbol":"slender","Flag":"maybe"}]""");
        Assert.Contains(jsonNotices, n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void B_A_05_A_Broken_Table_In_A_File_Blocks_Rather_Than_Dropping_Rows()
    {
        const string head = "symbol,value,unit,provenance,reference,revision\n";

        var gap = CalcCsv.ImportInputs(Template, head + "segments,,,,,\nsegments[1],,,,,\nsegments[1].l,10,mm,,,\nsegments[3],,,,,\nsegments[3].l,5,mm,,,\n").Notices;
        Assert.Contains(gap, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("row 2 is missing", StringComparison.Ordinal));

        var bad = CalcCsv.ImportInputs(Template, head + "segments[1].l,abc,mm,,,\n").Notices;
        Assert.Contains(bad, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("finite number", StringComparison.Ordinal));

        var formula = CalcCsv.ImportInputs(Template, head + "segments[1].name,=1+1,,,,\n").Notices;
        Assert.Contains(formula, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("formula", StringComparison.Ordinal));

        var twice = CalcCsv.ImportInputs(Template, head + "segments[1].l,1,mm,,,\nsegments[1].l,2,mm,,,\n").Notices;
        Assert.Contains(twice, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("appears twice", StringComparison.Ordinal));

        var tooMany = CalcCsv.ImportInputs(Template, head + "segments[11].l,1,mm,,,\n").Notices;
        Assert.Contains(tooMany, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("at most 10 rows", StringComparison.Ordinal));

        var unknown = CalcCsv.ImportInputs(Template, head + "segments[1].zz,1,,,,\n").Notices;
        Assert.Contains(unknown, n => n.Severity == CalcNoticeSeverity.Warning && n.Message.Contains("no column 'zz'", StringComparison.Ordinal));
    }

    [Fact]
    public void G_A_02_NewSheetInputs_Takes_Declared_Defaults_Only_Never_A_Test_Case()
    {
        var template = Template with { Inputs = [.. Template.Inputs.Select(i => i.Symbol == "gap" ? i with { Default = 2 } : i)] };
        var opened = CalcTemplateHasher.NewSheetInputs(template);

        Assert.Equal(2, opened.Find("gap")!.Number);
        Assert.Null(opened.Find("segments"));
        Assert.NotNull(CalcTemplateHasher.DefaultInputs(template).Find("segments"));

        // An input whose only value is in a test case opens blank.
        var noDefault = Template with { Inputs = [.. Template.Inputs.Select(i => i.Symbol == "gap" ? i with { Default = null } : i)], TestCases = [Template.TestCases[0] with { Inputs = [CalcInputValue.Quantity("gap", 99, "mm")] }, Template.TestCases[1]] };
        Assert.Null(CalcTemplateHasher.NewSheetInputs(noDefault).Find("gap"));
        Assert.Equal(99, CalcTemplateHasher.DefaultInputs(noDefault).Find("gap")!.Number);
    }

    [Fact]
    public void B_A_18_Record_Warnings_Collapse_On_The_Sheet_Only()
    {
        var template = Template with { RecordKinds = [new CalcRecordKind("test.segments", "Segment data", ["segments", "gap"], "From your own source.")] };
        var calculator = new Wrapped(template, Calculator);
        var inputs = Inputs();
        var result = CalcRunner.Run(calculator, inputs);

        // The run keeps one warning per symbol (the form shows each next to its input)...
        Assert.Equal(2, result.Warnings.Count(w => w.RuleId.StartsWith("record.", StringComparison.Ordinal)));

        // ... the sheet prints them once.
        var sheet = CalcSheetBuilder.Build(template, inputs, result,
            new CalcSheetHeader("P", "C", template.Title, "A", CalcSheetStatus.Draft, "a", null, null, null, null),
            GoldenFiles.FixedFooter(template, 2, 2));
        var notice = Assert.Single(sheet.Warnings, w => w.RuleId == "record");
        Assert.Equal("records: tabulated values not taken from a reference record are segments, gap (Segment data). Enter each as one so its source, edition and licence travel with it.", notice.Message);
        Assert.DoesNotContain(sheet.Warnings, w => w.RuleId.StartsWith("record.", StringComparison.Ordinal));
    }

    [Fact]
    public void R_A_01_A_Withheld_Table_Prints_Its_Pin_And_No_Rows()
    {
        var inputs = Inputs();
        var result = CalcRunner.Run(Calculator, inputs);
        var sheet = CalcSheetBuilder.Build(Template, inputs, result,
            new CalcSheetHeader("P", "C", Template.Title, "A", CalcSheetStatus.Draft, "a", null, null, null, null),
            GoldenFiles.FixedFooter(Template, 2, 2), null, new Dictionary<string, string> { ["segments"] = "record:abc@1" });

        Assert.DoesNotContain(sheet.Inputs, r => r.Symbol.StartsWith("segments[", StringComparison.Ordinal) && r.Symbol != "segments[…]");
        var withheld = Assert.Single(sheet.Inputs, r => r.Symbol == "segments[…]");
        Assert.Equal("record:abc@1", withheld.Source);
        Assert.Contains("rows withheld (licensed record)", withheld.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("30.5", CalcSheetTextRenderer.Render(sheet).Split("WORKING", 2)[0], StringComparison.Ordinal);
    }

    /// <summary>A calculator running another's code under a changed template.</summary>
    private sealed class Wrapped(CalcTemplate template, ICalcCalculator inner) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = template;

        public void Run(CalcRunContext c) => inner.Run(c);
    }
}

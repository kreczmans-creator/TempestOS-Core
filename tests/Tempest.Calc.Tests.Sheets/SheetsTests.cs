using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Sheets;

public sealed class SheetsTests
{
    private static readonly DirectStressCalculator Calculator = new();

    private static CalcInputSet Inputs(double f = 12, double a = 200, double allow = 160, string grade = "S275", bool slender = false) => new(
    [
        CalcInputValue.Quantity("F", f, "kN", CalcProvenance.Drawing("DRG-001", "B")),
        CalcInputValue.Quantity("A", a, "mm^2", CalcProvenance.Drawing("DRG-001", "B")),
        CalcInputValue.Quantity("sigma_allow", allow, "MPa", CalcProvenance.Specification("SPEC-7 §4")),
        CalcInputValue.Choice("grade", grade, CalcProvenance.Assumed("Grade not stated on the drawing.")),
        CalcInputValue.Switch("slender", slender, CalcProvenance.Manual),
    ]);

    // ---- validation -------------------------------------------------

    [Fact]
    public void Valid_Inputs_Produce_No_Notices() => Assert.Empty(CalcInputValidator.Validate(Calculator.Template, Inputs()));

    [Fact]
    public void Missing_Required_Input_Without_Default_Blocks()
    {
        var template = Calculator.Template with { Inputs = [new CalcInputSpec("x", "Something", CalcInputKind.Quantity, "mm")] };
        var notices = CalcInputValidator.Validate(template, new CalcInputSet());
        Assert.Single(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("x (Something) needs a value", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_Input_With_Default_Is_Fine() => Assert.Empty(CalcInputValidator.Validate(Calculator.Template, new CalcInputSet()));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_Finite_Numbers_Block(double value)
    {
        var notices = CalcInputValidator.Validate(Calculator.Template, Inputs(f: value));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.StartsWith("F must be a finite number", StringComparison.Ordinal));
    }

    [Fact]
    public void Wrong_Dimension_Blocks()
    {
        var inputs = Inputs().With(CalcInputValue.Quantity("F", 12, "mm"));
        var notices = CalcInputValidator.Validate(Calculator.Template, inputs);
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("unit of kN, not mm", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_Unit_Blocks()
    {
        var inputs = Inputs().With(CalcInputValue.Quantity("F", 12, "furlongs"));
        Assert.Contains(CalcInputValidator.Validate(Calculator.Template, inputs), n => n.Severity == CalcNoticeSeverity.Block && n.Message.StartsWith("F:", StringComparison.Ordinal));
    }

    [Fact]
    public void Soft_Range_Warns_Hard_Range_Blocks()
    {
        Assert.Contains(CalcInputValidator.Validate(Calculator.Template, Inputs(f: 20_000)), n => n.Severity == CalcNoticeSeverity.Warning && n.Message.Contains("outside its usual range", StringComparison.Ordinal));
        Assert.Contains(CalcInputValidator.Validate(Calculator.Template, Inputs(f: 2e6)), n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("must be at most", StringComparison.Ordinal));
        Assert.Contains(CalcInputValidator.Validate(Calculator.Template, Inputs(a: 0)), n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("must be at least", StringComparison.Ordinal));
    }

    [Fact]
    public void Limits_Are_Checked_In_The_Default_Unit()
    {
        // 2 MN = 2000 kN, within the soft max of 10 000 kN.
        Assert.Empty(CalcInputValidator.Validate(Calculator.Template, Inputs().With(CalcInputValue.Quantity("F", 2, "MN"))));
    }

    [Fact]
    public void Bad_Choice_Blocks() => Assert.Contains(CalcInputValidator.Validate(Calculator.Template, Inputs(grade: "S999")), n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("must be one of", StringComparison.Ordinal));

    [Fact]
    public void Unknown_Symbol_Warns() => Assert.Contains(CalcInputValidator.Validate(Calculator.Template, Inputs().With(CalcInputValue.Of("zz", 1))), n => n.Severity == CalcNoticeSeverity.Warning && n.Message.Contains("zz is not an input", StringComparison.Ordinal));

    [Fact]
    public void Number_With_Unit_Blocks()
    {
        var template = Calculator.Template with { Inputs = [new CalcInputSpec("n", "Count", CalcInputKind.Number)] };
        Assert.Contains(CalcInputValidator.Validate(template, new CalcInputSet([CalcInputValue.Quantity("n", 1, "mm")])), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void Long_Text_Blocks() => Assert.Contains(CalcInputValidator.Validate(Calculator.Template, Inputs().With(new CalcInputValue("note", Text: new string('x', 300)))), n => n.Severity == CalcNoticeSeverity.Block);

    [Fact]
    public void Vector_Validation_Checks_Length_Finiteness_And_Unit()
    {
        var template = Calculator.Template with { Inputs = [new CalcInputSpec("v", "Curve", CalcInputKind.Vector, "m")] };
        Assert.Contains(CalcInputValidator.Validate(template, new CalcInputSet([CalcInputValue.VectorOf("v", [1, double.NaN], "m")])), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Contains(CalcInputValidator.Validate(template, new CalcInputSet([CalcInputValue.VectorOf("v", [1, 2], "kg")])), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Contains(CalcInputValidator.Validate(template, new CalcInputSet([CalcInputValue.VectorOf("v", new double[CalcInputValidator.MaxVectorLength + 1], "m")])), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Empty(CalcInputValidator.Validate(template, new CalcInputSet([CalcInputValue.VectorOf("v", [1, 2], "mm")])));
    }

    [Fact]
    public void Table_Validation_Checks_Rows_And_Cells()
    {
        var template = Calculator.Template with
        {
            Inputs = [new CalcInputSpec("rows", "Rows", CalcInputKind.Table, Columns: [new CalcColumnSpec("d", "Diameter", CalcInputKind.Quantity, "mm", HardMin: 0), new CalcColumnSpec("k", "Kind", CalcInputKind.Choice, Choices: ["a", "b"])], MaxRows: 2)],
        };

        var good = new CalcInputSet([CalcInputValue.Table("rows", [CalcRow.Of(("d", new CalcCell(10, "mm")), ("k", new CalcCell(Text: "a")))])]);
        Assert.Empty(CalcInputValidator.Validate(template, good));

        var badUnit = new CalcInputSet([CalcInputValue.Table("rows", [CalcRow.Of(("d", new CalcCell(10, "kg")), ("k", new CalcCell(Text: "a")))])]);
        Assert.Contains(CalcInputValidator.Validate(template, badUnit), n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("row 1, Diameter", StringComparison.Ordinal));

        var badChoice = new CalcInputSet([CalcInputValue.Table("rows", [CalcRow.Of(("d", new CalcCell(10, "mm")), ("k", new CalcCell(Text: "zz")))])]);
        Assert.Contains(CalcInputValidator.Validate(template, badChoice), n => n.Severity == CalcNoticeSeverity.Block);

        var negative = new CalcInputSet([CalcInputValue.Table("rows", [CalcRow.Of(("d", new CalcCell(-1, "mm")), ("k", new CalcCell(Text: "a")))])]);
        Assert.Contains(CalcInputValidator.Validate(template, negative), n => n.Severity == CalcNoticeSeverity.Block);

        var tooMany = new CalcInputSet([CalcInputValue.Table("rows", Enumerable.Range(0, 3).Select(_ => CalcRow.Of(("d", new CalcCell(10, "mm")), ("k", new CalcCell(Text: "a")))).ToList())]);
        Assert.Contains(CalcInputValidator.Validate(template, tooMany), n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("at most 2 rows", StringComparison.Ordinal));

        var missing = new CalcInputSet([CalcInputValue.Table("rows", [CalcRow.Of(("d", new CalcCell(10, "mm")))])]);
        Assert.Contains(CalcInputValidator.Validate(template, missing), n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("needs a value", StringComparison.Ordinal));
    }

    [Fact]
    public void Provenance_Is_Required_To_Issue()
    {
        Assert.Empty(CalcInputValidator.ValidateProvenanceForIssue(Calculator.Template, Inputs().With(CalcInputValue.Switch("slender", false, CalcProvenance.Assumed()))));
        var notices = CalcInputValidator.ValidateProvenanceForIssue(Calculator.Template, Inputs());
        Assert.Single(notices, n => n.Message.StartsWith("slender needs a source", StringComparison.Ordinal));
    }

    // ---- running ----------------------------------------------------

    [Fact]
    public void Run_Produces_Steps_Outputs_Checks_And_Series()
    {
        var r = CalcRunner.Run(Calculator, Inputs());
        Assert.False(r.IsBlocked);
        Assert.Equal(60e6, r.OutputSi("sigma"), 3);
        Assert.Equal(0.375, r.OutputSi("u"), 9);
        Assert.Single(r.Checks);
        Assert.True(r.Passed);
        Assert.Equal("Direct stress", r.Governing!.Name);
        Assert.Single(r.Series);
        Assert.Contains(r.Steps, s => s.Symbol == "sigma" && s.Substitution == "12 kN / 200 mm²" && s.ResultText == "60 MPa");
    }

    [Fact]
    public void Run_With_Blocking_Input_Never_Calls_The_Calculator()
    {
        var r = CalcRunner.Run(Calculator, Inputs(a: 0));
        Assert.True(r.IsBlocked);
        Assert.Empty(r.Steps);
        Assert.Empty(r.Outputs);
    }

    [Fact]
    public void Calculator_Blocks_Are_Reported()
    {
        var r = CalcRunner.Run(Calculator, Inputs(f: -5, slender: true));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "slender");
    }

    [Fact]
    public void Calculator_Warnings_Are_Reported()
    {
        var r = CalcRunner.Run(Calculator, Inputs(f: 30));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "high-stress");
        Assert.True(r.Passed);
        Assert.False(CalcRunner.Run(Calculator, Inputs(f: 40)).Passed);
    }

    [Fact]
    public void Engine_Failures_Become_Blocks_Not_Exceptions()
    {
        var broken = new BrokenCalculator();
        var r = CalcRunner.Run(broken, new CalcInputSet());
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "engine" && b.Message.Contains("Division by zero", StringComparison.Ordinal));
    }

    [Fact]
    public void Cancellation_Becomes_A_Block()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var r = CalcRunner.Run(new CancellingCalculator(), new CalcInputSet(), cancellationToken: cts.Token);
        Assert.Contains(r.Blocks, b => b.RuleId == "cancelled");
    }

    [Fact]
    public void Check_Across_Dimensions_Is_Refused()
    {
        var r = CalcRunner.Run(new MismatchedCheckCalculator(), new CalcInputSet());
        Assert.True(r.IsBlocked);
    }

    // ---- analysis ---------------------------------------------------

    [Fact]
    public void Tornado_Over_A_Calculator_Ranks_Inputs()
    {
        var bars = CalcRunner.Tornado(Calculator, Inputs(), "sigma", ["F", "A"]);
        Assert.Equal(2, bars.Count);
        Assert.True(bars[0].Swing > 0);
        Assert.Equal("A", bars[0].Input);
    }

    [Fact]
    public void Monte_Carlo_Over_A_Calculator_Reports_Yield()
    {
        var r = CalcRunner.MonteCarlo(Calculator, Inputs(), "sigma", new Dictionary<string, Distribution>
        {
            ["F"] = Distribution.Normal(12_000, 600),
            ["A"] = Distribution.Uniform(195e-6, 205e-6),
        }, samples: 2_000, seed: 3, upperLimit: 70e6);
        Assert.Equal(2_000, r.Samples);
        Assert.InRange(r.Mean, 55e6, 65e6);
        Assert.NotNull(r.Yield);
        Assert.InRange(r.Yield!.Value, 0.9, 1.0);
    }

    [Fact]
    public void Monte_Carlo_Over_A_Non_Numeric_Input_Is_Refused() =>
        Assert.Throws<Engine.CalcEvaluationException>(() => CalcRunner.MonteCarlo(Calculator, Inputs(), "sigma", new Dictionary<string, Distribution> { ["grade"] = Distribution.Uniform(0, 1) }, samples: 10));

    // ---- sheet ------------------------------------------------------

    private static CalcSheet Sheet(CalcInputSet? inputs = null)
    {
        inputs ??= Inputs();
        var result = CalcRunner.Run(Calculator, inputs);
        var header = new CalcSheetHeader("P-001 — Test project", "CALC-001", "Direct stress check", "A", CalcSheetStatus.Draft, "s.kreczman", null, new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero), null, null);
        var footer = new CalcSheetFooter("test.direct-stress@1.0.0", "abcdef0123456789", "calc-engine/1.0.0", 1, 2, new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero));
        return CalcSheetBuilder.Build(Calculator.Template, inputs, result, header, footer);
    }

    [Fact]
    public void Sheet_Has_Every_Section_In_Order()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        var order = new[] { "CALCULATION SHEET — DRAFT", "1. SCOPE AND METHOD", "2. INPUTS", "3. ASSUMPTIONS", "4. WORKING", "5. RESULTS", "6. CHECKS", "7. APPLICABILITY", "8. VERIFICATION" };
        var last = -1;

        foreach (var marker in order)
        {
            var at = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(at > last, $"'{marker}' missing or out of order");
            last = at;
        }
    }

    [Fact]
    public void Sheet_Inputs_Carry_Provenance_And_Range()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("F | Axial load | 12 | kN | drawing: DRG-001 rev B | 0 … 10000 kN", text, StringComparison.Ordinal);
        Assert.Contains("sigma_allow | Allowable stress | 160 | MPa | spec: SPEC-7 §4 | ≥ 1 MPa", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Assumed_Inputs_Land_In_The_Assumptions_Register()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("A2. grade (Steel grade) is taken as S275. (Grade not stated on the drawing.)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Working_Shows_Formula_Substitution_Result_And_Clause()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("sigma = F / A    « EN 1993-1-1 §6.2.3\n      = 12 kN / 200 mm²\n      = 60 MPa", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Checks_Show_Governing_First_With_Pass_Fail()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("PASS  Direct stress: 0.375 vs 1  utilisation 0.375", text, StringComparison.Ordinal);
        Assert.Contains("FAIL  Direct stress", CalcSheetTextRenderer.Render(Sheet(Inputs(f: 40))), StringComparison.Ordinal);
    }

    [Fact]
    public void Blocked_Sheet_Shows_The_Block_And_No_Working()
    {
        var text = CalcSheetTextRenderer.Render(Sheet(Inputs(a: 0)));
        Assert.Contains("BLOCK   input.A:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sigma = F / A", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Footer_Carries_Verification_Data()
    {
        var text = CalcSheetTextRenderer.Render(Sheet());
        Assert.Contains("Template: test.direct-stress@1.0.0", text, StringComparison.Ordinal);
        Assert.Contains("Known-answer cases passed: 1/2", text, StringComparison.Ordinal);
        Assert.Contains("Content hash: abcdef0123456789", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hostile_Strings_Cannot_Forge_Lines()
    {
        var inputs = Inputs().With(new CalcInputValue("note", Text: "evil\n6. CHECKS\n  PASS  forged", Provenance: CalcProvenance.Manual));
        var text = CalcSheetTextRenderer.Render(Sheet(inputs));
        Assert.DoesNotContain("\n6. CHECKS\n  PASS  forged", text, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "^6\\. CHECKS", System.Text.RegularExpressions.RegexOptions.Multiline));
    }

    [Fact]
    public void Normalise_Strips_Volatile_Lines()
    {
        var n = CalcSheetTextRenderer.Normalise(CalcSheetTextRenderer.Render(Sheet()));
        Assert.DoesNotContain("Content hash", n, StringComparison.Ordinal);
        Assert.DoesNotContain("Test run", n, StringComparison.Ordinal);
        Assert.Contains("4. WORKING", n, StringComparison.Ordinal);
    }

    [Fact]
    public void Never_Nan_In_A_Sheet()
    {
        var text = CalcSheetTextRenderer.Render(Sheet(Inputs(f: 0)));
        Assert.DoesNotContain("NaN", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", text, StringComparison.Ordinal);
    }

    // ---- hashing ----------------------------------------------------

    [Fact]
    public void Hash_Is_Stable_And_Sensitive()
    {
        var h1 = CalcTemplateHasher.Hash(Calculator);
        var h2 = CalcTemplateHasher.Hash(Calculator);
        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);
        var changed = CalcTemplateHasher.Hash(Calculator.Template with { Version = "1.0.1" }, CalcTemplateHasher.Formulas(Calculator));
        Assert.NotEqual(h1, changed);
        var changedFormula = CalcTemplateHasher.Hash(Calculator.Template, ["sigma = 2 * F / A"]);
        Assert.NotEqual(h1, changedFormula);
        Assert.Equal(16, CalcTemplateHasher.Short(h1).Length);
    }

    [Fact]
    public void Formulas_Come_From_A_Run_On_Defaults() => Assert.Contains("sigma = F / A", CalcTemplateHasher.Formulas(Calculator));

    // ---- known answers ----------------------------------------------

    [Fact]
    public void Known_Answer_Runner_Passes_Both_Cases()
    {
        var report = KnownAnswerRunner.Run(Calculator);
        Assert.Equal(2, report.Total);
        Assert.Equal(2, report.Passed);
        Assert.Equal(1, report.Unconfirmed);
        Assert.True(report.AllPassed);
    }

    [Fact]
    public void Known_Answer_Runner_Reports_A_Wrong_Expectation()
    {
        var wrong = Calculator.Template.TestCases[0] with { Expected = [new CalcExpectedValue("sigma", 61, "MPa", 0.001)] };
        var outcome = KnownAnswerRunner.RunCase(Calculator, wrong);
        Assert.False(outcome.Passed);
        Assert.InRange(outcome.Checks[0].RelativeError, 0.016, 0.017);
    }

    [Fact]
    public void Known_Answer_Runner_Reports_Missing_Output_And_Dimension_Mismatch()
    {
        var missing = Calculator.Template.TestCases[0] with { Expected = [new CalcExpectedValue("nope", 1, "MPa")] };
        Assert.False(KnownAnswerRunner.RunCase(Calculator, missing).Passed);
        var mismatch = Calculator.Template.TestCases[0] with { Expected = [new CalcExpectedValue("sigma", 60, "kg")] };
        Assert.False(KnownAnswerRunner.RunCase(Calculator, mismatch).Passed);
    }

    // ---- golden -----------------------------------------------------

    [Fact]
    public void Golden_File_Matches()
    {
        var path = GoldenFiles.SourcePath("direct-stress");
        var actual = GoldenFiles.Render(Calculator, Inputs());
        var diff = GoldenFiles.Compare(path, actual);
        Assert.True(diff is null || diff.StartsWith("Golden file created", StringComparison.Ordinal), diff);
        Assert.Null(GoldenFiles.Compare(path, actual));
    }

    // ---- csv --------------------------------------------------------

    [Fact]
    public void Csv_Round_Trips_Inputs()
    {
        var csv = CalcCsv.ExportInputs(Calculator.Template, Inputs());
        Assert.Contains("F,12,kN,Drawing,DRG-001,B", csv, StringComparison.Ordinal);
        var (imported, notices) = CalcCsv.ImportInputs(Calculator.Template, csv);
        Assert.Empty(notices);
        Assert.Equal(12, imported.Find("F")!.Number);
        Assert.Equal("kN", imported.Find("F")!.Unit);
        Assert.Equal(CalcProvenanceKind.Drawing, imported.Find("F")!.Provenance!.Kind);
        Assert.Equal("S275", imported.Find("grade")!.Text);
        Assert.False(imported.Find("slender")!.Flag);
    }

    [Fact]
    public void Csv_Export_Neutralises_Formula_Cells()
    {
        var inputs = Inputs().With(new CalcInputValue("note", Text: "=HYPERLINK(\"http://x\")"));
        var csv = CalcCsv.ExportInputs(Calculator.Template, inputs);
        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\")\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_Import_Refuses_Formula_Cells()
    {
        var (_, notices) = CalcCsv.ImportInputs(Calculator.Template, "symbol,value\nF,=1+1\n");
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("spreadsheet formula", StringComparison.Ordinal));
    }

    [Fact]
    public void Csv_Import_Accepts_Negative_Numbers() => Assert.Equal(-5, CalcCsv.ImportInputs(Calculator.Template, "symbol,value,unit\nF,-5,kN\n").Inputs.Find("F")!.Number);

    [Fact]
    public void Csv_Import_Caps_Size()
    {
        var huge = "symbol,value\n" + string.Concat(Enumerable.Repeat("F,1\n", 300_000));
        var (_, notices) = CalcCsv.ImportInputs(Calculator.Template, huge);
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("larger than", StringComparison.Ordinal));
    }

    [Fact]
    public void Csv_Import_Caps_Rows()
    {
        var many = "symbol,value\n" + string.Concat(Enumerable.Repeat("F,1\n", CalcCsv.MaxRows + 5));
        var (_, notices) = CalcCsv.ImportInputs(Calculator.Template, many);
        Assert.Contains(notices, n => n.Message.Contains("more than", StringComparison.Ordinal));
    }

    [Fact]
    public void Csv_Import_Handles_Bad_Rows_Without_Throwing()
    {
        var (imported, notices) = CalcCsv.ImportInputs(Calculator.Template, "symbol,value\nF,abc\nzz,1\n\"unterminated,1\n");
        Assert.Null(imported.Find("F"));
        Assert.Contains(notices, n => n.Message.Contains("finite number", StringComparison.Ordinal));
        Assert.Contains(notices, n => n.Message.Contains("zz is not an input", StringComparison.Ordinal));
    }

    [Fact]
    public void Csv_Import_Without_Header_Blocks() => Assert.Contains(CalcCsv.ImportInputs(Calculator.Template, "a,b\n1,2\n").Notices, n => n.Severity == CalcNoticeSeverity.Block);

    [Fact]
    public void Csv_Outputs_Export()
    {
        var csv = CalcCsv.ExportOutputs(CalcRunner.Run(Calculator, Inputs()));
        Assert.Contains("sigma,Direct stress,60,MPa", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_Escapes_Quotes_And_Commas() => Assert.Equal("\"a,\"\"b\"\"\"", CalcCsv.Escape("a,\"b\""));

    private sealed class BrokenCalculator : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new("test.broken", "1.0.0", CalcDiscipline.Mechanical, "Broken", "", "", [], [], [], [], []);

        public void Run(CalcRunContext context) => context.Assign("x", "1 / 0");
    }

    private sealed class CancellingCalculator : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new("test.cancel", "1.0.0", CalcDiscipline.Mechanical, "Cancel", "", "", [], [], [], [], []);

        public void Run(CalcRunContext context) => context.CancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class MismatchedCheckCalculator : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new("test.mismatch", "1.0.0", CalcDiscipline.Mechanical, "Mismatch", "", "", [], [], [], [], []);

        public void Run(CalcRunContext context)
        {
            context.Assign("a", "1 m");
            context.Assign("b", "1 kg");
            context.Check("bad", "a", "b");
        }
    }
}

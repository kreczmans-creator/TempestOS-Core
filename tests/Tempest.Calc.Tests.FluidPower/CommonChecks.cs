using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>
/// The definition-of-done checks every worksheet calculator must pass
/// (the same set <c>Tempest.Calc.Tests.Calculators.CommonCalculatorTests</c>
/// runs for the other disciplines): known answers, a well-formed template,
/// a clean run on defaults, hostile numbers that never reach the sheet,
/// empty tables that block cleanly, a golden sheet, a stable template hash
/// and a sheet that renders in under a second. Every check runs for every
/// calculator and ALL failures are reported together (a calculator that
/// breaks three checks says so, not just the first), so a run over
/// <see cref="Tempest.Calc.FluidPower.FluidPowerCalculators.All"/> is as
/// informative as the theory the other disciplines have, and passes
/// vacuously while that list is empty (a theory with no data would fail).
/// </summary>
public static class CommonChecks
{
    /// <summary>The checks, by the name a failure is reported under.</summary>
    private static readonly (string Name, Action<ICalcCalculator> Check)[] Checks =
    [
        ("discipline", calculator => Assert.Equal(CalcDiscipline.FluidPower, calculator.Template.Discipline)),
        ("known answers", KnownAnswers),
        ("template is well formed", TemplateIsWellFormed),
        ("defaults run without a block", DefaultsRunWithoutABlock),
        ("a new sheet says what to enter next", NewSheetSaysWhatToEnterNext),
        ("hostile numbers never reach a sheet", HostileNumbersNeverReachASheet),
        ("empty tables block cleanly", EmptyTablesBlockCleanly),
        ("golden sheet", GoldenSheetMatches),
        ("template hash is stable", calculator => Assert.Equal(CalcTemplateHasher.Hash(calculator), CalcTemplateHasher.Hash(calculator))),
        ("renders under a second", RendersUnderASecond),
        ("has an id", calculator => Assert.False(string.IsNullOrWhiteSpace(calculator.Template.Id))),
    ];

    /// <summary>Runs every check on one calculator and fails once, listing every check that failed.</summary>
    public static void Run(ICalcCalculator calculator) => RunAll([calculator]);

    /// <summary>Runs every check on every calculator and fails once, listing every check that failed for each.</summary>
    public static void RunAll(IEnumerable<ICalcCalculator> calculators)
    {
        var failures = Collect(calculators);
        Assert.True(failures.Count == 0, $"{failures.Count} common check(s) failed:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    /// <summary>Every failure of every check on every calculator, as "id / check: what went wrong"; empty when all pass.</summary>
    public static IReadOnlyList<string> Collect(IEnumerable<ICalcCalculator> calculators)
    {
        ArgumentNullException.ThrowIfNull(calculators);
        var failures = new List<string>();

        foreach (var calculator in calculators)
        {
            ArgumentNullException.ThrowIfNull(calculator);

            foreach (var (name, check) in Checks)
            {
                try
                {
                    check(calculator);
                }
                catch (Exception ex)
                {
                    var firstLine = ex.Message.Split('\n', 2)[0].Trim();
                    failures.Add($"{calculator.Template.Id} / {name}: {(ex is Xunit.Sdk.XunitException ? string.Empty : ex.GetType().Name + ": ")}{firstLine}");
                }
            }
        }

        return failures;
    }

    private static void KnownAnswers(ICalcCalculator calculator)
    {
        var id = calculator.Template.Id;
        var report = KnownAnswerRunner.Run(calculator);
        Assert.True(report.Total >= 2, $"{id} declares {report.Total} known-answer case(s); at least two are required");

        foreach (var outcome in report.Outcomes)
        {
            var detail = string.Join("; ", outcome.Checks.Select(c => $"{c.Symbol}: expected {c.Expected} {c.Unit}, got {c.Actual:G6} (tol {c.RelativeTolerance:P2}{(c.Problem is null ? string.Empty : ", " + c.Problem)})"));
            Assert.True(outcome.Passed, $"{id} / {outcome.Case.Id} [{outcome.Case.Status}] {outcome.Case.Source.Title} — {outcome.Problem}{detail}");
        }
    }

    private static void TemplateIsWellFormed(ICalcCalculator calculator)
    {
        var t = calculator.Template;
        Assert.NotEmpty(t.Inputs);
        Assert.NotEmpty(t.Assumptions);
        Assert.NotEmpty(t.ClauseRefs);
        Assert.All(t.ClauseRefs, c => Assert.True(c.Title.Length < 120, "a clause reference carries a number and a title only"));
        Assert.NotEmpty(t.Scope);
        Assert.NotEmpty(t.Method);
        Assert.Equal(t.Inputs.Count, t.Inputs.Select(i => i.Symbol).Distinct(StringComparer.Ordinal).Count());
        Assert.All(t.Inputs, i => Assert.True(i.Kind != CalcInputKind.Quantity || i.DefaultUnit.Length > 0, $"{i.Symbol} needs a default unit"));
        Assert.All(t.Inputs, i => Assert.True(i.HardMin is null || i.Min is null || i.HardMin <= i.Min, $"{i.Symbol}: hard minimum above soft minimum"));
        Assert.All(t.Inputs, i => Assert.True(i.HardMax is null || i.Max is null || i.HardMax >= i.Max, $"{i.Symbol}: hard maximum below soft maximum"));
    }

    private static void DefaultsRunWithoutABlock(ICalcCalculator calculator)
    {
        var result = CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(calculator.Template));
        Assert.False(result.IsBlocked, string.Join("; ", result.Blocks.Select(b => b.Message)));
        Assert.NotEmpty(result.Outputs);
        Assert.NotEmpty(result.Steps);
    }

    private static void NewSheetSaysWhatToEnterNext(ICalcCalculator calculator)
    {
        var result = CalcRunner.Run(calculator, CalcTemplateHasher.NewSheetInputs(calculator.Template));

        foreach (var block in result.Blocks)
        {
            Assert.DoesNotContain("needs a value", block.Message, StringComparison.Ordinal);
            Assert.Matches(@"^[A-Za-z_0-9]+(\[[^\]]*\])?(\.[A-Za-z_0-9]+)?: ", block.Message);
            Assert.Contains("enter", block.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void HostileNumbersNeverReachASheet(ICalcCalculator calculator)
    {
        var id = calculator.Template.Id;
        var baseline = CalcTemplateHasher.DefaultInputs(calculator.Template);

        foreach (var spec in calculator.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number))
        {
            foreach (var hostile in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1e300, -1e300, 1e-300 })
            {
                var inputs = baseline.With(new CalcInputValue(spec.Symbol, Number: hostile, Unit: spec.Kind == CalcInputKind.Quantity ? spec.DefaultUnit : null));
                var result = CalcRunner.Run(calculator, inputs);
                var sheet = CalcSheetBuilder.Build(calculator.Template, inputs, result, GoldenFiles.FixedHeader(id, id), GoldenFiles.FixedFooter(calculator.Template, 0, 0));
                var text = CalcSheetTextRenderer.Render(sheet);
                Assert.DoesNotContain("NaN", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Infinity", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
            }
        }
    }

    private static void EmptyTablesBlockCleanly(ICalcCalculator calculator)
    {
        var id = calculator.Template.Id;
        var baseline = CalcTemplateHasher.DefaultInputs(calculator.Template);

        foreach (var spec in calculator.Template.Inputs.Where(i => i.Kind == CalcInputKind.Table))
        {
            var result = CalcRunner.Run(calculator, baseline.With(CalcInputValue.Table(spec.Symbol, [])));
            Assert.True(result.IsBlocked || result.Outputs.Count > 0, $"{id}: an empty {spec.Symbol} table produced neither a block nor outputs");
        }
    }

    private static void GoldenSheetMatches(ICalcCalculator calculator)
    {
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var diff = GoldenFiles.Compare(GoldenFiles.SourcePath(calculator.Template.Id), GoldenFiles.Render(calculator, inputs));
        Assert.True(diff is null || diff.StartsWith("Golden file created", StringComparison.Ordinal), diff);
    }

    private static void RendersUnderASecond(ICalcCalculator calculator)
    {
        var id = calculator.Template.Id;
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = CalcRunner.Run(calculator, inputs);
        CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(calculator.Template, inputs, result, GoldenFiles.FixedHeader(id, id), GoldenFiles.FixedFooter(calculator.Template, 0, 0)));
        Assert.True(sw.ElapsedMilliseconds < 1000, $"{id} took {sw.ElapsedMilliseconds} ms");
    }
}

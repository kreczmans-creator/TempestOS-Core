using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>The §5 Definition-of-Done checks every calculator must pass: known answers, robustness, golden sheet, template hygiene.</summary>
public sealed class CommonCalculatorTests
{
    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    [Trait("Verification", "KnownAnswer")]
    public void Every_Known_Answer_Case_Passes(string id)
    {
        var calculator = AllCalculators.Get(id);
        var report = KnownAnswerRunner.Run(calculator);
        Assert.True(report.Total >= 2, $"{id} declares {report.Total} known-answer case(s); at least two are required");

        foreach (var outcome in report.Outcomes)
        {
            var detail = string.Join("; ", outcome.Checks.Select(c => $"{c.Symbol}: expected {c.Expected} {c.Unit}, got {c.Actual:G6} (tol {c.RelativeTolerance:P2}{(c.Problem is null ? string.Empty : ", " + c.Problem)})"));
            Assert.True(outcome.Passed, $"{id} / {outcome.Case.Id} [{outcome.Case.Status}] {outcome.Case.Source.Title} — {outcome.Problem}{detail}");
        }
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Template_Is_Well_Formed(string id)
    {
        var t = AllCalculators.Get(id).Template;
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

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Method_Is_One_Step_Per_Line_Each_Saying_What_It_Calculates(string id)
    {
        // The owner's ask (2026-10-24): each line of the method says what it is doing before the relation, so the sheet
        // does not read as a wall of formulas. "What it calculates: relation"; a line without a relation is narrative.
        var lines = AllCalculators.Get(id).Template.Method.Split('\n');
        Assert.True(lines.Length >= 3, $"{id}: the method is {lines.Length} line(s); state it one step per line");

        foreach (var line in lines)
        {
            Assert.False(string.IsNullOrWhiteSpace(line), $"{id}: blank method line");
            var at = line.IndexOf(": ", StringComparison.Ordinal);
            var words = at > 0 ? line[..at] : line;
            Assert.True(words.Count(c => c == ' ') >= 1 && char.IsLetter(words[0]), $"{id}: '{line}' does not say in words what it calculates");
        }

        Assert.Contains(lines, l => l.Contains(": ", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Defaults_Or_First_Case_Run_Without_A_Block(string id)
    {
        var calculator = AllCalculators.Get(id);
        var result = CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(calculator.Template));
        Assert.False(result.IsBlocked, string.Join("; ", result.Blocks.Select(b => b.Message)));
        Assert.NotEmpty(result.Outputs);
        Assert.NotEmpty(result.Steps);
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void A_New_Sheet_That_Opens_Blocked_Says_What_To_Enter_Next(string id)
    {
        // Purple P-A-05: a table or required input with no value on a new sheet is not just "needs a value".
        var calculator = AllCalculators.Get(id);
        var result = CalcRunner.Run(calculator, CalcTemplateHasher.NewSheetInputs(calculator.Template));

        foreach (var block in result.Blocks)
        {
            Assert.DoesNotContain("needs a value", block.Message, StringComparison.Ordinal);
            Assert.Matches(@"^[A-Za-z_0-9]+(\[[^\]]*\])?(\.[A-Za-z_0-9]+)?: ", block.Message);
            Assert.Contains("enter", block.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Hostile_Numbers_Never_Throw_And_Never_Reach_A_Sheet(string id)
    {
        var calculator = AllCalculators.Get(id);
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

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Empty_Table_Inputs_Block_Cleanly(string id)
    {
        var calculator = AllCalculators.Get(id);
        var baseline = CalcTemplateHasher.DefaultInputs(calculator.Template);

        foreach (var spec in calculator.Template.Inputs.Where(i => i.Kind == CalcInputKind.Table))
        {
            var result = CalcRunner.Run(calculator, baseline.With(CalcInputValue.Table(spec.Symbol, [])));
            Assert.True(result.IsBlocked || result.Outputs.Count > 0, $"{id}: an empty {spec.Symbol} table produced neither a block nor outputs");
        }
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Golden_Sheet_Matches(string id)
    {
        var calculator = AllCalculators.Get(id);
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var diff = GoldenFiles.Compare(GoldenFiles.SourcePath(id), GoldenFiles.Render(calculator, inputs));
        Assert.True(diff is null || diff.StartsWith("Golden file created", StringComparison.Ordinal), diff);
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Hash_Is_Deterministic(string id)
    {
        var calculator = AllCalculators.Get(id);
        Assert.Equal(CalcTemplateHasher.Hash(calculator), CalcTemplateHasher.Hash(calculator));
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Sheet_Renders_Under_A_Second(string id)
    {
        var calculator = AllCalculators.Get(id);
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = CalcRunner.Run(calculator, inputs);
        CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(calculator.Template, inputs, result, GoldenFiles.FixedHeader(id, id), GoldenFiles.FixedFooter(calculator.Template, 0, 0)));
        Assert.True(sw.ElapsedMilliseconds < 1000, $"{id} took {sw.ElapsedMilliseconds} ms");
    }
}

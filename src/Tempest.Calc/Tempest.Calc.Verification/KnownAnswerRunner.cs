using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Verification;

/// <summary>One expected value checked against what the calculator produced.</summary>
public sealed record KnownAnswerCheck(string Symbol, double Expected, string Unit, double Actual, double RelativeTolerance, bool Passed, string? Problem = null)
{
    public double RelativeError => Expected == 0 ? Math.Abs(Actual) : Math.Abs(Actual - Expected) / Math.Abs(Expected);
}

/// <summary>The outcome of one known-answer case.</summary>
public sealed record KnownAnswerOutcome(CalcTestCase Case, bool Passed, IReadOnlyList<KnownAnswerCheck> Checks, string? Problem = null);

/// <summary>Every case of a template, run and compared.</summary>
public sealed record KnownAnswerReport(string TemplateReference, IReadOnlyList<KnownAnswerOutcome> Outcomes, DateTimeOffset RunAt)
{
    public int Passed => Outcomes.Count(o => o.Passed);

    public int Total => Outcomes.Count;

    public int Unconfirmed => Outcomes.Count(o => o.Case.Status == CalcReferenceStatus.Unconfirmed);

    /// <summary>Cases whose expected values come from a secondary source (Addendum B §B4).</summary>
    public int Secondary => Outcomes.Count(o => o.Case.Source.Secondary);

    public bool AllPassed => Outcomes.All(o => o.Passed);
}

/// <summary>Runs a calculator's declared known-answer cases and compares each expected output within its tolerance.</summary>
public static class KnownAnswerRunner
{
    public static KnownAnswerReport Run(ICalcCalculator calculator, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        var outcomes = calculator.Template.TestCases.Select(c => RunCase(calculator, c)).ToList();
        return new KnownAnswerReport(calculator.Template.Reference, outcomes, (time ?? TimeProvider.System).GetUtcNow());
    }

    public static KnownAnswerOutcome RunCase(ICalcCalculator calculator, CalcTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(testCase);

        var result = CalcRunner.Run(calculator, new CalcInputSet(testCase.Inputs));

        if (result.IsBlocked)
            return new KnownAnswerOutcome(testCase, false, [], string.Join(" ", result.Blocks.Select(b => b.Message)));

        var checks = new List<KnownAnswerCheck>();

        foreach (var e in testCase.Expected)
        {
            var output = result.FindOutput(e.Symbol);

            if (output is null)
            {
                checks.Add(new KnownAnswerCheck(e.Symbol, e.Value, e.Unit, double.NaN, e.RelativeTolerance, false, $"'{e.Symbol}' is not an output."));
                continue;
            }

            if (output.Value is not ScalarValue scalar)
            {
                checks.Add(new KnownAnswerCheck(e.Symbol, e.Value, e.Unit, double.NaN, e.RelativeTolerance, false, $"'{e.Symbol}' is not a single number."));
                continue;
            }

            if (!UnitCatalog.TryParse(e.Unit, out var unit, out var error))
            {
                checks.Add(new KnownAnswerCheck(e.Symbol, e.Value, e.Unit, double.NaN, e.RelativeTolerance, false, error));
                continue;
            }

            if (unit.Dimension != scalar.Dimension)
            {
                checks.Add(new KnownAnswerCheck(e.Symbol, e.Value, e.Unit, double.NaN, e.RelativeTolerance, false, $"'{e.Symbol}' has dimension [{scalar.Dimension}], the expected value [{unit.Dimension}]."));
                continue;
            }

            var actual = unit.FromSi(scalar.Si);
            var passed = e.Value == 0 ? Math.Abs(actual) <= e.RelativeTolerance : Math.Abs(actual - e.Value) <= e.RelativeTolerance * Math.Abs(e.Value);
            checks.Add(new KnownAnswerCheck(e.Symbol, e.Value, e.Unit, actual, e.RelativeTolerance, passed));
        }

        return new KnownAnswerOutcome(testCase, checks.All(c => c.Passed), checks);
    }
}

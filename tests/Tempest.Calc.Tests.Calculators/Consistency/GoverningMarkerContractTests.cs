using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// The sheet marks the output row the governing check belongs to (C2-31, <see cref="CalcSheet.GoverningOutputSymbol"/>): the governing check's
/// <see cref="CalcCheck.StepSymbol"/> must name an emitted output, or the governing line loses its link to the Results table. The A03 rewrites
/// ("demand against capacity") put an input or an intermediate first as the demand, and the step symbol followed it, so the marker was lost on
/// eighteen templates. This contract runs every template on its defaults, its known-answer cases and its worked examples: wherever a utilisation
/// check governs, its step symbol is an output of that run, and when that output is a utilisation its value is the check's own ratio.
/// </summary>
public sealed class GoverningMarkerContractTests
{
    private static IEnumerable<(string Run, CalcRunResult Result)> Runs(ICalcCalculator calculator)
    {
        var template = calculator.Template;
        yield return ("defaults", CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(template)));

        foreach (var testCase in template.TestCases)
        {
            var set = CalcTemplateHasher.NewSheetInputs(template);

            foreach (var v in testCase.Inputs)
                set.Set(v);

            yield return ($"kat {testCase.Id}", CalcRunner.Run(calculator, set));
        }

        foreach (var example in WorkedExamples.All.Where(e => e.TemplateId == template.Id))
            yield return ($"example {example.Slug}", CalcRunner.Run(calculator, example.Inputs));
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void The_Governing_Utilisation_Check_Of_Every_Run_Names_An_Emitted_Output_Holding_Its_Ratio(string id)
    {
        var calculator = AllCalculators.Get(id);
        var problems = new List<string>();
        var marked = 0;

        foreach (var (run, result) in Runs(calculator))
        {
            if (result.IsBlocked || result.Governing is not { HasUtilisation: true } governing)
                continue;

            var output = governing.StepSymbol is { } symbol ? result.FindOutput(symbol) : null;

            if (output is null)
            {
                problems.Add($"{run}: the governing check '{governing.Name}' (u = {governing.Utilisation:G4}) has the step symbol '{governing.StepSymbol}', which is not an output of the run; outputs: {string.Join(", ", result.Outputs.Where(o => o.Kind == CalcOutputKind.Utilisation).Select(o => o.Symbol))}");
                continue;
            }

            if (output.Kind == CalcOutputKind.Utilisation && output.Value is Tempest.Calc.Engine.Values.ScalarValue { IsFinite: true } u && double.IsFinite(governing.Utilisation)
                && Math.Abs(u.Si - governing.Utilisation) > 1e-6 * Math.Max(1, Math.Abs(governing.Utilisation)))
                problems.Add($"{run}: the governing check '{governing.Name}' has u = {governing.Utilisation:G6} but its output '{output.Symbol}' holds {u.Si:G6}");

            marked++;
        }

        Assert.True(problems.Count == 0, $"{marked} run(s) marked; {problems.Count} lost:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }
}

using System.Text.RegularExpressions;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// Shot-fix integration (DC-31 open item): a working step whose substitution is empty prints a bare "=" line between its formula and its
/// result, on the form, the sheet text and the PDF. The screenshot review found it on <c>bs.duct-sizing</c> v_peak, <c>bs.psychrometrics</c>
/// SHR, the state dry-bulbs and densities, and <c>elec.enclosure-heat</c> V_duty. Every step of every worked example of every template, and
/// of every calculator on its defaults and its known-answer cases, must say what was substituted.
/// </summary>
public sealed partial class EmptySubstitutionContractTests
{
    [GeneratedRegex(@"^\s*=\s*$", RegexOptions.Multiline)]
    private static partial Regex BareEqualsLine();

    private static void AssertEverySubstitutionStated(string where, ICalcCalculator calculator, CalcInputSet inputs)
    {
        var template = calculator.Template;
        var result = CalcRunner.Run(calculator, inputs);
        var empty = result.Steps.Where(s => string.IsNullOrWhiteSpace(s.Substitution)).Select(s => $"[{s.Ordinal}] {s.Symbol}").ToList();
        Assert.True(empty.Count == 0, $"{where}: step(s) with an empty substitution: {string.Join(", ", empty)}");

        var sheet = CalcSheetBuilder.Build(template, inputs, result, GoldenFiles.FixedHeader(template.Id, template.Title), GoldenFiles.FixedFooter(template, 0, 0));
        var text = CalcSheetTextRenderer.Render(sheet);
        Assert.False(BareEqualsLine().IsMatch(text), $"{where}: the sheet text prints a bare '=' line");
    }

    [Theory]
    [MemberData(nameof(WorkedExamples.Keys), MemberType = typeof(WorkedExamples))]
    public void No_Step_Of_A_Worked_Example_Has_An_Empty_Substitution(string key)
    {
        var example = WorkedExamples.Get(key);
        AssertEverySubstitutionStated(key, AllCalculators.Get(example.TemplateId), example.Inputs);
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void No_Step_Of_A_Calculator_On_Its_Defaults_Or_Known_Answer_Cases_Has_An_Empty_Substitution(string id)
    {
        var calculator = AllCalculators.Get(id);
        var template = calculator.Template;
        AssertEverySubstitutionStated($"{id} on its defaults", calculator, CalcTemplateHasher.DefaultInputs(template));

        foreach (var testCase in template.TestCases)
        {
            var inputs = CalcTemplateHasher.NewSheetInputs(template);

            foreach (var value in testCase.Inputs)
                inputs.Set(value);

            AssertEverySubstitutionStated($"{id} / {testCase.Id}", calculator, inputs);
        }
    }

    /// <summary>The worked-example theory reaches every template: each of the shipped calculators has at least one worked example.</summary>
    [Fact]
    public void Every_Template_Has_A_Worked_Example_Under_The_Empty_Substitution_Check()
    {
        var covered = WorkedExamples.All.Select(e => e.TemplateId).ToHashSet(StringComparer.Ordinal);
        var missing = AllCalculators.All.Select(c => c.Template.Id).Where(id => !covered.Contains(id)).ToList();
        Assert.True(missing.Count == 0, "templates without a worked example: " + string.Join(", ", missing));
        Assert.Equal(54, AllCalculators.All.Count);
    }
}

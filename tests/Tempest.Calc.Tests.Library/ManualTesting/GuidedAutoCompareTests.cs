using Tempest.Calc.Library.ManualTesting;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>The auto-compare reads the form's run against a worked example by the one rule, and suggests a result the tester confirms.</summary>
public sealed class GuidedAutoCompareTests
{
    public static TheoryData<string> ExampleIds => [.. GuidedTestScript.Current.Steps.Where(s => s.Kind == GuidedStepKind.Example).Select(s => s.Id)];

    /// <summary>
    /// The examples whose printed expected value differs from the engine's output by more than the allowance of the digits the form shows, found when the
    /// Guided test was built (Addendum C, C-5) and re-counted with the form's own digits (C-2: seven examples that differed from the five figures
    /// assumed then agree with the four or five the form shows, or with the directed rounding it uses for a safety factor and a failing utilisation). Each expected value there is a hand calculation or a published figure held to the example's
    /// own verification tolerance (<c>CalcExpectedValue.RelativeTolerance</c>, 1 % by default), not to the digits printed, so the auto-compare
    /// puts the line to the tester instead of failing it. The list is closed: an example that joins it is a new difference to look at.
    /// </summary>
    private static readonly HashSet<string> ForReview = new(StringComparer.Ordinal)
    {
        "example:struct.section-properties/ub-457x191x98",
        "example:bs.duct-sizing/office-supply-equal-friction",
        "example:elec.voltage-drop/three-phase-motor-feeder",
        "example:mech.press-fit/hollow-shaft",
        "example:mech.iso286-fit/h7-p6-50mm",

        // Joined with the 1 % cap (security review SEC-C-02): the README prints g6's es as the standard's rounded -7 µm and c_min as 7 µm; the engine
        // gives -7.2853 and 7.2853 µm. The half-unit rule alone let 0.29 µm (4 %) pass; the tester now decides.
        "example:mech.iso286-fit/h7-g6-25mm",

        // Not a digit difference: the README states p0 in a bare "bar" and the form shows "bar a" (absolute). A bare bar names no basis, so the rule
        // does not take it for absolute (L10, review); the tester confirms it is the absolute pre-charge.
        "example:fp.accumulator/clamp-circuit-bladder-accumulator",
    };

    private static CalcRunResult RunExample(GuidedTestStep step, Func<CalcInputSet, CalcInputSet>? change = null)
    {
        var calculator = ShippedCalculators.All.Single(c => c.Template.Id == step.TemplateId);
        var (inputs, notices) = CalcInputJson.Read(step.InputsJson!, calculator.Template);
        Assert.DoesNotContain(notices, n => n.Severity == CalcNoticeSeverity.Block);
        return CalcRunner.Run(calculator, change?.Invoke(inputs) ?? inputs);
    }

    /// <summary>The strongest check the rule has: the real engine, fed each example's inputs.json as the app feeds it, shows outputs that every expected value agrees with.</summary>
    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void The_Engines_Own_Outputs_Agree_With_Every_Example_By_The_Rule(string stepId)
    {
        var step = GuidedTestScript.Current.Find(stepId)!;
        var result = GuidedAutoCompare.Compare(step, RunExample(step));

        // Never a Fail. An output whose printed expected value differs from the engine's in the last digits (a hand calculation held to the example's own tolerance) is put to the tester.
        Assert.True(result.Suggested != ManualTestOutcome.Fail && !result.Mismatches.Any(), $"{stepId}: " + string.Join(" | ", result.Rows.Where(r => !r.Comparison.IsMatch).Select(r => r.Comparison.Detail)));
        Assert.True(result.Suggested == ManualTestOutcome.Pass || ForReview.Contains(stepId), $"{stepId} needs a look by hand but is not in the list of known examples: " + string.Join(" | ", result.Rows.Where(r => !r.Comparison.IsMatch).Select(r => r.Comparison.Detail)));
        Assert.True(result.Suggested != ManualTestOutcome.Pass || !ForReview.Contains(stepId), $"{stepId} now agrees with its printed expected values; take it out of ForReview.");
        Assert.Equal(step.Expected!.Count + (step.Governing is null ? 1 : 2), result.Rows.Count); // a governing check is two lines (its name and outcome, its utilisation); "none" is one
        Assert.False(string.IsNullOrWhiteSpace(result.ValuesShown));
        Assert.True(result.ValuesShown.Length <= ManualTestRun.MaxValuesShownLength);
    }

    [Fact]
    public void A_Changed_Input_Is_Suggested_As_A_Fail_And_The_Lines_That_Differ_Are_Named()
    {
        var step = GuidedTestScript.Current.Find("example:mech.bearing-life-basic/ball-bearing-combined-95")!;
        var run = RunExample(step, inputs =>
        {
            var changed = new CalcInputSet(inputs.Values);
            var f = inputs.Find("F_r")!;
            changed.Set(CalcInputValue.Quantity("F_r", f.Number!.Value * 1.1, f.Unit!, f.Provenance));
            return changed;
        });

        var result = GuidedAutoCompare.Compare(step, run);
        Assert.Equal(ManualTestOutcome.Fail, result.Suggested);
        Assert.Contains(result.Mismatches, r => r.Symbol == "P");
        Assert.All(result.Mismatches, r => Assert.StartsWith(r.Symbol + ":", r.Comparison.Detail, StringComparison.Ordinal));
    }

    [Fact]
    public void A_Blocked_Run_Is_Left_To_The_Tester_With_The_Blocks_Reason()
    {
        var step = GuidedTestScript.Current.Find("example:mech.bearing-life-basic/ball-bearing-combined-95")!;
        var blocked = CalcRunResult.Blocked([new CalcNotice("input.C", CalcNoticeSeverity.Block, "C must be above zero.")]);

        var result = GuidedAutoCompare.Compare(step, blocked);
        Assert.Null(result.Suggested);
        Assert.Contains("C must be above zero.", result.ValuesShown, StringComparison.Ordinal);
        Assert.Equal(GuidedCompareOutcome.NotComparable, Assert.Single(result.Rows).Comparison.Outcome);
    }

    [Fact]
    public void An_Output_The_Form_Does_Not_Have_Is_A_Difference_Not_A_Silence()
    {
        var step = GuidedTestScript.Current.Find("example:mech.bearing-life-basic/ball-bearing-combined-95")!;
        var empty = new CalcRunResult([], [], [], [], [], []);
        var result = GuidedAutoCompare.Compare(step, empty);
        Assert.Equal(ManualTestOutcome.Fail, result.Suggested);
        Assert.Contains(result.Rows, r => r.Symbol == "P" && r.Comparison.Detail.Contains("no such output", StringComparison.Ordinal));
        Assert.Contains(result.Rows, r => r.Symbol == "governing utilisation" && r.Comparison.Outcome == GuidedCompareOutcome.Mismatch);
    }

    // ---------------------------------------------------------------- L9: the governing check's name, and "none"

    private const string BearingId = "example:mech.bearing-life-basic/ball-bearing-combined-95";
    private const string NoGoverningId = "example:struct.beam-formulas/ss-point-offcentre";

    [Fact]
    public void A_Governing_Check_With_The_Right_Outcome_But_Another_Name_Is_A_Difference()
    {
        var step = GuidedTestScript.Current.Find(BearingId)!;
        var run = RunExample(step);
        var renamed = run with { Checks = [.. run.Checks.Select(c => c with { Name = "Some other check" })] };

        var result = GuidedAutoCompare.Compare(step, renamed);

        Assert.Equal(ManualTestOutcome.Fail, result.Suggested);
        var line = Assert.Single(result.Mismatches, r => r.Symbol == "governing check");
        Assert.Contains("Some other check", line.Comparison.Detail, StringComparison.Ordinal);
        Assert.Contains(step.Governing!.Name, line.Comparison.Detail, StringComparison.Ordinal);
        Assert.Contains("Some other check", result.ValuesShown, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Example_Whose_Readme_Says_Governing_Check_None_Asserts_That_The_Form_Has_None()
    {
        var step = GuidedTestScript.Current.Find(NoGoverningId)!;
        Assert.Null(step.Governing);
        var run = RunExample(step);
        Assert.Null(run.Governing);

        var plain = GuidedAutoCompare.Compare(step, run);
        Assert.Equal(ManualTestOutcome.Pass, plain.Suggested);
        Assert.Contains(plain.Rows, r => r.Symbol == "governing check" && r.Expected == "none" && r.Shown == "none" && r.Comparison.IsMatch);

        // A form that now shows a governing check is not the example the pack describes.
        var withCheck = run with { Checks = [new CalcCheck("An unexpected check", "1", "2", 0.5, true)] };
        var result = GuidedAutoCompare.Compare(step, withCheck);
        Assert.Equal(ManualTestOutcome.Fail, result.Suggested);
        var line = Assert.Single(result.Mismatches);
        Assert.Equal("governing check", line.Symbol);
        Assert.Contains("An unexpected check", line.Comparison.Detail, StringComparison.Ordinal);
        Assert.Contains("has none", line.Comparison.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_Example_The_Script_Gives_No_Governing_Check_Is_One_The_Readme_Says_Has_None()
    {
        // The generator refuses a Governing check line it cannot read, so a null here is "none" and never an unread line (L13).
        var none = GuidedTestScript.Current.Steps.Where(s => s.Kind == GuidedStepKind.Example && s.Governing is null).Select(s => s.Id).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(11, none.Count);
        var readmes = Directory.GetFiles(Path.Combine(GuidedTestHelpers.RepositoryRoot, "docs", "worked-examples"), "README.md", SearchOption.AllDirectories);

        foreach (var id in none)
        {
            var folder = id["example:".Length..].Split('/');
            var readme = readmes.Single(f => f.EndsWith(Path.Combine(folder[0], folder[1], "README.md"), StringComparison.Ordinal));
            Assert.Contains(File.ReadLines(readme), l => l.StartsWith("Governing check: none", StringComparison.Ordinal));
        }
    }

    // ---------------------------------------------------------------- L6: the allowance applied is on the line

    [Fact]
    public void Each_Compared_Number_Carries_The_Allowance_Applied_And_A_Text_Carries_None()
    {
        var step = GuidedTestScript.Current.Find(BearingId)!;
        var result = GuidedAutoCompare.Compare(step, RunExample(step));

        Assert.Equal("±0.005", result.Rows.Single(r => r.Symbol == "P").Comparison.Allowance);
        Assert.Equal("±0.5", result.Rows.Single(r => r.Symbol == "L_10h").Comparison.Allowance); // the form prints 5 figures of 16667 h, so its last digit is the unit
        Assert.NotNull(result.Rows.Single(r => r.Symbol == "governing utilisation").Comparison.Allowance);
        Assert.Null(result.Rows.Single(r => r.Symbol == "governing check").Comparison.Allowance);
    }
}

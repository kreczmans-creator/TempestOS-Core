using Tempest.Calc.Library.ManualTesting;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>The auto-compare reads the form's run against a worked example by the one rule, and suggests a result the tester confirms.</summary>
public sealed class GuidedAutoCompareTests
{
    public static TheoryData<string> ExampleIds => [.. GuidedTestScript.Current.Steps.Where(s => s.Kind == GuidedStepKind.Example).Select(s => s.Id)];

    /// <summary>
    /// The examples whose printed expected value differs from the engine's output by more than half a unit in its last digit, found when the
    /// Guided test was built (Addendum C, C-5). Each expected value there is a hand calculation or a published figure held to the example's
    /// own verification tolerance (<c>CalcExpectedValue.RelativeTolerance</c>, 1 % by default), not to the digits printed, so the auto-compare
    /// puts the line to the tester instead of failing it. The list is closed: an example that joins it is a new difference to look at.
    /// </summary>
    private static readonly HashSet<string> ForReview = new(StringComparer.Ordinal)
    {
        "example:struct.frame-2d/two-span-continuous-beam",
        "example:struct.en1993-member/ub-beam-bending-shear",
        "example:struct.fillet-weld/normal-load-overloaded",
        "example:struct.section-properties/ub-457x191x98",
        "example:struct.column-buckling/slender-strut-curve-b",
        "example:bs.duct-sizing/office-supply-equal-friction",
        "example:bs.pipe-pump/oil-transfer-laminar",
        "example:elec.voltage-drop/dc-battery-feed",
        "example:elec.voltage-drop/three-phase-motor-feeder",
        "example:elec.motor-sizing/geared-index-table",
        "example:mech.press-fit/hollow-shaft",
        "example:mech.iso286-fit/h7-p6-50mm",
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
        Assert.Equal(step.Expected!.Count + (step.Governing is null ? 0 : 2), result.Rows.Count);
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
}

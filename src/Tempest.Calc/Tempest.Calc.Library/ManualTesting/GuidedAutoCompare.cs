using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library.ManualTesting;

/// <summary>One line of an auto-compare: an expected output, what the form shows for it, and how they compare.</summary>
public sealed record GuidedOutputComparison(string Symbol, string Expected, string Shown, GuidedComparison Comparison);

/// <summary>What the form's current outputs say against a worked example (an auto-compare), and the result it suggests for the tester to confirm.</summary>
/// <param name="Rows">One line per expected output, then the governing check.</param>
/// <param name="Suggested">Pass when every line matches, Fail when any line differs, <see langword="null"/> when nothing can be decided by rule (the run is blocked, or only units or forms the rule does not cover).</param>
/// <param name="ValuesShown">The shown values as one line, for the run record.</param>
public sealed record GuidedAutoCompareResult(IReadOnlyList<GuidedOutputComparison> Rows, ManualTestOutcome? Suggested, string ValuesShown)
{
    /// <summary>The lines that differ.</summary>
    public IReadOnlyList<GuidedOutputComparison> Mismatches => [.. Rows.Where(r => r.Comparison.Outcome == GuidedCompareOutcome.Mismatch)];
}

/// <summary>
/// Reads the outputs of a worksheet run (what the form shows) against a worked example's expected values by the one rule of
/// <see cref="GuidedTolerance"/> (Addendum C, C-5). It only reads a <see cref="CalcRunResult"/>; the tester confirms or overrides what it suggests.
/// </summary>
public static class GuidedAutoCompare
{
    /// <summary>The most characters of shown values kept (the run record caps it again).</summary>
    public const int MaxShownLength = ManualTestRun.MaxValuesShownLength;

    /// <summary>Compares <paramref name="run"/> with the expected outputs and governing check of <paramref name="example"/>.</summary>
    public static GuidedAutoCompareResult Compare(GuidedTestStep example, CalcRunResult run)
    {
        ArgumentNullException.ThrowIfNull(example);
        ArgumentNullException.ThrowIfNull(run);

        var rows = new List<GuidedOutputComparison>();
        var shown = new List<string>();

        if (run.IsBlocked)
        {
            var reason = run.Blocks[0].Message;
            rows.Add(new GuidedOutputComparison("run", "no block", reason, new GuidedComparison(GuidedCompareOutcome.NotComparable, $"The worksheet is blocked: {reason}")));
            return new GuidedAutoCompareResult(rows, null, Clip("blocked: " + reason));
        }

        foreach (var expected in example.Expected ?? [])
        {
            var output = run.FindOutput(expected.Symbol);
            var text = output?.ValueText;
            var comparison = output is null
                ? new GuidedComparison(GuidedCompareOutcome.Mismatch, $"{expected.Symbol}: the form has no such output.")
                : AgainstExpected(expected, text, CalcDisplay.RoundingOf(output));
            rows.Add(new GuidedOutputComparison(expected.Symbol, Join(expected.Value, expected.Unit), text ?? "—", comparison));
            shown.Add($"{expected.Symbol} = {text ?? "—"}");
        }

        if (example.Governing is { } governing)
        {
            var form = run.Governing;
            var outcomeText = form is null ? "none" : form.Passed ? "PASS" : "FAIL";

            // The check's name is compared with its outcome: a worksheet whose governing check is another one is not the example, though both may PASS.
            var expectedCheck = $"{governing.Outcome} — {governing.Name}";
            var shownCheck = form is null ? "none" : $"{outcomeText} — {form.Name}";
            rows.Add(new GuidedOutputComparison("governing check", expectedCheck, shownCheck, GuidedTolerance.CompareText("governing check", expectedCheck, shownCheck)));

            // What the governing banner prints for a ratio: two decimals, by the display's own formatter (not a figure count of this class's own); the digits shown decide the allowance.
            var shownUtilisation = form is null ? null : double.IsInfinity(form.Utilisation) ? "—" : CalcDisplay.Utilisation(form.Utilisation);
            var utilisation = form is null
                ? new GuidedComparison(GuidedCompareOutcome.Mismatch, "governing utilisation: the form has no governing check.")
                : GuidedTolerance.CompareShown("governing utilisation", governing.Utilisation, string.Empty, shownUtilisation, form.Utilisation > 1.0 + 1e-9 ? CalcDisplay.Rounding.Up : CalcDisplay.Rounding.Nearest);
            rows.Add(new GuidedOutputComparison("governing utilisation", governing.Utilisation, shownUtilisation ?? "—", utilisation));
            shown.Add(form is null ? "governing none" : $"governing {outcomeText} — {form.Name} (utilisation {shownUtilisation ?? "—"})");
        }
        else if (example.Kind == GuidedStepKind.Example)
        {
            // The example documents no governing check ("Governing check: none"), so a form that shows one is not what the example describes.
            var form = run.Governing;
            var formText = form is null ? "none" : $"{(form.Passed ? "PASS" : "FAIL")} — {form.Name}";
            var comparison = form is null
                ? new GuidedComparison(GuidedCompareOutcome.Match, "governing check: the form has none, as the example says.")
                : new GuidedComparison(GuidedCompareOutcome.Mismatch, $"governing check: the form shows a governing check ('{form.Name}', {(form.Passed ? "PASS" : "FAIL")}) where the example has none.");
            rows.Add(new GuidedOutputComparison("governing check", "none", formText, comparison));
            shown.Add("governing " + formText);
        }

        ManualTestOutcome? suggested = rows.Any(r => r.Comparison.Outcome == GuidedCompareOutcome.Mismatch) ? ManualTestOutcome.Fail
            : rows.Count > 0 && rows.All(r => r.Comparison.Outcome == GuidedCompareOutcome.Match) ? ManualTestOutcome.Pass
            : null;

        return new GuidedAutoCompareResult(rows, suggested, Clip(string.Join("; ", shown)));
    }

    /// <summary>
    /// The rule against the printed expected value. Where that fails but the form shows exactly what this build computed for the example when its
    /// sheet was made, the difference is the example's own (its expected value is held to a verification tolerance, not to the printed digits), so it is
    /// put to the tester rather than called a fail: nothing has changed since the example was made.
    /// </summary>
    private static GuidedComparison AgainstExpected(GuidedExpectedValue expected, string? text, CalcDisplay.Rounding rounding)
    {
        var comparison = GuidedTolerance.CompareShown(expected.Symbol, expected.Value, expected.Unit, text, rounding);

        if (comparison.Outcome != GuidedCompareOutcome.Mismatch || string.IsNullOrEmpty(expected.Computed))
            return comparison;

        return GuidedTolerance.CompareShown(expected.Symbol, expected.Computed, expected.Unit, text, rounding).IsMatch
            ? new GuidedComparison(GuidedCompareOutcome.NotComparable, $"{comparison.Detail.TrimEnd('.')}, but it equals {expected.Computed}, the value this build computed for the example. The example's own tolerance accepts that difference; decide by hand.")
            : comparison;
    }

    private static string Join(string value, string unit) => unit.Length == 0 ? value : $"{value} {unit}";

    private static string Clip(string text) => text.Length <= MaxShownLength ? text : text[..(MaxShownLength - 1)] + "…";
}

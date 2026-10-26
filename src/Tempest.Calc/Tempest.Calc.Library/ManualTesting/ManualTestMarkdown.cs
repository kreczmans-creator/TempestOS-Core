using System.Text;

namespace Tempest.Calc.Library.ManualTesting;

/// <summary>
/// A run as the markdown results sheet of the runbook's section 11: the same six columns (Step, Item, Result, Value shown, Expected, Note),
/// one row per script step in script order, under a header that carries what the owner sends back with it (the build commit, the tester and the dates).
/// Text from the tester is escaped so that it cannot break the table: a pipe becomes <c>\|</c> and a line break becomes <c>&lt;br&gt;</c>.
/// </summary>
public static class ManualTestMarkdown
{
    /// <summary>Writes the sheet. A step with no result is listed as <c>Not run</c>.</summary>
    public static string Export(ManualTestRun run, GuidedTestScript script)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(script);

        var tally = ManualTestTally.Of(run, script);
        var sb = new StringBuilder();
        sb.Append("# TempestOS guided test results\n\n");
        sb.Append("- Build: ").Append(Line(run.Build)).Append('\n');
        sb.Append("- Commit: ").Append(Line(run.BuildCommit)).Append('\n');
        sb.Append("- Tester: ").Append(Line(run.Tester)).Append('\n');
        sb.Append("- Started: ").Append(ManualTestRunService.Stamp(run.StartedUtc)).Append('\n');
        sb.Append("- Finished: ").Append(run.FinishedUtc is { } finished ? ManualTestRunService.Stamp(finished) : "not finished").Append('\n');
        sb.Append("- Script: ").Append(Line(run.ScriptVersion)).Append('\n');
        sb.Append("- Run: ").Append(run.Id.ToString("D")).Append('\n');
        sb.Append("- Summary: ").Append(tally).Append("\n\n");
        sb.Append("| Step | Item (template, example or script) | Result (Pass / Fail / Blocked) | Value shown | Expected | Note |\n");
        sb.Append("|---|---|---|---|---|---|\n");

        foreach (var step in script.Steps)
        {
            var result = run.ResultFor(step.Id);
            sb.Append("| ").Append(Cell(step.Section))
                .Append(" | ").Append(Cell(ItemOf(step)))
                .Append(" | ").Append(result is null ? "Not run" : result.Outcome.ToString())
                .Append(" | ").Append(Cell(result?.ValuesShown))
                .Append(" | ").Append(Cell(ExpectedOf(step)))
                .Append(" | ").Append(Cell(result?.Note))
                .Append(" |\n");
        }

        sb.Append("\nSend the sheet back with the commit above. Faults already in `v0.26.0-known-defects.md` need only a tick.\n");
        return sb.ToString();
    }

    /// <summary>The Item column: the template and example of a worked example, or the step's own title.</summary>
    public static string ItemOf(GuidedTestStep step) =>
        step.Kind == GuidedStepKind.Example ? $"{step.TemplateId} — {step.ExampleId}" : step.Title;

    /// <summary>The Expected column: a worked example's expected outputs and governing check, or what a manual step must show.</summary>
    public static string ExpectedOf(GuidedTestStep step)
    {
        if (step.Kind != GuidedStepKind.Example)
            return step.PassWhen ?? string.Empty;

        var parts = (step.Expected ?? []).Select(e => $"{e.Symbol} = {e.Value}{(e.Unit.Length > 0 ? " " + e.Unit : string.Empty)}").ToList();

        if (step.Governing is { } governing)
            parts.Add($"governing {governing.Outcome} (utilisation {governing.Utilisation})");

        return string.Join("; ", parts);
    }

    /// <summary>Single-line text for the header.</summary>
    private static string Line(string text) => text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();

    /// <summary>A table cell: pipes escaped, line breaks as a break tag, the ends trimmed.</summary>
    private static string Cell(string? text) => (text ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r\n", "<br>", StringComparison.Ordinal)
        .Replace("\n", "<br>", StringComparison.Ordinal)
        .Replace("\r", "<br>", StringComparison.Ordinal)
        .Trim();
}

using System.Text;

namespace Tempest.Calc.Library.ManualTesting;

/// <summary>
/// A run as the markdown results sheet of the runbook's section 11: the same six columns (Step, Item, Result, Value shown, Expected, Note),
/// one row per script step in script order, under a header that carries what the owner sends back with it (the build commit, the tester and the dates).
/// Text from the tester is escaped so that it cannot break the table or be read as markup by a viewer that renders markdown with HTML: <c>&amp;</c>,
/// <c>&lt;</c> and <c>&gt;</c> become <c>&amp;amp;</c>, <c>&amp;lt;</c> and <c>&amp;gt;</c>, a pipe becomes <c>\|</c> and a line break becomes <c>&lt;br&gt;</c>
/// (the one tag the sheet writes itself).
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

        sb.Append("\nSend the sheet back with the commit above. Faults already in `")
            .Append(script.Release.Length > 0 ? $"v{script.Release}-known-defects.md" : "the known-defects list")
            .Append("` need only a tick.\n");
        return sb.ToString();
    }

    /// <summary>
    /// Writes <paramref name="sheet"/> to <paramref name="path"/> as UTF-8 without a byte-order mark, by writing a temporary file in the same folder
    /// and moving it over the target, so a failure part-way leaves the earlier file (or none) rather than half a sheet. The temporary file is removed
    /// when the write fails.
    /// </summary>
    /// <exception cref="IOException">The folder or file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or file is not writable.</exception>
    public static async Task WriteFileAsync(string path, string sheet, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(sheet);

        var full = Path.GetFullPath(path);
        var temporary = full + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";

        try
        {
            await File.WriteAllTextAsync(temporary, sheet, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, full, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The write failure is the one to report; a temporary file that cannot be removed is left for the next clean-up of that folder.
            }

            throw;
        }
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

    /// <summary>Single-line text for the header, with <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> encoded.</summary>
    private static string Line(string text) => Encode(text).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();

    /// <summary>HTML-encodes the three characters a markdown viewer could read as markup (the ampersand first, so the encoding is not encoded again).</summary>
    private static string Encode(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>A table cell: markup characters encoded, pipes escaped, line breaks as a break tag, the ends trimmed.</summary>
    private static string Cell(string? text) => Encode(text ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r\n", "<br>", StringComparison.Ordinal)
        .Replace("\n", "<br>", StringComparison.Ordinal)
        .Replace("\r", "<br>", StringComparison.Ordinal)
        .Trim();
}

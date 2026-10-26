using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>
/// The embedded guided test script must match the docs it was generated from (the worked examples and the runbook). The docs folder is not in an
/// installed build, so the app carries a copy; this fails when a worked example, its expected table or the runbook has moved on. The fix is one
/// command: <c>python3 scripts/generate-guided-test-script.py</c>. The checks below re-read the docs with their own small parsers (not the
/// generator's), so they would catch a generator that drifted too.
/// </summary>
public sealed partial class GuidedTestScriptStalenessTests
{
    private const string Fix = " Run: python3 scripts/generate-guided-test-script.py";

    private static readonly string Root = GuidedTestHelpers.RepositoryRoot;

    private static IReadOnlyList<(string Discipline, string Template, string Example, string Folder)> Examples() =>
    [
        .. new[] { "mechanical", "structural", "building-services", "electrical", "fluid-power" }
            .SelectMany(discipline => Directory.GetDirectories(Path.Combine(Root, "docs", "worked-examples", discipline))
                .SelectMany(template => Directory.GetDirectories(template)
                    .Where(example => File.Exists(Path.Combine(example, "README.md")))
                    .Select(example => (discipline, Path.GetFileName(template), Path.GetFileName(example), example)))),
    ];

    [Fact]
    public void The_Script_Has_Exactly_The_Worked_Examples_The_Docs_Have()
    {
        var onDisk = Examples().Select(e => $"example:{e.Template}/{e.Example}").Order(StringComparer.Ordinal).ToList();
        var inScript = GuidedTestScript.Current.Steps.Where(s => s.Kind == GuidedStepKind.Example).Select(s => s.Id).Order(StringComparer.Ordinal).ToList();
        Assert.True(onDisk.SequenceEqual(inScript), "The worked examples on disk and in the guided test script differ." + Fix + $" Missing from the script: [{string.Join(", ", onDisk.Except(inScript))}]. Not on disk: [{string.Join(", ", inScript.Except(onDisk))}].");
    }

    [Fact]
    public void Every_Example_Step_Carries_What_The_Docs_Say_Now()
    {
        var script = GuidedTestScript.Current;
        var stale = new List<string>();

        foreach (var (discipline, template, example, folder) in Examples())
        {
            var step = script.Find($"example:{template}/{example}");

            if (step is null)
                continue;

            var readme = Lf(File.ReadAllText(Path.Combine(folder, "README.md")));
            var sheet = Lf(File.ReadAllText(Path.Combine(folder, "sheet.txt")));
            var problems = new List<string>();

            if (step.Discipline != discipline)
                problems.Add("discipline");

            if (!JsonNode.DeepEquals(JsonNode.Parse(step.InputsJson!), JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "inputs.json")))))
                problems.Add("inputs.json");

            var heading = TitleSuffix().Replace(readme.Split('\n')[0].TrimStart('#', ' ').Trim(), string.Empty);

            if (step.Title != heading)
                problems.Add("title");

            if (!ExpectedOf(readme).SequenceEqual(step.Expected!.Select(e => (e.Symbol, e.Value, e.Unit, e.Computed ?? string.Empty))))
                problems.Add("expected outputs");

            (string, string, string)? shippedGoverning = step.Governing is { } g ? (g.Name, g.Outcome, g.Utilisation) : null;

            if (GoverningOf(readme) != shippedGoverning)
                problems.Add("governing check");

            if (TemplateVersionOf(sheet) != step.TemplateVersion)
                problems.Add("template version");

            if (!InputsOf(sheet).SequenceEqual(step.Inputs!.Select(i => (i.Symbol, i.Description, i.Value, i.Unit))))
                problems.Add("inputs table");

            if (problems.Count > 0)
                stale.Add($"{step.Id} ({string.Join(", ", problems)})");
        }

        Assert.True(stale.Count == 0, $"{stale.Count} example(s) in the guided test script are stale: {string.Join("; ", stale.Take(10))}." + Fix);
    }

    [Fact]
    public void The_Manual_Steps_Are_The_Runbooks_Sections_As_They_Read_Now()
    {
        var runbook = Lf(File.ReadAllText(Path.Combine(Root, "docs", "testing", "v0.26.0-runbook.md")));
        var plain = Plain(runbook);
        var script = GuidedTestScript.Current;

        Assert.True(Count(script, GuidedStepKind.Smoke) == Items(runbook, 4, @"^\d+\. ").Count, "The smoke test steps differ from runbook section 4." + Fix);
        Assert.True(Count(script, GuidedStepKind.Lifting) == Items(runbook, 8, @"^\d+\. ").Count, "The lifting steps differ from runbook section 8." + Fix);
        Assert.True(Count(script, GuidedStepKind.Legacy) == Items(runbook, 9, @"^- ").Count, "The legacy steps differ from runbook section 9." + Fix);
        Assert.True(Count(script, GuidedStepKind.Import) == Items(runbook, 7, @"^- ").Count, "The import steps differ from runbook section 7." + Fix);
        Assert.True(Count(script, GuidedStepKind.Pack) == Items(runbook, 6, @"^- ").Count, "The pack steps differ from runbook section 6." + Fix);

        var missing = new List<string>();

        foreach (var step in script.Steps.Where(s => s.Kind != GuidedStepKind.Example))
        {
            // A generated line (a pack step's numbered copy, its README path) is not in the runbook as such; every other line is.
            foreach (var line in (step.Instructions ?? []).Where(l => !Regex.IsMatch(l, @"^(\d+\. |- |Pack README: )") && !l.StartsWith('<')))
            {
                if (!plain.Contains(line, StringComparison.Ordinal))
                    missing.Add($"{step.Id}: {Clip(line)}");
            }

            // A pack step's pass criterion is written by the generator (the runbook states it as the four numbered points); the others are the runbook's own words.
            if (step.Kind != GuidedStepKind.Pack && step.PassWhen is { } pass && !plain.Contains(pass[1..], StringComparison.Ordinal))
                missing.Add($"{step.Id}: pass criterion '{Clip(pass)}'");
        }

        Assert.True(missing.Count == 0, $"The guided test script has manual text the runbook no longer has: {string.Join("; ", missing.Take(8))}." + Fix);
    }

    [Fact]
    public void The_Generator_Reproduces_The_Shipped_Script_Byte_For_Byte_When_Python_Is_Available()
    {
        var python = new[] { "python3", "python" }.FirstOrDefault(Runs);

        if (python is null)
            return; // The checks above stand in; CI images carry Python (scripts/check-licences.py needs it).

        var start = new ProcessStartInfo(python) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Path.Combine(Root, "scripts", "generate-guided-test-script.py"));
        start.ArgumentList.Add("--check");
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "The generator did not finish in 60 seconds.");
        Assert.True(process.ExitCode == 0, $"The guided test script is stale: {error.Trim()}");
    }

    private static bool Runs(string python)
    {
        try
        {
            var start = new ProcessStartInfo(python, "-I -c \"import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)\"") { RedirectStandardOutput = true, RedirectStandardError = true };
            using var probe = Process.Start(start);
            return probe is not null && probe.WaitForExit(20_000) && probe.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static int Count(GuidedTestScript script, GuidedStepKind kind) => script.Steps.Count(s => s.Kind == kind);

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Clip(string text) => text.Length <= 60 ? text : text[..60] + "…";

    private static string Plain(string markdown) => LinkSyntax().Replace(markdown, "$1").Replace("**", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal);

    private static List<string> SectionLines(string runbook, int number)
    {
        var lines = runbook.Split('\n').ToList();
        var start = lines.FindIndex(l => l.StartsWith($"## {number}. ", StringComparison.Ordinal));
        Assert.True(start >= 0, $"Runbook section {number} is missing.");
        var end = lines.FindIndex(start + 1, l => l.StartsWith("## ", StringComparison.Ordinal) || l.Trim() == "---");
        return lines.GetRange(start + 1, (end < 0 ? lines.Count : end) - start - 1);
    }

    /// <summary>The top-level list items of a runbook section (code blocks skipped).</summary>
    private static List<string> Items(string runbook, int section, string marker)
    {
        var items = new List<string>();
        var fenced = false;

        foreach (var line in SectionLines(runbook, section))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (!fenced && Regex.IsMatch(line, marker))
                items.Add(line);
        }

        // Section 6 holds a numbered list as well as the pack bullets; only the bullets name the chains.
        return items;
    }

    private static List<(string Symbol, string Value, string Unit, string Computed)> ExpectedOf(string readme)
    {
        var lines = readme.Split('\n');
        var header = Array.FindIndex(lines, l => l.StartsWith("| Output | Expected", StringComparison.Ordinal));
        var rows = new List<(string, string, string, string)>();

        for (var i = header + 2; header >= 0 && i < lines.Length && lines[i].StartsWith('|'); i++)
        {
            var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            var match = Regex.Match(cells[1], @"^(\S+)\s*(.*)$");
            rows.Add((cells[0].Trim('`'), match.Groups[1].Value, match.Groups[2].Value.Trim(), Regex.Match(cells[2], @"^(\S+)").Groups[1].Value));
        }

        return rows;
    }

    private static (string, string, string)? GoverningOf(string readme)
    {
        var line = readme.Split('\n').FirstOrDefault(l => l.StartsWith("Governing check:", StringComparison.Ordinal));
        var match = line is null ? null : Regex.Match(line, @"^Governing check: (.*?) — (PASS|FAIL) \(utilisation ([^)]+)\)");
        return match is { Success: true } ? (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value) : null;
    }

    private static string? TemplateVersionOf(string sheet) => Regex.Match(sheet, @"Template: (\S+)@(\S+)") is { Success: true } m ? m.Groups[2].Value : null;

    private static List<(string, string, string, string)> InputsOf(string sheet)
    {
        var lines = sheet.Split('\n');
        var start = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^2\. "));
        var rows = new List<(string, string, string, string)>();

        for (var i = start + 2; start >= 0 && i < lines.Length && !Regex.IsMatch(lines[i], @"^\d+\. [A-Z]"); i++)
        {
            var parts = lines[i].Trim().Split(" | ").Select(p => p.Trim()).ToArray();

            if (lines[i].Trim().Length > 0 && parts.Length >= 4)
                rows.Add((parts[0], parts[1], parts[2], parts[3]));
        }

        return rows;
    }

    [GeneratedRegex(@"\s*\(`[^`]+`\)\s*$")]
    private static partial Regex TitleSuffix();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex LinkSyntax();
}

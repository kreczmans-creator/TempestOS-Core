using System.Diagnostics;
using System.Text.Json;
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
        var runbook = Lf(File.ReadAllText(RunbookPath(Root)));
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

    /// <summary>Skipped, visibly, where there is no Python (see <see cref="PythonFactAttribute"/>); the checks above stand in for it there.</summary>
    [PythonFact]
    public void The_Generator_Reproduces_The_Shipped_Script_Byte_For_Byte()
    {
        var (exit, _, error) = RunGenerator(Root, "--check");
        Assert.True(exit == 0, $"The guided test script is stale: {error.Trim()}");
    }

    [PythonFact]
    public void The_Generator_Stops_On_A_Governing_Check_Line_It_Cannot_Parse_And_Accepts_None()
    {
        using var copy = GeneratorCopy("0.26.0");
        var readme = Path.Combine(copy.Path, "docs", "worked-examples", "mechanical", "t.x", "e1", "README.md");

        File.WriteAllText(readme, ExampleReadme("Governing check: Bolt shear — passes (utilisation 0.5)"));
        var (exit, _, error) = RunGenerator(copy.Path);
        Assert.NotEqual(0, exit);
        Assert.Contains("cannot parse the 'Governing check:' line", error, StringComparison.Ordinal);
        Assert.Contains("t.x/e1", error, StringComparison.Ordinal);

        File.WriteAllText(readme, ExampleReadme(string.Empty));
        (exit, _, error) = RunGenerator(copy.Path);
        Assert.NotEqual(0, exit);
        Assert.Contains("no 'Governing check:' line", error, StringComparison.Ordinal);

        File.WriteAllText(readme, ExampleReadme("Governing check: none. Files: `inputs.json`."));
        (exit, _, error) = RunGenerator(copy.Path);
        Assert.True(exit == 0, error);
        Assert.Equal(JsonValueKind.Null, ScriptStep(copy.Path, "example:t.x/e1").GetProperty("governing").ValueKind);

        File.WriteAllText(readme, ExampleReadme("Governing check: Bolt shear — PASS (utilisation 0.5). Files: `inputs.json`."));
        (exit, _, error) = RunGenerator(copy.Path);
        Assert.True(exit == 0, error);
        Assert.Equal("Bolt shear", ScriptStep(copy.Path, "example:t.x/e1").GetProperty("governing").GetProperty("name").GetString());
    }

    [PythonFact]
    public void The_Generator_Reads_The_Runbook_Of_The_Release_In_The_Version_File_Not_A_Fixed_One()
    {
        using var copy = GeneratorCopy("0.27.0");

        var (exit, output, error) = RunGenerator(copy.Path);
        Assert.True(exit == 0, error);
        Assert.Contains("version 0.27.0+", output, StringComparison.Ordinal);
        Assert.Contains("v0.27.0-runbook.md", File.ReadAllText(Path.Combine(copy.Path, "src", "Tempest.Calc", "Tempest.Calc.Library", "ManualTesting", "guided-test-script.json")), StringComparison.Ordinal);

        File.Move(Path.Combine(copy.Path, "docs", "testing", "v0.27.0-runbook.md"), Path.Combine(copy.Path, "docs", "testing", "v0.28.0-runbook.md"));
        (exit, _, error) = RunGenerator(copy.Path);
        Assert.NotEqual(0, exit);
        Assert.Contains("v0.27.0-runbook.md", error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Test_Reads_The_Runbook_Named_By_The_Version_File()
    {
        var release = File.ReadAllText(Path.Combine(Root, "VERSION")).Trim();
        Assert.EndsWith($"v{release}-runbook.md", RunbookPath(Root), StringComparison.Ordinal);
        Assert.True(File.Exists(RunbookPath(Root)), "The runbook named by the VERSION file is missing.");
    }

    private static string RunbookPath(string root) => Path.Combine(root, "docs", "testing", $"v{File.ReadAllText(Path.Combine(root, "VERSION")).Trim()}-runbook.md");

    private static (int ExitCode, string Output, string Error) RunGenerator(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo(PythonFactAttribute.Command!) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Path.Combine(root, "scripts", "generate-guided-test-script.py"));

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        Assert.True(process.WaitForExit(60_000), "The generator did not finish in 60 seconds.");
        return (process.ExitCode, output.Result, error.Result);
    }

    private static JsonElement ScriptStep(string root, string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "Tempest.Calc", "Tempest.Calc.Library", "ManualTesting", "guided-test-script.json")));
        return document.RootElement.GetProperty("steps").EnumerateArray().Single(s => s.GetProperty("id").GetString() == id).Clone();
    }

    private static string ExampleReadme(string governingLine) =>
        "# T example (`t.x`)\n\n| Output | Expected | Computed |\n|---|---|---|\n| `P` | 3.93 kN | 3.93 kN |\n\n" + governingLine + "\n";

    /// <summary>A scratch copy of what the generator reads (the script, the version, the runbook, the pack READMEs) with one worked example of its own.</summary>
    private static TempRepository GeneratorCopy(string release)
    {
        var copy = new TempRepository();
        Directory.CreateDirectory(Path.Combine(copy.Path, "scripts"));
        File.Copy(Path.Combine(Root, "scripts", "generate-guided-test-script.py"), Path.Combine(copy.Path, "scripts", "generate-guided-test-script.py"));
        File.WriteAllText(Path.Combine(copy.Path, "VERSION"), release + "\n");
        Directory.CreateDirectory(Path.Combine(copy.Path, "docs", "testing"));
        File.Copy(RunbookPath(Root), Path.Combine(copy.Path, "docs", "testing", $"v{release}-runbook.md"));

        foreach (var pack in Directory.GetDirectories(Path.Combine(Root, "docs", "worked-examples", "packs")))
        {
            var target = Path.Combine(copy.Path, "docs", "worked-examples", "packs", Path.GetFileName(pack));
            Directory.CreateDirectory(target);
            File.Copy(Path.Combine(pack, "README.md"), Path.Combine(target, "README.md"));
        }

        var example = Path.Combine(copy.Path, "docs", "worked-examples", "mechanical", "t.x", "e1");
        Directory.CreateDirectory(example);
        File.WriteAllText(Path.Combine(example, "sheet.txt"), "Template: t.x@1.0.0\n");
        File.WriteAllText(Path.Combine(example, "inputs.json"), "[]");
        File.WriteAllText(Path.Combine(example, "README.md"), ExampleReadme("Governing check: none."));
        return copy;
    }

    private sealed class TempRepository : IDisposable
    {
        public TempRepository() => Directory.CreateDirectory(Path);

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tempest-guided-generator-" + Guid.NewGuid().ToString("N")[..10]);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A scratch folder left behind in the temp folder is harmless.
            }
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
        Assert.True(line is not null, "A worked example README has no 'Governing check:' line.");

        if (Regex.IsMatch(line, @"^Governing check: none\b"))
            return null;

        // Neither a check nor "none": the README is not read as "none" by default (the app would then insist the form shows no check).
        var match = Regex.Match(line, @"^Governing check: (.*?) — (PASS|FAIL) \(utilisation ([^)]+)\)");
        Assert.True(match.Success, $"A worked example README has a 'Governing check:' line the guided test cannot read: {line}");
        return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
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

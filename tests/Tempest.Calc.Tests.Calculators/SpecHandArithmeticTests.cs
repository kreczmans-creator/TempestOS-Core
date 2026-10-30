using System.Text;
using System.Text.RegularExpressions;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// ADR-0167 §3: every template spec carries the hand arithmetic of one worked
/// example, as the legacy teaching-register specs did. The block is generated
/// from the stored worked-example sheet (so it can never disagree with the
/// engine) and kept between markers; run with
/// <see cref="WorkedExamples.WriteVariable"/>=1 to write it, otherwise a drift fails.
/// </summary>
public sealed partial class SpecHandArithmeticTests
{
    public const string Begin = "<!-- hand-arithmetic:begin";
    public const string End = "<!-- hand-arithmetic:end -->";
    public const int MaxSteps = 14;

    public static TheoryData<string> TemplateIds => [.. WorkedExamples.All.Select(e => e.TemplateId).Distinct(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Spec_Carries_The_Hand_Arithmetic_Of_Its_First_Worked_Example(string templateId)
    {
        var example = WorkedExamples.All.First(e => e.TemplateId == templateId);
        var folder = WorkedExamples.Folder(example);
        var sheetPath = Path.Combine(folder, "sheet.txt");
        Assert.True(File.Exists(sheetPath), $"{sheetPath} is missing; run the worked examples with {WorkedExamples.WriteVariable}=1 first.");

        var discipline = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(folder))!);
        var specPath = Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs", discipline, templateId + ".md");
        Assert.True(File.Exists(specPath), $"{specPath} is missing.");

        var block = Block(example, discipline, File.ReadAllText(sheetPath).Replace("\r\n", "\n", StringComparison.Ordinal));
        var spec = File.ReadAllText(specPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = spec.IndexOf(Begin, StringComparison.Ordinal);
        var end = spec.IndexOf(End, StringComparison.Ordinal);
        var current = start >= 0 && end > start ? spec[start..(end + End.Length)] : null;

        if (Environment.GetEnvironmentVariable(WorkedExamples.WriteVariable) == "1")
        {
            var updated = current is null ? spec.TrimEnd('\n') + "\n\n" + block + "\n" : spec.Replace(current, block, StringComparison.Ordinal);
            File.WriteAllText(specPath, updated);
            return;
        }

        Assert.True(current is not null, $"{specPath} has no hand-arithmetic block; run with {WorkedExamples.WriteVariable}=1.");
        Assert.Equal(block, current);
    }

    [Fact]
    public void Every_Spec_Explains_Itself_In_Plain_Terms()
    {
        // The spec pages of the templates and workflows, in the folders under docs/calcs. The pages that sit directly in docs/calcs (the sheet
        // standard, the glossary and its intro, the generated work list) are not specs of one calculation.
        foreach (var path in Directory.GetFiles(Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs"), "*.md", SearchOption.AllDirectories).Where(p => Path.GetDirectoryName(p) != Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs")))
            Assert.Contains("**In plain terms.**", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>The block: title, link to the sheet, the first steps as "symbol = substitution = result", and the governing check.</summary>
    public static string Block(WorkedExample example, string discipline, string sheet)
    {
        var steps = Steps(sheet);
        var sb = new StringBuilder();
        sb.Append(Begin).Append(" generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->\n");
        sb.Append("**Worked example, by hand.** ").Append(example.Title).Append(" ([sheet](../../worked-examples/").Append(discipline).Append('/').Append(example.TemplateId).Append('/').Append(example.Slug).Append("/sheet.txt)).\n\n");
        sb.Append("```text\n");

        foreach (var step in steps.Take(MaxSteps))
            sb.Append(step).Append('\n');

        if (steps.Count > MaxSteps)
            sb.Append("… ").Append(steps.Count - MaxSteps).Append(" more step(s) on the sheet\n");

        if (Governing(sheet) is { } governing)
            sb.Append("Governing: ").Append(governing).Append('\n');

        sb.Append("```\n");
        sb.Append(End);
        return sb.ToString();
    }

    private static List<string> Steps(string sheet)
    {
        var lines = sheet.Split('\n');
        var start = Array.FindIndex(lines, l => l == "4. Working");
        var stop = Array.FindIndex(lines, l => l == "5. Results");
        var steps = new List<string>();

        if (start < 0 || stop < start)
            return steps;

        for (var i = start + 1; i < stop; i++)
        {
            var header = StepHeader().Match(lines[i]);

            if (!header.Success)
                continue;

            var symbol = header.Groups["symbol"].Value;
            var formula = header.Groups["formula"].Value.Trim();
            var equals = new List<string>();
            var j = i + 1;

            while (j < stop && lines[j].StartsWith("      = ", StringComparison.Ordinal))
            {
                equals.Add(lines[j]["      = ".Length..].Trim());
                j++;
            }

            var result = equals.Count > 0 ? equals[^1] : formula;
            var working = equals.Count > 1 ? equals[0] : formula;
            steps.Add(string.Equals(working, result, StringComparison.Ordinal) ? $"{symbol} = {result}" : $"{symbol} = {working} = {result}");
            i = j - 1;
        }

        return steps;
    }

    private static string? Governing(string sheet)
    {
        var lines = sheet.Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("6. Checks", StringComparison.Ordinal));
        return start >= 0 && start + 1 < lines.Length && (lines[start + 1].StartsWith("  PASS", StringComparison.Ordinal) || lines[start + 1].StartsWith("  FAIL", StringComparison.Ordinal))
            ? lines[start + 1].Trim()
            : null;
    }

    [GeneratedRegex(@"^  \[(?<n>\d+)\] (?<symbol>\S+) = (?<formula>.+?)(    « .*)?$")]
    private static partial Regex StepHeader();
}

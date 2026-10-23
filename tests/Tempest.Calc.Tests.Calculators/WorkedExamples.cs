using System.Globalization;
using System.Text;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>One worked example (§9): a published or analytic case with inputs, provenance, expected outputs; written under docs/worked-examples and rendered to PDF by the Desktop.</summary>
public sealed record WorkedExample(string TemplateId, string Slug, string Title, CalcReferenceSource Source, CalcInputSet Inputs, IReadOnlyList<CalcExpectedValue> Expected, string Notes, CalcReferenceStatus Status = CalcReferenceStatus.Confirmed);

public static class WorkedExamples
{
    public const string WriteVariable = "TEMPEST_WRITE_WORKED_EXAMPLES";

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        .. Mechanical.MechanicalWorkedExamples.All,
        .. Mechanical.MechanicalPortWorkedExamples.All,
        .. Structural.StructuralWorkedExamples.All,
        .. BuildingServices.BuildingServicesWorkedExamples.All,
        .. Electrical.ElectricalWorkedExamples.All,
    ];

    public static TheoryData<string> Keys => [.. All.Select(e => e.TemplateId + "/" + e.Slug)];

    public static WorkedExample Get(string key) => All.Single(e => e.TemplateId + "/" + e.Slug == key);

    public static string RepositoryRoot { get; } = FindRoot();

    public static string Folder(WorkedExample e)
    {
        var discipline = AllCalculators.Get(e.TemplateId).Template.Discipline switch
        {
            CalcDiscipline.BuildingServices => "building-services",
            var d => d.ToString().ToLowerInvariant(),
        };

        return Path.Combine(RepositoryRoot, "docs", "worked-examples", discipline, e.TemplateId, e.Slug);
    }

    public static string Readme(WorkedExample e, CalcRunResult result, KnownAnswerOutcome outcome)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(e.Title).Append(" (`").Append(e.TemplateId).Append("`)\n\n");
        sb.Append("**Source.** ").Append(e.Source.Title).Append(e.Source.Edition is { Length: > 0 } ? $", {e.Source.Edition}" : string.Empty).Append(", ").Append(e.Source.Location).Append(" — ").Append(e.Status).Append(".\n\n");
        sb.Append(e.Notes).Append("\n\n");
        sb.Append("| Input | Value | Source |\n|---|---|---|\n");

        foreach (var v in e.Inputs.Values.OrderBy(v => v.Symbol, StringComparer.Ordinal))
            sb.Append("| `").Append(v.Symbol).Append("` | ").Append(v.ValueText).Append(v.Unit is { Length: > 0 } ? " " + v.Unit : string.Empty).Append(" | ").Append(v.Provenance?.ToString() ?? "manual").Append(" |\n");

        sb.Append("\n| Output | Expected | Computed | Result |\n|---|---|---|---|\n");

        foreach (var c in outcome.Checks)
            sb.Append("| `").Append(c.Symbol).Append("` | ").Append(c.Expected.ToString("G6", CultureInfo.InvariantCulture)).Append(' ').Append(c.Unit).Append(" | ").Append(c.Actual.ToString("G6", CultureInfo.InvariantCulture)).Append(' ').Append(c.Unit).Append(" | ").Append(c.Passed ? "PASS" : "FAIL").Append(" |\n");

        sb.Append("\nGoverning check: ").Append(result.Governing is { } g ? $"{g.Name} — {(g.Passed ? "PASS" : "FAIL")} (utilisation {CalcSheetTextRendererUtil.U(g.Utilisation)})" : "none").Append(". Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).\n");
        return sb.ToString();
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VERSION")))
            dir = dir.Parent;

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}

internal static class CalcSheetTextRendererUtil
{
    public static string U(double u) => double.IsInfinity(u) ? "—" : u.ToString("0.000", CultureInfo.InvariantCulture);
}

public sealed class WorkedExampleTests
{
    [Theory]
    [MemberData(nameof(WorkedExamples.Keys), MemberType = typeof(WorkedExamples))]
    [Trait("Verification", "WorkedExample")]
    public void Worked_Example_Runs_And_Matches_Its_Expectations(string key)
    {
        var example = WorkedExamples.Get(key);
        var calculator = AllCalculators.Get(example.TemplateId);
        var result = CalcRunner.Run(calculator, example.Inputs);
        Assert.False(result.IsBlocked, string.Join("; ", result.Blocks.Select(b => b.Message)));
        Assert.Empty(CalcInputValidator.ValidateProvenanceForIssue(calculator.Template, example.Inputs));

        var testCase = new CalcTestCase(example.Slug, example.Title, example.Source, [.. example.Inputs.Values], example.Expected, example.Status);
        var outcome = KnownAnswerRunner.RunCase(calculator, testCase);
        Assert.True(outcome.Passed, string.Join("; ", outcome.Checks.Where(c => !c.Passed).Select(c => $"{c.Symbol}: expected {c.Expected} {c.Unit}, got {c.Actual:G6} {c.Problem}")));

        var folder = WorkedExamples.Folder(example);
        var json = CalcInputJson.Write(example.Inputs);
        var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(calculator.Template, example.Inputs, result,
            new CalcSheetHeader("Worked example", example.Slug, calculator.Template.Title, "A", CalcSheetStatus.Draft, "TempestOS", null, null, null, null),
            GoldenFiles.FixedFooter(calculator.Template, KnownAnswerRunner.Run(calculator).Passed, calculator.Template.TestCases.Count)));

        if (Environment.GetEnvironmentVariable(WorkedExamples.WriteVariable) == "1")
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "inputs.json"), json);
            File.WriteAllText(Path.Combine(folder, "README.md"), WorkedExamples.Readme(example, result, outcome));
            File.WriteAllText(Path.Combine(folder, "sheet.txt"), sheet);
            return;
        }

        var stored = Path.Combine(folder, "inputs.json");
        Assert.True(File.Exists(stored), $"{stored} is missing; run with {WorkedExamples.WriteVariable}=1 to write it.");
        var (reloaded, notices) = CalcInputJson.Read(File.ReadAllText(stored));
        Assert.Empty(notices);
        Assert.False(CalcRunner.Run(calculator, reloaded).IsBlocked);
        Assert.Equal(json.Replace("\r\n", "\n", StringComparison.Ordinal), File.ReadAllText(stored).Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}

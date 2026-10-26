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
        .. Structural.StructuralPortWorkedExamples.All,
        .. BuildingServices.BuildingServicesWorkedExamples.All,
        .. BuildingServices.BuildingServicesPortWorkedExamples.All,
        .. Electrical.ElectricalWorkedExamples.All,
        .. FluidPower.FluidPowerWorkedExamples.All,
        .. Electrical.ElectricalPortWorkedExamples.All,
    ];

    public static TheoryData<string> Keys => [.. All.Select(e => e.TemplateId + "/" + e.Slug)];

    public static WorkedExample Get(string key) => All.Single(e => e.TemplateId + "/" + e.Slug == key);

    public static string RepositoryRoot { get; } = FindRoot();

    public static string Folder(WorkedExample e)
    {
        var discipline = AllCalculators.Get(e.TemplateId).Template.Discipline switch
        {
            CalcDiscipline.BuildingServices => "building-services",
            CalcDiscipline.FluidPower => "fluid-power",
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

        sb.Append("\nGoverning check: ").Append(result.Governing is { } g ? $"{g.Name} — {(g.Passed ? "PASS" : "FAIL")}{(g.HasUtilisation ? $" (utilisation {g.UtilisationText})" : string.Empty)}" : "none").Append(". Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).\n");
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

public sealed class WorkedExampleTests
{
    /// <summary>
    /// C-2 verification F5: the README wrote the governing utilisation with a formatter of its own ("1.044", "0.000" for a yes/no check) while the sheet, the form and the banner print
    /// two decimals rounded by the verdict ("1.05") and no utilisation for a yes/no check. The README's line is the sheet's: the guided test reads it as what the form must show.
    /// </summary>
    [Theory]
    [MemberData(nameof(WorkedExamples.Keys), MemberType = typeof(WorkedExamples))]
    [Trait("Verification", "WorkedExample")]
    public void The_Readme_Governing_Line_Is_The_Line_The_Sheet_Prints(string key)
    {
        var example = WorkedExamples.Get(key);
        var calculator = AllCalculators.Get(example.TemplateId);
        var result = CalcRunner.Run(calculator, example.Inputs);
        var outcome = KnownAnswerRunner.RunCase(calculator, new CalcTestCase(example.Slug, example.Title, example.Source, [.. example.Inputs.Values], example.Expected, example.Status));
        var line = WorkedExamples.Readme(example, result, outcome).Split('\n').Single(l => l.StartsWith("Governing check:", StringComparison.Ordinal));

        if (result.Governing is not { } governing)
        {
            Assert.StartsWith("Governing check: none.", line, StringComparison.Ordinal);
            return;
        }

        var expected = $"Governing check: {governing.Name} — {(governing.Passed ? "PASS" : "FAIL")}" + (governing.HasUtilisation ? $" (utilisation {governing.UtilisationText})" : string.Empty) + ".";
        Assert.StartsWith(expected, line, StringComparison.Ordinal);
        Assert.Equal(governing.HasUtilisation, line.Contains("(utilisation", StringComparison.Ordinal));

        if (governing.HasUtilisation)
            Assert.Matches(@"\(utilisation (?:\d+\.\d\d|0\.\d*[1-9]\d*|—)\)", line);
    }

    [Fact]
    public void A_Yes_No_Governing_Check_Has_No_Utilisation_In_Its_Readme_And_A_Failing_Ratio_Check_Is_Rounded_Up()
    {
        var fit = WorkedExamples.Get("mech.iso286-fit/h7-g6-25mm");
        var fitCalculator = AllCalculators.Get(fit.TemplateId);
        var fitResult = CalcRunner.Run(fitCalculator, fit.Inputs);
        Assert.False(fitResult.Governing!.HasUtilisation);
        var fitLine = WorkedExamples.Readme(fit, fitResult, KnownAnswerRunner.RunCase(fitCalculator, new CalcTestCase(fit.Slug, fit.Title, fit.Source, [.. fit.Inputs.Values], fit.Expected, fit.Status))).Split('\n').Single(l => l.StartsWith("Governing check:", StringComparison.Ordinal));
        Assert.DoesNotContain("utilisation", fitLine.Split(" Files:")[0], StringComparison.Ordinal);

        // A failing check prints its utilisation rounded up, as the sheet does: 1.0435 is 1.05 (the README printed 1.044).
        var member = WorkedExamples.Get("struct.en1993-member/uc-column-axial-moment");
        var memberCalculator = AllCalculators.Get(member.TemplateId);
        var memberResult = CalcRunner.Run(memberCalculator, member.Inputs);
        Assert.False(memberResult.Governing!.Passed);
        var memberLine = WorkedExamples.Readme(member, memberResult, KnownAnswerRunner.RunCase(memberCalculator, new CalcTestCase(member.Slug, member.Title, member.Source, [.. member.Inputs.Values], member.Expected, member.Status))).Split('\n').Single(l => l.StartsWith("Governing check:", StringComparison.Ordinal));
        Assert.Contains($"(utilisation {memberResult.Governing.UtilisationText})", memberLine, StringComparison.Ordinal);
        Assert.Equal(Math.Ceiling(memberResult.Governing.Utilisation * 100) / 100, double.Parse(memberResult.Governing.UtilisationText, CultureInfo.InvariantCulture), 9);
    }

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

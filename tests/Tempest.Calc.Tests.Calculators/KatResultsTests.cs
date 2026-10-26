using System.Text;
using System.Text.Json;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// Addendum C, C-4: <c>docs/testing/kat-results.json</c> is the small machine-readable summary of the last real run of every worksheet
/// calculator's known-answer cases (KATs) and worked examples. The verification dashboard reads it (through the embedded snapshot, see
/// <c>scripts/generate-verification-snapshot.py</c>) to show pass or fail per template. This test is its only writer: it runs every KAT
/// and every worked example with <see cref="KnownAnswerRunner"/> and compares the result with the committed file, so a calculator that
/// gains, loses or fails a case, or changes version, fails here until the file is regenerated. Regenerate with
/// <c>TEMPEST_WRITE_KAT_RESULTS=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter KatResultsTests</c> and commit the diff.
/// The file holds no date and no machine name, so a run that changes nothing writes the same bytes.
/// </summary>
public sealed class KatResultsTests
{
    public const string WriteVariable = "TEMPEST_WRITE_KAT_RESULTS";

    public const int SchemaVersion = 1;

    /// <summary>The file's path under the repository root.</summary>
    public static string FilePath { get; } = Path.Combine(WorkedExamples.RepositoryRoot, "docs", "testing", "kat-results.json");

    private sealed record TemplateResult(string Id, string Version, int Kats, int KatsPassed, IReadOnlyList<string> FailedKats, int Examples, int ExamplesPassed, IReadOnlyList<string> FailedExamples);

    private static IReadOnlyList<TemplateResult> Run()
    {
        var results = new List<TemplateResult>();

        foreach (var calculator in AllCalculators.All.OrderBy(c => c.Template.Id, StringComparer.Ordinal))
        {
            var template = calculator.Template;
            var kats = KnownAnswerRunner.Run(calculator);
            var examples = WorkedExamples.All.Where(e => e.TemplateId == template.Id).OrderBy(e => e.Slug, StringComparer.Ordinal).ToList();
            var failedExamples = new List<string>();

            foreach (var example in examples)
            {
                var testCase = new CalcTestCase(example.Slug, example.Title, example.Source, [.. example.Inputs.Values], example.Expected, example.Status);

                if (!KnownAnswerRunner.RunCase(calculator, testCase).Passed)
                    failedExamples.Add(example.Slug);
            }

            results.Add(new TemplateResult(
                template.Id, template.Version, kats.Total, kats.Passed,
                [.. kats.Outcomes.Where(o => !o.Passed).Select(o => o.Case.Id).OrderBy(id => id, StringComparer.Ordinal)],
                examples.Count, examples.Count - failedExamples.Count, failedExamples));
        }

        return results;
    }

    /// <summary>The file text for <paramref name="results"/>: indented, sorted, LF line ends, one trailing newline.</summary>
    private static string Write(IReadOnlyList<TemplateResult> results)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", SchemaVersion);
            json.WriteString("writtenBy", "tests/Tempest.Calc.Tests.Calculators/KatResultsTests.cs (set " + WriteVariable + "=1 to rewrite)");
            json.WriteStartArray("templates");

            foreach (var r in results)
            {
                json.WriteStartObject();
                json.WriteString("id", r.Id);
                json.WriteString("version", r.Version);
                json.WriteNumber("kats", r.Kats);
                json.WriteNumber("katsPassed", r.KatsPassed);
                json.WriteStartArray("failedKats");

                foreach (var id in r.FailedKats)
                    json.WriteStringValue(id);

                json.WriteEndArray();
                json.WriteNumber("examples", r.Examples);
                json.WriteNumber("examplesPassed", r.ExamplesPassed);
                json.WriteStartArray("failedExamples");

                foreach (var id in r.FailedExamples)
                    json.WriteStringValue(id);

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    [Fact]
    public void Committed_Kat_Results_Are_What_A_Fresh_Run_Of_Every_Case_Gives()
    {
        var fresh = Write(Run());

        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, fresh, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(FilePath), $"{FilePath} is missing; run with {WriteVariable}=1 to write it.");
        var committed = File.ReadAllText(FilePath).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(string.Equals(fresh, committed, StringComparison.Ordinal),
            $"docs/testing/kat-results.json is stale: a calculator's KAT count, version or result changed. Regenerate it with {WriteVariable}=1 (see this class), then run scripts/generate-verification-snapshot.py.");
    }

    [Fact]
    public void Every_Calculator_Is_Listed_And_Every_Known_Answer_Case_Passes()
    {
        var results = Run();
        Assert.Equal(AllCalculators.All.Count, results.Count);
        Assert.All(results, r => Assert.True(r.FailedKats.Count == 0 && r.FailedExamples.Count == 0, $"{r.Id}: failed cases {string.Join(", ", r.FailedKats.Concat(r.FailedExamples))}"));
        Assert.All(results, r => Assert.Equal(r.Kats, r.KatsPassed));
    }
}

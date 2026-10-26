using System.Text;
using System.Text.Json;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum C, C-4: the verification dashboard shows a chain as passing only when the chain really ran. This writes the run outcome of every
/// built-in chain into the <c>packs</c> section of <c>docs/testing/kat-results.json</c> (the <c>templates</c> section is
/// <c>KatResultsTests</c>'s, in the calculators project, which cannot build a chain). A chain passes when its worked example is issued as one
/// pack under the assertions the chain tests make (no refused or blocked step, no stale link, full provenance, checked, issued, every step issued
/// and read-only), every issued step is current against its upstream, and the committed worked-example README is what the run regenerates
/// (the sheets carry the date of the run, the README does not). Regenerate with
/// <c>TEMPEST_WRITE_KAT_RESULTS=1 dotnet test tests/Tempest.Calc.Tests.Library --filter PackResultsTests</c> and commit the diff.
/// </summary>
public sealed class PackResultsTests
{
    public const string WriteVariable = "TEMPEST_WRITE_KAT_RESULTS";

    private const int SchemaVersion = 1;

    /// <summary>The longest failure sentence kept in the file.</summary>
    private const int MaxFailureLength = 300;

    private static string FilePath { get; } = Path.Combine(RepositoryRoot(), "docs", "testing", "kat-results.json");

    internal sealed record PackResult(string Id, int Steps, int Bindings, bool Passed, IReadOnlyList<string> Failures);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VERSION")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("The repository root (the folder holding VERSION) was not found above the test binaries.");
    }

    /// <summary>Runs one chain's worked example and reports what failed, as one short sentence each; nothing is thrown for a failing chain.</summary>
    internal static async Task<PackResult> RunAsync(ChainPackWorkedExampleTests.Example example)
    {
        var template = example.Template;
        var failures = new List<string>();

        try
        {
            var (host, made, _) = await ChainPackWorkedExampleTests.IssueAsync(example);
            var issued = await ChainPackWorkedExampleTests.IssuedMembersAsync(host, made, template);

            foreach (var (key, member) in issued)
            {
                if (member.Status != CalcInstanceStatus.Issued)
                    failures.Add($"step {key} is not issued");

                if ((await host.Service.StalenessAsync(member)).IsStale)
                    failures.Add($"step {key} is stale against its upstream step");
            }

            var committed = Path.Combine(RepositoryRoot(), "docs", "worked-examples", "packs", example.Slug, "README.md");

            if (!File.Exists(committed))
                failures.Add("the worked example README is missing");
            else if (!string.Equals(File.ReadAllText(committed).Replace("\r\n", "\n", StringComparison.Ordinal), ChainPackWorkedExampleTests.Readme(example, issued), StringComparison.Ordinal))
                failures.Add("the committed worked example README is not what the run regenerates");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An assertion of the chain tests failed (or the chain could not be built): that is the chain's result, not a fault of this run.
            var first = (ex.Message.Split('\n', 2)[0]).Trim();
            failures.Add(first.Length > MaxFailureLength ? first[..MaxFailureLength] : first);
        }

        return new PackResult(template.Id, template.Steps.Count, template.Bindings.Count, failures.Count == 0, failures);
    }

    private static async Task<IReadOnlyList<PackResult>> RunAllAsync()
    {
        var results = new List<PackResult>();

        foreach (var example in ChainPackWorkedExampleTests.Examples.OrderBy(e => e.Template.Id, StringComparer.Ordinal))
            results.Add(await RunAsync(example));

        return results;
    }

    /// <summary>The whole file: the committed <c>templates</c> section carried through, <paramref name="packs"/> written. The layout is the one <c>KatResultsTests</c> writes.</summary>
    private static string Write(IReadOnlyList<PackResult> packs)
    {
        using var committed = JsonDocument.Parse(File.ReadAllText(FilePath));
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", SchemaVersion);
            json.WriteString("writtenBy", "templates: tests/Tempest.Calc.Tests.Calculators/KatResultsTests.cs; packs: tests/Tempest.Calc.Tests.Library/Chains/PackResultsTests.cs (set " + WriteVariable + "=1 to rewrite)");
            json.WritePropertyName("templates");
            committed.RootElement.GetProperty("templates").WriteTo(json);
            json.WriteStartArray("packs");

            foreach (var p in packs)
            {
                json.WriteStartObject();
                json.WriteString("id", p.Id);
                json.WriteNumber("steps", p.Steps);
                json.WriteNumber("bindings", p.Bindings);
                json.WriteBoolean("passed", p.Passed);
                json.WriteStartArray("failures");

                foreach (var f in p.Failures)
                    json.WriteStringValue(f);

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static IReadOnlyList<PackResult> Committed()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FilePath));

        if (!document.RootElement.TryGetProperty("packs", out var packs))
            return [];

        return [.. packs.EnumerateArray().Select(p => new PackResult(
            p.GetProperty("id").GetString()!, p.GetProperty("steps").GetInt32(), p.GetProperty("bindings").GetInt32(), p.GetProperty("passed").GetBoolean(),
            [.. p.GetProperty("failures").EnumerateArray().Select(f => f.GetString()!)]))];
    }

    [Fact]
    public async Task Committed_Pack_Results_Are_What_A_Fresh_Run_Of_Every_Chain_Gives()
    {
        var fresh = await RunAllAsync();

        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            File.WriteAllText(FilePath, Write(fresh), new UTF8Encoding(false));
            return;
        }

        var committed = Committed();
        Assert.Equal(fresh.Count, committed.Count);

        for (var i = 0; i < fresh.Count; i++)
        {
            Assert.True(
                fresh[i].Id == committed[i].Id && fresh[i].Steps == committed[i].Steps && fresh[i].Bindings == committed[i].Bindings && fresh[i].Passed == committed[i].Passed && fresh[i].Failures.SequenceEqual(committed[i].Failures),
                $"docs/testing/kat-results.json is stale for {fresh[i].Id}: the run gives passed={fresh[i].Passed} ({string.Join("; ", fresh[i].Failures)}), the file has passed={committed[i].Passed} with {committed[i].Steps} steps and {committed[i].Bindings} links. Regenerate it with {WriteVariable}=1 (see this class), then run scripts/generate-verification-snapshot.py.");
        }
    }

    [Fact]
    public async Task Every_Built_In_Chain_Has_A_Worked_Example_And_Passes()
    {
        var examples = ChainPackWorkedExampleTests.Examples;
        Assert.Equal(CalcPackTemplates.All.Select(t => t.Id).Order(StringComparer.Ordinal), examples.Select(e => e.Template.Id).Order(StringComparer.Ordinal));

        var results = await RunAllAsync();
        Assert.All(results, r => Assert.True(r.Passed, $"{r.Id}: {string.Join("; ", r.Failures)}"));
    }

    [Fact]
    public async Task A_Chain_Whose_Worked_Example_Readme_Is_Not_What_The_Run_Regenerates_Fails()
    {
        // The proof that a chain's result can fail: the same example with a different README expectation (its title changed) reports the mismatch.
        var real = ChainPackWorkedExampleTests.Examples.First(e => e.Slug == "cold-pipe");
        var changed = real with { Title = real.Title + " (changed)", Note = real.Note + " changed" };
        var result = await RunAsync(changed);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, f => f.Contains("README", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Chain_That_Cannot_Be_Built_Reports_The_Reason_Instead_Of_Throwing()
    {
        var real = ChainPackWorkedExampleTests.Examples.First(e => e.Slug == "motor-circuit");
        var broken = real with { Inputs = _ => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>>(new Dictionary<string, IReadOnlyList<CalcInputValue>>()) };
        var result = await RunAsync(broken);

        Assert.False(result.Passed);
        Assert.NotEmpty(result.Failures);
    }
}

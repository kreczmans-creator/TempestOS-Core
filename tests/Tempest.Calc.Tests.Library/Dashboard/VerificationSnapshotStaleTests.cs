using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>
/// Addendum C, C-4: the dashboard shows document-derived data from an embedded snapshot, not from <c>docs/</c> (an installed build has no docs
/// folder). These tests fail when the snapshot was made from older documents than the ones in the repository, and when it holds a result for a
/// version of a template that is no longer the shipped one. The fix is always the same: run
/// <c>python3 scripts/generate-verification-snapshot.py</c> (after <c>TEMPEST_WRITE_KAT_RESULTS=1</c> on KatResultsTests if the KAT results are
/// what moved) and commit the diff.
/// </summary>
public sealed class VerificationSnapshotStaleTests
{
    private const string Regenerate = "Regenerate it: TEMPEST_WRITE_KAT_RESULTS=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter KatResultsTests, then python3 scripts/generate-verification-snapshot.py, and commit the result.";

    private static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VERSION")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("The repository root (the folder holding VERSION) was not found above the test binaries.");
    }

    /// <summary>
    /// The same value <c>scripts/generate-verification-snapshot.py</c> writes under "sources": SHA-256 over
    /// "relative/path\n&lt;sha256 of the file with CRLF folded to LF&gt;\n" for each file in ordinal path order.
    /// </summary>
    internal static string Digest(IEnumerable<string> files)
    {
        var lines = new StringBuilder();

        foreach (var file in files.Select(f => f.Replace('\\', '/')).Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, file));
            var text = Encoding.Latin1.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal);
            lines.Append(file).Append('\n').Append(Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(text)))).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString())));
    }

    private static IEnumerable<string> Files(string folder, string pattern, SearchOption option = SearchOption.TopDirectoryOnly) =>
        Directory.GetFiles(Path.Combine(Root, folder), pattern, option).Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'));

    [Fact]
    public void The_Embedded_Snapshot_Is_Readable_And_Names_The_Four_Input_Groups()
    {
        var snapshot = VerificationSnapshot.LoadEmbedded();

        Assert.Null(snapshot.Problem);
        Assert.Equal(["katResults", "knownDefects", "rederivation", "workedExamples"], snapshot.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.True(snapshot.Items.Count > 60, $"{snapshot.Items.Count} items");
    }

    [Fact]
    public void The_Snapshot_Was_Made_From_The_Documents_That_Are_In_The_Repository_Now()
    {
        var snapshot = VerificationSnapshot.LoadEmbedded();
        var current = new Dictionary<string, string>
        {
            ["rederivation"] = Digest(Files("docs/verification/rederivation", "*.md")),
            ["knownDefects"] = Digest(["docs/testing/v0.26.0-known-defects.md"]),
            ["katResults"] = Digest(["docs/testing/kat-results.json"]),
            ["workedExamples"] = Digest(Files("docs/worked-examples", "README.md", SearchOption.AllDirectories)),
        };

        foreach (var (group, digest) in current)
        {
            Assert.True(snapshot.Sources.TryGetValue(group, out var recorded) && recorded == digest,
                $"The verification snapshot is stale: the {group} documents changed since it was generated. {Regenerate}");
        }
    }

    [Fact]
    public void Every_Shipped_Template_Has_Kat_Results_For_Its_Current_Version_And_Case_Count()
    {
        var snapshot = VerificationSnapshot.LoadEmbedded();

        foreach (var calculator in ShippedCalculators.All)
        {
            var t = calculator.Template;
            Assert.True(snapshot.Items.TryGetValue(t.Id, out var item) && item.Kat is not null, $"No KAT results for {t.Id}. {Regenerate}");
            Assert.True(item.Kat!.Version == t.Version && item.Kat.Kats == t.TestCases.Count,
                $"The KAT results for {t.Id} are for version {item.Kat.Version} with {item.Kat.Kats} cases; the template is {t.Version} with {t.TestCases.Count}. {Regenerate}");
            Assert.True(item.Kat.KatsPassed == item.Kat.Kats && item.Kat.FailedKats.Count == 0 && item.Kat.FailedExamples.Count == 0, $"{t.Id} has failing known-answer cases in the recorded results.");
        }
    }

    [Fact]
    public async Task Every_Calculator_And_Chain_Of_The_Product_Has_A_Row_With_Results_And_No_Row_Is_Not_Run()
    {
        var catalogue = new CalcCatalogue(ShippedCalculators.All);
        var service = new VerificationDashboardService(catalogue, CalcPackTemplates.All, VerificationSnapshot.LoadEmbedded());
        var report = await service.BuildAsync();

        Assert.Null(report.DataProblem);
        Assert.False(report.Truncated);
        Assert.Equal(catalogue.Current().Count + CalcPackTemplates.All.Count, report.Rows.Count);
        Assert.All(report.Rows, r => Assert.True(r.KatState == KatRunState.Passed, $"{r.Id}: {r.KatText}"));
        Assert.All(report.Rows.Where(r => r.Kind == VerificationRowKind.Calculator), r => Assert.True(r.WorkedExamples > 0, $"{r.Id} has no worked example in the snapshot"));
        Assert.Contains(report.Rows, r => r.Id == "mech.shaft" && r.RederivationResult == "Match, with findings" && r.Findings.Count > 5);
        Assert.Contains(report.Rows, r => r.Id == "pack.drive-train" && r.Findings.Count > 5);
    }

    [Fact]
    public void The_Statuses_In_The_Snapshot_Are_The_Ones_The_Known_Defects_Document_Gives()
    {
        var table = KnownDefectsTable.Parse(File.ReadAllText(Path.Combine(Root, "docs/testing/v0.26.0-known-defects.md")));
        var snapshot = VerificationSnapshot.LoadEmbedded();
        var findings = snapshot.Items.Values.Where(i => i.Rederivation is not null).SelectMany(i => i.Rederivation!.Findings).ToDictionary(f => f.Id, StringComparer.Ordinal);

        Assert.True(table.Count > 100, $"{table.Count} defect rows");

        foreach (var row in table.Where(r => !r.Id.StartsWith("desktop.", StringComparison.Ordinal)))
        {
            Assert.True(findings.TryGetValue(row.Id, out var finding), $"{row.Id} is in the known-defects document but not in the snapshot. {Regenerate}");
            Assert.True(finding!.Defect == row.Status, $"{row.Id}: the document says {row.Status}, the snapshot {finding.Defect}. {Regenerate}");
            Assert.Equal(row.Severity, finding.Severity);
        }

        // Nothing is open in the snapshot that the document does not list as open.
        var openInDocument = table.Where(r => r.Status is VerificationDefectStatus.StillOpen or VerificationDefectStatus.OwnerDecision).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(findings.Values.Where(f => f.Defect is VerificationDefectStatus.StillOpen or VerificationDefectStatus.OwnerDecision), f => Assert.Contains(f.Id, openInDocument));
    }
}

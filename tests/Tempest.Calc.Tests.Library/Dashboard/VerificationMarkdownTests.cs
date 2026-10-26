using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Tests.Sheets;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>Addendum C, C-4: the markdown export for the owner's records.</summary>
public sealed class VerificationMarkdownTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 26, 9, 30, 0, TimeSpan.Zero);

    private static VerificationRow Row(string id, string title = "Title", string discipline = "Mechanical", IReadOnlyList<VerificationFinding>? findings = null, IReadOnlyList<string>? missing = null) =>
        new(VerificationRowKind.Calculator, id, title, discipline, "1.2.0", 4, 4, KatRunState.Passed, string.Empty, 2, 1, 2,
            "Match, with findings", new VerificationSeverityCounts(0, 1, 2), "MATCH WITH FINDINGS", missing ?? [], missing ?? [], findings ?? [], ManualTestStatus.NotTested, []);

    private static VerificationReport Report(params VerificationRow[] rows) => new(rows, rows.Length, false, null, At);

    [Fact]
    public void The_Document_Has_The_Header_The_Summary_One_Table_Row_Per_Row_And_The_Open_Defects()
    {
        var open = new[] { new VerificationFinding("mech.shaft-02", "Medium", VerificationDefectStatus.StillOpen), new VerificationFinding("mech.shaft-01", "High", VerificationDefectStatus.OwnerDecision), new VerificationFinding("mech.shaft-03", "Medium", VerificationDefectStatus.Fixed) };
        var report = Report(Row("mech.shaft", "Shaft", findings: open, missing: ["Shaft material", "Shaft limits"]), Row("mech.spring", "Spring"));

        var text = VerificationMarkdown.Write(report, report.Rows, "Discipline: Mechanical; status: all", "0.26.0");

        Assert.StartsWith("# TempestOS verification dashboard\n", text, StringComparison.Ordinal);
        Assert.Contains("Generated 2026-10-26 09:30 UTC by TempestOS 0.26.0.", text, StringComparison.Ordinal);
        Assert.Contains("Filter: Discipline: Mechanical; status: all.", text, StringComparison.Ordinal);
        Assert.Contains("2 of 2 rows shown: 0 failing, 2 open defect(s) on 1 row(s), 2 with Unconfirmed or Secondary cases, 1 with records not entered, 0 not run, 0 clean.", text, StringComparison.Ordinal);
        Assert.Contains("| Kind | Id | Title | Discipline | Version | Known answers | Unconfirmed | Secondary | Track P re-derivation | Records not entered | Open defects | Manual test |", text, StringComparison.Ordinal);
        Assert.Contains("| Calculator | `mech.shaft` | Shaft | Mechanical | 1.2.0 | 4 of 4 pass | 1 | 2 | Match, with findings: 1 High, 2 Medium | Shaft material; Shaft limits | 2 | Not tested |", text, StringComparison.Ordinal);
        Assert.Contains("| Calculator | `mech.spring` | Spring | Mechanical | 1.2.0 | 4 of 4 pass | 1 | 2 | Match, with findings: 1 High, 2 Medium | none | 0 | Not tested |", text, StringComparison.Ordinal);

        // Open defects, worst first, Fixed ones left out.
        Assert.Contains("- `mech.shaft` (2): `mech.shaft-01` High, owner decision, `mech.shaft-02` Medium, still open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("mech.shaft-03", text, StringComparison.Ordinal);
        Assert.Contains("## How to read the columns", text, StringComparison.Ordinal);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_The_Rows_Passed_In_Are_Written_And_The_Summary_Says_How_Many_Of_All()
    {
        var report = Report(Row("a.one"), Row("b.two"), Row("c.three"));
        var text = VerificationMarkdown.Write(report, [report.Rows[1]], "Discipline: Mechanical", "0.26.0");

        Assert.Contains("1 of 3 rows shown", text, StringComparison.Ordinal);
        Assert.Contains("`b.two`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("`a.one`", text, StringComparison.Ordinal);
        Assert.Contains("None of the rows above has an open defect.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Chain_Shows_A_Dash_For_Its_Version_And_Notes_Come_Through()
    {
        var chain = Row("pack.drive-train", "Drive train", "Chains") with { Kind = VerificationRowKind.Chain, Version = string.Empty };
        var report = new VerificationReport([chain], 700, true, "The verification data is not part of this build.", At);
        var text = VerificationMarkdown.Write(report, report.Rows, string.Empty, "0.26.0");

        Assert.Contains("| Chain | `pack.drive-train` | Drive train | Chains | — |", text, StringComparison.Ordinal);
        Assert.Contains("Filter: all rows.", text, StringComparison.Ordinal);
        Assert.Contains("**Note.** The verification data is not part of this build. The columns that come from documents are empty.", text, StringComparison.Ordinal);
        Assert.Contains("**Note.** The report holds the first 1 of 700 rows.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_That_Would_Break_The_Table_Is_Made_Harmless_And_Long_Text_Is_Cut()
    {
        Assert.Equal("a\\|b c d", VerificationMarkdown.Cell("a|b\nc\rd"));
        Assert.Equal("&lt;script&gt;x&lt;/script&gt; 'tick'", VerificationMarkdown.Cell("<script>x</script> `tick`"));
        Assert.Equal(string.Empty, VerificationMarkdown.Cell(null));
        Assert.Equal("x y", VerificationMarkdown.Cell("x\u0007y"));

        var cut = VerificationMarkdown.Cell(new string('a', 5000));
        Assert.Equal(VerificationMarkdown.MaxCellLength + 1, cut.Length);
        Assert.EndsWith("…", cut, StringComparison.Ordinal);

        var report = Report(Row("x.y", "Bad | title\nwith <b>html</b>"));
        var text = VerificationMarkdown.Write(report, report.Rows, "a|b", "0.26.0");
        Assert.Contains("Bad \\| title with &lt;b&gt;html&lt;/b&gt;", text, StringComparison.Ordinal);
        Assert.Contains("Filter: a\\|b.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Document_Size_Is_Capped_And_Says_Where_It_Stops()
    {
        var rows = Enumerable.Range(0, 6000).Select(i => Row($"t.{i}", new string('x', VerificationMarkdown.MaxCellLength), missing: [new string('m', VerificationMarkdown.MaxCellLength)])).ToArray();
        var report = Report(rows);
        var text = VerificationMarkdown.Write(report, report.Rows, string.Empty, "0.26.0");

        Assert.True(text.Length < VerificationMarkdown.MaxCharacters + 5_000, $"{text.Length} characters");
        Assert.Contains("The document stops after", text, StringComparison.Ordinal);
        Assert.Contains("of 6000 rows (size limit).", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Real_Report_Writes_Every_Row_And_Marks_The_Open_Defects()
    {
        var host = new LibraryTestHost();
        var report = await new VerificationDashboardService(new CalcCatalogue(ShippedCalculators.All), CalcPackTemplates.All, VerificationSnapshot.LoadEmbedded(), new StoreRequiredRecordsSource(host.Store)).BuildAsync();
        var text = VerificationMarkdown.Write(report, report.Rows, "all rows", "0.26.0");

        Assert.All(report.Rows, r => Assert.Contains($"`{r.Id}`", text, StringComparison.Ordinal));
        Assert.Contains("## Open defects", text, StringComparison.Ordinal);
        Assert.Contains("- `elec.load-budget` (", text, StringComparison.Ordinal); // elec.load-budget-02 is Still open (partly fixed)
        Assert.DoesNotContain("not run (", text, StringComparison.Ordinal);
    }
}

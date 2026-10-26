using Tempest.Calc.Library.Dashboard;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>
/// The test reader of the known-defects document (<see cref="KnownDefectsTable"/>) and the script's reader of the same document
/// (<c>tests/scripts/test_generate_verification_snapshot.py</c> holds the same cases) must accept every bulk-line variant the document uses and
/// refuse a document whose rows do not add up to its summary table.
/// </summary>
public sealed class KnownDefectsTableTests
{
    private const string Header = "### Medium (4), bulk classification\n\n| ID | Decision | Evidence |\n|---|---|---|\n";

    private static string Document(string bulkLine, string summaryMedium = "| Medium | 1 | 0 | 3 | 0 | 4 |", string tableRows = "| a.one-01 | **Fixed (abc)** | x |\n| a.two-02 | **Still open (partly fixed)** | y |\n") =>
        $"| Severity | Fixed | Owner decision | Still open | Not a defect | Total |\n|---|---|---|---|---|---|\n{summaryMedium}\n\n{Header}{tableRows}\n{bulkLine}\n";

    [Fact]
    public void The_Code_Unchanged_Line_Lists_Its_Ids()
    {
        var rows = KnownDefectsTable.Parse(Document("Still open, code unchanged (2): a.three-03, a.four-04.", "| Medium | 1 | 0 | 3 | 0 | 4 |"));

        Assert.Equal(["a.one-01", "a.two-02", "a.three-03", "a.four-04"], rows.Select(r => r.Id));
        Assert.Equal(VerificationDefectStatus.Fixed, rows[0].Status);
        Assert.All(rows.Skip(1), r => Assert.Equal(VerificationDefectStatus.StillOpen, r.Status));
        Assert.All(rows, r => Assert.Equal("Medium", r.Severity));
    }

    [Fact]
    public void The_Listed_For_The_Owner_Line_Reads_The_Id_And_Drops_The_Bracketed_Note_Even_When_The_Note_Holds_Commas()
    {
        var rows = KnownDefectsTable.Parse(Document("Still open, listed for the owner (2): a.three-03 (a supply-side drop, a relief margin; a cycle), a.four-04 (note)."));

        Assert.Equal(["a.one-01", "a.two-02", "a.three-03", "a.four-04"], rows.Select(r => r.Id));
    }

    [Fact]
    public void Backticked_Ids_Are_Read_Without_The_Backticks()
    {
        var rows = KnownDefectsTable.Parse(Document("Still open, code unchanged (2): `a.three-03`, `a.four-04` (a note)."));

        Assert.Equal(["a.one-01", "a.two-02", "a.three-03", "a.four-04"], rows.Select(r => r.Id));
    }

    [Fact]
    public void A_Partly_Fixed_Decision_Is_Still_Open_Not_Fixed()
    {
        var table = "| a.one-01 | **Partly fixed (abc)** | x |\n| a.two-02 | **Still open (partly fixed)** | y |\n";
        var rows = KnownDefectsTable.Parse(Document("Still open, code unchanged (2): a.three-03, a.four-04.", "| Medium | 0 | 0 | 4 | 0 | 4 |", table));

        Assert.All(rows, r => Assert.Equal(VerificationDefectStatus.StillOpen, r.Status));
    }

    [Fact]
    public void A_Count_That_Does_Not_Match_The_Listed_Ids_Is_Refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => KnownDefectsTable.Parse(Document("Still open, listed for the owner (3): a.three-03 (note).")));

        Assert.Contains("says 3 ids and lists 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_That_Do_Not_Add_Up_To_The_Summary_Table_Are_Refused()
    {
        // The summary prints 3 still open and 4 in total; only 1 id is listed in the bulk line, so 2 are open.
        var ex = Assert.Throws<InvalidOperationException>(() => KnownDefectsTable.Parse(Document("Still open, listed for the owner (1): a.three-03 (note).")));

        Assert.Contains("summary table prints [1, 0, 3, 0, 4]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Id_Listed_In_A_Table_And_In_A_Bulk_Line_Is_Refused()
    {
        Assert.Throws<InvalidOperationException>(() => KnownDefectsTable.Parse(Document("Still open, code unchanged (1): a.two-02.")));
    }

    [Fact]
    public void An_Item_That_Is_Not_An_Id_Is_Refused_Not_Skipped()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => KnownDefectsTable.Parse(Document("Still open, code unchanged (2): a.three-03, (a note only).")));

        Assert.Contains("does not start with a finding id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Document_With_No_Summary_Table_Is_Read_Without_The_Cross_Check()
    {
        var rows = KnownDefectsTable.Parse(Header + "| a.one-01 | **Fixed** | x |\n");

        Assert.Single(rows);
    }
}

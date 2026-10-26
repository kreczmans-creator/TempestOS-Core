using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Found by the C-7 soak: the 4 MB cap on a stored document was checked after the whole document had been serialised. The shape caps on a reference
/// record (200 values, 500 rows, 50 cells to a row) allow some 250 million characters of JSON, so refusing one save took over a gigabyte of memory.
/// The cap is now enforced as the document is written: past it, the save is refused having built at most the cap's worth.
/// </summary>
public sealed class StoreDocumentCapTests
{
    private static CalcRow Row(int cells) =>
        new(Enumerable.Range(0, cells).ToDictionary(c => $"c{c:00}", c => new CalcCell(c * 1.5, "mm"), StringComparer.Ordinal));

    private static CalcDataValue Table(string symbol, int rows, int cells) =>
        new(symbol, Rows: [.. Enumerable.Range(0, rows).Select(_ => Row(cells))]);

    private static CalcDataRecord Record(params CalcDataValue[] values) =>
        new(Guid.NewGuid(), "test.table", "Cap test", 1, "Test source", "Test edition", "Table 1", "Test holder", "test", false, CalcDataOrigin.UserEntered, values, DateTimeOffset.UtcNow);

    /// <summary>Twenty tables of 500 rows of 50 cells: within every shape cap, about 17 million characters of JSON, four times the document cap.</summary>
    private static CalcDataRecord HugeRecord() => Record([.. Enumerable.Range(0, 20).Select(i => Table($"t{i:00}", CalcDataRecord.MaxRows, CalcDataRecord.MaxCellsPerRow))]);

    [Fact]
    public async Task A_Record_Past_The_Document_Cap_Is_Refused_Without_Building_The_Whole_Document()
    {
        var host = new LibraryTestHost(withProject: false);
        var record = HugeRecord();
        Assert.Null(record.Problem());

        var before = GC.GetAllocatedBytesForCurrentThread();
        var save = host.Store.SaveRecordAsync(record);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => save);
        Assert.Contains("larger than 4 MB", refused.Message, StringComparison.Ordinal);

        // The refusal builds at most the cap's worth (a few buffers of about 4 MB), not the 17 million characters of the whole document twice over.
        Assert.True(allocated < 32L * 1024 * 1024, $"Refusing the record allocated {allocated / (1024.0 * 1024):0.0} MiB.");
        Assert.Null(await host.Store.FindRecordAsync(record.Id));
    }

    [Fact]
    public async Task The_Service_Refuses_Such_A_Record_With_A_Sentence_And_Saves_Nothing()
    {
        var host = new LibraryTestHost(withProject: false);
        var (saved, refusal) = await host.Service.SaveRecordAsync(HugeRecord() with { Id = Guid.Empty });

        Assert.Null(saved);
        Assert.Contains("larger than 4 MB", refusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync());
    }

    [Fact]
    public async Task A_Record_Under_The_Cap_Is_Written_And_Reads_Back_Whole()
    {
        var host = new LibraryTestHost(withProject: false);
        var record = Record(Table("t", CalcDataRecord.MaxRows, CalcDataRecord.MaxCellsPerRow));
        Assert.InRange(CalcInstanceStore.Serialise(record).Length, 500_000, CalcInstanceStore.MaxDocumentBytes);

        await host.Store.SaveRecordAsync(record);

        var read = await host.Store.FindRecordAsync(record.Id);
        Assert.NotNull(read);
        Assert.Equal(CalcDataRecord.MaxRows, read.Values[0].Rows!.Count);
        Assert.Equal(CalcInstanceStore.Serialise(record), CalcInstanceStore.Serialise(read));
    }

    [Fact]
    public void The_Bounded_Serialiser_Writes_Exactly_The_Text_The_Plain_One_Does_And_Refuses_At_The_Same_Size()
    {
        var small = Record(Table("t", 40, 10));
        Assert.Equal(CalcInstanceStore.Serialise(small), CalcInstanceStore.SerialiseWithin(small, () => "too large"));

        // Text the serialiser escapes (it writes ASCII only), and a null that is left out.
        var odd = Record(new CalcDataValue("é", 1, "µm", "naïve — “quoted” \\ \" \n"));
        Assert.Equal(CalcInstanceStore.Serialise(odd), CalcInstanceStore.SerialiseWithin(odd, () => "too large"));

        // One table of 500 × 50 is about 830,000 characters; four are under the cap, five are over it.
        var under = Record(Table("a", 500, 50), Table("b", 500, 50), Table("c", 500, 50), Table("d", 500, 50));
        var over = Record(Table("a", 500, 50), Table("b", 500, 50), Table("c", 500, 50), Table("d", 500, 50), Table("e", 500, 50));
        var underLength = CalcInstanceStore.Serialise(under).Length;
        var overLength = CalcInstanceStore.Serialise(over).Length;
        Assert.True(underLength <= CalcInstanceStore.MaxDocumentBytes && overLength > CalcInstanceStore.MaxDocumentBytes, $"The test's records are {underLength:N0} and {overLength:N0} characters; they must straddle the cap.");
        Assert.Equal(underLength, CalcInstanceStore.SerialiseWithin(under, () => "too large").Length);
        Assert.Equal("too large", Assert.Throws<InvalidOperationException>(() => CalcInstanceStore.SerialiseWithin(over, () => "too large")).Message);
    }

    [Fact]
    public async Task An_Instance_Is_Written_Through_The_Bounded_Serialiser_And_Reads_Back_Unchanged()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var created = await host.Service.CreateAsync("test.direct-stress", project, "Bounded write", LibraryTestHost.ProvenancedInputs());

        var read = await host.Store.FindInstanceAsync(created.Id);
        Assert.Equal(CalcInstanceStore.Serialise(created), CalcInstanceStore.Serialise(read!));
        Assert.Equal(CalcInstanceStore.Serialise(created), CalcInstanceStore.SerialiseWithin(created, () => "too large"));
    }
}

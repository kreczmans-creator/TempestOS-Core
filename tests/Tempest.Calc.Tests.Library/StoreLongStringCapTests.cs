using Tempest.Calc.Library;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Found by the C-7 soak review: the store's bounded writer refused a document whose <em>reserved</em> space passed the cap, not one whose written bytes did.
/// <c>Utf8JsonWriter</c> asks for room for the worst case of a string (about three bytes a character, up to six when the text needs escaping) before it
/// writes it, so an issued calculation whose <c>Issue.SheetText</c> was 1.4 million characters (1,401,862 of them, well under 4 MB stored) was refused. The cap is
/// now enforced on the bytes actually written, so every document that fits is stored and every one that does not is still refused.
/// </summary>
public sealed class StoreLongStringCapTests
{
    private static CalcIssueSnapshot Issue(string sheetText, string templateText = "T") =>
        new(sheetText, templateText, "engine", 1, 1, DateTimeOffset.UnixEpoch, null, "test");

    private static async Task<(LibraryTestHost Host, CalcInstance Instance)> InstanceAsync()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var created = await host.Service.CreateAsync("test.direct-stress", project, "Long sheet", LibraryTestHost.ProvenancedInputs());
        return (host, created);
    }

    /// <summary>The text of a calculation as stored, in bytes; the serialiser writes ASCII only, so this is also its length.</summary>
    private static int StoredSize(CalcInstance instance) => CalcInstanceStore.Serialise(instance).Length;

    private static string Ascii(int length) => new('x', length);

    /// <summary>Non-ASCII text: the serialiser writes each character as a six-byte escape (é).</summary>
    private static string Accented(int length) => new('é', length);

    public static TheoryData<string, int, int, int, bool> Cases => new()
    {
        // description, sheet characters, template characters, accented? (0 or 1), accepted
        { "1.4 million ASCII characters (the soak's 1,401,862-character sheet)", 1_401_862, 1, 0, true },
        { "2.0 million ASCII characters", 2_000_000, 1, 0, true },
        { "3.9 million ASCII characters (just under the cap)", 3_900_000, 1, 0, true },
        { "4.1 million ASCII characters (over the cap)", 4_100_000, 1, 0, false },
        { "600 thousand characters after 2.5 million of other content", 600_000, 2_500_000, 0, true },
        { "1.6 million characters after 2.5 million of other content (over the cap)", 1_600_000, 2_500_000, 0, false },
        { "500 thousand accented characters, which escape to about 3 MB", 500_000, 1, 1, true },
        { "700 thousand accented characters, which escape to about 4.2 MB (over the cap)", 700_000, 1, 1, false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_Document_Is_Stored_When_Its_Written_Size_Is_Under_The_Cap_And_Refused_When_It_Is_Over(string description, int sheet, int template, int accented, bool accepted)
    {
        var (host, created) = await InstanceAsync();
        var text = accented == 1 ? Accented(sheet) : Ascii(sheet);
        var big = created with { Issue = Issue(text, Ascii(template)) };
        var size = StoredSize(big);

        // The test's own premise: the document is on the side of the cap the case says. (The plain serialiser has no cap, so it can measure it.)
        Assert.True(accepted ? size <= CalcInstanceStore.MaxDocumentBytes : size > CalcInstanceStore.MaxDocumentBytes, $"{description}: the document is {size:N0} bytes.");

        if (accepted)
        {
            await host.Store.SaveInstanceAsync(big);
            var read = await host.Store.FindInstanceAsync(created.Id);
            Assert.Equal(text, read!.Issue!.SheetText);
            Assert.Equal(size, StoredSize(read));
        }
        else
        {
            var refused = await Assert.ThrowsAsync<CalcDocumentTooLargeException>(() => host.Store.SaveInstanceAsync(big));
            Assert.Contains("larger than 4 MB", refused.Message, StringComparison.Ordinal);
            Assert.Null((await host.Store.FindInstanceAsync(created.Id))!.Issue);
        }
    }

    [Fact]
    public void The_Bounded_Serialiser_Writes_A_Long_String_Exactly_As_The_Plain_One_Does()
    {
        foreach (var length in new[] { 70_000, 1_401_862, 2_000_000 })
        {
            var doc = new { Text = Ascii(length), Odd = "q\"uo\\te\n" + Accented(length / 10) };
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(doc), CalcInstanceStore.SerialiseWithin(doc, () => "too large"));
        }
    }

    [Fact]
    public void A_String_Far_Past_The_Cap_Is_Refused_Without_Reserving_Several_Times_Its_Length()
    {
        var text = Ascii(12_000_000);
        var doc = new { Text = text };

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<CalcDocumentTooLargeException>(() => CalcInstanceStore.SerialiseWithin(doc, () => "too large"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The writer asks for room for three times the string (36 MB); a document that cannot fit however it is written is refused before that room is made.
        Assert.True(allocated < 24L * 1024 * 1024, $"Refusing the document allocated {allocated / (1024.0 * 1024):0.0} MiB.");
    }
}

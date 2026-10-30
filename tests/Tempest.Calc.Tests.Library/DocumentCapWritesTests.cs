using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Library.ManualTesting;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// The C-7 soak review (findings 3, 4 and 5): every document the calculation store writes meets the 4 MB cap as it is written, in one plain sentence that
/// names the item, and the services turn that refusal into a result by its type (<see cref="CalcDocumentTooLargeException"/>), never by looking for words
/// in its message. A refused instantiation or revision makes nothing.
/// </summary>
public sealed class DocumentCapWritesTests
{
    private const string BracketTooLarge = "Calculation 'Bracket' is larger than 4 MB and was not saved; take some of its table rows out and save it again.";

    private static string Ascii(int length) => new('x', length);

    private static CalcPack Pack(string title, string note) =>
        new(Guid.NewGuid(), null, title, "A", [Guid.NewGuid()], [new CalcPackRevision("A", DateTimeOffset.UnixEpoch, "someone", note)], CalcInstanceStatus.Draft, null, "someone", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static CalcDataRecord Record(string title, string text, params CalcDataValue[] values) =>
        new(Guid.NewGuid(), "test.table", title, 1, text, "Test edition", "Table 1", "Test holder", "test", false, CalcDataOrigin.UserEntered, values.Length > 0 ? values : [new CalcDataValue("a", 1, "mm")], DateTimeOffset.UtcNow);

    private static CalcDataValue Table(string symbol) =>
        new(symbol, Rows: [.. Enumerable.Range(0, CalcDataRecord.MaxRows).Select(_ => new CalcRow(Enumerable.Range(0, CalcDataRecord.MaxCellsPerRow).ToDictionary(c => $"c{c:00}", c => new CalcCell(c * 1.5, "mm"), StringComparer.Ordinal)))]);

    // ---- Finding 3: packs, pack templates, reference records ----

    [Fact]
    public async Task A_Pack_Past_The_Cap_Is_Refused_By_Its_Title_Whichever_Way_It_Is_Saved_And_The_Stored_One_Stays()
    {
        var host = new LibraryTestHost(withProject: false);
        var small = Pack("Frame pack", "First issue");
        await host.Store.SavePackAsync(small);

        var big = small with { Revisions = [new CalcPackRevision("A", DateTimeOffset.UnixEpoch, "someone", Ascii(CalcInstanceStore.MaxDocumentBytes + 1_000))] };
        var refused = await Assert.ThrowsAsync<CalcDocumentTooLargeException>(() => host.Store.SavePackAsync(big));
        Assert.Equal("Pack 'Frame pack' is larger than 4 MB and was not saved.", refused.Message);

        // With the version the caller read (the check, the second check, the reopen) it is refused in the same words and the stored pack is as it was.
        await Assert.ThrowsAsync<CalcDocumentTooLargeException>(() => host.Store.SavePackAsync(big, small.UpdatedAt));
        Assert.Equal(CalcInstanceStore.Serialise(small), CalcInstanceStore.Serialise((await host.Store.FindPackAsync(small.Id))!));

        // Under the cap it is stored, as before.
        var fits = small with { Revisions = [new CalcPackRevision("A", DateTimeOffset.UnixEpoch, "someone", Ascii(3_500_000))] };
        await host.Store.SavePackAsync(fits, small.UpdatedAt);
        Assert.Equal(3_500_000, (await host.Store.FindPackAsync(small.Id))!.Revisions[0].Description.Length);
    }

    [Fact]
    public async Task A_Pack_Template_Past_The_Cap_Is_Refused_By_Its_Title_And_Nothing_Is_Stored()
    {
        var host = new LibraryTestHost(withProject: false);
        var huge = new CalcPackTemplate("test.huge", "Huge chain", "A chain.",
            [new CalcPackStep("a", "test.direct-stress", "Bracket", [CalcInputValue.Choice("grade", Ascii(CalcInstanceStore.MaxDocumentBytes + 1_000))])], []);

        var refused = await Assert.ThrowsAsync<CalcDocumentTooLargeException>(() => host.Store.SavePackTemplateAsync(huge));

        Assert.Equal("Pack template 'Huge chain' is larger than 4 MB and was not saved.", refused.Message);
        Assert.Empty(await host.Store.ListPackTemplatesAsync());

        await host.Store.SavePackTemplateAsync(huge with { Steps = [huge.Steps[0] with { Example = [CalcInputValue.Choice("grade", Ascii(1_500_000))] }] });
        Assert.Single(await host.Store.ListPackTemplatesAsync());
    }

    [Fact]
    public async Task A_Batch_Of_Records_With_One_Past_The_Cap_Is_Refused_By_Title_Without_Building_It_Whole_And_Writes_None()
    {
        var host = new LibraryTestHost(withProject: false);
        var fine = Record("Small record", "Source");

        // Twenty tables of 500 rows of 50 cells: within every shape cap, about 17 million characters of JSON, four times the cap.
        var huge = Record("Huge record", "Source", [.. Enumerable.Range(0, 20).Select(i => Table($"t{i:00}"))]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var save = host.Store.SaveRecordsAsync([fine, huge]);
        var refused = await Assert.ThrowsAsync<CalcDocumentTooLargeException>(() => save);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("Reference record 'Huge record' is larger than 4 MB and was not written.", refused.Message);
        Assert.Null(await host.Store.FindRecordAsync(fine.Id));
        Assert.Null(await host.Store.FindRecordAsync(huge.Id));
        Assert.True(allocated < 32L * 1024 * 1024, $"Refusing the batch allocated {allocated / (1024.0 * 1024):0.0} MiB.");
    }

    [Fact]
    public async Task A_Single_Record_Past_The_Cap_Is_Refused_By_Title_Not_By_Identifier()
    {
        var host = new LibraryTestHost(withProject: false);
        var huge = Record("Huge record", Ascii(CalcInstanceStore.MaxDocumentBytes + 1_000));

        var refused = await Assert.ThrowsAsync<CalcDocumentTooLargeException>(() => host.Store.SaveRecordAsync(huge));

        Assert.Equal("Reference record 'Huge record' is larger than 4 MB and was not written.", refused.Message);
        Assert.DoesNotContain(huge.Id.ToString("N"), refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Finding 4: a pack template step too large to store ----

    private static CalcPackTemplate TwoSteps() =>
        new("test.two", "Two steps", "A source and a bracket.", [new CalcPackStep("src", "test.force-source", "Load"), new CalcPackStep("big", "test.direct-stress", "Bracket")], []);

    private static LibraryTestHost PackHost(CalcPackTemplate template) => new(calculators: [new ForceSourceCalculator()], packTemplates: [template]);

    private static IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>> StepInputs(string grade) =>
        new Dictionary<string, IReadOnlyList<CalcInputValue>>
        {
            ["src"] = [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))],
            ["big"] = [.. LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "grade" ? i with { Text = grade } : i)],
        };

    [Fact]
    public async Task A_Pack_Template_Step_Too_Large_To_Store_Is_A_Refusal_Naming_The_Step_And_Makes_Nothing()
    {
        var template = TwoSteps();
        var host = PackHost(template);
        var project = await host.ProjectAsync();
        var objectsBefore = (await host.Context.Repository.ListAllAsync()).Count;

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Two-step pack", StepInputs(Ascii(CalcInstanceStore.MaxDocumentBytes + 100_000)));

        Assert.Null(made);
        Assert.Equal($"Step 'big': {BracketTooLarge}", refusal);

        // Found before anything was made: no draft of the first step, no pack, no Evidence record, no register entry.
        Assert.Empty(await host.Store.ListInstancesAsync(project));
        Assert.Empty(await host.Store.ListPacksAsync(project));
        Assert.Equal(objectsBefore, (await host.Context.Repository.ListAllAsync()).Count);
    }

    [Fact]
    public async Task A_Step_Over_The_Cap_By_Its_Identifiers_Alone_Is_Refused_At_Creation_And_The_Earlier_Drafts_Are_Taken_Away()
    {
        var template = TwoSteps();
        var host = PackHost(template);
        var project = await host.ProjectAsync();

        // The size of the Bracket as it is stored, with the grade empty: the stored text grows by one byte for each character of the grade.
        // (Measured on a host of its own, so that the Bracket's name is not taken in the project the template is instantiated in.)
        var other = PackHost(template);
        var probe = await other.Service.CreateAsync("test.direct-stress", await other.ProjectAsync(), "Bracket", StepInputs(string.Empty)["big"]);
        var empty = CalcInstanceStore.Serialise(probe).Length;
        var perCharacter = (CalcInstanceStore.Serialise(probe with { Inputs = [.. probe.Inputs.Select(i => i.Symbol == "grade" ? i with { Text = Ascii(1_000) } : i)] }).Length - empty) / 1_000;
        var stubLike = probe with { Id = Guid.Empty, ProjectId = null, EvidenceId = null, RegisterObjectId = null, CreatedBy = string.Empty, CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch, EditedBy = null };
        var identifiers = empty - CalcInstanceStore.Serialise(stubLike).Length;
        Assert.True(identifiers > 100, $"The test's premise: the project, Evidence and register identifiers and the author add {identifiers} bytes that the in-memory run does not.");

        // Over the cap by 40 bytes once the real identifiers are in, under it by 60 or more in the in-memory run that goes first.
        var grade = Ascii((CalcInstanceStore.MaxDocumentBytes - empty + 40) / perCharacter);
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Two-step pack", StepInputs(grade));

        Assert.Null(made);
        Assert.Equal($"Step 'big': {BracketTooLarge}", refusal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
        Assert.Empty(await host.Store.ListPacksAsync(project));
    }

    // ---- Finding 5: Revise checks the cap before it makes anything ----

    [Fact]
    public async Task Revising_A_Calculation_Whose_Revision_Passes_The_Cap_Is_Refused_Before_Evidence_Or_A_Register_Entry_Is_Made()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var created = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());

        // An issued calculation 5 bytes under the cap, made large by the list of people who edited it (a revision carries it on); its revision adds
        // the predecessor's identifier, so it is over.
        IReadOnlyList<string> Editors(int length) => [Ascii(length)];
        var empty = CalcInstanceStore.Serialise(created with { EditedBy = Editors(0), Status = CalcInstanceStatus.Issued }).Length;
        var issued = created with { EditedBy = Editors(CalcInstanceStore.MaxDocumentBytes - 5 - empty), Status = CalcInstanceStatus.Issued };
        Assert.Equal(CalcInstanceStore.MaxDocumentBytes - 5, CalcInstanceStore.Serialise(issued).Length);
        await host.Store.SaveInstanceAsync(issued);
        var objectsBefore = (await host.Context.Repository.ListAllAsync()).Count;

        var revised = await host.Service.ReviseAsync(created.Id);

        Assert.False(revised.Succeeded);
        Assert.Equal(BracketTooLarge, revised.Reason);
        Assert.Equal(objectsBefore, (await host.Context.Repository.ListAllAsync()).Count);
        Assert.Single(await host.Store.ListInstancesAsync(project));
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task Revising_An_Ordinary_Calculation_Still_Makes_Its_Evidence_And_Register_Entry()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var created = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(created.Id, "ISS-1", "Client Co")).Succeeded);
        var objectsBefore = (await host.Context.Repository.ListAllAsync()).Count;

        var revised = await host.Service.ReviseAsync(created.Id);

        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal("B", revised.Instance!.Revision);
        Assert.NotNull(revised.Instance.EvidenceId);
        Assert.NotNull(revised.Instance.RegisterObjectId);
        Assert.True((await host.Context.Repository.ListAllAsync()).Count > objectsBefore);
        Assert.Equal(CalcInstanceStatus.Superseded, (await host.Store.FindInstanceAsync(created.Id))!.Status);
    }

    // ---- Finding 5: the services know the refusal by its type ----

    [Fact]
    public void The_Refusal_Is_A_Type_Of_Its_Own_And_The_Library_Never_Reads_Words_In_A_Message_To_Find_It()
    {
        Assert.True(typeof(InvalidOperationException).IsAssignableFrom(typeof(CalcDocumentTooLargeException)));
        var thrown = Assert.Throws<CalcDocumentTooLargeException>(() => CalcInstanceStore.SerialiseWithin(new { Text = Ascii(CalcInstanceStore.MaxDocumentBytes + 1) }, () => "any words at all"));
        Assert.Equal("any words at all", thrown.Message);

        var library = Path.Combine(GuidedTestHelpers.RepositoryRoot, "src", "Tempest.Calc", "Tempest.Calc.Library");
        var offenders = Directory.EnumerateFiles(library, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("Message.Contains(\"larger than\"", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .ToList();
        Assert.Empty(offenders);
    }
}

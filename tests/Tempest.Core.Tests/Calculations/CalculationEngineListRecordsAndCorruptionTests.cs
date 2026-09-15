using Tempest.Core.Calculations;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Calculations;

/// <summary>
/// <see cref="CalculationEngine.ListRecordsAsync"/>, <c>ReadSummary</c> and
/// <see cref="CalculationEngine.FindRecordAsync{TResult}"/>'s own corrupted-
/// content handling — WP 21.5D. None of these paths had any coverage at
/// all: every existing engine test builds the engine with no
/// <c>recordIndex</c>, so <c>ListRecordsAsync</c> always took its own
/// "nothing configured" early return, and no existing test ever wrote
/// content a record could not be read back from.
/// </summary>
public sealed class CalculationEngineListRecordsAndCorruptionTests
{
    private sealed class SquareInput
    {
        public double X { get; init; }
    }

    private sealed class SquareResult
    {
        public double Value { get; init; }
    }

    private sealed class OtherResult
    {
        public double Different { get; init; }
    }

    private sealed class SquareCalculation : ICalculationDefinition<SquareInput, SquareResult>
    {
        public const string Id = "test.list-records.square";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Square", null, null, [], []);

        public SquareResult Calculate(SquareInput input, CalculationContext context, CancellationToken cancellationToken = default) =>
            new() { Value = input.X * input.X };
    }

    private static CalculationEngine BuildEngine(
        out EngineeringDocumentStore documentStore,
        out InMemoryPersistenceStore? recordIndex,
        out CurrentPrincipalAccessor accessor,
        bool withRecordIndex)
    {
        accessor = new CurrentPrincipalAccessor();
        documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), accessor);
        recordIndex = withRecordIndex ? new InMemoryPersistenceStore() : null;

        var engine = new CalculationEngine(documentStore, accessor, logger: null, recordIndex: recordIndex);
        engine.RegisterDefinition(new SquareCalculation());
        return engine;
    }

    // ------------------------------------------------------------
    // ListRecordsAsync — no record index configured
    // ------------------------------------------------------------

    [Fact]
    public async Task ListRecordsAsync_NoRecordIndexConfigured_ReturnsEmpty()
    {
        var engine = BuildEngine(out _, out _, out _, withRecordIndex: false);

        await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 3.0 });

        Assert.Empty(await engine.ListRecordsAsync());
    }

    // ------------------------------------------------------------
    // ListRecordsAsync — the happy path
    // ------------------------------------------------------------

    [Fact]
    public async Task ListRecordsAsync_RecordIndexConfigured_ReturnsTheExecutedCalculation()
    {
        var engine = BuildEngine(out _, out _, out var accessor, withRecordIndex: true);
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("executor-9", "executor-9"), []));

        var executed = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 4.0 });

        var summary = Assert.Single(await engine.ListRecordsAsync());

        Assert.Equal(executed.Id, summary.Id);
        Assert.Equal(SquareCalculation.Id, summary.CalculationId);
        Assert.NotEqual(default, summary.ExecutedAt);
        Assert.Equal("executor-9", summary.ExecutedByPrincipalId);
        Assert.Equal(1, summary.RevisionNumber);
        Assert.Equal(typeof(SquareResult).FullName, summary.ResultTypeName);
    }

    [Fact]
    public async Task ListRecordsAsync_NoPrincipalEstablished_SummaryRecordsUnknownExecutor()
    {
        var engine = BuildEngine(out _, out _, out _, withRecordIndex: true);

        await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 4.0 });

        var summary = Assert.Single(await engine.ListRecordsAsync());

        Assert.Equal(CalculationEngine.UnknownExecutorPrincipalId, summary.ExecutedByPrincipalId);
    }

    [Fact]
    public async Task ListRecordsAsync_MultipleRecords_OrdersNewestFirst()
    {
        var engine = BuildEngine(out _, out _, out _, withRecordIndex: true);

        var first = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 1.0 });
        var second = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 2.0 });

        var summaries = await engine.ListRecordsAsync();

        Assert.Equal(2, summaries.Count);
        var firstSummary = summaries.Single(s => s.Id == first.Id);
        var secondSummary = summaries.Single(s => s.Id == second.Id);

        // The later execution must sort ahead of the earlier one.
        Assert.True(secondSummary.ExecutedAt >= firstSummary.ExecutedAt);
        Assert.Equal(second.Id, summaries[0].Id);
    }

    // ------------------------------------------------------------
    // ListRecordsAsync — a stale or foreign index entry is skipped,
    // never aborting the whole listing
    // ------------------------------------------------------------

    [Fact]
    public async Task ListRecordsAsync_IndexEntryPointingAtNoDocument_IsSkipped()
    {
        var engine = BuildEngine(out _, out var recordIndex, out _, withRecordIndex: true);

        var goodRecord = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 5.0 });

        // A stale entry: a well-formed Guid("N") key, but no document was
        // ever created with that Id.
        await recordIndex!.WriteAsync(CalculationEngine.RecordIndexCollection, Guid.NewGuid().ToString("N"), SquareCalculation.Id);

        var summaries = await engine.ListRecordsAsync();

        var summary = Assert.Single(summaries);
        Assert.Equal(goodRecord.Id, summary.Id);
    }

    [Fact]
    public async Task ListRecordsAsync_IndexEntryWithAMalformedGuidKey_IsSkipped_NotThrown()
    {
        var engine = BuildEngine(out _, out var recordIndex, out _, withRecordIndex: true);

        var goodRecord = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 5.0 });

        await recordIndex!.WriteAsync(CalculationEngine.RecordIndexCollection, "not-a-guid", SquareCalculation.Id);

        var summaries = await engine.ListRecordsAsync();

        var summary = Assert.Single(summaries);
        Assert.Equal(goodRecord.Id, summary.Id);
    }

    [Fact]
    public async Task ListRecordsAsync_IndexEntryPointingAtAWrongKindDocument_IsExcluded()
    {
        var engine = BuildEngine(out var documentStore, out var recordIndex, out _, withRecordIndex: true);

        var goodRecord = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 5.0 });

        // Content that, if the Kind check were skipped, would parse
        // successfully and leak a foreign document into the listing.
        var foreignJson = System.Text.Json.JsonSerializer.Serialize(new { CalculationId = "not-a-real-calculation" });
        var foreignDocument = await documentStore.CreateAsync("SomeOtherDocumentKind", foreignJson);
        await recordIndex!.WriteAsync(CalculationEngine.RecordIndexCollection, foreignDocument.Id.ToString("N"), "not-a-real-calculation");

        var summaries = await engine.ListRecordsAsync();

        var summary = Assert.Single(summaries);
        Assert.Equal(goodRecord.Id, summary.Id);
    }

    [Fact]
    public async Task ListRecordsAsync_IndexEntryPointingAtUnparsableContent_IsExcluded()
    {
        var engine = BuildEngine(out var documentStore, out var recordIndex, out _, withRecordIndex: true);

        var goodRecord = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 5.0 });

        // Valid JSON, correct Kind, but no CalculationId property at all.
        var noCalculationIdDocument = await documentStore.CreateAsync(CalculationEngine.CalculationRecordDocumentKind, "{}");
        await recordIndex!.WriteAsync(CalculationEngine.RecordIndexCollection, noCalculationIdDocument.Id.ToString("N"), "whatever");

        // Not JSON at all.
        var notJsonDocument = await documentStore.CreateAsync(CalculationEngine.CalculationRecordDocumentKind, "not json at all");
        await recordIndex.WriteAsync(CalculationEngine.RecordIndexCollection, notJsonDocument.Id.ToString("N"), "whatever");

        var summaries = await engine.ListRecordsAsync();

        var summary = Assert.Single(summaries);
        Assert.Equal(goodRecord.Id, summary.Id);
    }

    // ------------------------------------------------------------
    // FindRecordAsync — corrupted or unreadable content is reported,
    // never silently treated as "no such calculation"
    // ------------------------------------------------------------

    [Fact]
    public async Task FindRecordAsync_ContentIsNotValidJson_ThrowsCalculationException_NamingTheRecordAndType()
    {
        var engine = BuildEngine(out var documentStore, out _, out _, withRecordIndex: false);

        var document = await documentStore.CreateAsync(CalculationEngine.CalculationRecordDocumentKind, "not json at all");

        var exception = await Assert.ThrowsAsync<CalculationException>(
            () => engine.FindRecordAsync<SquareResult>(document.Id));

        Assert.Contains(document.Id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SquareResult), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindRecordAsync_ContentDeserialisesToNull_ThrowsCalculationException_NamingTheRecord()
    {
        var engine = BuildEngine(out var documentStore, out _, out _, withRecordIndex: false);

        // "null" is valid JSON that deserialises to a null DTO without
        // throwing a JsonException — a distinct failure from malformed JSON.
        var document = await documentStore.CreateAsync(CalculationEngine.CalculationRecordDocumentKind, "null");

        var exception = await Assert.ThrowsAsync<CalculationException>(
            () => engine.FindRecordAsync<SquareResult>(document.Id));

        Assert.Contains(document.Id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("deserialised to nothing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindRecordAsync_WrongResultType_ThrowsCalculationException_NamingBothTypes()
    {
        var engine = BuildEngine(out _, out _, out _, withRecordIndex: false);

        var executed = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput { X = 3.0 });

        var exception = await Assert.ThrowsAsync<CalculationException>(
            () => engine.FindRecordAsync<OtherResult>(executed.Id));

        Assert.Contains(executed.Id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(SquareResult).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(OtherResult).FullName!, exception.Message, StringComparison.Ordinal);
    }
}

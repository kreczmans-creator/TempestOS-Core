using System.Text.Json;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Calculations;

/// <summary>
/// `TD-22` (typed intermediates with a bound) and `TD-29` (retained input,
/// re-run, compare), exercised directly — <see cref="CalculationEngineTests"/>
/// and <see cref="CalculationContextTests"/> cover everything this Work
/// Package left unchanged; this file covers only what it added.
/// </summary>
public sealed class CalculationInputRetentionAndReadbackTests
{
    // ------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------

    private sealed record SquareInput(double X);
    private sealed record SquareResult(double Value);

    private sealed class SquareCalculation : ICalculationDefinition<SquareInput, SquareResult>
    {
        public const string Id = "test.square";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Square", null, null, [], []);

        public SquareResult Calculate(SquareInput input, CalculationContext context, CancellationToken cancellationToken = default)
        {
            context.RecordIntermediate("input-squared", input.X * input.X);
            return new SquareResult(input.X * input.X);
        }
    }

    private static CalculationEngine BuildEngine(out EngineeringDocumentStore documentStore)
    {
        documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());
        var engine = new CalculationEngine(documentStore, new CurrentPrincipalAccessor());
        engine.RegisterDefinition(new SquareCalculation());
        return engine;
    }

    /// <summary>
    /// Writes a calculation record document directly through the document
    /// store, in the shape a record executed before `TD-29` would have had
    /// — no <c>Input</c>/<c>InputTypeName</c>/<c>PredecessorRecordId</c>
    /// fields at all, exactly as a pre-package JSON payload never had them.
    /// </summary>
    private static async Task<Guid> WritePrePackageRecordAsync(
        EngineeringDocumentStore documentStore, string calculationId, double resultValue, double intermediateValue)
    {
        var legacyJson = JsonSerializer.Serialize(new
        {
            CalculationId = calculationId,
            Result = new { Value = resultValue },
            Assumptions = Array.Empty<object>(),
            IntermediateResults = new[] { new { Name = "input-squared", Value = intermediateValue } },
            Validation = new { Outcome = (int)CalculationValidationOutcome.Valid, ConstraintChecks = Array.Empty<object>() },
            ReferencedMaterialIds = Array.Empty<string>(),
            ExecutedAt = DateTimeOffset.UtcNow,
            ExecutedByPrincipalId = "legacy",
            ResultTypeName = typeof(SquareResult).FullName,
        });

        var document = await documentStore.CreateAsync(CalculationEngine.CalculationRecordDocumentKind, legacyJson).ConfigureAwait(false);
        return document.Id;
    }

    // ------------------------------------------------------------
    // TD-22 — typed read-back
    // ------------------------------------------------------------

    [Fact]
    public void As_SameProcessValue_MatchingType_ReturnsIt()
    {
        var intermediate = new CalculationIntermediateResult("step", 4.5);

        Assert.Equal(4.5, intermediate.As<double>());
    }

    [Fact]
    public void As_SameProcessValue_MismatchedType_ThrowsCalculationReadbackException_NamingKeyAndBothTypes()
    {
        var intermediate = new CalculationIntermediateResult("step", 4.5);

        var exception = Assert.Throws<CalculationReadbackException>(() => intermediate.As<string>());

        Assert.Equal("step", exception.Key);
        Assert.Equal(typeof(string).FullName, exception.RequestedTypeName);
        Assert.Equal(typeof(double).FullName, exception.ActualTypeName);
        Assert.IsAssignableFrom<CalculationException>(exception);
    }

    [Fact]
    public void As_NeverThrowsInvalidCastException_OnMismatch()
    {
        var intermediate = new CalculationIntermediateResult("step", 4.5);

        var exception = Record.Exception(() => intermediate.As<int>());

        Assert.IsType<CalculationReadbackException>(exception);
    }

    [Fact]
    public async Task FindRecordAsync_IntermediateResult_ReadsBackAsDeclaredType_AfterPersistence()
    {
        var engine = BuildEngine(out _);
        var executed = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(3.0));

        var record = await engine.FindRecordAsync<SquareResult>(executed.Id);

        var intermediate = Assert.Single(record!.IntermediateResults);
        Assert.Equal(9.0, intermediate.As<double>());
    }

    [Fact]
    public async Task FindRecordAsync_IntermediateResult_WrongRequestedType_ThrowsCalculationReadbackException_AfterPersistence()
    {
        var engine = BuildEngine(out _);
        var executed = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(3.0));

        var record = await engine.FindRecordAsync<SquareResult>(executed.Id);
        var intermediate = Assert.Single(record!.IntermediateResults);

        Assert.Throws<CalculationReadbackException>(() => intermediate.As<string>());
    }

    // ------------------------------------------------------------
    // TD-22 — the bound
    // ------------------------------------------------------------

    [Fact]
    public void RecordIntermediate_ExceedingConfiguredCount_ThrowsCalculationBoundExceededException_NamingTheDefinition()
    {
        var context = new CalculationContext("test.bound-count", maxIntermediateResultCount: 2, maxIntermediateResultTotalSizeBytes: 1_000_000);

        context.RecordIntermediate("first", 1.0);
        context.RecordIntermediate("second", 2.0);

        var exception = Assert.Throws<CalculationBoundExceededException>(() => context.RecordIntermediate("third", 3.0));

        Assert.Equal("test.bound-count", exception.CalculationId);
        Assert.Equal(CalculationBoundKind.IntermediateResultCount, exception.Kind);
        Assert.IsAssignableFrom<CalculationException>(exception);
    }

    [Fact]
    public void RecordIntermediate_ExceedingConfiguredTotalSize_ThrowsCalculationBoundExceededException_NamingTheDefinition()
    {
        var context = new CalculationContext("test.bound-size", maxIntermediateResultCount: 1_000, maxIntermediateResultTotalSizeBytes: 16);

        var exception = Assert.Throws<CalculationBoundExceededException>(
            () => context.RecordIntermediate("a-fairly-long-intermediate-name", "a value long enough to exceed sixteen bytes on its own"));

        Assert.Equal("test.bound-size", exception.CalculationId);
        Assert.Equal(CalculationBoundKind.IntermediateResultTotalSize, exception.Kind);
    }

    [Fact]
    public void RecordIntermediate_WithinTheDefaultBound_Succeeds()
    {
        var context = new CalculationContext("test.default-bound");

        for (var i = 0; i < 50; i++)
            context.RecordIntermediate($"step-{i}", (double)i);

        Assert.Equal(50, context.IntermediateResults.Count);
    }

    [Fact]
    public void Constructor_NonPositiveMaxCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalculationContext("test.x", maxIntermediateResultCount: 0));
    }

    [Fact]
    public void Constructor_NonPositiveMaxTotalSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalculationContext("test.x", maxIntermediateResultTotalSizeBytes: 0));
    }

    [Fact]
    public async Task ExecuteAsync_DefinitionExceedingTheBound_NoRecordIsCreated()
    {
        // A local definition that records more than the default bound
        // allows — proves the refusal reaches ExecuteAsync's own caller
        // and that nothing is left behind, mirroring
        // CalculationInputInvalidException's own identical proof shape.
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());
        var engine = new CalculationEngine(documentStore, new CurrentPrincipalAccessor());
        engine.RegisterDefinition(new RunawayIntermediateCalculation());

        await Assert.ThrowsAsync<CalculationBoundExceededException>(
            () => engine.ExecuteAsync<double, double>(RunawayIntermediateCalculation.Id, 1.0));
    }

    private sealed class RunawayIntermediateCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.runaway-intermediate";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Runaway", null, null, [], []);

        public double Calculate(double input, CalculationContext context, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < CalculationContext.DefaultMaxIntermediateResultCount + 1; i++)
                context.RecordIntermediate($"step-{i}", (double)i);

            return input;
        }
    }

    // ------------------------------------------------------------
    // TD-29 — input retained, in-process and through persistence
    // ------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_RecordCarriesItsOwnInput_SameProcess()
    {
        var engine = BuildEngine(out _);

        var record = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(5.0));

        Assert.Equal(new SquareInput(5.0), record.Input);
        Assert.Equal(typeof(SquareInput).FullName, record.InputTypeName);
        Assert.Null(record.PredecessorRecordId);
    }

    [Fact]
    public async Task FindRecordAsync_InputRoundTripsThroughPersistence()
    {
        var engine = BuildEngine(out _);
        var executed = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(7.0));

        var record = await engine.FindRecordAsync<SquareResult>(executed.Id);

        Assert.NotNull(record!.Input);
        Assert.Equal(typeof(SquareInput).FullName, record.InputTypeName);

        // CalculationTypedReadback is internal — reachable directly here via
        // this test project's own InternalsVisibleTo grant (AssemblyInfo.cs),
        // the identical access every other internal Calculations type already has.
        var readBack = CalculationTypedReadback.Read<SquareInput>(record.Input!, record.InputTypeName, "Input");
        Assert.Equal(new SquareInput(7.0), readBack);
    }

    [Fact]
    public async Task FindRecordAsync_PrePackageRecord_HasNullInput_NotAnException()
    {
        var engine = BuildEngine(out var documentStore);
        var recordId = await WritePrePackageRecordAsync(documentStore, SquareCalculation.Id, resultValue: 16.0, intermediateValue: 16.0);

        var record = await engine.FindRecordAsync<SquareResult>(recordId);

        Assert.NotNull(record);
        Assert.Null(record!.Input);
        Assert.Null(record.InputTypeName);
        Assert.Null(record.PredecessorRecordId);
    }

    // ------------------------------------------------------------
    // TD-29 — ReRunAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task ReRunAsync_IdenticalInput_ProducesANewRecord_LinkedToThePredecessor_WithTheSameResult()
    {
        var engine = BuildEngine(out _);
        var original = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(6.0));

        var rerun = await engine.ReRunAsync<SquareInput, SquareResult>(original.Id);

        Assert.NotEqual(original.Id, rerun.Id);
        Assert.Equal(original.Id, rerun.PredecessorRecordId);
        Assert.Equal(original.Result, rerun.Result);
        Assert.Equal(new SquareInput(6.0), rerun.Input);
    }

    [Fact]
    public async Task ReRunAsync_ChangedInput_ProducesADifferentResult_StillLinkedToThePredecessor()
    {
        var engine = BuildEngine(out _);
        var original = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(2.0));

        var rerun = await engine.ReRunAsync<SquareInput, SquareResult>(original.Id, new SquareInput(10.0));

        Assert.Equal(original.Id, rerun.PredecessorRecordId);
        Assert.Equal(new SquareResult(100.0), rerun.Result);
        Assert.NotEqual(original.Result, rerun.Result);
    }

    [Fact]
    public async Task ReRunAsync_IdenticalInput_RecordWithNoRetainedInput_ThrowsCalculationRecordHasNoInputException()
    {
        var engine = BuildEngine(out var documentStore);
        var recordId = await WritePrePackageRecordAsync(documentStore, SquareCalculation.Id, resultValue: 4.0, intermediateValue: 4.0);

        var exception = await Assert.ThrowsAsync<CalculationRecordHasNoInputException>(
            () => engine.ReRunAsync<SquareInput, SquareResult>(recordId));

        Assert.Equal(recordId, exception.RecordId);
        Assert.IsAssignableFrom<CalculationException>(exception);
    }

    [Fact]
    public async Task ReRunAsync_ChangedInput_RecordWithNoRetainedInput_StillSucceeds()
    {
        // The changed-input overload needs no retained input at all — only
        // the "identical input" replay does.
        var engine = BuildEngine(out var documentStore);
        var recordId = await WritePrePackageRecordAsync(documentStore, SquareCalculation.Id, resultValue: 4.0, intermediateValue: 4.0);

        var rerun = await engine.ReRunAsync<SquareInput, SquareResult>(recordId, new SquareInput(3.0));

        Assert.Equal(recordId, rerun.PredecessorRecordId);
        Assert.Equal(new SquareResult(9.0), rerun.Result);
    }

    [Fact]
    public async Task ReRunAsync_UnknownRecordId_ThrowsCalculationException()
    {
        var engine = BuildEngine(out _);

        await Assert.ThrowsAsync<CalculationException>(() => engine.ReRunAsync<SquareInput, SquareResult>(Guid.NewGuid()));
    }

    // ------------------------------------------------------------
    // TD-29 — CompareAsync / CalculationComparer
    // ------------------------------------------------------------

    [Fact]
    public async Task CompareAsync_ChangedInputAndResult_ReportsBothWithOldAndNew()
    {
        var engine = BuildEngine(out _);
        var recordA = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(3.0));
        var recordB = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(4.0));

        var comparison = await engine.CompareAsync<SquareInput, SquareResult>(recordA.Id, recordB.Id);

        Assert.Equal(recordA.Id, comparison.RecordIdA);
        Assert.Equal(recordB.Id, comparison.RecordIdB);
        Assert.Null(comparison.InputComparisonNote);

        var inputDiff = Assert.Single(comparison.InputChanges);
        Assert.Equal(nameof(SquareInput.X), inputDiff.FieldName);
        Assert.Equal("3", inputDiff.OldDisplay);
        Assert.Equal("4", inputDiff.NewDisplay);

        var resultDiff = Assert.Single(comparison.ResultChanges);
        Assert.Equal(nameof(SquareResult.Value), resultDiff.FieldName);
        Assert.Equal("9", resultDiff.OldDisplay);
        Assert.Equal("16", resultDiff.NewDisplay);
    }

    [Fact]
    public async Task CompareAsync_PlainNumbers_ReadToSixSignificantFigures_TrailingZerosTrimmed()
    {
        var engine = BuildEngine(out _);
        var recordA = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(1.5));
        var recordB = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(1.1111111111));

        var comparison = await engine.CompareAsync<SquareInput, SquareResult>(recordA.Id, recordB.Id);

        var inputDiff = Assert.Single(comparison.InputChanges);
        Assert.Equal("1.5", inputDiff.OldDisplay);
        Assert.Equal("1.11111", inputDiff.NewDisplay);

        var resultDiff = Assert.Single(comparison.ResultChanges);
        Assert.Equal("2.25", resultDiff.OldDisplay);
        Assert.Equal("1.23457", resultDiff.NewDisplay);
    }

    [Fact]
    public async Task CompareAsync_SameInput_ReportsNoChanges()
    {
        var engine = BuildEngine(out _);
        var recordA = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(5.0));
        var recordB = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(5.0));

        var comparison = await engine.CompareAsync<SquareInput, SquareResult>(recordA.Id, recordB.Id);

        Assert.Empty(comparison.InputChanges);
        Assert.Empty(comparison.ResultChanges);
        Assert.Null(comparison.InputComparisonNote);
    }

    [Fact]
    public async Task CompareAsync_OneRecordPredatesInputRetention_ReportsANote_RatherThanThrowing()
    {
        var engine = BuildEngine(out var documentStore);
        var legacyId = await WritePrePackageRecordAsync(documentStore, SquareCalculation.Id, resultValue: 16.0, intermediateValue: 16.0);
        var modern = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(5.0));

        var comparison = await engine.CompareAsync<SquareInput, SquareResult>(legacyId, modern.Id);

        Assert.NotNull(comparison.InputComparisonNote);
        Assert.Contains(legacyId.ToString(), comparison.InputComparisonNote, StringComparison.Ordinal);
        Assert.Empty(comparison.InputChanges);

        // The result side is unaffected — both records have a real result.
        var resultDiff = Assert.Single(comparison.ResultChanges);
        Assert.Equal(nameof(SquareResult.Value), resultDiff.FieldName);
        Assert.Equal("16", resultDiff.OldDisplay);
        Assert.Equal("25", resultDiff.NewDisplay);
    }

    [Fact]
    public async Task CompareAsync_BothRecordsPredateInputRetention_ReportsBothIdsInTheNote()
    {
        var engine = BuildEngine(out var documentStore);
        var firstId = await WritePrePackageRecordAsync(documentStore, SquareCalculation.Id, resultValue: 1.0, intermediateValue: 1.0);
        var secondId = await WritePrePackageRecordAsync(documentStore, SquareCalculation.Id, resultValue: 4.0, intermediateValue: 4.0);

        var comparison = await engine.CompareAsync<SquareInput, SquareResult>(firstId, secondId);

        Assert.NotNull(comparison.InputComparisonNote);
        Assert.Contains(firstId.ToString(), comparison.InputComparisonNote, StringComparison.Ordinal);
        Assert.Contains(secondId.ToString(), comparison.InputComparisonNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompareAsync_UnknownRecordId_ThrowsCalculationException()
    {
        var engine = BuildEngine(out _);
        var existing = await engine.ExecuteAsync<SquareInput, SquareResult>(SquareCalculation.Id, new SquareInput(1.0));

        await Assert.ThrowsAsync<CalculationException>(
            () => engine.CompareAsync<SquareInput, SquareResult>(existing.Id, Guid.NewGuid()));
    }
}

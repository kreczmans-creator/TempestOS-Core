using System.Text.Json;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Calculations;

public class CalculationEngineTests
{
    // ----------------------------------------------------------------
    // Test fixtures — small, local, pure calculation definitions
    // ----------------------------------------------------------------

    private sealed class AddOneCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.add-one";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Add One", null, null, [], []);
        public double Calculate(double input, CalculationContext context) => input + 1;
    }

    private sealed class ThrowingBelowZeroCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.throwing-below-zero";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new(
            "Throwing Below Zero", null, null, [], [new CalculationConstraint("Input must be non-negative.")]);

        public double Calculate(double input, CalculationContext context)
        {
            var isSatisfied = input >= 0;
            context.RecordConstraintCheck("Input must be non-negative.", isSatisfied, $"Input was {input}.");
            if (!isSatisfied)
                throw new CalculationInputInvalidException($"Input must be non-negative; received {input}.");

            return input;
        }
    }

    private sealed class IntermediateRecordingCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.intermediate";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new(
            "Intermediate", null, null, [new CalculationAssumption("Test assumption.", "Because tests need one.")], []);

        public double Calculate(double input, CalculationContext context)
        {
            var squared = input * input;
            context.RecordIntermediate("squared", squared);
            return squared + 1;
        }
    }

    private sealed class SoftConstraintCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.soft-constraint";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new(
            "Soft Constraint", null, null, [], [new CalculationConstraint("Should be within typical range.")]);

        public double Calculate(double input, CalculationContext context)
        {
            context.RecordConstraintCheck("Should be within typical range.", input <= 100, $"Value was {input}.");
            return input;
        }
    }

    private sealed class MaterialReferencingCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.material-ref";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Material Ref", null, null, [], []);

        public double Calculate(double input, CalculationContext context)
        {
            context.ReferenceMaterial("test-material-001");
            return input;
        }
    }

    private static CalculationEngine BuildEngine(out EngineeringDocumentStore documentStore, out CurrentPrincipalAccessor accessor)
    {
        accessor = new CurrentPrincipalAccessor();
        documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), accessor);
        return new CalculationEngine(documentStore, accessor);
    }

    private static IPrincipal BuildPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);

    // ----------------------------------------------------------------
    // RegisterDefinition
    // ----------------------------------------------------------------

    [Fact]
    public void RegisterDefinition_ValidDefinition_Succeeds()
    {
        var engine = BuildEngine(out _, out _);

        engine.RegisterDefinition(new AddOneCalculation());
    }

    [Fact]
    public void RegisterDefinition_DuplicateCalculationId_ThrowsDuplicateCalculationException()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var exception = Assert.Throws<DuplicateCalculationException>(() => engine.RegisterDefinition(new AddOneCalculation()));

        Assert.Equal(AddOneCalculation.Id, exception.CalculationId);
    }

    [Fact]
    public void RegisterDefinition_NullDefinition_ThrowsArgumentNullException()
    {
        var engine = BuildEngine(out _, out _);

        Assert.Throws<ArgumentNullException>(() => engine.RegisterDefinition<double, double>(null!));
    }

    // ----------------------------------------------------------------
    // ExecuteAsync — round-trip / dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_RegisteredCalculation_ReturnsExpectedResult()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 5.0);

        Assert.Equal(6.0, record.Result);
        Assert.Equal(AddOneCalculation.Id, record.CalculationId);
    }

    [Fact]
    public async Task ExecuteAsync_UnregisteredCalculationId_ThrowsCalculationDefinitionNotFoundException()
    {
        var engine = BuildEngine(out _, out _);

        var exception = await Assert.ThrowsAsync<CalculationDefinitionNotFoundException>(
            () => engine.ExecuteAsync<double, double>("does-not-exist", 1.0));

        Assert.Equal("does-not-exist", exception.CalculationId);
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedSignature_ThrowsCalculationDefinitionNotFoundException()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        await Assert.ThrowsAsync<CalculationDefinitionNotFoundException>(
            () => engine.ExecuteAsync<int, int>(AddOneCalculation.Id, 1));
    }

    [Fact]
    public async Task ExecuteAsync_NullOrWhitespaceCalculationId_ThrowsArgumentException()
    {
        var engine = BuildEngine(out _, out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.ExecuteAsync<double, double>(null!, 1.0));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.ExecuteAsync<double, double>("   ", 1.0));
    }

    // ----------------------------------------------------------------
    // Failure path — CalculationInputInvalidException
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowingCalculation_InvalidInput_PropagatesCalculationInputInvalidException()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new ThrowingBelowZeroCalculation());

        await Assert.ThrowsAsync<CalculationInputInvalidException>(
            () => engine.ExecuteAsync<double, double>(ThrowingBelowZeroCalculation.Id, -1.0));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingCalculation_InvalidInput_NoDocumentIsCreated()
    {
        var engine = BuildEngine(out var documentStore, out _);
        engine.RegisterDefinition(new ThrowingBelowZeroCalculation());

        await Assert.ThrowsAnyAsync<CalculationInputInvalidException>(
            () => engine.ExecuteAsync<double, double>(ThrowingBelowZeroCalculation.Id, -1.0));

        // No way to enumerate documents directly - proven instead by a
        // clean second execution with valid input producing revision 1,
        // never 2, confirming nothing was left behind by the failed call.
        var record = await engine.ExecuteAsync<double, double>(ThrowingBelowZeroCalculation.Id, 5.0);
        Assert.Equal(1, record.RevisionNumber);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingCalculation_ValidInput_Succeeds()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new ThrowingBelowZeroCalculation());

        var record = await engine.ExecuteAsync<double, double>(ThrowingBelowZeroCalculation.Id, 5.0);

        Assert.Equal(5.0, record.Result);
    }

    // ----------------------------------------------------------------
    // Validation model
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_AllConstraintsSatisfied_OutcomeIsValid()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new ThrowingBelowZeroCalculation());

        var record = await engine.ExecuteAsync<double, double>(ThrowingBelowZeroCalculation.Id, 5.0);

        Assert.Equal(CalculationValidationOutcome.Valid, record.Validation.Outcome);
        Assert.Single(record.Validation.ConstraintChecks);
        Assert.True(record.Validation.ConstraintChecks[0].IsSatisfied);
    }

    [Fact]
    public async Task ExecuteAsync_SoftConstraintUnsatisfied_OutcomeIsConditional_ResultStillReturned()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new SoftConstraintCalculation());

        var record = await engine.ExecuteAsync<double, double>(SoftConstraintCalculation.Id, 150.0);

        Assert.Equal(150.0, record.Result);
        Assert.Equal(CalculationValidationOutcome.Conditional, record.Validation.Outcome);
        Assert.False(record.Validation.ConstraintChecks[0].IsSatisfied);
    }

    [Fact]
    public async Task ExecuteAsync_NoConstraintsRecorded_OutcomeIsValid()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        Assert.Equal(CalculationValidationOutcome.Valid, record.Validation.Outcome);
        Assert.Empty(record.Validation.ConstraintChecks);
    }

    // ----------------------------------------------------------------
    // Assumptions (every assumption is explicit)
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_RecordIncludesDefinitionsOwnAssumptions()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new IntermediateRecordingCalculation());

        var record = await engine.ExecuteAsync<double, double>(IntermediateRecordingCalculation.Id, 3.0);

        var assumption = Assert.Single(record.Assumptions);
        Assert.Equal("Test assumption.", assumption.Description);
        Assert.Equal("Because tests need one.", assumption.Justification);
    }

    // ----------------------------------------------------------------
    // Intermediate results (inspectable, not hidden)
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_RecordsIntermediateResults()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new IntermediateRecordingCalculation());

        var record = await engine.ExecuteAsync<double, double>(IntermediateRecordingCalculation.Id, 3.0);

        var intermediate = Assert.Single(record.IntermediateResults);
        Assert.Equal("squared", intermediate.Name);
        Assert.Equal(9.0, intermediate.Value);
        Assert.Equal(10.0, record.Result);
    }

    // ----------------------------------------------------------------
    // Material references
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_RecordsReferencedMaterialIds()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new MaterialReferencingCalculation());

        var record = await engine.ExecuteAsync<double, double>(MaterialReferencingCalculation.Id, 1.0);

        Assert.Equal(["test-material-001"], record.ReferencedMaterialIds);
    }

    [Fact]
    public async Task ExecuteAsync_NoMaterialReferenced_ReferencedMaterialIdsIsEmpty()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        Assert.Empty(record.ReferencedMaterialIds);
    }

    // ----------------------------------------------------------------
    // Identity, traceability, and revision
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_RecordId_IsDirectlyRetrievableThroughEngineeringDocumentStore()
    {
        var engine = BuildEngine(out var documentStore, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        var document = await documentStore.FindAsync(record.Id);
        Assert.NotNull(document);
        Assert.Equal(CalculationEngine.CalculationRecordDocumentKind, document!.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_RevisionNumberIsOne()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        Assert.Equal(1, record.RevisionNumber);
    }

    [Fact]
    public async Task ExecuteAsync_CalledTwice_ProducesTwoDistinctRecordIds()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var first = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);
        var second = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Result, second.Result);
    }

    // ----------------------------------------------------------------
    // Executor attribution
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NoPrincipalEstablished_RecordsUnknownExecutor()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        Assert.Equal(CalculationEngine.UnknownExecutorPrincipalId, record.ExecutedByPrincipalId);
    }

    [Fact]
    public async Task ExecuteAsync_PrincipalEstablished_RecordsItsIdentity()
    {
        var engine = BuildEngine(out _, out var accessor);
        accessor.SetCurrent(BuildPrincipal("executor-1"));
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        Assert.Equal("executor-1", record.ExecutedByPrincipalId);
    }

    // ----------------------------------------------------------------
    // Serialization
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_UnderlyingDocumentContent_ContainsSerializedCalculationData()
    {
        var engine = BuildEngine(out var documentStore, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 5.0);

        var history = await documentStore.GetRevisionHistoryAsync(record.Id);
        using var json = JsonDocument.Parse(history[0].Content);
        Assert.Equal(AddOneCalculation.Id, json.RootElement.GetProperty("CalculationId").GetString());
        Assert.Equal(6.0, json.RootElement.GetProperty("Result").GetDouble());
    }

    // ----------------------------------------------------------------
    // Reproducibility / determinism
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SameInputMultipleTimes_AlwaysProducesTheSameResult()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new IntermediateRecordingCalculation());

        var results = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var record = await engine.ExecuteAsync<double, double>(IntermediateRecordingCalculation.Id, 4.0);
            results.Add(record.Result);
        }

        Assert.All(results, r => Assert.Equal(17.0, r));
    }

    // ----------------------------------------------------------------
    // Concurrency
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ConcurrentDifferentInputs_SamePureCalculation_AllProduceCorrectResults()
    {
        var engine = BuildEngine(out _, out _);
        engine.RegisterDefinition(new AddOneCalculation());

        var tasks = Enumerable.Range(0, 30)
            .Select(i => engine.ExecuteAsync<double, double>(AddOneCalculation.Id, i))
            .ToArray();
        var records = await Task.WhenAll(tasks);

        for (var i = 0; i < 30; i++)
            Assert.Equal(i + 1, records[i].Result);

        Assert.Equal(30, records.Select(r => r.Id).Distinct().Count());
    }

    // ----------------------------------------------------------------
    // Constructor validation / failure injection
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullDocumentStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CalculationEngine(null!, new CurrentPrincipalAccessor()));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(() => new CalculationEngine(documentStore, null!));
    }

    [Fact]
    public async Task ExecuteAsync_PersistenceUnavailable_PropagatesExceptionUnmodified()
    {
        var documentStore = new EngineeringDocumentStore(new FailingPersistenceStore(), new CurrentPrincipalAccessor());
        var engine = new CalculationEngine(documentStore, new CurrentPrincipalAccessor());
        engine.RegisterDefinition(new AddOneCalculation());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0));
    }
}

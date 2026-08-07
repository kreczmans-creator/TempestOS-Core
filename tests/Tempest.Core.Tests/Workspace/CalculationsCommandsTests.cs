using System.Text.Json;
using Tempest.App.Workspace.Calculations;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers every `WP 9.2A` <c>IWorkspaceCommand</c>/<c>ICommand</c>
/// implementation over the Engineering Calculations Workspace, directly
/// against a real, in-memory <see cref="EngineeringDomainContext"/> and a
/// real <see cref="CalculationEngine"/>, mirroring
/// <c>MechanicalCommandsTests</c>'s own lightweight construction.
/// </summary>
public class CalculationsCommandsTests
{
    private const string SampleCalculationId = "test.double-length";

    private static EngineeringDomainContext BuildContext()
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var lifecycleTable = new LifecycleTransitionTable();
        var validationRuleSet = new ValidationRuleSet();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);
        var evidenceComposer = new EvidenceComposer(relationshipDiscovery, repository);

        return new EngineeringDomainContext(
            store, repository, relationshipRepository, lifecycleTable, validationRuleSet, evidenceComposer, principalAccessor);
    }

    private static async Task<Calculation> CreateCalculationAsync(EngineeringDomainContext context, string identifier = "CALC-1", string name = "Calculation")
    {
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", context, (doc, rev) => new Calculation(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Calculation)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<CalculationSet> CreateCalculationSetAsync(
        EngineeringDomainContext context, string identifier, string name, IReadOnlyList<Guid>? members = null)
    {
        var factory = new EngineeringObjectFactory<CalculationSet>(
            "CalculationSet", context, (doc, rev) => new CalculationSet(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty, members));

        return (CalculationSet)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    /// <summary>A test-local <see cref="ICalculationEngine"/> with one trivial definition registered — mirrors <c>DoubleLengthCalculationDefinition</c>'s own shape, kept local so these tests never depend on <c>Tempest.Samples</c>.</summary>
    private static ICalculationEngine BuildCalculationEngine(EngineeringDomainContext context)
    {
        var engine = new CalculationEngine(context.Store, context.CurrentPrincipalAccessor);
        engine.RegisterDefinition(new TestDoubleLengthDefinition());
        return engine;
    }

    // ---- CreateCalculationObjectCommand ----

    [Theory]
    [InlineData("Calculation")]
    [InlineData("CalculationSet")]
    public async Task Create_SupportedKind_Succeeds(string kind)
    {
        var context = BuildContext();
        var registry = new CalculationObjectFactoryRegistry(context);
        var handler = new CreateCalculationObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateCalculationObjectCommand(kind, "New Object"), default);

        Assert.True(result.Succeeded);
        Assert.Single(await context.Repository.ListByKindAsync(kind));
    }

    [Fact]
    public async Task Create_UnsupportedKind_Fails()
    {
        var context = BuildContext();
        var registry = new CalculationObjectFactoryRegistry(context);
        var handler = new CreateCalculationObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateCalculationObjectCommand("Part", "Not a Calculation"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_CalculationSetWithMembers_MembersAreFrozenAtConstruction()
    {
        var context = BuildContext();
        var member = await CreateCalculationAsync(context);
        var registry = new CalculationObjectFactoryRegistry(context);
        var handler = new CreateCalculationObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateCalculationObjectCommand("CalculationSet", "Set", memberCalculationIds: [member.Id]), default);

        var set = (CalculationSet)(await context.Repository.ListByKindAsync("CalculationSet")).Single();
        Assert.Equal([member.Id], set.MemberCalculationIds);
    }

    [Fact]
    public async Task Create_WithParent_MovesTheNewObjectUnderIt()
    {
        var context = BuildContext();
        var parent = await CreateCalculationAsync(context);
        var registry = new CalculationObjectFactoryRegistry(context);
        var handler = new CreateCalculationObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateCalculationObjectCommand("Calculation", "Child", parentId: parent.Id), default);

        var created = (await context.Repository.ListByKindAsync("Calculation")).Single(c => c.Id != parent.Id);
        Assert.Equal(parent.Id, ((IHasParent)created).ParentId);
    }

    // ---- RenameCalculationObjectCommand ----

    [Fact]
    public async Task Rename_KnownTarget_Succeeds()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var handler = new RenameCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameCalculationObjectCommand(calculation.Id, "Calculation", "New Name"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", calculation.DisplayName);
    }

    [Fact]
    public async Task Rename_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new RenameCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameCalculationObjectCommand(Guid.NewGuid(), "Calculation", "New Name"), default);

        Assert.False(result.Succeeded);
    }

    // ---- ReviseCalculationCommand ----

    [Fact]
    public async Task Revise_KnownTarget_RecordsANewRevision()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var handler = new ReviseCalculationCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseCalculationCommand(calculation.Id, "Calculation", "Updated method statement."), default);

        Assert.True(result.Succeeded);
        var revisions = await context.Store.GetRevisionHistoryAsync(calculation.Id);
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Updated method statement.", revisions[^1].Content);
    }

    [Fact]
    public async Task Revise_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new ReviseCalculationCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseCalculationCommand(Guid.NewGuid(), "Calculation", "content"), default);

        Assert.False(result.Succeeded);
    }

    // ---- DeleteCalculationObjectCommand ----

    [Fact]
    public async Task Delete_KnownTargetWithNoChildren_Succeeds()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var handler = new DeleteCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteCalculationObjectCommand(calculation.Id, "Calculation"), default);

        Assert.True(result.Succeeded);
        Assert.True(calculation.IsDeleted);
    }

    [Fact]
    public async Task Delete_TargetWithLiveChildren_Fails()
    {
        var context = BuildContext();
        var parent = await CreateCalculationAsync(context);
        var child = await CreateCalculationAsync(context, "CALC-2", "Child");
        await child.MoveAsync(parent.Id);
        var handler = new DeleteCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteCalculationObjectCommand(parent.Id, "Calculation"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new DeleteCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteCalculationObjectCommand(Guid.NewGuid(), "Calculation"), default);

        Assert.False(result.Succeeded);
    }

    // ---- MoveCalculationObjectCommand ----

    [Fact]
    public async Task Move_ToKnownParent_Succeeds()
    {
        var context = BuildContext();
        var parent = await CreateCalculationAsync(context);
        var child = await CreateCalculationAsync(context, "CALC-2", "Child");
        var handler = new MoveCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveCalculationObjectCommand(child.Id, "Calculation", parent.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Fails()
    {
        var context = BuildContext();
        var parent = await CreateCalculationAsync(context, "CALC-1", "Parent");
        var child = await CreateCalculationAsync(context, "CALC-2", "Child");
        await child.MoveAsync(parent.Id);
        var handler = new MoveCalculationObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveCalculationObjectCommand(parent.Id, "Calculation", child.Id), default);

        Assert.False(result.Succeeded);
    }

    // ---- CopyCalculationObjectCommand / DuplicateCalculationObjectCommand ----

    [Fact]
    public async Task Copy_KnownSource_CreatesNewObjectOfSameKindUnderTargetParent()
    {
        var context = BuildContext();
        var source = await CreateCalculationAsync(context, "CALC-1", "Original Calculation");
        var targetParent = await CreateCalculationAsync(context, "CALC-2", "Target Parent");
        var registry = new CalculationObjectFactoryRegistry(context);
        var handler = new CopyCalculationObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyCalculationObjectCommand(source.Id, "Calculation", targetParent.Id), default);

        Assert.True(result.Succeeded);
        var calculations = await context.Repository.ListByKindAsync("Calculation");
        Assert.Equal(3, calculations.Count);
        var copy = calculations.Single(c => c.Id != source.Id && c.Id != targetParent.Id);
        Assert.Equal(targetParent.Id, ((IHasParent)copy).ParentId);
        Assert.Equal("Original Calculation (Copy)", ((IHasBusinessIdentifier)copy).DisplayName);
    }

    [Fact]
    public async Task Copy_CalculationSet_MembersAreCopiedAsIs()
    {
        var context = BuildContext();
        var member = await CreateCalculationAsync(context);
        var source = await CreateCalculationSetAsync(context, "SET-1", "Original Set", [member.Id]);
        var registry = new CalculationObjectFactoryRegistry(context);
        var handler = new CopyCalculationObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyCalculationObjectCommand(source.Id, "CalculationSet", null), default);

        Assert.True(result.Succeeded);
        var copy = (CalculationSet)(await context.Repository.ListByKindAsync("CalculationSet")).Single(s => s.Id != source.Id);
        Assert.Equal([member.Id], copy.MemberCalculationIds);
    }

    [Fact]
    public async Task Duplicate_KnownSource_CreatesNewObjectUnderSameParent()
    {
        var context = BuildContext();
        var parent = await CreateCalculationAsync(context, "CALC-1", "Parent");
        var source = await CreateCalculationAsync(context, "CALC-2", "Original");
        await source.MoveAsync(parent.Id);

        var registry = new CalculationObjectFactoryRegistry(context);
        var copyHandler = new CopyCalculationObjectCommandHandler(context, registry);
        var handler = new DuplicateCalculationObjectCommandHandler(context, copyHandler);

        var result = await handler.HandleAsync(new DuplicateCalculationObjectCommand(source.Id, "Calculation"), default);

        Assert.True(result.Succeeded);
        var duplicate = (await context.Repository.ListByKindAsync("Calculation")).Single(c => c.Id != source.Id && c.Id != parent.Id);
        Assert.Equal(parent.Id, ((IHasParent)duplicate).ParentId);
    }

    // ---- SetCalculationStatusCommand ----

    [Fact]
    public async Task SetStatus_PermittedTransition_Succeeds()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var handler = new SetCalculationStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetCalculationStatusCommand(calculation.Id, "Calculation", LifecycleState.InReview), default);

        Assert.True(result.Succeeded);
        Assert.Equal(LifecycleState.InReview, calculation.Status);
    }

    [Fact]
    public async Task SetStatus_ImpermissibleTransition_Fails()
    {
        // Draft -> Released is not a permitted transition (must pass
        // through InReview/Approved first) - proves the "Lock"/"Approve"
        // Command Palette aliases really do go through the existing
        // LifecycleTransitionTable, not a bypass.
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var handler = new SetCalculationStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetCalculationStatusCommand(calculation.Id, "Calculation", LifecycleState.Released), default);

        Assert.False(result.Succeeded);
        Assert.Equal(LifecycleState.Draft, calculation.Status);
    }

    [Fact]
    public async Task SetStatus_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new SetCalculationStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetCalculationStatusCommand(Guid.NewGuid(), "Calculation", LifecycleState.InReview), default);

        Assert.False(result.Succeeded);
    }

    // ---- ExecuteCalculationCommand / RecalculateCalculationCommand ----

    [Fact]
    public async Task Execute_KnownTemplate_ProducesRecordAndLinksItToTheTarget()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var engine = BuildCalculationEngine(context);
        var templateRegistry = new CalculationTemplateRegistry(engine, context);
        templateRegistry.Register<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new TestDoubleLengthDefinition().Metadata);
        var handler = new ExecuteCalculationCommandHandler(templateRegistry);

        var inputJson = JsonSerializer.Serialize(new Quantity<Length>(5.0, LengthUnits.Metre));
        var result = await handler.HandleAsync(new ExecuteCalculationCommand(calculation.Id, "Calculation", SampleCalculationId, inputJson), default);

        Assert.True(result.Succeeded);
        var relationships = await context.RelationshipRepository.GetOutgoingAsync(calculation.Id);
        var link = Assert.Single(relationships);
        Assert.Equal(CalculationTemplateRegistry.CalculatedByRelationshipKind, link.RelationshipKind);

        var history = await CalculationRecordReader.GetResultHistoryAsync(context, calculation.Id);
        var record = Assert.Single(history);
        Assert.Equal(SampleCalculationId, record.CalculationId);
        Assert.Equal(CalculationValidationOutcome.Valid, record.Outcome);
    }

    [Fact]
    public async Task Execute_UnregisteredTemplate_Fails()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var engine = BuildCalculationEngine(context);
        var templateRegistry = new CalculationTemplateRegistry(engine, context);
        var handler = new ExecuteCalculationCommandHandler(templateRegistry);

        var result = await handler.HandleAsync(new ExecuteCalculationCommand(calculation.Id, "Calculation", "not-registered", "{}"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Execute_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var engine = BuildCalculationEngine(context);
        var templateRegistry = new CalculationTemplateRegistry(engine, context);
        templateRegistry.Register<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new TestDoubleLengthDefinition().Metadata);
        var handler = new ExecuteCalculationCommandHandler(templateRegistry);

        var inputJson = JsonSerializer.Serialize(new Quantity<Length>(5.0, LengthUnits.Metre));
        var result = await handler.HandleAsync(new ExecuteCalculationCommand(Guid.NewGuid(), "Calculation", SampleCalculationId, inputJson), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Execute_InvalidInputJson_FailsWithoutThrowing()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var engine = BuildCalculationEngine(context);
        var templateRegistry = new CalculationTemplateRegistry(engine, context);
        templateRegistry.Register<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new TestDoubleLengthDefinition().Metadata);
        var handler = new ExecuteCalculationCommandHandler(templateRegistry);

        var result = await handler.HandleAsync(new ExecuteCalculationCommand(calculation.Id, "Calculation", SampleCalculationId, "not json"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Recalculate_ComposesExecute_ProducesASecondRecord()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);
        var engine = BuildCalculationEngine(context);
        var templateRegistry = new CalculationTemplateRegistry(engine, context);
        templateRegistry.Register<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new TestDoubleLengthDefinition().Metadata);
        var executeHandler = new ExecuteCalculationCommandHandler(templateRegistry);
        var recalculateHandler = new RecalculateCalculationCommandHandler(executeHandler);

        var inputJson = JsonSerializer.Serialize(new Quantity<Length>(5.0, LengthUnits.Metre));
        await executeHandler.HandleAsync(new ExecuteCalculationCommand(calculation.Id, "Calculation", SampleCalculationId, inputJson), default);
        var result = await recalculateHandler.HandleAsync(new RecalculateCalculationCommand(calculation.Id, "Calculation", SampleCalculationId, inputJson), default);

        Assert.True(result.Succeeded);
        var history = await CalculationRecordReader.GetResultHistoryAsync(context, calculation.Id);
        Assert.Equal(2, history.Count);
    }

    /// <summary>A deliberately trivial, test-local calculation — mirrors <c>Tempest.Samples.DoubleLengthCalculationDefinition</c>'s own shape without depending on <c>Tempest.Samples</c>.</summary>
    private sealed class TestDoubleLengthDefinition : ICalculationDefinition<Quantity<Length>, Quantity<Length>>
    {
        public string CalculationId => SampleCalculationId;

        public CalculationMetadata Metadata { get; } = new(
            "Double Length (Test)", "Doubles a length.", "Test",
            [new CalculationAssumption("The input represents a valid physical length.", null)],
            [new CalculationConstraint("Input length must be positive.")]);

        public Quantity<Length> Calculate(Quantity<Length> input, CalculationContext context)
        {
            var isPositive = input.Value > 0;
            context.RecordConstraintCheck("Input length must be positive.", isPositive, $"Input value was {input.Value}.");

            if (!isPositive)
                throw new CalculationInputInvalidException($"Input length must be positive; received {input.Value}.");

            return input * 2.0;
        }
    }
}

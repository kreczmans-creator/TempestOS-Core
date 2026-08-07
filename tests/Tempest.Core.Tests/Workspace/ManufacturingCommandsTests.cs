using Tempest.App.Workspace.Manufacturing;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers every `WP 9.5A` <c>IWorkspaceCommand</c>/<c>ICommand</c>
/// implementation over the Manufacturing Workspace, directly against a
/// real, in-memory <see cref="EngineeringDomainContext"/>, mirroring
/// <c>VerificationActivityCommandsTests</c>'s own lightweight construction.
/// </summary>
public class ManufacturingCommandsTests
{
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

    private static async Task<ManufacturingOperation> CreateOperationAsync(
        EngineeringDomainContext context, string displayName = "Operation", Guid? partId = null, string? classification = "Operation")
    {
        var metadata = classification is null ? EngineeringObjectMetadata.Empty : new EngineeringObjectMetadata(Classification: classification);
        var factory = new EngineeringObjectFactory<ManufacturingOperation>(
            "ManufacturingOperation", context, (doc, rev) => new ManufacturingOperation(
                doc, rev, context, identifier: null, displayName, metadata, partId ?? Guid.NewGuid()));

        return (ManufacturingOperation)await factory.CreateAsync($"{displayName} — for test purposes.").ConfigureAwait(false);
    }

    // ---- CreateManufacturingObjectCommand ----

    [Fact]
    public async Task Create_Operation_ValidInput_Succeeds()
    {
        var context = BuildContext();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(
            new CreateManufacturingObjectCommand("ManufacturingOperation", "New Operation", partId: Guid.NewGuid(), classification: "Operation"), default);

        Assert.True(result.Succeeded);
        Assert.Single(await context.Repository.ListByKindAsync("ManufacturingOperation"));
    }

    [Fact]
    public async Task Create_Operation_MissingPartId_Fails()
    {
        var context = BuildContext();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateManufacturingObjectCommand("ManufacturingOperation", "New Operation"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_WorkInstruction_StoresManufacturingOperationId()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(
            new CreateManufacturingObjectCommand("WorkInstruction", "New Work Instruction", manufacturingOperationId: operation.Id), default);

        Assert.True(result.Succeeded);
        var created = (IWorkInstruction)(await context.Repository.ListByKindAsync("WorkInstruction")).Single();
        Assert.Equal(operation.Id, created.ManufacturingOperationId);
    }

    [Fact]
    public async Task Create_WorkInstruction_MissingManufacturingOperationId_Fails()
    {
        var context = BuildContext();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateManufacturingObjectCommand("WorkInstruction", "New Work Instruction"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_Inspection_StoresSubjectAndMethod()
    {
        var context = BuildContext();
        var subjectId = Guid.NewGuid();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(
            new CreateManufacturingObjectCommand("Inspection", "New Inspection", subjectId: subjectId, method: "Inspection"), default);

        Assert.True(result.Succeeded);
        var created = (IVerificationActivity)(await context.Repository.ListByKindAsync("Inspection")).Single();
        Assert.Equal(subjectId, created.SubjectId);
        Assert.Equal("Inspection", created.Method);
    }

    [Fact]
    public async Task Create_Inspection_MissingSubjectId_Fails()
    {
        var context = BuildContext();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateManufacturingObjectCommand("Inspection", "New Inspection", method: "Inspection"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_UnsupportedKind_Fails()
    {
        var context = BuildContext();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateManufacturingObjectCommand("Routing", "New Routing"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_Operation_WithParent_MovesTheNewObjectUnderIt()
    {
        var context = BuildContext();
        var routing = await CreateOperationAsync(context, "Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CreateManufacturingObjectCommandHandler(registry);

        await handler.HandleAsync(
            new CreateManufacturingObjectCommand("ManufacturingOperation", "Step 1", partId: Guid.NewGuid(), classification: "Operation", parentId: routing.Id), default);

        var created = (await context.Repository.ListByKindAsync("ManufacturingOperation")).Single(o => o.Id != routing.Id);
        Assert.Equal(routing.Id, ((IHasParent)created).ParentId);
    }

    // ---- RenameManufacturingObjectCommand ----

    [Fact]
    public async Task Rename_KnownTarget_Succeeds()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var handler = new RenameManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameManufacturingObjectCommand(operation.Id, "ManufacturingOperation", "New Name"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", operation.DisplayName);
    }

    [Fact]
    public async Task Rename_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new RenameManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameManufacturingObjectCommand(Guid.NewGuid(), "ManufacturingOperation", "New Name"), default);

        Assert.False(result.Succeeded);
    }

    // ---- ReviseManufacturingObjectCommand ----

    [Fact]
    public async Task Revise_KnownTarget_RecordsANewRevision()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var handler = new ReviseManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseManufacturingObjectCommand(operation.Id, "ManufacturingOperation", "Updated content."), default);

        Assert.True(result.Succeeded);
        var revisions = await context.Store.GetRevisionHistoryAsync(operation.Id);
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Updated content.", revisions[^1].Content);
    }

    [Fact]
    public async Task Revise_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new ReviseManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseManufacturingObjectCommand(Guid.NewGuid(), "ManufacturingOperation", "content"), default);

        Assert.False(result.Succeeded);
    }

    // ---- DeleteManufacturingObjectCommand ----

    [Fact]
    public async Task Delete_KnownTargetWithNoChildren_Succeeds()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var handler = new DeleteManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteManufacturingObjectCommand(operation.Id, "ManufacturingOperation"), default);

        Assert.True(result.Succeeded);
        Assert.True(operation.IsDeleted);
    }

    [Fact]
    public async Task Delete_TargetWithLiveChildren_Fails()
    {
        var context = BuildContext();
        var routing = await CreateOperationAsync(context, "Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var step = await CreateOperationAsync(context, "Step");
        await step.MoveAsync(routing.Id);
        var handler = new DeleteManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteManufacturingObjectCommand(routing.Id, "ManufacturingOperation"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new DeleteManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteManufacturingObjectCommand(Guid.NewGuid(), "ManufacturingOperation"), default);

        Assert.False(result.Succeeded);
    }

    // ---- MoveManufacturingObjectCommand ----

    [Fact]
    public async Task Move_ToKnownParent_Succeeds()
    {
        var context = BuildContext();
        var routing = await CreateOperationAsync(context, "Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var step = await CreateOperationAsync(context, "Step");
        var handler = new MoveManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveManufacturingObjectCommand(step.Id, "ManufacturingOperation", routing.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(routing.Id, step.ParentId);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Fails()
    {
        var context = BuildContext();
        var parent = await CreateOperationAsync(context, "Parent", classification: ManufacturingObjectFactoryRegistry.Routing);
        var child = await CreateOperationAsync(context, "Child");
        await child.MoveAsync(parent.Id);
        var handler = new MoveManufacturingObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveManufacturingObjectCommand(parent.Id, "ManufacturingOperation", child.Id), default);

        Assert.False(result.Succeeded);
    }

    // ---- CopyManufacturingObjectCommand / DuplicateManufacturingObjectCommand ----

    [Fact]
    public async Task Copy_Operation_PreservesPartIdAndClassification()
    {
        var context = BuildContext();
        var partId = Guid.NewGuid();
        var source = await CreateOperationAsync(context, "Original Operation", partId, ManufacturingObjectFactoryRegistry.SupplierOperation);
        var targetParent = await CreateOperationAsync(context, "Target Parent", classification: ManufacturingObjectFactoryRegistry.Routing);
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CopyManufacturingObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyManufacturingObjectCommand(source.Id, "ManufacturingOperation", targetParent.Id), default);

        Assert.True(result.Succeeded);
        var operations = await context.Repository.ListByKindAsync("ManufacturingOperation");
        Assert.Equal(3, operations.Count);
        var copy = (IManufacturingOperation)operations.Single(o => o.Id != source.Id && o.Id != targetParent.Id);
        Assert.Equal(targetParent.Id, ((IHasParent)copy).ParentId);
        Assert.Equal("Original Operation (Copy)", ((IHasBusinessIdentifier)copy).DisplayName);
        Assert.Equal(partId, copy.PartId);
        Assert.Equal(ManufacturingObjectFactoryRegistry.SupplierOperation, ((IHasMetadata)copy).Classification);
    }

    [Fact]
    public async Task Copy_WorkInstruction_PreservesManufacturingOperationId()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var source = (IWorkInstruction)await registry.CreateWorkInstructionAsync(
            null, "Source Work Instruction", "content", operation.Id, null);
        var handler = new CopyManufacturingObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyManufacturingObjectCommand(((IEngineeringObject)source).Id, "WorkInstruction", null), default);

        Assert.True(result.Succeeded);
        var copy = (IWorkInstruction)(await context.Repository.ListByKindAsync("WorkInstruction")).Single(w => ((IEngineeringObject)w).Id != ((IEngineeringObject)source).Id);
        Assert.Equal(operation.Id, copy.ManufacturingOperationId);
    }

    [Fact]
    public async Task Copy_UnknownSource_Fails()
    {
        var context = BuildContext();
        var registry = new ManufacturingObjectFactoryRegistry(context);
        var handler = new CopyManufacturingObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyManufacturingObjectCommand(Guid.NewGuid(), "ManufacturingOperation", null), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Duplicate_KnownSource_CreatesNewObjectUnderSameParent()
    {
        var context = BuildContext();
        var routing = await CreateOperationAsync(context, "Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var source = await CreateOperationAsync(context, "Original");
        await source.MoveAsync(routing.Id);

        var registry = new ManufacturingObjectFactoryRegistry(context);
        var copyHandler = new CopyManufacturingObjectCommandHandler(context, registry);
        var handler = new DuplicateManufacturingObjectCommandHandler(context, copyHandler);

        var result = await handler.HandleAsync(new DuplicateManufacturingObjectCommand(source.Id, "ManufacturingOperation"), default);

        Assert.True(result.Succeeded);
        var duplicate = (await context.Repository.ListByKindAsync("ManufacturingOperation")).Single(o => o.Id != source.Id && o.Id != routing.Id);
        Assert.Equal(routing.Id, ((IHasParent)duplicate).ParentId);
    }

    // ---- SetManufacturingObjectStatusCommand ----

    [Fact]
    public async Task SetStatus_PermittedTransition_Succeeds()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var handler = new SetManufacturingObjectStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetManufacturingObjectStatusCommand(operation.Id, "ManufacturingOperation", LifecycleState.InReview), default);

        Assert.True(result.Succeeded);
        Assert.Equal(LifecycleState.InReview, operation.Status);
    }

    [Fact]
    public async Task SetStatus_ImpermissibleTransition_Fails()
    {
        var context = BuildContext();
        var operation = await CreateOperationAsync(context);
        var handler = new SetManufacturingObjectStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetManufacturingObjectStatusCommand(operation.Id, "ManufacturingOperation", LifecycleState.Released), default);

        Assert.False(result.Succeeded);
        Assert.Equal(LifecycleState.Draft, operation.Status);
    }

    [Fact]
    public async Task SetStatus_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new SetManufacturingObjectStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetManufacturingObjectStatusCommand(Guid.NewGuid(), "ManufacturingOperation", LifecycleState.InReview), default);

        Assert.False(result.Succeeded);
    }
}

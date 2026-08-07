using Tempest.App.Workspace.Documents;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers every `WP 9.4A` <c>IWorkspaceCommand</c>/<c>ICommand</c>
/// implementation over the Engineering Documents Workspace, directly
/// against a real, in-memory <see cref="EngineeringDomainContext"/>,
/// mirroring <c>CalculationsCommandsTests</c>'s own lightweight
/// construction.
/// </summary>
public class DocumentsCommandsTests
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

    private static async Task<Document> CreateDocumentAsync(EngineeringDomainContext context, string identifier = "DOC-1", string name = "Document", string? classification = null)
    {
        var metadata = classification is null ? EngineeringObjectMetadata.Empty : new EngineeringObjectMetadata(Classification: classification);
        var factory = new EngineeringObjectFactory<Document>(
            "Document", context, (doc, rev) => new Document(doc, rev, context, identifier, name, metadata));

        return (Document)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Drawing> CreateDrawingAsync(EngineeringDomainContext context, string identifier = "DWG-1", string name = "Drawing", string? drawingNumber = "DWG-001")
    {
        var factory = new EngineeringObjectFactory<Drawing>(
            "Drawing", context, (doc, rev) => new Drawing(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty, drawingNumber));

        return (Drawing)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    // ---- CreateDocumentObjectCommand ----

    [Theory]
    [InlineData("Document")]
    [InlineData("Drawing")]
    [InlineData("CadModel")]
    public async Task Create_SupportedKind_Succeeds(string kind)
    {
        var context = BuildContext();
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateDocumentObjectCommand(kind, "New Object"), default);

        Assert.True(result.Succeeded);
        Assert.Single(await context.Repository.ListByKindAsync(kind));
    }

    [Fact]
    public async Task Create_UnsupportedKind_Fails()
    {
        var context = BuildContext();
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateDocumentObjectCommand("Part", "Not a Document"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_WithClassification_IsStoredOnMetadata()
    {
        var context = BuildContext();
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateDocumentObjectCommand("Document", "A Specification", classification: DocumentObjectFactoryRegistry.Specification), default);

        var created = (await context.Repository.ListByKindAsync("Document")).Single();
        Assert.Equal(DocumentObjectFactoryRegistry.Specification, ((IHasMetadata)created).Classification);
    }

    [Fact]
    public async Task Create_Drawing_WithDrawingNumber_IsStoredOnTheDrawing()
    {
        var context = BuildContext();
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateDocumentObjectCommand("Drawing", "GA Drawing", drawingNumber: "GA-1000"), default);

        var created = (Drawing)(await context.Repository.ListByKindAsync("Drawing")).Single();
        Assert.Equal("GA-1000", created.DrawingNumber);
    }

    [Fact]
    public async Task Create_WithParent_MovesTheNewObjectUnderIt()
    {
        var context = BuildContext();
        var parent = await CreateDocumentAsync(context);
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateDocumentObjectCommand("Document", "Child", parentId: parent.Id), default);

        var created = (await context.Repository.ListByKindAsync("Document")).Single(c => c.Id != parent.Id);
        Assert.Equal(parent.Id, ((IHasParent)created).ParentId);
    }

    // ---- RenameDocumentObjectCommand ----

    [Fact]
    public async Task Rename_KnownTarget_Succeeds()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var handler = new RenameDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameDocumentObjectCommand(document.Id, "Document", "New Name"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", document.DisplayName);
    }

    [Fact]
    public async Task Rename_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new RenameDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameDocumentObjectCommand(Guid.NewGuid(), "Document", "New Name"), default);

        Assert.False(result.Succeeded);
    }

    // ---- ReviseDocumentCommand ----

    [Fact]
    public async Task Revise_KnownTarget_RecordsANewRevision()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var handler = new ReviseDocumentCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseDocumentCommand(document.Id, "Document", "Updated content."), default);

        Assert.True(result.Succeeded);
        var revisions = await context.Store.GetRevisionHistoryAsync(document.Id);
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Updated content.", revisions[^1].Content);
    }

    [Fact]
    public async Task Revise_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new ReviseDocumentCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseDocumentCommand(Guid.NewGuid(), "Document", "content"), default);

        Assert.False(result.Succeeded);
    }

    // ---- DeleteDocumentObjectCommand ----

    [Fact]
    public async Task Delete_KnownTargetWithNoChildren_Succeeds()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var handler = new DeleteDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteDocumentObjectCommand(document.Id, "Document"), default);

        Assert.True(result.Succeeded);
        Assert.True(document.IsDeleted);
    }

    [Fact]
    public async Task Delete_TargetWithLiveChildren_Fails()
    {
        var context = BuildContext();
        var parent = await CreateDocumentAsync(context);
        var child = await CreateDocumentAsync(context, "DOC-2", "Child");
        await child.MoveAsync(parent.Id);
        var handler = new DeleteDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteDocumentObjectCommand(parent.Id, "Document"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new DeleteDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteDocumentObjectCommand(Guid.NewGuid(), "Document"), default);

        Assert.False(result.Succeeded);
    }

    // ---- MoveDocumentObjectCommand ----

    [Fact]
    public async Task Move_ToKnownParent_Succeeds()
    {
        var context = BuildContext();
        var parent = await CreateDocumentAsync(context);
        var child = await CreateDocumentAsync(context, "DOC-2", "Child");
        var handler = new MoveDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveDocumentObjectCommand(child.Id, "Document", parent.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Fails()
    {
        var context = BuildContext();
        var parent = await CreateDocumentAsync(context, "DOC-1", "Parent");
        var child = await CreateDocumentAsync(context, "DOC-2", "Child");
        await child.MoveAsync(parent.Id);
        var handler = new MoveDocumentObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveDocumentObjectCommand(parent.Id, "Document", child.Id), default);

        Assert.False(result.Succeeded);
    }

    // ---- CopyDocumentObjectCommand / DuplicateDocumentObjectCommand ----

    [Fact]
    public async Task Copy_KnownSource_CreatesNewObjectOfSameKindUnderTargetParent()
    {
        var context = BuildContext();
        var source = await CreateDocumentAsync(context, "DOC-1", "Original Document", DocumentObjectFactoryRegistry.Specification);
        var targetParent = await CreateDocumentAsync(context, "DOC-2", "Target Parent");
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CopyDocumentObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyDocumentObjectCommand(source.Id, "Document", targetParent.Id), default);

        Assert.True(result.Succeeded);
        var documents = await context.Repository.ListByKindAsync("Document");
        Assert.Equal(3, documents.Count);
        var copy = documents.Single(d => d.Id != source.Id && d.Id != targetParent.Id);
        Assert.Equal(targetParent.Id, ((IHasParent)copy).ParentId);
        Assert.Equal("Original Document (Copy)", ((IHasBusinessIdentifier)copy).DisplayName);
        Assert.Equal(DocumentObjectFactoryRegistry.Specification, ((IHasMetadata)copy).Classification);
    }

    [Fact]
    public async Task Copy_Drawing_PreservesDrawingNumber()
    {
        var context = BuildContext();
        var source = await CreateDrawingAsync(context, "DWG-1", "Original Drawing", "GA-1000");
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CopyDocumentObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyDocumentObjectCommand(source.Id, "Drawing", null), default);

        Assert.True(result.Succeeded);
        var copy = (Drawing)(await context.Repository.ListByKindAsync("Drawing")).Single(d => d.Id != source.Id);
        Assert.Equal("GA-1000", copy.DrawingNumber);
    }

    [Fact]
    public async Task Duplicate_KnownSource_CreatesNewObjectUnderSameParent()
    {
        var context = BuildContext();
        var parent = await CreateDocumentAsync(context, "DOC-1", "Parent");
        var source = await CreateDocumentAsync(context, "DOC-2", "Original");
        await source.MoveAsync(parent.Id);

        var registry = new DocumentObjectFactoryRegistry(context);
        var copyHandler = new CopyDocumentObjectCommandHandler(context, registry);
        var handler = new DuplicateDocumentObjectCommandHandler(context, copyHandler);

        var result = await handler.HandleAsync(new DuplicateDocumentObjectCommand(source.Id, "Document"), default);

        Assert.True(result.Succeeded);
        var duplicate = (await context.Repository.ListByKindAsync("Document")).Single(d => d.Id != source.Id && d.Id != parent.Id);
        Assert.Equal(parent.Id, ((IHasParent)duplicate).ParentId);
    }

    // ---- SetDocumentStatusCommand ----

    [Fact]
    public async Task SetStatus_PermittedTransition_Succeeds()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var handler = new SetDocumentStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetDocumentStatusCommand(document.Id, "Document", LifecycleState.InReview), default);

        Assert.True(result.Succeeded);
        Assert.Equal(LifecycleState.InReview, document.Status);
    }

    [Fact]
    public async Task SetStatus_ImpermissibleTransition_Fails()
    {
        // Draft -> Released is not a permitted transition (must pass
        // through InReview/Approved first).
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var handler = new SetDocumentStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetDocumentStatusCommand(document.Id, "Document", LifecycleState.Released), default);

        Assert.False(result.Succeeded);
        Assert.Equal(LifecycleState.Draft, document.Status);
    }

    [Fact]
    public async Task SetStatus_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new SetDocumentStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetDocumentStatusCommand(Guid.NewGuid(), "Document", LifecycleState.InReview), default);

        Assert.False(result.Succeeded);
    }

    // ---- AttachDocumentCommand ----

    [Fact]
    public async Task Attach_KnownTarget_Succeeds()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var handler = new AttachDocumentCommandHandler(context);

        var result = await handler.HandleAsync(new AttachDocumentCommand(document.Id, "Document", "test-report.pdf", "application/pdf", 1024), default);

        Assert.True(result.Succeeded);
        var attachments = await document.GetAttachmentsAsync();
        var attachment = Assert.Single(attachments);
        Assert.Equal("test-report.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(1024, attachment.SizeInBytes);
    }

    [Fact]
    public async Task Attach_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new AttachDocumentCommandHandler(context);

        var result = await handler.HandleAsync(new AttachDocumentCommand(Guid.NewGuid(), "Document", "file.pdf", "application/pdf", 1024), default);

        Assert.False(result.Succeeded);
    }
}

using Tempest.Workspace.Documents;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tests.EngineeringDomain;

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
        return TestEngineeringDomain.NewContext();
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

    /// <summary>
    /// Product Owner decision 2026-10-01 §3 (`ADR-0156`): a Document,
    /// Drawing or CAD model created with no identifier inside a
    /// <c>CUSTOMER-PROJECTREF</c> project — directly or below one of its
    /// members — is numbered <c>CUSTOMER-PROJECTREF-DOC|DWG|CAD-NNN</c>, a
    /// sequence per project per type from 001; inside an older project, or
    /// standalone, it stays unnumbered; a given identifier is kept.
    /// </summary>
    [Fact]
    public async Task Create_InsideAProjectCentricProject_IsNumberedPerProjectPerType_ElsewhereUnnumbered()
    {
        var context = BuildContext();
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry, context);

        var bridge = await CreateProjectAsync(context, "ACMEE-BRIDG");
        var legacy = await CreateProjectAsync(context, "P-0001");

        async Task<string?> CreateAsync(string kind, Guid? parentId, string? identifier = null)
        {
            var result = await handler.HandleAsync(new CreateDocumentObjectCommand(kind, $"{kind} under test", identifier, parentId), default);
            Assert.True(result.Succeeded, result.Message);
            return ((IHasBusinessIdentifier)(await context.Repository.FindAsync(result.SubjectId!.Value))!).Identifier;
        }

        var first = await CreateAsync(DocumentObjectFactoryRegistry.Document, bridge);
        Assert.Equal("ACMEE-BRIDG-DOC-001", first);
        Assert.Equal("ACMEE-BRIDG-DOC-002", await CreateAsync(DocumentObjectFactoryRegistry.Document, bridge));
        Assert.Equal("ACMEE-BRIDG-DWG-001", await CreateAsync(DocumentObjectFactoryRegistry.Drawing, bridge));
        Assert.Equal("ACMEE-BRIDG-CAD-001", await CreateAsync(DocumentObjectFactoryRegistry.CadModel, bridge));

        // Below a member of the project, not only directly under it.
        var firstId = (await context.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
            .Single(e => ((IHasBusinessIdentifier)context.Repository.FindAsync(e.Id).GetAwaiter().GetResult()!).Identifier == first).Id;
        Assert.Equal("ACMEE-BRIDG-DOC-003", await CreateAsync(DocumentObjectFactoryRegistry.Document, firstId));

        Assert.Null(await CreateAsync(DocumentObjectFactoryRegistry.Document, legacy));
        Assert.Null(await CreateAsync(DocumentObjectFactoryRegistry.Document, parentId: null));
        Assert.Equal("MY-DOC", await CreateAsync(DocumentObjectFactoryRegistry.Document, bridge, "MY-DOC"));
    }

    private static async Task<Guid> CreateProjectAsync(EngineeringDomainContext context, string identifier)
    {
        var factory = new EngineeringObjectFactory<Project>(
            Tempest.Workspace.Projects.ProjectDirectory.ProjectKind, context,
            (doc, rev) => new Project(doc, rev, context, identifier, $"Project {identifier}", EngineeringObjectMetadata.Empty));

        return (await factory.CreateAsync($"Project {identifier} — for test purposes.").ConfigureAwait(false)).Id;
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

        var createdEntries = await context.Repository.ListByKindAsync("Document");
        var created = (await context.Repository.MaterialiseAsync<IHasMetadata>(createdEntries)).Single();
        Assert.Equal(DocumentObjectFactoryRegistry.Specification, created.Classification);
    }

    [Fact]
    public async Task Create_Drawing_WithDrawingNumber_IsStoredOnTheDrawing()
    {
        var context = BuildContext();
        var registry = new DocumentObjectFactoryRegistry(context);
        var handler = new CreateDocumentObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateDocumentObjectCommand("Drawing", "GA Drawing", drawingNumber: "GA-1000"), default);

        var createdEntries = await context.Repository.ListByKindAsync("Drawing");
        var created = (await context.Repository.MaterialiseAsync<Drawing>(createdEntries)).Single();
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

        var created = (await context.Repository.ListByKindAsync("Document")).Single(entry => entry.Id != parent.Id);
        Assert.Equal(parent.Id, created.ParentId);
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
        var copyEntry = documents.Single(entry => entry.Id != source.Id && entry.Id != targetParent.Id);
        Assert.Equal(targetParent.Id, copyEntry.ParentId);
        Assert.Equal("Original Document (Copy)", copyEntry.DisplayName);
        var copy = await context.Repository.MaterialiseAsync<IHasMetadata>([copyEntry]);
        Assert.Equal(DocumentObjectFactoryRegistry.Specification, copy.Single().Classification);
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
        var copyEntries = await context.Repository.ListByKindAsync("Drawing");
        var copy = (await context.Repository.MaterialiseAsync<Drawing>(copyEntries)).Single(d => d.Id != source.Id);
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
        var duplicate = (await context.Repository.ListByKindAsync("Document")).Single(entry => entry.Id != source.Id && entry.Id != parent.Id);
        Assert.Equal(parent.Id, duplicate.ParentId);
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

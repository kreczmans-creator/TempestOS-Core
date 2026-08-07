using Tempest.App.Workspace;
using Tempest.App.Workspace.Documents;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers `WP 9.4A`'s four real Kind-keyed Workspace providers —
/// <see cref="DocumentsNodeProvider"/>, <see cref="DocumentsPropertyFacetProvider"/>,
/// <see cref="DocumentsWorkspaceViewFactory"/>/<see cref="DocumentsWorkspaceView"/>
/// — directly against a real, in-memory <see cref="EngineeringDomainContext"/>,
/// mirroring <c>CalculationsNodeProviderAndFacetsTests</c>'s own lightweight
/// construction.
/// </summary>
public class DocumentsNodeProviderAndFacetsTests
{
    private const string AreaKind = "tempest.documents.management";

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

    private static async Task<Document> CreateDocumentAsync(
        EngineeringDomainContext context, string identifier = "DOC-1", string name = "Document", string? classification = null)
    {
        var metadata = classification is null ? EngineeringObjectMetadata.Empty : new EngineeringObjectMetadata(Classification: classification);
        var factory = new EngineeringObjectFactory<Document>(
            "Document", context, (doc, rev) => new Document(doc, rev, context, identifier, name, metadata));

        return (Document)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Drawing> CreateDrawingAsync(
        EngineeringDomainContext context, string identifier = "DWG-1", string name = "Drawing", string? drawingNumber = "DWG-001")
    {
        var factory = new EngineeringObjectFactory<Drawing>(
            "Drawing", context, (doc, rev) => new Drawing(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty, drawingNumber));

        return (Drawing)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    // ---- DocumentCategory ----

    [Fact]
    public async Task DocumentCategory_Drawing_MapsToDrawingsCategory()
    {
        var context = BuildContext();
        var drawing = await CreateDrawingAsync(context);

        Assert.Equal("Drawings", DocumentCategory.Of(drawing));
    }

    [Theory]
    [InlineData(DocumentObjectFactoryRegistry.Specification, "Specifications")]
    [InlineData(DocumentObjectFactoryRegistry.Report, "Reports")]
    [InlineData(DocumentObjectFactoryRegistry.Procedure, "Procedures")]
    [InlineData(DocumentObjectFactoryRegistry.Standard, "Standards")]
    [InlineData(DocumentObjectFactoryRegistry.Datasheet, "Datasheets")]
    [InlineData(DocumentObjectFactoryRegistry.ExternalReference, "External References")]
    public async Task DocumentCategory_ClassifiedDocument_MapsToItsOwnCategory(string classification, string expectedCategory)
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context, classification: classification);

        Assert.Equal(expectedCategory, DocumentCategory.Of(document));
    }

    [Fact]
    public async Task DocumentCategory_UnclassifiedDocument_MapsToUncategorized()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);

        Assert.Equal("Uncategorized", DocumentCategory.Of(document));
    }

    // ---- DocumentsNodeProvider ----

    [Fact]
    public async Task GetRootNodesAsync_ReturnsOneCategoryNodePerLabel()
    {
        var context = BuildContext();
        var provider = new DocumentsNodeProvider(AreaKind, context);

        var roots = await provider.GetRootNodesAsync();

        Assert.Equal(DocumentsNodeProvider.CategoryLabels.Count, roots.Count);
        Assert.All(roots, n => Assert.Equal(ProjectExplorerNodeType.Category, n.NodeType));
    }

    [Fact]
    public async Task GetChildrenAsync_SpecificationsCategory_ReturnsOnlyLiveSpecifications()
    {
        var context = BuildContext();
        var spec = await CreateDocumentAsync(context, "DOC-1", "Live Spec", DocumentObjectFactoryRegistry.Specification);
        var deletedSpec = await CreateDocumentAsync(context, "DOC-2", "Deleted Spec", DocumentObjectFactoryRegistry.Specification);
        await deletedSpec.DeleteAsync();
        await CreateDocumentAsync(context, "DOC-3", "Report", DocumentObjectFactoryRegistry.Report);

        var provider = new DocumentsNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();
        var specificationsNode = roots.Single(n => n.Title == "Specifications");

        var children = await provider.GetChildrenAsync(specificationsNode.Id);

        var node = Assert.Single(children);
        Assert.Equal(spec.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_ParentedDocument_ReturnsItsOwnRealChildren()
    {
        var context = BuildContext();
        var gaDrawing = await CreateDrawingAsync(context, "DWG-1", "GA Drawing", "GA-1000");
        var detailDrawing = await CreateDrawingAsync(context, "DWG-2", "Detail Drawing", "DWG-2000");
        await detailDrawing.MoveAsync(gaDrawing.Id);

        var provider = new DocumentsNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(gaDrawing.Id);

        var node = Assert.Single(children);
        Assert.Equal(detailDrawing.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_UnknownNodeId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var provider = new DocumentsNodeProvider(AreaKind, context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetChildrenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAncestryAsync_ReturnsParentChainRootFirst()
    {
        var context = BuildContext();
        var grandparent = await CreateDocumentAsync(context, "DOC-1", "Grandparent");
        var parent = await CreateDocumentAsync(context, "DOC-2", "Parent");
        await parent.MoveAsync(grandparent.Id);
        var child = await CreateDocumentAsync(context, "DOC-3", "Child");
        await child.MoveAsync(parent.Id);

        var provider = new DocumentsNodeProvider(AreaKind, context);
        var ancestry = await provider.GetAncestryAsync(child.Id);

        Assert.Equal(2, ancestry.Count);
        Assert.Equal(grandparent.Id, ancestry[0].Id);
        Assert.Equal(parent.Id, ancestry[1].Id);
    }

    // ---- DocumentsPropertyFacetProvider ----

    [Fact]
    public async Task GetFacetsAsync_Document_IncludesIdentityStatusAndApprovalFacets()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context, "DOC-1", "Wing Specification", DocumentObjectFactoryRegistry.Specification);

        var provider = new DocumentsPropertyFacetProvider("Document", context);
        var facets = await provider.GetFacetsAsync(document.Id);

        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Wing Specification");
        Assert.Contains(facets, f => f.Name == "Document Number" && f.Value == "DOC-1");
        Assert.Contains(facets, f => f.Name == "Classification" && f.Value == DocumentObjectFactoryRegistry.Specification);
        Assert.Contains(facets, f => f.Name == "Status" && f.Value == "Draft");
        Assert.Contains(facets, f => f.Name == "Approved" && f.Value == "No");
        Assert.Contains(facets, f => f.Name == "Attachments" && f.Value == "(none)");
    }

    [Fact]
    public async Task GetFacetsAsync_Drawing_IncludesDrawingNumber()
    {
        var context = BuildContext();
        var drawing = await CreateDrawingAsync(context, "DWG-1", "GA Drawing", "GA-1000");

        var provider = new DocumentsPropertyFacetProvider("Drawing", context);
        var facets = await provider.GetFacetsAsync(drawing.Id);

        Assert.Contains(facets, f => f.Name == "Drawing Number" && f.Value == "GA-1000");
    }

    [Fact]
    public async Task GetFacetsAsync_ApprovedDocument_ApprovalFacetIsYes()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        await document.TransitionAsync(LifecycleState.InReview);
        await document.TransitionAsync(LifecycleState.Approved);

        var provider = new DocumentsPropertyFacetProvider("Document", context);
        var facets = await provider.GetFacetsAsync(document.Id);

        Assert.Contains(facets, f => f.Name == "Approved" && f.Value == "Yes");
    }

    [Fact]
    public async Task GetFacetsAsync_DocumentWithAttachment_IncludesAttachmentFacet()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        await document.AttachAsync(new Attachment("test-report.pdf", "application/pdf", 1024));

        var provider = new DocumentsPropertyFacetProvider("Document", context);
        var facets = await provider.GetFacetsAsync(document.Id);

        Assert.Contains(facets, f => f.Name == "Attachments" && f.Value == "test-report.pdf");
    }

    [Fact]
    public async Task GetFacetsAsync_ReferencesLink_IncludesDigitalThreadFacet()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context);
        var subject = await CreateDocumentAsync(context, "DOC-2", "Subject");
        await document.LinkAsync(subject.Id, "references");

        var provider = new DocumentsPropertyFacetProvider("Document", context);
        var facets = await provider.GetFacetsAsync(document.Id);

        Assert.Contains(facets, f => f.Name == "References (Digital Thread)" && f.Value == subject.Id.ToString());
    }

    [Fact]
    public async Task GetFacetsAsync_DocumentedByLink_IncludesDigitalThreadFacet()
    {
        var context = BuildContext();
        var subject = await CreateDocumentAsync(context, "DOC-1", "Subject");
        var drawing = await CreateDrawingAsync(context, "DWG-1", "Drawing");
        await subject.LinkAsync(drawing.Id, "documentedBy");

        var provider = new DocumentsPropertyFacetProvider("Drawing", context);
        var facets = await provider.GetFacetsAsync(drawing.Id);

        Assert.Contains(facets, f => f.Name == "Documents (Digital Thread)" && f.Value == subject.Id.ToString());
    }

    [Fact]
    public async Task GetFacetsAsync_UnknownObjectId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var provider = new DocumentsPropertyFacetProvider("Document", context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetFacetsAsync(Guid.NewGuid()));
    }

    // ---- DocumentsWorkspaceViewFactory / DocumentsWorkspaceView ----

    [Fact]
    public async Task Create_Document_ReturnsViewWithCorrectTitleAndKind()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context, "DOC-1", "Wing Specification");
        var factory = new DocumentsWorkspaceViewFactory("Document", context);

        var view = factory.Create(document.Id, new WorkspaceContext());

        Assert.Equal("Wing Specification", view.Title);
        Assert.Equal("Document", view.ObjectKind);
        Assert.False(view.IsDirty);
    }

    [Fact]
    public void Create_UnknownObjectId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var factory = new DocumentsWorkspaceViewFactory("Document", context);

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.NewGuid(), new WorkspaceContext()));
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARenameMadeAfterTheViewWasCreated()
    {
        var context = BuildContext();
        var document = await CreateDocumentAsync(context, "DOC-1", "Original Name");
        var factory = new DocumentsWorkspaceViewFactory("Document", context);
        var view = factory.Create(document.Id, new WorkspaceContext());

        await document.RenameAsync("Renamed Document");
        await view.RefreshAsync();

        Assert.Equal("Renamed Document", view.Title);
    }
}

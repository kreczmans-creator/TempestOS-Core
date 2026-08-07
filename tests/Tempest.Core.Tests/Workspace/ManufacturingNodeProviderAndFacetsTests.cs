using Tempest.App.Workspace;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers `WP 9.5A`'s real Kind-keyed Workspace providers —
/// <see cref="ManufacturingNodeProvider"/>,
/// <see cref="ManufacturingOperationPropertyFacetProvider"/>,
/// <see cref="ManufacturingWorkspaceViewFactory"/>/
/// <see cref="ManufacturingWorkspaceView"/> — plus the disclosed
/// cross-Work-Package reuse: <see cref="DocumentsPropertyFacetProvider"/>
/// constructed with <c>kind: "WorkInstruction"</c> and
/// <see cref="VerificationActivityPropertyFacetProvider"/> constructed with
/// <c>kind: "Inspection"</c> genuinely produce correct facets against a
/// real Manufacturing object, directly against a real, in-memory
/// <see cref="EngineeringDomainContext"/> and a real
/// <see cref="VerificationService"/>, mirroring
/// <c>VerificationActivityNodeProviderAndFacetsTests</c>'s own lightweight
/// construction.
/// </summary>
public class ManufacturingNodeProviderAndFacetsTests
{
    private const string AreaKind = "tempest.manufacturing.management";

    private static (EngineeringDomainContext Context, IVerificationService VerificationService) BuildContext()
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var lifecycleTable = new LifecycleTransitionTable();
        var validationRuleSet = new ValidationRuleSet();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);
        var evidenceComposer = new EvidenceComposer(relationshipDiscovery, repository);

        var context = new EngineeringDomainContext(
            store, repository, relationshipRepository, lifecycleTable, validationRuleSet, evidenceComposer, principalAccessor);

        var verificationService = new VerificationService(store, principalAccessor, new PermissionEvaluator());

        return (context, verificationService);
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

    private static async Task<WorkInstruction> CreateWorkInstructionAsync(EngineeringDomainContext context, Guid manufacturingOperationId, string displayName = "Work Instruction")
    {
        var factory = new EngineeringObjectFactory<WorkInstruction>(
            "WorkInstruction", context, (doc, rev) => new WorkInstruction(
                doc, rev, context, "WI-001", displayName, EngineeringObjectMetadata.Empty, manufacturingOperationId));

        return (WorkInstruction)await factory.CreateAsync($"{displayName} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Inspection> CreateInspectionAsync(EngineeringDomainContext context, Guid subjectId, string displayName = "Inspection")
    {
        var factory = new EngineeringObjectFactory<Inspection>(
            "Inspection", context, (doc, rev) => new Inspection(
                doc, rev, context, displayName, EngineeringObjectMetadata.Empty, subjectId, "Inspection"));

        return (Inspection)await factory.CreateAsync($"{displayName} — for test purposes.").ConfigureAwait(false);
    }

    // ---- ManufacturingCategory ----

    [Fact]
    public async Task ManufacturingCategory_RoutingClassification_MapsToRoutings()
    {
        var (context, _) = BuildContext();
        var routing = await CreateOperationAsync(context, classification: ManufacturingObjectFactoryRegistry.Routing);

        Assert.Equal("Routings", ManufacturingCategory.Of(routing));
    }

    [Fact]
    public async Task ManufacturingCategory_SupplierOperationClassification_MapsToSupplierOperations()
    {
        var (context, _) = BuildContext();
        var supplierOperation = await CreateOperationAsync(context, classification: ManufacturingObjectFactoryRegistry.SupplierOperation);

        Assert.Equal("Supplier Operations", ManufacturingCategory.Of(supplierOperation));
    }

    [Fact]
    public async Task ManufacturingCategory_UnrecognisedClassification_MapsToOperations()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context, classification: "Something Else");

        Assert.Equal("Operations", ManufacturingCategory.Of(operation));
    }

    [Fact]
    public async Task ManufacturingCategory_WorkInstruction_MapsToWorkInstructions()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context);
        var workInstruction = await CreateWorkInstructionAsync(context, operation.Id);

        Assert.Equal("Work Instructions", ManufacturingCategory.Of(workInstruction));
    }

    [Fact]
    public async Task ManufacturingCategory_Inspection_MapsToInspections()
    {
        var (context, _) = BuildContext();
        var inspection = await CreateInspectionAsync(context, Guid.NewGuid());

        Assert.Equal("Inspections", ManufacturingCategory.Of(inspection));
    }

    // ---- ManufacturingNodeProvider ----

    [Fact]
    public async Task GetRootNodesAsync_ReturnsOneCategoryNodePerLabel()
    {
        var (context, _) = BuildContext();
        var provider = new ManufacturingNodeProvider(AreaKind, context);

        var roots = await provider.GetRootNodesAsync();

        Assert.Equal(ManufacturingCategory.Labels.Count, roots.Count);
        Assert.All(roots, n => Assert.Equal(ProjectExplorerNodeType.Category, n.NodeType));
    }

    [Fact]
    public async Task GetChildrenAsync_RoutingsCategory_ReturnsOnlyLiveRoutings()
    {
        var (context, _) = BuildContext();
        var routing = await CreateOperationAsync(context, "Live Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var deletedRouting = await CreateOperationAsync(context, "Deleted Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        await deletedRouting.DeleteAsync();
        await CreateOperationAsync(context, "Plain Operation");

        var provider = new ManufacturingNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();
        var routingsNode = roots.Single(n => n.Title == "Routings");

        var children = await provider.GetChildrenAsync(routingsNode.Id);

        var node = Assert.Single(children);
        Assert.Equal(routing.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_RoutingWithSteps_ReturnsItsOwnRealChildren()
    {
        var (context, _) = BuildContext();
        var routing = await CreateOperationAsync(context, "Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var step = await CreateOperationAsync(context, "Step");
        await step.MoveAsync(routing.Id);

        var provider = new ManufacturingNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(routing.Id);

        var node = Assert.Single(children);
        Assert.Equal(step.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_UnknownNodeId_ThrowsArgumentException()
    {
        var (context, _) = BuildContext();
        var provider = new ManufacturingNodeProvider(AreaKind, context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetChildrenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAncestryAsync_ReturnsParentChainRootFirst()
    {
        var (context, _) = BuildContext();
        var routing = await CreateOperationAsync(context, "Routing", classification: ManufacturingObjectFactoryRegistry.Routing);
        var step = await CreateOperationAsync(context, "Step");
        await step.MoveAsync(routing.Id);

        var provider = new ManufacturingNodeProvider(AreaKind, context);
        var ancestry = await provider.GetAncestryAsync(step.Id);

        var node = Assert.Single(ancestry);
        Assert.Equal(routing.Id, node.Id);
    }

    // ---- ManufacturingOperationPropertyFacetProvider ----

    [Fact]
    public async Task GetFacetsAsync_Operation_IncludesPartClassificationAndStatusFacets()
    {
        var (context, _) = BuildContext();
        var partId = Guid.NewGuid();
        var operation = await CreateOperationAsync(context, "Wing Assembly Fit-Up", partId, ManufacturingObjectFactoryRegistry.Operation);

        var provider = new ManufacturingOperationPropertyFacetProvider("ManufacturingOperation", context);
        var facets = await provider.GetFacetsAsync(operation.Id);

        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Wing Assembly Fit-Up");
        Assert.Contains(facets, f => f.Name == "Part" && f.Value == partId.ToString());
        Assert.Contains(facets, f => f.Name == "Classification" && f.Value == ManufacturingObjectFactoryRegistry.Operation);
        Assert.Contains(facets, f => f.Name == "Status" && f.Value == "Draft");
        Assert.Contains(facets, f => f.Name == "Released" && f.Value == "No");
    }

    [Fact]
    public async Task GetFacetsAsync_ReleasedOperation_ReleasedFacetIsYes()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context);
        await operation.TransitionAsync(LifecycleState.InReview);
        await operation.TransitionAsync(LifecycleState.Approved);
        await operation.TransitionAsync(LifecycleState.Released);

        var provider = new ManufacturingOperationPropertyFacetProvider("ManufacturingOperation", context);
        var facets = await provider.GetFacetsAsync(operation.Id);

        Assert.Contains(facets, f => f.Name == "Released" && f.Value == "Yes");
    }

    [Fact]
    public async Task GetFacetsAsync_DigitalThreadLinks_IncludesOutgoingRelationshipFacets()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context);
        var part = await CreateOperationAsync(context, "Part Stand-in");
        var workInstruction = await CreateWorkInstructionAsync(context, operation.Id);
        var inspection = await CreateInspectionAsync(context, operation.Id);

        await operation.LinkAsync(part.Id, "references");
        await operation.LinkAsync(workInstruction.Id, "documentedBy");
        await operation.LinkAsync(inspection.Id, "verifiedBy");

        var provider = new ManufacturingOperationPropertyFacetProvider("ManufacturingOperation", context);
        var facets = await provider.GetFacetsAsync(operation.Id);

        Assert.Contains(facets, f => f.Name == "References (Digital Thread)" && f.Value == part.Id.ToString());
        Assert.Contains(facets, f => f.Name == "Documented By (Digital Thread)" && f.Value == workInstruction.Id.ToString());
        Assert.Contains(facets, f => f.Name == "Verified By (Digital Thread)" && f.Value == inspection.Id.ToString());
    }

    [Fact]
    public async Task GetFacetsAsync_UnknownObjectId_ThrowsArgumentException()
    {
        var (context, _) = BuildContext();
        var provider = new ManufacturingOperationPropertyFacetProvider("ManufacturingOperation", context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetFacetsAsync(Guid.NewGuid()));
    }

    // ---- Disclosed cross-Work-Package reuse: DocumentsPropertyFacetProvider("WorkInstruction") ----

    [Fact]
    public async Task GetFacetsAsync_WorkInstruction_ViaReusedDocumentsFacetProvider_ProducesCorrectFacets()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context);
        var workInstruction = await CreateWorkInstructionAsync(context, operation.Id, "Wing Assembly Fit-Up Work Instruction");

        var provider = new DocumentsPropertyFacetProvider("WorkInstruction", context);
        var facets = await provider.GetFacetsAsync(workInstruction.Id);

        Assert.Contains(facets, f => f.Name == "Kind" && f.Value == "WorkInstruction");
        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Wing Assembly Fit-Up Work Instruction");
        Assert.Contains(facets, f => f.Name == "Document Number" && f.Value == "WI-001");
    }

    // ---- Disclosed cross-Work-Package reuse: VerificationActivityPropertyFacetProvider("Inspection") ----

    [Fact]
    public async Task GetFacetsAsync_Inspection_ViaReusedVerificationFacetProvider_ProducesCorrectFacets()
    {
        var (context, verificationService) = BuildContext();
        var operation = await CreateOperationAsync(context);
        var inspection = await CreateInspectionAsync(context, operation.Id, "Wing Assembly Fit-Up Inspection");

        var verificationContext = new VerificationContext();
        verificationContext.RecordCriterion("Fit-up meets tolerance.", true);
        await verificationService.RecordAsync(inspection.Id, VerificationOutcome.Pass, "Inspection", verificationContext);

        var provider = new VerificationActivityPropertyFacetProvider("Inspection", context);
        var facets = await provider.GetFacetsAsync(inspection.Id);

        Assert.Contains(facets, f => f.Name == "Subject" && f.Value == operation.Id.ToString());
        Assert.Contains(facets, f => f.Name == "Method" && f.Value == "Inspection");
        Assert.Contains(facets, f => f.Name == "Latest Outcome" && f.Value == "Pass");
    }

    // ---- ManufacturingWorkspaceViewFactory / ManufacturingWorkspaceView ----

    [Fact]
    public async Task Create_Operation_ReturnsViewWithCorrectTitleAndKind()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context, "Wing Assembly Fit-Up");
        var factory = new ManufacturingWorkspaceViewFactory("ManufacturingOperation", context);

        var view = factory.Create(operation.Id, new WorkspaceContext());

        Assert.Equal("Wing Assembly Fit-Up", view.Title);
        Assert.Equal("ManufacturingOperation", view.ObjectKind);
        Assert.False(view.IsDirty);
    }

    [Fact]
    public void Create_UnknownObjectId_ThrowsArgumentException()
    {
        var (context, _) = BuildContext();
        var factory = new ManufacturingWorkspaceViewFactory("ManufacturingOperation", context);

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.NewGuid(), new WorkspaceContext()));
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARenameMadeAfterTheViewWasCreated()
    {
        var (context, _) = BuildContext();
        var operation = await CreateOperationAsync(context, "Original Name");
        var factory = new ManufacturingWorkspaceViewFactory("ManufacturingOperation", context);
        var view = factory.Create(operation.Id, new WorkspaceContext());

        await operation.RenameAsync("Renamed Operation");
        await view.RefreshAsync();

        Assert.Equal("Renamed Operation", view.Title);
    }
}

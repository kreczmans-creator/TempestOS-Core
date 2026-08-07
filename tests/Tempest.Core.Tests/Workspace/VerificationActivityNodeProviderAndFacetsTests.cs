using Tempest.App.Workspace;
using Tempest.App.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers `WP 9.3A`'s four real Kind-keyed Workspace providers —
/// <see cref="VerificationActivityNodeProvider"/>,
/// <see cref="VerificationActivityPropertyFacetProvider"/>,
/// <see cref="VerificationActivityWorkspaceViewFactory"/>/
/// <see cref="VerificationActivityWorkspaceView"/> — plus
/// <see cref="VerificationRecordReader"/>, directly against a real,
/// in-memory <see cref="EngineeringDomainContext"/> and a real
/// <see cref="VerificationService"/>, mirroring
/// <c>DocumentsNodeProviderAndFacetsTests</c>'s own lightweight
/// construction.
/// </summary>
public class VerificationActivityNodeProviderAndFacetsTests
{
    private const string AreaKind = "tempest.verification.management";

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

    private static async Task<VerificationActivity> CreateActivityAsync(
        EngineeringDomainContext context, string displayName = "Activity", string method = "Inspection", Guid? subjectId = null)
    {
        var factory = new EngineeringObjectFactory<VerificationActivity>(
            "VerificationActivity", context, (doc, rev) => new VerificationActivity(
                doc, rev, context, displayName, EngineeringObjectMetadata.Empty, subjectId ?? Guid.NewGuid(), method));

        return (VerificationActivity)await factory.CreateAsync($"{displayName} — for test purposes.").ConfigureAwait(false);
    }

    // ---- VerificationMethodCategory ----

    [Theory]
    [InlineData("Inspection")]
    [InlineData("Analysis")]
    [InlineData("Test")]
    [InlineData("Demonstration")]
    public async Task VerificationMethodCategory_NamedMethod_MapsToItsOwnCategory(string method)
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context, method: method);

        Assert.Equal(method, VerificationMethodCategory.Of(activity));
    }

    [Fact]
    public async Task VerificationMethodCategory_UnrecognisedMethod_MapsToOther()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context, method: "Simulation");

        Assert.Equal("Other", VerificationMethodCategory.Of(activity));
    }

    // ---- VerificationActivityNodeProvider ----

    [Fact]
    public async Task GetRootNodesAsync_ReturnsOneCategoryNodePerLabel()
    {
        var (context, _) = BuildContext();
        var provider = new VerificationActivityNodeProvider(AreaKind, context);

        var roots = await provider.GetRootNodesAsync();

        Assert.Equal(VerificationMethodCategory.Labels.Count, roots.Count);
        Assert.All(roots, n => Assert.Equal(ProjectExplorerNodeType.Category, n.NodeType));
    }

    [Fact]
    public async Task GetChildrenAsync_TestCategory_ReturnsOnlyLiveTestActivities()
    {
        var (context, _) = BuildContext();
        var testActivity = await CreateActivityAsync(context, "Live Test", "Test");
        var deletedActivity = await CreateActivityAsync(context, "Deleted Test", "Test");
        await deletedActivity.DeleteAsync();
        await CreateActivityAsync(context, "Analysis Activity", "Analysis");

        var provider = new VerificationActivityNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();
        var testNode = roots.Single(n => n.Title == "Test");

        var children = await provider.GetChildrenAsync(testNode.Id);

        var node = Assert.Single(children);
        Assert.Equal(testActivity.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_ParentedActivity_ReturnsItsOwnRealChildren()
    {
        var (context, _) = BuildContext();
        var parent = await CreateActivityAsync(context, "Parent");
        var child = await CreateActivityAsync(context, "Child");
        await child.MoveAsync(parent.Id);

        var provider = new VerificationActivityNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(parent.Id);

        var node = Assert.Single(children);
        Assert.Equal(child.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_UnknownNodeId_ThrowsArgumentException()
    {
        var (context, _) = BuildContext();
        var provider = new VerificationActivityNodeProvider(AreaKind, context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetChildrenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAncestryAsync_ReturnsParentChainRootFirst()
    {
        var (context, _) = BuildContext();
        var grandparent = await CreateActivityAsync(context, "Grandparent");
        var parent = await CreateActivityAsync(context, "Parent");
        await parent.MoveAsync(grandparent.Id);
        var child = await CreateActivityAsync(context, "Child");
        await child.MoveAsync(parent.Id);

        var provider = new VerificationActivityNodeProvider(AreaKind, context);
        var ancestry = await provider.GetAncestryAsync(child.Id);

        Assert.Equal(2, ancestry.Count);
        Assert.Equal(grandparent.Id, ancestry[0].Id);
        Assert.Equal(parent.Id, ancestry[1].Id);
    }

    // ---- VerificationActivityPropertyFacetProvider ----

    [Fact]
    public async Task GetFacetsAsync_Activity_IncludesSubjectMethodStatusAndApprovalFacets()
    {
        var (context, _) = BuildContext();
        var subjectId = Guid.NewGuid();
        var activity = await CreateActivityAsync(context, "Bolt Inspection", "Inspection", subjectId);

        var provider = new VerificationActivityPropertyFacetProvider("VerificationActivity", context);
        var facets = await provider.GetFacetsAsync(activity.Id);

        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Bolt Inspection");
        Assert.Contains(facets, f => f.Name == "Subject" && f.Value == subjectId.ToString());
        Assert.Contains(facets, f => f.Name == "Method" && f.Value == "Inspection");
        Assert.Contains(facets, f => f.Name == "Status" && f.Value == "Draft");
        Assert.Contains(facets, f => f.Name == "Approved" && f.Value == "No");
        Assert.Contains(facets, f => f.Name == "Result History" && f.Value == "Never recorded");
    }

    [Fact]
    public async Task GetFacetsAsync_ApprovedActivity_ApprovalFacetIsYes()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);
        await activity.TransitionAsync(LifecycleState.InReview);
        await activity.TransitionAsync(LifecycleState.Approved);

        var provider = new VerificationActivityPropertyFacetProvider("VerificationActivity", context);
        var facets = await provider.GetFacetsAsync(activity.Id);

        Assert.Contains(facets, f => f.Name == "Approved" && f.Value == "Yes");
    }

    [Fact]
    public async Task GetFacetsAsync_RecordedActivity_IncludesLatestOutcomeAndCriteriaAndEvidence()
    {
        var (context, verificationService) = BuildContext();
        var activity = await CreateActivityAsync(context, "Activity", "Test");

        var verificationContext = new VerificationContext();
        verificationContext.RecordCriterion("Load withstood.", true);
        verificationContext.RecordEvidence("Test report.", "report-001.pdf");
        await verificationService.RecordAsync(activity.Id, VerificationOutcome.Pass, "Test", verificationContext);

        var provider = new VerificationActivityPropertyFacetProvider("VerificationActivity", context);
        var facets = await provider.GetFacetsAsync(activity.Id);

        Assert.Contains(facets, f => f.Name == "Result History" && f.Value == "1 record(s)");
        Assert.Contains(facets, f => f.Name == "Latest Outcome" && f.Value == "Pass");
        Assert.Contains(facets, f => f.Name == "Latest Criteria" && f.Value.Contains("Load withstood."));
        Assert.Contains(facets, f => f.Name == "Latest Evidence" && f.Value.Contains("Test report."));
    }

    [Fact]
    public async Task GetFacetsAsync_DigitalThreadLink_IncludesVerifiesFacet()
    {
        var (context, _) = BuildContext();
        var subject = await CreateActivityAsync(context, "Subject Stand-in");
        var activity = await CreateActivityAsync(context, "Activity");
        await subject.LinkAsync(activity.Id, "verifiedBy");

        var provider = new VerificationActivityPropertyFacetProvider("VerificationActivity", context);
        var facets = await provider.GetFacetsAsync(activity.Id);

        Assert.Contains(facets, f => f.Name == "Verifies (Digital Thread)" && f.Value == subject.Id.ToString());
    }

    [Fact]
    public async Task GetFacetsAsync_UnknownObjectId_ThrowsArgumentException()
    {
        var (context, _) = BuildContext();
        var provider = new VerificationActivityPropertyFacetProvider("VerificationActivity", context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetFacetsAsync(Guid.NewGuid()));
    }

    // ---- VerificationActivityWorkspaceViewFactory / VerificationActivityWorkspaceView ----

    [Fact]
    public async Task Create_Activity_ReturnsViewWithCorrectTitleAndKind()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context, "Bolt Inspection");
        var factory = new VerificationActivityWorkspaceViewFactory("VerificationActivity", context);

        var view = factory.Create(activity.Id, new WorkspaceContext());

        Assert.Equal("Bolt Inspection", view.Title);
        Assert.Equal("VerificationActivity", view.ObjectKind);
        Assert.False(view.IsDirty);
    }

    [Fact]
    public void Create_UnknownObjectId_ThrowsArgumentException()
    {
        var (context, _) = BuildContext();
        var factory = new VerificationActivityWorkspaceViewFactory("VerificationActivity", context);

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.NewGuid(), new WorkspaceContext()));
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARenameMadeAfterTheViewWasCreated()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context, "Original Name");
        var factory = new VerificationActivityWorkspaceViewFactory("VerificationActivity", context);
        var view = factory.Create(activity.Id, new WorkspaceContext());

        await activity.RenameAsync("Renamed Activity");
        await view.RefreshAsync();

        Assert.Equal("Renamed Activity", view.Title);
    }

    // ---- VerificationRecordReader ----

    [Fact]
    public async Task GetResultHistoryAsync_NoRecords_ReturnsEmpty()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);

        var history = await VerificationRecordReader.GetResultHistoryAsync(context, activity.Id);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetLatestAsync_MultipleRecords_ReturnsTheMostRecentOne()
    {
        var (context, verificationService) = BuildContext();
        var activity = await CreateActivityAsync(context);

        await verificationService.RecordAsync(activity.Id, VerificationOutcome.Fail, "Test", new VerificationContext());
        await verificationService.RecordAsync(activity.Id, VerificationOutcome.Pass, "Test", new VerificationContext());

        var latest = await VerificationRecordReader.GetLatestAsync(context, activity.Id);

        Assert.NotNull(latest);
        Assert.Equal(VerificationOutcome.Pass, latest!.Outcome);
    }
}

using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

public class EngineeringDomainFrameworkTests
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

    private static async Task<Portfolio> CreateSamplePortfolioAsync(EngineeringDomainContext context)
    {
        var factory = new EngineeringObjectFactory<Portfolio>(
            "Portfolio", context, (doc, rev) => new Portfolio(doc, rev, context, "PORT-1", "Sample Portfolio", EngineeringObjectMetadata.Empty));

        return (Portfolio)await factory.CreateAsync("Initial content.").ConfigureAwait(false);
    }

    // ---- EngineeringObjectFactory<T> ----

    [Fact]
    public async Task CreateAsync_CreatesObjectWithCorrectKindAndRegistersInRepository()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        Assert.Equal("Portfolio", portfolio.Kind);
        Assert.Equal(1, portfolio.CurrentRevisionNumber);
        Assert.Equal("PORT-1", portfolio.Identifier);

        var found = await context.Repository.FindAsync(portfolio.Id);
        Assert.Same(portfolio, found);
    }

    [Fact]
    public async Task CreateAsync_TwoDifferentKinds_AreIndependentlyQueryableByKind()
    {
        var context = BuildContext();
        await CreateSamplePortfolioAsync(context);
        await CreateSamplePortfolioAsync(context);

        var portfolios = await context.Repository.ListByKindAsync("Portfolio");
        var programmes = await context.Repository.ListByKindAsync("Programme");

        Assert.Equal(2, portfolios.Count);
        Assert.Empty(programmes);
    }

    // ---- Lifecycle ----

    [Fact]
    public async Task TransitionAsync_PermittedTransition_UpdatesStatusAndAppendsHistory()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        Assert.Equal(LifecycleState.Draft, portfolio.Status);

        await portfolio.TransitionAsync(LifecycleState.InReview);

        Assert.Equal(LifecycleState.InReview, portfolio.Status);
        Assert.Single(portfolio.History);
        Assert.Equal(LifecycleState.Draft, portfolio.History[0].From);
        Assert.Equal(LifecycleState.InReview, portfolio.History[0].To);
    }

    [Fact]
    public async Task TransitionAsync_NotPermitted_ThrowsInvalidLifecycleTransitionException()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        await Assert.ThrowsAsync<InvalidLifecycleTransitionException>(
            () => portfolio.TransitionAsync(LifecycleState.Released));
    }

    [Theory]
    [InlineData(LifecycleState.Archived)]
    [InlineData(LifecycleState.Cancelled)]
    public void GetPermittedTargets_TerminalState_ReturnsEmpty(LifecycleState terminal)
    {
        var table = new LifecycleTransitionTable();

        Assert.Empty(table.GetPermittedTargets(terminal));
    }

    [Fact]
    public void IsPermitted_SameToSame_IsNeverPermitted()
    {
        var table = new LifecycleTransitionTable();

        Assert.False(table.IsPermitted(LifecycleState.Draft, LifecycleState.Draft));
    }

    // ---- Revisions ----

    [Fact]
    public async Task ReviseAsync_ReturnsNewInstanceWithUpdatedContentAndHigherRevisionNumber()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        var revised = await portfolio.ReviseAsync("Revised content.", "Sample revision.");

        Assert.Equal("Revised content.", revised.Content);
        Assert.Equal(2, ((Portfolio)revised).CurrentRevisionNumber);
        Assert.Equal("Initial content.", portfolio.Content);
        Assert.Equal(1, portfolio.CurrentRevisionNumber);
    }

    [Fact]
    public async Task ReviseAsync_RevisedInstance_CanItselfBeRevisedAgain()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        var revised = await portfolio.ReviseAsync("Second content.", null);
        var revisedAgain = await revised.ReviseAsync("Third content.", null);

        Assert.Equal("Third content.", revisedAgain.Content);
        Assert.Equal(3, ((Portfolio)revisedAgain).CurrentRevisionNumber);
    }

    [Fact]
    public async Task GetRevisionHistoryAsync_ReturnsEveryRevisionInOrder()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);
        await portfolio.ReviseAsync("Second content.", null);

        var history = await portfolio.GetRevisionHistoryAsync();

        Assert.Equal(2, history.Count);
        Assert.Equal(1, history[0].RevisionNumber);
        Assert.Equal(2, history[1].RevisionNumber);
        Assert.Equal("Second content.", history[1].Content);
    }

    // ---- Relationships ----

    [Fact]
    public async Task LinkAsync_SelfReference_ThrowsSelfReferentialRelationshipException()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        await Assert.ThrowsAsync<SelfReferentialRelationshipException>(
            () => portfolio.LinkAsync(portfolio.Id, "relatedTo"));
    }

    [Fact]
    public async Task LinkAsync_RecordsRelationshipWithInferredCategory()
    {
        var context = BuildContext();
        var source = await CreateSamplePortfolioAsync(context);
        var target = await CreateSamplePortfolioAsync(context);

        await source.LinkAsync(target.Id, "allocatedTo");

        var relationships = await source.GetRelationshipsAsync();

        Assert.Single(relationships);
        Assert.Equal(target.Id, relationships[0].TargetId);
        Assert.Equal("allocatedTo", relationships[0].RelationshipKind);
        Assert.Equal(RelationshipCategory.Allocation, relationships[0].Category);
    }

    [Fact]
    public async Task LinkAsync_UnrecognisedKind_DefaultsToReferenceCategory()
    {
        var context = BuildContext();
        var source = await CreateSamplePortfolioAsync(context);
        var target = await CreateSamplePortfolioAsync(context);

        await source.LinkAsync(target.Id, "someBespokeKind");

        var relationships = await source.GetRelationshipsAsync();

        Assert.Equal(RelationshipCategory.Reference, relationships[0].Category);
    }

    // ---- Validation ----

    [Fact]
    public async Task ValidateAsync_NoRulesRegistered_ReturnsValid()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);

        var result = await portfolio.ValidateAsync();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ---- Attachments ----

    [Fact]
    public async Task AttachAsync_GetAttachmentsAsync_RoundTrips()
    {
        var context = BuildContext();
        var portfolio = await CreateSamplePortfolioAsync(context);
        var attachment = new Attachment("drawing.pdf", "application/pdf", 1024);

        await portfolio.AttachAsync(attachment);
        var attachments = await portfolio.GetAttachmentsAsync();

        Assert.Single(attachments);
        Assert.Same(attachment, attachments[0]);
    }

    // ---- Digital Thread ----

    [Fact]
    public async Task TraverseAsync_FollowsCategoryAndRespectsMaxDepth()
    {
        var context = BuildContext();
        var grandparent = await CreateSamplePortfolioAsync(context);
        var parent = await CreateSamplePortfolioAsync(context);
        var child = await CreateSamplePortfolioAsync(context);

        await grandparent.LinkAsync(parent.Id, "groupedUnder");
        await parent.LinkAsync(child.Id, "groupedUnder");

        var discovery = new RelationshipDiscoveryService(context.RelationshipRepository, context.Repository);

        var depthOne = await discovery.TraverseAsync(grandparent.Id, RelationshipCategory.Composition, maxDepth: 1);
        var depthTwo = await discovery.TraverseAsync(grandparent.Id, RelationshipCategory.Composition, maxDepth: 2);

        Assert.Single(depthOne);
        Assert.Equal(parent.Id, depthOne[0].Id);
        Assert.Equal(2, depthTwo.Count);
    }

    [Fact]
    public async Task GetImpactedObjectsAsync_TraversesIncomingOverImpactCategoriesOnly()
    {
        var context = BuildContext();
        var subject = await CreateSamplePortfolioAsync(context);
        var dependant = await CreateSamplePortfolioAsync(context);
        var unrelated = await CreateSamplePortfolioAsync(context);

        await dependant.LinkAsync(subject.Id, "dependsOn");
        await unrelated.LinkAsync(subject.Id, "groupedUnder");

        var discovery = new RelationshipDiscoveryService(context.RelationshipRepository, context.Repository);
        var impacted = await discovery.GetImpactedObjectsAsync(subject.Id);

        Assert.Single(impacted);
        Assert.Equal(dependant.Id, impacted[0].Id);
    }

    // ---- Evidence ----

    [Fact]
    public async Task EvidenceComposer_ComposeAsync_SupportingRelationshipsIncludesEveryOutgoingLink()
    {
        var context = BuildContext();
        var subject = await CreateSamplePortfolioAsync(context);
        var target = await CreateSamplePortfolioAsync(context);
        await subject.LinkAsync(target.Id, "relatedTo");

        var evidence = await subject.GetEvidenceAsync();

        Assert.Equal(subject.Id, evidence.SubjectId);
        Assert.Single(evidence.SupportingRelationships);
        Assert.Empty(evidence.VerificationResults);
        Assert.Empty(evidence.CalculationResults);
    }

    // ---- Reference integrity ----

    [Fact]
    public async Task ReferenceIntegrityChecker_MissingTarget_ReturnsError()
    {
        var context = BuildContext();
        var source = await CreateSamplePortfolioAsync(context);
        var checker = new ReferenceIntegrityChecker(context.Repository);

        var relationship = new EngineeringRelationship(source.Id, Guid.NewGuid(), "relatedTo", RelationshipCategory.Reference, "tester", DateTimeOffset.UtcNow);
        var result = await checker.CheckAsync(relationship);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ReferenceIntegrityChecker_BothEndsExist_ReturnsValid()
    {
        var context = BuildContext();
        var source = await CreateSamplePortfolioAsync(context);
        var target = await CreateSamplePortfolioAsync(context);
        var checker = new ReferenceIntegrityChecker(context.Repository);

        var relationship = new EngineeringRelationship(source.Id, target.Id, "relatedTo", RelationshipCategory.Reference, "tester", DateTimeOffset.UtcNow);
        var result = await checker.CheckAsync(relationship);

        Assert.True(result.IsValid);
    }
}

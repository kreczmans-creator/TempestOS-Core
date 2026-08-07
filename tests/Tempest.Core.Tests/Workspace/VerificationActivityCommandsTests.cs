using Tempest.App.Workspace.Verification;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers every `WP 9.3A` <c>IWorkspaceCommand</c>/<c>ICommand</c>
/// implementation over the Verification Management Workspace, directly
/// against a real, in-memory <see cref="EngineeringDomainContext"/> and a
/// real <see cref="VerificationService"/>, mirroring
/// <c>DocumentsCommandsTests</c>'s own lightweight construction.
/// </summary>
public class VerificationActivityCommandsTests
{
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

        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(store, principalAccessor, permissionEvaluator);

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

    // ---- CreateVerificationActivityCommand ----

    [Fact]
    public async Task Create_ValidInput_Succeeds()
    {
        var (context, _) = BuildContext();
        var registry = new VerificationActivityFactoryRegistry(context);
        var handler = new CreateVerificationActivityCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateVerificationActivityCommand("New Activity", Guid.NewGuid(), "Inspection"), default);

        Assert.True(result.Succeeded);
        Assert.Single(await context.Repository.ListByKindAsync("VerificationActivity"));
    }

    [Fact]
    public async Task Create_StoresSubjectAndMethod()
    {
        var (context, _) = BuildContext();
        var subjectId = Guid.NewGuid();
        var registry = new VerificationActivityFactoryRegistry(context);
        var handler = new CreateVerificationActivityCommandHandler(registry);

        await handler.HandleAsync(new CreateVerificationActivityCommand("New Activity", subjectId, "Analysis"), default);

        var created = (IVerificationActivity)(await context.Repository.ListByKindAsync("VerificationActivity")).Single();
        Assert.Equal(subjectId, created.SubjectId);
        Assert.Equal("Analysis", created.Method);
    }

    [Fact]
    public async Task Create_WithParent_MovesTheNewObjectUnderIt()
    {
        var (context, _) = BuildContext();
        var parent = await CreateActivityAsync(context);
        var registry = new VerificationActivityFactoryRegistry(context);
        var handler = new CreateVerificationActivityCommandHandler(registry);

        await handler.HandleAsync(new CreateVerificationActivityCommand("Child", Guid.NewGuid(), "Test", parent.Id), default);

        var created = (await context.Repository.ListByKindAsync("VerificationActivity")).Single(c => c.Id != parent.Id);
        Assert.Equal(parent.Id, ((IHasParent)created).ParentId);
    }

    // ---- RenameVerificationActivityCommand ----

    [Fact]
    public async Task Rename_KnownTarget_Succeeds()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);
        var handler = new RenameVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new RenameVerificationActivityCommand(activity.Id, "VerificationActivity", "New Name"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", activity.DisplayName);
    }

    [Fact]
    public async Task Rename_UnknownTarget_Fails()
    {
        var (context, _) = BuildContext();
        var handler = new RenameVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new RenameVerificationActivityCommand(Guid.NewGuid(), "VerificationActivity", "New Name"), default);

        Assert.False(result.Succeeded);
    }

    // ---- ReviseVerificationActivityCommand ----

    [Fact]
    public async Task Revise_KnownTarget_RecordsANewRevision()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);
        var handler = new ReviseVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseVerificationActivityCommand(activity.Id, "VerificationActivity", "Updated content."), default);

        Assert.True(result.Succeeded);
        var revisions = await context.Store.GetRevisionHistoryAsync(activity.Id);
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Updated content.", revisions[^1].Content);
    }

    [Fact]
    public async Task Revise_UnknownTarget_Fails()
    {
        var (context, _) = BuildContext();
        var handler = new ReviseVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new ReviseVerificationActivityCommand(Guid.NewGuid(), "VerificationActivity", "content"), default);

        Assert.False(result.Succeeded);
    }

    // ---- DeleteVerificationActivityCommand ----

    [Fact]
    public async Task Delete_KnownTargetWithNoChildren_Succeeds()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);
        var handler = new DeleteVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteVerificationActivityCommand(activity.Id, "VerificationActivity"), default);

        Assert.True(result.Succeeded);
        Assert.True(activity.IsDeleted);
    }

    [Fact]
    public async Task Delete_TargetWithLiveChildren_Fails()
    {
        var (context, _) = BuildContext();
        var parent = await CreateActivityAsync(context);
        var child = await CreateActivityAsync(context, "Child");
        await child.MoveAsync(parent.Id);
        var handler = new DeleteVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteVerificationActivityCommand(parent.Id, "VerificationActivity"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_UnknownTarget_Fails()
    {
        var (context, _) = BuildContext();
        var handler = new DeleteVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteVerificationActivityCommand(Guid.NewGuid(), "VerificationActivity"), default);

        Assert.False(result.Succeeded);
    }

    // ---- MoveVerificationActivityCommand ----

    [Fact]
    public async Task Move_ToKnownParent_Succeeds()
    {
        var (context, _) = BuildContext();
        var parent = await CreateActivityAsync(context);
        var child = await CreateActivityAsync(context, "Child");
        var handler = new MoveVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new MoveVerificationActivityCommand(child.Id, "VerificationActivity", parent.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Fails()
    {
        var (context, _) = BuildContext();
        var parent = await CreateActivityAsync(context, "Parent");
        var child = await CreateActivityAsync(context, "Child");
        await child.MoveAsync(parent.Id);
        var handler = new MoveVerificationActivityCommandHandler(context);

        var result = await handler.HandleAsync(new MoveVerificationActivityCommand(parent.Id, "VerificationActivity", child.Id), default);

        Assert.False(result.Succeeded);
    }

    // ---- CopyVerificationActivityCommand / DuplicateVerificationActivityCommand ----

    [Fact]
    public async Task Copy_KnownSource_CreatesNewObjectPreservingSubjectAndMethod()
    {
        var (context, _) = BuildContext();
        var subjectId = Guid.NewGuid();
        var source = await CreateActivityAsync(context, "Original Activity", "Demonstration", subjectId);
        var targetParent = await CreateActivityAsync(context, "Target Parent");
        var registry = new VerificationActivityFactoryRegistry(context);
        var handler = new CopyVerificationActivityCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyVerificationActivityCommand(source.Id, "VerificationActivity", targetParent.Id), default);

        Assert.True(result.Succeeded);
        var activities = await context.Repository.ListByKindAsync("VerificationActivity");
        Assert.Equal(3, activities.Count);
        var copy = (IVerificationActivity)activities.Single(a => a.Id != source.Id && a.Id != targetParent.Id);
        Assert.Equal(targetParent.Id, ((IHasParent)copy).ParentId);
        Assert.Equal("Original Activity (Copy)", ((IHasBusinessIdentifier)copy).DisplayName);
        Assert.Equal(subjectId, copy.SubjectId);
        Assert.Equal("Demonstration", copy.Method);
    }

    [Fact]
    public async Task Copy_NonVerificationActivitySource_Fails()
    {
        var (context, _) = BuildContext();
        var registry = new VerificationActivityFactoryRegistry(context);
        var handler = new CopyVerificationActivityCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyVerificationActivityCommand(Guid.NewGuid(), "VerificationActivity", null), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Duplicate_KnownSource_CreatesNewObjectUnderSameParent()
    {
        var (context, _) = BuildContext();
        var parent = await CreateActivityAsync(context, "Parent");
        var source = await CreateActivityAsync(context, "Original");
        await source.MoveAsync(parent.Id);

        var registry = new VerificationActivityFactoryRegistry(context);
        var copyHandler = new CopyVerificationActivityCommandHandler(context, registry);
        var handler = new DuplicateVerificationActivityCommandHandler(context, copyHandler);

        var result = await handler.HandleAsync(new DuplicateVerificationActivityCommand(source.Id, "VerificationActivity"), default);

        Assert.True(result.Succeeded);
        var duplicate = (await context.Repository.ListByKindAsync("VerificationActivity")).Single(a => a.Id != source.Id && a.Id != parent.Id);
        Assert.Equal(parent.Id, ((IHasParent)duplicate).ParentId);
    }

    // ---- SetVerificationActivityStatusCommand ----

    [Fact]
    public async Task SetStatus_PermittedTransition_Succeeds()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);
        var handler = new SetVerificationActivityStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetVerificationActivityStatusCommand(activity.Id, "VerificationActivity", LifecycleState.InReview), default);

        Assert.True(result.Succeeded);
        Assert.Equal(LifecycleState.InReview, activity.Status);
    }

    [Fact]
    public async Task SetStatus_ImpermissibleTransition_Fails()
    {
        var (context, _) = BuildContext();
        var activity = await CreateActivityAsync(context);
        var handler = new SetVerificationActivityStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetVerificationActivityStatusCommand(activity.Id, "VerificationActivity", LifecycleState.Released), default);

        Assert.False(result.Succeeded);
        Assert.Equal(LifecycleState.Draft, activity.Status);
    }

    [Fact]
    public async Task SetStatus_UnknownTarget_Fails()
    {
        var (context, _) = BuildContext();
        var handler = new SetVerificationActivityStatusCommandHandler(context);

        var result = await handler.HandleAsync(new SetVerificationActivityStatusCommand(Guid.NewGuid(), "VerificationActivity", LifecycleState.InReview), default);

        Assert.False(result.Succeeded);
    }

    // ---- RecordVerificationResultCommand ----

    [Fact]
    public async Task RecordResult_KnownTarget_ProducesRecordAndLinksItToTheTarget()
    {
        var (context, verificationService) = BuildContext();
        var activity = await CreateActivityAsync(context, "Activity", "Test");
        var handler = new RecordVerificationResultCommandHandler(verificationService);

        var criteria = new[] { new VerificationCriterion("Load withstood.", true, null) };
        var evidence = new[] { new VerificationEvidenceEntry("Test report.", "report-001.pdf") };

        var result = await handler.HandleAsync(
            new RecordVerificationResultCommand(activity.Id, "VerificationActivity", VerificationOutcome.Pass, "Test", criteria, evidence), default);

        Assert.True(result.Succeeded);
        var references = await context.Store.GetReferencesAsync(activity.Id);
        var link = Assert.Single(references);
        Assert.Equal(VerificationService.VerifiedByRelationshipKind, link.RelationshipKind);

        var history = await VerificationRecordReader.GetResultHistoryAsync(context, activity.Id);
        var record = Assert.Single(history);
        Assert.Equal(VerificationOutcome.Pass, record.Outcome);
        Assert.Equal("Test", record.Method);
        Assert.Single(record.Criteria);
        Assert.Single(record.Evidence);
    }

    [Fact]
    public async Task RecordResult_UnknownTarget_FailsWithoutThrowing()
    {
        var (_, verificationService) = BuildContext();
        var handler = new RecordVerificationResultCommandHandler(verificationService);

        var result = await handler.HandleAsync(
            new RecordVerificationResultCommand(Guid.NewGuid(), "VerificationActivity", VerificationOutcome.Fail, "Test"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RecordResult_LinksReferencedMaterialsAndCalculationRecords()
    {
        var (context, verificationService) = BuildContext();
        var activity = await CreateActivityAsync(context, "Activity", "Analysis");
        var linkedDocument = await CreateActivityAsync(context, "Linked Document Stand-in");
        var handler = new RecordVerificationResultCommandHandler(verificationService);

        var result = await handler.HandleAsync(new RecordVerificationResultCommand(
            activity.Id, "VerificationActivity", VerificationOutcome.Conditional, "Analysis",
            linkedDocumentIds: [linkedDocument.Id], referencedMaterialIds: ["SAMPLE-MAT-001"]), default);

        Assert.True(result.Succeeded);
        var latest = await VerificationRecordReader.GetLatestAsync(context, activity.Id);
        Assert.NotNull(latest);
        Assert.Equal(VerificationOutcome.Conditional, latest!.Outcome);
        Assert.Contains(linkedDocument.Id, latest.LinkedDocumentIds);
        Assert.Contains("SAMPLE-MAT-001", latest.ReferencedMaterialIds);
    }
}

using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Verification;
using Tempest.Workspace.Requirements;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 21.6A` item 1b: one test per compensable Requirements command
/// family (<c>requirements.create</c>/<c>delete</c>/<c>move</c>/
/// <c>move-group</c>/<c>set-status</c>), each proving the real Undo/Redo
/// round trip through <see cref="ICommandDispatcher"/> and the one named
/// refusal that family's own compensation carries ("a group since
/// deleted" for Move/MoveGroup; "the lifecycle table forbidding the
/// reverse transition" for SetStatus).
/// </summary>
public class RequirementsCompensationTests
{
    private static (IRequirementsService Requirements, ICommandDispatcher Dispatcher) BuildPipeline()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("test-user", "test-user"), []));

        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(
            documentStore, principalAccessor, permissionEvaluator, store, new InMemoryEngineeringRelationshipRepository());
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);

        var dispatcher = new CommandDispatcher(new CommandHandlerTable());
        dispatcher.RegisterHandler<CreateRequirementCommand>(new CreateRequirementCommandHandler(requirementsService, dispatcher));
        dispatcher.RegisterHandler<DeleteRequirementCommand>(new DeleteRequirementCommandHandler(requirementsService, dispatcher));
        dispatcher.RegisterHandler<UndeleteRequirementCommand>(new UndeleteRequirementCommandHandler(requirementsService));
        dispatcher.RegisterHandler<MoveRequirementCommand>(new MoveRequirementCommandHandler(requirementsService, dispatcher));
        dispatcher.RegisterHandler<MoveRequirementGroupCommand>(new MoveRequirementGroupCommandHandler(requirementsService, dispatcher));
        dispatcher.RegisterHandler<SetRequirementStatusCommand>(new SetRequirementStatusCommandHandler(requirementsService, dispatcher));

        return (requirementsService, dispatcher);
    }

    // ------------------------------------------------------------
    // requirements.create
    // ------------------------------------------------------------

    [Fact]
    public async Task Create_UndoSoftDeletesTheCreatedRequirement_RedoRestoresIt()
    {
        var (requirements, dispatcher) = BuildPipeline();

        var result = await dispatcher.DispatchAsync(new CreateRequirementCommand("REQ-001", "The system shall."), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Compensation);
        var createdId = result.SubjectId!.Value;

        var undoResult = await result.Compensation!.Undo(CancellationToken.None);
        Assert.True(undoResult.Succeeded);
        var afterUndo = await requirements.FindAsync(createdId);
        Assert.True(afterUndo!.IsDeleted);

        var redoResult = await result.Compensation!.Redo(CancellationToken.None);
        Assert.True(redoResult.Succeeded);
        var afterRedo = await requirements.FindAsync(createdId);
        Assert.False(afterRedo!.IsDeleted);
    }

    // ------------------------------------------------------------
    // requirements.delete
    // ------------------------------------------------------------

    [Fact]
    public async Task Delete_UndoRestoresTheRequirement_RedoDeletesItAgain()
    {
        var (requirements, dispatcher) = BuildPipeline();
        var created = await requirements.CreateAsync("REQ-002", "The system shall.");

        var result = await dispatcher.DispatchAsync(new DeleteRequirementCommand(created.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Compensation);
        Assert.True((await requirements.FindAsync(created.Id))!.IsDeleted);

        var undoResult = await result.Compensation!.Undo(CancellationToken.None);
        Assert.True(undoResult.Succeeded);
        Assert.False((await requirements.FindAsync(created.Id))!.IsDeleted);

        var redoResult = await result.Compensation!.Redo(CancellationToken.None);
        Assert.True(redoResult.Succeeded);
        Assert.True((await requirements.FindAsync(created.Id))!.IsDeleted);
    }

    // ------------------------------------------------------------
    // requirements.move
    // ------------------------------------------------------------

    [Fact]
    public async Task Move_UndoAndRedoRoundTripBetweenGroups_AndRefusesUndoWhenThePreviousGroupWasSinceDeleted()
    {
        var (requirements, dispatcher) = BuildPipeline();
        var groupA = await requirements.CreateGroupAsync("Group A");
        var groupB = await requirements.CreateGroupAsync("Group B");
        var requirement = await requirements.CreateAsync("REQ-003", "The system shall.");
        await requirements.MoveToGroupAsync(requirement.Id, groupA.Id);

        var result = await dispatcher.DispatchAsync(new MoveRequirementCommand(requirement.Id, groupB.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Compensation);
        Assert.Equal(groupB.Id, (await requirements.FindAsync(requirement.Id))!.GroupId);

        var redoResult = await result.Compensation!.Redo(CancellationToken.None);
        Assert.True(redoResult.Succeeded);
        Assert.Equal(groupB.Id, (await requirements.FindAsync(requirement.Id))!.GroupId);

        var undoResult = await result.Compensation!.Undo(CancellationToken.None);
        Assert.True(undoResult.Succeeded);
        Assert.Equal(groupA.Id, (await requirements.FindAsync(requirement.Id))!.GroupId);

        // "a group since deleted": group A has no live children once the
        // requirement moved back out of it in this test's own next step,
        // so it can genuinely be deleted — then Undo's own destination is
        // gone.
        await requirements.MoveToGroupAsync(requirement.Id, groupB.Id);
        await requirements.DeleteGroupAsync(groupA.Id);

        var refusedUndo = await result.Compensation!.Undo(CancellationToken.None);
        Assert.False(refusedUndo.Succeeded);
        Assert.Contains("no longer exists", refusedUndo.Message);
    }

    // ------------------------------------------------------------
    // requirements.move-group
    // ------------------------------------------------------------

    [Fact]
    public async Task MoveGroup_UndoAndRedoRoundTripBetweenParents_AndRefusesUndoWhenThePreviousParentWasSinceDeleted()
    {
        var (requirements, dispatcher) = BuildPipeline();
        var parentA = await requirements.CreateGroupAsync("Parent A");
        var parentB = await requirements.CreateGroupAsync("Parent B");
        var child = await requirements.CreateGroupAsync("Child", parentA.Id);

        var result = await dispatcher.DispatchAsync(new MoveRequirementGroupCommand(child.Id, parentB.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Compensation);
        Assert.Equal(parentB.Id, (await requirements.FindGroupAsync(child.Id))!.ParentGroupId);

        var undoResult = await result.Compensation!.Undo(CancellationToken.None);
        Assert.True(undoResult.Succeeded);
        Assert.Equal(parentA.Id, (await requirements.FindGroupAsync(child.Id))!.ParentGroupId);

        var redoResult = await result.Compensation!.Redo(CancellationToken.None);
        Assert.True(redoResult.Succeeded);
        Assert.Equal(parentB.Id, (await requirements.FindGroupAsync(child.Id))!.ParentGroupId);

        // "a group since deleted": Parent A has no live children once the
        // child moved back out of it, so it can genuinely be deleted —
        // then Undo's own destination is gone.
        await requirements.DeleteGroupAsync(parentA.Id);

        var refusedUndo = await result.Compensation!.Undo(CancellationToken.None);
        Assert.False(refusedUndo.Succeeded);
        Assert.Contains("no longer exists", refusedUndo.Message);
    }

    // ------------------------------------------------------------
    // requirements.set-status
    // ------------------------------------------------------------

    [Fact]
    public async Task SetStatus_UndoAndRedoRoundTripWhenTheLifecycleTablePermitsReversal_AndCarriesNoCompensationWhenItDoesNot()
    {
        var (requirements, dispatcher) = BuildPipeline();
        var requirement = await requirements.CreateAsync("REQ-004", "The system shall.");

        // Draft -> Reviewed permits its own reverse (Reviewed -> Draft) —
        // a real, undoable transition.
        var reviewedResult = await dispatcher.DispatchAsync(
            new SetRequirementStatusCommand(requirement.Id, RequirementStatus.Reviewed), CancellationToken.None);

        Assert.True(reviewedResult.Succeeded);
        Assert.NotNull(reviewedResult.Compensation);
        Assert.Null(reviewedResult.UndoUnavailableReason);

        var undoResult = await reviewedResult.Compensation!.Undo(CancellationToken.None);
        Assert.True(undoResult.Succeeded);
        Assert.Equal(RequirementStatus.Draft, (await requirements.FindAsync(requirement.Id))!.Status);

        var redoResult = await reviewedResult.Compensation!.Redo(CancellationToken.None);
        Assert.True(redoResult.Succeeded);
        Assert.Equal(RequirementStatus.Reviewed, (await requirements.FindAsync(requirement.Id))!.Status);

        // Draft -> Obsolete is permitted, but Obsolete's own row in
        // RequirementStatusTransitions permits nothing at all — the
        // lifecycle table forbids the reverse transition, so no
        // compensation is offered, and the reason is stated rather than
        // left silent.
        var second = await requirements.CreateAsync("REQ-005", "The system shall.");
        var obsoleteResult = await dispatcher.DispatchAsync(
            new SetRequirementStatusCommand(second.Id, RequirementStatus.Obsolete), CancellationToken.None);

        Assert.True(obsoleteResult.Succeeded);
        Assert.Null(obsoleteResult.Compensation);
        Assert.NotNull(obsoleteResult.UndoUnavailableReason);
        Assert.Contains("lifecycle does not permit reversing", obsoleteResult.UndoUnavailableReason);
    }
}

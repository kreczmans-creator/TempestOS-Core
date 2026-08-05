using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Requirements;

/// <summary>
/// Covers `WP 9.1A`'s additive `IRequirementsService` lifecycle/ownership/
/// priority operations (`ADR-0084`) — mirrors
/// <c>RequirementsServiceTests</c>'s own <c>BuildServices</c> construction
/// exactly.
/// </summary>
public class RequirementsLifecycleExtensionsTests
{
    private static (IRequirementsService Requirements, EngineeringDocumentStore Documents) BuildServices()
    {
        var store = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(documentStore, principalAccessor, permissionEvaluator);
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);

        return (requirementsService, documentStore);
    }

    // ---- SetOwnerAsync / SetPriorityAsync ----

    [Fact]
    public async Task SetOwnerAsync_SetsOwner()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        var updated = await requirements.SetOwnerAsync(requirement.Id, "alice");

        Assert.Equal("alice", updated.Owner);
    }

    [Fact]
    public async Task SetOwnerAsync_Null_ClearsOwner()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.SetOwnerAsync(requirement.Id, "alice");

        var updated = await requirements.SetOwnerAsync(requirement.Id, null);

        Assert.Null(updated.Owner);
    }

    [Fact]
    public async Task SetOwnerAsync_UnknownRequirement_Throws() =>
        await Assert.ThrowsAsync<RequirementNotFoundException>(() => BuildServices().Requirements.SetOwnerAsync(Guid.NewGuid(), "alice"));

    [Fact]
    public async Task SetPriorityAsync_SetsPriority()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        var updated = await requirements.SetPriorityAsync(requirement.Id, RequirementPriority.High);

        Assert.Equal(RequirementPriority.High, updated.Priority);
    }

    [Fact]
    public async Task SetOwnerAsync_PreservesStatementAndStatus()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Reviewed);

        var updated = await requirements.SetOwnerAsync(requirement.Id, "alice");

        Assert.Equal("The system shall do X.", updated.Statement);
        Assert.Equal(RequirementStatus.Reviewed, updated.Status);
    }

    // ---- DeleteAsync (requirement) ----

    [Fact]
    public async Task DeleteAsync_MarksIsDeleted()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        var deleted = await requirements.DeleteAsync(requirement.Id);

        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotEraseTheRequirement()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        await requirements.DeleteAsync(requirement.Id);
        var found = await requirements.FindAsync(requirement.Id);

        Assert.NotNull(found);
        Assert.True(found!.IsDeleted);
    }

    // ---- MoveToGroupAsync ----

    [Fact]
    public async Task MoveToGroupAsync_SetsGroupId()
    {
        var (requirements, _) = BuildServices();
        var group = await requirements.CreateGroupAsync("Group A");
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        var moved = await requirements.MoveToGroupAsync(requirement.Id, group.Id);

        Assert.Equal(group.Id, moved.GroupId);
    }

    [Fact]
    public async Task MoveToGroupAsync_ToNull_Ungroups()
    {
        var (requirements, _) = BuildServices();
        var group = await requirements.CreateGroupAsync("Group A");
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.MoveToGroupAsync(requirement.Id, group.Id);

        var moved = await requirements.MoveToGroupAsync(requirement.Id, null);

        Assert.Null(moved.GroupId);
    }

    [Fact]
    public async Task MoveToGroupAsync_ASecondTime_PreservesTheOriginalGroupedUnderLinkAsHistory()
    {
        var (requirements, _) = BuildServices();
        var groupA = await requirements.CreateGroupAsync("Group A");
        var groupB = await requirements.CreateGroupAsync("Group B");
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        await requirements.MoveToGroupAsync(requirement.Id, groupA.Id);
        var moved = await requirements.MoveToGroupAsync(requirement.Id, groupB.Id);

        Assert.Equal(groupB.Id, moved.GroupId);
        var relationships = await requirements.GetRelationshipsAsync(requirement.Id);
        Assert.Contains(relationships, r => r.TargetDocumentId == groupA.Id && r.RelationshipKind == RequirementRelationshipKinds.GroupedUnder);
        Assert.Contains(relationships, r => r.TargetDocumentId == groupB.Id && r.RelationshipKind == RequirementRelationshipKinds.GroupedUnder);
    }

    [Fact]
    public async Task MoveToGroupAsync_UnknownGroup_Throws()
    {
        var (requirements, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(() => requirements.MoveToGroupAsync(requirement.Id, Guid.NewGuid()));
    }

    // ---- MoveGroupAsync ----

    [Fact]
    public async Task MoveGroupAsync_SetsParentGroupId()
    {
        var (requirements, _) = BuildServices();
        var root = await requirements.CreateGroupAsync("Root");
        var child = await requirements.CreateGroupAsync("Child");

        var moved = await requirements.MoveGroupAsync(child.Id, root.Id);

        Assert.Equal(root.Id, moved.ParentGroupId);
    }

    [Fact]
    public async Task MoveGroupAsync_FindGroupAsync_ReflectsTheMove()
    {
        // Direct proof of the RequirementGroupDto storage-model fix: two
        // groupedUnder links now exist for the same source, and
        // FindGroupAsync must still resolve the correct, current parent.
        var (requirements, _) = BuildServices();
        var groupA = await requirements.CreateGroupAsync("Group A");
        var groupB = await requirements.CreateGroupAsync("Group B");
        var child = await requirements.CreateGroupAsync("Child", groupA.Id);

        await requirements.MoveGroupAsync(child.Id, groupB.Id);
        var found = await requirements.FindGroupAsync(child.Id);

        Assert.Equal(groupB.Id, found!.ParentGroupId);
    }

    [Fact]
    public async Task MoveGroupAsync_ToNull_BecomesRoot()
    {
        var (requirements, _) = BuildServices();
        var root = await requirements.CreateGroupAsync("Root");
        var child = await requirements.CreateGroupAsync("Child", root.Id);

        var moved = await requirements.MoveGroupAsync(child.Id, null);

        Assert.Null(moved.ParentGroupId);
    }

    // ---- DeleteGroupAsync ----

    [Fact]
    public async Task DeleteGroupAsync_EmptyGroup_Succeeds()
    {
        var (requirements, _) = BuildServices();
        var group = await requirements.CreateGroupAsync("Group A");

        var deleted = await requirements.DeleteGroupAsync(group.Id);

        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteGroupAsync_WithLiveGroupedRequirement_Throws()
    {
        var (requirements, _) = BuildServices();
        var group = await requirements.CreateGroupAsync("Group A");
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.MoveToGroupAsync(requirement.Id, group.Id);

        await Assert.ThrowsAsync<RequirementGroupHasChildrenException>(() => requirements.DeleteGroupAsync(group.Id));
    }

    [Fact]
    public async Task DeleteGroupAsync_WithLiveSubGroup_Throws()
    {
        var (requirements, _) = BuildServices();
        var root = await requirements.CreateGroupAsync("Root");
        await requirements.CreateGroupAsync("Child", root.Id);

        await Assert.ThrowsAsync<RequirementGroupHasChildrenException>(() => requirements.DeleteGroupAsync(root.Id));
    }

    [Fact]
    public async Task DeleteGroupAsync_WhoseOnlySubGroupIsAlreadyDeleted_Succeeds()
    {
        var (requirements, _) = BuildServices();
        var root = await requirements.CreateGroupAsync("Root");
        var child = await requirements.CreateGroupAsync("Child", root.Id);
        await requirements.DeleteGroupAsync(child.Id);

        var deleted = await requirements.DeleteGroupAsync(root.Id);

        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteGroupAsync_WhoseOnlyGroupedRequirementIsAlreadyDeleted_Succeeds()
    {
        var (requirements, _) = BuildServices();
        var group = await requirements.CreateGroupAsync("Group A");
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.MoveToGroupAsync(requirement.Id, group.Id);
        await requirements.DeleteAsync(requirement.Id);

        var deleted = await requirements.DeleteGroupAsync(group.Id);

        Assert.True(deleted.IsDeleted);
    }

    // ---- ListCollectionsAsync / ListGroupsAsync ----

    [Fact]
    public async Task ListCollectionsAsync_BeforeAnyCollection_IsEmpty()
    {
        var (requirements, _) = BuildServices();

        Assert.Empty(await requirements.ListCollectionsAsync());
    }

    [Fact]
    public async Task ListCollectionsAsync_ReturnsEveryCreatedCollection_LiveAndDeletedAlike()
    {
        var (requirements, _) = BuildServices();
        var first = await requirements.CreateCollectionAsync("Baseline Set");
        var second = await requirements.CreateCollectionAsync("Review Package");
        await requirements.DeleteCollectionAsync(second.Id);

        var all = await requirements.ListCollectionsAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, c => c.Id == first.Id && !c.IsDeleted);
        Assert.Contains(all, c => c.Id == second.Id && c.IsDeleted);
    }

    [Fact]
    public async Task ListGroupsAsync_BeforeAnyGroup_IsEmpty()
    {
        var (requirements, _) = BuildServices();

        Assert.Empty(await requirements.ListGroupsAsync());
    }

    [Fact]
    public async Task ListGroupsAsync_ReturnsEveryCreatedGroup_LiveAndDeletedAlike()
    {
        var (requirements, _) = BuildServices();
        var root = await requirements.CreateGroupAsync("Root");
        var child = await requirements.CreateGroupAsync("Child", root.Id);
        await requirements.DeleteGroupAsync(child.Id);

        var all = await requirements.ListGroupsAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, g => g.Id == root.Id && !g.IsDeleted);
        Assert.Contains(all, g => g.Id == child.Id && g.IsDeleted && g.ParentGroupId == root.Id);
    }

    // ---- DeleteCollectionAsync ----

    [Fact]
    public async Task DeleteCollectionAsync_MarksIsDeleted_NeverAffectsMembers()
    {
        var (requirements, _) = BuildServices();
        var collection = await requirements.CreateCollectionAsync("Baseline Set");
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.AddToCollectionAsync(collection.Id, requirement.Id);

        var deleted = await requirements.DeleteCollectionAsync(collection.Id);

        Assert.True(deleted.IsDeleted);
        var stillFound = await requirements.FindAsync(requirement.Id);
        Assert.False(stillFound!.IsDeleted);
    }
}

using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Tests.ReferenceData;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Audit;

/// <summary>
/// `WP 21.6A`, OSA-15, scope item 4: one test per mutator across the three
/// services this Work Package audits — <see cref="RequirementsService"/>
/// (fourteen mutators, plus <see cref="RequirementsService.UndeleteAsync"/>
/// added by item 1b), <see cref="VerificationService.RecordAsync"/>, and
/// <see cref="ReferenceData.ReferenceDataCatalog{TDefinition}"/> — each
/// proving the row exists in the same transaction as the write, and is
/// absent when the transaction's own commit faults (the
/// <see cref="R7RegressionProofTests"/>/`TD-158` fault-injection
/// convention: <see cref="InMemoryPersistenceStore.FailNextCommit"/> lets
/// the whole body run, then fails on the way out).
/// </summary>
public class RequirementsVerificationReferenceDataAuditTests
{
    // ==================================================================
    // RequirementsService — fourteen mutators, plus UndeleteAsync (item 1b)
    // ==================================================================

    private sealed record RequirementsRig(
        RequirementsService Requirements, InMemoryPersistenceStore Store, IAuditQuery AuditQuery);

    private static RequirementsRig BuildRequirementsRig()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity("audit-test-user", "audit-test-user"), [AuditQuery.QueryPermission, VerificationService.ReadPermission]));

        var documentStore = new EngineeringDocumentStore(persistenceStore, principalAccessor);
        var verification = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), persistenceStore, new InMemoryEngineeringRelationshipRepository());
        var requirements = new RequirementsService(documentStore, persistenceStore, principalAccessor, verification);
        var auditQuery = new AuditQuery(persistenceStore, principalAccessor, new PermissionEvaluator());

        return new RequirementsRig(requirements, persistenceStore, auditQuery);
    }

    private static async Task<IReadOnlyList<IAuditRecord>> RowsForAsync(RequirementsRig rig, Guid objectId) =>
        await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: objectId));

    [Fact]
    public async Task CreateAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();

        var created = await rig.Requirements.CreateAsync("REQ-C1", "Statement.");
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.Created);

        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.CreateAsync("REQ-C2", "Statement."));
        // The faulted create never committed a document id at all, so there
        // is no object id to query by — proven instead via ListAsync.
        Assert.DoesNotContain(await rig.Requirements.ListAsync(), r => r.Identifier == "REQ-C2");
    }

    [Fact]
    public async Task Requirements_ReviseAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var created = await rig.Requirements.CreateAsync("REQ-RV", "Original.");

        await rig.Requirements.ReviseAsync(created.Id, "Revised.", "Because.");
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.Revised);

        var before = await RowsForAsync(rig, created.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.ReviseAsync(created.Id, "Faulted.", null));
        Assert.Equal(before.Count, (await RowsForAsync(rig, created.Id)).Count);
    }

    [Fact]
    public async Task SetStatusAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var created = await rig.Requirements.CreateAsync("REQ-ST", "Statement.");

        await rig.Requirements.SetStatusAsync(created.Id, RequirementStatus.Reviewed);
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.StatusChanged);

        var before = await RowsForAsync(rig, created.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.SetStatusAsync(created.Id, RequirementStatus.Approved));
        Assert.Equal(before.Count, (await RowsForAsync(rig, created.Id)).Count);
    }

    [Fact]
    public async Task SetOwnerAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var created = await rig.Requirements.CreateAsync("REQ-OW", "Statement.");

        await rig.Requirements.SetOwnerAsync(created.Id, "J. Engineer");
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.OwnerChanged);

        var before = await RowsForAsync(rig, created.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.SetOwnerAsync(created.Id, "Someone Else"));
        Assert.Equal(before.Count, (await RowsForAsync(rig, created.Id)).Count);
    }

    [Fact]
    public async Task SetPriorityAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var created = await rig.Requirements.CreateAsync("REQ-PR", "Statement.");

        await rig.Requirements.SetPriorityAsync(created.Id, RequirementPriority.High);
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.PriorityChanged);

        var before = await RowsForAsync(rig, created.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.SetPriorityAsync(created.Id, RequirementPriority.Low));
        Assert.Equal(before.Count, (await RowsForAsync(rig, created.Id)).Count);
    }

    [Fact]
    public async Task DeleteAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var created = await rig.Requirements.CreateAsync("REQ-DL", "Statement.");

        await rig.Requirements.DeleteAsync(created.Id);
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.Deleted);

        var other = await rig.Requirements.CreateAsync("REQ-DL2", "Statement.");
        var before = await RowsForAsync(rig, other.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.DeleteAsync(other.Id));
        Assert.Equal(before.Count, (await RowsForAsync(rig, other.Id)).Count);
    }

    [Fact]
    public async Task UndeleteAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var created = await rig.Requirements.CreateAsync("REQ-UD", "Statement.");
        await rig.Requirements.DeleteAsync(created.Id);

        await rig.Requirements.UndeleteAsync(created.Id);
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.Undeleted);

        await rig.Requirements.DeleteAsync(created.Id);
        var before = await RowsForAsync(rig, created.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.UndeleteAsync(created.Id));
        Assert.Equal(before.Count, (await RowsForAsync(rig, created.Id)).Count);
    }

    [Fact]
    public async Task MoveToGroupAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var group = await rig.Requirements.CreateGroupAsync("Group");
        var created = await rig.Requirements.CreateAsync("REQ-MV", "Statement.");

        await rig.Requirements.MoveToGroupAsync(created.Id, group.Id);
        Assert.Contains(await RowsForAsync(rig, created.Id), r => r.Action == RequirementsAuditActions.Moved);

        var before = await RowsForAsync(rig, created.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.MoveToGroupAsync(created.Id, null));
        Assert.Equal(before.Count, (await RowsForAsync(rig, created.Id)).Count);
    }

    [Fact]
    public async Task LinkAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var source = await rig.Requirements.CreateAsync("REQ-LK1", "Statement.");
        var target = await rig.Requirements.CreateAsync("REQ-LK2", "Statement.");

        await rig.Requirements.LinkAsync(source.Id, target.Id, RequirementRelationshipKinds.References);
        Assert.Contains(await RowsForAsync(rig, source.Id), r => r.Action == RequirementsAuditActions.Linked);

        var before = await RowsForAsync(rig, source.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => rig.Requirements.LinkAsync(source.Id, target.Id, RequirementRelationshipKinds.DependsOn));
        Assert.Equal(before.Count, (await RowsForAsync(rig, source.Id)).Count);
    }

    [Fact]
    public async Task CreateCollectionAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();

        var collection = await rig.Requirements.CreateCollectionAsync("Collection 1");
        Assert.Contains(await RowsForAsync(rig, collection.Id), r => r.Action == RequirementsAuditActions.CollectionCreated);

        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.CreateCollectionAsync("Collection 2"));
        Assert.DoesNotContain(await rig.Requirements.ListCollectionsAsync(), c => c.Name == "Collection 2");
    }

    [Fact]
    public async Task DeleteCollectionAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var collection = await rig.Requirements.CreateCollectionAsync("To delete");

        await rig.Requirements.DeleteCollectionAsync(collection.Id);
        Assert.Contains(await RowsForAsync(rig, collection.Id), r => r.Action == RequirementsAuditActions.CollectionDeleted);

        var other = await rig.Requirements.CreateCollectionAsync("Still live");
        var before = await RowsForAsync(rig, other.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.DeleteCollectionAsync(other.Id));
        Assert.Equal(before.Count, (await RowsForAsync(rig, other.Id)).Count);
    }

    [Fact]
    public async Task AddToCollectionAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var collection = await rig.Requirements.CreateCollectionAsync("Collection");
        var requirementA = await rig.Requirements.CreateAsync("REQ-AC1", "Statement.");
        var requirementB = await rig.Requirements.CreateAsync("REQ-AC2", "Statement.");

        await rig.Requirements.AddToCollectionAsync(collection.Id, requirementA.Id);
        Assert.Contains(await RowsForAsync(rig, collection.Id), r => r.Action == RequirementsAuditActions.AddedToCollection);

        var before = await RowsForAsync(rig, collection.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.AddToCollectionAsync(collection.Id, requirementB.Id));
        Assert.Equal(before.Count, (await RowsForAsync(rig, collection.Id)).Count);
    }

    [Fact]
    public async Task CreateGroupAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();

        var group = await rig.Requirements.CreateGroupAsync("Group 1");
        Assert.Contains(await RowsForAsync(rig, group.Id), r => r.Action == RequirementsAuditActions.GroupCreated);

        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.CreateGroupAsync("Group 2"));
        Assert.DoesNotContain(await rig.Requirements.ListGroupsAsync(), g => g.Name == "Group 2");
    }

    [Fact]
    public async Task MoveGroupAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var parent = await rig.Requirements.CreateGroupAsync("Parent");
        var child = await rig.Requirements.CreateGroupAsync("Child");

        await rig.Requirements.MoveGroupAsync(child.Id, parent.Id);
        Assert.Contains(await RowsForAsync(rig, child.Id), r => r.Action == RequirementsAuditActions.GroupMoved);

        var before = await RowsForAsync(rig, child.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.MoveGroupAsync(child.Id, null));
        Assert.Equal(before.Count, (await RowsForAsync(rig, child.Id)).Count);
    }

    [Fact]
    public async Task DeleteGroupAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildRequirementsRig();
        var group = await rig.Requirements.CreateGroupAsync("To delete");

        await rig.Requirements.DeleteGroupAsync(group.Id);
        Assert.Contains(await RowsForAsync(rig, group.Id), r => r.Action == RequirementsAuditActions.GroupDeleted);

        var other = await rig.Requirements.CreateGroupAsync("Still live");
        var before = await RowsForAsync(rig, other.Id);
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Requirements.DeleteGroupAsync(other.Id));
        Assert.Equal(before.Count, (await RowsForAsync(rig, other.Id)).Count);
    }

    // ==================================================================
    // VerificationService.RecordAsync
    // ==================================================================

    [Fact]
    public async Task RecordAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity("audit-test-user", "audit-test-user"), [AuditQuery.QueryPermission, VerificationService.ReadPermission]));
        var documentStore = new EngineeringDocumentStore(persistenceStore, principalAccessor);
        var verification = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), persistenceStore, new InMemoryEngineeringRelationshipRepository());
        var auditQuery = new AuditQuery(persistenceStore, principalAccessor, new PermissionEvaluator());

        var subject = await documentStore.CreateAsync("Requirement", "subject content");
        var record = await verification.RecordAsync(
            subject.Id, VerificationOutcome.Pass, "Inspection", new VerificationContext());

        Assert.Contains(
            await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: record.Id)),
            r => r.Action == VerificationAuditActions.Recorded);

        persistenceStore.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => verification.RecordAsync(subject.Id, VerificationOutcome.Pass, "Inspection", new VerificationContext()));

        // The faulted record never committed a document id, so its own
        // audit trail can only be shown absent by the subject's history
        // not growing (`GetVerificationHistoryAsync`).
        var history = await verification.GetVerificationHistoryAsync(subject.Id);
        Assert.Single(history);
    }

    // ==================================================================
    // ReferenceDataCatalog — RegisterAsync / ReviseAsync / SupersedeAsync
    // ==================================================================

    private sealed record ReferenceDataRig(WidgetCatalog Catalog, InMemoryPersistenceStore Store, IAuditQuery AuditQuery);

    private static ReferenceDataRig BuildReferenceDataRig()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out _, out var persistenceStore);

        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity("audit-test-user", "audit-test-user"), [AuditQuery.QueryPermission]));
        var auditQuery = new AuditQuery(persistenceStore, principalAccessor, new PermissionEvaluator());

        return new ReferenceDataRig(catalog, persistenceStore, auditQuery);
    }

    [Fact]
    public async Task RegisterAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildReferenceDataRig();

        var record = await rig.Catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());
        Assert.Contains(
            await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: record.UnderlyingDocumentId)),
            r => r.Action == ReferenceDataAuditActions.RecordAdded);

        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => rig.Catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Sourced()));
        Assert.Null(await rig.Catalog.FindAsync("w-2"));
    }

    [Fact]
    public async Task ReferenceData_ReviseAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        var rig = BuildReferenceDataRig();
        var record = await rig.Catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());

        await rig.Catalog.ReviseAsync("w-1", ReferenceDataFixtures.Widget("W-1-rev"), ReferenceDataFixtures.Sourced(), "Revised.");
        Assert.Contains(
            await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: record.UnderlyingDocumentId)),
            r => r.Action == ReferenceDataAuditActions.Revised);

        var before = await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: record.UnderlyingDocumentId));
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => rig.Catalog.ReviseAsync("w-1", ReferenceDataFixtures.Widget("W-1-faulted"), ReferenceDataFixtures.Sourced(), null));
        Assert.Equal(
            before.Count, (await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: record.UnderlyingDocumentId))).Count);
    }

    [Fact]
    public async Task SupersedeAsync_WritesAnAuditRow_AbsentWhenTheCommitFaults()
    {
        // Supersede is permitted only from Released (ReferenceValidationStates'
        // own table), so each candidate is walked Draft -> Checked ->
        // Validated -> Released first, over provenance a reviewer has
        // verified — the only kind that can reach Released.
        var rig = BuildReferenceDataRig();
        var original = await rig.Catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Verified());
        await ReferenceDataFixtures.ReleaseAsync(rig.Catalog, "w-1");
        await rig.Catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Verified());

        await rig.Catalog.SupersedeAsync("w-1", "w-2", "Superseded.");
        Assert.Contains(
            await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: original.UnderlyingDocumentId)),
            r => r.Action == ReferenceDataAuditActions.Superseded);

        var third = await rig.Catalog.RegisterAsync("w-3", ReferenceDataFixtures.Widget("W-3"), ReferenceDataFixtures.Verified());
        await ReferenceDataFixtures.ReleaseAsync(rig.Catalog, "w-3");
        await rig.Catalog.RegisterAsync("w-4", ReferenceDataFixtures.Widget("W-4"), ReferenceDataFixtures.Verified());
        var before = await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: third.UnderlyingDocumentId));
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => rig.Catalog.SupersedeAsync("w-3", "w-4", "Faulted."));
        Assert.Equal(before.Count, (await rig.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: third.UnderlyingDocumentId))).Count);
    }
}

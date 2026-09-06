using Tempest.App.Workspace.Mechanical;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Logging;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Engineering object rehydration (`TD-85`) — the mechanism that closes
/// `ADR-0077`'s own disclosed gap: documents survived a restart, the
/// objects over them did not.
/// </summary>
/// <remarks>
/// Every test here uses the real persistent <see cref="EngineeringDocumentStore"/>
/// and <see cref="EngineeringObjectStateStore"/> over one shared
/// <see cref="IPersistenceStore"/>. A "second lifetime" is a genuinely new
/// <see cref="EngineeringDomainContext"/> with a new, empty object
/// repository and relationship index — nothing is carried over in memory.
/// </remarks>
public class EngineeringObjectRehydrationTests
{
    private sealed record Lifetime(
        EngineeringDomainContext Domain,
        EngineeringObjectRehydratorRegistry Rehydrators,
        EngineeringObjectRehydrationService Service,
        IEngineeringObjectStateStore StateStore,
        CurrentPrincipalAccessor Principal);

    private static Lifetime NewLifetime(
        IPersistenceStore persistence, bool registerMechanical = true,
        IStateMigrationRegistry? migrations = null, ILogger? logger = null)
    {
        var principal = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(persistence, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var stateStore = new EngineeringObjectStateStore(persistence, migrations, logger);

        var domain = new EngineeringDomainContext(
            documentStore, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore);

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        if (registerMechanical)
            MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);

        return new Lifetime(domain, rehydrators, new EngineeringObjectRehydrationService(domain, rehydrators), stateStore, principal);
    }

    // `WP 16.4B-R6`: `EngineeringObjectFactory<T>` now requires the Kind's
    // own `IRehydratable<T>` reader (see that type). Every canonical Kind
    // already implements it — this constraint only restates that.
    private static async Task<T> CreateAsync<T>(
        EngineeringDomainContext domain, string kind, Func<IEngineeringDocument, IDocumentRevision, T> ctor)
        where T : EngineeringObjectBase, IRehydratable<T>
    {
        var factory = new EngineeringObjectFactory<T>(kind, domain, ctor);
        return (T)await factory.CreateAsync($"{kind} — for test purposes.");
    }

    private static Task<Part> CreatePartAsync(EngineeringDomainContext domain, string identifier = "PN-1001", string displayName = "Impeller", string? materialId = null) =>
        CreateAsync(domain, MechanicalObjectFactoryRegistry.Part,
            (doc, rev) => new Part(doc, rev, domain, identifier, displayName, EngineeringObjectMetadata.Empty, materialId));

    // ----------------------------------------------------------------
    // Creation makes state durable — the precondition for everything else
    // ----------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsTheObjectsOwnState_NotOnlyItsDocument()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);

        var part = await CreatePartAsync(life.Domain);

        var state = await life.StateStore.FindAsync(part.Id);

        Assert.NotNull(state);
        Assert.Equal(part.Id, state!.Id);
        Assert.Equal(MechanicalObjectFactoryRegistry.Part, state.Kind);
        Assert.Equal("PN-1001", state.Identifier);
        Assert.Equal("Impeller", state.DisplayName);
    }

    [Fact]
    public async Task WithNoStateStoreComposed_CreationStillWorks_AndPersistsNothing()
    {
        // Every pre-`TD-85` hand-assembled context must keep working
        // exactly as it did — the store is deliberately optional.
        var principal = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var domain = new EngineeringDomainContext(
            new InMemoryEngineeringDocumentStore(principal), repository, relationships,
            new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(new RelationshipDiscoveryService(relationships, repository), repository), principal);

        var part = await CreatePartAsync(domain);

        Assert.Null(domain.ObjectStateStore);
        Assert.NotNull(await domain.Repository.FindAsync(part.Id));
    }

    // ----------------------------------------------------------------
    // The object itself comes back
    // ----------------------------------------------------------------

    [Fact]
    public async Task AfterRestart_TheObjectComesBack_AsItsOwnConcreteType_WithTheSameIdentity()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var created = await CreatePartAsync(first.Domain, materialId: "AL-7075");

        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.Equal(1, result.ObjectCount);
        Assert.True(result.IsComplete);

        var recovered = Assert.IsType<Part>(await second.Domain.Repository.FindAsync(created.Id));
        Assert.Equal(created.Id, recovered.Id);
        Assert.Equal(MechanicalObjectFactoryRegistry.Part, recovered.Kind);
        Assert.Equal("PN-1001", recovered.Identifier);
        Assert.Equal("Impeller", recovered.DisplayName);
        Assert.Equal("AL-7075", recovered.MaterialId);
    }

    [Fact]
    public async Task AfterRestart_EveryMutableFacet_IsRestored()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);

        var parent = await CreateAsync(first.Domain, MechanicalObjectFactoryRegistry.Assembly,
            (doc, rev) => new Assembly(doc, rev, first.Domain, "ASM-100", "Pump Head", EngineeringObjectMetadata.Empty));
        var part = await CreatePartAsync(first.Domain);

        await part.TransitionAsync(LifecycleState.InReview);
        await part.RenameAsync("Impeller (Rev B)");
        await part.MoveAsync(parent.Id);
        await part.SetBomLineAsync(4m, "ea", "FN-07", "IT-3", "RD-9");
        await part.AttachAsync(new Attachment("profile.step", "model/step", 2048));

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = Assert.IsType<Part>(await second.Domain.Repository.FindAsync(part.Id));

        Assert.Equal(LifecycleState.InReview, recovered.Status);
        Assert.Equal("Impeller (Rev B)", recovered.DisplayName);
        Assert.Equal(parent.Id, recovered.ParentId);
        Assert.Equal(4m, recovered.Quantity);
        Assert.Equal("ea", recovered.UnitOfMeasure);
        Assert.Equal("FN-07", recovered.FindNumber);
        Assert.Equal("IT-3", recovered.ItemNumber);
        Assert.Equal("RD-9", recovered.ReferenceDesignator);

        var attachments = await recovered.GetAttachmentsAsync();
        var attachment = Assert.Single(attachments);
        Assert.Equal("profile.step", attachment.FileName);
        Assert.Equal("model/step", attachment.ContentType);
        Assert.Equal(2048, attachment.SizeInBytes);
    }

    [Fact]
    public async Task AfterRestart_AnAttachmentKeepsItsOwnIdentity_NotANewOne()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);

        var attachment = new Attachment("profile.step", "model/step", 2048);
        await part.AttachAsync(attachment);

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;
        var recoveredAttachment = Assert.Single(await recovered.GetAttachmentsAsync());

        Assert.Equal(attachment.Id, recoveredAttachment.Id);
    }

    [Fact]
    public async Task AfterRestart_TheFullLifecycleHistory_ComesBackInOrder_WithItsActorAndTime()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);

        await part.TransitionAsync(LifecycleState.InReview);
        await part.TransitionAsync(LifecycleState.Approved);
        var expected = part.History.Select(h => (h.From, h.To, h.ActorPrincipalId, h.OccurredAt)).ToList();

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;
        var actual = recovered.History.Select(h => (h.From, h.To, h.ActorPrincipalId, h.OccurredAt)).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task AfterRestart_ADeletedObject_IsStillDeleted()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);
        await part.DeleteAsync();

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;
        Assert.True(recovered.IsDeleted);
    }

    [Fact]
    public async Task AfterRestart_TheLatestRevisionIsTheCurrentOne_AndTheFullHistoryIsReadable()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);
        await part.ReviseAsync("Impeller — revised blade profile.", "Rev B");

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;

        Assert.Equal(2, recovered.CurrentRevisionNumber);
        Assert.Equal("Impeller — revised blade profile.", recovered.Content);

        var history = await recovered.GetRevisionHistoryAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Rev B", history[^1].ChangeSummary);
    }

    [Fact]
    public async Task ARehydratedObject_CanStillReviseItself_IntoItsOwnCorrectType()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;
        var revised = Assert.IsType<Part>(await recovered.ReviseAsync("Revised after restart.", "Rev B"));

        Assert.Equal(part.Id, revised.Id);
        Assert.Equal(2, revised.CurrentRevisionNumber);
        Assert.Equal("PN-1001", revised.Identifier);
    }

    // ----------------------------------------------------------------
    // Revision must carry the whole object, not half of it
    // (`TD-85` closure audit)
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_CarriesLifecycleStateAndHistoryOntoTheRevisedInstance()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);
        var part = await CreatePartAsync(life.Domain);

        await part.TransitionAsync(LifecycleState.InReview);
        await part.TransitionAsync(LifecycleState.Approved);

        var revised = Assert.IsType<Part>(await part.ReviseAsync("Revised.", "Rev B"));

        // A revision is a new instance of the *same* object. Before this
        // was fixed, the successor started at Draft with no history,
        // because the self-factory only ever knew the original factory
        // call's own arguments.
        Assert.Equal(LifecycleState.Approved, revised.Status);
        Assert.Equal(2, revised.History.Count);
        Assert.Equal(LifecycleState.Draft, revised.History[0].From);
        Assert.Equal(LifecycleState.Approved, revised.History[1].To);
    }

    [Fact]
    public async Task ReviseAsync_CarriesAttachmentsOntoTheRevisedInstance()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);
        var part = await CreatePartAsync(life.Domain);
        var attachment = new Attachment("profile.step", "model/step", 2048);
        await part.AttachAsync(attachment);

        var revised = Assert.IsType<Part>(await part.ReviseAsync("Revised.", "Rev B"));

        var carried = Assert.Single(await revised.GetAttachmentsAsync());
        Assert.Equal(attachment.Id, carried.Id);
    }

    [Fact]
    public async Task ReviseAsync_StillCarriesEveryStructuralField_TheWP90BBehaviourIsUnchanged()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);
        var parent = await CreateAsync(life.Domain, MechanicalObjectFactoryRegistry.Assembly,
            (doc, rev) => new Assembly(doc, rev, life.Domain, "ASM-100", "Pump Head", EngineeringObjectMetadata.Empty));
        var part = await CreatePartAsync(life.Domain);

        await part.RenameAsync("Impeller (Rev B)");
        await part.MoveAsync(parent.Id);
        await part.SetBomLineAsync(4m, "ea", "FN-07", "IT-3", "RD-9");

        var revised = Assert.IsType<Part>(await part.ReviseAsync("Revised.", "Rev B"));

        Assert.Equal("Impeller (Rev B)", revised.DisplayName);
        Assert.Equal(parent.Id, revised.ParentId);
        Assert.Equal(4m, revised.Quantity);
        Assert.Equal("ea", revised.UnitOfMeasure);
        Assert.Equal("FN-07", revised.FindNumber);
        Assert.Equal("IT-3", revised.ItemNumber);
        Assert.Equal("RD-9", revised.ReferenceDesignator);
    }

    [Fact]
    public async Task AfterRevising_AFurtherMutation_NeverOverwritesThePersistedLifecycleWithAResetOne()
    {
        // The defect this closes was durable, not cosmetic: a revised
        // instance that had silently reverted to Draft would write that
        // reset to disk on its very next mutation, destroying a recorded
        // lifecycle state and its whole transition history.
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);
        await part.TransitionAsync(LifecycleState.InReview);

        var revised = (Part)await part.ReviseAsync("Revised.", "Rev B");
        await revised.RenameAsync("Impeller B");

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();
        var recovered = Assert.IsType<Part>(await second.Domain.Repository.FindAsync(part.Id));

        Assert.Equal(LifecycleState.InReview, recovered.Status);
        Assert.Single(recovered.History);
        Assert.Equal("Impeller B", recovered.DisplayName);
        Assert.Equal(2, recovered.CurrentRevisionNumber);
    }

    // ----------------------------------------------------------------
    // Relationships
    // ----------------------------------------------------------------

    [Fact]
    public async Task AfterRestart_RelationshipsAreReIndexed_WithTheirOwnDurableProvenance()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        first.Principal.SetCurrent(TestPrincipal("engineer-1"));

        var assembly = await CreateAsync(first.Domain, MechanicalObjectFactoryRegistry.Assembly,
            (doc, rev) => new Assembly(doc, rev, first.Domain, "ASM-100", "Pump Head", EngineeringObjectMetadata.Empty));
        var part = await CreatePartAsync(first.Domain);
        await part.LinkAsync(assembly.Id, "dependsOn");

        var second = NewLifetime(persistence);
        second.Principal.SetCurrent(TestPrincipal("someone-else"));
        var result = await second.Service.RehydrateAsync();

        Assert.True(result.RelationshipCount >= 1);

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;
        var relationship = Assert.Single(await recovered.GetRelationshipsAsync(), r => r.RelationshipKind == "dependsOn");

        Assert.Equal(part.Id, relationship.SourceId);
        Assert.Equal(assembly.Id, relationship.TargetId);
        Assert.Equal(RelationshipCategory.Dependency, relationship.Category);

        // Attribution is the principal that made the link, never whoever
        // happens to be signed in when it is read back.
        Assert.Equal("engineer-1", relationship.CreatedByPrincipalId);
    }

    [Fact]
    public async Task AfterRestart_TheStructuralParentEdge_IsBothRestoredAndStillDiscoverable()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var assembly = await CreateAsync(first.Domain, MechanicalObjectFactoryRegistry.Assembly,
            (doc, rev) => new Assembly(doc, rev, first.Domain, "ASM-100", "Pump Head", EngineeringObjectMetadata.Empty));
        var part = await CreatePartAsync(first.Domain);
        await part.MoveAsync(assembly.Id);

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;

        Assert.Equal(assembly.Id, recovered.ParentId);
        Assert.Contains(await recovered.GetRelationshipsAsync(), r => r.RelationshipKind == "groupedUnder" && r.TargetId == assembly.Id);
    }

    // ----------------------------------------------------------------
    // Every registered Kind round-trips
    // ----------------------------------------------------------------

    [Fact]
    public async Task EveryMechanicalKind_ComesBackAsItsOwnRegisteredType()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var domain = first.Domain;
        var metadata = EngineeringObjectMetadata.Empty;

        var expected = new Dictionary<Guid, Type>
        {
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Project, (d, r) => new Project(d, r, domain, "P-1", "P", metadata))).Id] = typeof(Project),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Assembly, (d, r) => new Assembly(d, r, domain, "A-1", "A", metadata))).Id] = typeof(Assembly),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.SubAssembly, (d, r) => new SubAssembly(d, r, domain, "SA-1", "SA", metadata, Guid.NewGuid()))).Id] = typeof(SubAssembly),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Part, (d, r) => new Part(d, r, domain, "PN-1", "Pt", metadata))).Id] = typeof(Part),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Component, (d, r) => new Component(d, r, domain, "C-1", "C", metadata))).Id] = typeof(Component),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Configuration, (d, r) => new Tempest.Core.EngineeringDomain.Configuration(d, r, domain, "CFG-1", "Cfg", metadata))).Id] = typeof(Tempest.Core.EngineeringDomain.Configuration),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Baseline, (d, r) => new Baseline(d, r, domain, "BL-1", "Bl", metadata))).Id] = typeof(Baseline),
            [(await CreateAsync(domain, MechanicalObjectFactoryRegistry.Release, (d, r) => new Release(d, r, domain, "REL-1", "Rel", metadata))).Id] = typeof(Release),
        };

        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.Equal(expected.Count, result.ObjectCount);
        Assert.True(result.IsComplete);

        foreach (var (id, type) in expected)
            Assert.IsType(type, await second.Domain.Repository.FindAsync(id));
    }

    [Fact]
    public async Task AfterRestart_ASubAssemblysOwnParentAssemblyId_IsPreserved()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var parentAssemblyId = Guid.NewGuid();

        var subAssembly = await CreateAsync(first.Domain, MechanicalObjectFactoryRegistry.SubAssembly,
            (d, r) => new SubAssembly(d, r, first.Domain, "SA-1", "Sub", EngineeringObjectMetadata.Empty, parentAssemblyId, [Guid.NewGuid()]));
        var expectedChildren = subAssembly.ChildIds;

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = Assert.IsType<SubAssembly>(await second.Domain.Repository.FindAsync(subAssembly.Id));
        Assert.Equal(parentAssemblyId, recovered.ParentAssemblyId);
        Assert.Equal(expectedChildren, recovered.ChildIds);
    }

    [Fact]
    public async Task AfterRestart_AConfigurationsOwnMemberRevisions_ArePreserved()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var members = new List<ConfigurationMember> { new(Guid.NewGuid(), 3), new(Guid.NewGuid(), 7) };

        var configuration = await CreateAsync(first.Domain, MechanicalObjectFactoryRegistry.Baseline,
            (d, r) => new Baseline(d, r, first.Domain, "BL-1", "Baseline", EngineeringObjectMetadata.Empty, members));

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = Assert.IsType<Baseline>(await second.Domain.Repository.FindAsync(configuration.Id));
        Assert.Equal(members, recovered.MemberRevisions);
    }

    [Fact]
    public async Task AfterRestart_Metadata_IsPreserved()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var metadata = new EngineeringObjectMetadata("Rotating", "Mechanical", "a.engineer", ["pump", "critical"], "Specification", "A note.");

        var part = await CreateAsync(first.Domain, MechanicalObjectFactoryRegistry.Part,
            (d, r) => new Part(d, r, first.Domain, "PN-1001", "Impeller", metadata));

        var second = NewLifetime(persistence);
        await second.Service.RehydrateAsync();

        var recovered = (Part)(await second.Domain.Repository.FindAsync(part.Id))!;
        Assert.Equal("Rotating", recovered.Category);
        Assert.Equal("Mechanical", recovered.Discipline);
        Assert.Equal("a.engineer", recovered.Owner);
        Assert.Equal(["pump", "critical"], recovered.Tags);
        Assert.Equal("Specification", recovered.Classification);
        Assert.Equal("A note.", recovered.Notes);
    }

    // ----------------------------------------------------------------
    // Partial failure is survivable, and honestly reported
    // ----------------------------------------------------------------

    [Fact]
    public async Task AnUnregisteredKind_IsReportedAndSkipped_NeverThrown_AndNeverCostsTheOtherObjects()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);
        var exotic = await CreateAsync(first.Domain, "SomeFutureKind",
            (d, r) => new Part(d, r, first.Domain, "X-1", "Exotic", EngineeringObjectMetadata.Empty));

        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.Equal(1, result.ObjectCount);
        Assert.Equal(["SomeFutureKind"], result.UnknownKinds);
        Assert.False(result.IsComplete);
        Assert.NotNull(await second.Domain.Repository.FindAsync(part.Id));
        Assert.Null(await second.Domain.Repository.FindAsync(exotic.Id));
    }

    [Fact]
    public async Task StateWithNoBackingDocument_IsReportedAsOrphaned_NeverThrown()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var part = await CreatePartAsync(first.Domain);

        var orphanId = Guid.NewGuid();
        await first.StateStore.SaveAsync(new EngineeringObjectState(
            EngineeringObjectStateStore.CurrentSchemaVersion,
            orphanId, MechanicalObjectFactoryRegistry.Part, "PN-GONE", "Gone", EngineeringObjectMetadata.Empty,
            LifecycleState.Draft, null, false, EngineeringObjectBomLineState.Default, [], [],
            new Dictionary<string, string?>()));

        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.Equal(1, result.ObjectCount);
        Assert.Equal([orphanId], result.OrphanedStateIds);
        Assert.NotNull(await second.Domain.Repository.FindAsync(part.Id));
    }

    [Fact]
    public async Task ACorruptedStateRecord_IsSkipped_AndEveryOtherObjectStillComesBack()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var good = await CreatePartAsync(first.Domain);
        var bad = await CreatePartAsync(first.Domain, "PN-1002", "Wear Ring");

        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, bad.Id.ToString("N"), "{ not json");

        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.Equal(1, result.ObjectCount);
        Assert.NotNull(await second.Domain.Repository.FindAsync(good.Id));
        Assert.Null(await second.Domain.Repository.FindAsync(bad.Id));
    }

    // ----------------------------------------------------------------
    // `TD-87`/`ADR-0120` Decision 5 — a record whose schema version
    // cannot be bridged is logged and skipped, the same discipline
    // `ACorruptedStateRecord_IsSkipped...` above already proves for
    // malformed JSON, extended to a record's own state store read path.
    // ----------------------------------------------------------------

    [Fact]
    public async Task ARehydrationBatchContainingAStuckRecord_StillReturnsEveryOtherObject()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);
        var good = await CreatePartAsync(first.Domain);

        // A record from a build newer than this one — nothing can migrate
        // "the future" backward, so it can never be bridged to this
        // build's own EngineeringObjectStateStore.CurrentSchemaVersion.
        var stuckId = Guid.NewGuid();
        await first.StateStore.SaveAsync(new EngineeringObjectState(
            99, stuckId, MechanicalObjectFactoryRegistry.Part, "PN-STUCK", "Stuck Part", EngineeringObjectMetadata.Empty,
            LifecycleState.Draft, null, false, EngineeringObjectBomLineState.Default, [], [],
            new Dictionary<string, string?>()));

        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.Equal(1, result.ObjectCount);
        Assert.NotNull(await second.Domain.Repository.FindAsync(good.Id));
        Assert.Null(await second.Domain.Repository.FindAsync(stuckId));
    }

    [Fact]
    public async Task AStuckRecordEncounteredDuringRehydration_IsLoggedNamingItsIdKindAndStuckVersion()
    {
        // `ADR-0120` Decision 5 places the stuck-vs-corrupt distinction in
        // "the log line a caller reads" — not a new EngineeringRehydrationResult
        // field — the small increase in logging vocabulary its own
        // Consequences describe, reached here through the full rehydration
        // path (EngineeringObjectRehydrationService -> ListAsync -> Deserialise),
        // not only through the state store directly.
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = NewLifetime(persistence);

        var stuckId = Guid.NewGuid();
        await first.StateStore.SaveAsync(new EngineeringObjectState(
            99, stuckId, MechanicalObjectFactoryRegistry.Part, "PN-STUCK", "Stuck Part", EngineeringObjectMetadata.Empty,
            LifecycleState.Draft, null, false, EngineeringObjectBomLineState.Default, [], [],
            new Dictionary<string, string?>()));

        var logger = new RecordingLogger();
        var second = NewLifetime(persistence, logger: logger);

        await second.Service.RehydrateAsync();

        var message = Assert.Single(logger.Messages, m => m.Contains(stuckId.ToString(), StringComparison.Ordinal));
        Assert.Contains(MechanicalObjectFactoryRegistry.Part, message, StringComparison.Ordinal);
        Assert.Contains("99", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnObjectAlreadyLive_IsNeverReplacedByADiskSnapshot()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);
        var part = await CreatePartAsync(life.Domain);

        // A mutation made after the state was last written — exactly what
        // would be lost if rehydration overwrote a live object.
        await part.RenameAsync("Impeller (in-session rename)");

        var result = await life.Service.RehydrateAsync();

        Assert.Equal(0, result.ObjectCount);
        Assert.Equal(1, result.AlreadyLiveCount);
        Assert.Same(part, await life.Domain.Repository.FindAsync(part.Id));
        Assert.Equal("Impeller (in-session rename)", part.DisplayName);
    }

    [Fact]
    public async Task WithNoStateStoreComposed_RehydrationIsANoOp_NeverAFailure()
    {
        var principal = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var domain = new EngineeringDomainContext(
            new InMemoryEngineeringDocumentStore(principal), repository, relationships,
            new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(new RelationshipDiscoveryService(relationships, repository), repository), principal);

        var result = await new EngineeringObjectRehydrationService(domain, new EngineeringObjectRehydratorRegistry()).RehydrateAsync();

        Assert.Equal(0, result.ObjectCount);
        Assert.True(result.IsComplete);
    }

    // ----------------------------------------------------------------
    // The registry itself
    // ----------------------------------------------------------------

    [Fact]
    public void Registry_ReportsExactlyTheKindsRegistered()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);

        Assert.Equal(
            MechanicalObjectFactoryRegistry.SupportedKinds.Order(StringComparer.Ordinal),
            life.Rehydrators.RegisteredKinds);
    }

    [Fact]
    public void Registry_RegisteringTheIdenticalKindAndTypeTwice_IsIdempotent()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);

        var exception = Record.Exception(() => MechanicalObjectFactoryRegistry.RegisterRehydrators(life.Rehydrators, life.Domain));

        Assert.Null(exception);
        Assert.Equal(MechanicalObjectFactoryRegistry.SupportedKinds.Count, life.Rehydrators.RegisteredKinds.Count);
    }

    [Fact]
    public void Registry_TwoDifferentTypesClaimingTheSameKind_Throws()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);

        var exception = Assert.Throws<DuplicateRehydratorRegistrationException>(
            () => life.Rehydrators.Register<Component>(MechanicalObjectFactoryRegistry.Part, life.Domain));

        Assert.Equal(MechanicalObjectFactoryRegistry.Part, exception.Kind);
        Assert.Equal(typeof(Part), exception.ExistingType);
        Assert.Equal(typeof(Component), exception.AttemptedType);
    }

    [Fact]
    public void Registry_AnUnregisteredKind_ResolvesToNothing()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var life = NewLifetime(persistence);

        Assert.Null(life.Rehydrators.Find("NotAKind"));
    }

    private static IPrincipal TestPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);
}

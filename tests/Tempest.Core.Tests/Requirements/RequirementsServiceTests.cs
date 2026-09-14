using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Requirements;

public class RequirementsServiceTests
{
    private static (RequirementsService Requirements, EngineeringDocumentStore Documents, IVerificationService Verification) BuildServices(
        InMemoryQueryablePersistenceStore? persistenceStore = null, IWorkspaceChangePublisher? workspaceChanges = null)
    {
        var store = persistenceStore ?? new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(
            documentStore, principalAccessor, permissionEvaluator, store, new InMemoryEngineeringRelationshipRepository());
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService, logger: null, workspaceChanges);

        // GetEvidenceAsync transitively requires VerificationService.ReadPermission
        // (ADR-0061 — RequirementsService itself gates nothing internally).
        // Granted by default here so every test not specifically exercising the
        // denial path can call GetEvidenceAsync without its own setup.
        principalAccessor.SetCurrent(BuildPrincipal("test-user", VerificationService.ReadPermission));

        return (requirementsService, documentStore, verificationService);
    }

    private static IPrincipal BuildPrincipal(string id, params Permission[] permissions) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), permissions);

    // ------------------------------------------------------------
    // Constructor validation
    // ------------------------------------------------------------

    [Fact]
    public void Constructor_NullDocumentStore_ThrowsArgumentNullException()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var verification = new VerificationService(
            new EngineeringDocumentStore(store, principalAccessor), principalAccessor, new PermissionEvaluator(),
            store, new InMemoryEngineeringRelationshipRepository());

        Assert.Throws<ArgumentNullException>(() => new RequirementsService(null!, store, principalAccessor, verification));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var verification = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), store, new InMemoryEngineeringRelationshipRepository());

        Assert.Throws<ArgumentNullException>(() => new RequirementsService(documentStore, null!, principalAccessor, verification));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var verification = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), store, new InMemoryEngineeringRelationshipRepository());

        Assert.Throws<ArgumentNullException>(() => new RequirementsService(documentStore, store, null!, verification));
    }

    [Fact]
    public void Constructor_NullVerificationService_ThrowsArgumentNullException()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);

        Assert.Throws<ArgumentNullException>(() => new RequirementsService(documentStore, store, principalAccessor, null!));
    }

    [Fact]
    public void Constructor_PersistenceStoreNotQueryable_ThrowsArgumentException()
    {
        var store = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var queryableStore = new InMemoryQueryablePersistenceStore();
        var verification = new VerificationService(
            new EngineeringDocumentStore(queryableStore, principalAccessor), principalAccessor, new PermissionEvaluator(),
            queryableStore, new InMemoryEngineeringRelationshipRepository());

        Assert.Throws<ArgumentException>(() => new RequirementsService(documentStore, store, principalAccessor, verification));
    }

    // ------------------------------------------------------------
    // CreateAsync / FindAsync / FindByIdentifierAsync — unit
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsRequirementWithDraftStatus()
    {
        var (requirements, _, _) = BuildServices();

        var requirement = await requirements.CreateAsync("REQ-001", "The system shall do X.", "functional");

        Assert.Equal("REQ-001", requirement.Identifier);
        Assert.Equal("The system shall do X.", requirement.Statement);
        Assert.Equal("functional", requirement.Category);
        Assert.Equal(RequirementStatus.Draft, requirement.Status);
        Assert.Equal(1, requirement.RevisionNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_InvalidIdentifier_ThrowsArgumentException(string? identifier)
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => requirements.CreateAsync(identifier!, "Statement"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_InvalidStatement_ThrowsArgumentException(string? statement)
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => requirements.CreateAsync("REQ-001", statement!));
    }

    [Fact]
    public async Task CreateAsync_DuplicateIdentifier_ThrowsDuplicateRequirementIdentifierException()
    {
        var (requirements, _, _) = BuildServices();
        await requirements.CreateAsync("REQ-001", "First.");

        var exception = await Assert.ThrowsAsync<DuplicateRequirementIdentifierException>(() => requirements.CreateAsync("REQ-001", "Second."));
        Assert.Equal("REQ-001", exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_NoCategory_ReturnsNullCategory()
    {
        var (requirements, _, _) = BuildServices();

        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");

        Assert.Null(requirement.Category);
    }

    [Fact]
    public async Task FindAsync_ExistingRequirement_ReturnsIt()
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Statement.");

        var found = await requirements.FindAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal("REQ-001", found!.Identifier);
    }

    [Fact]
    public async Task FindAsync_NonExistentId_ReturnsNull()
    {
        var (requirements, _, _) = BuildServices();

        Assert.Null(await requirements.FindAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task FindAsync_DocumentOfDifferentKind_ReturnsNull()
    {
        var (requirements, documents, _) = BuildServices();
        var otherDocument = await documents.CreateAsync("SomeOtherKind", "content");

        Assert.Null(await requirements.FindAsync(otherDocument.Id));
    }

    [Fact]
    public async Task FindByIdentifierAsync_ExistingIdentifier_ReturnsIt()
    {
        var (requirements, _, _) = BuildServices();
        await requirements.CreateAsync("REQ-001", "Statement.");

        var found = await requirements.FindByIdentifierAsync("REQ-001");

        Assert.NotNull(found);
    }

    [Fact]
    public async Task FindByIdentifierAsync_NonExistentIdentifier_ReturnsNull() =>
        Assert.Null(await BuildServices().Requirements.FindByIdentifierAsync("NO-SUCH-ID"));

    [Fact]
    public async Task FindByIdentifierAsync_InvalidIdentifier_ThrowsArgumentException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<ArgumentException>(() => requirements.FindByIdentifierAsync(""));
    }

    [Fact]
    public async Task ListAsync_MultipleRequirements_ReturnsAll()
    {
        var (requirements, _, _) = BuildServices();
        await requirements.CreateAsync("REQ-001", "First.");
        await requirements.CreateAsync("REQ-002", "Second.");

        var all = await requirements.ListAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ListAsync_NoRequirements_ReturnsEmpty() =>
        Assert.Empty(await BuildServices().Requirements.ListAsync());

    // ------------------------------------------------------------
    // ReviseAsync — revision tests
    // ------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_ValidInput_IncrementsRevisionAndUpdatesStatement()
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Original.");

        var revised = await requirements.ReviseAsync(created.Id, "Revised.", "Sample revision.");

        Assert.Equal("Revised.", revised.Statement);
        Assert.Equal(2, revised.RevisionNumber);
    }

    [Fact]
    public async Task ReviseAsync_PreservesIdentifierCategoryAndCreationAttribution()
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Original.", "safety");

        var revised = await requirements.ReviseAsync(created.Id, "Revised.", null);

        Assert.Equal("REQ-001", revised.Identifier);
        Assert.Equal("safety", revised.Category);
        Assert.Equal(created.CreatedByPrincipalId, revised.CreatedByPrincipalId);
        Assert.Equal(created.CreatedAt, revised.CreatedAt);
    }

    [Fact]
    public async Task ReviseAsync_NonExistentRequirement_ThrowsRequirementNotFoundException()
    {
        var (requirements, _, _) = BuildServices();
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<RequirementNotFoundException>(() => requirements.ReviseAsync(id, "New.", null));
        Assert.Equal(id, exception.RequirementId);
    }

    [Fact]
    public async Task ReviseAsync_InvalidStatement_ThrowsArgumentException()
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Original.");

        await Assert.ThrowsAsync<ArgumentException>(() => requirements.ReviseAsync(created.Id, "", null));
    }

    [Fact]
    public async Task ReviseAsync_MultipleRevisions_EachIncrementsSequentially()
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "V0.");

        await requirements.ReviseAsync(created.Id, "V1.", null);
        await requirements.ReviseAsync(created.Id, "V2.", null);
        var v3 = await requirements.ReviseAsync(created.Id, "V3.", null);

        Assert.Equal(4, v3.RevisionNumber);
    }

    // ------------------------------------------------------------
    // SetStatusAsync — exhaustive lifecycle transition table
    // ------------------------------------------------------------

    [Theory]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Obsolete)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Obsolete)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Obsolete)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Obsolete)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Obsolete)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Obsolete)]
    public async Task SetStatusAsync_PermittedTransition_Succeeds(RequirementStatus from, RequirementStatus to)
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Statement.");
        await DriveToStatus(requirements, created.Id, from);

        await requirements.SetStatusAsync(created.Id, to);

        var result = await requirements.FindAsync(created.Id);
        Assert.Equal(to, result!.Status);
    }

    [Theory]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Reviewed, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Allocated, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Verified, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Satisfied, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Reviewed)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Allocated)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Verified)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Satisfied)]
    [InlineData(RequirementStatus.Obsolete, RequirementStatus.Obsolete)]
    public async Task SetStatusAsync_ForbiddenTransition_ThrowsInvalidRequirementStatusTransitionException(RequirementStatus from, RequirementStatus to)
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Statement.");
        await DriveToStatus(requirements, created.Id, from);

        var exception = await Assert.ThrowsAsync<InvalidRequirementStatusTransitionException>(() => requirements.SetStatusAsync(created.Id, to));
        Assert.Equal(from, exception.FromStatus);
        Assert.Equal(to, exception.ToStatus);
    }

    [Fact]
    public async Task SetStatusAsync_NonExistentRequirement_ThrowsRequirementNotFoundException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<RequirementNotFoundException>(() => requirements.SetStatusAsync(Guid.NewGuid(), RequirementStatus.Reviewed));
    }

    [Fact]
    public async Task SetStatusAsync_NeverDerivesFromVerificationOutcome()
    {
        // The governing distinction (WP7.2C Requirement Lifecycle Model.md):
        // recording a Fail verification must never itself change Status.
        var (requirements, _, verification) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Statement.");

        var context = new VerificationContext();
        context.RecordCriterion("Some criterion.", isSatisfied: false);
        await verification.RecordAsync(created.Id, VerificationOutcome.Fail, "inspection", context);

        var afterVerification = await requirements.FindAsync(created.Id);
        Assert.Equal(RequirementStatus.Draft, afterVerification!.Status);
    }

    /// <summary>Drives a fresh (Draft) requirement to <paramref name="target"/> along a real, permitted path.</summary>
    private static async Task DriveToStatus(IRequirementsService requirements, Guid requirementId, RequirementStatus target)
    {
        if (target == RequirementStatus.Draft)
            return;

        await requirements.SetStatusAsync(requirementId, RequirementStatus.Reviewed);
        if (target == RequirementStatus.Reviewed)
            return;

        await requirements.SetStatusAsync(requirementId, RequirementStatus.Approved);
        if (target == RequirementStatus.Approved)
            return;

        await requirements.SetStatusAsync(requirementId, RequirementStatus.Allocated);
        if (target == RequirementStatus.Allocated)
            return;

        await requirements.SetStatusAsync(requirementId, RequirementStatus.Verified);
        if (target == RequirementStatus.Verified)
            return;

        await requirements.SetStatusAsync(requirementId, RequirementStatus.Satisfied);
        if (target == RequirementStatus.Satisfied)
            return;

        await requirements.SetStatusAsync(requirementId, RequirementStatus.Obsolete);
    }

    // ------------------------------------------------------------
    // Relationships — LinkAsync / GetRelationshipsAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task LinkAsync_ValidSourceAndTarget_RecordsRelationship()
    {
        var (requirements, _, _) = BuildServices();
        var a = await requirements.CreateAsync("REQ-A", "A.");
        var b = await requirements.CreateAsync("REQ-B", "B.");

        await requirements.LinkAsync(a.Id, b.Id, RequirementRelationshipKinds.DependsOn);

        var relationships = await requirements.GetRelationshipsAsync(a.Id);
        Assert.Contains(relationships, r => r.TargetDocumentId == b.Id && r.RelationshipKind == RequirementRelationshipKinds.DependsOn);
    }

    [Fact]
    public async Task LinkAsync_NonExistentSource_ThrowsRequirementNotFoundException()
    {
        var (requirements, _, _) = BuildServices();
        var b = await requirements.CreateAsync("REQ-B", "B.");
        var badSource = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<RequirementNotFoundException>(
            () => requirements.LinkAsync(badSource, b.Id, RequirementRelationshipKinds.DependsOn));
        Assert.Equal(badSource, exception.RequirementId);
    }

    [Fact]
    public async Task LinkAsync_NonExistentTarget_ThrowsEngineeringDocumentNotFoundException()
    {
        var (requirements, _, _) = BuildServices();
        var a = await requirements.CreateAsync("REQ-A", "A.");

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => requirements.LinkAsync(a.Id, Guid.NewGuid(), RequirementRelationshipKinds.DependsOn));
    }

    [Fact]
    public async Task LinkAsync_InvalidRelationshipKind_ThrowsArgumentException()
    {
        var (requirements, _, _) = BuildServices();
        var a = await requirements.CreateAsync("REQ-A", "A.");
        var b = await requirements.CreateAsync("REQ-B", "B.");

        await Assert.ThrowsAsync<ArgumentException>(() => requirements.LinkAsync(a.Id, b.Id, ""));
    }

    [Fact]
    public async Task GetRelationshipsAsync_NonExistentRequirement_ThrowsRequirementNotFoundException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<RequirementNotFoundException>(() => requirements.GetRelationshipsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRelationshipsAsync_MultipleRelationshipKinds_ReturnsAll()
    {
        var (requirements, documents, _) = BuildServices();
        var a = await requirements.CreateAsync("REQ-A", "A.");
        var b = await requirements.CreateAsync("REQ-B", "B.");
        var external = await documents.CreateAsync("ExternalDoc", "content");

        await requirements.LinkAsync(a.Id, b.Id, RequirementRelationshipKinds.DependsOn);
        await requirements.LinkAsync(a.Id, external.Id, RequirementRelationshipKinds.References);

        var relationships = await requirements.GetRelationshipsAsync(a.Id);
        Assert.Equal(2, relationships.Count);
    }

    // ------------------------------------------------------------
    // Allocation — discipline-neutral targets
    // ------------------------------------------------------------

    [Fact]
    public async Task Allocation_ToArbitraryDocumentKind_Succeeds()
    {
        var (requirements, documents, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");
        var component = await documents.CreateAsync("SampleComponent", "fictional component");

        await requirements.LinkAsync(requirement.Id, component.Id, RequirementRelationshipKinds.AllocatedTo);

        var relationships = await requirements.GetRelationshipsAsync(requirement.Id);
        Assert.Contains(relationships, r => r.TargetDocumentId == component.Id && r.RelationshipKind == RequirementRelationshipKinds.AllocatedTo);
    }

    [Fact]
    public async Task Allocation_ReverseTraceability_AgainstRealDocument_IsAvailable()
    {
        var (requirements, documents, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");
        var component = await documents.CreateAsync("SampleComponent", "fictional component");
        await requirements.LinkAsync(requirement.Id, component.Id, RequirementRelationshipKinds.AllocatedTo);

        // Reverse traceability: which requirements allocate to this component?
        var incoming = await documents.GetReferencesAsync(component.Id);

        // GetReferencesAsync returns only the document's own outgoing references;
        // reverse lookup for a real target is confirmed unavailable this way too —
        // WP7.2C Traceability Contract.md §3's own disclosed limitation applies
        // even to a real document target, not only an open-string one, since
        // IEngineeringDocumentStore itself has no reverse-reference index.
        Assert.Empty(incoming);
    }

    // ------------------------------------------------------------
    // Traceability — DerivesFrom / Satisfies
    // ------------------------------------------------------------

    [Fact]
    public async Task Traceability_DerivesFrom_RecordsBackwardLink()
    {
        var (requirements, _, _) = BuildServices();
        var source = await requirements.CreateAsync("REQ-SOURCE", "Source need.");
        var derived = await requirements.CreateAsync("REQ-DERIVED", "Derived requirement.");

        await requirements.LinkAsync(derived.Id, source.Id, RequirementRelationshipKinds.DerivesFrom);

        var relationships = await requirements.GetRelationshipsAsync(derived.Id);
        Assert.Contains(relationships, r => r.TargetDocumentId == source.Id && r.RelationshipKind == RequirementRelationshipKinds.DerivesFrom);
    }

    [Fact]
    public async Task Traceability_Satisfies_RecordsForwardLink()
    {
        var (requirements, documents, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");
        var design = await documents.CreateAsync("SampleDesignElement", "fictional design");

        await requirements.LinkAsync(design.Id, requirement.Id, RequirementRelationshipKinds.Satisfies);

        var relationships = await requirements.GetRelationshipsAsync(design.Id);
        Assert.Contains(relationships, r => r.TargetDocumentId == requirement.Id && r.RelationshipKind == RequirementRelationshipKinds.Satisfies);
    }

    // ------------------------------------------------------------
    // Requirement Collection
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateCollectionAsync_ValidName_ReturnsEmptyCollection()
    {
        var (requirements, _, _) = BuildServices();

        var collection = await requirements.CreateCollectionAsync("Baseline 1");

        Assert.Equal("Baseline 1", collection.Name);
        Assert.Empty(collection.MemberRequirementIds);
    }

    [Fact]
    public async Task CreateCollectionAsync_InvalidName_ThrowsArgumentException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<ArgumentException>(() => requirements.CreateCollectionAsync(""));
    }

    [Fact]
    public async Task AddToCollectionAsync_ValidMembers_AreRetrievable()
    {
        var (requirements, _, _) = BuildServices();
        var collection = await requirements.CreateCollectionAsync("Baseline 1");
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");

        await requirements.AddToCollectionAsync(collection.Id, requirement.Id);

        var found = await requirements.FindCollectionAsync(collection.Id);
        Assert.Contains(requirement.Id, found!.MemberRequirementIds);
    }

    [Fact]
    public async Task AddToCollectionAsync_NonExistentCollection_ThrowsEngineeringDocumentNotFoundException()
    {
        var (requirements, _, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(() => requirements.AddToCollectionAsync(Guid.NewGuid(), requirement.Id));
    }

    [Fact]
    public async Task AddToCollectionAsync_NonExistentRequirement_ThrowsRequirementNotFoundException()
    {
        var (requirements, _, _) = BuildServices();
        var collection = await requirements.CreateCollectionAsync("Baseline 1");

        await Assert.ThrowsAsync<RequirementNotFoundException>(() => requirements.AddToCollectionAsync(collection.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task FindCollectionAsync_NonExistentCollection_ReturnsNull() =>
        Assert.Null(await BuildServices().Requirements.FindCollectionAsync(Guid.NewGuid()));

    [Fact]
    public async Task AddToCollectionAsync_MultipleMembers_AllRetrievable()
    {
        var (requirements, _, _) = BuildServices();
        var collection = await requirements.CreateCollectionAsync("Baseline 1");
        var a = await requirements.CreateAsync("REQ-A", "A.");
        var b = await requirements.CreateAsync("REQ-B", "B.");

        await requirements.AddToCollectionAsync(collection.Id, a.Id);
        await requirements.AddToCollectionAsync(collection.Id, b.Id);

        var found = await requirements.FindCollectionAsync(collection.Id);
        Assert.Equal(2, found!.MemberRequirementIds.Count);
    }

    // ------------------------------------------------------------
    // Requirement Group
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateGroupAsync_NoParent_ReturnsRootGroup()
    {
        var (requirements, _, _) = BuildServices();

        var group = await requirements.CreateGroupAsync("Structural");

        Assert.Null(group.ParentGroupId);
    }

    [Fact]
    public async Task CreateGroupAsync_WithParent_RecordsHierarchy()
    {
        var (requirements, _, _) = BuildServices();
        var parent = await requirements.CreateGroupAsync("Structural");

        var child = await requirements.CreateGroupAsync("Structural.Loads", parent.Id);

        Assert.Equal(parent.Id, child.ParentGroupId);
    }

    [Fact]
    public async Task CreateGroupAsync_NonExistentParent_ThrowsEngineeringDocumentNotFoundException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(() => requirements.CreateGroupAsync("Child", Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateGroupAsync_InvalidName_ThrowsArgumentException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<ArgumentException>(() => requirements.CreateGroupAsync(""));
    }

    [Fact]
    public async Task FindGroupAsync_ExistingGroup_ReturnsIt()
    {
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateGroupAsync("Structural");

        var found = await requirements.FindGroupAsync(created.Id);
        Assert.Equal("Structural", found!.Name);
    }

    [Fact]
    public async Task FindGroupAsync_NonExistentGroup_ReturnsNull() =>
        Assert.Null(await BuildServices().Requirements.FindGroupAsync(Guid.NewGuid()));

    // ------------------------------------------------------------
    // Verification integration — reuse, not duplication
    // ------------------------------------------------------------

    [Fact]
    public async Task Verification_RecordAsync_AcceptsRequirementIdDirectly_WithNoWrapper()
    {
        var (requirements, _, verification) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");

        var context = new VerificationContext();
        context.RecordCriterion("Demonstrated by inspection.", isSatisfied: true);
        var record = await verification.RecordAsync(requirement.Id, VerificationOutcome.Pass, "inspection", context);

        Assert.Equal(requirement.Id, record.SubjectDocumentId);
    }

    // ------------------------------------------------------------
    // Requirement Evidence — aggregation
    // ------------------------------------------------------------

    [Fact]
    public async Task GetEvidenceAsync_AggregatesVerificationHistoryAndLinkedReferences()
    {
        var (requirements, documents, verification) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");
        var component = await documents.CreateAsync("SampleComponent", "content");
        await requirements.LinkAsync(requirement.Id, component.Id, RequirementRelationshipKinds.AllocatedTo);

        var context = new VerificationContext();
        context.RecordCriterion("Criterion.", isSatisfied: true);
        await verification.RecordAsync(requirement.Id, VerificationOutcome.Pass, "inspection", context);

        var evidence = await requirements.GetEvidenceAsync(requirement.Id);

        Assert.Single(evidence.VerificationHistory);
        Assert.Contains(evidence.LinkedReferences, r => r.TargetDocumentId == component.Id);
    }

    [Fact]
    public async Task GetEvidenceAsync_NonExistentRequirement_ThrowsRequirementNotFoundException()
    {
        var (requirements, _, _) = BuildServices();

        await Assert.ThrowsAsync<RequirementNotFoundException>(() => requirements.GetEvidenceAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetEvidenceAsync_NoVerificationsOrLinks_ReturnsEmptyAggregation()
    {
        var (requirements, _, _) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-001", "Statement.");

        var evidence = await requirements.GetEvidenceAsync(requirement.Id);

        Assert.Empty(evidence.VerificationHistory);
        Assert.Empty(evidence.LinkedReferences);
    }

    [Fact]
    public async Task GetEvidenceAsync_InheritsVerificationReadPermissionGate()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(
            documentStore, principalAccessor, permissionEvaluator, store, new InMemoryEngineeringRelationshipRepository());
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);

        var requirement = await requirementsService.CreateAsync("REQ-001", "Statement.");

        principalAccessor.SetCurrent(BuildPrincipal("no-permissions"));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => requirementsService.GetEvidenceAsync(requirement.Id));
    }

    // ------------------------------------------------------------
    // Serialization
    // ------------------------------------------------------------

    [Fact]
    public void RequirementDto_RoundTripsThroughJsonSerializer()
    {
        var dto = new RequirementDto("REQ-001", "Statement.", "functional", RequirementStatus.Draft, "user-1", DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<RequirementDto>(json);

        Assert.Equal(dto, deserialized);
    }

    [Fact]
    public void RequirementDto_SerializesAsPlainJsonObject()
    {
        var dto = new RequirementDto("REQ-001", "Statement.", "functional", RequirementStatus.Draft, "user-1", DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(dto);

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("REQ-001", parsed.RootElement.GetProperty("Identifier").GetString());
    }

    [Fact]
    public async Task Requirement_PersistedContent_IsRealJson()
    {
        var (requirements, documents, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "Statement.");

        var history = await documents.GetRevisionHistoryAsync(created.Id);
        using var parsed = JsonDocument.Parse(history[0].Content);

        Assert.Equal("REQ-001", parsed.RootElement.GetProperty("Identifier").GetString());
    }

    // ------------------------------------------------------------
    // Equality
    // ------------------------------------------------------------

    [Fact]
    public void RequirementDto_EqualValues_AreEqual()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new RequirementDto("REQ-001", "Statement.", "functional", RequirementStatus.Draft, "user-1", now);
        var b = new RequirementDto("REQ-001", "Statement.", "functional", RequirementStatus.Draft, "user-1", now);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RequirementDto_DifferentStatement_AreNotEqual()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new RequirementDto("REQ-001", "Statement A.", "functional", RequirementStatus.Draft, "user-1", now);
        var b = new RequirementDto("REQ-001", "Statement B.", "functional", RequirementStatus.Draft, "user-1", now);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RequirementDto_With_ProducesModifiedCopyLeavingOriginalUnchanged()
    {
        var original = new RequirementDto("REQ-001", "Original.", null, RequirementStatus.Draft, "user-1", DateTimeOffset.UtcNow);
        var revised = original with { Statement = "Revised." };

        Assert.Equal("Original.", original.Statement);
        Assert.Equal("Revised.", revised.Statement);
    }

    [Fact]
    public void RequirementCollectionDto_EqualValues_AreEqual() =>
        Assert.Equal(new RequirementCollectionDto("Baseline"), new RequirementCollectionDto("Baseline"));

    [Fact]
    public void RequirementGroupDto_EqualValues_AreEqual() =>
        Assert.Equal(new RequirementGroupDto("Structural"), new RequirementGroupDto("Structural"));

    // ------------------------------------------------------------
    // Concurrency
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ConcurrentCallsWithSameIdentifier_OnlyOneSucceeds()
    {
        var (requirements, _, _) = BuildServices();

        var tasks = Enumerable.Range(0, 15)
            .Select(_ => requirements.CreateAsync("REQ-CONCURRENT", "Statement."))
            .ToArray();

        var results = await Task.WhenAll(tasks.Select(async t =>
        {
            try { await t; return true; }
            catch (DuplicateRequirementIdentifierException) { return false; }
        }));

        Assert.Single(results, succeeded => succeeded);
    }

    [Fact]
    public async Task CreateAsync_ConcurrentCallsWithDifferentIdentifiers_AllSucceed()
    {
        var (requirements, _, _) = BuildServices();

        var tasks = Enumerable.Range(0, 15)
            .Select(i => requirements.CreateAsync($"REQ-{i}", "Statement."))
            .ToArray();

        await Task.WhenAll(tasks);

        var all = await requirements.ListAsync();
        Assert.Equal(15, all.Count);
    }

    [Fact]
    public async Task ReviseAsync_ConcurrentRevisions_NeverProduceDuplicateRevisionNumbers()
    {
        var (requirements, documents, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "V0.");

        var tasks = Enumerable.Range(0, 15)
            .Select(i => requirements.ReviseAsync(created.Id, $"V{i}.", null))
            .ToArray();

        await Task.WhenAll(tasks);

        var history = await documents.GetRevisionHistoryAsync(created.Id);
        var revisionNumbers = history.Select(r => r.RevisionNumber).ToList();
        Assert.Equal(revisionNumbers.Distinct().Count(), revisionNumbers.Count);
    }

    // ------------------------------------------------------------
    // Failure — persistence propagation
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistenceStoreUnavailable_PropagatesUnmodified()
    {
        var store = new FailingPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var workingStore = new InMemoryQueryablePersistenceStore();
        var verification = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), workingStore, new InMemoryEngineeringRelationshipRepository());
        var requirements = new RequirementsService(documentStore, store, principalAccessor, verification);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => requirements.CreateAsync("REQ-001", "Statement."));
    }

    // ------------------------------------------------------------
    // Atomicity (`TD-67`) — the document and its identifier-index entry
    // are one transaction, the same shape and the same primitive
    // VerificationService.RecordAsync's own TD-23 closure uses.
    // ------------------------------------------------------------

    /// <summary>
    /// A fault injected after the transaction body has fully run — the
    /// document and its identifier-index entry both staged — but before
    /// the commit lands, leaves nothing durable: no document, and no
    /// identifier-index entry either. Before `TD-67`'s closure the two
    /// writes were independent, so a crash in this exact window left a
    /// document that existed but could never be found by its own
    /// identifier — precisely the "invisible orphan" the row named, and
    /// exactly the scenario `RequirementsReconciliationServiceTests`'s own
    /// suite exists to detect and repair after the fact rather than
    /// prevent.
    /// </summary>
    [Fact]
    public async Task CreateAsync_CommitFails_LeavesNothingDurable_NotEvenTheDocument()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(backing, principalAccessor);
        var failable = new FailableTransactionStore(backing) { FailNextCommit = true };
        var verification = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), backing, new InMemoryEngineeringRelationshipRepository());
        var requirements = new RequirementsService(documentStore, failable, principalAccessor, verification);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => requirements.CreateAsync("REQ-RACE-1", "The system shall survive a failed commit."));

        Assert.Equal(1, failable.BodiesCompleted);
        Assert.Null(await requirements.FindByIdentifierAsync("REQ-RACE-1"));
        Assert.Empty(await backing.ReadAllAsync(RequirementsService.IdentifierIndexCollectionName));
    }

    /// <summary>
    /// Wraps a <see cref="InMemoryQueryablePersistenceStore"/> so a test can
    /// inject a commit failure (via <see cref="CommitFailingPersistenceStore"/>)
    /// while still satisfying <see cref="RequirementsService"/>'s single
    /// <see cref="IPersistenceStore"/> constructor parameter, which it also
    /// casts to <see cref="IQueryablePersistenceStore"/> internally
    /// (`TD-67`) — every plain <see cref="IPersistenceStore"/> read goes
    /// straight to the backing store, so a caller can read what really
    /// committed exactly as <c>TestEngineeringDomain.NewContextOver</c>'s
    /// own <c>transactional</c>/<c>backing</c> split does.
    /// </summary>
    private sealed class FailableTransactionStore : IPersistenceStore, IQueryablePersistenceStore
    {
        private readonly InMemoryQueryablePersistenceStore _backing;
        private readonly CommitFailingPersistenceStore _failing;

        public FailableTransactionStore(InMemoryQueryablePersistenceStore backing)
        {
            _backing = backing;
            _failing = new CommitFailingPersistenceStore(backing);
        }

        public bool FailNextCommit
        {
            get => _failing.FailNextCommit;
            set => _failing.FailNextCommit = value;
        }

        public int BodiesCompleted => _failing.BodiesCompleted;

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _backing.ReadAsync(collection, key, cancellationToken);

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
            _backing.WriteAsync(collection, key, value, cancellationToken);

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _backing.DeleteAsync(collection, key, cancellationToken);

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
            _backing.ListKeysAsync(collection, cancellationToken);

        public long CurrentSequence => _backing.CurrentSequence;

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
            _backing.ListKeysAsync(collection, keyPrefix, cancellationToken);

        public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
            _backing.ReadAllAsync(collection, cancellationToken);

        public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            _backing.ReadManyAsync(collection, keys, cancellationToken);

        public Task ExecuteInTransactionAsync(Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            _failing.ExecuteInTransactionAsync(work, cancellationToken);

        public Task<T> ExecuteInReadTransactionAsync<T>(Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) =>
            _backing.ExecuteInReadTransactionAsync(read, cancellationToken);

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            _backing.SearchAsync(query, limit, cancellationToken);

        public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) =>
            _backing.IsSearchIndexEmptyAsync(cancellationToken);
    }

    // ------------------------------------------------------------
    // Change feed publishing (`TD-28`) — every mutator publishes exactly
    // once, after its own commit, naming the touched object, its Kind, and
    // the right WorkspaceChangeType; a refused write publishes nothing.
    // ------------------------------------------------------------

    /// <summary>Captures every <see cref="WorkspaceChange"/> published, in order — a docked-style subscriber stand-in.</summary>
    private sealed class FakeWorkspaceChangePublisher : IWorkspaceChangePublisher
    {
        public List<WorkspaceChange> Published { get; } = [];

        public void Publish(WorkspaceChange change) => Published.Add(change);
    }

    private static void AssertPublishedOnce(FakeWorkspaceChangePublisher publisher, Guid objectId, string kind, WorkspaceChangeType changeType)
    {
        var change = Assert.Single(publisher.Published);
        var entry = Assert.Single(change.Entries, e => e.ObjectId == objectId);
        Assert.Equal(kind, entry.Kind);
        Assert.Equal(changeType, entry.ChangeType);
        Assert.True(change.Sequence > 0, "A committed write must report a real, advanced sequence number.");
    }

    [Fact]
    public async Task CreateAsync_PublishesCreated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);

        var created = await requirements.CreateAsync("REQ-PUB-001", "Statement.");

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Created);
    }

    [Fact]
    public async Task ReviseAsync_PublishesUpdated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-002", "Statement.");
        publisher.Published.Clear();

        await requirements.ReviseAsync(created.Id, "Revised.", null);

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Updated);
    }

    [Fact]
    public async Task SetStatusAsync_PublishesStatusChanged()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-003", "Statement.");
        publisher.Published.Clear();

        await requirements.SetStatusAsync(created.Id, RequirementStatus.Reviewed);

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.StatusChanged);
    }

    [Fact]
    public async Task SetOwnerAsync_PublishesUpdated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-004", "Statement.");
        publisher.Published.Clear();

        await requirements.SetOwnerAsync(created.Id, "owner-1");

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Updated);
    }

    [Fact]
    public async Task SetPriorityAsync_PublishesUpdated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-005", "Statement.");
        publisher.Published.Clear();

        await requirements.SetPriorityAsync(created.Id, RequirementPriority.Critical);

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Updated);
    }

    [Fact]
    public async Task DeleteAsync_PublishesDeleted()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-006", "Statement.");
        publisher.Published.Clear();

        await requirements.DeleteAsync(created.Id);

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Deleted);
    }

    [Fact]
    public async Task MoveToGroupAsync_PublishesMoved()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-007", "Statement.");
        var group = await requirements.CreateGroupAsync("Group 1");
        publisher.Published.Clear();

        await requirements.MoveToGroupAsync(created.Id, group.Id);

        AssertPublishedOnce(publisher, created.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Moved);
    }

    [Fact]
    public async Task LinkAsync_PublishesUpdated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var first = await requirements.CreateAsync("REQ-PUB-008", "Statement.");
        var second = await requirements.CreateAsync("REQ-PUB-009", "Statement.");
        publisher.Published.Clear();

        await requirements.LinkAsync(first.Id, second.Id, RequirementRelationshipKinds.References);

        AssertPublishedOnce(publisher, first.Id, RequirementsService.RequirementDocumentKind, WorkspaceChangeType.Updated);
    }

    [Fact]
    public async Task CreateCollectionAsync_PublishesCreated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);

        var collection = await requirements.CreateCollectionAsync("Baseline 1");

        AssertPublishedOnce(publisher, collection.Id, RequirementsService.RequirementCollectionDocumentKind, WorkspaceChangeType.Created);
    }

    [Fact]
    public async Task AddToCollectionAsync_PublishesUpdated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var collection = await requirements.CreateCollectionAsync("Baseline 1");
        var requirement = await requirements.CreateAsync("REQ-PUB-010", "Statement.");
        publisher.Published.Clear();

        await requirements.AddToCollectionAsync(collection.Id, requirement.Id);

        AssertPublishedOnce(publisher, collection.Id, RequirementsService.RequirementCollectionDocumentKind, WorkspaceChangeType.Updated);
    }

    [Fact]
    public async Task DeleteCollectionAsync_PublishesDeleted()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var collection = await requirements.CreateCollectionAsync("Baseline 1");
        publisher.Published.Clear();

        await requirements.DeleteCollectionAsync(collection.Id);

        AssertPublishedOnce(publisher, collection.Id, RequirementsService.RequirementCollectionDocumentKind, WorkspaceChangeType.Deleted);
    }

    [Fact]
    public async Task CreateGroupAsync_PublishesCreated()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);

        var group = await requirements.CreateGroupAsync("Structural");

        AssertPublishedOnce(publisher, group.Id, RequirementsService.RequirementGroupDocumentKind, WorkspaceChangeType.Created);
    }

    [Fact]
    public async Task MoveGroupAsync_PublishesMoved()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var parent = await requirements.CreateGroupAsync("Parent");
        var child = await requirements.CreateGroupAsync("Child");
        publisher.Published.Clear();

        await requirements.MoveGroupAsync(child.Id, parent.Id);

        AssertPublishedOnce(publisher, child.Id, RequirementsService.RequirementGroupDocumentKind, WorkspaceChangeType.Moved);
    }

    [Fact]
    public async Task DeleteGroupAsync_PublishesDeleted()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var group = await requirements.CreateGroupAsync("Structural");
        publisher.Published.Clear();

        await requirements.DeleteGroupAsync(group.Id);

        AssertPublishedOnce(publisher, group.Id, RequirementsService.RequirementGroupDocumentKind, WorkspaceChangeType.Deleted);
    }

    [Fact]
    public async Task SetStatusAsync_ForbiddenTransition_PublishesNothing()
    {
        var publisher = new FakeWorkspaceChangePublisher();
        var (requirements, _, _) = BuildServices(workspaceChanges: publisher);
        var created = await requirements.CreateAsync("REQ-PUB-011", "Statement.");
        publisher.Published.Clear();

        // Draft -> Verified is not a permitted transition (see the
        // exhaustive lifecycle table above) — refused before any
        // transaction is ever opened.
        await Assert.ThrowsAsync<InvalidRequirementStatusTransitionException>(
            () => requirements.SetStatusAsync(created.Id, RequirementStatus.Verified));

        Assert.Empty(publisher.Published);
    }

    // ------------------------------------------------------------
    // Regression
    // ------------------------------------------------------------

    [Fact]
    public async Task Regression_IdentifierIndex_SurvivesAcrossMultipleOperations()
    {
        // Regression guard: the identifier index must remain correct after
        // create, revise, and status-transition operations against the same
        // requirement — a real defect class if the index were ever
        // accidentally rewritten or dropped during ReviseAsync.
        var (requirements, _, _) = BuildServices();
        var created = await requirements.CreateAsync("REQ-001", "V0.");
        await requirements.ReviseAsync(created.Id, "V1.", null);
        await requirements.SetStatusAsync(created.Id, RequirementStatus.Reviewed);

        var found = await requirements.FindByIdentifierAsync("REQ-001");
        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
    }
}

using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Verification;

public class VerificationServiceTests
{
    private static VerificationService BuildService(out EngineeringDocumentStore documentStore, out CurrentPrincipalAccessor accessor)
    {
        accessor = new CurrentPrincipalAccessor();
        var store = new InMemoryQueryablePersistenceStore();
        documentStore = new EngineeringDocumentStore(store, accessor);
        return new VerificationService(
            documentStore, accessor, new PermissionEvaluator(), store, new InMemoryEngineeringRelationshipRepository());
    }

    private static IPrincipal BuildPrincipal(string id, params Permission[] permissions) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), permissions);

    private static VerificationContext BuildContext()
    {
        var context = new VerificationContext();
        context.RecordCriterion("Sample dimension within allowable.", isSatisfied: true, detail: "Test detail.");
        context.RecordEvidence("Sample inspection note.", reference: "test-report-001");
        return context;
    }

    // ----------------------------------------------------------------
    // RecordAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_ValidSubject_ReturnsRecord_WithGivenOutcomeAndMethod()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "subject content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

        Assert.Equal(subject.Id, record.SubjectDocumentId);
        Assert.Equal(VerificationOutcome.Pass, record.Outcome);
        Assert.Equal("inspection", record.Method);
        Assert.Equal(1, record.RevisionNumber);
        Assert.NotEqual(Guid.Empty, record.Id);
    }

    [Fact]
    public async Task RecordAsync_NonExistentSubject_ThrowsEngineeringDocumentNotFoundException()
    {
        var service = BuildService(out _, out _);
        var missingId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => service.RecordAsync(missingId, VerificationOutcome.Pass, "inspection", new VerificationContext()));

        Assert.Equal(missingId, exception.DocumentId);
    }

    [Fact]
    public async Task RecordAsync_NullMethod_ThrowsArgumentNullException()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RecordAsync(subject.Id, VerificationOutcome.Pass, null!, new VerificationContext()));
    }

    [Fact]
    public async Task RecordAsync_WhitespaceMethod_ThrowsArgumentException()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordAsync(subject.Id, VerificationOutcome.Pass, "   ", new VerificationContext()));
    }

    [Fact]
    public async Task RecordAsync_NullContext_ThrowsArgumentNullException()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", null!));
    }

    [Fact]
    public async Task RecordAsync_RecordsCriteriaAndEvidence()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", BuildContext());

        var criterion = Assert.Single(record.Criteria);
        Assert.Equal("Sample dimension within allowable.", criterion.Description);
        Assert.True(criterion.IsSatisfied);

        var evidence = Assert.Single(record.Evidence);
        Assert.Equal("Sample inspection note.", evidence.Description);
        Assert.Equal("test-report-001", evidence.Reference);
    }

    [Fact]
    public async Task RecordAsync_NoCriteriaOrEvidence_RecordsEmptyLists()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

        Assert.Empty(record.Criteria);
        Assert.Empty(record.Evidence);
        Assert.Empty(record.LinkedDocumentIds);
        Assert.Empty(record.LinkedCalculationRecordIds);
        Assert.Empty(record.ReferencedMaterialIds);
    }

    [Fact]
    public async Task RecordAsync_LinksAdditionalDocument_RetrievableThroughEngineeringDocumentStore()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        var standard = await documentStore.CreateAsync("Standard", "governing standard content");

        var context = new VerificationContext();
        context.LinkDocument(standard.Id);
        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "analysis", context);

        Assert.Equal([standard.Id], record.LinkedDocumentIds);

        var references = await documentStore.GetReferencesAsync(record.Id);
        var reference = Assert.Single(references);
        Assert.Equal(standard.Id, reference.TargetDocumentId);
        Assert.Equal(VerificationService.ReferencesRelationshipKind, reference.RelationshipKind);
    }

    [Fact]
    public async Task RecordAsync_NonExistentLinkedDocument_ThrowsEngineeringDocumentNotFoundException()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        var missingLinkedId = Guid.NewGuid();

        var context = new VerificationContext();
        context.LinkDocument(missingLinkedId);

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => service.RecordAsync(subject.Id, VerificationOutcome.Pass, "analysis", context));
    }

    [Fact]
    public async Task RecordAsync_LinksCalculationRecord_RecordedAndRetrievable()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        // Simulates a real Calculations.CalculationRecord<TResult>.Id - itself
        // just an EngineeringDocument Id, requiring no compile-time
        // dependency on Tempest.Core.Calculations to link to.
        var calculationRecordDocument = await documentStore.CreateAsync("CalculationRecord", "{}");

        var context = new VerificationContext();
        context.LinkCalculationRecord(calculationRecordDocument.Id);
        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "analysis", context);

        Assert.Equal([calculationRecordDocument.Id], record.LinkedCalculationRecordIds);

        var references = await documentStore.GetReferencesAsync(record.Id);
        var reference = Assert.Single(references);
        Assert.Equal(VerificationService.BasedOnCalculationRelationshipKind, reference.RelationshipKind);
    }

    [Fact]
    public async Task RecordAsync_ReferencesMaterial_RecordsOpenUnvalidatedString()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var context = new VerificationContext();
        context.ReferenceMaterial("material-that-does-not-need-to-exist");
        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "analysis", context);

        Assert.Equal(["material-that-does-not-need-to-exist"], record.ReferencedMaterialIds);
    }

    [Fact]
    public async Task RecordAsync_NoPrincipalEstablished_RecordsUnknownVerifier()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

        Assert.Equal(VerificationService.UnknownVerifierPrincipalId, record.VerifiedByPrincipalId);
    }

    [Fact]
    public async Task RecordAsync_PrincipalEstablished_RecordsItsIdentity()
    {
        var service = BuildService(out var documentStore, out var accessor);
        accessor.SetCurrent(BuildPrincipal("verifier-1"));
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

        Assert.Equal("verifier-1", record.VerifiedByPrincipalId);
    }

    [Fact]
    public async Task RecordAsync_Id_IsDirectlyRetrievableThroughEngineeringDocumentStore()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Fail, "test", new VerificationContext());

        var document = await documentStore.FindAsync(record.Id);
        Assert.NotNull(document);
        Assert.Equal(VerificationService.VerificationRecordDocumentKind, document!.Kind);
    }

    // ----------------------------------------------------------------
    // GetVerificationHistoryAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetVerificationHistoryAsync_NoPrincipalEstablished_ThrowsPermissionDeniedException()
    {
        var service = BuildService(out _, out _);

        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.GetVerificationHistoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_PrincipalLacksPermission_ThrowsPermissionDeniedException()
    {
        var service = BuildService(out _, out var accessor);
        accessor.SetCurrent(BuildPrincipal("someone"));

        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.GetVerificationHistoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_PrincipalHoldsPermission_ReturnsRecords()
    {
        var service = BuildService(out var documentStore, out var accessor);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

        accessor.SetCurrent(BuildPrincipal("verifier", VerificationService.ReadPermission));
        var history = await service.GetVerificationHistoryAsync(subject.Id);

        var record = Assert.Single(history);
        Assert.Equal(VerificationOutcome.Pass, record.Outcome);
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_NoVerificationsRecorded_ReturnsEmpty()
    {
        var service = BuildService(out var documentStore, out var accessor);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        accessor.SetCurrent(BuildPrincipal("verifier", VerificationService.ReadPermission));

        var history = await service.GetVerificationHistoryAsync(subject.Id);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_NonExistentSubject_ReturnsEmpty()
    {
        var service = BuildService(out _, out var accessor);
        accessor.SetCurrent(BuildPrincipal("verifier", VerificationService.ReadPermission));

        var history = await service.GetVerificationHistoryAsync(Guid.NewGuid());

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_MultipleVerifications_ReturnsAllOrderedByVerifiedAt()
    {
        var service = BuildService(out var documentStore, out var accessor);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        await service.RecordAsync(subject.Id, VerificationOutcome.Fail, "inspection", new VerificationContext());
        await Task.Delay(20);
        await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "test", new VerificationContext());

        accessor.SetCurrent(BuildPrincipal("verifier", VerificationService.ReadPermission));
        var history = await service.GetVerificationHistoryAsync(subject.Id);

        Assert.Equal(2, history.Count);
        Assert.Equal([VerificationOutcome.Fail, VerificationOutcome.Pass], history.Select(r => r.Outcome));
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_OnlyReturnsVerificationReferences_NotOtherRelationshipKinds()
    {
        var service = BuildService(out var documentStore, out var accessor);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        var other = await documentStore.CreateAsync("Requirement", "other content");
        await documentStore.LinkAsync(subject.Id, other.Id, "unrelatedRelationship");

        accessor.SetCurrent(BuildPrincipal("verifier", VerificationService.ReadPermission));
        var history = await service.GetVerificationHistoryAsync(subject.Id);

        Assert.Empty(history);
    }

    // ----------------------------------------------------------------
    // Concurrency
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_ConcurrentCallsAgainstSameSubject_AllSucceedAndAppearInHistory()
    {
        var service = BuildService(out var documentStore, out var accessor);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var tasks = Enumerable.Range(0, 15)
            .Select(i => service.RecordAsync(subject.Id, VerificationOutcome.Pass, $"method-{i}", new VerificationContext()))
            .ToArray();
        await Task.WhenAll(tasks);

        accessor.SetCurrent(BuildPrincipal("verifier", VerificationService.ReadPermission));
        var history = await service.GetVerificationHistoryAsync(subject.Id);

        Assert.Equal(15, history.Count);
        Assert.Equal(15, history.Select(r => r.Id).Distinct().Count());
    }

    // ----------------------------------------------------------------
    // Serialization
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_UnderlyingDocumentContent_ContainsSerializedVerificationData()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");

        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Conditional, "demonstration", BuildContext());

        var history = await documentStore.GetRevisionHistoryAsync(record.Id);
        using var json = JsonDocument.Parse(history[0].Content);
        Assert.Equal("demonstration", json.RootElement.GetProperty("Method").GetString());
        Assert.Equal(subject.Id, json.RootElement.GetProperty("SubjectDocumentId").GetGuid());
    }

    // ----------------------------------------------------------------
    // TD-23 — the record and its links are one transaction
    // TD-32 — the "verifiedBy" link is visible to the relationship
    // repository / RelationshipDiscoveryService (the Digital Thread)
    // ----------------------------------------------------------------

    /// <summary>
    /// A fault injected after the whole transaction body has run — the
    /// document, the "verifiedBy" link, and every additional link all
    /// staged — but before the commit lands, leaves nothing durable: no
    /// verification record, no "verifiedBy" reference from the subject,
    /// and nothing recorded with the relationship repository (`TD-23`).
    /// The strongest form of the fact, mirroring
    /// <c>CommitFailingPersistenceStore</c>'s own established convention
    /// elsewhere in this suite (`R7RegressionProofTests`).
    /// </summary>
    [Fact]
    public async Task RecordAsync_CommitFails_LeavesNothingDurable_NotEvenTheDocument()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(backing, accessor);
        var failing = new CommitFailingPersistenceStore(backing) { FailNextCommit = true };
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var service = new VerificationService(documentStore, accessor, new PermissionEvaluator(), failing, relationshipRepository);

        var subject = await documentStore.CreateAsync("Requirement", "content");
        var standard = await documentStore.CreateAsync("Standard", "governing standard content");
        var context = new VerificationContext();
        context.LinkDocument(standard.Id);

        await Assert.ThrowsAsync<Tempest.Core.Persistence.PersistenceStoreUnavailableException>(
            () => service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", context));

        Assert.Equal(1, failing.BodiesCompleted);

        // Nothing durable: no "verifiedBy" reference from the subject...
        var references = await documentStore.GetReferencesAsync(subject.Id);
        Assert.DoesNotContain(references, r => r.RelationshipKind == VerificationService.VerifiedByRelationshipKind);

        // ...and nothing recorded with the relationship repository either.
        Assert.Empty(await relationshipRepository.GetOutgoingAsync(subject.Id));
    }

    /// <summary>
    /// A missing linked document used to leave the verification record
    /// itself durably created, with the failed link the only thing that
    /// did not land — a durably committed, partly linked record (`TD-23`).
    /// Now the whole write is one transaction, so the failure leaves the
    /// subject showing no "verifiedBy" reference at all.
    /// </summary>
    [Fact]
    public async Task RecordAsync_NonExistentLinkedDocument_LeavesNoVerifiedByReference_NotEvenTheRecord()
    {
        var service = BuildService(out var documentStore, out _);
        var subject = await documentStore.CreateAsync("Requirement", "content");
        var missingLinkedId = Guid.NewGuid();

        var context = new VerificationContext();
        context.LinkDocument(missingLinkedId);

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => service.RecordAsync(subject.Id, VerificationOutcome.Pass, "analysis", context));

        var references = await documentStore.GetReferencesAsync(subject.Id);
        Assert.DoesNotContain(references, r => r.RelationshipKind == VerificationService.VerifiedByRelationshipKind);
    }

    /// <summary>
    /// After a successful <see cref="VerificationService.RecordAsync"/>,
    /// the "verifiedBy" link is discoverable through
    /// <see cref="IEngineeringRelationshipRepository"/> — and therefore
    /// <see cref="RelationshipDiscoveryService"/>, the Digital Thread and
    /// impact analysis — from both ends, in this same session, without
    /// any restart or rehydration (`TD-32`).
    /// </summary>
    [Fact]
    public async Task RecordAsync_RegistersVerifiedByLink_DiscoverableFromBothEnds_InThisSameSession()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, accessor);
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var objectRepository = new InMemoryEngineeringObjectRepository();
        var discovery = new RelationshipDiscoveryService(relationshipRepository, objectRepository);
        var service = new VerificationService(documentStore, accessor, new PermissionEvaluator(), store, relationshipRepository);

        var subject = await documentStore.CreateAsync("Requirement", "content");
        var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

        var outgoing = await discovery.GetOutgoingAsync(subject.Id);
        Assert.Contains(outgoing, r =>
            r.TargetId == record.Id && r.RelationshipKind == VerificationService.VerifiedByRelationshipKind
            && r.Category == RelationshipCategory.Verification);

        var incoming = await discovery.GetIncomingAsync(record.Id);
        Assert.Contains(incoming, r => r.SourceId == subject.Id && r.RelationshipKind == VerificationService.VerifiedByRelationshipKind);
    }

    // ----------------------------------------------------------------
    // Constructor validation / failure injection
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullDocumentStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new VerificationService(
                null!, new CurrentPrincipalAccessor(), new PermissionEvaluator(),
                new InMemoryQueryablePersistenceStore(), new InMemoryEngineeringRelationshipRepository()));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(
            () => new VerificationService(
                documentStore, null!, new PermissionEvaluator(),
                new InMemoryQueryablePersistenceStore(), new InMemoryEngineeringRelationshipRepository()));
    }

    [Fact]
    public void Constructor_NullPermissionEvaluator_ThrowsArgumentNullException()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(
            () => new VerificationService(
                documentStore, new CurrentPrincipalAccessor(), null!,
                new InMemoryQueryablePersistenceStore(), new InMemoryEngineeringRelationshipRepository()));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(
            () => new VerificationService(
                documentStore, new CurrentPrincipalAccessor(), new PermissionEvaluator(),
                null!, new InMemoryEngineeringRelationshipRepository()));
    }

    [Fact]
    public void Constructor_NullRelationshipRepository_ThrowsArgumentNullException()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(
            () => new VerificationService(
                documentStore, new CurrentPrincipalAccessor(), new PermissionEvaluator(),
                new InMemoryQueryablePersistenceStore(), null!));
    }

    [Fact]
    public void Constructor_DocumentStoreNotTransactional_ThrowsArgumentException()
    {
        var nonTransactional = new NonTransactionalDocumentStore();

        Assert.Throws<ArgumentException>(
            () => new VerificationService(
                nonTransactional, new CurrentPrincipalAccessor(), new PermissionEvaluator(),
                new InMemoryQueryablePersistenceStore(), new InMemoryEngineeringRelationshipRepository()));
    }

    [Fact]
    public async Task RecordAsync_PersistenceUnavailable_PropagatesExceptionUnmodified()
    {
        var accessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(new FailingPersistenceStore(), accessor);
        var service = new VerificationService(
            documentStore, accessor, new PermissionEvaluator(),
            new InMemoryQueryablePersistenceStore(), new InMemoryEngineeringRelationshipRepository());

        await Assert.ThrowsAsync<Tempest.Core.Persistence.PersistenceStoreUnavailableException>(
            () => service.RecordAsync(Guid.NewGuid(), VerificationOutcome.Pass, "inspection", new VerificationContext()));
    }
}

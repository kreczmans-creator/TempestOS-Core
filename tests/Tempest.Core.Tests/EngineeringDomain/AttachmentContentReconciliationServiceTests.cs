using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-97` closure tests: attachment content is released when the owning
/// object is deleted, and <see cref="AttachmentContentReconciliationService"/>
/// finds and collects content nothing references, without ever touching
/// content a live attachment still holds.
/// </summary>
public sealed class AttachmentContentReconciliationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-attachment-reconciliation-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed record Fixture(EngineeringDomainContext Context, PersistenceStore Persistence, AttachmentContentStore ContentStore, EngineeringObjectStateStore StateStore);

    private Fixture Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _root),
            ]))
            .Build();

        var persistence = new PersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var documents = new InMemoryEngineeringDocumentStore(principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var stateStore = new EngineeringObjectStateStore(persistence);
        var contentStore = new AttachmentContentStore(persistence);

        var context = new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore, contentStore);

        return new Fixture(context, persistence, contentStore, stateStore);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    // ---- Direct half: content released on delete ----

    [Fact]
    public async Task DeleteAsync_ReleasesTheObjectsAttachmentContent()
    {
        var fixture = Build();
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

        await part.DeleteAsync();

        var result = await fixture.ContentStore.ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes);
        Assert.Equal(AttachmentContentStatus.Missing, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_NeverErasesTheAttachmentsOwnMetadata()
    {
        var fixture = Build();
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

        await part.DeleteAsync();

        var attachments = await part.GetAttachmentsAsync();
        Assert.Contains(attachments, a => a.Id == attachment.Id);
    }

    [Fact]
    public async Task DeleteAsync_OnAnObjectWithNoAttachmentContentStoreConfigured_StillSucceeds()
    {
        // Mirrors every other AttachmentContentStore-optional call site
        // (AttachAsync/ReadAttachmentContentAsync): a domain with no
        // content store configured must keep behaving exactly as it did
        // before `TD-31` existed.
        var principal = new CurrentPrincipalAccessor();
        var documents = new InMemoryEngineeringDocumentStore(principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var context = new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal);

        var part = await CreatePartAsync(context, "PART-1", "Bracket");
        await part.AttachAsync(new Attachment("notes.txt", "text/plain", 10));

        await part.DeleteAsync();

        Assert.True(part.IsDeleted);
    }

    // ---- Sweep half: orphaned content ----

    [Fact]
    public async Task DetectAsync_FindsAContentRecordNothingReferences()
    {
        var fixture = Build();
        var orphanId = Guid.NewGuid();
        await fixture.ContentStore.SaveAsync(orphanId, new byte[] { 9, 9, 9 });

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore);
        var report = await sweep.DetectAsync();

        var orphan = Assert.Single(report.Orphans);
        Assert.Equal(orphanId, orphan.AttachmentId);
        Assert.False(orphan.Collected);
    }

    [Fact]
    public async Task DetectAsync_DoesNotChangeAnything()
    {
        var fixture = Build();
        var orphanId = Guid.NewGuid();
        await fixture.ContentStore.SaveAsync(orphanId, new byte[] { 9, 9, 9 });

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore);
        await sweep.DetectAsync();

        var result = await fixture.ContentStore.ReadAsync(orphanId, expectedHash: null, expectedSizeInBytes: 3);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);
    }

    [Fact]
    public async Task SweepAsync_CollectsTheOrphanedContent()
    {
        var fixture = Build();
        var orphanId = Guid.NewGuid();
        await fixture.ContentStore.SaveAsync(orphanId, new byte[] { 9, 9, 9 });

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore);
        var report = await sweep.SweepAsync();

        Assert.True(Assert.Single(report.Orphans).Collected);
        var result = await fixture.ContentStore.ReadAsync(orphanId, expectedHash: null, expectedSizeInBytes: 3);
        Assert.Equal(AttachmentContentStatus.Missing, result.Status);
    }

    [Fact]
    public async Task SweepAsync_NeverCollectsContentALiveObjectStillReferences()
    {
        var fixture = Build();
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

        // A second, unrelated orphan alongside the live one — the sweep
        // must tell them apart, not treat "any orphan exists" as licence
        // to collect everything.
        var orphanId = Guid.NewGuid();
        await fixture.ContentStore.SaveAsync(orphanId, new byte[] { 9, 9, 9 });

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore);
        var report = await sweep.SweepAsync();

        Assert.Single(report.Orphans);
        Assert.DoesNotContain(report.Orphans, o => o.AttachmentId == attachment.Id);

        var liveResult = await fixture.ContentStore.ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes);
        Assert.Equal(AttachmentContentStatus.Available, liveResult.Status);
    }

    [Fact]
    public async Task SweepAsync_NeverCollectsContentASoftDeletedObjectsMetadataStillNames()
    {
        // The direct fix already released this content at delete time;
        // the sweep must not treat the still-present metadata record as
        // licence to touch it a second time (it has nothing to collect —
        // ReadAsync already reports Missing — but this proves the sweep
        // does not, say, throw or mis-report on a state/content mismatch
        // it did not create).
        var fixture = Build();
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");
        await part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });
        await part.DeleteAsync();

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore);
        var report = await sweep.DetectAsync();

        Assert.Empty(report.Orphans);
    }

    [Fact]
    public void Constructor_NullPersistenceStore_Throws()
    {
        var stateStore = new EngineeringObjectStateStore(new PersistenceStore(new ConfigurationBuilder().Build()));
        var contentStore = new AttachmentContentStore(new PersistenceStore(new ConfigurationBuilder().Build()));

        Assert.Throws<ArgumentNullException>(() => new AttachmentContentReconciliationService(null!, stateStore, contentStore));
    }
}

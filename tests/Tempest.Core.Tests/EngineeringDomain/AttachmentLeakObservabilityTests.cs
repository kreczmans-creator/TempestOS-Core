using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R6` answered board 5's "a bounded leak nobody can see is
/// indistinguishable from a leak nobody has" by adding
/// <see cref="AttachmentContentReconciliationReport.SkippedByMarker"/>.
/// This proves it end to end, <b>on the path that actually creates the
/// leak</b>.
/// </summary>
/// <remarks>
/// <para>
/// The existing cover for that member drives the sweep against a marker
/// placed by hand. That proves the sweep reports what it is given; it does
/// not prove that the one production sequence which strands a marker
/// produces a marker the sweep then reports. Those are different claims,
/// and only the second one closes the finding — so this test makes a real
/// <see cref="EngineeringObjectBase.AttachContentAsync"/> fail at its
/// state write, over a real <see cref="PersistenceStore"/> on disk, and
/// then asks the sweep.
/// </para>
/// <para>
/// This file cannot be compiled against the pre-`WP 16.4B-R6` tree, since
/// <c>SkippedByMarker</c> did not exist there. Its "fails before"
/// direction was therefore established by mutation instead — restoring the
/// silent <c>continue</c> in
/// <see cref="AttachmentContentReconciliationService"/> makes it fail —
/// which is the same method the fix's own author used, applied
/// independently to a different assertion.
/// </para>
/// </remarks>
public sealed class AttachmentLeakObservabilityTests : IDisposable
{
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-r6c-leak-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// A state write that fails strands a marker over real bytes, the
    /// sweep declines to collect them — and says so, by name.
    /// </summary>
    [Fact]
    public async Task AStateWriteFailureDuringAnAttach_ProducesALeakTheSweepNamesRatherThanPassesOverInSilence()
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _root),
            ]))
            .Build();

        var persistence = new PersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        var stateStore = new FailableObjectStateStore(new EngineeringObjectStateStore(persistence));
        var contentStore = new AttachmentContentStore(persistence);
        var writeIntentStore = new AttachmentWriteIntentStore(persistence);

        var context = new EngineeringDomainContext(
            new EngineeringDocumentStore(persistence, principal), repository, relationships,
            new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore, contentStore, writeIntentStore);

        var part = (Part)await new EngineeringObjectFactory<Part>(
                "Part", context, (d, r) => new Part(d, r, context, "PRT-1", "Bracket", EngineeringObjectMetadata.Empty, "AL-7075"))
            .CreateAsync("Bracket — for test purposes.");

        var sweep = new AttachmentContentReconciliationService(persistence, stateStore, contentStore, writeIntentStore);

        // A healthy store reports nothing skipped — so the assertion below
        // cannot pass because the sweep reports everything.
        var healthy = await sweep.DetectAsync();
        Assert.Empty(healthy.SkippedByMarker);
        Assert.Empty(healthy.Orphans);

        stateStore.FailNextSave = true;
        await Assert.ThrowsAsync<IOException>(() => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        var report = await sweep.SweepAsync();

        var leaked = Assert.Single(report.SkippedByMarker);
        Assert.Empty(report.Orphans);

        // The bytes are still there — declining to collect them is the
        // deliberate, conservative half of this outcome, and reporting
        // them is the half `WP 16.4B-R6` added.
        var stillThere = await contentStore.ReadAsync(leaked, expectedHash: null, expectedSizeInBytes: Bytes.Length);
        Assert.True(stillThere.IsAvailable);

        // And the instance still claims the attachment the caller was told
        // had failed, which is the other, independent way an operator can
        // see this residue.
        Assert.Contains(await part.GetAttachmentsAsync(), a => a.Id == leaked);
    }

    private sealed class FailableObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly IEngineeringObjectStateStore _inner;

        public FailableObjectStateStore(IEngineeringObjectStateStore inner) => _inner = inner;

        public bool FailNextSave { get; set; }

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("Simulated object state store failure.");
            }

            return _inner.SaveAsync(state, cancellationToken);
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.FindAsync(id, cancellationToken);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(id, cancellationToken);

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default) =>
            _inner.ListAsync(cancellationToken);
    }
}

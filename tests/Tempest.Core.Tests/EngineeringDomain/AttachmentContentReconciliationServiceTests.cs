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

    private sealed record Fixture(
        EngineeringDomainContext Context, PersistenceStore Persistence, AttachmentContentStore ContentStore,
        EngineeringObjectStateStore StateStore, AttachmentWriteIntentStore WriteIntentStore);

    /// <summary>
    /// A <see cref="GatedObjectStateStore"/>-wrapped variant of
    /// <see cref="Build"/>, for tests that need to pause deterministically
    /// between an attachment's content write landing and its state write
    /// landing (`WP 16.4B-R2`'s own race). The reconciliation service
    /// reads through the real, ungated <paramref name="fixture"/> — only
    /// <see cref="EngineeringObjectBase.AttachContentAsync"/>'s own state
    /// write, reached through <see cref="EngineeringDomainContext"/>, is
    /// gated.
    /// </summary>
    private static (Fixture Fixture, GatedObjectStateStore Gate) BuildGated(Fixture fixture)
    {
        var gate = new GatedObjectStateStore(fixture.StateStore);
        var gatedContext = new EngineeringDomainContext(
            fixture.Context.Store, fixture.Context.Repository, fixture.Context.RelationshipRepository,
            fixture.Context.LifecycleTable, fixture.Context.ValidationRuleSet, fixture.Context.EvidenceComposer,
            fixture.Context.CurrentPrincipalAccessor, gate, fixture.ContentStore, fixture.WriteIntentStore);

        return (fixture with { Context = gatedContext }, gate);
    }

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
        var writeIntentStore = new AttachmentWriteIntentStore(persistence);

        var context = new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore, contentStore, writeIntentStore);

        return new Fixture(context, persistence, contentStore, stateStore, writeIntentStore);
    }

    /// <summary>
    /// Wraps a real <see cref="IEngineeringObjectStateStore"/> so a test
    /// can pause deterministically — no timing, no <see cref="Task.Delay(int)"/> —
    /// at the exact instant a chosen <c>SaveAsync</c> call lands: after
    /// the content write it follows has already landed (and, with a
    /// marker store configured, after the marker for it has already
    /// landed too) but before the state write that would clear the race
    /// window completes.
    /// </summary>
    /// <remarks>
    /// Disarmed by default — every <see cref="SaveAsync"/> call passes
    /// straight through, including the ones <see cref="CreatePartAsync"/>
    /// itself makes to persist a freshly-created object's initial state.
    /// A test calls <see cref="ArmNextSave"/> immediately before the one
    /// call it actually wants to pause (the attachment's own state write),
    /// so an earlier, unrelated <c>SaveAsync</c> — for the same object,
    /// through the same gate — is never itself paused waiting for a
    /// release nobody has reached yet.
    /// </remarks>
    private sealed class GatedObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly IEngineeringObjectStateStore _inner;
        private TaskCompletionSource? _reachedSave;
        private TaskCompletionSource? _releaseSave;

        public GatedObjectStateStore(IEngineeringObjectStateStore inner) => _inner = inner;

        /// <summary>
        /// Arms the gate so the very next <see cref="SaveAsync"/> call
        /// pauses until <see cref="ReleaseSave"/> is called. Returns the
        /// task that completes once that call has reached the pause.
        /// </summary>
        public Task ArmNextSave()
        {
            _reachedSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _reachedSave.Task;
        }

        /// <summary>Lets the armed, now-paused <see cref="SaveAsync"/> call proceed to the real, underlying write.</summary>
        public void ReleaseSave() => _releaseSave?.TrySetResult();

        /// <summary>
        /// When set, an armed <see cref="SaveAsync"/> call performs the
        /// real write and then throws — simulating a crash after the
        /// state write has durably landed but before <c>AttachContentAsync</c>
        /// can reach its own marker-clearing step.
        /// </summary>
        public bool ThrowAfterSave { get; set; }

        public async Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            // Only _reachedSave is swapped out here (disarming the gate for
            // any later, unrelated SaveAsync call). _releaseSave is
            // deliberately left in place: ReleaseSave() reads it from the
            // field, and swapping it out here too would hand this method a
            // local reference to the exact same TaskCompletionSource while
            // leaving the field null — ReleaseSave()'s own null-conditional
            // would then silently no-op against a TCS nothing is awaiting
            // any more, and the await below would never complete. This was
            // caught by the race test itself hanging, not reasoned out in
            // advance.
            var reached = Interlocked.Exchange(ref _reachedSave, null);

            if (reached is not null)
            {
                reached.TrySetResult();
                await _releaseSave!.Task.ConfigureAwait(false);
            }

            await _inner.SaveAsync(state, cancellationToken).ConfigureAwait(false);

            if (ThrowAfterSave)
                throw new InvalidOperationException("Simulated crash: the state write landed, but the caller never got to clear its marker.");
        }

        public Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            _inner.FindAsync(objectId, cancellationToken);

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default) =>
            _inner.ListAsync(cancellationToken);

        public Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(objectId, cancellationToken);
    }

    /// <summary>
    /// Pauses the <em>second</em> of two calls it is told about, regardless
    /// of which of the two arrives first — the seam
    /// <see cref="SweepAsync_InterleavedBetweenItsMarkerReadAndItsStateRead_NeverCollectsTheLiveContent"/>
    /// uses to hold the sweep between its marker read and its state read
    /// without hard-coding which of the two <see cref="AttachmentContentReconciliationService"/>
    /// actually issues first (a deliberate reordering fix, itself under
    /// test here, changed that).
    /// </summary>
    private sealed class SecondReadPausingGate
    {
        private int _callCount;
        private readonly TaskCompletionSource _reachedSecondRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondRead = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes once the second of the two gated calls has arrived and is paused.</summary>
        public Task ReachedSecondRead => _reachedSecondRead.Task;

        /// <summary>Lets the paused second call proceed.</summary>
        public void ReleaseSecondRead() => _releaseSecondRead.TrySetResult();

        /// <summary>Call before performing the real read. The first caller passes straight through; the second pauses until released.</summary>
        public async Task BeforeReadAsync()
        {
            if (Interlocked.Increment(ref _callCount) == 2)
            {
                _reachedSecondRead.TrySetResult();
                await _releaseSecondRead.Task.ConfigureAwait(false);
            }
        }
    }

    /// <summary>Wraps a real <see cref="IEngineeringObjectStateStore"/> so its <see cref="ListAsync"/> — the sweep's own read — reports to a shared <see cref="SecondReadPausingGate"/> before returning.</summary>
    private sealed class ReadGatedStateStore : IEngineeringObjectStateStore
    {
        private readonly IEngineeringObjectStateStore _inner;
        private readonly SecondReadPausingGate _gate;

        public ReadGatedStateStore(IEngineeringObjectStateStore inner, SecondReadPausingGate gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public async Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
        {
            await _gate.BeforeReadAsync().ConfigureAwait(false);
            return await _inner.ListAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default) =>
            _inner.SaveAsync(state, cancellationToken);

        public Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            _inner.FindAsync(objectId, cancellationToken);

        public Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(objectId, cancellationToken);
    }

    /// <summary>Wraps a real <see cref="IAttachmentWriteIntentStore"/> so its <see cref="ListMarkedAsync"/> — the sweep's own read — reports to a shared <see cref="SecondReadPausingGate"/> before returning.</summary>
    private sealed class ReadGatedWriteIntentStore : IAttachmentWriteIntentStore
    {
        private readonly IAttachmentWriteIntentStore _inner;
        private readonly SecondReadPausingGate _gate;

        public ReadGatedWriteIntentStore(IAttachmentWriteIntentStore inner, SecondReadPausingGate gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public async Task<IReadOnlySet<Guid>> ListMarkedAsync(CancellationToken cancellationToken = default)
        {
            await _gate.BeforeReadAsync().ConfigureAwait(false);
            return await _inner.ListMarkedAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
            _inner.MarkAsync(attachmentId, cancellationToken);

        public Task ClearAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
            _inner.ClearAsync(attachmentId, cancellationToken);
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

    // ---- The race itself (`WP 16.4B-R2`) ----

    /// <summary>
    /// The test whose absence let the bug ship: interleaves a sweep
    /// deterministically between an attachment's content write landing
    /// and its state write landing — <c>ADR-0114</c> Decision 4's own
    /// window — and proves the content survives. No timing, no
    /// <see cref="Task.Delay(int)"/>: the interleaving is enforced by
    /// <see cref="GatedObjectStateStore"/> pausing exactly at the state
    /// write, released only once the sweep run inside that pause has
    /// returned.
    /// </summary>
    [Fact]
    public async Task SweepAsync_InterleavedBetweenContentWriteAndStateWrite_NeverCollectsTheLiveContent()
    {
        var (fixture, gate) = BuildGated(Build());
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");

        // Arm the gate immediately before this specific call, so the
        // object-creation state write CreatePartAsync already made above
        // was never itself paused waiting for a release nobody had
        // reached yet.
        var reachedSave = gate.ArmNextSave();
        var attachTask = part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

        // Wait for AttachContentAsync to reach its state write: by this
        // point the content bytes and the write-intent marker are both
        // already durable, but nothing yet references the attachment.
        await reachedSave;

        // A sweep run entirely inside that window, against the real,
        // ungated stores — this is the exact race the finding described.
        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore, fixture.WriteIntentStore);
        var report = await sweep.SweepAsync();

        // Let the state write (and AttachContentAsync's own marker clear)
        // proceed and complete.
        gate.ReleaseSave();
        var attachment = await attachTask;

        // The marker, not the reorder, is what must have prevented this:
        // the content key was visible and genuinely unreferenced by any
        // state at the moment this sweep ran, so only the live marker
        // explains survival.
        Assert.Empty(report.Orphans);

        var result = await fixture.ContentStore.ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);

        var attachments = await part.GetAttachmentsAsync();
        Assert.Contains(attachments, a => a.Id == attachment.Id);
    }

    /// <summary>
    /// The interleaving the review board's second pass caught: sampling
    /// the marker <em>after</em> the state read (this sweep's original
    /// shape, before the fix below) reopens the exact same race one read
    /// later — <c>content &lt; T1 &lt; T2(state) &lt; state-write &lt;
    /// Clear &lt; T3(marker)</c> looks "present, unreferenced, unmarked"
    /// and gets collected even though it is now fully live. This test
    /// pauses the sweep between whichever of its state read and marker
    /// read runs first and the one that runs second — via
    /// <see cref="SecondReadPausingGate"/>, which does not care which
    /// order the production code actually uses — and, inside that gap,
    /// lets the attachment's state write <em>and</em> its marker clear
    /// both complete. Against the fixed read order (content, marker,
    /// state), the marker read is the one that runs first and it still
    /// finds the marker in place, so the attachment is excluded before
    /// the state read even matters. I confirmed the converse directly:
    /// with the marker read moved back to last (this sweep's original
    /// order), this exact test fails — the content is collected and
    /// <see cref="AttachmentContentStatus.Missing"/> comes back for a
    /// fully live attachment — restored immediately afterwards.
    /// </summary>
    [Fact]
    public async Task SweepAsync_InterleavedBetweenItsMarkerReadAndItsStateRead_NeverCollectsTheLiveContent()
    {
        var (fixture, saveGate) = BuildGated(Build());
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");

        // Content and the marker are both durable; the state write is
        // paused before it lands (the same seam the previous test uses).
        var reachedSave = saveGate.ArmNextSave();
        var attachTask = part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });
        await reachedSave;

        // The sweep's own two reads of interest — state and markers —
        // both route through this shared gate; whichever the production
        // code issues first passes straight through, the second pauses.
        var readGate = new SecondReadPausingGate();
        var sweepStateStore = new ReadGatedStateStore(fixture.StateStore, readGate);
        var sweepWriteIntentStore = new ReadGatedWriteIntentStore(fixture.WriteIntentStore, readGate);
        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, sweepStateStore, fixture.ContentStore, sweepWriteIntentStore);

        var sweepTask = sweep.SweepAsync();
        await readGate.ReachedSecondRead;

        // Inside the gap between the sweep's first and second read: let
        // the attachment's state write land, and let AttachContentAsync
        // run all the way to its own marker clear.
        saveGate.ReleaseSave();
        var attachment = await attachTask;

        readGate.ReleaseSecondRead();
        var report = await sweepTask;

        Assert.Empty(report.Orphans);
        var result = await fixture.ContentStore.ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);
    }

    /// <summary>A stale marker (a crash between the state write landing and the marker's own removal) leaves content uncollected, never errors.</summary>
    [Fact]
    public async Task SweepAsync_AStaleMarker_LeavesContentUncollectedRatherThanErroring()
    {
        var (fixture, gate) = BuildGated(Build());
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");

        // Not paused — ThrowAfterSave fires after the real, underlying
        // write completes normally, regardless of arming. The state write
        // lands durably, but the simulated crash means AttachContentAsync
        // never reaches its own marker-clearing step.
        gate.ThrowAfterSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 }));

        var marked = await fixture.WriteIntentStore.ListMarkedAsync();
        var attachmentId = Assert.Single(marked);

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore, fixture.WriteIntentStore);
        var report = await sweep.SweepAsync();

        // Not collected (the marker is doing its job)...
        Assert.Empty(report.Orphans);
        var result = await fixture.ContentStore.ReadAsync(attachmentId, expectedHash: null, expectedSizeInBytes: 4);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);

        // ...and, since the state write itself actually landed, this is
        // in fact a live, referenced attachment, not merely a protected
        // orphan — the marker cost nothing real here.
        var attachments = await part.GetAttachmentsAsync();
        Assert.Contains(attachments, a => a.Id == attachmentId);
    }

    /// <summary>The marker is removed on the success path, so an ordinary, unrelated orphan is still collectable afterwards.</summary>
    [Fact]
    public async Task SweepAsync_AfterAnOrdinaryAttach_TheMarkerIsClearedAndUnrelatedOrphansStillCollect()
    {
        var fixture = Build();
        var part = await CreatePartAsync(fixture.Context, "PART-1", "Bracket");
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

        Assert.Empty(await fixture.WriteIntentStore.ListMarkedAsync());

        var orphanId = Guid.NewGuid();
        await fixture.ContentStore.SaveAsync(orphanId, new byte[] { 9, 9, 9 });

        var sweep = new AttachmentContentReconciliationService(fixture.Persistence, fixture.StateStore, fixture.ContentStore, fixture.WriteIntentStore);
        var report = await sweep.SweepAsync();

        var orphan = Assert.Single(report.Orphans);
        Assert.Equal(orphanId, orphan.AttachmentId);
        Assert.True(orphan.Collected);

        var liveResult = await fixture.ContentStore.ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes);
        Assert.Equal(AttachmentContentStatus.Available, liveResult.Status);
    }

    [Fact]
    public void Constructor_NullPersistenceStore_Throws()
    {
        var stateStore = new EngineeringObjectStateStore(new PersistenceStore(new ConfigurationBuilder().Build()));
        var contentStore = new AttachmentContentStore(new PersistenceStore(new ConfigurationBuilder().Build()));

        Assert.Throws<ArgumentNullException>(() => new AttachmentContentReconciliationService(null!, stateStore, contentStore));
    }
}

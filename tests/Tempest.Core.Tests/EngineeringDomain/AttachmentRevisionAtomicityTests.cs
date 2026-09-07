using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R6` — attaching content is atomic with respect to a revision,
/// and <b>no compensation path deletes attachment content while any live or
/// durable object state can legitimately reference it</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The regression these close.</b> `WP 16.4B-R5` compensated a refused
/// state write by deleting the content bytes it had already written,
/// arguing that the supersession guard throws strictly before
/// <c>store.SaveAsync</c>, so "nothing was written". That is true of the
/// failing call's own save and false of the durable record, which the
/// <c>ReviseAsync</c> successor also owns: the in-memory add happened
/// <em>before</em> the per-object write lock, so a revision that took the
/// lock in between captured the pending attachment into the successor —
/// and the compensation then deleted the bytes of an attachment the live
/// successor holds. Three reviewers reproduced it independently against
/// the real classes. It is strictly worse than the bounded, disclosed leak
/// it was written to close.
/// </para>
/// <para>
/// <b>Why the fix is a lock and not a wider — or narrower — catch.</b>
/// <c>AttachContentAsync</c> now takes this object's write lock before its
/// first durable step and holds it to the end, so a revision cannot
/// interleave at all. The two failure classes the review board named stop
/// being a taxonomy the compensation has to get right:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Nothing durable was written.</b> The refusal happens before the
/// marker, so there is nothing to roll back — proved by
/// <see cref="AnAttachRefusedByASupersededInstance_WritesNothingAtAll"/>,
/// which asserts the stores were never called rather than that their
/// contents ended up empty.
/// </description></item>
/// <item><description>
/// <b>A successor legitimately inherited the attachment.</b> Only
/// reachable once the state write has committed, at which point the method
/// has already succeeded and there is no compensation left to run —
/// <see cref="AConcurrentRevisionCapturingAnInFlightAttach_NeverInheritsAnAttachmentWhoseBytesAreGone"/>
/// (the board's own interleaving) and
/// <see cref="AnAttachmentASuccessorLegitimatelyInherits_KeepsItsContent"/>.
/// </description></item>
/// </list>
/// </remarks>
public sealed class AttachmentRevisionAtomicityTests
{
    private static readonly byte[] Bytes = [1, 2, 3];

    // ================================================================
    // RED — the board's interleaving
    // ================================================================

    /// <summary>
    /// The reproduction all three reviewers built, made an assertion, and
    /// deterministic: the revision is parked <em>inside</em> its own
    /// capture — holding the write lock, before it reads the attachment
    /// list — and the attach is started while it is parked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No timing is relied on. <see cref="GatedPart.ArmNextCapture"/>
    /// signals once the revision is provably parked; every store here
    /// completes synchronously, so <c>AttachContentAsync</c> runs on the
    /// calling thread until its first genuinely incomplete await and the
    /// returned task is therefore already at that point when the next line
    /// executes. Before the fix that point was the write lock, with the
    /// marker set, the bytes written and the attachment already added to
    /// the list the parked revision is about to copy. After it, that point
    /// is the write lock with nothing written at all.
    /// </para>
    /// <para>
    /// The assertion is the invariant, not the mechanism: whatever the
    /// attach's outcome, every attachment the live successor claims — in
    /// memory, and durably after one ordinary later mutation — must still
    /// have its content.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AConcurrentRevisionCapturingAnInFlightAttach_NeverInheritsAnAttachmentWhoseBytesAreGone()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        var parked = part.ArmNextCapture();
        var revising = Task.Run(() => part.ReviseAsync("Revised content.", "Rev B."));
        await parked;

        // The revision now holds the write lock and has not yet read
        // `_attachments`. Started on this thread deliberately: the call
        // runs synchronously as far as it can get before suspending.
        var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        part.ReleaseCapture();

        var successor = (GatedPart)await revising;
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => attaching);

        // The invariant, in memory...
        foreach (var attachment in await successor.GetAttachmentsAsync())
            Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);

        // ...and durably, once an ordinary mutation on the live successor
        // has written its snapshot. This is the step that made the dangling
        // reference permanent.
        await successor.RenameAsync("Renamed Bracket");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);

        foreach (var attachment in state.Attachments)
            Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);

        // And nothing was left half-done behind the refusal.
        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());
        Assert.Empty(await part.GetAttachmentsAsync());
    }

    /// <summary>
    /// The other half of the same invariant: an attachment a successor
    /// inherits <em>legitimately</em> — because the attach committed before
    /// the revision — must keep its content, for ever. Deleting it is the
    /// data loss; refusing to delete it is the whole point.
    /// </summary>
    /// <remarks>
    /// A regression pin: it passed before this change too. It is here
    /// because the cheapest wrong fix — deleting the bytes whenever
    /// <c>_supersededBy</c> is set — would break it, and nothing else in
    /// the suite would notice.
    /// </remarks>
    [Fact]
    public async Task AnAttachmentASuccessorLegitimatelyInherits_KeepsItsContent()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        var successor = (GatedPart)await part.ReviseAsync("Revised content.", "Rev B.");

        Assert.Contains(await successor.GetAttachmentsAsync(), a => a.Id == attachment.Id);
        Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);

        await successor.RenameAsync("Renamed Bracket");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);
        Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);
        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
    }

    /// <summary>
    /// Failure class (a), stated as an ordering rather than as an end
    /// state: a superseded instance's attach is refused <b>before</b> the
    /// marker and the content are written, so there is nothing to roll
    /// back. The existing `WP 16.4B-R5` facts assert the stores end up
    /// empty, which a written-then-compensated sequence also satisfies;
    /// this asserts they were never called.
    /// </summary>
    [Fact]
    public async Task AnAttachRefusedByASupersededInstance_WritesNothingAtAll()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        _ = await part.ReviseAsync("Revised content.", "Rev B.");

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        Assert.Equal(0, rig.WriteIntentStore.MarkCallCount);
        Assert.Equal(0, rig.ContentStore.SaveCallCount);
        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
        Assert.Empty(await part.GetAttachmentsAsync());
    }

    // ================================================================
    // AMBER — the three the board raised alongside it
    // ================================================================

    /// <summary>
    /// Board finding `P2-2`. Cancelling while the attach waits for the
    /// write lock used to leave `TD-139`'s stranded marker — with no
    /// revision and no crash anywhere — because the marker and the content
    /// were both written before the wait. The marker is now set inside the
    /// lock, so a cancelled wait throws before anything durable exists.
    /// </summary>
    [Fact]
    public async Task CancellingWhileWaitingForTheWriteLock_StrandsNoMarkerAndWritesNoContent()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        using (await rig.Context.AcquireObjectWriteLockAsync(part.Id))
        {
            using var cancellation = new CancellationTokenSource();

            var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes, cancellation.Token);
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attaching);
        }

        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());
        Assert.Empty(rig.ContentStore.StoredKeys);
        Assert.Empty(await part.GetAttachmentsAsync());
    }

    /// <summary>
    /// Board finding `P2-3`. The success path passed the caller's token to
    /// the marker clear while the compensation correctly passed
    /// <see cref="CancellationToken.None"/> — so a cancellation arriving
    /// after the state write had already landed stranded a marker on a
    /// successfully attached, live, referenced attachment. Both now pass
    /// <see cref="CancellationToken.None"/>: cancellation is honoured
    /// everywhere it can still prevent work and nowhere it could only leave
    /// one half-done.
    /// </summary>
    [Fact]
    public async Task CancellationArrivingAfterTheStateWriteLands_DoesNotStrandTheMarker()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        using var cancellation = new CancellationTokenSource();

        // Armed only now, so the factory's own initial persist is not the
        // one that trips it.
        rig.StateStore.AfterSave = () => cancellation.Cancel();

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes, cancellation.Token);

        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());
        Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);
        Assert.Contains(await part.GetAttachmentsAsync(), a => a.Id == attachment.Id);
    }

    /// <summary>
    /// Board finding `P3-6`. `WP 16.4B-R5` gave <c>AttachContentAsync</c> a
    /// compensation and left the metadata-only entry point alone, so a
    /// refused <see cref="IHasAttachments.AttachAsync"/> left the instance
    /// permanently claiming an attachment the platform had told the caller
    /// it did not accept. The supersession check now precedes the add.
    /// </summary>
    [Fact]
    public async Task ARefusedAttachAsync_LeavesTheInstanceClaimingNothing()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        var successor = (GatedPart)await part.ReviseAsync("Revised content.", "Rev B.");
        var refused = new Attachment("drawing.pdf", "application/pdf", 3);

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.AttachAsync(refused));

        Assert.Empty(await part.GetAttachmentsAsync());
        Assert.Empty(await successor.GetAttachmentsAsync());

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.DoesNotContain(state.Attachments, a => a.Id == refused.Id);
    }

    /// <summary>
    /// A content write that fails leaves no marker behind. The marker was
    /// protecting an id nothing in memory or on disk names, so withdrawing
    /// it cannot expose a referenced attachment — and leaving it set would
    /// make whatever the failed write left permanently uncollectable, which
    /// is the leak the marker protocol is meant to bound, not create.
    /// </summary>
    [Fact]
    public async Task AFailedContentWrite_StrandsNoMarker()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        rig.ContentStore.FailNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());
        Assert.Empty(await part.GetAttachmentsAsync());
    }

    // ================================================================
    // Rig
    // ================================================================

    private sealed class Rig
    {
        public Rig()
        {
            var principalAccessor = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
            var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

            Context = new EngineeringDomainContext(
                new InMemoryEngineeringDocumentStore(principalAccessor), repository, relationshipRepository,
                new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(relationshipDiscovery, repository), principalAccessor,
                StateStore, ContentStore, WriteIntentStore);
        }

        public RecordingObjectStateStore StateStore { get; } = new();
        public RecordingAttachmentContentStore ContentStore { get; } = new();
        public RecordingWriteIntentStore WriteIntentStore { get; } = new();
        public EngineeringDomainContext Context { get; }

        public async Task<GatedPart> CreateGatedPartAsync()
        {
            var factory = new EngineeringObjectFactory<GatedPart>(
                GatedPart.KindName, Context,
                (doc, rev) => new GatedPart(doc, rev, Context, "PART-1", "Bracket", EngineeringObjectMetadata.Empty));

            return (GatedPart)await factory.CreateAsync("Bracket — for test purposes.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// An ordinary Engineering Object with one seam: its
    /// <c>CaptureTypeState</c> can be parked on demand.
    /// </summary>
    /// <remarks>
    /// <c>CaptureTypeState</c> is invoked by <c>CaptureState</c>, which
    /// <c>ReviseAsync</c> calls while holding the per-object write lock —
    /// so parking there parks the revision at exactly the instant the
    /// review board's reproduction needs, with no timing and no sleep. It
    /// is a seam, not a defect: the same extension point every concrete
    /// Kind in the platform already overrides.
    /// </remarks>
    private sealed class GatedPart : EngineeringObjectBase, IRehydratable<GatedPart>
    {
        public const string KindName = "GatedPart";

        private TaskCompletionSource? _parked;
        private TaskCompletionSource? _release;

        public GatedPart(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
            string? identifier, string displayName, EngineeringObjectMetadata metadata)
            : base(document, currentRevision, context, identifier, displayName, metadata)
        {
        }

        /// <summary>Arms the next capture to park. Returns a task that completes once it has.</summary>
        public Task ArmNextCapture()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _parked.Task;
        }

        /// <summary>Lets the parked capture finish.</summary>
        public void ReleaseCapture() => _release?.TrySetResult();

        protected override void CaptureTypeState(IDictionary<string, string?> state)
        {
            // Disarm first, so the successor's own captures are never gated.
            var parked = Interlocked.Exchange(ref _parked, null);

            if (parked is not null)
            {
                parked.TrySetResult();
                _release!.Task.GetAwaiter().GetResult();
            }
        }

        static GatedPart IRehydratable<GatedPart>.Rehydrate(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
            new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
    }

    private sealed class RecordingObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        /// <summary>Runs after a successful save — the seam `P2-3` needs to cancel at exactly the right instant.</summary>
        public Action? AfterSave { get; set; }

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            lock (_states) { _states[state.Id] = state; }
            AfterSave?.Invoke();
            return Task.CompletedTask;
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_states) { return Task.FromResult(_states.TryGetValue(id, out var state) ? state : null); }
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_states) { _states.Remove(id); }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
        {
            lock (_states) { return Task.FromResult<IReadOnlyList<EngineeringObjectState>>(_states.Values.ToList()); }
        }
    }

    private sealed class RecordingAttachmentContentStore : IAttachmentContentStore
    {
        private readonly Dictionary<Guid, byte[]> _content = new();
        private int _saveCallCount;
        private int _deleteCallCount;

        public bool FailNextSave { get; set; }

        public int SaveCallCount => Volatile.Read(ref _saveCallCount);
        public int DeleteCallCount => Volatile.Read(ref _deleteCallCount);

        public IReadOnlyCollection<Guid> StoredKeys
        {
            get { lock (_content) { return _content.Keys.ToList(); } }
        }

        public Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _saveCallCount);

            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("Simulated content-store failure.");
            }

            lock (_content) { _content[attachmentId] = content.ToArray(); }
            return Task.FromResult(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content.Span)));
        }

        public Task<AttachmentContentResult> ReadAsync(Guid attachmentId, string? expectedHash, long expectedSizeInBytes, CancellationToken cancellationToken = default)
        {
            lock (_content)
            {
                return Task.FromResult(_content.TryGetValue(attachmentId, out var bytes)
                    ? AttachmentContentResult.Available(bytes)
                    : AttachmentContentResult.Missing());
            }
        }

        public Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _deleteCallCount);
            lock (_content) { _content.Remove(attachmentId); }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            lock (_content) { return Task.FromResult<IReadOnlyList<Guid>>(_content.Keys.ToList()); }
        }
    }

    /// <summary>
    /// Honours the cancellation token it is handed, which the production
    /// <c>AttachmentWriteIntentStore</c> does too (it forwards to
    /// <c>IPersistenceStore</c>) — that is what makes `P2-3` observable
    /// here rather than only by reading the code.
    /// </summary>
    private sealed class RecordingWriteIntentStore : IAttachmentWriteIntentStore
    {
        private readonly HashSet<Guid> _marked = new();
        private int _markCallCount;

        public int MarkCallCount => Volatile.Read(ref _markCallCount);

        public Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _markCallCount);
            lock (_marked) { _marked.Add(attachmentId); }
            return Task.CompletedTask;
        }

        public Task ClearAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_marked) { _marked.Remove(attachmentId); }
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<Guid>> ListMarkedAsync(CancellationToken cancellationToken = default)
        {
            lock (_marked) { return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>(_marked)); }
        }
    }
}

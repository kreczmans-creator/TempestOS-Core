using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Attaching content is atomic with respect to a revision, and no path in
/// the platform can leave a live or durable object state referencing
/// attachment bytes that are not there (`ADR-0145`, `WP 17.1B`).
/// </summary>
/// <remarks>
/// <para>
/// <b>The regression these were written for, and what closed it.</b>
/// `WP 16.4B-R5` compensated a refused state write by deleting content
/// bytes it had already written. The fifth review board proved that
/// destructive: the in-memory add happened before the per-object write
/// lock, so a revision that took the lock in between captured the pending
/// attachment into the successor, and the compensation then deleted the
/// bytes of an attachment the live successor held. `WP 16.4B-R6` answered
/// it by widening the lock; the facts below were written against that
/// answer, and asserted the ordering it produced — the write-intent
/// marker, the four durable steps, "the stores were never called".
/// </para>
/// <para>
/// <b>`ADR-0145` answers it differently, and the difference is what these
/// facts now assert.</b> There is one domain-wide write lock and one
/// transaction. An attach computes its next state inside that
/// transaction, writes the object-state record and the attachment BLOB
/// through it, and touches this instance's own <c>_attachments</c> only
/// after it commits. So:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>A refusal is not an operation.</b> A superseded instance is refused
/// inside the transaction, before anything is staged, and the transaction
/// rolls back — asserted on the store's own commit and rollback counters
/// rather than on a hand-written double's call counts, in
/// <see cref="AnAttachRefusedByASupersededInstance_WritesNothingAtAll"/>
/// and <see cref="ARefusedAttachAsync_LeavesTheInstanceClaimingNothing"/>.
/// </description></item>
/// <item><description>
/// <b>A revision cannot capture an in-flight attach at all.</b> The two
/// cannot interleave, because one lock and one transaction serialise
/// them, and the attach adds nothing to the instance until it has
/// committed — <see cref="AConcurrentRevisionCapturingAnInFlightAttach_NeverInheritsAnAttachmentWhoseBytesAreGone"/>.
/// </description></item>
/// <item><description>
/// <b>An attachment a successor legitimately inherits keeps its
/// content</b>, for ever — <see cref="AnAttachmentASuccessorLegitimatelyInherits_KeepsItsContent"/>.
/// Nothing deletes attachment bytes except a committed delete of the
/// object that owns them.
/// </description></item>
/// </list>
/// <para>
/// The rig is the shipped write path: the real
/// <see cref="EngineeringDocumentStore"/>,
/// <see cref="EngineeringObjectStateStore"/> and
/// <see cref="AttachmentContentStore"/> over one
/// <see cref="InMemoryQueryablePersistenceStore"/>, with faults injected
/// at the store — where a real fault occurs — rather than at a
/// hand-written stand-in for one of four writers. Nothing sleeps, polls
/// or races; every interleaving is forced with a
/// <see cref="TaskCompletionSource"/> gate.
/// </para>
/// </remarks>
public sealed class AttachmentRevisionAtomicityTests
{
    private static readonly byte[] Bytes = [1, 2, 3];

    // ================================================================
    // The board's interleaving, against the transaction
    // ================================================================

    /// <summary>
    /// The reproduction all three reviewers built, re-pointed at the
    /// transactional write path: the revision is parked <em>inside its own
    /// transaction</em> — holding the one domain write lock, before it
    /// reads the attachment list — and the attach is started while it is
    /// parked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The mid-flight assertions are the point, and they are the
    /// inversion of the board's finding.</b> While the revision is parked,
    /// the attach has not added anything to <c>_attachments</c> and has
    /// written nothing — it is blocked on the domain write lock, which
    /// <see cref="EngineeringDomainContext.ExecuteWriteAsync"/> takes
    /// before it opens the transaction. There is therefore no pending
    /// attachment for the parked capture to copy, which is exactly the
    /// state the board's reproduction depended on. Deterministic, not
    /// timed: every store here completes synchronously, so the attach runs
    /// on the calling thread as far as that lock and its returned task is
    /// provably incomplete on the next line.
    /// </para>
    /// <para>
    /// The outcome of the attach is deliberately not asserted. Whichever
    /// of the two takes the lock second may find the predecessor retired
    /// or may not; what must hold either way is the invariant that was
    /// always the real claim: <em>every attachment the live successor
    /// claims — in memory, and durably after one ordinary later mutation —
    /// still has its content.</em>
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

        // The revision holds the one domain write lock and is inside its
        // transaction. Started on this thread deliberately: the call runs
        // synchronously as far as that lock, and no further.
        var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        Assert.False(attaching.IsCompleted, "The attach entered the write path while a revision held the domain write lock.");
        Assert.Empty(await part.GetAttachmentsAsync());
        Assert.Empty(rig.ContentKeys);

        part.ReleaseCapture();

        var successor = (GatedPart)await revising;
        _ = await Outcome(attaching);

        // The invariant, in memory...
        foreach (var attachment in await successor.GetAttachmentsAsync())
            Assert.True(
                rig.HasContent(attachment.Id),
                $"The live successor claims attachment '{attachment.Id}' ('{attachment.FileName}') but its content is gone.");

        // ...and durably, once an ordinary mutation on the live successor
        // has written its snapshot. This is the step that made the dangling
        // reference permanent.
        await successor.RenameAsync("Renamed Bracket");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);

        foreach (var attachment in state.Attachments)
            Assert.True(rig.HasContent(attachment.Id), $"The durable record names attachment '{attachment.Id}' but its content is gone.");
    }

    /// <summary>
    /// The other half of the same invariant: an attachment a successor
    /// inherits <em>legitimately</em> — because the attach committed before
    /// the revision — must keep its content, for ever. Deleting it is the
    /// data loss; refusing to delete it is the whole point.
    /// </summary>
    /// <remarks>
    /// A regression pin: it passed before `ADR-0145` too. It is here
    /// because the cheapest wrong fix — deleting the bytes whenever
    /// <c>_supersededBy</c> is set — would break it, and nothing else in
    /// the suite would notice. The content is checked by reading it back
    /// through the successor, not by counting calls to a double's
    /// <c>DeleteAsync</c>: the assertion is that the bytes are there and
    /// are the right bytes, which no accounting of calls can establish.
    /// </remarks>
    [Fact]
    public async Task AnAttachmentASuccessorLegitimatelyInherits_KeepsItsContent()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        var successor = (GatedPart)await part.ReviseAsync("Revised content.", "Rev B.");

        Assert.Contains(await successor.GetAttachmentsAsync(), a => a.Id == attachment.Id);

        var inherited = await successor.ReadAttachmentContentAsync(attachment.Id);
        Assert.True(inherited.IsAvailable);
        Assert.Equal(Bytes, inherited.Bytes);

        await successor.RenameAsync("Renamed Bracket");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);

        var afterRename = await successor.ReadAttachmentContentAsync(attachment.Id);
        Assert.True(afterRename.IsAvailable);
        Assert.Equal(Bytes, afterRename.Bytes);
    }

    /// <summary>
    /// A superseded instance's attach is refused <b>inside the
    /// transaction</b>, before anything is staged: no transaction commits,
    /// no bytes exist, and the instance claims nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The predecessor of this fact asserted that the content store and
    /// the write-intent store "were never called", because a
    /// written-then-compensated sequence also ends with both empty and the
    /// two had to be distinguishable. There is no compensation to
    /// distinguish it from now, and the stronger statement is available
    /// directly from the one store: the refusal throws out of the
    /// transaction body, so the store records a rollback and no commit,
    /// and its committed contents are untouched.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AnAttachRefusedByASupersededInstance_WritesNothingAtAll()
    {
        var rig = new Rig();
        var part = await rig.CreateGatedPartAsync();

        _ = await part.ReviseAsync("Revised content.", "Rev B.");

        var commitsBefore = rig.Store.CommitCount;
        var rollbacksBefore = rig.Store.RollbackCount;

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        Assert.Equal(commitsBefore, rig.Store.CommitCount);
        Assert.Equal(rollbacksBefore + 1, rig.Store.RollbackCount);
        Assert.Empty(rig.ContentKeys);
        Assert.Empty(await part.GetAttachmentsAsync());
    }

    // ================================================================
    // The write path's own liabilities, re-pointed
    // ================================================================

    /// <summary>
    /// Board finding `P2-2`, re-expressed against the domain-wide lock:
    /// cancelling while an attach waits for the write lock writes nothing
    /// at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original left `TD-139`'s stranded write-intent marker behind,
    /// because the marker and the content were both written before the
    /// wait. Both the marker and the ordering are deleted; what survives
    /// is the invariant underneath, and it is now unconditional rather
    /// than a consequence of where the marker was set — a cancelled wait
    /// never reaches the transaction, so there is nothing for it to leave.
    /// </para>
    /// <para>
    /// The lock is held by a second writer parked inside
    /// <see cref="GatedPersistenceStore"/>, at the end of its transaction
    /// body: all of its writes staged, both the domain lock and the
    /// store's writer lock held. The old fact took the per-object lock
    /// from the test itself; there is no per-object lock to take.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task CancellingWhileWaitingForTheWriteLock_WritesNothing()
    {
        var rig = Rig.WithGate(out var gate);
        var part = await rig.CreateGatedPartAsync();
        var other = await rig.CreateGatedPartAsync();

        var parked = gate.ArmNextTransaction();
        var holding = other.RenameAsync("Holds the one domain write lock");
        await parked;

        using (var cancellation = new CancellationTokenSource())
        {
            var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes, cancellation.Token);
            Assert.False(attaching.IsCompleted, "The attach did not wait for the domain write lock.");

            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attaching);
        }

        gate.Release();
        await holding;

        Assert.Empty(rig.ContentKeys);
        Assert.Empty(await part.GetAttachmentsAsync());

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Empty(state.Attachments);
    }

    /// <summary>
    /// Board finding `P3-6`. A refused <see cref="IHasAttachments.AttachAsync"/>
    /// — the metadata-only entry point — leaves the instance claiming
    /// nothing, and leaves nothing durable.
    /// </summary>
    /// <remarks>
    /// `WP 16.4B-R5` gave <c>AttachContentAsync</c> a compensation and left
    /// this entry point alone, so a refused attach left the instance
    /// permanently claiming an attachment the platform had told the caller
    /// it did not accept. The supersession check is now inside the
    /// transaction and the instance's list is written only after the
    /// commit, so neither entry point can leave a phantom.
    /// </remarks>
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
    /// An attach whose <b>commit</b> fails leaves neither the bytes nor a
    /// claim on them — and the object is still usable afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its predecessor asserted that a failed content write "strands no
    /// marker", which was the best available statement while the bytes and
    /// the record that names them were two durable writes with a marker
    /// bounding the gap between them. They are one write now, so the fact
    /// asserts what the marker protocol could only approximate: after a
    /// failure there is no row <em>and</em> no payload.
    /// </para>
    /// <para>
    /// The failure is injected at the commit rather than at the byte
    /// write, which is the strongest form: the whole body ran, every write
    /// the attach wanted to make was staged, and the only thing that did
    /// not happen is the commit.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AnAttachContentWhoseCommitFails_LeavesNeitherTheBytesNorAClaim()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreateGatedPartAsync();

        var bodiesBefore = failing.BodiesCompleted;
        failing.FailNextCommit = true;

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        // The body ran to completion — everything was staged — and the
        // commit is the only thing that did not happen.
        Assert.Equal(bodiesBefore + 1, failing.BodiesCompleted);
        Assert.Empty(rig.ContentKeys);
        Assert.Empty(await part.GetAttachmentsAsync());

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Empty(state.Attachments);

        // Still usable: the failure was a rollback, not damage.
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        Assert.True(rig.HasContent(attachment.Id));
        Assert.Single((await rig.StateStore.FindAsync(part.Id))!.Attachments);
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>The attachment an attach produced, or <see langword="null"/> if it was refused.</summary>
    private static async Task<IAttachment?> Outcome(Task<IAttachment> attaching)
    {
        try
        {
            return await attaching.ConfigureAwait(false);
        }
        catch (SupersededEngineeringObjectException)
        {
            return null;
        }
    }

    // ================================================================
    // Rig
    // ================================================================

    /// <summary>
    /// The shipped write path over one
    /// <see cref="InMemoryQueryablePersistenceStore"/>, optionally wrapped
    /// so a fault can be injected at the store.
    /// </summary>
    private sealed class Rig
    {
        public Rig()
            : this(new InMemoryQueryablePersistenceStore(), transactional: null)
        {
        }

        private Rig(InMemoryQueryablePersistenceStore store, IQueryablePersistenceStore? transactional)
        {
            Store = store;

            var principalAccessor = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
            var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

            StateStore = new EngineeringObjectStateStore(store);
            ContentStore = new AttachmentContentStore(store);

            Context = new EngineeringDomainContext(
                transactional ?? store,
                new EngineeringDocumentStore(store, principalAccessor),
                repository,
                relationshipRepository,
                new LifecycleTransitionTable(),
                new ValidationRuleSet(),
                new EvidenceComposer(relationshipDiscovery, repository),
                principalAccessor,
                StateStore,
                ContentStore);
        }

        /// <summary>The one durable store — the read surface for every "what landed?" assertion.</summary>
        public InMemoryQueryablePersistenceStore Store { get; }

        public EngineeringObjectStateStore StateStore { get; }

        public AttachmentContentStore ContentStore { get; }

        public EngineeringDomainContext Context { get; }

        /// <summary>Every committed attachment payload key.</summary>
        public IReadOnlyList<string> ContentKeys => Store.CommittedKeys(AttachmentContentStore.ContentCollectionName);

        /// <summary>Whether committed attachment content exists for <paramref name="attachmentId"/>.</summary>
        public bool HasContent(Guid attachmentId) =>
            Store.CommittedBytes(AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N")) is not null;

        /// <summary>A rig whose transactions can be parked at the end of their bodies.</summary>
        public static Rig WithGate(out GatedPersistenceStore gate)
        {
            var store = new InMemoryQueryablePersistenceStore();
            gate = new GatedPersistenceStore(store);
            return new Rig(store, gate);
        }

        /// <summary>A rig whose commits can be failed deterministically, after the whole body has run.</summary>
        public static Rig WithFailableCommit(out CommitFailingPersistenceStore failing)
        {
            var store = new InMemoryQueryablePersistenceStore();
            failing = new CommitFailingPersistenceStore(store);
            return new Rig(store, failing);
        }

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
    /// <c>ReviseAsync</c> calls <b>inside its transaction</b>, while the
    /// domain write lock is held — so parking there parks the revision at
    /// exactly the instant the review board's reproduction needs, with no
    /// timing and no sleep. It is a seam, not a defect: the same extension
    /// point every concrete Kind in the platform already overrides.
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
}

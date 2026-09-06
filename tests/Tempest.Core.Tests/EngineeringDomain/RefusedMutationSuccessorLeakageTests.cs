using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// <b>Board 5 finding P1-F4, executable — and CLOSED for all seven
/// mutators (`WP 16.4B-R6b`).</b> An operation the platform
/// <em>refused</em>, with
/// <see cref="SupersededEngineeringObjectException"/>, leaves nothing
/// behind: not in the instance that was refused, not in the
/// <see cref="EngineeringObjectBase.ReviseAsync"/> successor that is now
/// live for the Id, and not in the durable record after that successor's
/// next write.
/// </summary>
/// <remarks>
/// <para>
/// <b>Provenance — these facts were inverted, not written.</b> Until
/// `WP 16.4B-R6b` this file was a set of characterisation tests that
/// pinned the defect: five of them asserted the leak, each named
/// <c>…_OpenDefect</c>, and the file's own instruction to the next
/// engineer read <i>"when the defect is fixed, invert these assertions —
/// do not delete these facts: the exact leak each one names is what the
/// fix has to stop."</i> That is what happened here. Every one of the five
/// interleavings is still exercised, byte for byte the same race in the
/// same rig; only the expectation moved, and each test still names the
/// leak it used to assert. The <c>_OpenDefect</c> suffixes are gone
/// because they would now be false; the sixth board is invited to check
/// the inversion against `WP 16.4B-R6`'s tree, where all five of these
/// fail.
/// </para>
/// <para>
/// <b>The shape that was wrong.</b> All five of <c>TransitionAsync</c>,
/// <c>RenameAsync</c>, <c>MoveAsync</c>, <c>DeleteAsync</c> and
/// <c>SetBomLineAsync</c> mutated their in-memory field <em>first</em> and
/// only then called <c>PersistStateAsync</c>, which is where the write
/// lock and the supersession check live. A concurrent <c>ReviseAsync</c>
/// that took the lock in between captured the mutation into the successor
/// and then made the mutator's own write throw. The caller was told the
/// write did not happen. It had, in the only instance that still answered
/// for the Id. All five now take the lock themselves and refuse before
/// they touch anything — the shape `WP 16.4B-R6` had already applied to
/// the two attachment entry points.
/// </para>
/// <para>
/// <b>What each one was, and therefore what each one now has to prove:</b>
/// </para>
/// <list type="bullet">
/// <item><description><c>TransitionAsync</c> — a lifecycle state and an audit-trail entry the caller was told were rejected, both durable. The most serious of the five: the transition history is the platform's own governance record and has no removal path.</description></item>
/// <item><description><c>DeleteAsync</c> — the object ended up durably soft-deleted although the caller saw the delete fail, with no undelete anywhere in the platform, <b>and</b> its attachment content was never released, because the throw happened before the <c>TD-97</c> byte release.</description></item>
/// <item><description><c>MoveAsync</c> — the new parent was durable, and the permanent, append-only <c>groupedUnder</c> relationship was recorded even though the move was refused.</description></item>
/// <item><description><c>RenameAsync</c> and <c>SetBomLineAsync</c> — the new value was durable.</description></item>
/// </list>
/// <para>
/// <b>Determinism.</b> No sleeps, no polling. <see cref="GatedFixture"/>
/// parks inside <c>CaptureTypeState</c>, which <c>CaptureState</c> calls
/// from inside <c>ReviseAsync</c>'s write lock and <em>before</em> it
/// reads any of the fields under test. Against the pre-fix code the
/// mutator, started on the calling thread while the revision is parked
/// there, had applied its in-memory change and was waiting for the lock;
/// against the fixed code it is waiting for the lock before it has done
/// anything at all. Either way the interleaving is forced, not raced —
/// and <see cref="EveryMutatorOnAnAlreadyRetiredInstance_IsRefusedAndChangesNothing"/>
/// establishes the same invariant with no concurrency whatsoever.
/// </para>
/// </remarks>
public sealed class RefusedMutationSuccessorLeakageTests
{
    private static readonly byte[] Bytes = [4, 5, 6];

    // ================================================================
    // The five that `WP 16.4B-R6b` closed
    // (inverted from the `_OpenDefect` facts that pinned each leak)
    // ================================================================

    /// <summary>
    /// Was <c>ARefusedRename_StillLeaksTheNewNameIntoTheSuccessor_OpenDefect</c>:
    /// a refused rename renamed the successor and became durable.
    /// </summary>
    [Fact]
    public async Task ARefusedRename_LeaksNothingIntoTheSuccessor()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.RenameAsync("Leaked name"));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal("Bracket", successor.DisplayName);
        Assert.Equal("Bracket", part.DisplayName);

        await successor.SetBomLineAsync(2m);
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Bracket", state.DisplayName);
    }

    /// <summary>
    /// Was
    /// <c>ARefusedTransition_StillLeaksTheStateAndTheAuditEntryIntoTheSuccessor_OpenDefect</c>:
    /// a refused lifecycle transition moved the successor's lifecycle
    /// state <b>and</b> wrote an entry into the transition history — the
    /// record this platform treats as an audit trail, with an actor
    /// principal id and a timestamp, for a transition that was rejected.
    /// </summary>
    [Fact]
    public async Task ARefusedTransition_WritesNoStateAndNoAuditEntryAnywhere()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        Assert.Equal(LifecycleState.Draft, part.Status);

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.TransitionAsync(LifecycleState.InReview));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal(LifecycleState.Draft, successor.Status);
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(successor.History);
        Assert.Empty(part.History);

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Draft, state.Status);
        Assert.Empty(state.History);
    }

    /// <summary>
    /// Was
    /// <c>ARefusedDelete_StillLeavesTheSuccessorDeleted_AndNeverReleasesItsContent_OpenDefect</c>,
    /// the worst-consequence member of the five: a refused delete left the
    /// successor durably soft-deleted, with no undelete anywhere in the
    /// platform and every read model filtering deleted objects out, while
    /// the <c>TD-97</c> attachment-content release ran for neither.
    /// </summary>
    /// <remarks>
    /// The content assertions read the same way as they did before the fix
    /// and mean the opposite thing: the bytes are still present and
    /// <c>DeleteCallCount</c> is still zero — but now because <b>nothing
    /// was deleted</b>, which is the correct outcome for an object that,
    /// after the refusal, is not deleted. That the release does still run
    /// on a delete that succeeds is asserted separately by
    /// <see cref="ASuccessfulDelete_StillReleasesItsContent_AfterTheStateWrite"/>,
    /// so this pair cannot be satisfied by a fix that simply stops
    /// releasing bytes.
    /// </remarks>
    [Fact]
    public async Task ARefusedDelete_DeletesNothingAndReleasesNoContent()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.DeleteAsync());

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.False(successor.IsDeleted, "A delete the caller was told had failed left the live successor soft-deleted — P1-F4 for DeleteAsync.");
        Assert.False(part.IsDeleted);

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.False(state.IsDeleted);

        // Nothing was deleted, so nothing was released — and the successor
        // still legitimately holds the attachment it inherited.
        Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);
        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
        Assert.Contains(await successor.GetAttachmentsAsync(), a => a.Id == attachment.Id);
    }

    /// <summary>
    /// Was
    /// <c>ARefusedMove_StillLeaksTheNewParentAndRecordsTheRelationship_OpenDefect</c>:
    /// a refused move reparented the successor and recorded the permanent,
    /// append-only <c>groupedUnder</c> relationship for a move the caller
    /// was told did not happen.
    /// </summary>
    [Fact]
    public async Task ARefusedMove_LeaksNoParentAndRecordsNoRelationship()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();
        var parent = await rig.CreateAsync("PRT-2", "Housing");

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.MoveAsync(parent.Id));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Null(successor.ParentId);
        Assert.Null(part.ParentId);

        var relationships = await part.GetRelationshipsAsync();
        Assert.DoesNotContain(relationships, r => r.TargetId == parent.Id && r.RelationshipKind == "groupedUnder");

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Null(state.ParentId);
    }

    /// <summary>
    /// Was
    /// <c>ARefusedSetBomLine_StillLeaksTheNewLineIntoTheSuccessor_OpenDefect</c>:
    /// a refused BOM-line change landed on the successor and became
    /// durable.
    /// </summary>
    [Fact]
    public async Task ARefusedSetBomLine_LeaksNothingIntoTheSuccessor()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var (successor, refused) = await RaceAgainstARevisionAsync(
            part, () => part.SetBomLineAsync(17m, "each", "FN-9", "IN-9", "RD-9"));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal(1m, successor.Quantity);
        Assert.Null(successor.UnitOfMeasure);
        Assert.Null(successor.FindNumber);
        Assert.Equal(1m, part.Quantity);
        Assert.Null(part.FindNumber);

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(1m, state.BomLine.Quantity);
        Assert.Null(state.BomLine.FindNumber);
    }

    // ================================================================
    // The same invariant with no concurrency at all, and the success
    // paths the refusals must not have broken (`WP 16.4B-R6b`)
    // ================================================================

    /// <summary>
    /// The whole invariant in <b>program order</b>: revise once, then put
    /// every mutator declared on <see cref="EngineeringObjectBase"/>
    /// through the retired predecessor. Each is refused, and nothing
    /// anywhere moves — not the predecessor, not the live successor, not
    /// the durable record, not the relationship repository, not the
    /// content store, not the write-intent store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No gate, no second thread, no <c>Task.Run</c>: the interleaving the
    /// five race tests force is only one way to reach a retired instance,
    /// and this is the way a real caller reaches it — by holding a
    /// reference across someone else's completed revision. It is also the
    /// test that would hang rather than fail if any of these mutators
    /// re-entered the non-reentrant write lock, which is why every call is
    /// bounded by <see cref="Timeout"/> instead of being awaited
    /// indefinitely.
    /// </para>
    /// <para>
    /// The seven are enumerated here explicitly rather than by reflection,
    /// so that adding an eighth mutator to that type does not silently
    /// inherit a pass.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task EveryMutatorOnAnAlreadyRetiredInstance_IsRefusedAndChangesNothing()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();
        var parent = await rig.CreateAsync("PRT-2", "Housing");

        var inherited = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        var successor = (GatedFixture)await part.ReviseAsync("Revised content.", "Rev B.");

        var phantom = new Attachment("phantom.pdf", "application/pdf", 3);

        await RefusedAsync(() => part.TransitionAsync(LifecycleState.InReview));
        await RefusedAsync(() => part.RenameAsync("Leaked name"));
        await RefusedAsync(() => part.MoveAsync(parent.Id));
        await RefusedAsync(() => part.DeleteAsync());
        await RefusedAsync(() => part.SetBomLineAsync(17m, "each", "FN-9", "IN-9", "RD-9"));
        await RefusedAsync(() => part.AttachAsync(phantom));
        await RefusedAsync(() => part.AttachContentAsync("second.pdf", "application/pdf", Bytes));

        // The retired instance did not move.
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);
        Assert.Equal("Bracket", part.DisplayName);
        Assert.Null(part.ParentId);
        Assert.False(part.IsDeleted);
        Assert.Equal(1m, part.Quantity);

        // Neither did the live successor.
        Assert.Equal(LifecycleState.Draft, successor.Status);
        Assert.Empty(successor.History);
        Assert.Equal("Bracket", successor.DisplayName);
        Assert.Null(successor.ParentId);
        Assert.False(successor.IsDeleted);
        Assert.Equal(1m, successor.Quantity);
        Assert.Equal(inherited.Id, Assert.Single(await successor.GetAttachmentsAsync()).Id);

        // Nor did anything durable. The successor's own write is what
        // makes this an assertion about the record rather than about a
        // call that simply never reached its store.
        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Draft, state.Status);
        Assert.Empty(state.History);
        Assert.Equal("Written by the successor", state.DisplayName);
        Assert.Null(state.ParentId);
        Assert.False(state.IsDeleted);
        Assert.Equal(1m, state.BomLine.Quantity);
        Assert.Equal(inherited.Id, Assert.Single(state.Attachments).Id);

        Assert.Empty(await part.GetRelationshipsAsync());
        Assert.Equal([inherited.Id], rig.ContentStore.StoredKeys);
        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());
    }

    /// <summary>
    /// The other half of
    /// <see cref="ARefusedDelete_DeletesNothingAndReleasesNoContent"/>:
    /// a delete that <em>succeeds</em> still releases its attachment
    /// content, and still does so <b>after</b> the state write that records
    /// the deletion, never before (`TD-97`, <c>ADR-0114</c>).
    /// </summary>
    /// <remarks>
    /// The ordering is asserted from an observation log the two fakes
    /// share, not inferred from the call counts, because "released" and
    /// "released in the right order" are different claims and only the
    /// second one is the `TD-97` contract: bytes must not be dropped ahead
    /// of the durable deletion that justifies dropping them.
    /// </remarks>
    [Fact]
    public async Task ASuccessfulDelete_StillReleasesItsContent_AfterTheStateWrite()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        rig.Log.Clear();
        await part.DeleteAsync();

        Assert.True(part.IsDeleted);

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.True(state.IsDeleted);

        Assert.Equal(1, rig.ContentStore.DeleteCallCount);
        Assert.DoesNotContain(attachment.Id, rig.ContentStore.StoredKeys);

        // Exactly one state write, then the release — in that order.
        Assert.Equal(["state-save", "content-delete"], rig.Log.Snapshot());
    }

    // ================================================================
    // The two that R6 closed — the contrast the other five now match
    // ================================================================

    /// <summary>
    /// The same race, against the metadata-only attach entry point that
    /// `WP 16.4B-R6` fixed. The supersession check precedes the in-memory
    /// add, so nothing leaks — this is the shape `WP 16.4B-R6b` extended to
    /// the five above.
    /// </summary>
    /// <remarks>
    /// Discriminating: before R6 this fact failed exactly as its five
    /// neighbours did until `WP 16.4B-R6b`, with the phantom attachment
    /// carried into the successor and made durable by the successor's next
    /// write.
    /// </remarks>
    [Fact]
    public async Task ARefusedAttachAsync_LeaksNothingIntoTheSuccessor()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();
        var phantom = new Attachment("phantom.pdf", "application/pdf", 3);

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.AttachAsync(phantom));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.DoesNotContain(await successor.GetAttachmentsAsync(), a => a.Id == phantom.Id);
        Assert.DoesNotContain(await part.GetAttachmentsAsync(), a => a.Id == phantom.Id);

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.DoesNotContain(state.Attachments, a => a.Id == phantom.Id);
    }

    /// <summary>
    /// The same race against <c>AttachContentAsync</c>, which R6 also
    /// closed — and where the leak was not merely divergence but the
    /// board's RED, because the R5 compensation then deleted the bytes the
    /// successor had adopted.
    /// </summary>
    [Fact]
    public async Task ARefusedAttachContentAsync_LeaksNothingIntoTheSuccessorAndWritesNothing()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var (successor, refused) = await RaceAgainstARevisionAsync(
            part, () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Empty(await successor.GetAttachmentsAsync());
        Assert.Empty(rig.ContentStore.StoredKeys);
        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());

        // And the durable record for this Id — which the successor also
        // owns — names nothing either, once the successor has written it.
        // "The exception happened before this call's own SaveAsync" is not
        // the question; "did any instance for this Id write or inherit it"
        // is, and the answer has to be no.
        await successor.RenameAsync("Written by the successor");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Empty(state.Attachments);
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Long enough that no machine fails this by being slow, short enough
    /// that a genuine self-deadlock on the non-reentrant write lock is
    /// reported as a failed fact rather than a hung test run.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Asserts <paramref name="mutate"/> is refused with
    /// <see cref="SupersededEngineeringObjectException"/>, and that it
    /// completes at all — a mutator that took this object's write lock and
    /// then reached <c>PersistStateAsync</c> would deadlock against itself
    /// here rather than throw.
    /// </summary>
    private static async Task RefusedAsync(Func<Task> mutate) =>
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => mutate().WaitAsync(Timeout));

    /// <summary>
    /// Runs <paramref name="mutate"/> in the one interleaving P1-F4 is
    /// about: the revision has the write lock and is parked inside its own
    /// capture, before it reads any of the fields under test; the mutator
    /// is then started on this thread, applies its in-memory change and
    /// blocks on the lock; the revision is released and captures the
    /// change; the mutator wakes and is refused.
    /// </summary>
    /// <returns>The successor, and the exception the refused mutator threw.</returns>
    private static async Task<(GatedFixture Successor, Exception Refused)> RaceAgainstARevisionAsync(
        GatedFixture instance, Func<Task> mutate)
    {
        var parked = instance.ArmNextCapture();
        var revising = Task.Run(() => instance.ReviseAsync("Revised content.", "Rev B."));
        await parked;

        var mutating = mutate();

        instance.ReleaseCapture();

        var successor = (GatedFixture)await revising;
        var refused = await Record.ExceptionAsync(() => mutating);

        Assert.NotNull(refused);
        return (successor, refused);
    }

    /// <summary>
    /// An ordered record of the durable calls the fakes below receive, so
    /// a fact can assert <em>sequence</em> and not only occurrence.
    /// </summary>
    private sealed class ObservationLog
    {
        private readonly List<string> _entries = new();

        public void Record(string entry)
        {
            lock (_entries) { _entries.Add(entry); }
        }

        public void Clear()
        {
            lock (_entries) { _entries.Clear(); }
        }

        public IReadOnlyList<string> Snapshot()
        {
            lock (_entries) { return _entries.ToList(); }
        }
    }

    private sealed class Rig
    {
        public Rig()
        {
            var principal = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            StateStore = new FakeObjectStateStore(Log);
            ContentStore = new FakeAttachmentContentStore(Log);

            Context = new EngineeringDomainContext(
                new InMemoryEngineeringDocumentStore(principal), repository, relationships,
                new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, StateStore, ContentStore, WriteIntentStore);
        }

        public ObservationLog Log { get; } = new();
        public FakeObjectStateStore StateStore { get; }
        public FakeAttachmentContentStore ContentStore { get; }
        public FakeWriteIntentStore WriteIntentStore { get; } = new();
        public EngineeringDomainContext Context { get; }

        public async Task<GatedFixture> CreateAsync(string identifier = "PRT-1", string displayName = "Bracket") =>
            (GatedFixture)await new EngineeringObjectFactory<GatedFixture>(
                    GatedFixture.KindName, Context,
                    (d, r) => new GatedFixture(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.").ConfigureAwait(false);
    }

    /// <summary>
    /// An ordinary Engineering Object whose <c>CaptureTypeState</c> — the
    /// same extension point every concrete Kind overrides — can be parked
    /// on demand.
    /// </summary>
    private sealed class GatedFixture : EngineeringObjectBase, IRehydratable<GatedFixture>
    {
        public const string KindName = "GatedFixture";

        private TaskCompletionSource? _parked;
        private TaskCompletionSource? _release;

        public GatedFixture(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
            string? identifier, string displayName, EngineeringObjectMetadata metadata)
            : base(document, currentRevision, context, identifier, displayName, metadata)
        {
        }

        public Task ArmNextCapture()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _parked.Task;
        }

        public void ReleaseCapture() => _release?.TrySetResult();

        protected override void CaptureTypeState(IDictionary<string, string?> state)
        {
            var parked = Interlocked.Exchange(ref _parked, null);

            if (parked is not null)
            {
                parked.TrySetResult();
                _release!.Task.GetAwaiter().GetResult();
            }
        }

        static GatedFixture IRehydratable<GatedFixture>.Rehydrate(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
            new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
    }

    private sealed class FakeObjectStateStore(ObservationLog log) : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            log.Record("state-save");
            lock (_states) { _states[state.Id] = state; }
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

    private sealed class FakeAttachmentContentStore(ObservationLog log) : IAttachmentContentStore
    {
        private readonly Dictionary<Guid, byte[]> _content = new();
        private int _deleteCallCount;

        public int DeleteCallCount => Volatile.Read(ref _deleteCallCount);

        public IReadOnlyCollection<Guid> StoredKeys
        {
            get { lock (_content) { return _content.Keys.ToList(); } }
        }

        public Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
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
            log.Record("content-delete");
            Interlocked.Increment(ref _deleteCallCount);
            lock (_content) { _content.Remove(attachmentId); }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWriteIntentStore : IAttachmentWriteIntentStore
    {
        private readonly HashSet<Guid> _marked = new();

        public Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_marked) { _marked.Add(attachmentId); }
            return Task.CompletedTask;
        }

        public Task ClearAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            lock (_marked) { _marked.Remove(attachmentId); }
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<Guid>> ListMarkedAsync(CancellationToken cancellationToken = default)
        {
            lock (_marked) { return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>(_marked)); }
        }
    }
}

using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// <b>Board 5 finding P1-F4, executable — and OPEN.</b> A durable write
/// that the platform <em>refused</em>, with
/// <see cref="SupersededEngineeringObjectException"/>, still leaks its
/// in-memory mutation into the successor and becomes durable on the
/// successor's next write.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are characterisation tests. They pin behaviour this file
/// asserts is wrong.</b> They exist because `WP 16.4B-R6` closed P1-F4
/// for the two attachment entry points only and disclosed that it remains
/// open for the other five mutators, and a disclosure that lives in a
/// report is one board away from being forgotten. When the defect is
/// fixed, <b>invert these assertions — do not delete these facts</b>: the
/// exact leak each one names is what the fix has to stop.
/// </para>
/// <para>
/// <b>The shape.</b> All five of <c>TransitionAsync</c>,
/// <c>RenameAsync</c>, <c>MoveAsync</c>, <c>DeleteAsync</c> and
/// <c>SetBomLineAsync</c> mutate their in-memory field <em>first</em> and
/// only then call <c>PersistStateAsync</c>, which is where the write lock
/// and the supersession check live. A concurrent <c>ReviseAsync</c> that
/// takes the lock in between captures the mutation into the successor and
/// then makes the mutator's own write throw. The caller is told the write
/// did not happen. It did, in the only instance that now matters.
/// </para>
/// <para>
/// <b>Why this is not the same severity as the RED the board found.</b>
/// Nothing is destroyed and no accepted write is lost — this is in-memory
/// divergence that becomes durable, not deletion. The consequences are
/// nonetheless concrete, and each test names its own:
/// </para>
/// <list type="bullet">
/// <item><description><c>TransitionAsync</c> — a lifecycle state and an audit-trail entry the caller was told were rejected, both durable. The most serious of the five: the transition history is the platform's own governance record.</description></item>
/// <item><description><c>DeleteAsync</c> — the object ends up durably soft-deleted although the caller saw the delete fail, <b>and</b> its attachment content is never released, because the throw happens before the <c>TD-97</c> byte release. A durable delete with the disclosed cleanup skipped.</description></item>
/// <item><description><c>MoveAsync</c> — the new parent is durable, and the permanent <c>groupedUnder</c> relationship is recorded even though the move was refused.</description></item>
/// <item><description><c>RenameAsync</c> and <c>SetBomLineAsync</c> — the new value is durable.</description></item>
/// </list>
/// <para>
/// <b>Determinism.</b> No sleeps, no polling. <see cref="GatedFixture"/>
/// parks inside <c>CaptureTypeState</c>, which <c>CaptureState</c> calls
/// from inside <c>ReviseAsync</c>'s write lock and <em>before</em> it
/// reads any of the fields under test — so the mutator, started on the
/// calling thread while the revision is parked there, is guaranteed to
/// have applied its in-memory change and to be waiting for the lock.
/// </para>
/// </remarks>
public sealed class RefusedMutationSuccessorLeakageTests
{
    private static readonly byte[] Bytes = [4, 5, 6];

    // ================================================================
    // The five that remain open
    // ================================================================

    /// <summary>OPEN DEFECT (P1-F4): a refused rename still renames the successor.</summary>
    [Fact]
    public async Task ARefusedRename_StillLeaksTheNewNameIntoTheSuccessor_OpenDefect()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.RenameAsync("Leaked name"));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal("Leaked name", successor.DisplayName);

        await successor.SetBomLineAsync(2m);
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Leaked name", state.DisplayName);
    }

    /// <summary>
    /// OPEN DEFECT (P1-F4): a refused lifecycle transition still moves the
    /// successor's lifecycle state <b>and</b> writes an entry into the
    /// transition history — the record this platform treats as an audit
    /// trail.
    /// </summary>
    [Fact]
    public async Task ARefusedTransition_StillLeaksTheStateAndTheAuditEntryIntoTheSuccessor_OpenDefect()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        Assert.Equal(LifecycleState.Draft, part.Status);

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.TransitionAsync(LifecycleState.InReview));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal(LifecycleState.InReview, successor.Status);

        var leaked = Assert.Single(successor.History);
        Assert.Equal(LifecycleState.Draft, leaked.From);
        Assert.Equal(LifecycleState.InReview, leaked.To);

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(LifecycleState.InReview, state.Status);
        Assert.Single(state.History);
    }

    /// <summary>
    /// OPEN DEFECT (P1-F4), and the worst-consequence member of the five:
    /// a refused delete leaves the successor durably soft-deleted, while
    /// the <c>TD-97</c> attachment-content release that
    /// <c>DeleteAsync</c> performs <em>after</em> its persist never runs —
    /// because the persist threw.
    /// </summary>
    [Fact]
    public async Task ARefusedDelete_StillLeavesTheSuccessorDeleted_AndNeverReleasesItsContent_OpenDefect()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.DeleteAsync());

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.True(successor.IsDeleted, "The successor is not soft-deleted — if this now fails, P1-F4 has been closed for DeleteAsync and this test must be inverted, not deleted.");

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.True(state.IsDeleted);

        // The bytes of a durably deleted object's attachment are still
        // there: `DeleteAsync`'s own release runs after the persist that
        // threw, so it never ran.
        Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);
        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
    }

    /// <summary>
    /// OPEN DEFECT (P1-F4): a refused move still reparents the successor,
    /// and the permanent, append-only <c>groupedUnder</c> relationship is
    /// recorded for a move the caller was told did not happen.
    /// </summary>
    [Fact]
    public async Task ARefusedMove_StillLeaksTheNewParentAndRecordsTheRelationship_OpenDefect()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();
        var parent = await rig.CreateAsync("PRT-2", "Housing");

        var (successor, refused) = await RaceAgainstARevisionAsync(part, () => part.MoveAsync(parent.Id));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal(parent.Id, successor.ParentId);

        var relationships = await part.GetRelationshipsAsync();
        Assert.Contains(relationships, r => r.TargetId == parent.Id && r.RelationshipKind == "groupedUnder");

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(parent.Id, state.ParentId);
    }

    /// <summary>OPEN DEFECT (P1-F4): a refused BOM-line change still lands on the successor.</summary>
    [Fact]
    public async Task ARefusedSetBomLine_StillLeaksTheNewLineIntoTheSuccessor_OpenDefect()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var (successor, refused) = await RaceAgainstARevisionAsync(
            part, () => part.SetBomLineAsync(17m, "each", "FN-9", "IN-9", "RD-9"));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        Assert.Equal(17m, successor.Quantity);
        Assert.Equal("each", successor.UnitOfMeasure);
        Assert.Equal("FN-9", successor.FindNumber);

        await successor.RenameAsync("Written by the successor");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(17m, state.BomLine.Quantity);
        Assert.Equal("FN-9", state.BomLine.FindNumber);
    }

    // ================================================================
    // The two that R6 closed — the contrast that gives the five meaning
    // ================================================================

    /// <summary>
    /// The same race, against the metadata-only attach entry point that
    /// `WP 16.4B-R6` fixed. The supersession check now precedes the
    /// in-memory add, so nothing leaks — this is what the five above are
    /// expected to look like once the pattern is extended to them.
    /// </summary>
    /// <remarks>
    /// Discriminating: before R6 this fact failed exactly as its five
    /// neighbours still do, with the phantom attachment carried into the
    /// successor and made durable by the successor's next write.
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

    private sealed class Rig
    {
        public Rig()
        {
            var principal = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            Context = new EngineeringDomainContext(
                new InMemoryEngineeringDocumentStore(principal), repository, relationships,
                new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, StateStore, ContentStore, WriteIntentStore);
        }

        public FakeObjectStateStore StateStore { get; } = new();
        public FakeAttachmentContentStore ContentStore { get; } = new();
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

    private sealed class FakeObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
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

    private sealed class FakeAttachmentContentStore : IAttachmentContentStore
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

using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R3` — the lost-update fix. <see cref="EngineeringObjectBase.PersistStateAsync"/>
/// now serialises each object's own capture-then-persist sequence
/// end-to-end, keyed by the object's Id
/// (<see cref="EngineeringDomainContext"/>'s own per-object write lock).
/// </summary>
/// <remarks>
/// These tests force two concurrent mutations into the exact interleaving
/// the independent post-remediation review reproduced against the real
/// classes — one mutation's state is captured before the other's own
/// change is added, but only lands on disk after it — using the
/// codebase's own <see cref="TaskCompletionSource"/> gating idiom (see
/// <c>AsyncKeyedLockTests</c> and <c>AttachmentContentReconciliationServiceTests</c>'s
/// own <c>GatedObjectStateStore</c>): no <see cref="Task.Delay(int)"/>, no
/// timing dependence, no retry loop.
/// <para>
/// <b>Why a fresh, fully in-memory gate rather than reusing the file-backed
/// <c>GatedObjectStateStore</c>.</b> Forcing the bug's exact interleaving
/// deterministically requires knowing, without guessing, that the second
/// call's own capture-and-save has actually run to completion before the
/// first call's paused, stale save is released — and the fixed code's own
/// lock makes that combination structurally impossible to also await
/// directly (the second call cannot even begin its own capture until the
/// first releases the lock it holds while paused). Using <see cref="IHasAttachments.AttachAsync"/>
/// (metadata only — no content-store or write-intent-store I/O) together
/// with a gate that has no persistence layer of its own underneath it
/// (no file I/O, no second <see cref="Concurrency.AsyncKeyedLock"/> from
/// <c>PersistenceStore</c> to race) means every step before the gate —
/// this object's own field mutation, its <see cref="EngineeringObjectBase.CaptureState"/> —
/// is <em>synchronous, uninterrupted C# code</em> on both sides of the
/// fix: under the pre-fix shape this makes the second call's full round
/// trip (capture and land) complete deterministically, as one synchronous
/// chain, within the single statement that starts it — before the test
/// ever reaches the line that releases the first call — which is exactly
/// what makes the failure below deterministic rather than the
/// reviewer's own probabilistic "one trial in 500". Under the fixed
/// shape the second call instead blocks acquiring the lock, and the test
/// never awaits it before releasing the first, so nothing here can hang
/// either way.
/// </para>
/// </remarks>
public sealed class EngineeringObjectWriteSerializationTests
{
    /// <summary>
    /// A fully in-memory <see cref="IEngineeringObjectStateStore"/> with a
    /// single-slot save gate — the in-memory twin of
    /// <c>AttachmentContentReconciliationServiceTests.GatedObjectStateStore</c>,
    /// used here instead of that file-backed one for the determinism
    /// reason this file's own remarks give. Disarmed by default: every
    /// <see cref="SaveAsync"/> call passes straight through (including the
    /// one <see cref="EngineeringObjectFactory{T}.CreateAsync"/> itself
    /// makes to persist a freshly-created object's initial state) until a
    /// test calls <see cref="ArmNextSave"/> immediately before the one
    /// call it wants to pause.
    /// </summary>
    private sealed class GatedInMemoryObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();
        private TaskCompletionSource? _reachedSave;
        private TaskCompletionSource? _releaseSave;

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

        /// <summary>Lets the armed, now-paused <see cref="SaveAsync"/> call proceed to actually land.</summary>
        public void ReleaseSave() => _releaseSave?.TrySetResult();

        public async Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            // Only _reachedSave is swapped out here — disarming the gate
            // for any later, unrelated SaveAsync call — mirroring the
            // file-backed GatedObjectStateStore's own reasoning exactly.
            var reached = Interlocked.Exchange(ref _reachedSave, null);

            if (reached is not null)
            {
                reached.TrySetResult();
                await _releaseSave!.Task.ConfigureAwait(false);
            }

            lock (_states)
            {
                _states[state.Id] = state;
            }
        }

        public Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default)
        {
            lock (_states)
                return Task.FromResult(_states.TryGetValue(objectId, out var state) ? state : null);
        }

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
        {
            lock (_states)
                return Task.FromResult<IReadOnlyList<EngineeringObjectState>>(_states.Values.ToList());
        }

        public Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default)
        {
            lock (_states)
                _states.Remove(objectId);

            return Task.CompletedTask;
        }
    }

    private static EngineeringDomainContext BuildContext(IEngineeringObjectStateStore stateStore)
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var lifecycleTable = new LifecycleTransitionTable();
        var validationRuleSet = new ValidationRuleSet();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);
        var evidenceComposer = new EvidenceComposer(relationshipDiscovery, repository);

        return new EngineeringDomainContext(
            store, repository, relationshipRepository, lifecycleTable, validationRuleSet, evidenceComposer,
            principalAccessor, stateStore);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    /// <summary>
    /// The reproduction itself, matching the independent review's own
    /// repro shape (two concurrent attaches on one <see cref="Part"/>).
    /// The first call's state is captured — holding only its own
    /// attachment — and paused before landing; the second, free to run
    /// while the first is paused, adds its own attachment. Before
    /// `WP 16.4B-R3`, the second call's fresher, two-attachment snapshot
    /// lands first and the first call's stale, one-attachment snapshot
    /// then overwrites it once released — attachment two is durably lost
    /// even though <see cref="IHasAttachments.AttachAsync"/> reported
    /// success for it. With the fix, the assertion holds unconditionally,
    /// regardless of which of the two calls the per-object lock lets in
    /// first — see <see cref="EngineeringDomainContext"/>'s own remarks on
    /// why: whichever call is the last to actually enter the lock always
    /// captures the union of both mutations, because each one's own field
    /// write happens, in program order, before it ever asks for the lock.
    /// </summary>
    [Fact]
    public async Task TwoConcurrentAttachAsyncCalls_BothSurviveOnDisk()
    {
        var stateStore = new GatedInMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var attachment1 = new Attachment("first.pdf", "application/pdf", 3);
        var attachment2 = new Attachment("second.pdf", "application/pdf", 3);

        // Arm immediately before this specific call, so the
        // object-creation state write CreatePartAsync already made above
        // was never itself paused waiting for a release nobody had
        // reached yet.
        var reachedSave = stateStore.ArmNextSave();
        var attach1 = part.AttachAsync(attachment1);
        await reachedSave;

        // The second attach starts here, and is deliberately not awaited
        // yet: against the fix its own PersistStateAsync call blocks
        // trying to acquire the write lock the first call still holds
        // while paused, and nothing here waits on it before the release
        // below, so this can never hang the test either way (see this
        // file's own remarks).
        var attach2 = part.AttachAsync(attachment2);

        // Let the first call's already-captured (attachment1-only) state
        // land.
        stateStore.ReleaseSave();

        await attach1;
        await attach2;

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment1.Id);
        Assert.Contains(state.Attachments, a => a.Id == attachment2.Id);
    }

    /// <summary>
    /// The same forced interleaving with two genuinely <em>different</em>
    /// mutations — a rename and an attach — proving the per-object lock
    /// serialises every mutator uniformly, not attachments specifically.
    /// </summary>
    [Fact]
    public async Task ConcurrentRenameAndAttach_BothSurviveOnDisk()
    {
        var stateStore = new GatedInMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");
        var attachment = new Attachment("drawing.pdf", "application/pdf", 3);

        var reachedSave = stateStore.ArmNextSave();
        var rename = part.RenameAsync("Renamed Bracket");
        await reachedSave;

        var attach = part.AttachAsync(attachment);

        stateStore.ReleaseSave();

        await rename;
        await attach;

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed Bracket", state.DisplayName);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);
    }
}

using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R6` — an Engineering Object identity has <b>at most one live
/// successor</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these pin.</b> <c>ReviseAsync</c> assigned
/// <c>_supersededBy</c> unconditionally, so revising one predecessor twice
/// minted successors A and B, overwrote the predecessor's own retirement
/// pointer from A to B, and left <b>A un-retired</b>. A and B were then two
/// independently mutable instances mapped onto one durable record with no
/// ordering point between them: A's durable write was accepted, and B's
/// next write overwrote the whole record from a snapshot taken before it —
/// which is verbatim the `TD-136` lost update that
/// <c>SupersededEngineeringObjectException</c> exists to prevent, reachable
/// in program order with no threads at all.
/// </para>
/// <para>
/// The guard is deliberately <em>per instance</em>, not a one-shot latch on
/// the Id: revising a successor is ordinary and must keep working, which is
/// what <see cref="ARevisionChain_IsUnaffected"/> holds down. The
/// difference is that each successor is itself unrevised until it is
/// revised, whereas an already-revised instance has handed its identity on
/// and has nothing left to hand on again.
/// </para>
/// </remarks>
public sealed class RevisionSuccessorUniquenessTests
{
    /// <summary>
    /// The refusal itself. Deterministic, in program order — no threads and
    /// no gate are needed to reach the defect, which is what made it a RED
    /// finding rather than a race.
    /// </summary>
    [Fact]
    public async Task RevisingAnAlreadyRevisedInstance_IsRefused()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var first = (Part)await part.ReviseAsync("Second content.", "Rev B.");

        var thrown = await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.ReviseAsync("Third content.", "Rev C."));

        Assert.Equal(part.Id, thrown.ObjectId);
        Assert.Equal(first.CurrentRevisionNumber, thrown.SuccessorRevisionNumber);
    }

    /// <summary>
    /// The data loss the refusal prevents, stated in the terms the defect
    /// actually took: two successors of one predecessor, only one of them
    /// retired, and the un-retired one's accepted durable write silently
    /// discarded by the other.
    /// </summary>
    /// <remarks>
    /// Framed as "accepted implies durable" rather than "the second revise
    /// throws", so it pins the property and not the mechanism: a fix that
    /// serialised the two successors some other way would satisfy it too.
    /// </remarks>
    [Fact]
    public async Task ASecondSuccessor_CannotSilentlyDiscardTheFirstSuccessorsAcceptedDurableWrite()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var a = (Part)await part.ReviseAsync("Second content.", null);

        Part? b = null;
        try
        {
            b = (Part)await part.ReviseAsync("Third content.", null);
        }
        catch (SupersededEngineeringObjectException)
        {
            // The predecessor refuses to mint a second successor at all,
            // which is the strongest form of the property below.
        }

        var renamedThroughA = false;
        try
        {
            await a.RenameAsync("Named through successor A");
            renamedThroughA = true;
        }
        catch (SupersededEngineeringObjectException)
        {
            // Also sound: A was told its write did not land.
        }

        if (b is not null)
            await b.SetBomLineAsync(7m);

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);

        // A write the platform *accepted* must still be on disk. Accepting
        // it and then losing it is the whole defect.
        if (renamedThroughA)
            Assert.Equal("Named through successor A", state.DisplayName);
    }

    /// <summary>
    /// A refused revision must not leave a durable revision record behind
    /// either — the document store is asked for a new revision only after
    /// the refusal check, inside the same lock, so the two cannot disagree.
    /// </summary>
    /// <remarks>
    /// This is the "can a durable revision record be minted for a revision
    /// that must then be refused?" question the remediation brief asked to
    /// be handled or disclosed explicitly. It is handled: the check moved
    /// ahead of <c>IEngineeringDocumentStore.ReviseAsync</c>, so no orphan
    /// revision is written and the revision history a user reads never
    /// gains an entry for a revision that did not happen.
    /// </remarks>
    [Fact]
    public async Task ARefusedRevision_MintsNoDurableRevisionRecord()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        _ = await part.ReviseAsync("Second content.", null);

        var before = await part.GetRevisionHistoryAsync();

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.ReviseAsync("Third content.", null));

        var after = await part.GetRevisionHistoryAsync();

        Assert.Equal(2, before.Count);
        Assert.Equal(before.Count, after.Count);
        Assert.DoesNotContain(after, r => r.Content == "Third content.");
    }

    /// <summary>
    /// The ordinary chain still works: a successor may itself be revised,
    /// as many times as a caller likes, because each successor is its own
    /// unrevised instance.
    /// </summary>
    /// <remarks>
    /// A non-discriminating regression pin — it passed before this change
    /// too. It is here because the obvious wrong fix (a latch on the Id
    /// rather than on the instance) would break it, and every
    /// <c>Revise*Command</c> in <c>Tempest.App</c> depends on it.
    /// </remarks>
    [Fact]
    public async Task ARevisionChain_IsUnaffected()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var second = (Part)await part.ReviseAsync("Second.", null);
        var third = (Part)await second.ReviseAsync("Third.", null);
        var fourth = (Part)await third.ReviseAsync("Fourth.", null);

        Assert.Equal(4, fourth.CurrentRevisionNumber);

        await fourth.RenameAsync("From the live fourth instance");

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("From the live fourth instance", state.DisplayName);
    }

    /// <summary>
    /// And the ordinary application sequence — rename, then revise, on the
    /// instance the repository currently holds — is untouched. This is what
    /// <c>ProjectTaskService.EditAsync</c>, <c>ProjectMilestoneService</c>
    /// and <c>ProjectGovernanceService</c> actually do, and what
    /// `WP 16.4B-R3`/`R4`/`R5` established.
    /// </summary>
    [Fact]
    public async Task RenameThenRevise_ThenRenameThenReviseAgainOnTheLiveInstance_AllSucceed()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        await part.RenameAsync("Renamed once");
        var second = (Part)await part.ReviseAsync("Second.", null);

        Assert.Equal("Renamed once", second.DisplayName);

        // The caller re-fetches, exactly as every Revise*Command does.
        var live = (Part)(await context.Repository.FindAsync(part.Id))!;
        await live.RenameAsync("Renamed twice");
        var third = (Part)await live.ReviseAsync("Third.", null);

        Assert.Equal("Renamed twice", third.DisplayName);

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed twice", state.DisplayName);
    }

    private static EngineeringDomainContext BuildContext(IEngineeringObjectStateStore stateStore)
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

        return new EngineeringDomainContext(
            store, repository, relationshipRepository, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository), principalAccessor, stateStore);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private sealed class InMemoryObjectStateStore : IEngineeringObjectStateStore
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
}

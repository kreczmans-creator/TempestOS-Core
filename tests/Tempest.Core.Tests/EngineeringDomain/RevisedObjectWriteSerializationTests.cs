using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R4` — the second half of the lost-update fix, on the one
/// path `WP 16.4B-R3` did not route through the write lock.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IHasRevisions.ReviseAsync"/> builds a second, independently
/// mutable instance for the same Id and registers it in place of its
/// predecessor. `WP 16.4B-R3` keyed the durable-write lock by Id rather
/// than by instance precisely because of that, and said so in
/// <c>PersistStateAsync</c>'s own remarks — but it stopped at ordering
/// the two writers. Ordering is not enough. The successor is built from a
/// snapshot taken at revision time, so a predecessor write that lands
/// after that snapshot is invisible to it, and the successor's next
/// mutation overwrites the whole record and discards it.
/// </para>
/// <para>
/// The independent release review reproduced that against the real
/// classes: attach on a predecessor lands durably, an ordinary rename on
/// the live successor follows, and the attachment is gone from disk. With
/// real content attached, the write-intent marker has already been
/// cleared by the predecessor's own successful persist, so the
/// reconciliation sweep then sees content that is present, unmarked and
/// unreferenced, and deletes the file's bytes as a genuine orphan.
/// </para>
/// <para>
/// These tests pin both halves of the closure: a predecessor write that
/// arrives <em>after</em> the revision is refused rather than silently
/// lost, and one that arrives <em>before</em> it is carried into the
/// successor. The second matters as much as the first — a fix that
/// simply stopped predecessors writing at all would pass the first test
/// and quietly break ordinary edit-then-revise sequences, which is what
/// every <c>Revise*Command</c> in <c>Tempest.App</c> actually does.
/// </para>
/// </remarks>
public sealed class RevisedObjectWriteSerializationTests
{
    /// <summary>
    /// The review's own reproduction, made an assertion. Before
    /// `WP 16.4B-R4` this attach succeeded silently and its attachment was
    /// then destroyed by the next mutation on the successor; now the
    /// stale write is refused at the point it would have done the damage,
    /// having changed nothing on disk.
    /// </summary>
    [Fact]
    public async Task AWriteThroughAPredecessorAfterItsRevision_IsRefusedRatherThanSilentlyDiscarded()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var revised = (Part)await part.ReviseAsync("Revised content.", "Rev B.");

        var lost = new Attachment("drawing.pdf", "application/pdf", 3);

        var thrown = await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachAsync(lost));

        Assert.Equal(part.Id, thrown.ObjectId);
        Assert.Equal(revised.CurrentRevisionNumber, thrown.SuccessorRevisionNumber);

        // The refusal changed nothing on disk — this is a refusal, not a
        // partial write.
        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.DoesNotContain(state.Attachments, a => a.Id == lost.Id);
    }

    /// <summary>
    /// The data-loss assertion itself, stated in the terms the defect
    /// actually took: an ordinary mutation on the live successor must
    /// never be able to silently destroy a durable write that the
    /// platform reported as successful.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryMutationOnTheSuccessor_CannotSilentlyDestroyAnAcceptedDurableWrite()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var revised = (Part)await part.ReviseAsync("Revised content.", "Rev B.");

        var attachment = new Attachment("drawing.pdf", "application/pdf", 3);

        // Either this write is accepted, in which case it must survive the
        // rename below; or it is refused, in which case nothing was ever
        // reported as durable. Both are sound. Silently accepting it and
        // then losing it — the pre-fix behaviour — is what must not happen.
        var accepted = true;

        try
        {
            await part.AttachAsync(attachment);
        }
        catch (SupersededEngineeringObjectException)
        {
            accepted = false;
        }

        await revised.RenameAsync("Renamed Bracket");

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed Bracket", state.DisplayName);

        if (accepted)
            Assert.Contains(state.Attachments, a => a.Id == attachment.Id);
        else
            Assert.DoesNotContain(state.Attachments, a => a.Id == attachment.Id);
    }

    /// <summary>
    /// The other half: a write made <em>before</em> the revision is
    /// carried across it. This is the ordinary edit-then-revise sequence
    /// <c>ProjectMilestoneService</c> and <c>ProjectGovernanceService</c>
    /// both perform (rename, then revise), and it must keep working.
    /// </summary>
    [Fact]
    public async Task AWriteThroughAPredecessorBeforeItsRevision_IsCarriedIntoTheSuccessor()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var attachment = new Attachment("drawing.pdf", "application/pdf", 3);
        await part.AttachAsync(attachment);
        await part.RenameAsync("Renamed Before Revision");

        var revised = (Part)await part.ReviseAsync("Revised content.", "Rev B.");

        Assert.Equal("Renamed Before Revision", revised.DisplayName);
        Assert.Contains(await revised.GetAttachmentsAsync(), a => a.Id == attachment.Id);

        // And the successor's own next mutation preserves it durably.
        await revised.RenameAsync("Renamed After Revision");

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed After Revision", state.DisplayName);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);
    }

    /// <summary>
    /// A successor may itself be revised, and its own successor takes over
    /// the same way — the guard is per-instance, not a one-shot latch on
    /// the Id.
    /// </summary>
    [Fact]
    public async Task RevisingASuccessor_RetiresItInTurn()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var second = (Part)await part.ReviseAsync("Second.", null);
        var third = (Part)await second.ReviseAsync("Third.", null);

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => second.RenameAsync("From the retired second instance"));

        await third.RenameAsync("From the live third instance");

        var state = await stateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("From the live third instance", state.DisplayName);
    }

    /// <summary>
    /// Reading a retired instance stays legal — only durable writes are
    /// refused. `WP 9.0B`'s own
    /// <c>ReviseAsync_TheOriginalInstanceIsUnaffectedByTheNewRevisionsOwnFutureMutation</c>
    /// depends on exactly this, and so does every caller that keeps the
    /// predecessor's Id around for logging or a configuration member.
    /// </summary>
    [Fact]
    public async Task ARetiredInstanceRemainsReadable()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var revised = (Part)await part.ReviseAsync("Revised content.", null);
        await revised.RenameAsync("Renamed After Revision");

        Assert.Equal("Bracket", part.DisplayName);
        Assert.Equal(part.Id, revised.Id);
        Assert.Equal("PART-1", part.Identifier);
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
    /// A minimal in-memory state store — no gating needed here, because
    /// the defect this file pins is a sequential one: the revision, the
    /// predecessor's write and the successor's write happen in program
    /// order, and the loss came from a stale snapshot rather than from a
    /// race.
    /// </summary>
    private sealed class InMemoryObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            lock (_states)
            {
                _states[state.Id] = state;
            }

            return Task.CompletedTask;
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_states)
            {
                return Task.FromResult(_states.TryGetValue(id, out var state) ? state : null);
            }
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_states)
            {
                _states.Remove(id);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
        {
            lock (_states)
            {
                return Task.FromResult<IReadOnlyList<EngineeringObjectState>>(_states.Values.ToList());
            }
        }
    }
}

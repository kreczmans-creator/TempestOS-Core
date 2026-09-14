using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Adversarial facts about the write path, re-pointed at the transactional
/// store (`ADR-0145`, `WP 17.1B`).
/// </summary>
/// <remarks>
/// <para>
/// This file was written to falsify `WP 16.4B-R7`'s compensation: it
/// attacked the evidence comparison, the undo's fidelity, and the residues
/// the undo could not reach. Most of those attacks no longer have a target.
/// A mutator does not mutate and then undo; it projects, commits, and
/// applies. The evidence comparison, the rollback point and the write-intent
/// marker are deleted, so §B, §D's move fact and §D's attach-content fact —
/// each of which asserted a property <em>of the compensation</em> — were
/// deleted with them rather than rewritten into claims about machinery that
/// is not there.
/// </para>
/// <para>
/// What remains is the part that was always about the platform rather than
/// about the workaround: the whole-mutator sweep against the real durable
/// stack (§B), and the type-state divergence that `TD-142` disclosed (§C)
/// — which this Work Package closes, so that fact is inverted here rather
/// than deleted. §A, the file-per-key store's own `TD-59` encoding
/// behaviour, is deleted in turn by `WP 18.1A` (`ADR-0144`): the store it
/// was a property of no longer exists.
/// </para>
/// </remarks>
public sealed class R7FalsificationTests : IDisposable
{
    private static readonly byte[] Bytes = [1, 2, 3];

    private readonly List<DurableRig> _rigs = [];
    private readonly List<string> _roots = [];

    public void Dispose()
    {
        foreach (var rig in _rigs)
            rig.Dispose();

        foreach (var root in _roots)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // ================================================================
    // §A (deleted, `WP 18.1A`) — a surviving legacy-encoded record was
    // inert. That was a property of the file-per-key store's own `TD-59`
    // name encoding (a reserved-device-stem stem, a legacy-path fallback,
    // a best-effort forward migration on next write), never of the
    // engineering write path this suite otherwise tests. `ADR-0144`
    // deletes that store and every one of those mechanisms with it in
    // `v0.18.0`; there is no successor claim to invert, only one to
    // remove.
    // ================================================================

    // ================================================================
    // §B  Every mutator, in one sweep, against the real durable stack.
    // ================================================================

    /// <summary>
    /// Every mutator on <see cref="EngineeringObjectBase"/>, against the
    /// real durable stack with the commit failed deterministically: nothing
    /// is kept in memory, and the next successful write carries nothing.
    /// </summary>
    /// <remarks>
    /// The per-mutator, per-clause version of this is
    /// <see cref="R7RegressionProofTests"/> §1. This one is the sweep: it
    /// runs the whole set against one object in one sequence, so a mutator
    /// that leaks into a <em>later</em> mutator's write — rather than into
    /// its own — is caught here and nowhere else.
    /// </remarks>
    [Fact]
    public async Task EveryMutator_AgainstTheRealDurableStack_KeepsNothingWhenTheCommitFails()
    {
        var rig = NewRig("r7b-sweep");
        var part = await rig.CreatePartAsync("P-1", "Bracket");
        var parent = await rig.CreatePartAsync("P-2", "Assembly");

        var baseline = await rig.States.FindAsync(part.Id);
        Assert.NotNull(baseline);

        // 1 Transition
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.TransitionAsync(LifecycleState.InReview));
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);

        // 2 Rename
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.RenameAsync("Renamed"));
        Assert.Equal("Bracket", part.DisplayName);

        // 3 Move
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.MoveAsync(parent.Id));
        Assert.Null(part.ParentId);

        // 4 SetBomLine
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.SetBomLineAsync(42m, "ea"));
        Assert.Equal(1m, part.Quantity);

        // 5 Attach
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachAsync(new Attachment(Guid.NewGuid(), "a.pdf", "application/pdf", 3, null)));
        Assert.Empty(await part.GetAttachmentsAsync());

        // 6 AttachContent
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachContentAsync("b.pdf", "application/pdf", Bytes));
        Assert.Empty(await part.GetAttachmentsAsync());

        // 7 Delete
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.DeleteAsync());
        Assert.False(part.IsDeleted);

        // 8 Revise
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.ReviseAsync("A revision.", "R7"));
        Assert.Equal(1, part.CurrentRevisionNumber);

        // The durable record is byte-for-byte what it was before the sweep,
        // and the next successful write carries none of the eight with it.
        var afterSweep = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterSweep);
        Assert.Equal(Fingerprint(baseline), Fingerprint(afterSweep));

        await part.RenameAsync("Renamed properly");

        var afterWrite = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterWrite);
        Assert.Equal(Fingerprint(baseline with { DisplayName = "Renamed properly" }), Fingerprint(afterWrite));
    }

    // ================================================================
    // §C  Type-specific mutators — `TD-142`, inverted.
    // ================================================================

    /// <summary>
    /// A type-specific mutator whose commit fails keeps nothing either:
    /// the Kind's own field is left exactly as it was, and it agrees with
    /// disk (`TD-142`, closed by `WP 17.1B`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted from its predecessor.</b> `TD-142`
    /// disclosed that the type-specific mutators — <c>SetPriorityAsync</c>,
    /// <c>ChangeStatusAsync</c>, <c>ChangeWorkStateAsync</c> and the rest —
    /// wrote the Kind's own field <em>before</em> persisting, and were
    /// outside the base mutators' rollback unit, so a failed write left the
    /// instance holding a value that disk did not. The old fact asserted
    /// that divergence and then checked that a base mutator's undo neither
    /// repaired nor clobbered it.
    /// </para>
    /// <para>
    /// <c>MutateTypeStateAndPersistAsync</c> now projects the next type
    /// state, commits it in the same transaction as the object state, and
    /// applies it to the Kind's fields only afterwards. There is no
    /// divergence to preserve, so the fact asserts its absence — in memory
    /// and on disk, before and after a subsequent base mutation.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ATypeSpecificMutator_WhoseCommitFails_LeavesTheKindsOwnFieldUnchanged()
    {
        var rig = NewRig("r7b-typestate");
        var task = await rig.CreateTaskAsync("T-1", "Draw it");

        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => task.SetPriorityAsync(WorkPriority.High));

        // Memory did not move, and it agrees with disk.
        Assert.Equal(WorkPriority.Normal, task.Priority);
        Assert.Equal(nameof(WorkPriority.Normal), (await rig.States.FindAsync(task.Id))!.Type("Priority"));

        // A base mutator that fails afterwards leaves both alone too.
        rig.Store.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => task.RenameAsync("Renamed"));

        Assert.Equal("Draw it", task.DisplayName);
        Assert.Equal(WorkPriority.Normal, task.Priority);

        // And the mutator works when the commit lands, on both halves.
        await task.SetPriorityAsync(WorkPriority.High);
        Assert.Equal(WorkPriority.High, task.Priority);
        Assert.Equal(nameof(WorkPriority.High), (await rig.States.FindAsync(task.Id))!.Type("Priority"));
    }

    // ================================================================
    // Harnesses
    // ================================================================

    private string NewRoot(string label)
    {
        var root = ProjectFixtureRoot.NewIsolatedRoot(label);
        _roots.Add(root);
        return root;
    }

    private DurableRig NewRig(string label)
    {
        var rig = new DurableRig(NewRoot(label));
        _rigs.Add(rig);
        return rig;
    }

    private static string Fingerprint(EngineeringObjectState state) =>
        string.Join(
            " | ",
            $"status={state.Status}",
            $"name={state.DisplayName}",
            $"parent={state.ParentId}",
            $"deleted={state.IsDeleted}",
            $"bom={state.BomLine.Quantity}/{state.BomLine.UnitOfMeasure}",
            $"history=[{string.Join(", ", state.History.Select(h => $"{h.From}->{h.To}"))}]",
            $"attachments=[{string.Join(", ", state.Attachments.Select(a => $"{a.Id:N}:{a.FileName}"))}]");

    /// <summary>The real durable stack, with a deterministically failable commit.</summary>
    private sealed class DurableRig : IDisposable
    {
        private readonly SqlitePersistenceStore _sqlite;

        public DurableRig(string root)
        {
            _sqlite = new SqlitePersistenceStore(new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                    [new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, root)]))
                .Build());

            Store = new CommitFailingPersistenceStore(_sqlite);

            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(_sqlite, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            States = new EngineeringObjectStateStore(_sqlite);

            Context = new EngineeringDomainContext(
                Store, documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, States, new AttachmentContentStore(_sqlite));
        }

        public CommitFailingPersistenceStore Store { get; }

        public EngineeringDomainContext Context { get; }

        public EngineeringObjectStateStore States { get; }

        public void Dispose() => _sqlite.Dispose();

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");

        public async Task<EngineeringTask> CreateTaskAsync(string identifier, string displayName) =>
            (EngineeringTask)await new EngineeringObjectFactory<EngineeringTask>(
                    CanonicalObjectKinds.Task, Context,
                    (d, r) => new EngineeringTask(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");
    }
}

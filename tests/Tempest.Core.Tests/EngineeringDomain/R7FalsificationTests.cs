using System.Text.Json;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Adversarial tests for the undo-on-failure guarantee a mutating
/// <c>EngineeringObject</c> operation makes: when its durable write fails,
/// the in-memory object must end up exactly as it was before the call, the
/// evidence that decision turns on must be this object's own durable
/// record and not some other actor's write, and the guarantee must hold
/// across every mutator and at every persistence boundary — not only the
/// ones a happy-path test happens to exercise. Every failure is injected
/// deterministically against the real classes; nothing here is timed or
/// raced.
/// </summary>
/// <remarks>
/// Every fact below must stay green. This file previously also carried six
/// "characterisation" facts, each asserting a specific way the guarantee
/// above did not yet hold — kept passing on purpose, as a live pin on a
/// known, still-open defect, until the day the underlying defect was
/// fixed and the fact inverted into a guard-rail. That pattern hid a
/// standing defect list inside a suite that is supposed to be green:
/// removed here, on the standing rule that a passing test whose whole
/// purpose is documenting a bug belongs on the defect backlog, not in the
/// test suite. Any defect those six facts still pinned as of this
/// removal remains open and is tracked in <c>BACKLOG.md</c> instead.
/// </remarks>
public sealed class R7FalsificationTests : IDisposable
{
    private readonly List<string> _roots = new();

    private static readonly byte[] Bytes = [7, 8, 9];

    public void Dispose()
    {
        foreach (var root in _roots)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // ================================================================
    // §A  The store layer — a surviving legacy-encoded record is inert,
    //     and nothing fallible runs after the commit point.
    // ================================================================

    /// <summary>
    /// A legacy-encoded record that outlives the best-effort migration
    /// must never be read in preference to the current-encoding record, on
    /// either read overload, and must never double a listing.
    /// </summary>
    [Fact]
    public async Task ASurvivingLegacyRecord_IsNeverReadInPreferenceToTheCurrentEncodingRecord()
    {
        var root = NewRoot("r7b-legacy");
        var store = NewStore(root);

        // "CON" is a reserved Win32 device stem, so `TD-59`'s encoding puts
        // the current record at "%43ON" and the pre-`TD-59` legacy record at
        // "CON" — the only shape in which the two paths differ at all.
        await store.WriteAsync("coll", "CON", "current-value");

        var collectionDirectory = Path.Combine(root, "coll");
        var legacyPath = Path.Combine(collectionDirectory, "CON");
        var currentPath = Path.Combine(collectionDirectory, "%43ON");

        Assert.True(File.Exists(currentPath), "the current-encoding record should exist");

        // Simulate the residue `MigrateLegacyRecordAfterCommit` is now
        // allowed to leave behind.
        await File.WriteAllTextAsync(legacyPath, "STALE-LEGACY-VALUE");

        Assert.Equal("current-value", await store.ReadAsync("coll", "CON"));
        Assert.Equal("current-value", System.Text.Encoding.UTF8.GetString((await store.ReadBytesAsync("coll", "CON"))!));

        var keys = await store.ListKeysAsync("coll");
        Assert.Equal(["CON"], keys);

        // And the next successful write retries the removal, as claimed.
        await store.WriteAsync("coll", "CON", "second-value");
        Assert.False(File.Exists(legacyPath), "the next successful write should have retried the legacy removal");
        Assert.Equal("second-value", await store.ReadAsync("coll", "CON"));
    }

    // ================================================================
    // §B  The evidence check.
    // ================================================================

    /// <summary>
    /// A throw from the evidence step — the comparison, or the
    /// <c>CaptureState()</c> that feeds it — leaves the caller's real
    /// exception intact and still performs the undo.
    /// </summary>
    /// <remarks>
    /// <c>DurableRecordAlreadyShowsThisStateAsync</c> once guarded only
    /// <c>store.FindAsync</c>, leaving the comparison and
    /// <c>CaptureState()</c> outside that try/catch and inside
    /// <c>RollBackOnFailureAsync</c>'s catch — so a throw there replaced the
    /// caller's real exception with an <c>ArgumentNullException</c> AND
    /// skipped the undo (<c>TD-143</c>), and the object's next successful
    /// write then made the failed attach durable. Fixed by moving the
    /// comparison inside the guard.
    /// <para>
    /// <b>Mutant that kills it:</b> move that <c>return</c> line back
    /// outside the <c>try</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AThrowFromTheEvidenceComparison_LeavesTheRealFailureIntactAndStillUndoes()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        // A durable record whose History is absent — reachable against the
        // real store, since the shipped EngineeringObjectStateStore catches
        // only JsonException and History is an ordinary, non-required
        // constructor parameter on the durable record.
        rig.States.HollowOutHistoryOnRead = true;
        rig.States.FailNextSave();

        var attachment = new Attachment(Guid.NewGuid(), "spec.pdf", "application/pdf", 3, null);

        var thrown = await Record.ExceptionAsync(() => part.AttachAsync(attachment));

        // The caller sees the IOException the store raised, not the evidence
        // step's own fault.
        Assert.IsType<IOException>(thrown);
        Assert.Contains("could not be written", thrown!.Message, StringComparison.Ordinal);

        // And the mutation is gone from the instance, so the object's next
        // successful write of anything at all carries nothing.
        Assert.Empty(await part.GetAttachmentsAsync());

        rig.States.HollowOutHistoryOnRead = false;
        await part.RenameAsync("Renamed");
        Assert.Empty(rig.States.Peek(part.Id)!.Attachments);
    }

    // ================================================================
    // §C  Every persistence boundary — attack 5, against the REAL
    //     durable stack rather than an in-memory probe.
    // ================================================================

    /// <summary>
    /// All seven mutators, real <see cref="PersistenceStore"/>,
    /// real <see cref="EngineeringObjectStateStore"/>, with the underlying
    /// write failed deterministically at the persistence layer: nothing is
    /// kept in memory and the next successful write carries nothing.
    /// </summary>
    [Fact]
    public async Task AllSevenMutators_AgainstTheRealDurableStack_KeepNothingWhenTheWriteFails()
    {
        var fixture = NewFixture("r7b-seven");
        var part = await fixture.CreatePartAsync("P-1", "Bracket");
        var parent = await fixture.CreatePartAsync("P-2", "Assembly");

        var baseline = await fixture.States.FindAsync(part.Id);
        Assert.NotNull(baseline);

        // 1 Transition
        fixture.Failing.FailNextWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.TransitionAsync(LifecycleState.InReview));
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);

        // 2 Rename
        fixture.Failing.FailNextWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.RenameAsync("Renamed"));
        Assert.Equal("Bracket", part.DisplayName);

        // 3 Move
        fixture.Failing.FailNextWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.MoveAsync(parent.Id));
        Assert.Null(part.ParentId);

        // 4 SetBomLine
        fixture.Failing.FailNextWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.SetBomLineAsync(42m, "ea"));
        Assert.Equal(1m, part.Quantity);

        // 5 Attach
        fixture.Failing.FailNextWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachAsync(new Attachment(Guid.NewGuid(), "a.pdf", "application/pdf", 3, null)));
        Assert.Empty(await part.GetAttachmentsAsync());

        // 6 AttachContent
        fixture.Failing.FailNextStateWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachContentAsync("b.pdf", "application/pdf", Bytes));
        Assert.Empty(await part.GetAttachmentsAsync());

        // 7 Delete
        fixture.Failing.FailNextWrite();
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.DeleteAsync());
        Assert.False(part.IsDeleted);

        // The durable record is byte-for-byte what it was, and a later
        // successful write carries none of the seven.
        await part.RenameAsync("Finally");

        var after = await fixture.States.FindAsync(part.Id);
        Assert.NotNull(after);
        Assert.Equal("Finally", after!.DisplayName);
        Assert.Equal(LifecycleState.Draft, after.Status);
        Assert.Empty(after.History);
        Assert.Empty(after.Attachments);
        Assert.Null(after.ParentId);
        Assert.False(after.IsDeleted);
        Assert.Equal(1m, after.BomLine.Quantity);
    }

    /// <summary>
    /// The `TD-140` supersession refusal must not regress, and
    /// must not pay for the new evidence read: a retired instance refuses
    /// BEFORE the rollback machinery is entered, so no durable read happens.
    /// </summary>
    [Fact]
    public async Task TheSupersessionRefusal_StillRefusesBeforeAnythingIsTouched_AndCostsNoDurableRead()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        await part.ReviseAsync("Revised.", "because");

        rig.States.Reads = 0;

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.RenameAsync("Renamed"));
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.TransitionAsync(LifecycleState.InReview));
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.DeleteAsync());
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.SetBomLineAsync(5m));
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachAsync(new Attachment(Guid.NewGuid(), "a.pdf", "application/pdf", 3, null)));

        Assert.Equal("Bracket", part.DisplayName);
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.False(part.IsDeleted);
        Assert.Equal(0, rig.States.Reads);
    }

    /// <summary>
    /// Repeated failed mutations leave no accumulation, and the
    /// first success carries only itself.
    /// </summary>
    [Fact]
    public async Task RepeatedFailedMutations_AccumulateNothing()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            rig.States.FailNextSave();
            await Assert.ThrowsAsync<IOException>(() => part.TransitionAsync(LifecycleState.InReview));
        }

        Assert.Empty(part.History);

        await part.TransitionAsync(LifecycleState.InReview);

        Assert.Single(part.History);
        Assert.Single(rig.States.Peek(part.Id)!.History);
    }

    /// <summary>
    /// The `TD-142` type-specific mutators still keep a mutation
    /// whose write failed — unchanged and disclosed — and a BASE mutator's
    /// undo neither repairs nor clobbers that divergence: the rollback point
    /// and the evidence comparison both stop at the base fields, exactly as
    /// documented.
    /// </summary>
    [Fact]
    public async Task ABaseMutatorsUndo_WorksWhileTypeStateIsDiverged_AndNeitherRepairsNorClobbersIt()
    {
        var rig = new Rig();
        var task = await rig.CreateTaskAsync("T-1", "Draw it");

        // TD-142, untouched: the failed write keeps the type-specific field.
        rig.States.FailNextSave();
        await Assert.ThrowsAsync<IOException>(() => task.SetPriorityAsync(WorkPriority.High));
        Assert.Equal(WorkPriority.High, task.Priority);
        Assert.Equal(nameof(WorkPriority.Normal), rig.States.Peek(task.Id)!.Type("Priority"));

        // With TypeState now diverged from disk, a base mutator's undo still
        // fires (the comparison ignores TypeState, so the divergence neither
        // suppresses nor forces it) and leaves TypeState alone.
        rig.States.FailNextSave();
        await Assert.ThrowsAsync<IOException>(() => task.RenameAsync("Renamed"));

        Assert.Equal("Draw it", task.DisplayName);
        Assert.Equal(WorkPriority.High, task.Priority);
    }

    // ================================================================
    // §D  The individual mutators.
    // ================================================================

    /// <summary>
    /// <c>MoveAsync</c>: a failing LINK write leaves no reparent
    /// and no link; a link that succeeds followed by a failing STATE write
    /// leaves no reparent but DOES leave the `groupedUnder` edge — a
    /// disclosed residue, pinned here independently.
    /// </summary>
    [Fact]
    public async Task MoveAsync_UndoesTheReparentOnBothDurableSteps_ButNeverRemovesTheLink()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");
        var parent = await rig.CreatePartAsync("P-2", "Assembly");

        rig.Documents.FailNextLink();
        await Assert.ThrowsAsync<IOException>(() => part.MoveAsync(parent.Id));
        Assert.Null(part.ParentId);
        Assert.Equal(0, rig.Documents.Links);

        rig.States.FailNextSave();
        await Assert.ThrowsAsync<IOException>(() => part.MoveAsync(parent.Id));
        Assert.Null(part.ParentId);
        Assert.Null(rig.States.Peek(part.Id)!.ParentId);
        Assert.Equal(1, rig.Documents.Links);
    }

    /// <summary>
    /// <c>AttachContentAsync</c>: the in-memory add is undone,
    /// the bytes and the write-intent marker are deliberately not — the
    /// disclosed `TD-97` residue, and the ONLY channel it now shows in.
    /// </summary>
    [Fact]
    public async Task AttachContentAsync_UndoesTheInstanceClaim_AndLeavesTheBytesAndTheMarker()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        rig.States.FailNextSave();
        await Assert.ThrowsAsync<IOException>(() => part.AttachContentAsync("a.pdf", "application/pdf", Bytes));

        Assert.Empty(await part.GetAttachmentsAsync());
        Assert.Empty(rig.States.Peek(part.Id)!.Attachments);
        Assert.Single(rig.Content.StoredKeys);
        Assert.Single(rig.WriteIntents.Marked);
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

    private static PersistenceStore NewStore(string root) =>
        new(new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root)]))
            .Build());

    private DurableRig NewFixture(string label) => new(NewRoot(label));

    /// <summary>The real durable stack, with a persistence store that can be made to fail exactly one write.</summary>
    private sealed class DurableRig
    {
        public DurableRig(string root)
        {
            Failing = new FailingPersistenceStore(NewStore(root));
            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(Failing, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);
            States = new EngineeringObjectStateStore(Failing);
            var content = new AttachmentContentStore(Failing);

            Context = new EngineeringDomainContext(
                documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, States, content);
        }

        public FailingPersistenceStore Failing { get; }
        public EngineeringDomainContext Context { get; }
        public EngineeringObjectStateStore States { get; }

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");
    }

    /// <summary>
    /// A real <see cref="PersistenceStore"/> with a deterministic one-shot
    /// failure on the next write — the ordinary durable-store failure, raised
    /// BEFORE anything is committed, which is the only shape the shipped
    /// store can now produce.
    /// </summary>
    private sealed class FailingPersistenceStore(PersistenceStore inner) : IPersistenceStore, IBinaryPersistenceStore
    {
        private bool _failNext;
        private bool _failNextState;

        public void FailNextWrite() => _failNext = true;

        public void FailNextStateWrite() => _failNextState = true;

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(collection, key, cancellationToken);

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
        {
            if (_failNext || (_failNextState && collection == EngineeringObjectStateStore.StateCollectionName))
            {
                _failNext = false;
                _failNextState = false;
                throw new PersistenceStoreUnavailableException($"Injected failure writing '{collection}'/'{key}'.");
            }

            return inner.WriteAsync(collection, key, value, cancellationToken);
        }

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(collection, key, cancellationToken);

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
            inner.ListKeysAsync(collection, cancellationToken);

        public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.ReadBytesAsync(collection, key, cancellationToken);

        public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            if (_failNext)
            {
                _failNext = false;
                throw new PersistenceStoreUnavailableException($"Injected failure writing bytes '{collection}'/'{key}'.");
            }

            return inner.WriteBytesAsync(collection, key, value, cancellationToken);
        }
    }

    /// <summary>The in-memory rig, for the interleavings the real stack cannot express.</summary>
    private sealed class Rig
    {
        public Rig()
        {
            var principal = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);
            Documents = new LinkFailingDocumentStore(new InMemoryEngineeringDocumentStore(principal));

            Context = new EngineeringDomainContext(
                Documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, States, Content, WriteIntents);
        }

        public EngineeringDomainContext Context { get; }
        public LinkFailingDocumentStore Documents { get; }
        public HostileStateStore States { get; } = new();
        public FailingContentStore Content { get; } = new();
        public MarkerStore WriteIntents { get; } = new();

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");

        public async Task<EngineeringTask> CreateTaskAsync(string identifier, string displayName) =>
            (EngineeringTask)await new EngineeringObjectFactory<EngineeringTask>(
                    "Task", Context,
                    (d, r) => new EngineeringTask(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");
    }

    private sealed class HostileStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();
        private readonly Dictionary<Guid, EngineeringObjectState> _previous = new();
        private bool _failNext;
        private bool _commitThenFailNext;

        public int Reads { get; set; }

        /// <summary>Reads answer with the record as it was BEFORE the last write — a plausible read cache, and perfectly readable.</summary>
        public bool StaleReads { get; set; }

        /// <summary>Reads answer with a record whose <c>History</c> is absent, as a durable record with no such property deserialises.</summary>
        public bool HollowOutHistoryOnRead { get; set; }

        public void FailNextSave() => _failNext = true;

        public void CommitThenFailNextSave() => _commitThenFailNext = true;

        public EngineeringObjectState? Peek(Guid id)
        {
            lock (_states) { return _states.TryGetValue(id, out var state) ? state : null; }
        }

        /// <summary>A writer outside this object's write lock lands a record, and this write then fails.</summary>
        public Func<EngineeringObjectState, EngineeringObjectState>? ForeignWriteBeforeFailingNextSave { get; set; }

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            if (ForeignWriteBeforeFailingNextSave is { } foreign)
            {
                ForeignWriteBeforeFailingNextSave = null;
                lock (_states)
                {
                    var existing = _states.TryGetValue(state.Id, out var found) ? found : state;
                    _states[state.Id] = foreign(existing);
                }

                throw new IOException("The state record could not be written.");
            }

            if (_failNext)
            {
                _failNext = false;
                throw new IOException("The state record could not be written.");
            }

            lock (_states)
            {
                if (_states.TryGetValue(state.Id, out var existing))
                    _previous[state.Id] = existing;

                _states[state.Id] = state;
            }

            if (_commitThenFailNext)
            {
                _commitThenFailNext = false;
                throw new IOException("The state record landed, and the write then failed anyway.");
            }

            return Task.CompletedTask;
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;

            EngineeringObjectState? state;
            lock (_states)
            {
                var source = StaleReads ? _previous : _states;
                state = source.TryGetValue(id, out var found) ? found : null;
            }

            if (state is not null && HollowOutHistoryOnRead)
                state = state with { History = null! };

            return Task.FromResult(state);
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

    private sealed class FailingContentStore : IAttachmentContentStore
    {
        private readonly Dictionary<Guid, byte[]> _content = new();
        private bool _failNextDelete;

        public void FailNextDelete() => _failNextDelete = true;

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
            if (_failNextDelete)
            {
                _failNextDelete = false;
                throw new IOException("The attachment bytes could not be released.");
            }

            lock (_content) { _content.Remove(attachmentId); }
            return Task.CompletedTask;
        }
    }

    private sealed class MarkerStore : IAttachmentWriteIntentStore
    {
        private readonly HashSet<Guid> _marked = new();

        public IReadOnlyCollection<Guid> Marked
        {
            get { lock (_marked) { return _marked.ToList(); } }
        }

        public Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
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
            lock (_marked) { return Task.FromResult<IReadOnlySet<Guid>>(_marked.ToHashSet()); }
        }
    }

    private sealed class LinkFailingDocumentStore(InMemoryEngineeringDocumentStore inner) : IEngineeringDocumentStore
    {
        private bool _failNextLink;

        public int Links { get; private set; }

        public void FailNextLink() => _failNextLink = true;

        public Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(kind, initialContent, cancellationToken);

        public Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            inner.FindAsync(documentId, cancellationToken);

        public Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default) =>
            inner.ReviseAsync(documentId, newContent, changeSummary, cancellationToken);

        public Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            inner.GetRevisionHistoryAsync(documentId, cancellationToken);

        public Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default)
        {
            if (_failNextLink)
            {
                _failNextLink = false;
                throw new IOException("The relationship could not be written.");
            }

            Links++;
            return inner.LinkAsync(sourceDocumentId, targetDocumentId, relationshipKind, cancellationToken);
        }

        public Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            inner.GetReferencesAsync(documentId, cancellationToken);
    }
}

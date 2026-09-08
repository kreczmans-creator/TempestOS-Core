using Tempest.Workspace.Mechanical;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Executable regression proof for the invariant `TD-143` named, re-pointed
/// at the transactional write path (`ADR-0145`, `WP 17.1B`).
/// </summary>
/// <remarks>
/// <para>
/// The invariant, which every fact in §1 checks in full, is word for word
/// the one this file has always asserted: <em>if a mutator reports failure
/// because its durable write did not complete, it leaves no durable state,
/// no in-memory state and no audit evidence representing the operation as
/// having happened — including on this object's next successful
/// write.</em>
/// </para>
/// <para>
/// <b>What changed is the mechanism underneath, and therefore what a fact
/// can honestly inject.</b> Before `ADR-0145` a mutator wrote memory
/// first, then disk, and a compensating undo — <c>RollBackOnFailureAsync</c>,
/// <c>MutationRollbackPoint</c>, <c>DurableRecordAlreadyShowsThisStateAsync</c>
/// — tried to put memory back when the disk write threw. The invariant
/// held only as well as that undo did, so this file's older facts injected
/// failures at individual stores and pinned individual terms of the
/// evidence comparison the undo turned on.
/// </para>
/// <para>
/// There is now one transaction. A mutator computes its next state,
/// commits it, and touches memory only afterwards. The failure worth
/// injecting is therefore the <b>commit</b>, and there is exactly one
/// place to inject it: <see cref="CommitFailingPersistenceStore"/> lets
/// the whole body run — object state, document records, revisions,
/// references, attachment bytes and the audit row all staged — and then
/// fails on the way out. That is the strongest form of the test, because
/// every write the mutation wanted to make has been made and the only
/// thing that did not happen is the commit.
/// </para>
/// <para>
/// <b>Facts deleted rather than re-pointed, and why.</b> §2's durable-read
/// cost accounting, §3's seven-term theory over
/// <c>HoldsTheSameMutableState</c> and §4's three undo-fidelity facts all
/// pinned decisions <em>inside</em> the compensation. The compensation is
/// gone: a mutator now reads no durable record to decide whether to undo,
/// because it never mutated anything to undo. Those facts had no
/// behaviour left to describe and were deleted rather than rewritten into
/// assertions about a mechanism that no longer exists. The
/// <em>invariant</em> they protected is unchanged and is asserted here,
/// per mutator, in full.
/// </para>
/// <para>
/// §5's characterisation fact carried a standing instruction — "when this
/// is closed, INVERT the assertions, do not delete the fact". `TD-147` is
/// closed by this Work Package, and §4 below is that fact inverted.
/// </para>
/// <para>
/// The stack is the real one: <see cref="SqlitePersistenceStore"/>,
/// <see cref="EngineeringDocumentStore"/>,
/// <see cref="EngineeringObjectStateStore"/> and
/// <see cref="AttachmentContentStore"/> over a temporary root. SQLite and
/// not the file-per-key store deliberately — the file store's
/// <c>ExecuteInTransactionAsync</c> is a bare sequence of writes with no
/// mechanism capable of being atomic, and `WP 17.1B` is built on the
/// backend that has one. Every failure is deterministically injected.
/// Nothing is timed and nothing is raced.
/// </para>
/// </remarks>
public sealed class R7RegressionProofTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly byte[] Bytes = [4, 5, 6, 7];

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
    // §1 THE INVARIANT, PER MUTATOR, PER CLAUSE — against the real
    //    durable stack, with the COMMIT failed deterministically.
    //
    //    Each fact establishes, in this order:
    //      (a) the caller is told the operation failed, with the real
    //          exception;
    //      (b) the instance carries no trace of it;
    //      (c) the DURABLE RECORD, read immediately and before any other
    //          write, carries no trace of it — including the audit rows,
    //          which still hold exactly what the object had before;
    //      (d) the object's NEXT SUCCESSFUL WRITE — of a different field,
    //          so it cannot mask the one under test — carries nothing of
    //          the failed operation to disk;
    //      (e) the object is still usable: the same mutator, retried
    //          against a healthy store, works.
    // ================================================================

    /// <summary>1 of 8 — <c>TransitionAsync</c>. The audit clause in full.</summary>
    /// <remarks>
    /// The highest-consequence mutator: `TD-143`'s headline product harm is
    /// a <c>LifecycleTransitionRecord</c> with a real actor principal id
    /// standing in an append-only governance record for a transition the
    /// caller was told had failed, with no removal path anywhere. Under
    /// `ADR-0145` that record and the audit row for it are in the same
    /// transaction as the state that carries them, so "the caller was told
    /// it failed" and "the record exists" cannot both be true.
    /// </remarks>
    [Fact]
    public async Task TransitionAsync_WhoseCommitFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-transition");
        var baseline = await ReadRecordAsync(rig, part);
        var auditBefore = await rig.AuditRowsForAsync(part.Id);

        rig.Store.FailNextCommit = true;
        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Approved).WaitAsync(Timeout));

        // (a)
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        // (b)
        Assert.Equal(LifecycleState.InReview, part.Status);
        Assert.Single(part.History);

        // (c) — read before anything else is written.
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed transition");
        Assert.Equal(auditBefore.Count, (await rig.AuditRowsForAsync(part.Id)).Count);

        // (d) — a DIFFERENT field is written successfully.
        await part.RenameAsync("Renamed after the failed transition").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed transition" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        // (e)
        await part.TransitionAsync(LifecycleState.Approved).WaitAsync(Timeout);
        var retried = await ReadRecordAsync(rig, part);
        Assert.Equal(LifecycleState.Approved, retried.Status);
        Assert.Equal(2, retried.History.Count);
        Assert.Equal(LifecycleState.InReview, retried.History[1].From);
        Assert.Equal(LifecycleState.Approved, retried.History[1].To);
        Assert.False(string.IsNullOrWhiteSpace(retried.History[1].ActorPrincipalId));
    }

    /// <summary>2 of 8 — <c>RenameAsync</c>.</summary>
    /// <remarks>
    /// The follow-up write is <c>SetBomLineAsync</c> and not another rename,
    /// deliberately: a second rename would overwrite <c>DisplayName</c> and
    /// so could not distinguish a rename that never landed from a surviving
    /// one that was simply written over.
    /// </remarks>
    [Fact]
    public async Task RenameAsync_WhoseCommitFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-rename");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.RenameAsync("Leaked name").WaitAsync(Timeout)));

        Assert.Equal("Bracket", part.DisplayName);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed rename");

        await part.SetBomLineAsync(3m, "each").WaitAsync(Timeout);
        var after = await ReadRecordAsync(rig, part);
        Assert.Equal("Bracket", after.DisplayName);

        await part.RenameAsync("Renamed properly").WaitAsync(Timeout);
        Assert.Equal("Renamed properly", (await ReadRecordAsync(rig, part)).DisplayName);
    }

    /// <summary>3 of 8 — <c>MoveAsync</c>. Both halves, together.</summary>
    /// <remarks>
    /// <b>This fact inverts the old one.</b> Its predecessor asserted a
    /// disclosed residue: <c>MoveAsync</c> wrote the <c>groupedUnder</c>
    /// edge and then the object state as two durable steps, so a failure
    /// between them left the edge behind for good — "nothing in this
    /// platform removes a relationship" made that residue permanent. The
    /// edge and the state are now one transaction, so a failed move leaves
    /// neither, and there is nothing to remove because nothing was written.
    /// </remarks>
    [Fact]
    public async Task MoveAsync_WhoseCommitFails_LeavesNeitherDurableStateNorTheEdge()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-move");
        var parent = await rig.CreatePartAsync("PRT-P", "Assembly");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.MoveAsync(parent.Id).WaitAsync(Timeout)));

        Assert.Null(part.ParentId);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed move");

        // The edge that used to survive as disclosed residue.
        Assert.DoesNotContain(
            await part.GetRelationshipsAsync(),
            e => e.TargetId == parent.Id && e.RelationshipKind == "groupedUnder");

        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed move" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        await part.MoveAsync(parent.Id).WaitAsync(Timeout);
        Assert.Equal(parent.Id, (await ReadRecordAsync(rig, part)).ParentId);
    }

    /// <summary>4 of 8 — <c>DeleteAsync</c>.</summary>
    [Fact]
    public async Task DeleteAsync_WhoseCommitFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-delete");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.DeleteAsync().WaitAsync(Timeout)));

        Assert.False(part.IsDeleted);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed delete");

        await part.RenameAsync("Renamed after the failed delete").WaitAsync(Timeout);
        var after = await ReadRecordAsync(rig, part);
        Assert.False(after.IsDeleted);

        await part.DeleteAsync().WaitAsync(Timeout);
        Assert.True((await ReadRecordAsync(rig, part)).IsDeleted);
    }

    /// <summary>5 of 8 — <c>SetBomLineAsync</c>.</summary>
    [Fact]
    public async Task SetBomLineAsync_WhoseCommitFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-bom");
        await part.SetBomLineAsync(1m, "each", "FN-1", "IT-1", "RD-1").WaitAsync(Timeout);
        var baseline = await ReadRecordAsync(rig, part);

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.SetBomLineAsync(9m, "kg", "FN-9", "IT-9", "RD-9").WaitAsync(Timeout)));

        Assert.Equal(1m, part.Quantity);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed BOM write");

        await part.RenameAsync("Renamed after the failed BOM write").WaitAsync(Timeout);
        Assert.Equal(1m, (await ReadRecordAsync(rig, part)).BomLine.Quantity);

        await part.SetBomLineAsync(9m, "kg", "FN-9", "IT-9", "RD-9").WaitAsync(Timeout);
        Assert.Equal(9m, (await ReadRecordAsync(rig, part)).BomLine.Quantity);
    }

    /// <summary>6 of 8 — <c>AttachAsync</c>, metadata only.</summary>
    [Fact]
    public async Task AttachAsync_WhoseCommitFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-attach");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.AttachAsync(new Attachment("leaked.txt", "text/plain", 3)).WaitAsync(Timeout)));

        Assert.Empty(await part.GetAttachmentsAsync());
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed attach");

        await part.RenameAsync("Renamed after the failed attach").WaitAsync(Timeout);
        Assert.Empty((await ReadRecordAsync(rig, part)).Attachments);

        await part.AttachAsync(new Attachment("real.txt", "text/plain", 3)).WaitAsync(Timeout);
        Assert.Single((await ReadRecordAsync(rig, part)).Attachments);
    }

    /// <summary>
    /// 7 of 8 — <c>AttachContentAsync</c>. The metadata row and the bytes,
    /// together (`WP 17.1B`, binary payload consistency).
    /// </summary>
    /// <remarks>
    /// <b>The clause about the bytes is new and is the point of the
    /// amendment.</b> Attachment content used to be written to its own
    /// store before the object state that named it, so a failure between
    /// the two left bytes on disk that no committed record referenced —
    /// which is what the write-intent marker and the reconciliation sweep
    /// existed to find and remove. The bytes are now a BLOB written through
    /// the same transaction as the row that references them, so this fact
    /// can assert the thing the marker could only approximate: after a
    /// failed commit there is no row <em>and</em> no payload.
    /// </remarks>
    [Fact]
    public async Task AttachContentAsync_WhoseCommitFails_LeavesNeitherTheRowNorTheBytes()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-attach-content");
        var baseline = await ReadRecordAsync(rig, part);
        var bytesBefore = await rig.AttachmentContentKeysAsync();

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.AttachContentAsync("leaked.pdf", "application/pdf", Bytes).WaitAsync(Timeout)));

        Assert.Empty(await part.GetAttachmentsAsync());
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed attach-content");

        // No orphaned payload — the failure the reconciliation sweep was for.
        Assert.Equal(bytesBefore, await rig.AttachmentContentKeysAsync());

        await part.RenameAsync("Renamed after the failed attach-content").WaitAsync(Timeout);
        Assert.Empty((await ReadRecordAsync(rig, part)).Attachments);
        Assert.Equal(bytesBefore, await rig.AttachmentContentKeysAsync());

        var attachment = await part.AttachContentAsync("real.pdf", "application/pdf", Bytes).WaitAsync(Timeout);
        Assert.Single((await ReadRecordAsync(rig, part)).Attachments);
        Assert.Equal(Bytes, (await rig.Content.ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes)).Bytes);
    }

    /// <summary>8 of 8 — <c>ReviseAsync</c>.</summary>
    /// <remarks>
    /// The revision and the document record naming it are one write in one
    /// transaction, so the ordering note this file's predecessor carried —
    /// "a crash between the two leaves an orphaned revision" — no longer
    /// describes a reachable state.
    /// </remarks>
    [Fact]
    public async Task ReviseAsync_WhoseCommitFails_LeavesNoNewRevision_AndTheInstanceIsNotRetired()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-revise");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.ReviseAsync("A revision.", "R7").WaitAsync(Timeout)));

        Assert.Equal(1, part.CurrentRevisionNumber);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed revision");

        // The instance was not retired by a revision that did not happen.
        await part.RenameAsync("Renamed after the failed revision").WaitAsync(Timeout);
        Assert.Equal("Renamed after the failed revision", (await ReadRecordAsync(rig, part)).DisplayName);

        var successor = await part.ReviseAsync("A revision.", "R7").WaitAsync(Timeout);
        Assert.Equal(2, ((IEngineeringObject)successor).CurrentRevisionNumber);
    }

    // ================================================================
    // §2 REFUSALS — a refusal is not an operation
    // ================================================================

    /// <summary>
    /// The supersession refusal is adjudicated inside the transaction,
    /// before anything is written, so a retired instance mutates nothing
    /// and leaves nothing durable.
    /// </summary>
    /// <remarks>
    /// The ordering claim this replaces was "before the rollback unit is
    /// entered", measured by counting durable reads. There is no rollback
    /// unit and no evidence read to count; the claim that survives is the
    /// one that always mattered — the refusal happens under the same lock
    /// and inside the same transaction that would have committed the
    /// write, so no interleaving exists in which a retired instance's
    /// mutation lands.
    /// </remarks>
    [Fact]
    public async Task TheSupersessionRefusal_RefusesInsideTheTransaction_AndWritesNothing()
    {
        var rig = NewRig("r7c-superseded");
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        await part.ReviseAsync("A revision, for test purposes.", "R7").WaitAsync(Timeout);

        var baseline = await ReadRecordAsync(rig, part);
        var commitsBefore = rig.Store.BodiesCompleted;

        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.RenameAsync("Leaked name").WaitAsync(Timeout)));
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.MoveAsync(Guid.NewGuid()).WaitAsync(Timeout)));
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.AttachContentAsync("x.pdf", "application/pdf", Bytes).WaitAsync(Timeout)));

        Assert.Equal("Bracket", part.DisplayName);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "after three refused mutations");

        // No transaction body ran to completion: each refusal threw before
        // reaching the end of the unit of work, so nothing was staged.
        Assert.Equal(commitsBefore, rig.Store.BodiesCompleted);
    }

    /// <summary>
    /// Repeated failed mutations accumulate nothing, in memory or on disk.
    /// </summary>
    [Fact]
    public async Task RepeatedFailedMutations_AccumulateNothing()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-repeated");
        var baseline = await ReadRecordAsync(rig, part);
        var auditBefore = (await rig.AuditRowsForAsync(part.Id)).Count;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            rig.Store.FailNextCommit = true;
            Assert.IsType<PersistenceStoreUnavailableException>(
                await Record.ExceptionAsync(() => part.RenameAsync($"Leaked {attempt}").WaitAsync(Timeout)));
        }

        Assert.Equal("Bracket", part.DisplayName);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "after ten failed renames");
        Assert.Equal(auditBefore, (await rig.AuditRowsForAsync(part.Id)).Count);
    }

    // ================================================================
    // §3 CREATION — `TD-147`, inverted
    // ================================================================

    /// <summary>
    /// A creation whose commit fails registers nothing and leaves nothing
    /// durable (`TD-147`, closed by `WP 17.1B`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is the inversion its predecessor asked for.</b> It used
    /// to read <c>ACreateWhoseInitialStateWriteFails_StillRegistersTheObject_AndItsNextWriteMakesItDurable</c>
    /// and it characterised a defect: <c>CreateAsync</c> registered the
    /// instance in the repository and only then wrote its initial state, so
    /// a failed creation left a live, mutable, findable object that the
    /// caller had never received, whose next successful write of anything
    /// at all made a creation reported as failed into a durable one. The
    /// fact carried a standing instruction to invert rather than delete it
    /// when the defect closed. `ADR-0145` closes it — the document, the
    /// revision, the state record and the creation audit row are one
    /// transaction, and <c>Register</c> is called after it commits — so the
    /// assertions are inverted here and the fact keeps its history.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ACreateWhoseCommitFails_RegistersNothing_AndLeavesNothingDurable()
    {
        var rig = NewRig("r7c-create");

        rig.Store.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => rig.CreatePartAsync("PRT-1", "Bracket")));

        // The repository is empty: the instance never reached it.
        Assert.Empty(await rig.Context.Repository.ListAllAsync());

        // And nothing durable represents it — not the state record, and not
        // the document the old shape had already written by this point.
        Assert.Empty(await rig.States.ListAsync());
        Assert.Empty(await rig.Raw.ListKeysAsync(EngineeringDocumentStore.DocumentsCollectionName, string.Empty));
        Assert.Empty(await rig.Raw.ListKeysAsync(EngineeringDocumentStore.RevisionsCollectionName, string.Empty));
        Assert.Empty(await rig.Raw.ListKeysAsync(AuditRecorder.AuditCollectionName, string.Empty));

        // A retry against a healthy store works, and is the first object.
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        Assert.Single(await rig.Context.Repository.ListAllAsync());
        Assert.NotNull(await rig.States.FindAsync(part.Id));
    }

    // ================================================================
    // Harnesses
    // ================================================================

    private DurableRig NewRig(string label)
    {
        var root = ProjectFixtureRoot.NewIsolatedRoot(label);
        _roots.Add(root);

        var rig = new DurableRig(root);
        _rigs.Add(rig);
        return rig;
    }

    private async Task<(DurableRig Rig, Part Part)> NewPartWithOneGenuineAuditEntryAsync(string label)
    {
        var rig = NewRig(label);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        // A real, durable transition, so that "no evidence for the failed
        // operation" is distinguishable from "an empty history".
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        return (rig, part);
    }

    private static async Task<EngineeringObjectState> ReadRecordAsync(DurableRig rig, IEngineeringObject part)
    {
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        return state;
    }

    /// <summary>
    /// Compares two durable records over every field any mutator can
    /// change, including the history entry by entry and the attachment
    /// metadata entry by entry.
    /// </summary>
    private static void AssertRecordIsExactly(EngineeringObjectState expected, EngineeringObjectState actual, string when) =>
        Assert.Equal($"{when}: {Fingerprint(expected)}", $"{when}: {Fingerprint(actual)}");

    private static string Fingerprint(EngineeringObjectState state) =>
        string.Join(
            " | ",
            $"status={state.Status}",
            $"name={state.DisplayName}",
            $"parent={state.ParentId}",
            $"deleted={state.IsDeleted}",
            $"bom={state.BomLine.Quantity}/{state.BomLine.UnitOfMeasure}/{state.BomLine.FindNumber}/" +
            $"{state.BomLine.ItemNumber}/{state.BomLine.ReferenceDesignator}",
            $"history=[{string.Join(", ", state.History.Select(h => $"{h.From}->{h.To} by {h.ActorPrincipalId}"))}]",
            $"attachments=[{string.Join(", ", state.Attachments.Select(a => $"{a.Id:N}:{a.FileName}:{a.SizeInBytes}"))}]");

    /// <summary>The real durable stack, with a deterministically failable commit.</summary>
    private sealed class DurableRig : IDisposable
    {
        private readonly SqlitePersistenceStore _sqlite;

        public DurableRig(string root)
        {
            _sqlite = new SqlitePersistenceStore(new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                    [new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root)]))
                .Build());

            Store = new CommitFailingPersistenceStore(_sqlite);

            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(_sqlite, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            States = new EngineeringObjectStateStore(_sqlite);
            Content = new AttachmentContentStore(_sqlite);

            Context = new EngineeringDomainContext(
                Store, documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, States, Content);
        }

        public CommitFailingPersistenceStore Store { get; }

        public IQueryablePersistenceStore Raw => _sqlite;

        public EngineeringDomainContext Context { get; }

        public EngineeringObjectStateStore States { get; }

        public AttachmentContentStore Content { get; }

        public void Dispose() => _sqlite.Dispose();

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");

        /// <summary>
        /// The audit rows written for one object, found by prefix rather
        /// than by scanning the collection (`ADR-0145`).
        /// </summary>
        public Task<IReadOnlyList<string>> AuditRowsForAsync(Guid objectId) =>
            _sqlite.ListKeysAsync(AuditRecorder.AuditCollectionName, objectId.ToString("N"));

        /// <summary>Every attachment payload key currently on disk.</summary>
        public Task<IReadOnlyList<string>> AttachmentContentKeysAsync() =>
            _sqlite.ListKeysAsync(AttachmentContentStore.ContentCollectionName, string.Empty);
    }
}

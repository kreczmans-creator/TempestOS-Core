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
/// <b>`WP 16.4B-R7`, Agent B — adversarial falsification of the `TD-143`
/// remediation.</b> Every fact here exists to break the design, not to
/// demonstrate it. Nothing is timed and nothing is raced: every failure is
/// injected deterministically against the real classes.
/// </summary>
/// <remarks>
/// <para>
/// The facts here fall into two kinds and they must not be confused.
/// </para>
/// <list type="bullet">
/// <item><description><b>Guard-rails</b> — these assert what the `WP 16.4B-R7`
/// remediation claims, verified independently of the engineer who wrote it and,
/// where it matters, against the real durable stack rather than an in-memory
/// probe. They must stay green.</description></item>
/// <item><description><b>Characterisations</b> — these assert what the platform
/// <em>currently does</em>, including where that is wrong. Each names the Agent
/// B finding it pins (<c>B-F1</c>…<c>B-F5</c>, `scratchpad/r7-agentB-report.md`).
/// <b>When one of those defects is fixed, invert the assertion — do not delete
/// the fact</b>, exactly as `WP 16.4B-R6b` and `WP 16.4B-R7` inverted the facts
/// they closed. Five facts are characterisations:
/// <see cref="ALegacyRecordThatOutlivesTheCurrentOne_BecomesTheLiveRecordAgain"/>
/// (`B-F3`),
/// <see cref="AThrowFromTheEvidenceComparison_ReplacesTheRealFailureAndSkipsTheUndo"/>
/// and <see cref="TheShippedStateStoreHappilyReturnsARecordWithNoHistory"/>
/// (`B-F1`),
/// <see cref="AStoreThatCommitsThrowsAndThenReadsStale_IsUndoneJustAsWrongly"/>
/// and <see cref="TheEvidenceTest_AcceptsARecordThisObjectNeverWrote"/> (`B-F2`),
/// <see cref="AnOrdinaryValidationRejection_NowPerformsADurableRead"/> (`B-F4`)
/// and
/// <see cref="ADeleteWhoseByteReleaseFailsAfterTheStateWriteCommitted_ReportsFailureForAnIrreversibleDelete"/>
/// (`B-F5`).</description></item>
/// </list>
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
    // §A  The store layer — the claim that a surviving legacy record is
    //     inert (Agent A §2.2), and that nothing fallible runs after the
    //     commit point.
    // ================================================================

    /// <summary>
    /// ATTACK 3. A legacy-encoded record that outlives the best-effort
    /// migration must never be read in preference to the current-encoding
    /// record, on either read overload, and must never double a listing.
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

    /// <summary>
    /// ATTACK 3/4, the other direction. A surviving legacy record is inert
    /// only while the current-encoding record exists. `DeleteAsync` removes
    /// the current record FIRST and the legacy record second, so a delete
    /// that fails on the second step resurrects the stale legacy value as
    /// the live record. Constructed here as an on-disk state, without
    /// asserting how it was reached.
    /// </summary>
    [Fact]
    public async Task ALegacyRecordThatOutlivesTheCurrentOne_BecomesTheLiveRecordAgain()
    {
        var root = NewRoot("r7b-legacy-resurrect");
        var store = NewStore(root);

        await store.WriteAsync("coll", "CON", "current-value");

        var collectionDirectory = Path.Combine(root, "coll");
        await File.WriteAllTextAsync(Path.Combine(collectionDirectory, "CON"), "STALE-LEGACY-VALUE");

        // The exact state `DeleteAsync` leaves when its first `File.Delete`
        // succeeds and its second throws.
        File.Delete(Path.Combine(collectionDirectory, "%43ON"));

        Assert.Equal("STALE-LEGACY-VALUE", await store.ReadAsync("coll", "CON"));
    }

    // ================================================================
    // §B  The evidence check — attacks 1 and 7.
    // ================================================================

    /// <summary>
    /// ATTACK 1/7. A throw from the evidence step — the comparison, or the
    /// <c>CaptureState()</c> that feeds it — leaves the caller's real
    /// exception intact and still performs the undo.
    /// </summary>
    /// <remarks>
    /// <b>INVERTED by `WP 16.4B-R7` round 2 (`B-F1`), on this file's own
    /// standing instruction; it was a characterisation and is now a
    /// guard-rail.</b> It stood as
    /// <c>AThrowFromTheEvidenceComparison_ReplacesTheRealFailureAndSkipsTheUndo</c>
    /// and asserted the defect it found: <c>DurableRecordAlreadyShowsThis-
    /// StateAsync</c> guarded only <c>store.FindAsync</c>, leaving the
    /// comparison and <c>CaptureState()</c> outside that try/catch and
    /// inside <c>RollBackOnFailureAsync</c>'s catch, so a throw there
    /// replaced the caller's real exception with an
    /// <c>ArgumentNullException</c> AND skipped the undo — `TD-143` in full,
    /// on the path built to close it, and the object's next successful write
    /// then made the attach durable. Agent A moved the comparison inside the
    /// guard. Every assertion below is the same one, negated.
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

        // A durable record whose History is absent — see
        // `TheShippedStateStoreHappilyReturnsARecordWithNoHistory` below for
        // its reachability against the real store.
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

    /// <summary>
    /// ATTACK 1/7, reachability. The shipped
    /// <see cref="EngineeringObjectStateStore"/> returns a non-null record
    /// with a null <c>History</c> for a durable record that simply has no
    /// <c>History</c> property — it catches only <see cref="JsonException"/>
    /// and the record's collections are ordinary, non-required constructor
    /// parameters.
    /// </summary>
    [Fact]
    public async Task TheShippedStateStoreHappilyReturnsARecordWithNoHistory()
    {
        var root = NewRoot("r7b-nohistory");
        var persistence = NewStore(root);
        var states = new EngineeringObjectStateStore(persistence);

        var id = Guid.NewGuid();
        var json =
            "{\"SchemaVersion\":1,\"Id\":\"" + id + "\",\"Kind\":\"Part\",\"Identifier\":\"P-1\"," +
            "\"DisplayName\":\"Bracket\",\"Metadata\":{},\"Status\":\"Draft\",\"ParentId\":null," +
            "\"IsDeleted\":false,\"BomLine\":{\"Quantity\":1,\"UnitOfMeasure\":null,\"FindNumber\":null," +
            "\"ItemNumber\":null,\"ReferenceDesignator\":null},\"Attachments\":[],\"TypeState\":{}}";

        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, id.ToString("N"), json);

        var state = await states.FindAsync(id);

        Assert.NotNull(state);
        Assert.Null(state!.History);
    }

    /// <summary>
    /// ATTACK 7. Every failed mutation now costs one durable read — INCLUDING
    /// an ordinary validation rejection that never touched a field. The
    /// store observes a read it never used to see, on a path where nothing
    /// was written.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryValidationRejection_NowPerformsADurableRead()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        rig.States.Reads = 0;

        await Assert.ThrowsAsync<InvalidLifecycleTransitionException>(
            () => part.TransitionAsync(LifecycleState.Released));

        Assert.Equal(1, rig.States.Reads);
    }

    /// <summary>
    /// ATTACK 1. Agent A's stated limitation is "commits, throws, and is
    /// then UNREADABLE". A store that commits, throws, and answers the
    /// re-read with a STALE but perfectly readable record produces the same
    /// wrong undo — so the stated scope is narrower than the real one.
    /// </summary>
    [Fact]
    public async Task AStoreThatCommitsThrowsAndThenReadsStale_IsUndoneJustAsWrongly()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        rig.States.StaleReads = true;
        rig.States.CommitThenFailNextSave();

        await Assert.ThrowsAsync<IOException>(() => part.RenameAsync("Renamed"));

        // The record DID land.
        var persisted = rig.States.Peek(part.Id);
        Assert.Equal("Renamed", persisted!.DisplayName);

        // The instance was undone anyway: it now disagrees with its own
        // durable record, and no later write on this object repairs it.
        Assert.Equal("Bracket", part.DisplayName);
    }

    // ================================================================
    // §C  Every persistence boundary — attack 5, against the REAL
    //     durable stack rather than an in-memory probe.
    // ================================================================

    /// <summary>
    /// ATTACK 5. All seven mutators, real <see cref="PersistenceStore"/>,
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
    /// ATTACK 5/8. The `TD-140` supersession refusal must not regress, and
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
    /// ATTACK 5. A delete whose STATE WRITE COMMITS and whose `TD-97` byte
    /// release then fails reports failure for an object that is durably and
    /// irreversibly soft-deleted — the same product harm `TD-143` was
    /// raised for, by a route `WP 16.4B-R7` does not close and does not
    /// disclose. Characterisation, not a regression: the byte release has
    /// always run after the persist.
    /// </summary>
    [Fact]
    public async Task ADeleteWhoseByteReleaseFailsAfterTheStateWriteCommitted_ReportsFailureForAnIrreversibleDelete()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");
        await part.AttachContentAsync("a.pdf", "application/pdf", Bytes);

        rig.Content.FailNextDelete();

        await Assert.ThrowsAsync<IOException>(() => part.DeleteAsync());

        Assert.True(part.IsDeleted);
        Assert.True(rig.States.Peek(part.Id)!.IsDeleted);
    }

    /// <summary>
    /// ATTACK 5. Repeated failed mutations leave no accumulation, and the
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
    /// ATTACK 2. The evidence the undo turns on is a record, not a receipt.
    /// The object write lock is an instance field of one
    /// <c>EngineeringDomainContext</c>, so it excludes nothing outside that
    /// one composed context — a second context, or a second process, over
    /// the same store writes freely inside the window between the failed
    /// write and the re-read. A record written by such an actor satisfies
    /// the test just as well as this object's own, and the mutation the
    /// caller was told had failed is then kept.
    /// </summary>
    [Fact]
    public async Task TheEvidenceTest_AcceptsARecordThisObjectNeverWrote()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("P-1", "Bracket");

        // A foreign writer lands a record carrying the mutated display name
        // — and an Identifier this object has never had, so it is provably
        // not this object's own write — in the window the failed write opens.
        rig.States.ForeignWriteBeforeFailingNextSave = existing =>
            existing with { DisplayName = "Renamed", Identifier = "WRITTEN-BY-SOMEBODY-ELSE" };

        await Assert.ThrowsAsync<IOException>(() => part.RenameAsync("Renamed"));

        Assert.Equal("WRITTEN-BY-SOMEBODY-ELSE", rig.States.Peek(part.Id)!.Identifier);

        // The undo was declined on that evidence. The caller was told the
        // rename failed; the instance kept it.
        Assert.Equal("Renamed", part.DisplayName);
    }

    /// <summary>
    /// ATTACK 8. The `TD-142` type-specific mutators still keep a mutation
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
    // §D  The individual mutators — attack 6.
    // ================================================================

    /// <summary>
    /// ATTACK 6. <c>MoveAsync</c>: a failing LINK write leaves no reparent
    /// and no link; a link that succeeds followed by a failing STATE write
    /// leaves no reparent but DOES leave the `groupedUnder` edge — Agent A's
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
    /// ATTACK 6. <c>AttachContentAsync</c>: the in-memory add is undone,
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

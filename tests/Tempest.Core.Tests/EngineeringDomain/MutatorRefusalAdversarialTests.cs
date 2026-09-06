using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// <b>`WP 16.4B-R6` round 2, independent adversarial verification.</b> These
/// facts exist to <em>falsify</em> the round-2 remediation, not to
/// demonstrate it. They attack the parts of it that
/// <see cref="RefusedMutationSuccessorLeakageTests"/> — written by the
/// engineer who made the change — does not reach: the deadlock surface the
/// change created by holding the object write lock across more work, the
/// exact boundary of the invariant it claims, and the three paths the
/// remediation deliberately left alone (`TD-141`, `TD-142`, and
/// <c>MoveAsync</c>'s guard).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read this before treating any fact here as a regression.</b> The
/// facts in this file fall into two kinds and they must not be confused:
/// </para>
/// <list type="bullet">
/// <item><description><b>Guard-rails</b> (§1, §2) — these assert what the remediation claims. They must stay green for ever; each is killed by a specific mutant, named in its own remarks.</description></item>
/// <item><description><b>Characterisations</b> (§3, §4, §5, §6) — these assert what the platform <em>currently does</em>, including where that is wrong. Every one of them names the defect it pins and the register row that owns it. <b>When a defect here is fixed, invert the assertion — do not delete the fact</b>, exactly as `WP 16.4B-R6b` inverted the five facts in <see cref="RefusedMutationSuccessorLeakageTests"/>.</description></item>
/// </list>
/// <para>
/// <b>Determinism.</b> Nothing here asserts the outcome of a race. The two
/// interleavings that need one are forced in program order by holding the
/// object write lock from the test itself (<see cref="TwoMovesThatEachPassTheCircularParentGuard_FormACycle"/>)
/// or by parking the document store inside <c>ReviseAsync</c>'s own lock
/// hold (<see cref="ARefusedTypeSpecificMutator_LeaksItsFieldIntoTheLiveSuccessor"/>).
/// The only timed values in the file are upper bounds that turn a hang into
/// a reported failure; no fact passes <em>because</em> of one.
/// </para>
/// </remarks>
public sealed class MutatorRefusalAdversarialTests
{
    /// <summary>
    /// Long enough that no machine fails one of these by being slow, short
    /// enough that a self-deadlock on the non-reentrant object write lock
    /// is a failed fact rather than a hung test run. Nothing asserts a
    /// value measured against it.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] Bytes = [7, 8, 9];

    // ================================================================
    // §1 Deadlock — the principal risk of this round
    //
    // `AsyncKeyedLock` is not reentrant and `PersistStateAsync` acquires
    // the object write lock itself. Round 2 made every mutator on
    // `EngineeringObjectBase` hold that lock across its mutation and its
    // durable write, and made `MoveAsync` hold it across a *second*
    // durable write as well. Any one of them reaching `PersistStateAsync`
    // from inside that hold deadlocks against itself, for ever, in
    // production.
    // ================================================================

    /// <summary>
    /// All seven mutators, on a real Kind, against the real
    /// <see cref="PersistenceStore"/>, <see cref="EngineeringDocumentStore"/>,
    /// <see cref="EngineeringObjectStateStore"/> and
    /// <see cref="AttachmentContentStore"/> — every one completes, and every
    /// one lands durably.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail. This is the fact that fails instead of hanging.</b>
    /// The existing round-2 suite exercises the seven mutators against a
    /// retired instance, where each is refused <em>before</em> it reaches
    /// its durable write — so a mutator that re-entered the lock on its
    /// success path would never be detected there. This puts all seven
    /// through the path that actually takes the lock twice if the fix is
    /// wrong, and bounds each call, so the failure mode is a red fact and
    /// not a test run that never finishes.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> replace
    /// <c>PersistStateHoldingWriteLockAsync</c> with
    /// <c>PersistStateAsync</c> inside <c>MutateAndPersistAsync</c> —
    /// the single most plausible mistake in this change. Verified: the
    /// first bounded call times out and this fact fails.
    /// </para>
    /// <para>
    /// The real stores are not decoration. <c>MoveAsync</c> holds the
    /// object write lock across <c>EngineeringDocumentStore.LinkAsync</c>,
    /// which takes <see cref="PersistenceStore"/>'s own per-key lock and
    /// touches the file system; a fake document store would not exercise
    /// that nesting at all.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task EverySevenMutator_CompletesAndPersists_AgainstTheRealDurableStores()
    {
        using var fixture = DurableFixture.Create("adversarial-seven");

        var parent = await fixture.CreatePartAsync("PRT-P", "Housing");
        var part = await fixture.CreatePartAsync("PRT-1", "Bracket");

        var phantom = new Attachment("metadata-only.txt", "text/plain", 4);

        await part.AttachAsync(phantom).WaitAsync(Timeout);
        var withContent = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);
        await part.RenameAsync("Bracket, revised").WaitAsync(Timeout);
        await part.MoveAsync(parent.Id).WaitAsync(Timeout);
        await part.SetBomLineAsync(4m, "each", "FN-1", "IN-1", "RD-1").WaitAsync(Timeout);
        await part.DeleteAsync().WaitAsync(Timeout);

        var state = await fixture.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Bracket, revised", state.DisplayName);
        Assert.Equal(LifecycleState.InReview, state.Status);
        Assert.Equal(parent.Id, state.ParentId);
        Assert.Equal(4m, state.BomLine.Quantity);
        Assert.True(state.IsDeleted);
        Assert.Equal(2, state.Attachments.Count);
        Assert.Single(state.History);

        // The move's own durable link write landed too, inside the same
        // hold of the object write lock as the state write above.
        var references = await fixture.Documents.GetReferencesAsync(part.Id);
        Assert.Contains(references, r => r.TargetDocumentId == parent.Id && r.RelationshipKind == "groupedUnder");

        // And the delete released the bytes, after the state write.
        Assert.False((await fixture.Content.ReadAsync(withContent.Id, withContent.ContentHash, withContent.SizeInBytes)).IsAvailable);
    }

    /// <summary>
    /// <c>MoveAsync</c> really does perform its durable <c>groupedUnder</c>
    /// link write inside its hold of this object's write lock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail, and the deadlock surface stated as a fact.</b> Round 2
    /// made <c>MoveAsync</c> the only mutator that keeps a <em>second</em>
    /// durable write inside the hold. What that pins is the constraint a
    /// future change can silently break: <b>any store implementation
    /// reached from inside one of these holds that itself acquires this
    /// object's write lock deadlocks the platform.</b> Today none does —
    /// the document store takes only its own per-document and per-key
    /// locks, and the relationship repository is a <c>ConcurrentBag</c>.
    /// </para>
    /// <para>
    /// <b>How the hold is observed.</b> Not by asking the lock — see
    /// <see cref="Contention"/> for why that probe was wrong and how it was
    /// caught — but by starting a real second mutator on the same object
    /// from inside the link write and observing that it cannot complete.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> move the <c>LinkAsync</c> call out of
    /// the <c>using</c> block (a "keep the link, shorten the hold" change).
    /// Verified.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task MoveAsync_PerformsItsDurableLinkWrite_WhileHoldingTheObjectWriteLock()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        var parent = await rig.CreatePartAsync("PRT-P", "Housing");

        rig.Documents.LinkWrite.Arm(() => part.RenameAsync("Renamed by the contender"));

        await part.MoveAsync(parent.Id).WaitAsync(Timeout);

        Assert.NotNull(rig.Documents.LinkWrite.Contender);
        Assert.True(rig.Documents.LinkWrite.ContenderWasBlocked, "MoveAsync's durable groupedUnder write ran outside the object write lock.");

        await rig.Documents.LinkWrite.Contender!.WaitAsync(Timeout);
        Assert.Equal("Renamed by the contender", part.DisplayName);
    }

    /// <summary>
    /// The durable <em>state</em> write of a mutator happens inside the same
    /// hold as the mutation it records.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> This is the whole of round 2's claim reduced to
    /// one observation: while a mutator is writing its state record, no
    /// other mutator on the same object can proceed — which is what makes
    /// the supersession decision and the write one indivisible step.
    /// Killed by any mutant that takes the mutation or the write out of the
    /// hold, and by the re-entrant-persist mutant (which turns it into a
    /// deadlock).
    /// </remarks>
    [Fact]
    public async Task TheDurableStateWrite_HappensWhileHoldingTheObjectWriteLock()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.StateWrite.Arm(() => part.SetBomLineAsync(3m));

        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        Assert.NotNull(rig.States.StateWrite.Contender);
        Assert.True(rig.States.StateWrite.ContenderWasBlocked, "A second mutator ran while the state write was in flight.");

        await rig.States.StateWrite.Contender!.WaitAsync(Timeout);
        Assert.Equal(3m, part.Quantity);
    }

    /// <summary>
    /// <c>AttachContentAsync</c>'s content write — the longest hold in the
    /// platform — is inside the lock, as `WP 16.4B-R6` intended and as the
    /// independent board recorded as a cost.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail, and the one measurement this file makes about the
    /// cost of the design.</b> The hold spans an arbitrarily large byte
    /// write. That is deliberate — it is what closes the board's `F1` — but
    /// it is also the reason `TD-140`'s row says the window for everything
    /// else was widened, so it is asserted rather than assumed.
    /// </remarks>
    [Fact]
    public async Task AttachContentAsync_WritesItsContent_WhileHoldingTheObjectWriteLock()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.Content.ContentWrite.Arm(() => part.RenameAsync("Renamed by the contender"));

        await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        Assert.NotNull(rig.Content.ContentWrite.Contender);
        Assert.True(rig.Content.ContentWrite.ContenderWasBlocked, "The attachment content write ran outside the object write lock.");

        await rig.Content.ContentWrite.Contender!.WaitAsync(Timeout);
        Assert.Equal("Renamed by the contender", part.DisplayName);
    }

    /// <summary>
    /// Sixteen mutations and four revisions, over four objects, all in
    /// flight at once against the real durable stores: every one of them
    /// finishes.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail — a hang detector, not a race assertion.</b> It asserts
    /// nothing about which call wins; only that the whole set completes
    /// inside a bound. A lock-order inversion or a re-entrant acquire on
    /// any of these paths stops the set completing and this fact reports
    /// it. Every outcome except a deadlock is accepted, and the accepted
    /// exception types are enumerated so an unexpected one is still a
    /// failure.
    /// </remarks>
    [Fact]
    public async Task ConcurrentMutatorsAndRevisions_AllComplete_NoneDeadlock()
    {
        using var fixture = DurableFixture.Create("adversarial-concurrent");

        var parent = await fixture.CreatePartAsync("PRT-P", "Housing");

        var parts = new List<Part>();
        for (var i = 0; i < 4; i++)
            parts.Add(await fixture.CreatePartAsync($"PRT-{i}", $"Bracket {i}"));

        var work = new List<Task<Exception?>>();

        foreach (var part in parts)
        {
            work.Add(RecordAsync(() => part.RenameAsync("Renamed")));
            work.Add(RecordAsync(() => part.SetBomLineAsync(9m, "each")));
            work.Add(RecordAsync(() => part.TransitionAsync(LifecycleState.InReview)));
            work.Add(RecordAsync(() => part.MoveAsync(parent.Id)));
            work.Add(RecordAsync(() => part.ReviseAsync("Revised.", "Rev B.")));
        }

        var outcomes = await Task.WhenAll(work).WaitAsync(Timeout);

        foreach (var outcome in outcomes.Where(o => o is not null))
        {
            Assert.True(
                outcome is SupersededEngineeringObjectException or InvalidLifecycleTransitionException,
                $"Unexpected failure from a concurrent mutator: {outcome!.GetType().Name}: {outcome.Message}");
        }
    }

    // ================================================================
    // §2 The refusal invariant, from an angle the author's own suite
    //     does not use
    // ================================================================

    /// <summary>
    /// A retired instance refuses <b>before</b> it adjudicates the
    /// operation: a lifecycle transition that is <em>also</em> impermissible
    /// is refused with <see cref="SupersededEngineeringObjectException"/>,
    /// and — the part that matters — the object's own state is untouched
    /// either way.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> Round 2 moved the <c>IsPermitted</c> check inside
    /// the lock, which changed the exception a retired instance raises for
    /// an impermissible transition. That precedence change is deliberate
    /// and is asserted here so that it is a decision on the record rather
    /// than an accident a later change can quietly reverse — and so that
    /// the accompanying claim, that neither exception leaves a history
    /// entry behind, is checked on the path where two refusals compete.
    /// </remarks>
    [Fact]
    public async Task ARetiredInstance_RefusesBeforeItAdjudicates_AndKeepsItsHistoryEmpty()
    {
        using var fixture = DurableFixture.Create("adversarial-precedence");

        var part = await fixture.CreatePartAsync("PRT-1", "Bracket");
        var successor = (Part)await part.ReviseAsync("Revised.", "Rev B.");

        // Approved is not reachable from Draft: impermissible *and* retired.
        var refused = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Approved).WaitAsync(Timeout));

        Assert.IsType<SupersededEngineeringObjectException>(refused);
        Assert.Empty(part.History);
        Assert.Empty(successor.History);
        Assert.Equal(LifecycleState.Draft, part.Status);

        // A live instance still gets the domain answer, unchanged.
        var rejected = await Record.ExceptionAsync(() => successor.TransitionAsync(LifecycleState.Approved).WaitAsync(Timeout));
        Assert.IsType<InvalidLifecycleTransitionException>(rejected);
        Assert.Empty(successor.History);

        await successor.RenameAsync("Written by the successor").WaitAsync(Timeout);
        var state = await fixture.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Empty(state.History);
        Assert.Equal(LifecycleState.Draft, state.Status);
    }

    // ================================================================
    // §3 CHARACTERISATION — the boundary of the invariant round 2 claims
    //
    // "An operation the platform reports as failed must not become
    // durable, and must not change this instance either." Round 2 makes
    // that true when the report is a SupersededEngineeringObjectException.
    // It is NOT true when the report is anything else. These four facts
    // pin where the boundary actually is, so that no register row can
    // claim the wider statement.
    // ================================================================

    /// <summary>
    /// A move whose durable <c>groupedUnder</c> link write fails still
    /// reparents the instance, and the object's <b>next successful write of
    /// any kind</b> makes that reparent durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation — open defect, no register row.</b> Nothing here
    /// is contrived: the target simply has no document, which
    /// <c>GuardAgainstCircularParentAsync</c> does not detect (an unknown
    /// id is not <c>IHasParent</c>, so the walk returns) and which
    /// <c>EngineeringDocumentStore</c>/<c>InMemoryEngineeringDocumentStore</c>
    /// both answer with <see cref="EngineeringDocumentNotFoundException"/>
    /// — from <em>inside</em> the write lock, after <c>_parentId</c> has
    /// already been assigned and before the state write. An I/O failure of
    /// the same durable write behaves identically.
    /// </para>
    /// <para>
    /// This is the one place round 2 chose not to route through
    /// <c>MutateAndPersistAsync</c>, and it is the one mutator whose
    /// failure path can therefore still leave the instance changed. It is
    /// pre-existing — the same ordering held before round 2 — and it is
    /// <b>narrower</b> than it was, because the hold now prevents a
    /// concurrent revision from adopting the orphan parent. It is not
    /// closed.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMoveWhoseDurableLinkWriteFails_StillReparentsTheInstance_AndTheNextWriteMakesItDurable()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var strayParentId = Guid.NewGuid();

        var failure = await Record.ExceptionAsync(() => part.MoveAsync(strayParentId).WaitAsync(Timeout));
        Assert.IsType<EngineeringDocumentNotFoundException>(failure);

        // The caller was told the move failed. The instance disagrees.
        Assert.Equal(strayParentId, part.ParentId);

        // The move's own write never happened...
        var afterMove = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterMove);
        Assert.Null(afterMove.ParentId);

        // ...but the next unrelated, successful operation carries it to disk.
        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);

        var afterRename = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRename);
        Assert.Equal(strayParentId, afterRename.ParentId);
    }

    /// <summary>
    /// A lifecycle transition whose durable write fails still stamps an
    /// actor principal id and a timestamp into the append-only transition
    /// history, and the next successful write makes that entry durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation — the highest-consequence member of §3.</b>
    /// `TD-140` closed exactly this outcome for the supersession refusal,
    /// on the stated ground that the transition history is this platform's
    /// governance record and has no removal path. Both of those grounds
    /// hold identically here; only the exception type differs. The fact is
    /// written to make that impossible to overlook: it asserts the
    /// fabricated entry's <em>actor and target state</em> on disk, not
    /// merely that the history is non-empty.
    /// </para>
    /// <para>
    /// The failing write is an ordinary <see cref="IOException"/> from the
    /// state store — the class every durable store in this platform can
    /// raise, and the class `WP 16.4B-R5` was told not to compensate for.
    /// No register row covers it for any of the seven mutators; the closest
    /// is Agent C's <c>C-F3</c>, which is about the two attachment paths
    /// and is itself recorded only in test remarks (the independent board's
    /// <c>D-A4</c>).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ATransitionWhoseDurableWriteFails_StillStampsTheAuditEntry_AndTheNextWriteMakesItDurable()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        // The caller was told the transition failed. The audit trail
        // disagrees, and there is no path that removes an entry.
        Assert.Equal(LifecycleState.InReview, part.Status);
        var fabricated = Assert.Single(part.History);
        Assert.Equal(LifecycleState.Draft, fabricated.From);
        Assert.Equal(LifecycleState.InReview, fabricated.To);
        Assert.False(string.IsNullOrWhiteSpace(fabricated.ActorPrincipalId));

        await part.RenameAsync("Renamed after the failed transition").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(LifecycleState.InReview, state.Status);
        var durable = Assert.Single(state.History);
        Assert.Equal(LifecycleState.InReview, durable.To);
    }

    /// <summary>
    /// A delete whose durable write fails still soft-deletes the instance —
    /// the outcome `TD-140` describes as removing the object from the whole
    /// product with no supported way back — and the next successful write
    /// makes it durable.
    /// </summary>
    /// <remarks>
    /// <b>Characterisation.</b> The `TD-140` reasoning about
    /// <c>DeleteAsync</c> (one writer, no undelete, twenty-one read models
    /// filtering <c>IDeletable { IsDeleted: true }</c>) is a property of the
    /// flag, not of the exception that interrupts the write. This fact
    /// records that the round-2 fix removed the supersession route to that
    /// outcome and did not remove the failure route to it. Note also that
    /// the `TD-97` byte release is skipped, exactly as it was on the
    /// refusal path before the fix.
    /// </remarks>
    [Fact]
    public async Task ADeleteWhoseDurableWriteFails_StillSoftDeletesTheInstance_AndTheNextWriteMakesItDurable()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.DeleteAsync().WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        Assert.True(part.IsDeleted, "A delete the caller was told had failed left the object soft-deleted.");
        Assert.Contains(attachment.Id, rig.Content.StoredKeys);

        await part.RenameAsync("Renamed after the failed delete").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.True(state.IsDeleted);
    }

    /// <summary>
    /// The same failure boundary, on the four remaining mutators: each
    /// keeps its in-memory mutation when the durable write fails.
    /// </summary>
    /// <remarks>
    /// <b>Characterisation.</b> Stated for the whole set rather than one
    /// example, so that a register row derived from this file cannot
    /// describe the boundary as a quirk of one method. <c>AttachAsync</c>
    /// and <c>AttachContentAsync</c> are included: this is Agent C's
    /// <c>C-F3</c>, still open, now asserted alongside its five siblings.
    /// </remarks>
    [Fact]
    public async Task TheOtherFourMutators_AlsoKeepTheirMutation_WhenTheDurableWriteFails()
    {
        var rig = new ProbeRig();

        var renamed = await rig.CreatePartAsync("PRT-1", "Bracket");
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => renamed.RenameAsync("Leaked name").WaitAsync(Timeout)));
        Assert.Equal("Leaked name", renamed.DisplayName);

        var bommed = await rig.CreatePartAsync("PRT-2", "Housing");
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => bommed.SetBomLineAsync(17m, "each", "FN-9").WaitAsync(Timeout)));
        Assert.Equal(17m, bommed.Quantity);
        Assert.Equal("FN-9", bommed.FindNumber);

        var attached = await rig.CreatePartAsync("PRT-3", "Plate");
        var phantom = new Attachment("phantom.txt", "text/plain", 3);
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => attached.AttachAsync(phantom).WaitAsync(Timeout)));
        Assert.Contains(await attached.GetAttachmentsAsync(), a => a.Id == phantom.Id);

        var contented = await rig.CreatePartAsync("PRT-4", "Cover");
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => contented.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout)));
        Assert.Single(await contented.GetAttachmentsAsync());
        Assert.Single(rig.Content.StoredKeys);
        Assert.Single(await rig.WriteIntents.ListMarkedAsync());
    }

    // ================================================================
    // §4 CHARACTERISATION — the checks that run outside the lock
    //     (MoveAsync's circular-parent guard, DeleteAsync's child count)
    // ================================================================

    /// <summary>
    /// Two moves that each pass <c>GuardAgainstCircularParentAsync</c>
    /// against a graph neither has yet modified between them create a
    /// parent cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation — recorded by round 2 as a known consequence of
    /// keeping the guard outside the write lock, and reproduced here for
    /// the first time.</b> Round 2's stated reason for that choice is
    /// sound (an unbounded chain walk inside a non-reentrant lock, calling
    /// <c>IHasParent.ParentId</c> on types outside this assembly, is a
    /// deadlock surface). This fact does not dispute the choice; it makes
    /// the accepted consequence executable, because "recorded in a code
    /// comment" is what the fifth and sixth boards both rejected.
    /// </para>
    /// <para>
    /// <b>Deterministic, no threads racing.</b> The test holds the object
    /// write lock for <c>a</c> itself. <c>a.MoveAsync(b)</c> therefore runs
    /// its guard to completion synchronously — the guard's repository reads
    /// all complete synchronously — and then parks on the lock, which is
    /// exactly the state the defect needs. <c>b.MoveAsync(a)</c> is then
    /// run to completion in program order before the lock is released.
    /// </para>
    /// <para>
    /// Note what is <em>not</em> claimed: fixing this needs a lock over the
    /// hierarchy rather than over one object, which is a design change
    /// outside this Work Package.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TwoMovesThatEachPassTheCircularParentGuard_FormACycle()
    {
        var rig = new ProbeRig();
        var a = await rig.CreatePartAsync("PRT-A", "A");
        var b = await rig.CreatePartAsync("PRT-B", "B");

        var held = await rig.Context.AcquireObjectWriteLockAsync(a.Id);

        // Passes its guard (b has no parent yet), then blocks on a's lock.
        var movingAUnderB = a.MoveAsync(b.Id);
        Assert.False(movingAUnderB.IsCompleted);

        // Completes entirely: its own guard walks a, whose ParentId is
        // still null, so no cycle is visible to it either.
        await b.MoveAsync(a.Id).WaitAsync(Timeout);

        held.Dispose();
        await movingAUnderB.WaitAsync(Timeout);

        Assert.Equal(b.Id, a.ParentId);
        Assert.Equal(a.Id, b.ParentId);
    }

    /// <summary>
    /// The consequence of that cycle, in production code: the Explorer's
    /// own breadcrumb walk never terminates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation — this is why the cycle is not merely untidy.</b>
    /// <c>MechanicalProductStructureNodeProvider.GetAncestryAsync</c> walks
    /// <c>IHasParent.ParentId</c> in a <c>while</c> loop with no visited
    /// set, and four other node providers in <c>Tempest.App</c> carry the
    /// identical loop. Over a cycle it allocates for ever.
    /// </para>
    /// <para>
    /// <b>How this is asserted safely.</b> The fact does not run the loop
    /// to completion — it cannot — and does not use a timeout. A tripwire
    /// repository throws after a bounded number of lookups, so a walk over
    /// a two-object graph that asks for a 500th ancestor is proof the loop
    /// does not terminate, and the fact finishes in milliseconds either
    /// way.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TheExplorersAncestryWalkOverThatCycle_NeverTerminates()
    {
        var rig = new ProbeRig();
        var a = await rig.CreatePartAsync("PRT-A", "A");
        var b = await rig.CreatePartAsync("PRT-B", "B");

        var held = await rig.Context.AcquireObjectWriteLockAsync(a.Id);
        var movingAUnderB = a.MoveAsync(b.Id);
        await b.MoveAsync(a.Id).WaitAsync(Timeout);
        held.Dispose();
        await movingAUnderB.WaitAsync(Timeout);

        rig.Repository.TripAfter(500);

        var provider = new MechanicalProductStructureNodeProvider("Part", rig.Context);

        await Assert.ThrowsAsync<TripwireException>(() => provider.GetAncestryAsync(a.Id));
    }

    /// <summary>
    /// <c>DeleteAsync</c>'s "has this object any live children?" check also
    /// runs outside the write lock, so an object can be soft-deleted with a
    /// live child still naming it as parent — the exact state
    /// <see cref="EngineeringObjectHasChildrenException"/> exists to
    /// prevent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation — a second consequence of the same design
    /// choice, and one nothing in the governance record names.</b> Round 2
    /// moved the supersession refusal inside the lock for
    /// <c>DeleteAsync</c>; the child check stayed where it was, above
    /// <c>MutateAndPersistAsync</c>, reading the whole repository. A
    /// <c>MoveAsync</c> that commits between that read and the lock
    /// acquisition creates the child the check just proved absent.
    /// </para>
    /// <para>
    /// The product consequence is not a refusal that should have happened:
    /// every read model filters the deleted parent out while the child
    /// remains live, so the child is reachable by Id and absent from every
    /// tree that walks down from a root. Pre-existing, unchanged by round
    /// 2, and reproduced here in program order for the first time.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ADeleteCanCommit_WhileAMoveGivesItALiveChild()
    {
        var rig = new ProbeRig();
        var parent = await rig.CreatePartAsync("PRT-P", "Housing");
        var child = await rig.CreatePartAsync("PRT-C", "Bracket");

        var held = await rig.Context.AcquireObjectWriteLockAsync(parent.Id);

        // The child check runs here, against a repository in which the
        // parent has no children; then the delete parks on the lock.
        var deleting = parent.DeleteAsync();
        Assert.False(deleting.IsCompleted);

        await child.MoveAsync(parent.Id).WaitAsync(Timeout);

        held.Dispose();
        await deleting.WaitAsync(Timeout);

        Assert.True(parent.IsDeleted);
        Assert.False(child.IsDeleted);
        Assert.Equal(parent.Id, child.ParentId);

        // Attempted in the other order it is refused, which is what the
        // check is for — so this is a hole in the check, not the absence
        // of one.
        var refused = await Record.ExceptionAsync(() => parent.DeleteAsync().WaitAsync(Timeout));
        Assert.IsType<EngineeringObjectHasChildrenException>(refused);
    }

    // ================================================================
    // §5 CHARACTERISATION — TD-141, LinkAsync
    // ================================================================

    /// <summary>
    /// <c>LinkAsync</c> on an instance <c>ReviseAsync</c> has already
    /// retired is <b>not refused</b>: it writes the permanent, append-only
    /// relationship, and the relationship is visible on the live successor,
    /// because both instances share one Id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation for `TD-141`, which the register records as
    /// "recorded, not characterised" and states from a reading of the code
    /// rather than a reproduction.</b> This is the reproduction. The row's
    /// reading is correct: no lock, no <c>ThrowIfSuperseded</c>, no
    /// <c>PersistStateAsync</c>, therefore no protection from `WP 16.4B-R3`
    /// through `-R6b` reaches this path.
    /// </para>
    /// <para>
    /// <b>What the reproduction adds to the reading.</b> Two things the row
    /// could not state. First, the consequence is not confined to the
    /// retired instance: <c>GetRelationshipsAsync</c> is keyed by
    /// <see cref="IEngineeringObject.Id"/>, which predecessor and successor
    /// share, so a write through a retired handle lands on the graph of the
    /// object that is live — it is an injection into current data, not a
    /// divergence in a dead one. Second, it is durable in the document
    /// store's references and therefore survives a restart, while
    /// <c>ThrowIfSuperseded</c> would have refused every other durable
    /// write made through the same handle in the same breath.
    /// </para>
    /// <para>
    /// Deliberately <b>not</b> asserted: whether this ought to be refused.
    /// An append-only relationship recorded through a handle that has since
    /// been revised is arguably legitimate — the Id is the same object.
    /// That judgement belongs with the row, not with this fact.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task LinkAsync_OnARetiredInstance_IsNotRefused_AndWritesThePermanentRelationship()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        var target = await rig.CreatePartAsync("PRT-2", "Housing");

        var successor = (Part)await part.ReviseAsync("Revised.", "Rev B.");

        // Every other durable write through this handle is refused...
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.RenameAsync("x").WaitAsync(Timeout));

        // ...this one is not.
        await part.LinkAsync(target.Id, "relatesTo").WaitAsync(Timeout);

        var references = await rig.Documents.GetReferencesAsync(part.Id);
        Assert.Contains(references, r => r.TargetDocumentId == target.Id && r.RelationshipKind == "relatesTo");

        // And it is on the live successor's own graph, not on a dead one.
        Assert.Equal(part.Id, successor.Id);
        Assert.Contains(await successor.GetRelationshipsAsync(), r => r.TargetId == target.Id);
    }

    /// <summary>
    /// That unguarded path is reachable through the product's own named
    /// relationship APIs, not only through <c>LinkAsync</c> itself.
    /// </summary>
    /// <remarks>
    /// <b>Characterisation for `TD-141`.</b> The row names
    /// <c>MoveAsync</c>'s <c>groupedUnder</c> write as the reachable
    /// caller. It is not the only one: several concrete Kinds expose a
    /// public relationship method whose whole body is a <c>LinkAsync</c>
    /// call — here <c>EngineeringTask.ContributeToAsync</c>. A caller
    /// holding a stale task handle records a permanent contribution edge
    /// that the platform would have refused had the same call needed to
    /// persist state.
    /// </remarks>
    [Fact]
    public async Task AKindsOwnRelationshipApi_ReachesTheUnguardedLinkPath()
    {
        var rig = new ProbeRig();
        var task = await rig.CreateTaskAsync("TSK-1", "Draft the report");
        var milestone = await rig.CreatePartAsync("MS-1", "Gate 3");

        _ = await task.ReviseAsync("Revised.", "Rev B.");

        await task.ContributeToAsync(milestone.Id).WaitAsync(Timeout);

        var references = await rig.Documents.GetReferencesAsync(task.Id);
        Assert.Contains(references, r => r.TargetDocumentId == milestone.Id);
    }

    // ================================================================
    // §6 CHARACTERISATION — TD-142, the eleven type-specific mutators
    // ================================================================

    /// <summary>
    /// A type-specific mutator on a concrete Kind, refused with
    /// <see cref="SupersededEngineeringObjectException"/>, still leaks its
    /// field into the live successor — and the successor's next write makes
    /// it durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation for `TD-142`, whose row states the consequence
    /// by analogy with `TD-140` rather than from a reproduction.</b> This
    /// is the reproduction, on the real <see cref="EngineeringTask"/> and
    /// the real <c>AssignAsync</c> — the site the chair verified by reading
    /// — and it establishes that the analogy is exact: the caller is told
    /// the assignment failed, and the object that answers for that Id is
    /// assigned, durably, to the principal the failed call named.
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> The interleaving is forced by parking the
    /// document store inside <c>ReviseAsync</c> — which happens inside
    /// <c>ReviseAsync</c>'s own hold of the object write lock and
    /// <em>before</em> <c>CaptureState</c> reads any field. While the
    /// revision is parked, <c>AssignAsync</c> applies its field under
    /// <c>_taskLock</c> synchronously, on this thread, and only then blocks
    /// on the lock. No sleeps, no polling, no second race.
    /// </para>
    /// <para>
    /// When `TD-142` is fixed, invert this fact — do not delete it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARefusedTypeSpecificMutator_LeaksItsFieldIntoTheLiveSuccessor()
    {
        var rig = new ProbeRig();
        var task = await rig.CreateTaskAsync("TSK-1", "Draft the report");

        Assert.Null(task.AssignedToPrincipalId);

        var parked = rig.Documents.ArmNextRevise();
        var revising = Task.Run(() => task.ReviseAsync("Revised.", "Rev B."));
        await parked.WaitAsync(Timeout);

        var assigning = task.AssignAsync("someone-who-was-told-it-failed");

        // The field is already mutated, before the refusal can be raised.
        Assert.Equal("someone-who-was-told-it-failed", task.AssignedToPrincipalId);

        rig.Documents.ReleaseRevise();

        var successor = (EngineeringTask)await revising.WaitAsync(Timeout);
        var refused = await Record.ExceptionAsync(() => assigning.WaitAsync(Timeout));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        // The live successor inherited the assignment the caller was told
        // had failed...
        Assert.Equal("someone-who-was-told-it-failed", successor.AssignedToPrincipalId);

        // ...and the successor's next write makes it durable.
        await successor.SetPriorityAsync(WorkPriority.High).WaitAsync(Timeout);

        var state = await rig.States.FindAsync(task.Id);
        Assert.NotNull(state);
        Assert.Equal("someone-who-was-told-it-failed", state.Type(nameof(EngineeringTask.AssignedToPrincipalId)));
    }

    /// <summary>
    /// The identical interleaving, on a mutator round 2 <em>did</em> fix,
    /// leaks nothing.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail, and the control for the fact above.</b> Same rig,
    /// same park point, same Kind, same object — only the mutator differs.
    /// Without this pair, the previous fact could be read as an artefact of
    /// the rig rather than as a property of the eleven unfixed sites; with
    /// it, the rig is shown to distinguish the two shapes. It is also the
    /// only fact in the repository that exercises the round-2 fix through a
    /// park inside <c>ReviseAsync</c> itself rather than inside
    /// <c>CaptureTypeState</c>, so it kills the same mutants from a
    /// different direction.
    /// </remarks>
    [Fact]
    public async Task TheSameInterleaving_OnAMutatorRoundTwoFixed_LeaksNothing()
    {
        var rig = new ProbeRig();
        var task = await rig.CreateTaskAsync("TSK-1", "Draft the report");

        var parked = rig.Documents.ArmNextRevise();
        var revising = Task.Run(() => task.ReviseAsync("Revised.", "Rev B."));
        await parked.WaitAsync(Timeout);

        var renaming = task.RenameAsync("Leaked name");

        // Nothing has been mutated: the refusal will happen before it is.
        Assert.Equal("Draft the report", task.DisplayName);

        rig.Documents.ReleaseRevise();

        var successor = (EngineeringTask)await revising.WaitAsync(Timeout);
        var refused = await Record.ExceptionAsync(() => renaming.WaitAsync(Timeout));

        Assert.IsType<SupersededEngineeringObjectException>(refused);
        Assert.Equal("Draft the report", successor.DisplayName);
        Assert.Equal("Draft the report", task.DisplayName);

        await successor.SetPriorityAsync(WorkPriority.High).WaitAsync(Timeout);

        var state = await rig.States.FindAsync(task.Id);
        Assert.NotNull(state);
        Assert.Equal("Draft the report", state.DisplayName);
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static async Task<Exception?> RecordAsync(Func<Task> operation) =>
        await Record.ExceptionAsync(async () => await Task.Run(operation).WaitAsync(Timeout));

    /// <summary>
    /// A one-shot contender: a real operation on the same object, started
    /// from inside a durable write, which can only fail to complete
    /// synchronously if that write is running inside a hold of this
    /// object's write lock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces an earlier probe that asked the lock directly
    /// (<c>AcquireObjectWriteLockAsync(...).IsCompleted</c>). That probe was
    /// <b>wrong</b>, and a sanity check written for this file caught it:
    /// a probe that loses the race to withdraw its own waiter reports the
    /// lock as held for ever afterwards, which makes the probing fact pass
    /// against a build that moved the write out of the hold. It is
    /// recorded here rather than quietly replaced, because a
    /// non-discriminating test that looks like evidence is exactly the
    /// class of defect this review campaign has been finding.
    /// </para>
    /// <para>
    /// The contender is an ordinary <c>RenameAsync</c> on the same object,
    /// through the public surface. Every fake in this rig completes
    /// synchronously, so an uncontended mutator's returned
    /// <see cref="Task"/> is already completed when it is handed back; a
    /// contended one is not. One shot, so the contender's own durable write
    /// cannot re-enter this.
    /// </para>
    /// </remarks>
    private sealed class Contention
    {
        private Func<Task>? _start;

        public bool ContenderWasBlocked { get; private set; }

        public Task? Contender { get; private set; }

        public void Arm(Func<Task> start) => _start = start;

        public void Run()
        {
            var start = Interlocked.Exchange(ref _start, null);

            if (start is null)
                return;

            var contender = start();
            ContenderWasBlocked = !contender.IsCompleted;
            Contender = contender;
        }
    }

    /// <summary>Thrown by <see cref="TripwireRepository"/> once a bounded number of lookups has been exceeded.</summary>
    private sealed class TripwireException(int limit)
        : Exception($"A walk over the object graph asked for more than {limit} objects — it does not terminate.");

    /// <summary>
    /// The real durable stack — <see cref="PersistenceStore"/> on a
    /// throwaway directory, and the real document, state and attachment
    /// content stores over it.
    /// </summary>
    private sealed class DurableFixture : IDisposable
    {
        private readonly string _root;

        private DurableFixture(string root, EngineeringDomainContext context, EngineeringDocumentStore documents, EngineeringObjectStateStore states, AttachmentContentStore content)
        {
            _root = root;
            Context = context;
            Documents = documents;
            States = states;
            Content = content;
        }

        public EngineeringDomainContext Context { get; }
        public EngineeringDocumentStore Documents { get; }
        public EngineeringObjectStateStore States { get; }
        public AttachmentContentStore Content { get; }

        public static DurableFixture Create(string label)
        {
            var root = ProjectFixtureRoot.NewIsolatedRoot(label);
            var configuration = new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                [
                    new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root),
                ]))
                .Build();

            var store = new PersistenceStore(configuration);
            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(store, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);
            var states = new EngineeringObjectStateStore(store);
            var content = new AttachmentContentStore(store);

            var context = new EngineeringDomainContext(
                documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, states, content);

            return new DurableFixture(root, context, documents, states, content);
        }

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // Cleanup only — never mask a real failure with one.
            }
        }
    }

    /// <summary>
    /// An in-memory stack whose stores can be probed, parked and made to
    /// fail, so that every interleaving in this file is forced in program
    /// order rather than raced.
    /// </summary>
    private sealed class ProbeRig
    {
        public ProbeRig()
        {
            var principal = new CurrentPrincipalAccessor();
            Repository = new TripwireRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, Repository);

            Documents = new ProbeDocumentStore(new InMemoryEngineeringDocumentStore(principal));
            States = new ProbeStateStore();
            Content = new ProbeContentStore();

            Context = new EngineeringDomainContext(
                Documents, Repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, Repository), principal, States, Content, WriteIntents);

        }

        public EngineeringDomainContext Context { get; }
        public TripwireRepository Repository { get; }
        public ProbeDocumentStore Documents { get; }
        public ProbeStateStore States { get; }
        public ProbeContentStore Content { get; }
        public FakeWriteIntentStore WriteIntents { get; } = new();

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

    /// <summary>
    /// The real in-memory repository, with a bounded lookup budget so a
    /// non-terminating walk over the object graph is reported rather than
    /// run.
    /// </summary>
    private sealed class TripwireRepository : IEngineeringObjectRepository
    {
        private readonly InMemoryEngineeringObjectRepository _inner = new();
        private int _limit = int.MaxValue;
        private int _lookups;

        public void TripAfter(int limit)
        {
            _limit = limit;
            _lookups = 0;
        }

        public void Register(IEngineeringObject engineeringObject) => _inner.Register(engineeringObject);

        public Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _lookups) > _limit)
                throw new TripwireException(_limit);

            return _inner.FindAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<IEngineeringObject>> ListByKindAsync(string kind, CancellationToken cancellationToken = default) =>
            _inner.ListByKindAsync(kind, cancellationToken);

        public Task<IReadOnlyList<IEngineeringObject>> ListAllAsync(CancellationToken cancellationToken = default) =>
            _inner.ListAllAsync(cancellationToken);
    }

    /// <summary>
    /// The real in-memory document store, with a one-shot park inside
    /// <see cref="ReviseAsync"/> — which runs inside <c>ReviseAsync</c>'s
    /// own hold of the object write lock and before <c>CaptureState</c> —
    /// and a probe on the durable link write.
    /// </summary>
    private sealed class ProbeDocumentStore(InMemoryEngineeringDocumentStore inner) : IEngineeringDocumentStore
    {
        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;

        public Contention LinkWrite { get; } = new();

        public Task ArmNextRevise()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }

        public void ReleaseRevise() => _release?.TrySetResult();

        public Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(kind, initialContent, cancellationToken);

        public Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            inner.FindAsync(documentId, cancellationToken);

        public async Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default)
        {
            var entered = Interlocked.Exchange(ref _entered, null);

            if (entered is not null)
            {
                entered.TrySetResult();
                await _release!.Task.ConfigureAwait(false);
            }

            return await inner.ReviseAsync(documentId, newContent, changeSummary, cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            inner.GetRevisionHistoryAsync(documentId, cancellationToken);

        public Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default)
        {
            LinkWrite.Run();

            return inner.LinkAsync(sourceDocumentId, targetDocumentId, relationshipKind, cancellationToken);
        }

        public Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            inner.GetReferencesAsync(documentId, cancellationToken);
    }

    /// <summary>An object state store that can be probed and made to fail one write.</summary>
    private sealed class ProbeStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();
        private bool _failNext;

        public Contention StateWrite { get; } = new();

        /// <summary>The next <see cref="SaveAsync"/> throws the ordinary durable-store failure every store in this platform can raise.</summary>
        public void FailNextSave() => _failNext = true;

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            StateWrite.Run();

            if (_failNext)
            {
                _failNext = false;
                throw new IOException("The state record could not be written.");
            }

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

    /// <summary>An attachment content store that records whether the object write lock was held during the content write.</summary>
    private sealed class ProbeContentStore : IAttachmentContentStore
    {
        private readonly Dictionary<Guid, byte[]> _content = new();

        public Contention ContentWrite { get; } = new();

        public IReadOnlyCollection<Guid> StoredKeys
        {
            get { lock (_content) { return _content.Keys.ToList(); } }
        }

        public Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            ContentWrite.Run();

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
            lock (_content) { _content.Remove(attachmentId); }
            return Task.CompletedTask;
        }
    }

    /// <summary>A write-intent store, so the marker outcomes in §3 are observable.</summary>
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

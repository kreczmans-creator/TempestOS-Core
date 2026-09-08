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
    // §3 THE BOUNDARY OF THE INVARIANT — round 2 claimed it for one
    //    exception type; `WP 16.4B-R7` closed the rest (`TD-143`)
    //
    // These four facts were written by round 2's independent verifier as
    // CHARACTERISATIONS: they pinned the boundary of round 2's invariant
    // by asserting that a mutation whose durable write FAILED kept its
    // in-memory mutation on all seven mutators, and that the object's next
    // successful write made it durable. `TD-143` recorded that, and
    // `WP 16.4B-R7` fixed it.
    //
    // Per this file's own standing instruction, they are INVERTED and NOT
    // DELETED. Each one now asserts the opposite outcome, on the same rig,
    // through the same failure, and each names in its remarks what it used
    // to assert. From here on they are guard-rails: any regression of the
    // undo in `EngineeringObjectBase.RollBackOnFailureAsync` turns them red
    // again.
    //
    // §3b then pins what R7 deliberately did NOT do, which is where the
    // next reader is most likely to over-read the fix: it does not undo
    // durable content or relationships, and it does not claim atomicity
    // against a store that commits and then throws.
    // ================================================================

    /// <summary>
    /// A move whose durable <c>groupedUnder</c> link write fails leaves the
    /// parent where it was, and the object's next successful write of any
    /// kind carries nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>INVERTED by `WP 16.4B-R7` (`TD-143`); it was a characterisation
    /// and is now a guard-rail.</b> It previously asserted the opposite,
    /// under the name
    /// <c>AMoveWhoseDurableLinkWriteFails_StillReparentsTheInstance_AndTheNextWriteMakesItDurable</c>:
    /// that <c>part.ParentId</c> was the stray id after the failure and
    /// that the following rename made it durable. Both assertions are kept
    /// below, negated, so the fix is checked at exactly the point the
    /// defect was measured.
    /// </para>
    /// <para>
    /// Nothing here is contrived: the target simply has no document, which
    /// <c>GuardAgainstCircularParentAsync</c> does not detect (an unknown
    /// id is not <c>IHasParent</c>, so the walk returns) and which
    /// <c>EngineeringDocumentStore</c>/<c>InMemoryEngineeringDocumentStore</c>
    /// both answer with <see cref="EngineeringDocumentNotFoundException"/>
    /// — from <em>inside</em> the write lock, after <c>_parentId</c> has
    /// already been assigned and before the state write. An I/O failure of
    /// the same durable write behaves identically.
    /// </para>
    /// <para>
    /// <b>Scope.</b> This is the in-memory half of what the register
    /// records as `TD-144`, closed here as a consequence of `TD-143`'s fix
    /// rather than as a separate piece of work — the undo cannot sensibly
    /// distinguish which of a move's two durable writes failed. It does not
    /// close what happens when the link write <em>succeeds</em> and the
    /// state write then fails; that is
    /// <see cref="AMoveWhoseStateWriteFailsAfterItsLinkWrite_RestoresTheParent_ButLeavesTheGroupedUnderLink"/>,
    /// below, and it is not closed.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> remove the <c>RollBackOnFailureAsync</c>
    /// wrapper from <c>MoveAsync</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMoveWhoseDurableLinkWriteFails_LeavesTheParentWhereItWas_AndTheNextWriteCarriesNothing()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var strayParentId = Guid.NewGuid();

        var failure = await Record.ExceptionAsync(() => part.MoveAsync(strayParentId).WaitAsync(Timeout));
        Assert.IsType<EngineeringDocumentNotFoundException>(failure);

        // The caller was told the move failed. The instance agrees.
        Assert.Null(part.ParentId);

        // The move's own write never happened...
        var afterMove = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterMove);
        Assert.Null(afterMove.ParentId);

        // ...and the next unrelated, successful operation has nothing to
        // carry to disk.
        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);

        var afterRename = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRename);
        Assert.Null(afterRename.ParentId);
        Assert.Equal("Renamed after the failed move", afterRename.DisplayName);
    }

    /// <summary>
    /// A lifecycle transition whose durable write fails stamps nothing into
    /// the append-only transition history, and the next successful write
    /// has no entry to make durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>INVERTED by `WP 16.4B-R7` (`TD-143`); it was the
    /// highest-consequence characterisation in §3 and is now the
    /// highest-consequence guard-rail.</b> It previously asserted, under
    /// the name
    /// <c>ATransitionWhoseDurableWriteFails_StillStampsTheAuditEntry_AndTheNextWriteMakesItDurable</c>,
    /// that a fabricated <c>LifecycleTransitionRecord</c> carrying a real
    /// actor principal id and target state survived the failure and was
    /// made durable by the following rename. `TD-140` had closed exactly
    /// that outcome for the supersession refusal, on the stated ground that
    /// the transition history is this platform's governance record and has
    /// no removal path; both grounds held identically for a failing write,
    /// and only the exception type differed. The assertions below are the
    /// same ones, negated — including the checks on the entry's actor and
    /// target state, so a fix that merely emptied the list would not be
    /// mistaken for one that never wrote the entry.
    /// </para>
    /// <para>
    /// The failing write is an ordinary <see cref="IOException"/> from the
    /// state store — the class every durable store in this platform can
    /// raise, and the class `WP 16.4B-R5` was told not to compensate for.
    /// The undo is not a removal path for a recorded transition: it
    /// restores <c>_history</c> to the contents it had moments earlier,
    /// inside the hold of the write lock that appended to it, so no reader
    /// and no later write ever sees the entry.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> remove the <c>RollBackOnFailureAsync</c>
    /// wrapper from <c>MutateAndPersistAsync</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ATransitionWhoseDurableWriteFails_StampsNoAuditEntry_AndTheNextWriteMakesNothingDurable()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        // The caller was told the transition failed. The audit trail agrees:
        // no entry exists, so no removal path is needed for one.
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);

        await part.RenameAsync("Renamed after the failed transition").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Draft, state.Status);
        Assert.Empty(state.History);

        // And the object is still usable: the failure undid its own
        // mutation, it did not retire the instance.
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        var afterRetry = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRetry);
        var recorded = Assert.Single(afterRetry.History);
        Assert.Equal(LifecycleState.Draft, recorded.From);
        Assert.Equal(LifecycleState.InReview, recorded.To);
        Assert.False(string.IsNullOrWhiteSpace(recorded.ActorPrincipalId));
    }

    /// <summary>
    /// A delete whose durable write fails leaves the instance undeleted —
    /// the outcome `TD-140` describes as removing the object from the whole
    /// product with no supported way back — and the next successful write
    /// makes nothing durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>INVERTED by `WP 16.4B-R7` (`TD-143`).</b> It previously asserted,
    /// under the name
    /// <c>ADeleteWhoseDurableWriteFails_StillSoftDeletesTheInstance_AndTheNextWriteMakesItDurable</c>,
    /// that <c>part.IsDeleted</c> was <see langword="true"/> after the
    /// failure and that the following rename made it durable. The `TD-140`
    /// reasoning about <c>DeleteAsync</c> — one writer, no undelete
    /// anywhere, twenty-one read models filtering
    /// <c>IDeletable { IsDeleted: true }</c> — is a property of the flag and
    /// not of the exception that interrupts the write, which is why the
    /// failure route had to be closed as well as the refusal route.
    /// </para>
    /// <para>
    /// The `TD-97` byte release is still skipped, and that is now the
    /// correct outcome rather than a second defect: the object is not
    /// deleted, so its attachment content must not be released. The
    /// assertion on <c>StoredKeys</c> is kept for exactly that reason.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> remove the <c>RollBackOnFailureAsync</c>
    /// wrapper from <c>MutateAndPersistAsync</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ADeleteWhoseDurableWriteFails_LeavesTheInstanceUndeleted_AndTheNextWriteMakesNothingDurable()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.DeleteAsync().WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        Assert.False(part.IsDeleted, "A delete the caller was told had failed left the object soft-deleted.");

        // Still correct, and now for the right reason: the object is not
        // deleted, so its content must not have been released.
        Assert.Contains(attachment.Id, rig.Content.StoredKeys);

        await part.RenameAsync("Renamed after the failed delete").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.False(state.IsDeleted);

        // And a delete that is then retried against a healthy store works,
        // releasing the content exactly as it always did — the undo did not
        // leave the object in a state that cannot be deleted.
        await part.DeleteAsync().WaitAsync(Timeout);

        var afterRetry = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRetry);
        Assert.True(afterRetry.IsDeleted);
        Assert.DoesNotContain(attachment.Id, rig.Content.StoredKeys);
    }

    /// <summary>
    /// The same failure boundary, on the four remaining mutators: each
    /// undoes its in-memory mutation when the durable write fails — and
    /// <c>AttachContentAsync</c>'s durable content and marker are still
    /// left exactly where they were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>INVERTED by `WP 16.4B-R7` (`TD-143`).</b> It previously asserted,
    /// under the name
    /// <c>TheOtherFourMutators_AlsoKeepTheirMutation_WhenTheDurableWriteFails</c>,
    /// that each of these four kept its mutation. Stated for the whole set
    /// rather than one example, so that a register row derived from this
    /// file cannot describe the boundary as a quirk of one method.
    /// </para>
    /// <para>
    /// <b>The last three assertions are unchanged on purpose and are the
    /// most important lines in the fact.</b> The content bytes and the
    /// write-intent marker written by the failed <c>AttachContentAsync</c>
    /// are still there. R7 undoes an in-memory mutation and deletes nothing
    /// durable — precisely the `WP 16.4B-R5` compensation the fifth review
    /// board proved destroys content a live successor references. A "fix"
    /// that also cleaned up the bytes would turn these three lines red, and
    /// should.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TheOtherFourMutators_AlsoUndoTheirMutation_WhenTheDurableWriteFails_AndDeleteNothingDurable()
    {
        var rig = new ProbeRig();

        var renamed = await rig.CreatePartAsync("PRT-1", "Bracket");
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => renamed.RenameAsync("Leaked name").WaitAsync(Timeout)));
        Assert.Equal("Bracket", renamed.DisplayName);

        var bommed = await rig.CreatePartAsync("PRT-2", "Housing");
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => bommed.SetBomLineAsync(17m, "each", "FN-9").WaitAsync(Timeout)));
        Assert.Equal(1m, bommed.Quantity);
        Assert.Null(bommed.FindNumber);

        var attached = await rig.CreatePartAsync("PRT-3", "Plate");
        var phantom = new Attachment("phantom.txt", "text/plain", 3);
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => attached.AttachAsync(phantom).WaitAsync(Timeout)));
        Assert.DoesNotContain(await attached.GetAttachmentsAsync(), a => a.Id == phantom.Id);

        var contented = await rig.CreatePartAsync("PRT-4", "Cover");
        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => contented.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout)));
        Assert.Empty(await contented.GetAttachmentsAsync());

        // Unchanged, and deliberately so: nothing durable is undone.
        Assert.Single(rig.Content.StoredKeys);
        Assert.Single(await rig.WriteIntents.ListMarkedAsync());
    }

    // ================================================================
    // §3b THE EDGE OF `WP 16.4B-R7` — what the undo deliberately does NOT
    //     do, and the one case it cannot get right
    //
    // Every fact here exists so that the next reader cannot mistake
    // `TD-143`'s closure for a claim of atomicity. Two are guard-rails on
    // the fix's own restraint; one is a characterisation of a residue that
    // is accepted and stated rather than compensated; one is a
    // characterisation of the single case the design cannot resolve and
    // says so in its own remarks.
    // ================================================================

    /// <summary>
    /// A move whose <c>groupedUnder</c> link write <b>succeeds</b> and
    /// whose state write then fails restores the parent — and leaves the
    /// relationship behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation of an accepted residue, new with
    /// `WP 16.4B-R7`.</b> This is the half of a failed move that cannot be
    /// undone honestly. The platform has no path that removes a
    /// relationship — <c>MoveAsync</c>'s own remarks have said so since
    /// `WP 9.0A`, because the <c>groupedUnder</c> trail is deliberately an
    /// append-only move history — so removing this one would be a delete of
    /// durable state without proven ownership, which is the shape of the
    /// `WP 16.4B-R5` regression the fifth review board proved destroys
    /// data. It is therefore left.
    /// </para>
    /// <para>
    /// <b>What the residue does and does not assert.</b> The trail already
    /// contains, by design, every superseded <c>groupedUnder</c> edge of
    /// every earlier move, so an extra edge is not a claim about the
    /// current parent — <c>ParentId</c> alone answers that, and the undo
    /// has restored it, which is what the first two assertions check. What
    /// is left is one edge in a history, indistinguishable from a
    /// historical one.
    /// </para>
    /// <para>
    /// Reversing the two durable steps would remove this residue and buy
    /// something strictly worse: a link write failing after the state write
    /// had committed would report failure for a move that had landed
    /// durably, which no in-memory undo can answer at all. That trade is
    /// recorded in <c>MoveAsync</c>'s own block comment.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMoveWhoseStateWriteFailsAfterItsLinkWrite_RestoresTheParent_ButLeavesTheGroupedUnderLink()
    {
        var rig = new ProbeRig();
        var parent = await rig.CreatePartAsync("PRT-P", "Housing");
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.MoveAsync(parent.Id).WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        // The reparent is undone, in memory and on disk, and no later
        // write can carry it.
        Assert.Null(part.ParentId);
        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Null(state.ParentId);

        // The relationship, however, was written before the state write and
        // is NOT removed. Asserted rather than tidied away.
        var relationships = await part.GetRelationshipsAsync();
        Assert.Contains(relationships, r => r.TargetId == parent.Id && r.RelationshipKind == "groupedUnder");
    }

    /// <summary>
    /// A store that <b>commits and then throws</b> does not have its
    /// mutation undone — because undoing it is what would create the
    /// divergence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail, and the fact that kills the naive fix.</b> The
    /// obvious way to close `TD-143` is to catch and revert unconditionally.
    /// That is wrong, and not hypothetically: this platform's own
    /// <c>PersistenceStore.WriteAsync</c> ran a legacy-record cleanup
    /// <em>after</em> its <c>File.Move</c> commit point until
    /// `WP 16.4B-R7`, and reported a failure of it with the same exception
    /// type it raises before the commit. An unconditional revert in that
    /// window reverts a change that landed, and because the revert is only
    /// in memory, a restart rehydrates the committed value.
    /// </para>
    /// <para>
    /// <c>EngineeringObjectBase.RollBackOnFailureAsync</c> therefore
    /// re-reads the record and undoes only what the record does not show.
    /// This fact drives exactly that: the save lands and then throws, and
    /// the instance must keep the mutation, because the disk has it.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> make the undo unconditional (drop the
    /// <c>DurableRecordAlreadyShowsThisStateAsync</c> check). Verified:
    /// this fact fails and
    /// <see cref="AttachmentContentReconciliationServiceTests"/>'s stale-marker
    /// fact fails with it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMutationWhoseStoreCommitsAndThenThrows_IsNotUndone_BecauseTheRecordShowsIt()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.CommitThenFailNextSave();

        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Committed then reported failed").WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        // The write landed. The instance must not disagree with it.
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Committed then reported failed", state.DisplayName);
        Assert.Equal("Committed then reported failed", part.DisplayName);
    }

    /// <summary>
    /// A store that commits, throws, and whose re-read then <b>fails</b>
    /// has its mutation undone anyway — and the instance therefore
    /// disagrees with its own durable record until a restart rehydrates it.
    /// One member of a family; see the remarks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation of a stated limitation, not a defect this Work
    /// Package hid.</b> <c>RollBackOnFailureAsync</c> decides on evidence,
    /// and here there is none: the write failed and the re-read that would
    /// settle whether it landed fails too — the most likely cause of both
    /// being a store that is simply unavailable. It must either undo or not
    /// undo, and it undoes, because "the operation did not happen" is the
    /// far likelier reading and because not undoing is the `TD-143`
    /// behaviour with a demonstrated harm.
    /// </para>
    /// <para>
    /// The consequence is asserted here rather than left to a reader's
    /// inference: memory and disk disagree, and no later write on this
    /// object repairs it, because the object's own state no longer carries
    /// the change. <b>This is the reason `WP 16.4B-R7` does not claim
    /// atomicity.</b>
    /// </para>
    /// <para>
    /// <b>A FAILING RE-READ IS ONLY ONE MEMBER OF THE FAMILY, and this
    /// fact must not be read as bounding it (`WP 16.4B-R7` round 2,
    /// `B-F2`).</b> The real condition is "the write landed and the re-read
    /// does not confirm it", which a perfectly healthy store can satisfy:
    /// see <see cref="AMutationWhoseStoreCommitsAndThenAnswersNoRecord_IsUndoneAnyway"/>
    /// for the route the shipped <c>EngineeringObjectStateStore</c> itself
    /// takes — it answers <see langword="null"/>, with a warning and no
    /// exception, for a record that is present but unparseable or at an
    /// unmigratable schema version — and a stale read from a caching store
    /// does the same. The mirror case, where the evidence test is satisfied
    /// by a record this object never wrote and the undo is wrongly
    /// declined, is real too: the per-object lock is per-
    /// <c>EngineeringDomainContext</c> and per-process. All of it is
    /// enumerated on <c>RollBackOnFailureAsync</c>.
    /// </para>
    /// <para>
    /// If a later board decides this must be closed — by a durable undo
    /// log, or by refusing to serve an instance whose durable state is
    /// unknown — this test is where that decision lands, and it must be
    /// inverted rather than deleted.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMutationWhoseStoreCommitsThrowsAndWhoseReReadFails_IsUndoneAnyway_LeavingTheInstanceDisagreeingWithDisk()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.CommitThenFailNextSave();
        rig.States.FailReads = true;

        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Committed but unverifiable").WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        rig.States.FailReads = false;

        // The write landed...
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Committed but unverifiable", state.DisplayName);

        // ...and the instance, unable to establish that, undid it.
        Assert.Equal("Bracket", part.DisplayName);
    }

    /// <summary>
    /// The undo restores the <b>same</b> attachment and history instances
    /// the object already held, not equal-valued copies of them.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> The obvious way to build the undo is to capture
    /// an <c>EngineeringObjectState</c> and put it back through
    /// <c>RestoreState</c>. That would silently replace a caller's own
    /// <c>IAttachment</c> implementation with this assembly's
    /// <c>Attachment</c> and swap every history record for an equal-valued
    /// copy — a rollback that is observable, which is not a rollback.
    /// <c>MutationRollbackPoint</c> exists for this reason and this fact is
    /// what holds it there. <b>Mutant that kills it:</b> implement
    /// <c>RollBackTo</c> as <c>RestoreState(capturedState)</c>.
    /// </remarks>
    [Fact]
    public async Task TheUndoRestoresTheSameInstances_NotEqualValuedCopies()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var original = new Attachment("kept.txt", "text/plain", 3);
        await part.AttachAsync(original).WaitAsync(Timeout);
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        var historyBefore = Assert.Single(part.History);

        rig.States.FailNextSave();
        Assert.IsType<IOException>(await Record.ExceptionAsync(
            () => part.AttachAsync(new Attachment("rolled-back.txt", "text/plain", 3)).WaitAsync(Timeout)));

        var survivor = Assert.Single(await part.GetAttachmentsAsync());
        Assert.Same(original, survivor);
        Assert.Same(historyBefore, Assert.Single(part.History));
    }

    /// <summary>
    /// A store that commits, throws, and then answers the re-read with
    /// <b>no record at all</b> — without failing — has its mutation undone
    /// just as wrongly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Characterisation, new in `WP 16.4B-R7` round 2 (`B-F2b`). This
    /// fact exists because the first version of this Work Package's report
    /// bounded the residual risk as requiring a store that is
    /// "unreadable", and that was materially narrower than the truth.</b>
    /// The shipped <c>EngineeringObjectStateStore.Deserialise</c> returns
    /// <see langword="null"/>, with a logged warning and no exception, for
    /// a record that is present but unparseable, or at a schema version
    /// this build has no migration path to. <c>DurableRecordAlreadyShows-
    /// ThisStateAsync</c> reads <see langword="null"/> as "the write did
    /// not land". So a perfectly healthy, perfectly readable store reaches
    /// the identical wrong undo.
    /// </para>
    /// <para>
    /// The real condition is <b>"the write landed and the re-read does not
    /// confirm it"</b>. Same standing instruction as its sibling: if a
    /// later board closes this, invert it, do not delete it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMutationWhoseStoreCommitsAndThenAnswersNoRecord_IsUndoneAnyway()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.CommitThenFailNextSave();
        rig.States.ReadsNothing = true;

        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Committed but unconfirmed").WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        rig.States.ReadsNothing = false;

        // The write landed, the store never failed a read, and the
        // instance undid it anyway.
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Committed but unconfirmed", state.DisplayName);
        Assert.Equal("Bracket", part.DisplayName);
    }

    /// <summary>
    /// A throw from the <b>evidence step itself</b> — the comparison, or
    /// the state re-capture that feeds it — leaves the caller's real
    /// exception intact and still undoes the mutation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail, new in `WP 16.4B-R7` round 2 (`B-F1`), and it pins a
    /// RED that was in the shipped fix.</b> The first version of
    /// <c>DurableRecordAlreadyShowsThisStateAsync</c> wrapped only
    /// <c>store.FindAsync</c> in its <c>try</c> and left the comparison —
    /// and the <c>CaptureState()</c> feeding it — outside. The whole method
    /// runs inside <c>RollBackOnFailureAsync</c>'s <c>catch</c>, so a throw
    /// there replaced the caller's real exception with the diagnostic's,
    /// <b>skipped <c>RollBackTo</c></b>, and skipped the rethrow —
    /// reinstating `TD-143` in full on the path built to close it.
    /// </para>
    /// <para>
    /// <b>The instrument is not contrived.</b> A state record whose
    /// <c>History</c> is <see langword="null"/> is what the shipped
    /// <c>EngineeringObjectStateStore</c> really returns, non-null, for a
    /// record whose JSON lacks that property: its <c>Deserialise</c>
    /// catches only <c>JsonException</c> and
    /// <c>EngineeringObjectState</c>'s collection members are ordinary
    /// non-<c>required</c> positional parameters. A foreign or hand-edited
    /// record, an <c>IStateMigration</c> that returns one, or any
    /// third-party <c>IEngineeringObjectStateStore</c> all produce it.
    /// <c>AttachAsync</c> is the mutator that reaches it most readily,
    /// because an attach changes no scalar field and the <c>&amp;&amp;</c>
    /// chain therefore runs all the way to the collection comparison.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> move the <c>return persisted is not
    /// null &amp;&amp; HoldsTheSameMutableState(...)</c> line back outside
    /// the <c>try</c>. Verified: the caller then receives
    /// <c>ArgumentNullException</c>, the attachment survives, and the next
    /// write makes it durable.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AThrowFromTheEvidenceStep_LeavesTheCallersExceptionIntact_AndStillUndoesTheMutation()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var phantom = new Attachment("phantom.txt", "text/plain", 3);

        rig.States.ReadsHollowRecords = true;
        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.AttachAsync(phantom).WaitAsync(Timeout));

        // The store's own failure, not the evidence step's.
        Assert.IsType<IOException>(failure);

        // And the undo still ran.
        Assert.DoesNotContain(await part.GetAttachmentsAsync(), a => a.Id == phantom.Id);

        rig.States.ReadsHollowRecords = false;
        await part.RenameAsync("Renamed after the failed attach").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.DoesNotContain(state.Attachments, a => a.Id == phantom.Id);
    }

    /// <summary>
    /// The same, on the path where the throwing comparison is reached with
    /// <b>certainty</b>: an ordinary validation rejection, which mutates
    /// nothing, so every scalar field matches and the comparison always
    /// runs to the collections.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail (`B-F1`, `B-F4`).</b> An impermissible lifecycle
    /// transition is a routine, user-facing outcome — five `Tempest.App`
    /// command handlers turn it into an ordinary failure result — and it is
    /// the one class of failure on which the pre-fix defect fired every
    /// single time rather than occasionally. The caller must receive
    /// <see cref="InvalidLifecycleTransitionException"/> and nothing else.
    /// </remarks>
    [Fact]
    public async Task AValidationRejection_AgainstAHollowRecord_StillRaisesItsOwnException()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.ReadsHollowRecords = true;

        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Released).WaitAsync(Timeout));

        Assert.IsType<InvalidLifecycleTransitionException>(failure);
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);
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
        private bool _commitThenFailNext;

        public Contention StateWrite { get; } = new();

        /// <summary>
        /// Reads throw instead of answering — the store that cannot say
        /// whether its own last write landed (`WP 16.4B-R7`, §3b).
        /// </summary>
        public bool FailReads { get; set; }

        /// <summary>
        /// Reads answer "no record" without failing — what the shipped
        /// <c>EngineeringObjectStateStore</c> does, with a warning and no
        /// exception, for a record that is present but unparseable or at a
        /// schema version it has no migration path to (`WP 16.4B-R7`
        /// round 2, `B-F2b`).
        /// </summary>
        public bool ReadsNothing { get; set; }

        /// <summary>
        /// Reads answer a NON-NULL record whose <c>History</c> is
        /// <see langword="null"/> — which the shipped
        /// <c>EngineeringObjectStateStore</c> really does return for a
        /// record whose JSON lacks that property, since its
        /// <c>Deserialise</c> catches only <c>JsonException</c> and
        /// <c>EngineeringObjectState</c>'s collection members are ordinary
        /// non-<c>required</c> positional parameters (`WP 16.4B-R7`
        /// round 2, `B-F1`).
        /// </summary>
        public bool ReadsHollowRecords { get; set; }

        /// <summary>The next <see cref="SaveAsync"/> throws the ordinary durable-store failure every store in this platform can raise.</summary>
        public void FailNextSave() => _failNext = true;

        /// <summary>
        /// The next <see cref="SaveAsync"/> stores the record and
        /// <em>then</em> throws — the post-commit failure window
        /// <c>PersistenceStore.WriteAsync</c> itself had until
        /// `WP 16.4B-R7`, and the case an unconditional in-memory undo gets
        /// wrong (`TD-143`).
        /// </summary>
        public void CommitThenFailNextSave() => _commitThenFailNext = true;

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            StateWrite.Run();

            if (_failNext)
            {
                _failNext = false;
                throw new IOException("The state record could not be written.");
            }

            lock (_states) { _states[state.Id] = state; }

            if (_commitThenFailNext)
            {
                _commitThenFailNext = false;
                throw new IOException("The state record landed, and the write then failed anyway.");
            }

            return Task.CompletedTask;
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (FailReads)
                throw new IOException("The state record could not be read.");

            if (ReadsNothing)
                return Task.FromResult<EngineeringObjectState?>(null);

            lock (_states)
            {
                if (!_states.TryGetValue(id, out var state))
                    return Task.FromResult<EngineeringObjectState?>(null);

                if (ReadsHollowRecords)
                    state = state with { History = null! };

                return Task.FromResult<EngineeringObjectState?>(state);
            }
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

}

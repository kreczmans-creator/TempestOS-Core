using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// <b>Independent adversarial verification of the one write path.</b> These
/// facts exist to <em>falsify</em> `ADR-0145`, not to demonstrate it. They
/// attack the parts of it that the engineer's own suite does not reach: the
/// deadlock surface a single, domain-wide, non-reentrant lock creates, the
/// exact boundary of the invariant the ADR claims, and the paths earlier
/// rounds deliberately left alone (`TD-141`, `TD-142`, `TD-145`, `TD-146`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read this before treating any fact here as a regression.</b> The
/// facts in this file fall into two kinds and they must not be confused:
/// </para>
/// <list type="bullet">
/// <item><description><b>Guard-rails</b> — these assert what the platform claims. They must stay green for ever.</description></item>
/// <item><description><b>Characterisations</b> — these assert what the platform <em>currently does</em>, including where that is wrong. Every one of them names the defect it pins and the register row that owns it. <b>When a defect here is fixed, invert the assertion — do not delete the fact.</b></description></item>
/// </list>
/// <para>
/// <b>What `WP 17.1B` did to this file.</b> Every characterisation it
/// carried has been closed, and each is inverted here rather than removed,
/// as this file's own standing instruction requires: `TD-141`
/// (<c>LinkAsync</c> unguarded on a retired instance, §5), `TD-142` (the
/// type-specific mutators writing their field before persisting, §6),
/// `TD-145` (the circular-parent guard running outside the lock) and
/// `TD-146` (the live-children check running outside it, both §4). Each
/// names in its own remarks what it used to assert.
/// </para>
/// <para>
/// <b>What was deleted rather than inverted, and why.</b> Three facts
/// pinned decisions <em>inside</em> the compensating undo —
/// <c>RollBackOnFailureAsync</c>,
/// <c>DurableRecordAlreadyShowsThisStateAsync</c> and the evidence
/// comparison it turned on. A mutator does not mutate and then undo; it
/// projects inside the transaction, commits, and applies afterwards. Those
/// facts had no behaviour left to describe. The one fact that pinned the
/// non-terminating ancestry walk is gone with them: it could only be
/// reached through a parent cycle, and a cycle can no longer be formed
/// through the public surface (§4).
/// </para>
/// <para>
/// <b>Determinism.</b> Nothing here asserts the outcome of a race. Every
/// interleaving is forced by parking one writer — at the end of its
/// transaction body with <see cref="GatedPersistenceStore"/>, all writes
/// staged and both the domain lock and the store's writer lock held — and
/// starting the second on the calling thread behind it, where it is
/// provably blocked on the one domain write lock. The only timed values in
/// the file are upper bounds that turn a hang into a reported failure; no
/// fact passes <em>because</em> of one.
/// </para>
/// </remarks>
public sealed class MutatorRefusalAdversarialTests
{
    /// <summary>
    /// Long enough that no machine fails one of these by being slow, short
    /// enough that a self-deadlock on the non-reentrant domain write lock
    /// is a failed fact rather than a hung test run. Nothing asserts a
    /// value measured against it.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] Bytes = [7, 8, 9];

    // ================================================================
    // §1 Deadlock — the principal risk of `ADR-0145`
    //
    // The domain write lock is a single, non-reentrant `SemaphoreSlim`
    // taken by `ExecuteWriteAsync` before the transaction is opened. Any
    // mutator that reaches `ExecuteWriteAsync` from inside one deadlocks
    // against itself, for ever, in production — and because the lock is
    // domain-wide it takes every other write in the process with it.
    // ================================================================

    /// <summary>
    /// All seven mutators, on a real Kind, against the real
    /// <see cref="EngineeringDocumentStore"/>,
    /// <see cref="EngineeringObjectStateStore"/> and
    /// <see cref="AttachmentContentStore"/> — every one completes, and
    /// every one lands durably.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail. This is the fact that fails instead of hanging.</b>
    /// A suite that exercises the mutators only against a retired instance
    /// refuses each of them before it reaches the write path, so a mutator
    /// that re-entered the lock on its <em>success</em> path would never be
    /// detected there. This puts all seven through the path that takes the
    /// lock twice if the change is wrong, and bounds each call, so the
    /// failure mode is a red fact and not a run that never finishes.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> call <c>ExecuteWriteAsync</c> from
    /// inside a transaction body — for instance by having
    /// <c>MoveAsync</c>'s <c>alsoWrite</c> delegate call a mutator rather
    /// than the transactional document writer.
    /// </para>
    /// <para>
    /// The real stores are not decoration. <c>MoveAsync</c> writes its
    /// <c>groupedUnder</c> reference through
    /// <c>ITransactionalDocumentWriter</c>, and <c>AttachContentAsync</c>
    /// writes its payload through <c>ITransactionalAttachmentWriter</c>;
    /// neither contract can be implemented by a hand-written double, which
    /// is why the constructor refuses one.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task EverySevenMutator_CompletesAndPersists_AgainstTheRealDurableStores()
    {
        var rig = new Rig();

        var parent = await rig.CreatePartAsync("PRT-P", "Housing");
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var phantom = new Attachment("metadata-only.txt", "text/plain", 4);

        await part.AttachAsync(phantom).WaitAsync(Timeout);
        var withContent = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);
        await part.RenameAsync("Bracket, revised").WaitAsync(Timeout);
        await part.MoveAsync(parent.Id).WaitAsync(Timeout);
        await part.SetBomLineAsync(4m, "each", "FN-1", "IN-1", "RD-1").WaitAsync(Timeout);
        await part.DeleteAsync().WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Bracket, revised", state.DisplayName);
        Assert.Equal(LifecycleState.InReview, state.Status);
        Assert.Equal(parent.Id, state.ParentId);
        Assert.Equal(4m, state.BomLine.Quantity);
        Assert.True(state.IsDeleted);
        Assert.Equal(2, state.Attachments.Count);
        Assert.Single(state.History);

        // The move's own durable link write landed too, in the same
        // transaction as the state write above.
        var references = await rig.Documents.GetReferencesAsync(part.Id);
        Assert.Contains(references, r => r.TargetDocumentId == parent.Id && r.RelationshipKind == "groupedUnder");

        // And the delete released the bytes, in the same transaction as the
        // record that stopped referencing them.
        Assert.False(rig.HasContent(withContent.Id));
        Assert.False((await rig.Content.ReadAsync(withContent.Id, withContent.ContentHash, withContent.SizeInBytes)).IsAvailable);
    }

    /// <summary>
    /// <c>MoveAsync</c>'s durable <c>groupedUnder</c> link write and its
    /// state write are <b>one transaction</b>: neither is visible until
    /// both are, and nothing else can write while it runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail, and the successor of the fact that asserted the
    /// per-object write lock was held across the link write.</b> There is
    /// no per-object lock to hold. What that fact was really protecting —
    /// that a move's two durable effects cannot be observed apart, and
    /// that no second writer can slip between them — is a property of the
    /// transaction now, and is asserted directly: while the move is parked
    /// with everything staged, the store shows neither the new parent nor
    /// the reference, and a second mutator cannot proceed.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> write the reference through
    /// <c>Store.LinkAsync</c> rather than through
    /// <c>DocumentWriter.LinkAsync</c> — the "keep the link, skip the
    /// transaction" change — which makes the reference visible while the
    /// move is still parked.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMovesLinkWriteAndItsStateWrite_AreOneTransaction()
    {
        var rig = Rig.WithGate(out var gate);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        var parent = await rig.CreatePartAsync("PRT-P", "Housing");

        var parked = gate.ArmNextTransaction();
        var moving = part.MoveAsync(parent.Id);
        await parked;

        // Everything is staged and nothing is committed.
        Assert.Null((await rig.States.FindAsync(part.Id))!.ParentId);
        Assert.Empty(await rig.Documents.GetReferencesAsync(part.Id));

        var contender = part.RenameAsync("Renamed by the contender");
        Assert.False(contender.IsCompleted, "MoveAsync's durable link write ran outside the domain write lock.");

        gate.Release();
        await moving.WaitAsync(Timeout);
        await contender.WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(parent.Id, state.ParentId);
        Assert.Equal("Renamed by the contender", state.DisplayName);
        Assert.Contains(
            await rig.Documents.GetReferencesAsync(part.Id),
            r => r.TargetDocumentId == parent.Id && r.RelationshipKind == "groupedUnder");
    }

    /// <summary>
    /// <b>The write lock is domain-wide.</b> A mutator on a completely
    /// unrelated object cannot proceed while another object's transaction
    /// is open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail, and the one measurement this file makes about the
    /// cost of `ADR-0145`'s design.</b> Its predecessor asserted that a
    /// mutator held <em>this object's</em> lock across its own state
    /// write. The lock is not per object any more, and the ADR records
    /// that as a decision rather than an oversight: one person, one
    /// process, one database file. The consequence is asserted here rather
    /// than assumed, because it is the cost — every durable write in the
    /// product is serialised behind every other one, including across
    /// projects that share nothing.
    /// </para>
    /// <para>
    /// <b>Mutant that kills it:</b> key the lock by object Id.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TheWriteLockIsDomainWide_AndBlocksAMutatorOnAnUnrelatedObject()
    {
        var rig = Rig.WithGate(out var gate);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        var unrelated = await rig.CreatePartAsync("PRT-2", "Nothing to do with the first");

        var parked = gate.ArmNextTransaction();
        var transitioning = part.TransitionAsync(LifecycleState.InReview);
        await parked;

        var onAnotherObject = unrelated.SetBomLineAsync(3m);
        Assert.False(onAnotherObject.IsCompleted, "A mutator on an unrelated object ran while another object's transaction was open.");

        // And the parked transaction is invisible until it commits.
        Assert.Equal(LifecycleState.Draft, (await rig.States.FindAsync(part.Id))!.Status);

        gate.Release();
        await transitioning.WaitAsync(Timeout);
        await onAnotherObject.WaitAsync(Timeout);

        Assert.Equal(LifecycleState.InReview, (await rig.States.FindAsync(part.Id))!.Status);
        Assert.Equal(3m, (await rig.States.FindAsync(unrelated.Id))!.BomLine.Quantity);
    }

    /// <summary>
    /// <c>AttachContentAsync</c>'s payload — the largest write in the
    /// platform — goes through the transaction, and is invisible until it
    /// commits.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> Its predecessor asserted that the content write
    /// was inside the per-object lock hold, which is what `WP 16.4B-R6`
    /// bought at the cost of holding a lock across an arbitrarily large
    /// byte write. The bytes are now a BLOB in the same transaction as the
    /// record that names them, so the cost is unchanged — the domain lock
    /// is held across the same write — and the benefit is stronger: not
    /// merely "no revision interleaves", but "no committed reference can
    /// ever name bytes that are not there".
    /// </remarks>
    [Fact]
    public async Task AttachContentAsync_WritesItsBytesInsideItsTransaction()
    {
        var rig = Rig.WithGate(out var gate);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var parked = gate.ArmNextTransaction();
        var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        await parked;

        Assert.Empty(rig.ContentKeys);
        Assert.Empty((await rig.States.FindAsync(part.Id))!.Attachments);

        var contender = part.RenameAsync("Renamed by the contender");
        Assert.False(contender.IsCompleted, "The attachment content write ran outside the domain write lock.");

        gate.Release();
        var attachment = await attaching.WaitAsync(Timeout);
        await contender.WaitAsync(Timeout);

        Assert.True(rig.HasContent(attachment.Id));
        Assert.Single((await rig.States.FindAsync(part.Id))!.Attachments);
        Assert.Equal("Renamed by the contender", part.DisplayName);
    }

    /// <summary>
    /// Sixteen mutations and four revisions, over four objects, all in
    /// flight at once against the real write path: every one of them
    /// finishes.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail — a hang detector, not a race assertion.</b> It
    /// asserts nothing about which call wins; only that the whole set
    /// completes inside a bound. A re-entrant acquire on any of these
    /// paths stops the set completing and this fact reports it. Every
    /// outcome except a deadlock is accepted, and the accepted exception
    /// types are enumerated so an unexpected one is still a failure.
    /// </remarks>
    [Fact]
    public async Task ConcurrentMutatorsAndRevisions_AllComplete_NoneDeadlock()
    {
        var rig = new Rig();

        var parent = await rig.CreatePartAsync("PRT-P", "Housing");

        var parts = new List<Part>();
        for (var i = 0; i < 4; i++)
            parts.Add(await rig.CreatePartAsync($"PRT-{i}", $"Bracket {i}"));

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
    /// either way, with no transaction committed for either refusal.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> Both refusals are adjudicated inside the
    /// transaction, which fixes their precedence: supersession is checked
    /// first, so a retired instance raises
    /// <see cref="SupersededEngineeringObjectException"/> even for a move
    /// the lifecycle table would have rejected anyway. That precedence is
    /// asserted here so it is a decision on the record rather than an
    /// accident a later change can quietly reverse — and so that the
    /// accompanying claim, that neither exception leaves a history entry
    /// behind, is checked on the path where two refusals compete.
    /// </remarks>
    [Fact]
    public async Task ARetiredInstance_RefusesBeforeItAdjudicates_AndKeepsItsHistoryEmpty()
    {
        var rig = new Rig();

        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        var successor = (Part)await part.ReviseAsync("Revised.", "Rev B.");

        var commitsBefore = rig.Store.CommitCount;

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

        // Neither refusal committed anything: each threw out of the
        // transaction body before it could be staged.
        Assert.Equal(commitsBefore, rig.Store.CommitCount);

        await successor.RenameAsync("Written by the successor").WaitAsync(Timeout);
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Empty(state.History);
        Assert.Equal(LifecycleState.Draft, state.Status);
    }

    // ================================================================
    // §3 THE BOUNDARY OF THE INVARIANT — a mutation whose durable write
    //    does not land leaves nothing, anywhere
    //
    // These four facts were written as CHARACTERISATIONS of the boundary
    // of an earlier round's invariant: they asserted that a mutation whose
    // durable write FAILED kept its in-memory mutation, and that the
    // object's next successful write made it durable. `TD-143` recorded
    // that; `WP 16.4B-R7` closed it with a compensating undo, and this
    // file's standing instruction inverted them then rather than deleting
    // them.
    //
    // `ADR-0145` closes it a second time and differently: there is no
    // undo, because a mutator computes its next state inside the
    // transaction and touches memory only after the commit returns. The
    // assertions therefore stand unchanged, and what moved is where the
    // failure is injected — at the COMMIT, after the whole body has run,
    // which is the strongest form available: every write the mutation
    // wanted to make has been made and the only thing that did not happen
    // is the commit.
    // ================================================================

    /// <summary>
    /// A move whose durable <c>groupedUnder</c> link write fails leaves the
    /// parent where it was, leaves no reference, and the object's next
    /// successful write of any kind carries nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guard-rail; it was inverted from a characterisation by
    /// `WP 16.4B-R7` (`TD-143`) and is strengthened here.</b> It once
    /// asserted the opposite, under the name
    /// <c>AMoveWhoseDurableLinkWriteFails_StillReparentsTheInstance_AndTheNextWriteMakesItDurable</c>.
    /// </para>
    /// <para>
    /// Nothing here is contrived: the target simply has no document, which
    /// <c>GuardAgainstCircularParent</c> does not detect (an unknown id is
    /// not <c>IHasParent</c>, so the walk returns) and which the
    /// transactional document writer answers with
    /// <see cref="EngineeringDocumentNotFoundException"/> — from inside the
    /// transaction, after the state record has already been staged. An I/O
    /// failure of the same write behaves identically, and so does a
    /// failure of the commit itself.
    /// </para>
    /// <para>
    /// <b>The reference clause is new and is what `ADR-0145` adds.</b> The
    /// state record and the reference are one write, so a failed move
    /// leaves neither — where `WP 16.4B-R7` could only restore the
    /// in-memory parent and had to leave a reference behind.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMoveWhoseDurableLinkWriteFails_LeavesTheParentWhereItWas_AndTheNextWriteCarriesNothing()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var strayParentId = Guid.NewGuid();
        var commitsBefore = rig.Store.CommitCount;

        var failure = await Record.ExceptionAsync(() => part.MoveAsync(strayParentId).WaitAsync(Timeout));
        Assert.IsType<EngineeringDocumentNotFoundException>(failure);

        // The caller was told the move failed. The instance agrees.
        Assert.Null(part.ParentId);

        // Nothing committed, so the move's own writes never happened...
        Assert.Equal(commitsBefore, rig.Store.CommitCount);

        var afterMove = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterMove);
        Assert.Null(afterMove.ParentId);
        Assert.Empty(await rig.Documents.GetReferencesAsync(part.Id));

        // ...and the next unrelated, successful operation has nothing to
        // carry to disk.
        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);

        var afterRename = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRename);
        Assert.Null(afterRename.ParentId);
        Assert.Equal("Renamed after the failed move", afterRename.DisplayName);
        Assert.Empty(await rig.Documents.GetReferencesAsync(part.Id));
    }

    /// <summary>
    /// A lifecycle transition whose commit fails stamps nothing into the
    /// append-only transition history, writes no audit row, and the next
    /// successful write has no entry to make durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The highest-consequence fact in §3.</b> It once asserted, under
    /// the name
    /// <c>ATransitionWhoseDurableWriteFails_StillStampsTheAuditEntry_AndTheNextWriteMakesItDurable</c>,
    /// that a fabricated <c>LifecycleTransitionRecord</c> carrying a real
    /// actor principal id survived the failure and was made durable by the
    /// following rename. The transition history is this platform's
    /// governance record and has no removal path, so the assertions below
    /// are the same ones negated — including the checks on the entry's
    /// actor and target state, so a fix that merely emptied the list would
    /// not be mistaken for one that never wrote the entry.
    /// </para>
    /// <para>
    /// The failure is now the commit: the state record and the audit row
    /// were both staged and neither landed. <b>Mutant that kills it:</b>
    /// apply the projected state to the instance before
    /// <c>ExecuteWriteAsync</c> returns.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ATransitionWhoseCommitFails_StampsNoAuditEntry_AndTheNextWriteMakesNothingDurable()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var auditBefore = rig.AuditRowsFor(part.Id).Count;

        failing.FailNextCommit = true;

        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout));
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        // The caller was told the transition failed. The governance record
        // agrees: no entry exists, so no removal path is needed for one.
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);
        Assert.Equal(auditBefore, rig.AuditRowsFor(part.Id).Count);

        await part.RenameAsync("Renamed after the failed transition").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Draft, state.Status);
        Assert.Empty(state.History);

        // And the object is still usable: the failure was a rollback, not
        // damage, and it did not retire the instance.
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        var afterRetry = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRetry);
        var recorded = Assert.Single(afterRetry.History);
        Assert.Equal(LifecycleState.Draft, recorded.From);
        Assert.Equal(LifecycleState.InReview, recorded.To);
        Assert.False(string.IsNullOrWhiteSpace(recorded.ActorPrincipalId));
    }

    /// <summary>
    /// A delete whose commit fails leaves the instance undeleted — the
    /// outcome `TD-140` describes as removing the object from the whole
    /// product with no supported way back — and the next successful write
    /// makes nothing durable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It once asserted, under the name
    /// <c>ADeleteWhoseDurableWriteFails_StillSoftDeletesTheInstance_AndTheNextWriteMakesItDurable</c>,
    /// that <c>part.IsDeleted</c> was <see langword="true"/> after the
    /// failure. The `TD-140` reasoning about <c>DeleteAsync</c> — one
    /// writer, no undelete anywhere, twenty-one read models filtering
    /// <c>IDeletable { IsDeleted: true }</c> — is a property of the flag
    /// and not of the exception that interrupts the write.
    /// </para>
    /// <para>
    /// <b>The attachment clause has changed sides and is the point of the
    /// re-pointing.</b> `WP 16.4B-R7` asserted that the `TD-97` byte
    /// release was <em>skipped</em>, because its undo deleted nothing
    /// durable. The bytes are now released in the same transaction as the
    /// record that stops referencing them, so the assertion is the
    /// stronger one it always wanted to be: the object is not deleted, and
    /// its content is still there, because neither happened.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ADeleteWhoseCommitFails_LeavesTheInstanceUndeleted_AndItsContentIntact()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        failing.FailNextCommit = true;

        var failure = await Record.ExceptionAsync(() => part.DeleteAsync().WaitAsync(Timeout));
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        Assert.False(part.IsDeleted, "A delete the caller was told had failed left the object soft-deleted.");

        // The object is not deleted, so its content must still be there —
        // and it is, because the delete of the bytes was in the
        // transaction that did not commit.
        Assert.True(rig.HasContent(attachment.Id));

        await part.RenameAsync("Renamed after the failed delete").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.False(state.IsDeleted);
        Assert.Single(state.Attachments);

        // And a delete retried against a healthy store works, releasing the
        // content exactly as it always did.
        await part.DeleteAsync().WaitAsync(Timeout);

        var afterRetry = await rig.States.FindAsync(part.Id);
        Assert.NotNull(afterRetry);
        Assert.True(afterRetry.IsDeleted);
        Assert.False(rig.HasContent(attachment.Id));
    }

    /// <summary>
    /// The same failure boundary, on the four remaining mutators: each
    /// leaves its in-memory state untouched when the commit fails — and
    /// <c>AttachContentAsync</c> leaves <b>no durable content either</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It once asserted, under the name
    /// <c>TheOtherFourMutators_AlsoKeepTheirMutation_WhenTheDurableWriteFails</c>,
    /// that each of these four kept its mutation; `WP 16.4B-R7` inverted
    /// that. Stated for the whole set rather than one example, so that a
    /// register row derived from this file cannot describe the boundary as
    /// a quirk of one method.
    /// </para>
    /// <para>
    /// <b>The last assertions are inverted by `WP 17.1B` and are the most
    /// important lines in the fact.</b> They used to read
    /// <c>Assert.Single(rig.Content.StoredKeys)</c> and
    /// <c>Assert.Single(await rig.WriteIntents.ListMarkedAsync())</c>, and
    /// they were correct then: R7's undo deliberately deleted nothing
    /// durable, so a failed attach left its bytes and its write-intent
    /// marker behind as a bounded, collectable orphan. The bytes are now
    /// written through the same transaction as the record that names them,
    /// so there is no orphan to bound — and the write-intent marker, the
    /// store behind it and the reconciliation sweep that hunted for such
    /// orphans are deleted. A "fix" that reinstated a durable residue here
    /// would turn these lines red, and should.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TheOtherFourMutators_LeaveNothingAtAll_WhenTheirCommitFails()
    {
        var rig = Rig.WithFailableCommit(out var failing);

        var renamed = await rig.CreatePartAsync("PRT-1", "Bracket");
        failing.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => renamed.RenameAsync("Leaked name").WaitAsync(Timeout)));
        Assert.Equal("Bracket", renamed.DisplayName);

        var bommed = await rig.CreatePartAsync("PRT-2", "Housing");
        failing.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => bommed.SetBomLineAsync(17m, "each", "FN-9").WaitAsync(Timeout)));
        Assert.Equal(1m, bommed.Quantity);
        Assert.Null(bommed.FindNumber);

        var attached = await rig.CreatePartAsync("PRT-3", "Plate");
        var phantom = new Attachment("phantom.txt", "text/plain", 3);
        failing.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => attached.AttachAsync(phantom).WaitAsync(Timeout)));
        Assert.DoesNotContain(await attached.GetAttachmentsAsync(), a => a.Id == phantom.Id);

        var contented = await rig.CreatePartAsync("PRT-4", "Cover");
        failing.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => contented.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout)));
        Assert.Empty(await contented.GetAttachmentsAsync());

        // Inverted by `WP 17.1B`: no orphaned payload, and no marker,
        // because there is neither a second durable write nor a marker.
        Assert.Empty(rig.ContentKeys);
    }

    // ================================================================
    // §3b THE EDGE OF THE CLAIM — what `ADR-0145` does and does not say
    //
    // Every fact here exists so that the next reader cannot over-read the
    // closure. `WP 16.4B-R7` needed four facts in this section to bound an
    // undo that decided on evidence; three of them pinned decisions inside
    // that evidence step and are deleted with it. What is left are the
    // claims about the platform rather than about the workaround.
    // ================================================================

    /// <summary>
    /// A move whose commit fails leaves neither the parent nor the
    /// <c>groupedUnder</c> reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B`, not deleted.</b> Its
    /// predecessor —
    /// <c>AMoveWhoseStateWriteFailsAfterItsLinkWrite_RestoresTheParent_ButLeavesTheGroupedUnderLink</c>
    /// — characterised an accepted residue: the link write and the state
    /// write were two durable steps, so a failure between them left the
    /// reference behind for good. Removing it would have been a delete of
    /// durable state without proven ownership, and "nothing in this
    /// platform removes a relationship" made the residue permanent; the
    /// fact asserted it rather than tidying it away.
    /// </para>
    /// <para>
    /// There is no "between them" any more. The reference and the state
    /// record are one write in one transaction, so nothing is left to
    /// remove because nothing was written — which is why the ordering
    /// trade-off recorded in <c>MoveAsync</c>'s own block comment (link
    /// first and leave a residue, or state first and report failure for a
    /// move that landed) has no third horn to choose between.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMoveWhoseCommitFails_LeavesNeitherTheParentNorTheGroupedUnderLink()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var parent = await rig.CreatePartAsync("PRT-P", "Housing");
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var bodiesBefore = failing.BodiesCompleted;
        failing.FailNextCommit = true;

        var failure = await Record.ExceptionAsync(() => part.MoveAsync(parent.Id).WaitAsync(Timeout));
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        // The whole body ran — the reference was staged — and none of it
        // landed.
        Assert.Equal(bodiesBefore + 1, failing.BodiesCompleted);

        Assert.Null(part.ParentId);
        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);
        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Null(state.ParentId);

        // The reference that used to survive as disclosed residue.
        Assert.Empty(await rig.Documents.GetReferencesAsync(part.Id));
        Assert.DoesNotContain(
            await part.GetRelationshipsAsync(),
            r => r.TargetId == parent.Id && r.RelationshipKind == "groupedUnder");
    }

    /// <summary>
    /// A failed commit is decided on <b>no evidence at all</b>: nothing is
    /// re-read, nothing is undone, and memory and disk agree afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B`, as its predecessor's
    /// standing instruction required.</b> It read
    /// <c>AMutationWhoseStoreCommitsThrowsAndWhoseReReadFails_IsUndoneAnyway_LeavingTheInstanceDisagreeingWithDisk</c>,
    /// and it characterised a stated limitation of `WP 16.4B-R7`:
    /// <c>RollBackOnFailureAsync</c> decided whether to undo by re-reading
    /// the durable record, so a write that landed and a re-read that could
    /// not confirm it produced an in-memory undo of a committed change —
    /// memory and disk disagreeing until a restart. Its remarks named the
    /// whole family ("the write landed and the re-read does not confirm
    /// it") and said that if a later board closed it, this test was where
    /// the decision would land and it must be inverted rather than
    /// deleted.
    /// </para>
    /// <para>
    /// `ADR-0145` closes it by removing the question. A mutator never
    /// mutates before it commits, so there is nothing to undo and no
    /// evidence to weigh; the store's own rollback drops a working copy
    /// that was never published. The assertion is therefore the negation
    /// of the old one — the record and the instance are both exactly what
    /// they were — plus the mechanism, read off the store's own counters:
    /// one rollback, no commit.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AFailedCommit_IsDecidedOnNoEvidence_AndLeavesMemoryAndDiskAgreeing()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var commitsBefore = rig.Store.CommitCount;
        var rollbacksBefore = rig.Store.RollbackCount;

        failing.FailNextCommit = true;

        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Committed but unverifiable").WaitAsync(Timeout));
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        // Nothing landed...
        Assert.Equal(commitsBefore, rig.Store.CommitCount);
        Assert.Equal(rollbacksBefore + 1, rig.Store.RollbackCount);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Bracket", state.DisplayName);

        // ...and the instance agrees with it, without having undone
        // anything, because it had not changed.
        Assert.Equal("Bracket", part.DisplayName);
    }

    /// <summary>
    /// A mutation whose commit <b>lands</b> is never withdrawn from
    /// memory, and a later failed commit does not disturb it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B`, as its predecessor's
    /// standing instruction required.</b> It read
    /// <c>AMutationWhoseStoreCommitsAndThenAnswersNoRecord_IsUndoneAnyway</c>,
    /// and it existed because the risk had first been bounded as requiring
    /// a store that was "unreadable", which was materially narrower than
    /// the truth: <c>EngineeringObjectStateStore.Deserialise</c> answers
    /// <see langword="null"/>, with a logged warning and no exception, for
    /// a record that is present but unparseable or at an unmigratable
    /// schema version, and the undo read that as "the write did not land".
    /// A perfectly healthy store therefore reached the same wrong undo.
    /// </para>
    /// <para>
    /// The direction of the harm is what is inverted here: a change that
    /// committed can no longer be taken back out of the instance, because
    /// nothing reads the record to decide anything and the only thing that
    /// writes the instance is a commit that returned. Asserted on both
    /// halves — the landed change survives a subsequent failed commit, and
    /// the record still shows it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMutationWhoseCommitLands_IsNeverWithdrawnFromMemory()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        await part.RenameAsync("Committed and confirmed").WaitAsync(Timeout);

        Assert.Equal("Committed and confirmed", part.DisplayName);
        Assert.Equal("Committed and confirmed", (await rig.States.FindAsync(part.Id))!.DisplayName);

        // A later failure takes nothing back with it.
        failing.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.SetBomLineAsync(17m, "each").WaitAsync(Timeout)));

        Assert.Equal("Committed and confirmed", part.DisplayName);
        Assert.Equal(1m, part.Quantity);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Committed and confirmed", state.DisplayName);
        Assert.Equal(1m, state.BomLine.Quantity);
    }

    /// <summary>
    /// An attach keeps the <b>same</b> attachment instances the caller
    /// handed in, and a failed commit leaves the ones the object already
    /// held exactly where they were.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> Its predecessor —
    /// <c>TheUndoRestoresTheSameInstances_NotEqualValuedCopies</c> — held
    /// <c>MutationRollbackPoint</c> in place against the obvious wrong
    /// implementation, <c>RestoreState(capturedState)</c>, which would
    /// have swapped a caller's own <see cref="IAttachment"/> for this
    /// assembly's <c>Attachment</c>. The rollback point is deleted, but the
    /// hazard moved rather than vanished: <c>ApplyCommittedState</c> takes
    /// an explicit attachment list precisely so that an attach's own
    /// <em>success</em> path does not rebuild the caller's instances from
    /// the record. This fact is what holds that parameter there, and it
    /// checks the failure path too, where nothing should be touched at
    /// all. <b>Mutant that kills it:</b> pass <see langword="null"/> for
    /// <c>attachments</c> in <c>AttachAsync</c>.
    /// <para>
    /// The identity claim is scoped where the contract makes it — to the
    /// attach that supplied the instance, and to a mutation that fails.
    /// Every <em>other</em> committed mutator rebuilds the list from the
    /// record it just wrote, which is deliberate and is what makes an
    /// instance and its durable state agree field for field.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AnAttachKeepsTheCallersOwnAttachmentInstances_AndAFailedCommitLeavesThemUntouched()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var original = new Attachment("kept.txt", "text/plain", 3);
        await part.AttachAsync(original).WaitAsync(Timeout);

        // The success path kept the caller's own instance rather than a
        // copy rebuilt from the record.
        Assert.Same(original, Assert.Single(await part.GetAttachmentsAsync()));

        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        var attachmentBefore = Assert.Single(await part.GetAttachmentsAsync());
        var historyBefore = Assert.Single(part.History);

        failing.FailNextCommit = true;
        Assert.IsType<PersistenceStoreUnavailableException>(await Record.ExceptionAsync(
            () => part.AttachAsync(new Attachment("never-landed.txt", "text/plain", 3)).WaitAsync(Timeout)));

        // And the failure path touched neither collection: not the values,
        // and not the instances.
        Assert.Same(attachmentBefore, Assert.Single(await part.GetAttachmentsAsync()));
        Assert.Same(historyBefore, Assert.Single(part.History));
    }

    /// <summary>
    /// An ordinary validation rejection is raised from <b>inside the
    /// transaction</b>, with its own exception type, and writes nothing.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail.</b> An impermissible lifecycle transition is a
    /// routine, user-facing outcome — five <c>Tempest.App</c> command
    /// handlers turn it into an ordinary failure result — so the caller
    /// must receive <see cref="InvalidLifecycleTransitionException"/> and
    /// nothing else. Its predecessor ran the same check against a
    /// deliberately hollow state record, because the rejection then ran
    /// through an evidence comparison that a malformed record could make
    /// throw, replacing the caller's exception with a diagnostic's. There
    /// is no evidence comparison and no re-read; the rejection is a throw
    /// from the projection, inside the transaction, before anything is
    /// staged, and that is what is asserted instead.
    /// </remarks>
    [Fact]
    public async Task AValidationRejection_IsRaisedInsideTheTransaction_AndWritesNothing()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        var commitsBefore = rig.Store.CommitCount;
        var rollbacksBefore = rig.Store.RollbackCount;

        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Released).WaitAsync(Timeout));

        Assert.IsType<InvalidLifecycleTransitionException>(failure);
        Assert.Equal(LifecycleState.Draft, part.Status);
        Assert.Empty(part.History);

        Assert.Equal(commitsBefore, rig.Store.CommitCount);
        Assert.Equal(rollbacksBefore + 1, rig.Store.RollbackCount);
        Assert.Equal(LifecycleState.Draft, (await rig.States.FindAsync(part.Id))!.Status);
    }

    // ================================================================
    // §4 THE CHECKS THAT USED TO RUN OUTSIDE THE LOCK — `TD-145`,
    //    `TD-146`, both inverted
    // ================================================================

    /// <summary>
    /// A move that would close a parent cycle is refused <b>inside the
    /// transaction</b>, and its refusal writes nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B` (`TD-145`), not deleted.</b>
    /// Its predecessor — <c>TwoMovesThatEachPassTheCircularParentGuard_FormACycle</c>
    /// — reproduced the accepted consequence of keeping
    /// <c>GuardAgainstCircularParentAsync</c> outside the write lock: two
    /// moves that each passed the guard against a graph neither had yet
    /// modified both committed, and left a cycle neither could see. It
    /// forced that in program order by holding the per-object write lock
    /// from the test itself.
    /// </para>
    /// <para>
    /// Both halves of that construction are gone. The guard runs inside
    /// the transaction, against the committed graph, under the one lock
    /// that the second move must also take; and there is no per-object
    /// lock for a test to hold. What survives, and is asserted here, is
    /// the invariant the guard exists for and the property `ADR-0145`
    /// adds to it: the refusal happens before anything is staged, so a
    /// refused move leaves no state change and no reference.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMoveThatWouldCloseACycle_IsRefusedInsideTheTransaction_AndWritesNothing()
    {
        var rig = new Rig();
        var a = await rig.CreatePartAsync("PRT-A", "A");
        var b = await rig.CreatePartAsync("PRT-B", "B");

        await a.MoveAsync(b.Id).WaitAsync(Timeout);

        var commitsBefore = rig.Store.CommitCount;

        var refused = await Record.ExceptionAsync(() => b.MoveAsync(a.Id).WaitAsync(Timeout));
        Assert.IsType<CircularParentAssignmentException>(refused);

        // No cycle, in memory or on disk.
        Assert.Equal(b.Id, a.ParentId);
        Assert.Null(b.ParentId);
        Assert.Null((await rig.States.FindAsync(b.Id))!.ParentId);

        // And the refusal wrote nothing: no commit, and no reference from
        // the move that was refused.
        Assert.Equal(commitsBefore, rig.Store.CommitCount);
        Assert.Empty(await rig.Documents.GetReferencesAsync(b.Id));
    }

    /// <summary>
    /// A delete is refused <b>inside the transaction</b> when the object
    /// has a live child, and its refusal writes nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B` (`TD-146`), not deleted.</b>
    /// Its predecessor — <c>ADeleteCanCommit_WhileAMoveGivesItALiveChild</c>
    /// — reproduced a hole in <see cref="EngineeringObjectHasChildrenException"/>:
    /// the "has this object any live children?" count ran above the write
    /// path, so a <c>MoveAsync</c> that committed between the count and
    /// the lock acquisition created the child the check had just proved
    /// absent, and the object was soft-deleted with a live child still
    /// naming it as parent. Every read model filters the deleted parent
    /// out while the child remains live, so the child was reachable by Id
    /// and absent from every tree walked down from a root.
    /// </para>
    /// <para>
    /// The count is now inside the transaction, under the one lock the
    /// competing move must also take, so the two are ordered and the loser
    /// is refused. Asserted on the committed record rather than on the
    /// instance, and with the refusal shown to have staged nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ADeleteWithALiveChild_IsRefusedInsideTheTransaction_AndWritesNothing()
    {
        var rig = new Rig();
        var parent = await rig.CreatePartAsync("PRT-P", "Housing");
        var child = await rig.CreatePartAsync("PRT-C", "Bracket");

        await child.MoveAsync(parent.Id).WaitAsync(Timeout);

        var commitsBefore = rig.Store.CommitCount;

        var refused = await Record.ExceptionAsync(() => parent.DeleteAsync().WaitAsync(Timeout));
        Assert.IsType<EngineeringObjectHasChildrenException>(refused);

        Assert.False(parent.IsDeleted);
        Assert.False((await rig.States.FindAsync(parent.Id))!.IsDeleted);
        Assert.Equal(commitsBefore, rig.Store.CommitCount);

        // And the check is not merely a latch: once the child is deleted,
        // the parent can be.
        await child.DeleteAsync().WaitAsync(Timeout);
        await parent.DeleteAsync().WaitAsync(Timeout);

        Assert.True((await rig.States.FindAsync(parent.Id))!.IsDeleted);
    }

    // ================================================================
    // §5 `TD-141`, LinkAsync — inverted
    // ================================================================

    /// <summary>
    /// <c>LinkAsync</c> on an instance <c>ReviseAsync</c> has already
    /// retired is <b>refused</b>, and writes no relationship — neither to
    /// the document store's references nor to the in-memory graph the live
    /// successor answers from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B` (`TD-141`), not deleted.</b>
    /// Its predecessor —
    /// <c>LinkAsync_OnARetiredInstance_IsNotRefused_AndWritesThePermanentRelationship</c>
    /// — was the reproduction the register row asked for, and it
    /// established two things a reading of the code could not. First, the
    /// consequence was not confined to the retired instance:
    /// <c>GetRelationshipsAsync</c> is keyed by
    /// <see cref="IEngineeringObject.Id"/>, which predecessor and
    /// successor share, so a write through a retired handle landed on the
    /// graph of the object that was live — an injection into current data,
    /// not a divergence in a dead one. Second, it was durable in the
    /// document store's references and survived a restart, while every
    /// other durable write through the same handle was refused in the same
    /// breath.
    /// </para>
    /// <para>
    /// <c>LinkAsync</c> now goes through <c>ExecuteWriteAsync</c> like
    /// every other durable write, checks supersession inside the
    /// transaction, and records the relationship in the in-memory cache
    /// only after the commit returns. Both of the reproduction's findings
    /// are therefore negated here, on the same rig, in the same order.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task LinkAsync_OnARetiredInstance_IsRefused_AndWritesNothing()
    {
        var rig = new Rig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        var target = await rig.CreatePartAsync("PRT-2", "Housing");

        var successor = (Part)await part.ReviseAsync("Revised.", "Rev B.");

        // Every other durable write through this handle is refused...
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.RenameAsync("x").WaitAsync(Timeout));

        // ...and so, now, is this one.
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.LinkAsync(target.Id, "relatesTo").WaitAsync(Timeout));

        var references = await rig.Documents.GetReferencesAsync(part.Id);
        Assert.DoesNotContain(references, r => r.TargetDocumentId == target.Id && r.RelationshipKind == "relatesTo");

        // And nothing reached the live successor's graph either.
        Assert.Equal(part.Id, successor.Id);
        Assert.DoesNotContain(await successor.GetRelationshipsAsync(), r => r.TargetId == target.Id);

        // The successor itself can still make the link, which is what
        // "refused" has to mean rather than "disabled".
        await successor.LinkAsync(target.Id, "relatesTo").WaitAsync(Timeout);
        Assert.Contains(await successor.GetRelationshipsAsync(), r => r.TargetId == target.Id);
    }

    /// <summary>
    /// The guard reaches the product's own named relationship APIs, not
    /// only <c>LinkAsync</c> itself.
    /// </summary>
    /// <remarks>
    /// <b>Inverted with its sibling (`TD-141`).</b> The row named
    /// <c>MoveAsync</c>'s <c>groupedUnder</c> write as the reachable
    /// caller; it was not the only one — several concrete Kinds expose a
    /// public relationship method whose whole body is a <c>LinkAsync</c>
    /// call, here <c>EngineeringTask.ContributeToAsync</c>. A caller
    /// holding a stale task handle used to record a permanent contribution
    /// edge that the platform would have refused had the same call needed
    /// to persist state. It is refused now, through the named API as
    /// through the general one.
    /// </remarks>
    [Fact]
    public async Task AKindsOwnRelationshipApi_IsRefusedThroughARetiredHandleToo()
    {
        var rig = new Rig();
        var task = await rig.CreateTaskAsync("TSK-1", "Draft the report");
        var milestone = await rig.CreatePartAsync("MS-1", "Gate 3");

        _ = await task.ReviseAsync("Revised.", "Rev B.");

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => task.ContributeToAsync(milestone.Id).WaitAsync(Timeout));

        var references = await rig.Documents.GetReferencesAsync(task.Id);
        Assert.DoesNotContain(references, r => r.TargetDocumentId == milestone.Id);
    }

    // ================================================================
    // §6 `TD-142`, the type-specific mutators — inverted
    // ================================================================

    /// <summary>
    /// A type-specific mutator on a concrete Kind, refused with
    /// <see cref="SupersededEngineeringObjectException"/>, leaks
    /// <b>nothing</b> — not into its own instance while it waits, not into
    /// the live successor, and not into the successor's next write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `WP 17.1B` (`TD-142`), not deleted, as
    /// its predecessor's own closing line required.</b> It read
    /// <c>ARefusedTypeSpecificMutator_LeaksItsFieldIntoTheLiveSuccessor</c>,
    /// and it was the reproduction the register row asked for: on the real
    /// <see cref="EngineeringTask"/> and the real <c>AssignAsync</c>, the
    /// caller was told the assignment failed and the object that answered
    /// for that Id was assigned, durably, to the principal the failed call
    /// named. The eleven type-specific mutators wrote the Kind's own field
    /// before persisting and sat outside the base mutators' protection
    /// entirely.
    /// </para>
    /// <para>
    /// <c>MutateTypeStateAndPersistAsync</c> now projects the next type
    /// state inside the transaction and applies it to the Kind's fields
    /// only after the commit. <b>The mid-flight assertion is the exact
    /// negation of the old one</b>: where the predecessor asserted "the
    /// field is already mutated, before the refusal can be raised", this
    /// asserts that it is not.
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> The revision is parked at the end of its
    /// transaction body — every write staged, the domain write lock held —
    /// and <c>AssignAsync</c> is started on the calling thread behind it,
    /// where it can get no further than that lock. No sleeps, no polling,
    /// no second race.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARefusedTypeSpecificMutator_LeaksNothingIntoTheLiveSuccessor()
    {
        var rig = Rig.WithGate(out var gate);
        var task = await rig.CreateTaskAsync("TSK-1", "Draft the report");

        Assert.Null(task.AssignedToPrincipalId);

        var parked = gate.ArmNextTransaction();
        var revising = Task.Run(() => task.ReviseAsync("Revised.", "Rev B."));
        await parked;

        var assigning = task.AssignAsync("someone-who-was-told-it-failed");

        // Inverted: the field is NOT mutated. The mutator is blocked on
        // the domain write lock, and applies nothing until it commits.
        Assert.False(assigning.IsCompleted);
        Assert.Null(task.AssignedToPrincipalId);

        gate.Release();

        var successor = (EngineeringTask)await revising.WaitAsync(Timeout);
        var refused = await Record.ExceptionAsync(() => assigning.WaitAsync(Timeout));

        Assert.IsType<SupersededEngineeringObjectException>(refused);

        // The live successor inherited nothing from the call the caller was
        // told had failed...
        Assert.Null(task.AssignedToPrincipalId);
        Assert.Null(successor.AssignedToPrincipalId);

        // ...and the successor's next write makes nothing durable either.
        await successor.SetPriorityAsync(WorkPriority.High).WaitAsync(Timeout);

        var state = await rig.States.FindAsync(task.Id);
        Assert.NotNull(state);
        Assert.Null(state.Type(nameof(EngineeringTask.AssignedToPrincipalId)));
    }

    /// <summary>
    /// The identical interleaving, on a base mutator, leaks nothing
    /// either.
    /// </summary>
    /// <remarks>
    /// <b>Guard-rail, and the control for the fact above.</b> Same rig,
    /// same park point, same Kind, same object — only the mutator differs.
    /// It was the control that distinguished the eleven unfixed
    /// type-specific sites from the base mutators an earlier round had
    /// already fixed; now that both behave the same way it is the control
    /// that shows the rig can still tell a leak from its absence, because
    /// it exercises the same park through a different code path
    /// (<c>MutateAndPersistAsync</c> directly rather than through
    /// <c>MutateTypeStateAndPersistAsync</c>).
    /// </remarks>
    [Fact]
    public async Task TheSameInterleaving_OnABaseMutator_LeaksNothing()
    {
        var rig = Rig.WithGate(out var gate);
        var task = await rig.CreateTaskAsync("TSK-1", "Draft the report");

        var parked = gate.ArmNextTransaction();
        var revising = Task.Run(() => task.ReviseAsync("Revised.", "Rev B."));
        await parked;

        var renaming = task.RenameAsync("Leaked name");

        // Nothing has been mutated: the refusal will happen before it is.
        Assert.False(renaming.IsCompleted);
        Assert.Equal("Draft the report", task.DisplayName);

        gate.Release();

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
    /// The shipped write path — the real
    /// <see cref="EngineeringDocumentStore"/>,
    /// <see cref="EngineeringObjectStateStore"/> and
    /// <see cref="AttachmentContentStore"/> — over one
    /// <see cref="InMemoryQueryablePersistenceStore"/>, optionally wrapped
    /// so a fault or a gate can be injected at the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces both of the file's earlier rigs: the on-disk
    /// <c>DurableFixture</c> and the <c>ProbeRig</c> of hand-written
    /// doubles. The doubles cannot exist any more — the document, state
    /// and attachment stores implement internal transactional-writer
    /// contracts and <see cref="EngineeringDomainContext"/> refuses
    /// anything else — and they are not needed: there is one durable
    /// authority, so a test that wants to fail a write fails <em>the</em>
    /// store, and a test that wants to see what landed reads
    /// <em>the</em> store.
    /// </para>
    /// <para>
    /// In memory rather than on disk deliberately.
    /// <see cref="InMemoryQueryablePersistenceStore"/> models the same
    /// copy-on-write transaction and the same one-writer-at-a-time rule
    /// that SQLite gives the production path, so every fact here exercises
    /// the real atomicity rather than a file-per-key store whose
    /// <c>ExecuteInTransactionAsync</c> could not be atomic at all.
    /// </para>
    /// </remarks>
    private sealed class Rig
    {
        public Rig()
            : this(new InMemoryQueryablePersistenceStore(), transactional: null)
        {
        }

        private Rig(InMemoryQueryablePersistenceStore store, IQueryablePersistenceStore? transactional)
        {
            Store = store;

            var principal = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            Documents = new EngineeringDocumentStore(store, principal);
            States = new EngineeringObjectStateStore(store);
            Content = new AttachmentContentStore(store);

            Context = new EngineeringDomainContext(
                transactional ?? store,
                Documents,
                repository,
                relationships,
                new LifecycleTransitionTable(),
                new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository),
                principal,
                States,
                Content);
        }

        /// <summary>The one durable store — the read surface for every "what landed?" assertion.</summary>
        public InMemoryQueryablePersistenceStore Store { get; }

        public EngineeringDocumentStore Documents { get; }

        public EngineeringObjectStateStore States { get; }

        public AttachmentContentStore Content { get; }

        public EngineeringDomainContext Context { get; }

        /// <summary>Every committed attachment payload key.</summary>
        public IReadOnlyList<string> ContentKeys => Store.CommittedKeys(AttachmentContentStore.ContentCollectionName);

        /// <summary>Whether committed attachment content exists for <paramref name="attachmentId"/>.</summary>
        public bool HasContent(Guid attachmentId) =>
            Store.CommittedBytes(AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N")) is not null;

        /// <summary>The committed audit rows for one object, found by key prefix.</summary>
        public IReadOnlyList<string> AuditRowsFor(Guid objectId) =>
            Store.CommittedKeys(AuditRecorder.AuditCollectionName)
                .Where(k => k.StartsWith(objectId.ToString("N"), StringComparison.Ordinal))
                .ToList();

        /// <summary>A rig whose transactions can be parked at the end of their bodies.</summary>
        public static Rig WithGate(out GatedPersistenceStore gate)
        {
            var store = new InMemoryQueryablePersistenceStore();
            gate = new GatedPersistenceStore(store);
            return new Rig(store, gate);
        }

        /// <summary>A rig whose commits can be failed deterministically, after the whole body has run.</summary>
        public static Rig WithFailableCommit(out CommitFailingPersistenceStore failing)
        {
            var store = new InMemoryQueryablePersistenceStore();
            failing = new CommitFailingPersistenceStore(store);
            return new Rig(store, failing);
        }

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

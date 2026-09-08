using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R6`, independent adversarial cover for the interleavings
/// between <see cref="EngineeringObjectBase.AttachContentAsync"/> and
/// <see cref="EngineeringObjectBase.ReviseAsync"/> that the fifth review
/// board did <b>not</b> name, plus a characterisation of what the new
/// ordering actually leaves behind on a genuine I/O failure.
/// </summary>
/// <remarks>
/// <para>
/// The board's own reproduction is "a revision parks inside its capture,
/// an attach starts behind it". That one is pinned by
/// <see cref="AttachmentRevisionAtomicityTests"/>. Everything here is a
/// <em>different</em> interleaving or a different failure, chosen because
/// this project's four-board failure mode is a fix that satisfies the
/// reported reproduction and leaves the adjacent path broken:
/// </para>
/// <list type="bullet">
/// <item><description>the attach starts <em>first</em> and the revision arrives while it is mid content-write;</description></item>
/// <item><description>two attaches race one revision, so the successor could inherit two byteless attachments rather than one;</description></item>
/// <item><description>a content write that fails <em>after</em> writing its bytes — the case that decides whether `WP 16.4B-R6`'s new marker-clear really demotes the residue to a collectable orphan, or only claims to;</description></item>
/// <item><description>a revision that fails part-way, which is the first time this method's whole body has run inside a <c>using</c> — a leaked write lock here would wedge every later write to that object for the life of the process;</description></item>
/// <item><description>a second revision arriving concurrently rather than in program order;</description></item>
/// <item><description>the reconciliation sweep running against an attach parked between its content write and its state write, which is the interleaving the marker protocol exists for and the one `WP 16.4B-R6` moved the marker inside the lock underneath.</description></item>
/// </list>
/// <para>
/// <b>Determinism.</b> Nothing here sleeps, polls or races. Every fake
/// store completes synchronously except where a test deliberately parks
/// one, so a call runs on the calling thread as far as its first genuinely
/// incomplete await and the returned <see cref="Task"/> is provably at
/// that point on the next line. Where a test needs to know whether the
/// per-object write lock is held, it asks
/// <see cref="ObjectWriteLockIsHeld"/>, which reads
/// <see cref="Task.IsCompleted"/> on an uncontended acquisition rather
/// than waiting for a timeout — see that method's own remarks.
/// </para>
/// </remarks>
public sealed class RevisionAttachmentInterleavingTests
{
    private static readonly byte[] Bytes = [7, 8, 9];
    private static readonly byte[] OtherBytes = [10, 11, 12, 13];

    // ================================================================
    // Interleavings the board did not name
    // ================================================================

    /// <summary>
    /// <b>The mirror of the board's interleaving.</b> The attach goes
    /// first and is parked inside the content write; the revision arrives
    /// behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The invariant asserted is the one that survives either outcome:
    /// <em>an attach is all-or-nothing</em>. Either it succeeded, in which
    /// case its bytes exist and the live successor carries the reference;
    /// or it was refused, in which case <b>no content was ever written</b>
    /// — not written-then-deleted, never written.
    /// </para>
    /// <para>
    /// Before `WP 16.4B-R6` this interleaving reached the second branch
    /// with <c>SaveCallCount == 1</c> and <c>DeleteCallCount == 1</c>: the
    /// bytes were written outside any lock, the revision slipped past, the
    /// state write was refused and the R5 compensation deleted them again.
    /// That is the "written then rolled back" shape whose safety argument
    /// the board falsified; asserting call counts rather than end states is
    /// what makes the two distinguishable at all.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionArrivingWhileAnAttachIsMidContentWrite_LeavesNoWrittenThenDeletedBytes()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var contentWriteEntered = rig.ContentStore.ParkNextSave();

        // Runs on this thread up to the parked content write.
        var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        await contentWriteEntered;

        // Runs on this thread as far as it can get. After `WP 16.4B-R6`
        // that is the write lock the parked attach is holding; before it,
        // the whole revision completed here, in front of the attach.
        var revising = part.ReviseAsync("Revised content.", "Rev B.");

        rig.ContentStore.ReleaseParkedSave();

        var successor = (RevisableFixture)await revising;
        var attached = await Outcome(attaching);

        if (attached is null)
        {
            Assert.True(
                rig.ContentStore.SaveCallCount == 0,
                "The attach was refused, so nothing durable may ever have been written for it — but the content " +
                $"store's SaveAsync was called {rig.ContentStore.SaveCallCount} time(s). A refusal that has already " +
                "written bytes needs a rollback, and a rollback is what destroyed a live successor's content in R5.");
            Assert.Equal(0, rig.ContentStore.DeleteCallCount);
        }
        else
        {
            Assert.Contains(await successor.GetAttachmentsAsync(), a => a.Id == attached.Id);
            Assert.Contains(attached.Id, rig.ContentStore.StoredKeys);
            Assert.Equal(0, rig.ContentStore.DeleteCallCount);
        }

        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());
    }

    /// <summary>
    /// Two attaches racing one revision. The board reproduced the defect
    /// with one attachment; nothing in its evidence said the fix had to
    /// hold for two, and a fix that special-cased "the pending attachment"
    /// would pass the single-attachment test and fail this.
    /// </summary>
    /// <remarks>
    /// Both attaches are started while the revision is parked inside its
    /// own capture, holding the write lock. Before `WP 16.4B-R6` both got
    /// as far as adding themselves to the list the parked capture was
    /// about to copy, both were then refused, and both compensations
    /// deleted their bytes — leaving the live successor claiming
    /// <b>two</b> attachments whose content no longer existed.
    /// </remarks>
    [Fact]
    public async Task TwoAttachesRacingOneRevision_NeverLeaveTheSuccessorClaimingBytelessAttachments()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var parked = part.ArmNextCapture();
        var revising = Task.Run(() => part.ReviseAsync("Revised content.", "Rev B."));
        await parked;

        var first = part.AttachContentAsync("a.pdf", "application/pdf", Bytes);
        var second = part.AttachContentAsync("b.pdf", "application/pdf", OtherBytes);

        part.ReleaseCapture();

        var successor = (RevisableFixture)await revising;
        _ = await Outcome(first);
        _ = await Outcome(second);

        foreach (var attachment in await successor.GetAttachmentsAsync())
            Assert.True(
                rig.ContentStore.StoredKeys.Contains(attachment.Id),
                $"The live successor claims attachment '{attachment.Id}' ('{attachment.FileName}') but its content " +
                "is gone. Nothing in this platform repairs a reference to content that does not exist — the sweep " +
                "only hunts content nothing references.");

        // ...and durably, after one ordinary write by the live successor.
        await successor.RenameAsync("Renamed");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);

        foreach (var attachment in state.Attachments)
            Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);

        Assert.Equal(0, rig.ContentStore.DeleteCallCount);
    }

    /// <summary>
    /// An attach racing a revision that <em>itself</em> then fails. The
    /// predecessor is not superseded after all, so the attach must
    /// succeed completely rather than being refused by a retirement that
    /// never happened.
    /// </summary>
    [Fact]
    public async Task AnAttachRacingARevisionThatThenFails_StillSucceedsCompletely()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        rig.DocumentStore.FailNextRevise = true;

        await Assert.ThrowsAsync<EngineeringDataException>(
            () => part.ReviseAsync("Revised content.", "Rev B."));

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        Assert.Contains(attachment.Id, rig.ContentStore.StoredKeys);
        Assert.Contains(await part.GetAttachmentsAsync(), a => a.Id == attachment.Id);
        Assert.Empty(await rig.WriteIntentStore.ListMarkedAsync());

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);
    }

    // ================================================================
    // The write lock's own new liabilities
    // ================================================================

    /// <summary>
    /// <b>A revision that fails part-way must not leak the per-object
    /// write lock.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// `WP 16.4B-R6` moved <em>the whole of</em> <c>ReviseAsync</c> —
    /// including the document store's own <c>ReviseAsync</c>, the Kind's
    /// state reader and the repository registration — inside one hold of
    /// this object's write lock. That is new: before it, only the capture
    /// and hand-off were inside, and the document write could not fail
    /// under the lock at all. Because the lock is not reentrant and is
    /// keyed by Id rather than by instance, a lock leaked on a failure
    /// path would not merely fail this call: it would wedge <b>every</b>
    /// later durable write to this object, through any instance, for the
    /// life of the process — a far worse outcome than the revision failing.
    /// </para>
    /// <para>
    /// Both reachable failure points are covered: the document store
    /// throwing (before anything else has happened) and the Kind's own
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> throwing (after a
    /// durable revision record has already been minted). The check is
    /// deterministic — see <see cref="ObjectWriteLockIsHeld"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionThatFailsPartWay_ReleasesTheObjectWriteLock()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        rig.DocumentStore.FailNextRevise = true;
        await Assert.ThrowsAsync<EngineeringDataException>(() => part.ReviseAsync("R1.", "Rev B."));

        Assert.False(
            ObjectWriteLockIsHeld(rig.Context, part.Id),
            "A revision that failed in the document store left this object's write lock held. Every later durable " +
            "write to this object — through any instance — would block for ever.");

        RevisableFixture.FailNextRehydrate = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => part.ReviseAsync("R2.", "Rev C."));
        }
        finally
        {
            RevisableFixture.FailNextRehydrate = false;
        }

        Assert.False(
            ObjectWriteLockIsHeld(rig.Context, part.Id),
            "A revision whose successor construction threw left this object's write lock held.");

        // The object is still fully usable, which is what "released" has to
        // mean in practice rather than only in a probe.
        await part.RenameAsync("Still writable");
        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Still writable", state.DisplayName);
    }

    /// <summary>
    /// The lock-holding surface `WP 16.4B-R6` created, made executable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After R6 the per-object write lock is held across <b>every</b>
    /// collaborator call an attach makes: the write-intent mark, the
    /// content byte write, the object state write and the marker clear.
    /// Before it, only the state write was inside. This is the change that
    /// buys the atomicity — and it is also the change that makes any
    /// collaborator which calls back into the same object deadlock rather
    /// than merely interleave, because <see cref="Concurrency.AsyncKeyedLock"/>
    /// is not reentrant.
    /// </para>
    /// <para>
    /// Recording it as a test rather than only as a comment means the next
    /// board can see the surface it has to reason about, and a future
    /// change that quietly narrows the hold (which would reopen the
    /// board's data-loss window) fails here rather than passing silently.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AnAttach_HoldsTheObjectWriteLockAcrossEveryOneOfItsFourDurableSteps()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        bool heldAtMark = false, heldAtContentWrite = false, heldAtStateWrite = false, heldAtClear = false;

        rig.WriteIntentStore.OnMark = () => heldAtMark = ObjectWriteLockIsHeld(rig.Context, part.Id);
        rig.ContentStore.OnSave = () => heldAtContentWrite = ObjectWriteLockIsHeld(rig.Context, part.Id);
        rig.StateStore.OnSave = () => heldAtStateWrite = ObjectWriteLockIsHeld(rig.Context, part.Id);
        rig.WriteIntentStore.OnClear = () => heldAtClear = ObjectWriteLockIsHeld(rig.Context, part.Id);

        await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        Assert.True(heldAtMark, "The write-intent marker is written outside this object's write lock — a revision can interleave between it and the state write.");
        Assert.True(heldAtContentWrite, "The content bytes are written outside this object's write lock — this is the exact window the fifth review board turned into permanent content destruction.");
        Assert.True(heldAtStateWrite, "The object state is written outside this object's write lock.");
        Assert.True(heldAtClear, "The marker is cleared outside this object's write lock.");
    }

    /// <summary>
    /// <b>Re-entrancy, characterised.</b> A collaborator that calls back
    /// into the same object while an attach is running is now ordered
    /// <em>behind</em> that attach rather than interleaved with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the standing hazard `WP 16.4B-R6` creates, made visible
    /// without hanging a test: the callback's mutation cannot make
    /// progress while the attach holds the lock, so a collaborator that
    /// <em>awaited</em> such a call synchronously would deadlock outright,
    /// where before R6 it would merely have raced. Nothing in production
    /// does this — every implementation of
    /// <see cref="IAttachmentContentStore"/>,
    /// <see cref="IEngineeringObjectStateStore"/> and
    /// <see cref="IAttachmentWriteIntentStore"/> in <c>src/</c> reaches
    /// <c>IPersistenceStore</c> and nothing else, and no
    /// <c>CaptureTypeState</c> override or
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> implementation touches
    /// the context at all — which is precisely why it is worth an
    /// executable record: the constraint is real, invisible from the call
    /// site, and only enforced by nobody having broken it yet.
    /// </para>
    /// <para>
    /// The observation is deterministic, not timed: the task is inspected
    /// for completion, never waited on.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMutationReenteringTheObjectFromAStoreCallback_CannotProceedUntilTheAttachReleasesTheLock()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        Task? reentrant = null;
        rig.ContentStore.OnSave = () => reentrant = part.RenameAsync("Renamed from inside the content store");

        await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        Assert.NotNull(reentrant);

        // It could not have completed inside the callback: the attach held
        // this object's write lock for the whole of it.
        await reentrant;

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed from inside the content store", state.DisplayName);
        Assert.Single(state.Attachments);
    }

    /// <summary>
    /// <b>The lock-ordering change, characterised.</b> `WP 16.4B-R6` moved
    /// <c>IEngineeringDocumentStore.ReviseAsync</c> inside the per-object
    /// write lock, so the object lock is now held across the document
    /// store's own per-document lock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two consequences, both asserted here. First, the nesting is
    /// object-lock → document-lock, and it is one-directional: nothing in
    /// <c>src/</c> takes the document lock and then asks for an object
    /// lock — <see cref="EngineeringData.EngineeringDocumentStore"/> holds
    /// no reference to <see cref="EngineeringDomainContext"/> and never
    /// calls into <see cref="EngineeringObjectBase"/>. So there is no
    /// cycle and no deadlock, only an ordering.
    /// </para>
    /// <para>
    /// Second, and this is the cost: a slow or stalled document write now
    /// blocks <b>every</b> durable write to that object, and widens the
    /// window in which a concurrent predecessor write is refused rather
    /// than accepted. A rename issued while a revision is still writing
    /// its document revision used to be accepted (the revision had not yet
    /// taken the lock); it is now refused. The cost is real and is left
    /// recorded here: R6 made a refusal reachable for the whole duration
    /// of a document write.
    /// </para>
    /// <para>
    /// <b>What that widening cost, until `WP 16.4B-R6b`.</b> The line below
    /// used to assert
    /// <c>Assert.Equal("Issued while the document was being written",
    /// successor.DisplayName)</c> — the refused rename reached the
    /// successor anyway (P1-F4), so R6 had widened the window on a leak it
    /// had not closed. `WP 16.4B-R6b` closed the leak for
    /// <c>RenameAsync</c> and the other four, and the assertion is
    /// inverted here rather than removed: the widened window is still
    /// exactly as wide, and now there is nothing in it to leak.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TheObjectWriteLockIsHeldAcrossTheDocumentStoresOwnWrite()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var documentWriteEntered = rig.DocumentStore.ParkNextRevise();
        var revising = part.ReviseAsync("Revised content.", "Rev B.");
        await documentWriteEntered;

        Assert.True(
            ObjectWriteLockIsHeld(rig.Context, part.Id),
            "The document revision is written outside this object's write lock, so a concurrent mutation can still " +
            "land durably between the document write and the hand-off.");

        var renaming = part.RenameAsync("Issued while the document was being written");

        rig.DocumentStore.ReleaseParkedRevise();

        var successor = (RevisableFixture)await revising;
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => renaming);

        // And the refused rename reached neither the successor nor the
        // predecessor (`WP 16.4B-R6b`). This assertion was the opposite
        // until that Work Package: the widened window is unchanged, and it
        // no longer carries a leak. See
        // `RefusedMutationSuccessorLeakageTests` for the closure in full.
        Assert.Equal("Bracket", successor.DisplayName);
        Assert.Equal("Bracket", part.DisplayName);
    }

    // ================================================================
    // Concurrent second revision
    // ================================================================

    /// <summary>
    /// The same invariant in <b>program order</b>, established
    /// independently rather than taken from the fix's own test: a second
    /// revision through an already-revised instance is refused, mints no
    /// revision record, creates no second live instance, and cannot
    /// displace the first successor's durable state.
    /// </summary>
    /// <remarks>
    /// Asserted on the persisted record and the repository's own answer
    /// for the Id, not on an in-memory flag. Before `WP 16.4B-R6` the
    /// second revision succeeded, minted a second successor whose snapshot
    /// predated the first successor's accepted write, and left the first
    /// successor un-retired — two independently mutable instances on one
    /// durable record, which is verbatim the `TD-136` lost update.
    /// </remarks>
    [Fact]
    public async Task ASecondSequentialRevision_IsRefusedAndCannotDisplaceTheFirstSuccessorsDurableState()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var successorA = (RevisableFixture)await part.ReviseAsync("First.", "Rev B.");
        await successorA.RenameAsync("Accepted, by successor A");

        var beforeSecondAttempt = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(beforeSecondAttempt);
        Assert.Equal("Accepted, by successor A", beforeSecondAttempt.DisplayName);

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.ReviseAsync("Second.", "Rev C."));

        // No second successor, no second revision record, and A's accepted
        // write is still the durable truth.
        Assert.Same(successorA, await rig.Context.Repository.FindAsync(part.Id));

        var history = await part.GetRevisionHistoryAsync();
        Assert.Equal(2, history.Count);
        Assert.DoesNotContain(history, r => r.Content == "Second.");

        var after = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(after);
        Assert.Equal("Accepted, by successor A", after.DisplayName);

        // Revising the live successor is ordinary and still works — the
        // guard is per instance, not a latch on the Id.
        var successorB = (RevisableFixture)await successorA.ReviseAsync("Third.", "Rev D.");
        Assert.Equal(3, successorB.CurrentRevisionNumber);
        Assert.Equal("Accepted, by successor A", successorB.DisplayName);
    }

    /// <summary>
    /// A second revision arriving <em>concurrently</em> rather than in
    /// program order. Exactly one successor may become authoritative, the
    /// loser must get the domain refusal, and the document's own revision
    /// history must not gain an entry for the revision that did not happen.
    /// </summary>
    /// <remarks>
    /// The program-order case is pinned by
    /// <see cref="RevisionSuccessorUniquenessTests"/>. This is the
    /// concurrent one, made deterministic by parking the first revision
    /// inside its own capture — at which point it is holding the write
    /// lock and has already minted its revision record — and starting the
    /// second on the calling thread behind it.
    /// </remarks>
    [Fact]
    public async Task TwoConcurrentRevisionsOfOneInstance_MintExactlyOneSuccessorAndOneRevisionRecord()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        var parked = part.ArmNextCapture();
        var first = Task.Run(() => part.ReviseAsync("First.", "Rev B."));
        await parked;

        var second = part.ReviseAsync("Second.", "Rev C.");

        part.ReleaseCapture();

        var winner = (RevisableFixture)await first;
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => second);

        // ---- exactly one successor is live -------------------------
        var live = await rig.Context.Repository.FindAsync(part.Id);
        Assert.Same(winner, live);
        Assert.Equal(2, winner.CurrentRevisionNumber);

        // ---- the durable revision history gained exactly one entry --
        var history = await part.GetRevisionHistoryAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("First.", history[^1].Content);
        Assert.DoesNotContain(history, r => r.Content == "Second.");

        // ---- and the DURABLE OBJECT STATE, not an in-memory flag ----
        // The winner's write lands.
        await winner.RenameAsync("Written by the winner");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Written by the winner", state.DisplayName);

        // The predecessor is durably retired: it cannot write again, and
        // nothing it does can displace the winner's record.
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.RenameAsync("Written by the retired predecessor"));

        var after = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(after);
        Assert.Equal("Written by the winner", after.DisplayName);

        // The refused revision produced no second live instance at all —
        // there is exactly one object answering for this Id, and it is the
        // winner, at exactly one revision number.
        Assert.Same(winner, await rig.Context.Repository.FindAsync(part.Id));
    }

    /// <summary>
    /// Characterisation: a revision that fails <em>after</em> the document
    /// store has minted its revision leaves that revision in the
    /// document's durable history with no object at it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `WP 16.4B-R6` moved <c>IEngineeringDocumentStore.ReviseAsync</c>
    /// inside the lock and behind <c>ThrowIfSuperseded</c>, so a revision
    /// <b>refused</b> as a second revision now mints nothing — which is a
    /// real improvement and is pinned elsewhere. It does not, and does not
    /// claim to, make the revision record and the successor atomic: any
    /// failure between the two still leaves the record. The document's
    /// <c>CurrentRevisionNumber</c> advances, no live instance answers at
    /// that number, and the next successful revision skips over it.
    /// </para>
    /// <para>
    /// Reported rather than graded: the only realistic way to reach it is
    /// an exception from a Kind's own <see cref="IRehydratable{TSelf}.Rehydrate"/>,
    /// and every implementation in <c>src/</c> is a pure constructor call.
    /// It is recorded because R6 is the change that put a second failure
    /// point (the Kind's state reader) after the mint, where before there
    /// was only a closure invocation.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionThatFailsAfterMintingItsRevisionRecord_LeavesThatRevisionInTheHistoryWithNoObjectAtIt()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        RevisableFixture.FailNextRehydrate = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => part.ReviseAsync("Never adopted.", "Rev B."));
        }
        finally
        {
            RevisableFixture.FailNextRehydrate = false;
        }

        var history = await part.GetRevisionHistoryAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Never adopted.", history[^1].Content);

        // No object answers at revision 2, and the predecessor is still
        // the live instance at revision 1.
        Assert.Equal(1, part.CurrentRevisionNumber);
        Assert.Same(part, await rig.Context.Repository.FindAsync(part.Id));

        // The next revision skips over it.
        var successor = (RevisableFixture)await part.ReviseAsync("Adopted.", "Rev C.");
        Assert.Equal(3, successor.CurrentRevisionNumber);
    }

    // ================================================================
    // Successor inheritance, end to end
    // ================================================================

    /// <summary>
    /// Every attachment a successor inherits must have readable content —
    /// through the successor, over a real content store, for all of them
    /// and not just the last.
    /// </summary>
    [Fact]
    public async Task ASuccessorInheritsEveryAttachment_WithItsContentStillReadable()
    {
        using var rig = new DiskRig();
        var part = await rig.CreateAsync();

        var expected = new Dictionary<Guid, byte[]>();

        for (var i = 0; i < 3; i++)
        {
            var payload = new byte[] { (byte)i, (byte)(i + 1), (byte)(i + 2) };
            var attachment = await part.AttachContentAsync($"file-{i}.bin", "application/octet-stream", payload);
            expected[attachment.Id] = payload;
        }

        var successor = (RevisableFixture)await part.ReviseAsync("Revised content.", "Rev B.");

        var inherited = await successor.GetAttachmentsAsync();
        Assert.Equal(3, inherited.Count);

        foreach (var attachment in inherited)
        {
            var result = await successor.ReadAttachmentContentAsync(attachment.Id);
            Assert.True(result.IsAvailable, $"Attachment '{attachment.FileName}' came through the revision with no readable content.");
            Assert.Equal(expected[attachment.Id], result.Bytes);
        }

        // The predecessor's own writes before the hand-off are all present
        // and none of them is duplicated.
        Assert.Equal(3, inherited.Select(a => a.Id).Distinct().Count());
    }

    /// <summary>
    /// A predecessor write issued strictly <em>before</em> the revision
    /// is accepted and is carried into the successor; one issued strictly
    /// after is refused. The middle case — issued before, landing after —
    /// is <see cref="RefusedMutationSuccessorLeakageTests"/>.
    /// </summary>
    [Fact]
    public async Task PredecessorWritesBeforeAndAfterTheHandoff_AreAcceptedAndRefusedRespectively()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        await part.RenameAsync("Before the hand-off");

        var successor = (RevisableFixture)await part.ReviseAsync("Revised content.", "Rev B.");
        Assert.Equal("Before the hand-off", successor.DisplayName);

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => part.RenameAsync("After the hand-off"));

        await successor.RenameAsync("Written by the successor");

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Written by the successor", state.DisplayName);
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Whether this object's per-object write lock is currently held, with
    /// no timing whatsoever.
    /// </summary>
    /// <remarks>
    /// <see cref="Concurrency.AsyncKeyedLock.AcquireAsync"/> is an
    /// <see langword="async"/> method whose only await is
    /// <see cref="SemaphoreSlim.WaitAsync(CancellationToken)"/>, which
    /// returns an already-completed task when the semaphore's count is
    /// positive. An uncontended acquisition therefore completes
    /// synchronously and a contended one does not, so
    /// <see cref="Task.IsCompleted"/> answers the question exactly — no
    /// timeout, no sleep, no probability. A contended probe leaves a
    /// waiter queued, which is released by the continuation below as soon
    /// as the holder lets go.
    /// </remarks>
    private static bool ObjectWriteLockIsHeld(EngineeringDomainContext context, Guid objectId)
    {
        var probe = context.AcquireObjectWriteLockAsync(objectId, CancellationToken.None);

        if (probe.IsCompleted)
        {
            probe.GetAwaiter().GetResult().Dispose();
            return false;
        }

        _ = probe.ContinueWith(
            static t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                    t.Result.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return true;
    }

    /// <summary>The attachment an attach produced, or <see langword="null"/> if it was refused.</summary>
    private static async Task<IAttachment?> Outcome(Task<IAttachment> attaching)
    {
        try
        {
            return await attaching.ConfigureAwait(false);
        }
        catch (SupersededEngineeringObjectException)
        {
            return null;
        }
    }

    // ================================================================
    // Rigs
    // ================================================================

    /// <summary>Fakes only — every store completes synchronously unless parked.</summary>
    private sealed class Rig
    {
        public Rig()
        {
            var principal = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            DocumentStore = new FailableDocumentStore(new InMemoryEngineeringDocumentStore(principal));

            Context = new EngineeringDomainContext(
                DocumentStore, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, StateStore, ContentStore);
        }

        public FailableDocumentStore DocumentStore { get; }
        public FakeObjectStateStore StateStore { get; } = new();
        public FakeAttachmentContentStore ContentStore { get; } = new();
        public EngineeringDomainContext Context { get; }

        public async Task<RevisableFixture> CreateAsync() =>
            (RevisableFixture)await new EngineeringObjectFactory<RevisableFixture>(
                    RevisableFixture.KindName, Context,
                    (d, r) => new RevisableFixture(d, r, Context, "PRT-1", "Bracket", EngineeringObjectMetadata.Empty))
                .CreateAsync("Bracket — for test purposes.").ConfigureAwait(false);
    }
    /// <summary>
    /// An ordinary Engineering Object with two seams: its
    /// <c>CaptureTypeState</c> can be parked on demand (the same extension
    /// point every concrete Kind overrides), and its state reader can be
    /// told to throw.
    /// </summary>
    private sealed class RevisableFixture : EngineeringObjectBase, IRehydratable<RevisableFixture>
    {
        public const string KindName = "RevisableFixture";

        [ThreadStatic]
        private static bool _failNextRehydrate;

        private TaskCompletionSource? _parked;
        private TaskCompletionSource? _release;

        public RevisableFixture(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
            string? identifier, string displayName, EngineeringObjectMetadata metadata)
            : base(document, currentRevision, context, identifier, displayName, metadata)
        {
        }

        /// <summary>Makes the next successor construction throw. Thread-static so parallel test classes cannot see each other's setting.</summary>
        public static bool FailNextRehydrate
        {
            get => _failNextRehydrate;
            set => _failNextRehydrate = value;
        }

        public Task ArmNextCapture()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _parked.Task;
        }

        public void ReleaseCapture() => _release?.TrySetResult();

        protected override void CaptureTypeState(IDictionary<string, string?> state)
        {
            var parked = Interlocked.Exchange(ref _parked, null);

            if (parked is not null)
            {
                parked.TrySetResult();
                _release!.Task.GetAwaiter().GetResult();
            }
        }

        static RevisableFixture IRehydratable<RevisableFixture>.Rehydrate(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state)
        {
            if (_failNextRehydrate)
                throw new InvalidOperationException("Simulated failure inside the Kind's own state reader.");

            return new RevisableFixture(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
        }
    }

    private sealed class FailableDocumentStore : IEngineeringDocumentStore
    {
        private readonly IEngineeringDocumentStore _inner;

        public FailableDocumentStore(IEngineeringDocumentStore inner) => _inner = inner;

        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;

        public bool FailNextRevise { get; set; }

        /// <summary>Parks the next document revision. Returns a task that completes once it has been entered.</summary>
        public Task ParkNextRevise()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }

        public void ReleaseParkedRevise() => _release?.TrySetResult();

        public Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(kind, initialContent, cancellationToken);

        public Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            _inner.FindAsync(documentId, cancellationToken);

        public async Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default)
        {
            if (FailNextRevise)
            {
                FailNextRevise = false;
                throw new EngineeringDataException("Simulated document store failure.");
            }

            var entered = Interlocked.Exchange(ref _entered, null);
            if (entered is not null)
            {
                entered.TrySetResult();
                await _release!.Task.ConfigureAwait(false);
            }

            return await _inner.ReviseAsync(documentId, newContent, changeSummary, cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            _inner.GetRevisionHistoryAsync(documentId, cancellationToken);

        public Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default) =>
            _inner.LinkAsync(sourceDocumentId, targetDocumentId, relationshipKind, cancellationToken);

        public Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            _inner.GetReferencesAsync(documentId, cancellationToken);
    }

    private sealed class FakeObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        public bool FailNextSave { get; set; }

        /// <summary>Runs at the top of a save, while whatever locks the caller holds are still held.</summary>
        public Action? OnSave { get; set; }

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            OnSave?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("Simulated object state store failure.");
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

    private sealed class FakeAttachmentContentStore : IAttachmentContentStore
    {
        private readonly Dictionary<Guid, byte[]> _content = new();
        private int _saveCallCount;
        private int _deleteCallCount;
        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;

        /// <summary>Writes the bytes and <em>then</em> throws — a partially-completed durable write.</summary>
        public bool StoreThenFailNextSave { get; set; }

        /// <summary>Runs at the top of a save, while the caller's locks are still held.</summary>
        public Action? OnSave { get; set; }

        /// <summary>Runs after the bytes have landed and before the save returns — the seam a cancellation between the two durable writes needs.</summary>
        public Action? AfterSave { get; set; }

        public int SaveCallCount => Volatile.Read(ref _saveCallCount);
        public int DeleteCallCount => Volatile.Read(ref _deleteCallCount);

        public IReadOnlyCollection<Guid> StoredKeys
        {
            get { lock (_content) { return _content.Keys.ToList(); } }
        }

        /// <summary>Parks the next save. Returns a task that completes once it has been entered.</summary>
        public Task ParkNextSave()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }

        public void ReleaseParkedSave() => _release?.TrySetResult();

        public async Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _saveCallCount);
            OnSave?.Invoke();

            var entered = Interlocked.Exchange(ref _entered, null);
            if (entered is not null)
            {
                entered.TrySetResult();
                await _release!.Task.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (StoreThenFailNextSave)
            {
                StoreThenFailNextSave = false;
                lock (_content) { _content[attachmentId] = content.ToArray(); }
                throw new InvalidOperationException("Simulated content store failure after the bytes landed.");
            }

            lock (_content) { _content[attachmentId] = content.ToArray(); }
            AfterSave?.Invoke();
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content.Span));
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
            Interlocked.Increment(ref _deleteCallCount);
            lock (_content) { _content.Remove(attachmentId); }
            return Task.CompletedTask;
        }

    }
    /// <summary>The real on-disk content store, with a key list and a parkable save.</summary>
    private sealed class ObservableAttachmentContentStore : IAttachmentContentStore
    {
        private readonly IAttachmentContentStore _inner;
        private readonly HashSet<Guid> _keys = new();

        public ObservableAttachmentContentStore(IAttachmentContentStore inner) => _inner = inner;

        /// <summary>Writes the bytes through to the real store and <em>then</em> throws — a partially-completed durable write.</summary>
        public bool StoreThenFailNextSave { get; set; }

        public IReadOnlyCollection<Guid> StoredKeys
        {
            get { lock (_keys) { return _keys.ToList(); } }
        }

        public async Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            var hash = await _inner.SaveAsync(attachmentId, content, cancellationToken).ConfigureAwait(false);
            lock (_keys) { _keys.Add(attachmentId); }

            if (StoreThenFailNextSave)
            {
                StoreThenFailNextSave = false;
                throw new InvalidOperationException("Simulated content store failure after the bytes landed.");
            }

            return hash;
        }

        public Task<AttachmentContentResult> ReadAsync(Guid attachmentId, string? expectedHash, long expectedSizeInBytes, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(attachmentId, expectedHash, expectedSizeInBytes, cancellationToken);

        public async Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            await _inner.DeleteAsync(attachmentId, cancellationToken).ConfigureAwait(false);
            lock (_keys) { _keys.Remove(attachmentId); }
        }

    }

    /// <summary>The real on-disk state store, with a parkable save.</summary>
    private sealed class ObservableObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly IEngineeringObjectStateStore _inner;
        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;

        public ObservableObjectStateStore(IEngineeringObjectStateStore inner) => _inner = inner;

        public Task ParkNextSave()
        {
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }

        public void ReleaseParkedSave() => _release?.TrySetResult();

        public async Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            var entered = Interlocked.Exchange(ref _entered, null);
            if (entered is not null)
            {
                entered.TrySetResult();
                await _release!.Task.ConfigureAwait(false);
            }

            await _inner.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.FindAsync(id, cancellationToken);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(id, cancellationToken);

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default) =>
            _inner.ListAsync(cancellationToken);
    }
}

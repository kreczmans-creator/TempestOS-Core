using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Independent adversarial cover for the interleavings between
/// <see cref="EngineeringObjectBase.AttachContentAsync"/> and
/// <see cref="EngineeringObjectBase.ReviseAsync"/>, re-pointed at the
/// transactional write path (`ADR-0145`, `WP 17.1B`).
/// </summary>
/// <remarks>
/// <para>
/// The board's own reproduction — "a revision parks inside its capture, an
/// attach starts behind it" — is pinned by
/// <see cref="AttachmentRevisionAtomicityTests"/>. Everything here is a
/// <em>different</em> interleaving or a different failure, chosen because
/// this project's repeated failure mode is a fix that satisfies the
/// reported reproduction and leaves the adjacent path broken:
/// </para>
/// <list type="bullet">
/// <item><description>the attach starts <em>first</em> and the revision arrives while it is inside its transaction;</description></item>
/// <item><description>two attaches race one revision, so the successor could inherit two byteless attachments rather than one;</description></item>
/// <item><description>an attach whose commit fails, which decides whether the record, the bytes and the audit row are really one write or only described as one;</description></item>
/// <item><description>a revision that fails part-way, which would wedge <b>every</b> later write in the process if it leaked the one domain write lock;</description></item>
/// <item><description>a second revision arriving concurrently rather than in program order;</description></item>
/// <item><description>a mutation re-entering the object from inside the transaction — the hazard the single non-reentrant lock creates.</description></item>
/// </list>
/// <para>
/// <b>What `ADR-0145` deleted from this file, and why.</b> Three facts here
/// described machinery rather than behaviour: the write-intent marker, the
/// four separate durable steps of an attach and the ordering between them,
/// and the per-object write lock the previous rounds nested the document
/// store's own lock inside. There is one lock and one transaction now, so
/// "the lock is held across all four steps" has no referent; the invariant
/// underneath it does, and is asserted here as
/// <see cref="AnAttachsRecord_ItsBytes_AndItsAuditRow_AreOneTransaction"/>.
/// The per-object lock probe (<c>ObjectWriteLockIsHeld</c>) and its
/// <c>DiskRig</c> are gone with the fakes they observed.
/// </para>
/// <para>
/// <b>Determinism.</b> Nothing here sleeps, polls or races. A writer is
/// parked either inside its own <c>CaptureTypeState</c> — which
/// <c>CaptureState</c> calls from inside the transaction, under the domain
/// write lock — or at the end of its transaction body with
/// <see cref="GatedPersistenceStore"/>, with every write staged and both
/// locks held. A second writer arriving then is blocked on the one domain
/// write lock, so its returned <see cref="Task"/> is provably incomplete on
/// the next line. The only timed values are upper bounds that turn a hang
/// into a reported failure; no fact passes <em>because</em> of one.
/// </para>
/// </remarks>
public sealed class RevisionAttachmentInterleavingTests
{
    /// <summary>
    /// Long enough that no machine fails one of these by being slow, short
    /// enough that a leaked domain write lock is a failed fact rather than
    /// a hung test run. Nothing asserts a value measured against it.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] Bytes = [7, 8, 9];
    private static readonly byte[] OtherBytes = [10, 11, 12, 13];

    // ================================================================
    // Interleavings the board did not name
    // ================================================================

    /// <summary>
    /// <b>The mirror of the board's interleaving.</b> The attach goes
    /// first and is parked at the end of its transaction — bytes staged,
    /// record staged, nothing committed — and the revision arrives behind
    /// it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The invariant asserted is the one that survives either outcome:
    /// <em>an attach is all-or-nothing</em>. Before `WP 16.4B-R6` this
    /// interleaving reached the losing branch with the bytes already
    /// written and then deleted again by the R5 compensation — the
    /// "written then rolled back" shape whose safety argument the board
    /// falsified.
    /// </para>
    /// <para>
    /// <b>What is asserted has changed with the mechanism.</b> The old
    /// fact distinguished "never written" from "written then deleted" by
    /// counting calls on a fake content store, because end states alone
    /// could not tell them apart. The bytes are now staged inside the same
    /// transaction as the record that names them, so the distinction is
    /// visible directly in the store: while the attach is parked, nothing
    /// is committed, and the revision cannot enter the write path at all.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionArrivingWhileAnAttachIsInFlight_IsOrderedBehindIt_AndSeesAllOfItOrNoneOfIt()
    {
        var rig = Rig.WithGate(out var gate);
        var part = await rig.CreateAsync();

        var parked = gate.ArmNextTransaction();
        var attaching = part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);
        await parked;

        // The attach has staged everything and holds the domain write
        // lock. Nothing of it is committed, and the revision cannot start.
        Assert.Empty(rig.ContentKeys);
        Assert.Empty((await rig.StateStore.FindAsync(part.Id))!.Attachments);

        var revising = part.ReviseAsync("Revised content.", "Rev B.");
        Assert.False(revising.IsCompleted, "A revision entered the write path while an attach held the domain write lock.");

        gate.Release();

        var attached = await attaching.WaitAsync(Timeout);
        var successor = (RevisableFixture)await revising.WaitAsync(Timeout);

        // All of it: the bytes, the record and the successor's inherited
        // reference — with content that is really there.
        Assert.True(rig.HasContent(attached.Id));
        Assert.Contains(await successor.GetAttachmentsAsync(), a => a.Id == attached.Id);

        var content = await successor.ReadAttachmentContentAsync(attached.Id);
        Assert.True(content.IsAvailable);
        Assert.Equal(Bytes, content.Bytes);
    }

    /// <summary>
    /// Two attaches racing one revision. The board reproduced the defect
    /// with one attachment; nothing in its evidence said the fix had to
    /// hold for two, and a fix that special-cased "the pending attachment"
    /// would pass the single-attachment test and fail this.
    /// </summary>
    /// <remarks>
    /// Both attaches are started while the revision is parked inside its
    /// own capture — inside its transaction, holding the domain write lock.
    /// Before `WP 16.4B-R6` both got as far as adding themselves to the
    /// list the parked capture was about to copy, both were then refused,
    /// and both compensations deleted their bytes, leaving the live
    /// successor claiming <b>two</b> attachments whose content no longer
    /// existed. Neither can now add anything to the instance before it has
    /// committed, which is asserted directly while they are blocked.
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

        // Neither has touched the instance the parked capture is reading.
        Assert.Empty(await part.GetAttachmentsAsync());
        Assert.Empty(rig.ContentKeys);

        part.ReleaseCapture();

        var successor = (RevisableFixture)await revising.WaitAsync(Timeout);
        _ = await Outcome(first);
        _ = await Outcome(second);

        foreach (var attachment in await successor.GetAttachmentsAsync())
            Assert.True(
                rig.HasContent(attachment.Id),
                $"The live successor claims attachment '{attachment.Id}' ('{attachment.FileName}') but its content is gone.");

        // ...and durably, after one ordinary write by the live successor.
        await successor.RenameAsync("Renamed").WaitAsync(Timeout);

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);

        foreach (var attachment in state.Attachments)
            Assert.True(rig.HasContent(attachment.Id), $"The durable record names attachment '{attachment.Id}' but its content is gone.");
    }

    /// <summary>
    /// An attach following a revision that <em>itself</em> failed. The
    /// predecessor is not superseded after all, so the attach must succeed
    /// completely rather than being refused by a retirement that never
    /// happened.
    /// </summary>
    /// <remarks>
    /// The revision's failure is injected at the commit — the whole body
    /// ran, the revision record and the successor were built, and only the
    /// commit did not land — because that is the case in which a
    /// half-applied retirement would be easiest to leave behind.
    /// </remarks>
    [Fact]
    public async Task AnAttachAfterARevisionThatFailed_StillSucceedsCompletely()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreateAsync();

        failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.ReviseAsync("Revised content.", "Rev B."));

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        Assert.True(rig.HasContent(attachment.Id));
        Assert.Contains(await part.GetAttachmentsAsync(), a => a.Id == attachment.Id);

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);

        // The failed revision left nothing: the object is still at
        // revision 1 and its history has no second entry.
        Assert.Equal(1, part.CurrentRevisionNumber);
        Assert.Single(await part.GetRevisionHistoryAsync());
    }

    // ================================================================
    // The one write lock's own liabilities
    // ================================================================

    /// <summary>
    /// <b>A revision that fails part-way must not leak the domain write
    /// lock.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// `ADR-0145` made the lock domain-wide, so the consequence of leaking
    /// it is strictly worse than it was when the lock was per object: a
    /// lock leaked on a failure path would not merely wedge later writes
    /// to <em>this</em> object, it would wedge <b>every</b> durable write
    /// in the process, to every object, for the life of the process.
    /// </para>
    /// <para>
    /// Both reachable failure points are covered: the commit failing
    /// (after the whole body has run) and the Kind's own
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> throwing from inside
    /// the transaction body, after a revision record has been staged. The
    /// check is behavioural rather than a probe on the semaphore — the
    /// next write completing is what "released" has to mean in practice —
    /// and it is bounded, so a leak is a red fact rather than a hung run.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionThatFailsPartWay_ReleasesTheDomainWriteLock()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreateAsync();
        var other = await rig.CreateAsync();

        failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => part.ReviseAsync("R1.", "Rev B."));

        // Any object at all can still be written — the lock is not this
        // object's, so proving it on a second object is the honest check.
        await other.RenameAsync("Still writable, on another object").WaitAsync(Timeout);

        RevisableFixture.FailNextRehydrate = true;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => part.ReviseAsync("R2.", "Rev C."));
        }
        finally
        {
            RevisableFixture.FailNextRehydrate = false;
        }

        await part.RenameAsync("Still writable").WaitAsync(Timeout);

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Still writable", state.DisplayName);
    }

    /// <summary>
    /// An attach's object-state record, its attachment bytes and its audit
    /// row are <b>one transaction</b>: a failed commit leaves none of the
    /// three, and a successful one leaves all three.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This replaces the fact that asserted the per-object write lock
    /// was held across all four durable steps of an attach.</b> There are
    /// no longer four steps to hold a lock across: the write-intent mark
    /// and the marker clear are deleted, and the two writes that remain go
    /// through one <see cref="IPersistenceTransaction"/> together with the
    /// audit row. The invariant the four-step hold bought — that no
    /// interleaving and no failure can leave some of them without the
    /// others — is what is asserted here, and it is now a property of the
    /// transaction rather than of a lock hold that a later change could
    /// quietly narrow.
    /// </para>
    /// <para>
    /// Each of the three is checked in the store itself rather than
    /// through the object, so a fact that passes because the instance was
    /// not updated is distinguishable from one that passes because nothing
    /// was written.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AnAttachsRecord_ItsBytes_AndItsAuditRow_AreOneTransaction()
    {
        var rig = Rig.WithFailableCommit(out var failing);
        var part = await rig.CreateAsync();

        var auditBefore = rig.AuditRowsFor(part.Id).Count;
        var bodiesBefore = failing.BodiesCompleted;

        failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        // The body ran to completion — every one of the three was staged —
        // and none of them landed.
        Assert.Equal(bodiesBefore + 1, failing.BodiesCompleted);
        Assert.Empty(rig.ContentKeys);
        Assert.Empty((await rig.StateStore.FindAsync(part.Id))!.Attachments);
        Assert.Equal(auditBefore, rig.AuditRowsFor(part.Id).Count);

        // And the same three arrive together when the commit lands.
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        Assert.True(rig.HasContent(attachment.Id));
        Assert.Single((await rig.StateStore.FindAsync(part.Id))!.Attachments);
        Assert.Equal(auditBefore + 1, rig.AuditRowsFor(part.Id).Count);
    }

    /// <summary>
    /// <b>Re-entrancy, characterised.</b> A mutation started on the same
    /// object from inside a transaction is ordered <em>behind</em> that
    /// transaction rather than interleaved with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the standing hazard `ADR-0145` creates, made visible
    /// without hanging a test. The domain write lock is not reentrant and
    /// is taken before the transaction is opened, so code that runs
    /// <em>inside</em> a transaction body and <em>awaits</em> a mutator on
    /// any object at all would deadlock outright — not merely race, and
    /// not merely against this object, because the lock is domain-wide.
    /// </para>
    /// <para>
    /// The seam is the Kind's own <c>CaptureTypeState</c>, which
    /// <c>CaptureState</c> calls from inside the transaction: the same
    /// extension point every concrete Kind overrides, and the one place
    /// outside this assembly's own code that runs there. Nothing in
    /// <c>src/</c> does this — no <c>CaptureTypeState</c> override and no
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> implementation touches
    /// the context — which is precisely why it is worth an executable
    /// record: the constraint is real, invisible from the call site, and
    /// enforced only by nobody having broken it yet.
    /// </para>
    /// <para>
    /// The observation is deterministic, not timed: the task is inspected
    /// for completion, never waited on, while the outer transaction is
    /// still open.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AMutationReenteringTheObjectFromInsideTheTransaction_CannotProceedUntilItCommits()
    {
        var rig = new Rig();
        var part = await rig.CreateAsync();

        Task? reentrant = null;
        part.OnNextCapture = () => reentrant = part.RenameAsync("Renamed from inside the transaction");

        await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);

        Assert.NotNull(reentrant);

        // It could not have completed inside the transaction: the attach
        // held the one domain write lock for the whole of it.
        await reentrant.WaitAsync(Timeout);

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed from inside the transaction", state.DisplayName);
        Assert.Single(state.Attachments);
    }

    /// <summary>
    /// A revision's <b>document write is inside its transaction</b>, so a
    /// mutation issued while it is in flight is ordered behind it and can
    /// see no part of it until it commits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `WP 16.4B-R6` moved <c>IEngineeringDocumentStore.ReviseAsync</c>
    /// inside the per-object write lock, and this fact asserted the
    /// resulting nesting of two locks and the cost it bought: a rename
    /// issued while a revision was writing its document record used to be
    /// accepted and is now refused. `ADR-0145` removes the nesting — there
    /// is one lock and one transaction, and the document store no longer
    /// takes a lock of its own on this path — but the widened window is
    /// exactly as wide, so the cost is unchanged and is still recorded
    /// here.
    /// </para>
    /// <para>
    /// <b>The inverted clause is kept.</b> Until `WP 16.4B-R6b` the last
    /// two assertions were the opposite: the refused rename reached the
    /// successor anyway (P1-F4). They stay negated here, and are now
    /// stronger, because a refused mutation never touches the instance at
    /// all rather than being undone after the fact. See
    /// <c>RefusedMutationSuccessorLeakageTests</c> for the closure in full.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionsDocumentWriteIsInsideItsTransaction_AndAMutationIssuedDuringIt_IsOrderedBehindIt()
    {
        var rig = Rig.WithGate(out var gate);
        var part = await rig.CreateAsync();

        var parked = gate.ArmNextTransaction();
        var revising = Task.Run(() => part.ReviseAsync("Revised content.", "Rev B."));
        await parked;

        // The revision record is staged and invisible: the document write
        // happened inside the transaction, not before it.
        Assert.Single(await part.GetRevisionHistoryAsync());

        var renaming = part.RenameAsync("Issued while the document was being written");
        Assert.False(renaming.IsCompleted, "A rename entered the write path while a revision held the domain write lock.");

        gate.Release();

        var successor = (RevisableFixture)await revising.WaitAsync(Timeout);
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => renaming.WaitAsync(Timeout));

        // The revision landed whole...
        Assert.Equal(2, successor.CurrentRevisionNumber);
        Assert.Equal(2, (await part.GetRevisionHistoryAsync()).Count);

        // ...and the refused rename reached neither the successor nor the
        // predecessor, nor the durable record.
        Assert.Equal("Bracket", successor.DisplayName);
        Assert.Equal("Bracket", part.DisplayName);
        Assert.Equal("Bracket", (await rig.StateStore.FindAsync(part.Id))!.DisplayName);
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
    /// inside its own capture — at which point it is inside its
    /// transaction, holding the one domain write lock, and has already
    /// staged its revision record — and starting the second on the calling
    /// thread behind it. The mid-flight assertion is what changed under
    /// `ADR-0145`: while the first is parked, <em>neither</em> revision
    /// record is durable, because a staged record is not a committed one.
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
        Assert.False(second.IsCompleted, "A second revision entered the write path while the first held the domain write lock.");
        Assert.Single(await part.GetRevisionHistoryAsync());

        part.ReleaseCapture();

        var winner = (RevisableFixture)await first.WaitAsync(Timeout);
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(() => second.WaitAsync(Timeout));

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
        await winner.RenameAsync("Written by the winner").WaitAsync(Timeout);

        var state = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Written by the winner", state.DisplayName);

        // The predecessor is durably retired: it cannot write again, and
        // nothing it does can displace the winner's record.
        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.RenameAsync("Written by the retired predecessor"));

        var after = await rig.StateStore.FindAsync(part.Id);
        Assert.NotNull(after);
        Assert.Equal("Written by the winner", after.DisplayName);

        Assert.Same(winner, await rig.Context.Repository.FindAsync(part.Id));
    }

    /// <summary>
    /// A revision that fails <em>after</em> its revision record has been
    /// written leaves <b>no</b> revision in the document's durable history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This fact is inverted by `ADR-0145`, not deleted.</b> Its
    /// predecessor — <c>ARevisionThatFailsAfterMintingItsRevisionRecord_LeavesThatRevisionInTheHistoryWithNoObjectAtIt</c>
    /// — characterised a residue that `WP 16.4B-R6` explicitly did not
    /// claim to close: the document store minted the revision, the Kind's
    /// own state reader then threw, and the record stayed in the history
    /// with no live object at that number, so the document's
    /// <c>CurrentRevisionNumber</c> advanced and the next successful
    /// revision skipped over it.
    /// </para>
    /// <para>
    /// The revision record and the successor are now written and built
    /// inside one transaction, so a throw from
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> rolls the record back
    /// with everything else. The assertions below are the same ones,
    /// negated — including the last, which used to prove the number was
    /// skipped and now proves it is not.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionThatFailsAfterWritingItsRevisionRecord_LeavesNoRevisionInTheHistory()
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
        Assert.Single(history);
        Assert.DoesNotContain(history, r => r.Content == "Never adopted.");

        // The predecessor is still the live instance, at revision 1.
        Assert.Equal(1, part.CurrentRevisionNumber);
        Assert.Same(part, await rig.Context.Repository.FindAsync(part.Id));

        // And the next revision does NOT skip a number, because none was
        // consumed.
        var successor = (RevisableFixture)await part.ReviseAsync("Adopted.", "Rev C.");
        Assert.Equal(2, successor.CurrentRevisionNumber);
    }

    // ================================================================
    // Successor inheritance, end to end
    // ================================================================

    /// <summary>
    /// Every attachment a successor inherits must have readable content —
    /// through the successor, over the real
    /// <see cref="AttachmentContentStore"/>, for all of them and not just
    /// the last.
    /// </summary>
    /// <remarks>
    /// The content store is the shipped one and the bytes really go
    /// through it, hash check and all; only the substrate underneath is in
    /// memory. That is what makes "readable" a checked claim rather than a
    /// dictionary lookup in a fake.
    /// </remarks>
    [Fact]
    public async Task ASuccessorInheritsEveryAttachment_WithItsContentStillReadable()
    {
        var rig = new Rig();
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
    // Rig
    // ================================================================

    /// <summary>
    /// The shipped write path — the real
    /// <see cref="EngineeringDocumentStore"/>,
    /// <see cref="EngineeringObjectStateStore"/> and
    /// <see cref="AttachmentContentStore"/> — over one
    /// <see cref="InMemoryQueryablePersistenceStore"/>, optionally wrapped
    /// so a fault or a gate can be injected at the store.
    /// </summary>
    /// <remarks>
    /// This replaces the file's four hand-written doubles and its
    /// <c>DiskRig</c>. A store double cannot take part in the one
    /// transaction every engineering change commits through, so there is
    /// nothing left for one to model: a test that wants to fail a write
    /// fails <em>the</em> store, and a test that wants to see what landed
    /// reads <em>the</em> store.
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

            StateStore = new EngineeringObjectStateStore(store);
            ContentStore = new AttachmentContentStore(store);

            Context = new EngineeringDomainContext(
                transactional ?? store,
                new EngineeringDocumentStore(store, principal),
                repository,
                relationships,
                new LifecycleTransitionTable(),
                new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository),
                principal,
                StateStore,
                ContentStore);
        }

        /// <summary>The one durable store — the read surface for every "what landed?" assertion.</summary>
        public InMemoryQueryablePersistenceStore Store { get; }

        public EngineeringObjectStateStore StateStore { get; }

        public AttachmentContentStore ContentStore { get; }

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

        public async Task<RevisableFixture> CreateAsync() =>
            (RevisableFixture)await new EngineeringObjectFactory<RevisableFixture>(
                    RevisableFixture.KindName, Context,
                    (d, r) => new RevisableFixture(d, r, Context, "PRT-1", "Bracket", EngineeringObjectMetadata.Empty))
                .CreateAsync("Bracket — for test purposes.").ConfigureAwait(false);
    }

    /// <summary>
    /// An ordinary Engineering Object with three seams, all on the one
    /// extension point every concrete Kind already overrides: its
    /// <c>CaptureTypeState</c> can be parked on demand, can run a one-shot
    /// callback, and its state reader can be told to throw.
    /// </summary>
    /// <remarks>
    /// <c>CaptureTypeState</c> is reached from <c>CaptureState</c>, which
    /// every mutator and <c>ReviseAsync</c> call from <b>inside</b> the
    /// transaction, under the domain write lock — which is what makes it
    /// the right place to park a writer or to re-enter the object.
    /// </remarks>
    private sealed class RevisableFixture : EngineeringObjectBase, IRehydratable<RevisableFixture>
    {
        public const string KindName = "RevisableFixture";

        [ThreadStatic]
        private static bool _failNextRehydrate;

        private TaskCompletionSource? _parked;
        private TaskCompletionSource? _release;
        private Action? _onNextCapture;

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

        /// <summary>Runs once, inside the next transaction that captures this object's state.</summary>
        public Action? OnNextCapture
        {
            set => _onNextCapture = value;
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
            // Disarm first, so a re-entrant or successor capture is never
            // gated by the same arming.
            Interlocked.Exchange(ref _onNextCapture, null)?.Invoke();

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
}

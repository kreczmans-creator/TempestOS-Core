using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// <b>`WP 16.4B-R7`, Agent C — executable regression proof for `TD-143`,
/// per mutator and per clause of the invariant.</b>
/// </summary>
/// <remarks>
/// <para>
/// The invariant Agent A states, and which every fact in §1 checks in full:
/// <em>if a mutator reports failure because its durable write did not
/// complete, it leaves no durable state, no in-memory state and no audit
/// evidence representing the operation as having happened — including on
/// this object's next successful write.</em>
/// </para>
/// <para>
/// <b>EVERY FACT IN THIS FILE HAS A MEASURED EVIDENTIARY STATUS, STATED IN
/// ITS OWN REMARKS</b>, from a 47-mutant campaign run in a throwaway
/// worktree against the whole `Tempest.Core.Tests` suite:
/// </para>
/// <list type="bullet">
/// <item><description><b>(a) behavioural regression proof</b> — it fails
/// against the `TD-143` defect itself, reinstated either globally (mutant
/// M-C1, the undo removed) or for that one mutator
/// (M-C31..M-C37).</description></item>
/// <item><description><b>(b) mutation-discriminating evidence</b> — it is
/// killed by a specific named, plausible mutant, but not by the defect
/// itself, because the behaviour it pins is a decision <em>inside</em> the
/// remediation rather than the defect it closes.</description></item>
/// <item><description><b>(c) coverage / pin test</b> — killed by nothing.
/// <b>There are none left in this file.</b> Two facts that were
/// (c) were DELETED rather than kept, and the report says which and
/// why.</description></item>
/// </list>
/// <para>
/// A mutant is only counted if it is a plausible way the implementation
/// could have been written or could regress. No mutant here exists solely
/// to manufacture a kill.
/// </para>
/// <para>
/// <b>Why this file exists alongside `MutatorRefusalAdversarialTests` §3 and
/// `R7FalsificationTests` §5.1.</b> Those establish the invariant, but not
/// clause by clause and not mutator by mutator. Between them they leave
/// three measurable holes, each of which a plausible regression walks
/// straight through:
/// </para>
/// <list type="number">
/// <item><description><b>The durable record is never read between the
/// failure and the next successful write.</b> Six of the seven mutators are
/// checked only in memory at that moment, and the following successful write
/// overwrites the whole record from the instance's own fields — so a failed
/// write that had in fact left durable residue would be erased by the very
/// operation used to look for it. Every fact in §1 reads the record
/// <em>immediately</em>, before anything else is written.</description></item>
/// <item><description><b>The "next successful write" clause is checked with
/// a follow-up that overwrites the field under test.</b>
/// `AllSevenMutators_AgainstTheRealDurableStack_KeepNothingWhenTheWriteFails`
/// follows a failed <c>RenameAsync("Renamed")</c> with a successful
/// <c>RenameAsync("Finally")</c>, so a surviving rename could not be seen.
/// Each fact in §1 uses a follow-up that touches a <em>different</em>
/// field.</description></item>
/// <item><description><b>Audit evidence is only ever checked on an object
/// with no audit history at all.</b> `Assert.Empty(History)` cannot tell a
/// history that was never written from one that was cleared. Every object in
/// §1 carries one genuine, durable transition record before the failure is
/// injected, and every fact asserts that exactly that entry — its actor, its
/// endpoints — is what remains.</description></item>
/// </list>
/// <para>
/// §2 pins the validation-rejection path, where nothing was mutated at all.
/// §3 pins every individual term of <c>HoldsTheSameMutableState</c>, the
/// evidence test on which the undo turns. §4 pins the ordering decisions and
/// the state the undo must preserve. §5 characterises an eighth path of the
/// `TD-143` shape that no register row names. Every failure here is
/// deterministically injected. Nothing is timed and nothing is raced.
/// </para>
/// </remarks>
public sealed class R7RegressionProofTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly byte[] Bytes = [4, 5, 6, 7];

    private readonly List<string> _roots = [];

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
    // §1 THE INVARIANT, PER MUTATOR, PER CLAUSE — against the real
    //    durable stack (`PersistenceStore` + `EngineeringObjectStateStore`
    //    + `EngineeringDocumentStore` + `AttachmentContentStore`), with
    //    the failure injected deterministically at the persistence layer.
    //
    //    Each fact establishes, in this order:
    //      (a) the caller is told the operation failed, with the real
    //          exception;
    //      (b) the instance carries no trace of it;
    //      (c) the DURABLE RECORD, read immediately and before any other
    //          write, carries no trace of it — including the audit
    //          history, which still holds exactly the one genuine entry
    //          the object had before;
    //      (d) the object's NEXT SUCCESSFUL WRITE — of a different field,
    //          so it cannot mask the one under test — carries nothing of
    //          the failed operation to disk;
    //      (e) the object is still usable: the same mutator, retried
    //          against a healthy store, works.
    // ================================================================

    /// <summary>1 of 7 — <c>TransitionAsync</c>. The audit clause in full.</summary>
    /// <remarks>
    /// The highest-consequence mutator: `TD-143`'s headline product harm is
    /// a <c>LifecycleTransitionRecord</c> with a real actor principal id
    /// standing in an append-only governance record for a transition the
    /// caller was told had failed, with no removal path anywhere.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> reproduces `TD-143` and fails against it. **Killed by** M-C1 (the undo removed) and M-C34 (`TransitionAsync` mutates outside the rollback unit — the pre-R7 shape of this mutator alone), and by M-C9/M-C21/M-C23 (`RollBackTo` stops restoring `_history` / `_status`, or clears the history instead of restoring it) and M-C12 (`CaptureRollbackPoint` aliases `_history` instead of copying it). Not the sole killer of any of them.</para>
    /// </remarks>
    [Fact]
    public async Task TransitionAsync_WhoseDurableWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-transition");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Approved).WaitAsync(Timeout));

        // (a)
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        // (b)
        Assert.Equal(LifecycleState.InReview, part.Status);
        Assert.Single(part.History);

        // (c) — read before anything else is written.
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed transition");

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

    /// <summary>2 of 7 — <c>RenameAsync</c>.</summary>
    /// <remarks>
    /// The follow-up write is <c>SetBomLineAsync</c> and not another rename,
    /// deliberately: a second rename would overwrite <c>DisplayName</c> and
    /// so could not distinguish an undone rename from a surviving one that
    /// was simply written over. This is the clause `AllSevenMutators_...`
    /// cannot check for this mutator.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C35 (`RenameAsync` mutates outside the rollback unit), and by M-C8e (the `DisplayName` term dropped from `HoldsTheSameMutableState`), M-C19 (`RollBackTo` stops restoring `_displayName`), M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task RenameAsync_WhoseDurableWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-rename");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Leaked name").WaitAsync(Timeout));

        Assert.IsType<PersistenceStoreUnavailableException>(failure);
        Assert.Equal("Bracket", part.DisplayName);

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed rename");

        await part.SetBomLineAsync(9m, "each").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { BomLine = new EngineeringObjectBomLineState(9m, "each", null, null, null) },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        await part.RenameAsync("Renamed on the retry").WaitAsync(Timeout);
        Assert.Equal("Renamed on the retry", (await ReadRecordAsync(rig, part)).DisplayName);
    }

    /// <summary>3 of 7 — <c>MoveAsync</c>, its durable link write failing.</summary>
    /// <remarks>
    /// The link write is the FIRST of <c>MoveAsync</c>'s two durable steps,
    /// so nothing durable happened at all and the undo is exact in both
    /// directions. The companion case — link succeeds, state write then
    /// fails — is §4's
    /// <see cref="MoveAsync_WhoseStateWriteFailsAfterItsLinkWrite_LeavesNoDurableStateOfItsOwn_ButKeepsTheAppendOnlyEdge"/>,
    /// where the invariant holds for this object's own record and a
    /// disclosed relationship residue remains.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C32 (`MoveAsync`'s own inline undo removed — its wiring is a separate call site from the shared helper's), and by M-C8d, M-C15 (the two durable steps reordered), M-C20, M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task MoveAsync_WhoseDurableLinkWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-move-link");
        var baseline = await ReadRecordAsync(rig, part);

        // A parent id with no document behind it: `GuardAgainstCircularParentAsync`
        // does not detect it (an unknown id is not `IHasParent`), and the real
        // `EngineeringDocumentStore` answers the link write with
        // `EngineeringDocumentNotFoundException` — from inside the write lock,
        // after `_parentId` has already been assigned.
        var failure = await Record.ExceptionAsync(() => part.MoveAsync(Guid.NewGuid()).WaitAsync(Timeout));

        Assert.IsType<EngineeringDocumentNotFoundException>(failure);
        Assert.Null(part.ParentId);

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed move");

        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed move" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        var parent = await rig.CreatePartAsync("PRT-P", "Assembly");
        await part.MoveAsync(parent.Id).WaitAsync(Timeout);
        Assert.Equal(parent.Id, (await ReadRecordAsync(rig, part)).ParentId);
    }

    /// <summary>4 of 7 — <c>DeleteAsync</c>.</summary>
    /// <remarks>
    /// There is no undelete anywhere in this platform and twenty-one
    /// `Tempest.App` sites filter <c>IDeletable { IsDeleted: true }</c>, so a
    /// soft delete the caller was told had failed removes the object from the
    /// whole product with no supported way back. The `TD-97` byte release is
    /// asserted NOT to have happened, because the object is not deleted.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C36 (`DeleteAsync` mutates outside the rollback unit), and by M-C8c (the `IsDeleted` term dropped), M-C10 (`RollBackTo` stops restoring `_isDeleted`), M-C12, M-C23, M-C24 and M-C25.</para>
    /// </remarks>
    [Fact]
    public async Task DeleteAsync_WhoseDurableWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-delete");
        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);
        var baseline = await ReadRecordAsync(rig, part);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(() => part.DeleteAsync().WaitAsync(Timeout));

        Assert.IsType<PersistenceStoreUnavailableException>(failure);
        Assert.False(part.IsDeleted, "a delete the caller was told had failed left the object soft-deleted");

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed delete");

        // The object is not deleted, so its content must not have been released.
        Assert.NotNull(await rig.Content.ReadAsync(attachment.Id, null, Bytes.Length));

        await part.RenameAsync("Renamed after the failed delete").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed delete" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        await part.DeleteAsync().WaitAsync(Timeout);
        Assert.True((await ReadRecordAsync(rig, part)).IsDeleted);
    }

    /// <summary>5 of 7 — <c>SetBomLineAsync</c>, all five of its fields.</summary>
    /// <remarks>
    /// `MutatorRefusalAdversarialTests` checks two of the five fields
    /// (<c>Quantity</c> and <c>FindNumber</c>). All five are restored by
    /// <c>RollBackTo</c> and all five are checked here, so a rollback that
    /// forgets one is caught.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C37 (`SetBomLineAsync` mutates outside the rollback unit), and by M-C8b, M-C22, M-C12, M-C23 — and it is the **only fact anywhere in either suite that kills M-C11**, which stops `RollBackTo` restoring `_itemNumber` and `_referenceDesignator`. Two of the five BOM fields have no other guard.</para>
    /// </remarks>
    [Fact]
    public async Task SetBomLineAsync_WhoseDurableWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-bom");
        await part.SetBomLineAsync(3m, "kg", "FN-1", "IT-1", "RD-1").WaitAsync(Timeout);
        var baseline = await ReadRecordAsync(rig, part);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(
            () => part.SetBomLineAsync(17m, "each", "FN-9", "IT-9", "RD-9").WaitAsync(Timeout));

        Assert.IsType<PersistenceStoreUnavailableException>(failure);
        Assert.Equal(3m, part.Quantity);
        Assert.Equal("kg", part.UnitOfMeasure);
        Assert.Equal("FN-1", part.FindNumber);
        Assert.Equal("IT-1", part.ItemNumber);
        Assert.Equal("RD-1", part.ReferenceDesignator);

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed BOM write");

        await part.RenameAsync("Renamed after the failed BOM write").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed BOM write" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        await part.SetBomLineAsync(17m, "each", "FN-9", "IT-9", "RD-9").WaitAsync(Timeout);
        Assert.Equal(17m, (await ReadRecordAsync(rig, part)).BomLine.Quantity);
    }

    /// <summary>6 of 7 — <c>AttachAsync</c>.</summary>
    /// <remarks>
    /// An attach changes no scalar field, which is what makes it the mutator
    /// on which the evidence comparison's <c>Attachments</c> term is the only
    /// thing standing between a failed write and a declined undo.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C31 (`AttachAsync`'s own inline undo removed), and by M-C8 (the `Attachments` term dropped), M-C18 (`RollBackTo` stops restoring `_attachments`), M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task AttachAsync_WhoseDurableWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-attach");
        var baseline = await ReadRecordAsync(rig, part);

        var phantom = new Attachment("phantom.txt", "text/plain", 3);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(() => part.AttachAsync(phantom).WaitAsync(Timeout));

        Assert.IsType<PersistenceStoreUnavailableException>(failure);
        Assert.Empty(await part.GetAttachmentsAsync());

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed attach");

        await part.RenameAsync("Renamed after the failed attach").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed attach" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        await part.AttachAsync(new Attachment("real.txt", "text/plain", 3)).WaitAsync(Timeout);
        Assert.Single((await ReadRecordAsync(rig, part)).Attachments);
    }

    /// <summary>
    /// 7 of 7 — <c>AttachContentAsync</c>, whose state write fails after its
    /// durable content bytes have already landed.
    /// </summary>
    /// <remarks>
    /// The last three assertions are the point of the fact and are NOT part
    /// of the invariant: R7 undoes an in-memory mutation and deletes nothing
    /// durable. The bytes stay written and the write-intent marker stays set,
    /// which is the disclosed, conservative `TD-97` residue —
    /// `WP 16.4B-R5` compensated here and the fifth review board proved that
    /// compensation destroys content a live successor references. A "fix"
    /// that also tidied the bytes away would turn those three lines red, and
    /// should.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C33 (`AttachContentAsync`'s own inline undo removed), and by M-C8, M-C18, M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task AttachContentAsync_WhoseStateWriteFails_LeavesNoDurableState_NoInMemoryState_AndNoAuditEvidence()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-attach-content");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout));

        Assert.IsType<PersistenceStoreUnavailableException>(failure);
        Assert.Empty(await part.GetAttachmentsAsync());

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed content attach");

        await part.RenameAsync("Renamed after the failed content attach").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed content attach" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        // NOT part of the invariant, and deliberately unchanged: nothing
        // durable is undone. One orphaned-but-marked set of bytes remains,
        // which is exactly what `AttachmentContentReconciliationReport.SkippedByMarker`
        // exists to report.
        var marked = await rig.WriteIntents.ListMarkedAsync();
        Assert.Single(marked);
        Assert.NotNull(await rig.Content.ReadAsync(marked.Single(), null, Bytes.Length));

        // And the object is still usable.
        var attached = await part.AttachContentAsync("second.pdf", "application/pdf", Bytes).WaitAsync(Timeout);
        Assert.Contains((await ReadRecordAsync(rig, part)).Attachments, a => a.Id == attached.Id);
    }

    // ================================================================
    // §2 THE VALIDATION-REJECTION PATH — nothing was mutated at all
    //
    //     This is the path Agent B's `B-F4` identified as both the most
    //     routine (five `Tempest.App` command handlers turn these into
    //     ordinary user-visible results) and the one on which `B-F1`'s
    //     defect fired with certainty, because every scalar matches when
    //     nothing has been mutated.
    //
    //     A THIRD FACT WAS DELETED FROM THIS SECTION rather than kept.
    //     `AValidationRejection_MutatesNothing_DurablyOrInMemory_AndThe-
    //     NextWriteCarriesNothing` asserted that an in-table rejection
    //     leaves the instance and the record untouched. NO MUTANT KILLS IT.
    //     The one built for it — M-C29, `TransitionAsync` stamping its
    //     audit entry BEFORE consulting the transition table, which is the
    //     shape `TD-140` was raised about — is NEUTRALISED by the very undo
    //     under test: the rejection enters `RollBackOnFailureAsync`'s catch,
    //     the record does not show the fabricated entry, and the undo
    //     removes it. That is a real and welcome property of the design,
    //     and it is exactly why no fact can discriminate on this path.
    // ================================================================

    /// <summary>
    /// A rejection raised by an argument guard <em>before</em>
    /// <c>MutateAndPersistAsync</c> is entered costs no durable read at all,
    /// while a rejection raised by the mutation itself costs exactly one.
    /// </summary>
    /// <remarks>
    /// Agent B's `B-F4` established the one-read cost for the in-table
    /// rejection. This fact pins the other half — which rejections are and
    /// are not on that path — so the cost is attributed to the right
    /// ordering decision rather than to "validation" in general, and so a
    /// later short-circuit can be measured against a stated baseline. Both
    /// halves are characterisations of a disclosed cost, not defects.
    /// <para><b>Evidentiary status: (b) mutation-discriminating evidence.</b> a characterisation of a disclosed cost (`B-F4`), not a defect. **Killed by** M-C28 — `SetBomLineAsync`'s argument guard moved inside the rollback unit — of which it is the **sole killer in either suite**; also by M-C1 and M-C34.</para>
    /// </remarks>
    [Fact]
    public async Task AnArgumentGuardRejection_CostsNoDurableRead_WhileAnInTableRejection_CostsExactlyOne()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.Reads = 0;
        Assert.IsType<ArgumentOutOfRangeException>(
            await Record.ExceptionAsync(() => part.SetBomLineAsync(0m).WaitAsync(Timeout)));
        Assert.Equal(0, rig.States.Reads);

        rig.States.Reads = 0;
        Assert.IsType<InvalidLifecycleTransitionException>(
            await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Released).WaitAsync(Timeout)));
        Assert.Equal(1, rig.States.Reads);
    }

    /// <summary>
    /// A validation rejection whose evidence read then FAILS still raises
    /// its own exception, and still changes nothing.
    /// </summary>
    /// <remarks>
    /// The rejection path enters <c>RollBackOnFailureAsync</c>'s catch with
    /// nothing mutated. If the evidence read throws, the undo runs — and
    /// must be a no-op, and must not replace the caller's
    /// <see cref="InvalidLifecycleTransitionException"/> with the store's
    /// fault. `B-F1` broke exactly this for a throw from the
    /// <em>comparison</em>; this is the same requirement for a throw from
    /// the <em>read</em>, which was always inside the guard and had no fact
    /// of its own.
    /// <para><b>Evidentiary status: (b) mutation-discriminating evidence.</b> **Killed by** M-C43 — the evidence step's `try`/`catch` removed altogether, so a fault in the diagnostic replaces the caller's `InvalidLifecycleTransitionException`. That is the read-side twin of `B-F1`, which was only ever covered on the comparison side. Also killed by M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task AValidationRejection_WhoseEvidenceReadThrows_StillRaisesItsOwnException_AndChangesNothing()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);

        rig.States.FailReads = true;

        var failure = await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Released).WaitAsync(Timeout));

        Assert.IsType<InvalidLifecycleTransitionException>(failure);
        Assert.Equal(LifecycleState.InReview, part.Status);
        Assert.Single(part.History);

        rig.States.FailReads = false;
        await part.RenameAsync("Renamed").WaitAsync(Timeout);

        var record = rig.States.Peek(part.Id);
        Assert.NotNull(record);
        Assert.Equal(LifecycleState.InReview, record.Status);
        Assert.Single(record.History);
    }

    // ================================================================
    // §3 THE EVIDENCE TEST — every term of `HoldsTheSameMutableState`
    //
    //     `RollBackOnFailureAsync` declines to undo when, and only when,
    //     the durable record already holds the state the instance has.
    //     Each term of that comparison is a field the undo would otherwise
    //     wrongly leave in place. Two of the seven terms — `Status` and
    //     `History` — are mutually redundant against any mutation THIS
    //     type can produce, so nothing in the pre-existing suite could
    //     tell either of them from a constant `true`; see this file's
    //     report entry.
    // ================================================================

    /// <summary>
    /// Every one of the seven terms of the evidence comparison is
    /// load-bearing: a durable record that differs from the instance in
    /// exactly one of them is not evidence, and the mutation is undone.
    /// </summary>
    /// <remarks>
    /// Each sub-case is a store that COMMITS the rename and then throws —
    /// the case an unconditional undo gets wrong — but whose re-read answers
    /// with a record perturbed in exactly one field. The write is not
    /// confirmed, so the undo must fire. Without the perturbation the undo
    /// is correctly declined
    /// (<c>AMutationWhoseStoreCommitsAndThenThrows_IsNotUndone_BecauseTheRecordShowsIt</c>),
    /// which is what makes each sub-case discriminating for its own term
    /// rather than for the mechanism as a whole.
    /// <para><b>Evidentiary status: (a)+(b), per term.</b> each of the seven cases is killed by the mutant that drops **its own** term and by no other term-drop: `Status`→M-C7, `DisplayName`→M-C8e, `ParentId`→M-C8d, `IsDeleted`→M-C8c, `BomLine`→M-C8b, `History`→M-C6, `Attachments`→M-C8. This fact is the **sole killer in either suite of M-C6 and M-C7** — nothing else in the platform can tell the `History` or the `Status` term of `HoldsTheSameMutableState` from a constant `true`. All seven cases are also killed by M-C1, M-C19 and M-C35.</para>
    /// </remarks>
    [Theory]
    [InlineData("Status")]
    [InlineData("DisplayName")]
    [InlineData("ParentId")]
    [InlineData("IsDeleted")]
    [InlineData("BomLine")]
    [InlineData("History")]
    [InlineData("Attachments")]
    public async Task EveryTermOfTheEvidenceComparison_IsLoadBearing(string term)
    {
        Func<EngineeringObjectState, EngineeringObjectState> perturb = term switch
        {
            "Status" => s => s with { Status = LifecycleState.Cancelled },
            "DisplayName" => s => s with { DisplayName = s.DisplayName + " (a record this object never wrote)" },
            "ParentId" => s => s with { ParentId = Guid.NewGuid() },
            "IsDeleted" => s => s with { IsDeleted = !s.IsDeleted },
            "BomLine" => s => s with { BomLine = s.BomLine with { Quantity = s.BomLine.Quantity + 1m } },
            "History" => s => s with
            {
                History = s.History
                    .Append(new EngineeringObjectTransitionState(
                        LifecycleState.InReview, LifecycleState.Approved, "someone-else", DateTimeOffset.UtcNow, null))
                    .ToList(),
            },
            "Attachments" => s => s with { Attachments = [] },
            _ => throw new ArgumentOutOfRangeException(nameof(term), term, "Unknown comparison term."),
        };

        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        await part.TransitionAsync(LifecycleState.InReview).WaitAsync(Timeout);
        await part.AttachAsync(new Attachment("a.txt", "text/plain", 3)).WaitAsync(Timeout);
        await part.SetBomLineAsync(2m, "each").WaitAsync(Timeout);

        rig.States.PerturbReadsWith = perturb;
        rig.States.CommitThenFailNextSave();

        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Renamed").WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        Assert.True(
            part.DisplayName == "Bracket",
            $"The evidence comparison accepted a record differing in '{term}' as proof that this write landed, " +
            "so the undo was declined and the instance kept a rename the caller was told had failed. " +
            $"The '{term}' term of HoldsTheSameMutableState is not load-bearing.");

        // And the undo really is what put it back, rather than the rename
        // never having been applied: the record the store committed holds
        // the rename, and the instance no longer agrees with it.
        Assert.Equal("Renamed", rig.States.Peek(part.Id)!.DisplayName);
    }

    /// <summary>
    /// A failed write against a record that ALREADY holds the target state
    /// is not undone — undoing it is what would create the divergence.
    /// </summary>
    /// <remarks>
    /// The mirror of the sub-cases above, and the case Agent A's §3 argues
    /// for in prose without a fact behind it: the evidence test is not
    /// "did my write land", it is "does the record hold this state", and
    /// when it does — because a durably-identical value was already there —
    /// there is nothing to undo and undoing would make the instance
    /// disagree with disk. The record here is set to the target name by a
    /// writer outside this object, so the rename is durably a no-op.
    /// <para><b>Evidentiary status: (b) mutation-discriminating evidence.</b> **Killed by** M-C2 (the undo made unconditional — the naive fix). Not the sole killer of M-C2: four other facts, one of them pre-existing and written by nobody on this Work Package, also die under it. Its distinct premise is a rename that is durably a *no-op*, rather than a store that commits and then throws.</para>
    /// </remarks>
    [Fact]
    public async Task AFailedWriteAgainstARecordThatAlreadyHoldsTheTargetState_IsNotUndone()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        rig.States.PerturbReadsWith = s => s with { DisplayName = "Renamed" };
        rig.States.FailNextSave();

        var failure = await Record.ExceptionAsync(() => part.RenameAsync("Renamed").WaitAsync(Timeout));
        Assert.IsType<IOException>(failure);

        Assert.Equal("Renamed", part.DisplayName);
    }

    /// <summary>
    /// The evidence read is made with <see cref="CancellationToken.None"/>,
    /// so a cancellation that failed the write does not also destroy the
    /// evidence that decides whether the undo is safe.
    /// </summary>
    /// <remarks>
    /// Agent A's §3 states this as a design decision and nothing measured
    /// it. It only shows on a store that COMMITS and then reports
    /// cancellation: with <see cref="CancellationToken.None"/> the re-read
    /// succeeds, the record confirms the write, and the undo is correctly
    /// declined. Threading the caller's already-cancelled token through
    /// would make the read throw, answer "not established", and undo a
    /// mutation that had durably landed.
    /// <para><b>Evidentiary status: (b) mutation-discriminating evidence.</b> **Killed by** M-C3 — the evidence read given the caller's already-cancelled token instead of `CancellationToken.None` — of which it is the **sole killer in either suite**. Also killed by M-C2.</para>
    /// </remarks>
    [Fact]
    public async Task TheEvidenceRead_IgnoresTheCancellationThatFailedTheWrite()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        using var cancellation = new CancellationTokenSource();
        rig.States.CommitThenCancelNextSave(cancellation);

        var failure = await Record.ExceptionAsync(
            () => part.RenameAsync("Renamed", cancellation.Token).WaitAsync(Timeout));

        Assert.IsType<OperationCanceledException>(failure, exactMatch: false);

        // The write landed. The evidence read proved it, despite the token
        // that failed the write being cancelled, so the undo was declined
        // and the instance still agrees with disk.
        Assert.Equal("Renamed", part.DisplayName);
        Assert.Equal("Renamed", rig.States.Peek(part.Id)!.DisplayName);
    }

    // ================================================================
    // §4 ORDERING — the decisions the undo depends on, each with a
    //    discriminating fact
    //
    //    A FACT WAS DELETED FROM THIS SECTION rather than kept.
    //    `AFailingMarkerClearAfterTheCommittedStateWrite_DoesNotUndoThe-
    //    CommittedAttach` asserted that `AttachContentAsync`'s rollback
    //    unit stopping before the success-path marker clear is load-bearing.
    //    NO MUTANT KILLS IT, including M-C16, which pulls the marker clear
    //    inside the rollback unit — the exact regression it was written for.
    //    The reason is worth recording: the evidence test already protects
    //    that case. The state write has committed, the re-read confirms it,
    //    and the undo is declined on evidence rather than on placement. The
    //    placement is therefore a clarity decision, not a correctness one,
    //    and the pre-existing committed fact
    //    `AFailingMarkerClearOnTheSuccessPath_ReportsAFailureForAFully-
    //    CommittedAttach` says the same thing (and is equally unkilled).
    // ================================================================

    /// <summary>
    /// <c>MoveAsync</c>'s state write failing after its link write has
    /// succeeded leaves no durable state of its own, and leaves the
    /// append-only <c>groupedUnder</c> edge in place.
    /// </summary>
    /// <remarks>
    /// The disclosed residue, on the REAL durable stack rather than an
    /// in-memory document store, and with the object's own record checked
    /// immediately and after the next successful write. Nothing in this
    /// platform removes a relationship; inventing a removal here is the
    /// delete-without-proven-ownership the fifth review board's regression
    /// was made of.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C32, and by M-C15 (the two durable steps reordered), M-C8d, M-C20, M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task MoveAsync_WhoseStateWriteFailsAfterItsLinkWrite_LeavesNoDurableStateOfItsOwn_ButKeepsTheAppendOnlyEdge()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-move-state");
        var parent = await rig.CreatePartAsync("PRT-P", "Assembly");
        var baseline = await ReadRecordAsync(rig, part);

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(() => part.MoveAsync(parent.Id).WaitAsync(Timeout));

        Assert.IsType<PersistenceStoreUnavailableException>(failure);
        Assert.Null(part.ParentId);

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed move");

        await part.RenameAsync("Renamed after the failed move").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed move" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");

        // The disclosed residue: the edge was written and is NOT removed.
        var edges = await part.GetRelationshipsAsync();
        Assert.Contains(edges, e => e.TargetId == parent.Id && e.RelationshipKind == "groupedUnder");
    }

    /// <summary>
    /// The supersession refusal is adjudicated before the rollback
    /// machinery is entered, so a retired instance neither mutates nor pays
    /// for a durable read.
    /// </summary>
    /// <remarks>
    /// An ordering fact rather than a `TD-140` re-check: if
    /// <c>ThrowIfSuperseded()</c> moved inside the rollback unit, the
    /// refusal would still refuse and still mutate nothing, and only the
    /// read count would show it.
    /// <para><b>Evidentiary status: (b) mutation-discriminating evidence.</b> **Killed by** M-C17 (the refusal moved inside `MutateAndPersistAsync`'s rollback unit) and M-C30 (the same, in `AttachAsync` and `MoveAsync`), and by M-C34 and M-C35. Agent B's `TheSupersessionRefusal_StillRefusesBeforeAnythingIsTouched_AndCostsNoDurableRead` dies under the same two, so this fact's marginal value is the three inline mutators B does not cover — `MoveAsync` and `AttachContentAsync` are checked here and nowhere else.</para>
    /// </remarks>
    [Fact]
    public async Task TheSupersessionRefusal_IsAdjudicatedBeforeTheRollbackUnitIsEntered()
    {
        var rig = new ProbeRig();
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");
        await part.ReviseAsync("A revision, for test purposes.", "R7 Agent C").WaitAsync(Timeout);

        rig.States.Reads = 0;

        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.RenameAsync("Leaked name").WaitAsync(Timeout)));
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.TransitionAsync(LifecycleState.Approved).WaitAsync(Timeout)));
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.DeleteAsync().WaitAsync(Timeout)));

        // The three mutators wired INLINE rather than through
        // `MutateAndPersistAsync` each hold their own copy of the refusal,
        // so each is checked separately: a refusal that moved inside any one
        // of these rollback units would still refuse, and only the read
        // count would show it.
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.AttachAsync(new Attachment("a.txt", "text/plain", 3)).WaitAsync(Timeout)));
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(() => part.MoveAsync(Guid.NewGuid()).WaitAsync(Timeout)));
        Assert.IsType<SupersededEngineeringObjectException>(
            await Record.ExceptionAsync(
                () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes).WaitAsync(Timeout)));

        Assert.Equal(0, rig.States.Reads);
        Assert.Equal("Bracket", part.DisplayName);
        Assert.Null(part.ParentId);
        Assert.Empty(await part.GetAttachmentsAsync());

        // AttachContentAsync refuses before it writes any content bytes or
        // any write-intent marker, so the refusal leaves no `TD-97` residue.
        Assert.Empty(rig.Content.StoredKeys);
        Assert.Empty(await rig.WriteIntents.ListMarkedAsync());
    }

    /// <summary>
    /// The undo restores every field the object already held, not only the
    /// one the failed mutator touched — and restores the collections'
    /// existing contents rather than emptying them.
    /// </summary>
    /// <remarks>
    /// Every other fact in this file, and every fact Agents A and B wrote,
    /// injects the failure into an object whose collections are empty or
    /// whose fields still hold their construction defaults, so a
    /// <c>RollBackTo</c> that CLEARED <c>_history</c> and <c>_attachments</c>
    /// instead of restoring them, or a <c>CaptureRollbackPoint</c> that
    /// aliased those lists instead of copying them, would leave every one of
    /// them green. Here the object carries a genuine durable transition
    /// entry, a genuine durable attachment and a genuine durable BOM line
    /// BEFORE the failing mutator runs, and all three must survive the undo
    /// — in memory, and on the object's next successful write.
    /// <para><b>Evidentiary status: (a) behavioural regression proof.</b> **Killed by** M-C1 and M-C31, and by M-C24/M-C25 (`RollBackTo` clears `_attachments` instead of restoring them / `CaptureRollbackPoint` aliases them), M-C8, M-C18, M-C12 and M-C23. Agent A's `TheUndoRestoresTheSameInstances_NotEqualValuedCopies` also kills M-C24/M-C25, so the marginal value here is the durable half: this is the only fact that reads the record on disk immediately after the failure AND after the next successful write, with pre-existing history, attachment and BOM state in place.</para>
    /// </remarks>
    [Fact]
    public async Task TheUndoRestoresWhatTheObjectAlreadyHeld_NotOnlyTheFieldTheMutatorTouched()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-preexisting");
        var existing = await part.AttachContentAsync("existing.pdf", "application/pdf", Bytes).WaitAsync(Timeout);
        await part.SetBomLineAsync(4m, "kg", "FN-4", "IT-4", "RD-4").WaitAsync(Timeout);
        var baseline = await ReadRecordAsync(rig, part);

        var historyBefore = part.History;
        var attachmentsBefore = await part.GetAttachmentsAsync();

        // A failing attach: it touches only `_attachments`, so everything
        // else here is state the undo must leave exactly where it found it.
        rig.Failing.FailNextStateWrite();
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.AttachAsync(new Attachment("phantom.txt", "text/plain", 3)).WaitAsync(Timeout)));

        var historyAfter = part.History;
        var attachmentsAfter = await part.GetAttachmentsAsync();

        Assert.Equal(LifecycleState.InReview, part.Status);
        Assert.Same(historyBefore.Single(), historyAfter.Single());
        Assert.Same(attachmentsBefore.Single(), attachmentsAfter.Single());
        Assert.Equal(existing.Id, attachmentsAfter.Single().Id);
        Assert.Equal(4m, part.Quantity);
        Assert.Equal("RD-4", part.ReferenceDesignator);

        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed attach");

        await part.RenameAsync("Renamed after the failed attach").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed attach" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");
    }

    /// <summary>
    /// A failed move restores the parent the object actually had, not a
    /// default.
    /// </summary>
    /// <remarks>
    /// Every existing fact for <c>MoveAsync</c> — this file's two, Agent A's
    /// and Agent B's — starts from an unparented object, so all of them are
    /// satisfied by a rollback that simply writes <see langword="null"/> into
    /// <c>_parentId</c>. That is not a rollback, and against an object that
    /// already belongs to an assembly it silently un-parents it: the very
    /// harm `TD-144`'s in-memory half describes, arrived at from the
    /// opposite direction.
    /// <para><b>Evidentiary status: (b) mutation-discriminating evidence.</b> **Killed by** M-C38 — `RollBackTo` restoring `_parentId` to `null` rather than to the captured value, the scalar analogue of M-C23/M-C24's restore-to-default shape — of which it is the **sole killer in either suite**, because every other `MoveAsync` fact in the platform starts from an unparented object and is satisfied by a rollback that simply writes `null`. Also killed by M-C1, M-C32, M-C8d, M-C20, M-C12 and M-C23.</para>
    /// </remarks>
    [Fact]
    public async Task AFailedMoveRestoresTheParentTheObjectActuallyHad_NotADefault()
    {
        var (rig, part) = await NewPartWithOneGenuineAuditEntryAsync("r7c-move-reparent");
        var first = await rig.CreatePartAsync("PRT-A", "Assembly A");
        var second = await rig.CreatePartAsync("PRT-B", "Assembly B");

        await part.MoveAsync(first.Id).WaitAsync(Timeout);
        var baseline = await ReadRecordAsync(rig, part);
        Assert.Equal(first.Id, baseline.ParentId);

        rig.Failing.FailNextStateWrite();
        Assert.IsType<PersistenceStoreUnavailableException>(
            await Record.ExceptionAsync(() => part.MoveAsync(second.Id).WaitAsync(Timeout)));

        Assert.Equal(first.Id, part.ParentId);
        AssertRecordIsExactly(baseline, await ReadRecordAsync(rig, part), "immediately after the failed re-move");

        await part.RenameAsync("Renamed after the failed re-move").WaitAsync(Timeout);
        AssertRecordIsExactly(
            baseline with { DisplayName = "Renamed after the failed re-move" },
            await ReadRecordAsync(rig, part),
            "after the next successful write");
    }

    // ================================================================
    // §5 CHARACTERISATION — an EIGHTH path of the same shape, which
    //    `TD-143` does not name and `WP 16.4B-R7` does not close
    // ================================================================

    /// <summary>
    /// An object creation whose initial durable state write fails still
    /// leaves the object registered in the repository, and the object's next
    /// successful write of anything at all makes its state durable — the
    /// `TD-143` shape exactly, on a path that is not one of the seven
    /// mutators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>CHARACTERISATION, not a guard-rail. This asserts what the platform
    /// does today, and it is wrong.</b>
    /// <c>EngineeringObjectFactory{T}.CreateAsync</c> registers the instance
    /// in <c>IEngineeringObjectRepository</c> and only then calls
    /// <c>PersistInitialStateAsync</c>. When that write fails the caller
    /// receives the store's exception and never receives the object — but
    /// the object is in the repository, is returned by <c>FindAsync</c>,
    /// <c>ListByKindAsync</c> and <c>ListAllAsync</c>, and its document has
    /// already been durably created. Any later successful write on it —
    /// reached through the repository, since the caller has no reference —
    /// persists its state, so a creation reported as failed becomes a real,
    /// durable object.
    /// </para>
    /// <para>
    /// This is out of `TD-143`'s scope as the register states it (seven
    /// mutators on <c>EngineeringObjectBase</c>) and out of
    /// `WP 16.4B-R7`'s: <c>PersistInitialStateAsync</c> is not wrapped in
    /// <c>RollBackOnFailureAsync</c>, and there is nothing on the instance to
    /// roll back to — the compensation an undo cannot supply is
    /// <em>unregistering</em>, which is the factory's decision and not this
    /// type's. It is recorded here rather than fixed, and it wants its own
    /// register row.
    /// </para>
    /// <para>
    /// <b>Standing instruction, as everywhere else in this campaign: when
    /// this is closed, INVERT the assertions — do not delete the
    /// fact.</b>
    /// </para>
    /// <para><b>Evidentiary status: (a) behavioural regression proof, inverted — it asserts a defect that is still open.</b> **Killed by** M-C27 — `EngineeringObjectFactory{T}.CreateAsync` registering the instance only after its initial state write succeeds, which is the closure of the finding — of which it is the **sole killer in either suite**. A characterisation fact's discriminator is its own fix, and this one has it.</para>
    /// </remarks>
    [Fact]
    public async Task ACreateWhoseInitialStateWriteFails_StillRegistersTheObject_AndItsNextWriteMakesItDurable()
    {
        var rig = new DurableRig(NewRoot("r7c-create"));

        rig.Failing.FailNextStateWrite();
        var failure = await Record.ExceptionAsync(() => rig.CreatePartAsync("PRT-1", "Bracket"));
        Assert.IsType<PersistenceStoreUnavailableException>(failure);

        // The caller never received the object. The repository has it anyway.
        var registered = Assert.Single(await rig.Context.Repository.ListAllAsync());
        var part = Assert.IsType<Part>(registered);

        // Nothing durable represents it yet...
        Assert.Null(await rig.States.FindAsync(part.Id));

        // ...until its next successful write of anything at all, which makes
        // a creation the caller was told had failed into a durable object.
        await part.RenameAsync("Renamed").WaitAsync(Timeout);

        var state = await rig.States.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed", state.DisplayName);
        Assert.Equal("PRT-1", state.Identifier);
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

    private async Task<(DurableRig Rig, Part Part)> NewPartWithOneGenuineAuditEntryAsync(string label)
    {
        var rig = new DurableRig(NewRoot(label));
        var part = await rig.CreatePartAsync("PRT-1", "Bracket");

        // A real, durable audit entry, so that "no audit evidence for the
        // failed operation" is distinguishable from "an empty history".
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
    /// Compares two durable records over every field any of the seven
    /// mutators can change, including the audit history entry by entry and
    /// the attachment metadata entry by entry.
    /// </summary>
    private static void AssertRecordIsExactly(EngineeringObjectState expected, EngineeringObjectState actual, string when)
    {
        Assert.Equal($"{when}: {Fingerprint(expected)}", $"{when}: {Fingerprint(actual)}");
    }

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

    /// <summary>The real durable stack, with one deterministically failable write.</summary>
    private sealed class DurableRig
    {
        public DurableRig(string root)
        {
            var inner = new PersistenceStore(new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                    [new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root)]))
                .Build());

            Failing = new FailingPersistenceStore(inner);

            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(Failing, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            States = new EngineeringObjectStateStore(Failing);
            Content = new AttachmentContentStore(Failing);

            Context = new EngineeringDomainContext(
                documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, States, Content, WriteIntents);
        }

        public FailingPersistenceStore Failing { get; }

        public EngineeringDomainContext Context { get; }

        public EngineeringObjectStateStore States { get; }

        public AttachmentContentStore Content { get; }

        public MarkerStore WriteIntents { get; } = new();

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");
    }

    /// <summary>
    /// A real <see cref="PersistenceStore"/> whose next write of the object
    /// state collection fails BEFORE anything is committed — the only shape
    /// the shipped store can now produce, and the premise of the invariant.
    /// </summary>
    private sealed class FailingPersistenceStore(PersistenceStore inner) : IPersistenceStore, IBinaryPersistenceStore
    {
        private bool _failNextState;

        public void FailNextStateWrite() => _failNextState = true;

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(collection, key, cancellationToken);

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
        {
            if (_failNextState && collection == EngineeringObjectStateStore.StateCollectionName)
            {
                _failNextState = false;
                throw new PersistenceStoreUnavailableException($"Injected pre-commit failure writing '{collection}'/'{key}'.");
            }

            return inner.WriteAsync(collection, key, value, cancellationToken);
        }

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(collection, key, cancellationToken);

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
            inner.ListKeysAsync(collection, cancellationToken);

        public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.ReadBytesAsync(collection, key, cancellationToken);

        public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
            inner.WriteBytesAsync(collection, key, value, cancellationToken);
    }

    /// <summary>The in-memory rig, for the interleavings the real stack cannot express.</summary>
    private sealed class ProbeRig
    {
        public ProbeRig()
        {
            var principal = new CurrentPrincipalAccessor();
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            Context = new EngineeringDomainContext(
                new InMemoryEngineeringDocumentStore(principal), repository, relationships,
                new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal, States, Content, WriteIntents);
        }

        public EngineeringDomainContext Context { get; }

        public CountingStateStore States { get; } = new();

        public InMemoryAttachmentContentStore Content { get; } = new();

        public MarkerStore WriteIntents { get; } = new();

        public async Task<Part> CreatePartAsync(string identifier, string displayName) =>
            (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, Context,
                    (d, r) => new Part(d, r, Context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync($"{displayName} — for test purposes.");
    }

    /// <summary>
    /// A state store that counts its reads and can fail a write, commit a
    /// write and then fail, commit a write and then report cancellation, or
    /// answer reads with a perturbed record.
    /// </summary>
    private sealed class CountingStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();
        private bool _failNext;
        private bool _commitThenFailNext;
        private CancellationTokenSource? _commitThenCancelNext;

        public int Reads { get; set; }

        public bool FailReads { get; set; }

        /// <summary>Reads answer with this transformation applied — a stale, foreign or partly-unreadable record.</summary>
        public Func<EngineeringObjectState, EngineeringObjectState>? PerturbReadsWith { get; set; }

        public void FailNextSave() => _failNext = true;

        public void CommitThenFailNextSave() => _commitThenFailNext = true;

        public void CommitThenCancelNextSave(CancellationTokenSource cancellation) => _commitThenCancelNext = cancellation;

        public EngineeringObjectState? Peek(Guid id)
        {
            lock (_states) { return _states.TryGetValue(id, out var state) ? state : null; }
        }


        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
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

            if (Interlocked.Exchange(ref _commitThenCancelNext, null) is { } cancellation)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;
            cancellationToken.ThrowIfCancellationRequested();

            if (FailReads)
                throw new IOException("The state record could not be read.");

            EngineeringObjectState? state;
            lock (_states) { state = _states.TryGetValue(id, out var found) ? found : null; }

            if (state is not null && PerturbReadsWith is { } perturb)
                state = perturb(state);

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

    private sealed class InMemoryAttachmentContentStore : IAttachmentContentStore
    {
        private readonly Dictionary<Guid, byte[]> _content = new();

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
            lock (_content) { _content.Remove(attachmentId); }
            return Task.CompletedTask;
        }
    }

    private sealed class MarkerStore : IAttachmentWriteIntentStore
    {
        private readonly HashSet<Guid> _marked = [];
        private bool _failNextClear;

        public void FailNextClear() => _failNextClear = true;

        public Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            lock (_marked) { _marked.Add(attachmentId); }
            return Task.CompletedTask;
        }

        public Task ClearAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            if (_failNextClear)
            {
                _failNextClear = false;
                throw new IOException("The write-intent marker could not be cleared.");
            }

            lock (_marked) { _marked.Remove(attachmentId); }
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<Guid>> ListMarkedAsync(CancellationToken cancellationToken = default)
        {
            lock (_marked) { return Task.FromResult<IReadOnlySet<Guid>>(_marked.ToHashSet()); }
        }
    }
}

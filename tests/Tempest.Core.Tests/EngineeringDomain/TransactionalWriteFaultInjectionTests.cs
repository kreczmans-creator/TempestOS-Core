using Tempest.Workspace.Mechanical;
using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 17.1B`'s acceptance facts: what a failed commit leaves behind
/// (nothing), what a successful one leaves behind (everything), and the
/// two concurrency defects the transaction boundary closes (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// The Work Package states its acceptance as a process kill between the
/// BLOB write and the commit, finding on restart neither the document nor
/// the bytes, and a second kill after the commit finding both. A killed
/// process is not something a unit test can stage deterministically; the
/// property it is reaching for is, and
/// <see cref="CommitFailingPersistenceStore"/> stages exactly it. The
/// transaction body runs to completion — object state, document record,
/// revision, references, attachment bytes and the audit row all written
/// through the handle — and then the commit fails. That is the same
/// durable outcome a kill at that instant produces, and unlike a kill it
/// is deterministic and leaves the store readable so the fact can inspect
/// it.
/// </para>
/// <para>
/// Every fact here reads the store <b>directly</b>, by collection and
/// key, rather than through the domain. A repository or a rehydration
/// pass could agree with a mutation that never landed; the raw
/// collections cannot.
/// </para>
/// </remarks>
public sealed class TransactionalWriteFaultInjectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly byte[] Bytes = [9, 8, 7, 6, 5];

    // ================================================================
    // (a) A failed creation is not in the repository and not on disk
    // ================================================================

    /// <summary>
    /// A creation whose commit fails leaves the repository empty and
    /// every durable collection empty (`TD-147`).
    /// </summary>
    [Fact]
    public async Task AFailedCreation_IsNotInTheRepository_AndNothingIsOnDisk()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var failing = new CommitFailingPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(failing, backing);

        failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => CreatePartAsync(context, "PRT-1", "Bracket"));

        // The transaction body did run to completion — this is a commit
        // failure, not an early throw that never wrote anything.
        Assert.Equal(1, failing.BodiesCompleted);

        Assert.Empty(await context.Repository.ListAllAsync());
        Assert.Empty(backing.CommittedKeys(EngineeringObjectStateStore.StateCollectionName));
        Assert.Empty(backing.CommittedKeys(EngineeringDocumentStore.DocumentsCollectionName));
        Assert.Empty(backing.CommittedKeys(EngineeringDocumentStore.RevisionsCollectionName));
        Assert.Empty(backing.CommittedKeys(AuditRecorder.AuditCollectionName));
        Assert.Equal(0, backing.CommitCount);
        Assert.Equal(1, backing.RollbackCount);
    }

    // ================================================================
    // (b) A failed attach-content leaves no row and no bytes
    // ================================================================

    /// <summary>
    /// An <c>AttachContentAsync</c> whose commit fails leaves neither the
    /// attachment row nor its payload — the failure
    /// <c>AttachmentWriteIntentStore</c> and the reconciliation sweep
    /// existed to detect and clean up after.
    /// </summary>
    [Fact]
    public async Task AFailedAttachContent_LeavesNoRow_AndNoBytes()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var failing = new CommitFailingPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(failing, backing);

        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        failing.FailNextCommit = true;
        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes));

        // No row.
        Assert.Empty(await part.GetAttachmentsAsync());
        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Empty(state.Attachments);

        // No bytes. Not "bytes whose marker the sweep will notice" — none.
        Assert.Empty(backing.CommittedKeys(AttachmentContentStore.ContentCollectionName));
    }

    // ================================================================
    // (c) A successful attach-content leaves both
    // ================================================================

    /// <summary>
    /// The contrast, without which (b) could pass because nothing ever
    /// writes anything: a successful <c>AttachContentAsync</c> leaves the
    /// row and the payload, both readable.
    /// </summary>
    [Fact]
    public async Task ASuccessfulAttachContent_LeavesBothTheRowAndTheBytes()
    {
        var context = TestEngineeringDomain.NewContext(out var store);
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", Bytes);

        // The row, on the instance and on disk.
        Assert.Contains(await part.GetAttachmentsAsync(), a => a.Id == attachment.Id);
        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);

        // The bytes, byte for byte, through the store and through the
        // content store's own verified read.
        Assert.Equal(
            Bytes,
            store.CommittedBytes(AttachmentContentStore.ContentCollectionName, attachment.Id.ToString("N")));

        var read = await context.AttachmentContentStore.ReadAsync(
            attachment.Id, attachment.ContentHash, attachment.SizeInBytes);
        Assert.True(read.IsAvailable);
        Assert.Equal(Bytes, read.Bytes);
    }

    // ================================================================
    // (d) TD-145 — concurrent moves never form a cycle
    // ================================================================

    /// <summary>
    /// Two hundred iterations of the `TD-145` scenario — two moves racing
    /// to make each of two objects the other's parent — never form a
    /// cycle, and exactly one of each pair is refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cycle check used to run before the write lock was taken, so
    /// both moves could independently walk a graph in which neither had
    /// happened yet, both pass, and both commit. The check now runs inside
    /// the transaction under the one domain-wide write lock, so the second
    /// move walks a graph that already contains the first.
    /// </para>
    /// <para>
    /// Two hundred iterations rather than one because this is a race and a
    /// single pass proves nothing about a window; the assertion inside the
    /// loop is deterministic, so a regression fails the fact rather than
    /// flaking it. It is the one fact in this Work Package that is a
    /// genuine race, and it is written so that a failure is a cycle rather
    /// than a timeout.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TwoHundredConcurrentMoveRaces_NeverFormAParentCycle()
    {
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var context = TestEngineeringDomain.NewContext();
            var first = await CreatePartAsync(context, "PRT-A", "A");
            var second = await CreatePartAsync(context, "PRT-B", "B");

            // Each tries to become the other's child, at the same time.
            var moveFirstUnderSecond = Task.Run(() => first.MoveAsync(second.Id));
            var moveSecondUnderFirst = Task.Run(() => second.MoveAsync(first.Id));

            var outcomes = await Task.WhenAll(
                Record.ExceptionAsync(() => moveFirstUnderSecond),
                Record.ExceptionAsync(() => moveSecondUnderFirst));

            var refusals = outcomes.Count(e => e is CircularParentAssignmentException);
            var other = outcomes.FirstOrDefault(e => e is not null and not CircularParentAssignmentException);

            Assert.True(other is null, $"Iteration {iteration} failed with an unexpected exception: {other}");
            Assert.True(
                refusals == 1,
                $"Iteration {iteration}: {refusals} of the two moves were refused; exactly one must be, or both " +
                "committed and the graph now holds a cycle.");

            // And the durable graph really is acyclic: exactly one of the
            // two has a parent, and it is the other one.
            var firstState = await context.ObjectStateStore.FindAsync(first.Id);
            var secondState = await context.ObjectStateStore.FindAsync(second.Id);
            Assert.NotNull(firstState);
            Assert.NotNull(secondState);

            var parented = new[] { firstState, secondState }.Count(s => s.ParentId is not null);
            Assert.True(
                parented == 1,
                $"Iteration {iteration}: {parented} of the two objects has a parent — a cycle if both do.");
        }
    }

    // ================================================================
    // (e) TD-146 — a delete cannot commit while a move gives it a child
    // ================================================================

    /// <summary>
    /// A delete cannot commit while a concurrent move is giving the object
    /// a live child: whichever takes the write lock second sees the
    /// other's committed state and is refused or is safe (`TD-146`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The live-children check used to run outside the write lock, so a
    /// delete could find no children, a move could then give the object
    /// one, and the delete could commit — leaving a live child parented to
    /// a deleted object, which every read model filters out. The check now
    /// runs inside the same transaction as the delete it guards.
    /// </para>
    /// <para>
    /// The gate makes the interleaving deterministic: the move is parked
    /// at the end of its transaction body, having staged the reparent but
    /// not committed it, and the delete is started while it is parked. The
    /// delete blocks on the domain write lock; the move is released and
    /// commits; the delete then runs its check against a graph that
    /// contains the child, and is refused. There is no ordering in which
    /// both succeed.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ADeleteCannotCommitWhileAConcurrentMoveGivesItALiveChild()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var gate = new GatedPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(gate, backing);

        var parent = await CreatePartAsync(context, "PRT-P", "Assembly");
        var child = await CreatePartAsync(context, "PRT-C", "Bracket");

        // The move stages the reparent and parks before committing it.
        var parked = gate.ArmNextTransaction();
        var moving = Task.Run(() => child.MoveAsync(parent.Id));
        await parked;

        // The delete starts here and blocks on the domain write lock.
        var deleting = Task.Run(() => parent.DeleteAsync());

        gate.Release();

        await moving.WaitAsync(Timeout);
        var deleteOutcome = await Record.ExceptionAsync(() => deleting.WaitAsync(Timeout));

        // The move committed.
        var childState = await context.ObjectStateStore.FindAsync(child.Id);
        Assert.NotNull(childState);
        Assert.Equal(parent.Id, childState.ParentId);

        // So the delete cannot have.
        Assert.IsType<EngineeringObjectHasChildrenException>(deleteOutcome);
        Assert.False(parent.IsDeleted);

        var parentState = await context.ObjectStateStore.FindAsync(parent.Id);
        Assert.NotNull(parentState);
        Assert.False(
            parentState.IsDeleted,
            "A delete committed while a concurrent move was giving the object a live child — TD-146.");
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name) =>
        (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, context,
                (d, r) => new Part(d, r, context, identifier, name, EngineeringObjectMetadata.Empty))
            .CreateAsync($"{name} — for test purposes.");
}

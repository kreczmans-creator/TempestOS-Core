using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R3`'s lost-update invariant, re-pointed at the transactional
/// write path (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// The invariant is unchanged: <em>two concurrent mutations of one object
/// both survive on disk; neither silently overwrites the other with a
/// stale snapshot.</em> What changed is why it holds, and the new reason
/// is stronger than the old one.
/// </para>
/// <para>
/// `WP 16.4B-R3` fixed a genuine lost update by serialising each object's
/// capture-then-persist sequence under a per-object write lock. The
/// capture still happened in memory, before the durable write, and the
/// lock existed to stop a second mutation from capturing between the
/// first's capture and its save. Under `ADR-0145` the capture happens
/// <em>inside</em> the transaction, after the domain write lock has been
/// taken — so a mutation cannot hold a snapshot from before another
/// mutation committed, because it does not take its snapshot until it is
/// the only writer.
/// </para>
/// <para>
/// The forced interleaving below is therefore the same in shape and
/// sharper in what it proves. <see cref="GatedPersistenceStore"/> parks
/// the first mutation at the end of its transaction body — everything
/// staged, commit not yet taken, both locks held — and the second
/// mutation starts while it is parked. No <see cref="Task.Delay(int)"/>,
/// no timing dependence, no retry loop; the second call is never awaited
/// before the release, so nothing here can hang whichever way the
/// implementation behaves.
/// </para>
/// </remarks>
public sealed class EngineeringObjectWriteSerializationTests
{
    /// <summary>
    /// Two concurrent <see cref="IHasAttachments.AttachAsync"/> calls both
    /// survive on disk.
    /// </summary>
    /// <remarks>
    /// Before `WP 16.4B-R3` the second call's fresher, two-attachment
    /// snapshot landed first and the first call's stale, one-attachment
    /// snapshot then overwrote it once released — attachment two was
    /// durably lost even though <c>AttachAsync</c> had reported success
    /// for it. The assertion holds regardless of which call enters the
    /// domain write lock first: whichever is second captures the union,
    /// because it captures inside the transaction and the first has
    /// already committed by then.
    /// </remarks>
    [Fact]
    public async Task TwoConcurrentAttachAsyncCalls_BothSurviveOnDisk()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var gate = new GatedPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(gate, backing);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var attachment1 = new Attachment("first.pdf", "application/pdf", 3);
        var attachment2 = new Attachment("second.pdf", "application/pdf", 3);

        // Armed immediately before this specific call, so the creation
        // transaction above was never itself parked waiting for a release
        // nobody had reached yet.
        var parked = gate.ArmNextTransaction();
        var attach1 = part.AttachAsync(attachment1);
        await parked;

        // The second attach starts here and is deliberately not awaited
        // yet: it blocks on the domain write lock the parked transaction
        // still holds, and nothing waits on it before the release below.
        var attach2 = part.AttachAsync(attachment2);

        gate.Release();

        await attach1;
        await attach2;

        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Contains(state.Attachments, a => a.Id == attachment1.Id);
        Assert.Contains(state.Attachments, a => a.Id == attachment2.Id);
    }

    /// <summary>
    /// The same forced interleaving with two genuinely <em>different</em>
    /// mutations — a rename and an attach — proving the write lock
    /// serialises every mutator uniformly, not attachments specifically.
    /// </summary>
    [Fact]
    public async Task ConcurrentRenameAndAttach_BothSurviveOnDisk()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var gate = new GatedPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(gate, backing);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");
        var attachment = new Attachment("drawing.pdf", "application/pdf", 3);

        var parked = gate.ArmNextTransaction();
        var rename = part.RenameAsync("Renamed Bracket");
        await parked;

        var attach = part.AttachAsync(attachment);

        gate.Release();

        await rename;
        await attach;

        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed Bracket", state.DisplayName);
        Assert.Contains(state.Attachments, a => a.Id == attachment.Id);
    }

    /// <summary>
    /// A second writer waiting on the domain lock reads what the first
    /// committed, not what it saw before the first began.
    /// </summary>
    /// <remarks>
    /// The clause the old per-object lock could not state. It serialised
    /// the write, but each mutation had already captured its snapshot in
    /// memory before asking for the lock, so "the second writer's view is
    /// fresh" was true only because of the order the fields happened to be
    /// mutated in. It is now structural: the projection runs inside the
    /// transaction.
    /// </remarks>
    [Fact]
    public async Task TheSecondWriterProjectsFromTheFirstWritersCommittedState()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var gate = new GatedPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(gate, backing);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var parked = gate.ArmNextTransaction();
        var transition = part.TransitionAsync(LifecycleState.InReview);
        await parked;

        var rename = part.RenameAsync("Renamed Bracket");

        gate.Release();

        await transition;
        await rename;

        // The rename's own commit carries the transition, because it
        // projected from the state the transition had committed.
        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed Bracket", state.DisplayName);
        Assert.Equal(LifecycleState.InReview, state.Status);
        Assert.Single(state.History);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }
}

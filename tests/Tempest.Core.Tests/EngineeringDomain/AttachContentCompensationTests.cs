using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R5` — the marker-stranding regression `WP 16.4B-R4` introduced,
/// and the unsynchronised attachment read that predated it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists.</b> `WP 16.4B-R4` made
/// <c>PersistStateAsync</c> throw when a concurrent <c>ReviseAsync</c> has
/// retired the instance. That throw landed inside
/// <c>AttachContentAsync</c>'s four-step durable sequence — mark, write
/// content, write state, clear marker — which had no exception handling,
/// because when it was written only a process crash could interrupt it.
/// The result was a marker stranded set for ever and content bytes the
/// sweep must therefore refuse to collect for ever: a permanent leak
/// reachable by ordinary concurrent use, with no crash involved.
/// </para>
/// <para>
/// The fix compensates rather than unwinding blindly: the in-memory
/// attachment is removed, the content is deleted, and only then is the
/// marker cleared — that order, so that a failure inside the compensation
/// leaves a bounded leak rather than content the sweep would collect while
/// something still believed it existed.
/// </para>
/// </remarks>
public sealed class AttachContentCompensationTests
{
    /// <summary>
    /// The board's reproduction, made an assertion: a state write refused
    /// mid-sequence must leave no marker behind.
    /// </summary>
    [Fact]
    public async Task WhenTheStateWriteIsRefused_TheWriteIntentMarkerIsNotStranded()
    {
        var stateStore = new RecordingObjectStateStore();
        var contentStore = new RecordingAttachmentContentStore();
        var writeIntentStore = new RecordingWriteIntentStore();
        var context = BuildContext(stateStore, contentStore, writeIntentStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        // Retire the instance, so its next durable write is refused.
        _ = await part.ReviseAsync("Revised content.", "Rev B.");

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3 }));

        Assert.Empty(await writeIntentStore.ListMarkedAsync());
    }

    /// <summary>
    /// And no orphaned bytes either — the compensation deletes the content
    /// it wrote, so the sweep is never handed an orphan to reason about.
    /// </summary>
    [Fact]
    public async Task WhenTheStateWriteIsRefused_TheContentBytesAreNotOrphaned()
    {
        var stateStore = new RecordingObjectStateStore();
        var contentStore = new RecordingAttachmentContentStore();
        var writeIntentStore = new RecordingWriteIntentStore();
        var context = BuildContext(stateStore, contentStore, writeIntentStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        _ = await part.ReviseAsync("Revised content.", "Rev B.");

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3 }));

        Assert.Empty(contentStore.StoredKeys);
    }

    /// <summary>
    /// The refused attach must not leave the instance claiming an
    /// attachment it does not have.
    /// </summary>
    [Fact]
    public async Task WhenTheStateWriteIsRefused_TheInstanceDoesNotClaimTheAttachment()
    {
        var stateStore = new RecordingObjectStateStore();
        var contentStore = new RecordingAttachmentContentStore();
        var writeIntentStore = new RecordingWriteIntentStore();
        var context = BuildContext(stateStore, contentStore, writeIntentStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        _ = await part.ReviseAsync("Revised content.", "Rev B.");

        await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3 }));

        Assert.Empty(await part.GetAttachmentsAsync());
    }

    /// <summary>
    /// The ordinary path is untouched: a successful attach still marks,
    /// writes, persists and clears, leaving no marker and real content.
    /// </summary>
    [Fact]
    public async Task TheOrdinaryAttachPath_StillClearsItsMarkerAndKeepsItsContent()
    {
        var stateStore = new RecordingObjectStateStore();
        var contentStore = new RecordingAttachmentContentStore();
        var writeIntentStore = new RecordingWriteIntentStore();
        var context = BuildContext(stateStore, contentStore, writeIntentStore);
        var part = await CreatePartAsync(context, "PART-1", "Bracket");

        var attachment = await part.AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3 });

        Assert.Empty(await writeIntentStore.ListMarkedAsync());
        Assert.Contains(attachment.Id, contentStore.StoredKeys);
        Assert.Contains(await part.GetAttachmentsAsync(), a => a.Id == attachment.Id);
    }

    /// <summary>
    /// `WP 16.4B-R5`, second half: <c>CaptureState</c> must read
    /// <c>_attachments</c> under the same monitor its writers use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before the fix, the projection ran under <c>_structuralLock</c> while
    /// every writer held <c>lock (_attachments)</c> — a different monitor,
    /// so the read was not synchronised at all and could tear.
    /// </para>
    /// <para>
    /// <b>The first version of this test could not fail, and that is worth
    /// recording.</b> It attached four hundred files to one object while
    /// capturing in a loop, and passed three times out of three against the
    /// unlocked code. The reason is mechanical: a <see cref="List{T}"/> only
    /// tears an in-flight projection while it is <em>resizing</em> its
    /// backing array, and four hundred sequential adds to a single list
    /// resize it roughly nine times — so the vulnerable window was entered
    /// about nine times in the whole test, not four hundred. Raising the
    /// attachment count would have been the wrong fix: it makes resizes
    /// rarer per add, not commoner.
    /// </para>
    /// <para>
    /// This version runs many short trials against a <em>fresh</em> object,
    /// so every trial replays the small-list region where resizes are
    /// densest, and the trial count is derived from a measured detection
    /// rate rather than guessed — see the arithmetic at the loop itself.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task CaptureState_IsNotTornByAConcurrentAttach()
    {
        // Sized from a measurement, not a guess. At 120 trials this test
        // detected the reverted code 3 times in 10 runs — a 70% miss rate,
        // which is a test that mostly cannot fail. Solving for the
        // per-trial rate: 1 - 0.70^(1/120) = 0.00297. For 99% confidence
        // that is 1,550 trials; 2,000 predicts 99.74%, and costs ~3s.
        // This is the same arithmetic `WP 16.4A-R1` applied to the
        // double-dispose test after measuring its own 0.45% rate.
        const int trials = 2000;
        const int attachmentsPerTrial = 64;

        for (var trial = 0; trial < trials; trial++)
        {
            var context = BuildContext(new RecordingObjectStateStore(), new RecordingAttachmentContentStore(), writeIntentStore: null);
            var part = await CreatePartAsync(context, $"PART-{trial}", "Bracket");

            var stop = false;

            var capturing = Task.Run(() =>
            {
                while (!Volatile.Read(ref stop))
                {
                    // Throws from inside the projection if the read is torn,
                    // and a coherent snapshot never carries a null entry.
                    var captured = part.CaptureState();
                    Assert.DoesNotContain(captured.Attachments, a => a is null);
                }
            });

            for (var i = 0; i < attachmentsPerTrial; i++)
                await part.AttachAsync(new Attachment($"file-{i}.pdf", "application/pdf", 3));

            Volatile.Write(ref stop, true);
            await capturing;

            Assert.Equal(attachmentsPerTrial, (await part.GetAttachmentsAsync()).Count);
        }
    }

    private static EngineeringDomainContext BuildContext(
        IEngineeringObjectStateStore stateStore,
        IAttachmentContentStore contentStore,
        IAttachmentWriteIntentStore? writeIntentStore)
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var lifecycleTable = new LifecycleTransitionTable();
        var validationRuleSet = new ValidationRuleSet();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);
        var evidenceComposer = new EvidenceComposer(relationshipDiscovery, repository);

        return new EngineeringDomainContext(
            store, repository, relationshipRepository, lifecycleTable, validationRuleSet, evidenceComposer,
            principalAccessor, stateStore, contentStore, writeIntentStore);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private sealed class RecordingObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
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

    private sealed class RecordingAttachmentContentStore : IAttachmentContentStore
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

        public Task<IReadOnlyList<Guid>> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            lock (_content) { return Task.FromResult<IReadOnlyList<Guid>>(_content.Keys.ToList()); }
        }
    }

    private sealed class RecordingWriteIntentStore : IAttachmentWriteIntentStore
    {
        private readonly HashSet<Guid> _marked = new();

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
            lock (_marked) { return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>(_marked)); }
        }
    }
}

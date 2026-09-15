using Tempest.Workspace.Mechanical;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-88`/`WP 21.5B`: the repository's own lazy-materialisation contract —
/// single-flight (a lazy id is materialised at most once no matter how many
/// callers ask concurrently), identity-map preservation (every reader after
/// the first sees the exact same instance), and a rehydrated object with a
/// real revision chain reading its newest end, never a stale earlier one.
/// </summary>
public sealed class LazyMaterialisationTests
{
    private sealed class FakeObject(Guid id, string kind) : IEngineeringObject
    {
        public Guid Id { get; } = id;
        public string Kind { get; } = kind;
        public int CurrentRevisionNumber => 1;
        public DateTimeOffset CreatedAt => DateTimeOffset.UnixEpoch;
        public string BusinessIdentifier => Id.ToString();
    }

    private static EngineeringObjectIndexEntry EntryFor(Guid id, string kind = "Part") =>
        new(id, kind, Identifier: null, DisplayName: "Lazy Object", ParentId: null, Status: LifecycleState.Draft, IsDeleted: false);

    // ----------------------------------------------------------------
    // Single-flight
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentFirstReads_MaterialiseTheMaterialiserExactlyOnce()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var id = Guid.NewGuid();
        var materialised = new FakeObject(id, "Part");
        var invocationCount = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        repository.RegisterLazy(EntryFor(id), async ct =>
        {
            Interlocked.Increment(ref invocationCount);
            await gate.Task.ConfigureAwait(false);
            return materialised;
        });

        // Every reader starts, and is genuinely waiting on the gate, before
        // any of them is allowed to finish — the window a single-flight
        // guard exists to close.
        var readers = Enumerable.Range(0, 25).Select(_ => Task.Run(() => repository.FindAsync(id))).ToList();
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        gate.SetResult();

        var results = await Task.WhenAll(readers);

        Assert.Equal(1, invocationCount);
        Assert.All(results, r => Assert.Same(materialised, r));
    }

    // ----------------------------------------------------------------
    // Identity map
    // ----------------------------------------------------------------

    [Fact]
    public async Task ALazyId_MaterialisesOnce_AndEveryLaterFindAsyncReturnsTheSameInstance()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var id = Guid.NewGuid();
        var materialised = new FakeObject(id, "Part");
        var invocationCount = 0;

        repository.RegisterLazy(EntryFor(id), ct =>
        {
            invocationCount++;
            return Task.FromResult<IEngineeringObject>(materialised);
        });

        var first = await repository.FindAsync(id);
        var second = await repository.FindAsync(id);
        var third = await repository.FindAsync(id);

        Assert.Same(materialised, first);
        Assert.Same(first, second);
        Assert.Same(second, third);
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public async Task AMaterialiserThatThrows_LeavesFindAsyncAnsweringNull_LikeAnUnknownId_NeverThrowingToTheReader()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var id = Guid.NewGuid();

        repository.RegisterLazy(EntryFor(id), ct => throw new InvalidOperationException("This id's own document is corrupt."));

        var result = await repository.FindAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterLazy_NeverDowngradesAnAlreadyLiveId()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var id = Guid.NewGuid();
        var live = new FakeObject(id, "Part");
        repository.Register(live);

        // A rehydration pass that finds this id already live must leave it
        // exactly as it is — never replace it with a lazy row from disk,
        // which could otherwise discard a mutation this process has not
        // yet written back.
        repository.RegisterLazy(EntryFor(id), _ => throw new InvalidOperationException("Must never be called."));

        Assert.Same(live, await repository.FindAsync(id));
    }

    // ----------------------------------------------------------------
    // Project-scoped eager materialisation (Scope #2)
    // ----------------------------------------------------------------

    [Fact]
    public async Task MaterialiseSubtreeAsync_MaterialisesTheRootAndEveryDescendant_ButNothingOutsideIt()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var grandchildId = Guid.NewGuid();
        var outsideId = Guid.NewGuid();
        var materialisedIds = new List<Guid>();

        Func<Guid, Func<CancellationToken, Task<IEngineeringObject>>> materialiserFor = id => ct =>
        {
            materialisedIds.Add(id);
            return Task.FromResult<IEngineeringObject>(new FakeObject(id, "Part"));
        };

        repository.RegisterLazy(EntryFor(rootId), materialiserFor(rootId));
        repository.RegisterLazy(EntryFor(childId) with { ParentId = rootId }, materialiserFor(childId));
        repository.RegisterLazy(EntryFor(grandchildId) with { ParentId = childId }, materialiserFor(grandchildId));
        repository.RegisterLazy(EntryFor(outsideId), materialiserFor(outsideId));

        await repository.MaterialiseSubtreeAsync(rootId);

        // One child per level here, so breadth-first visits root, then
        // child, then grandchild — deterministically, not by construction
        // order coincidence.
        Assert.Equal([rootId, childId, grandchildId], materialisedIds);
        Assert.NotNull(await repository.FindAsync(rootId));
        Assert.NotNull(await repository.FindAsync(childId));
        Assert.NotNull(await repository.FindAsync(grandchildId));

        // The unrelated top-level object is never touched by a subtree walk
        // rooted somewhere else — still lazy, provably: the materialiser
        // above was never asked for it.
        Assert.DoesNotContain(outsideId, materialisedIds);
    }

    // ----------------------------------------------------------------
    // A real revision chain, rehydrated, read from the newest end
    // ----------------------------------------------------------------

    private static (EngineeringDomainContext Domain, EngineeringObjectRehydrationService Service) NewLifetime(
        InMemoryQueryablePersistenceStore persistence)
    {
        var principal = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(persistence, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var stateStore = new EngineeringObjectStateStore(persistence);

        var domain = new EngineeringDomainContext(
            persistence, documentStore, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore);

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);

        return (domain, new EngineeringObjectRehydrationService(domain, rehydrators));
    }

    /// <summary>
    /// `WP 21.5B` Scope #5's own "a rehydration of an estate with a
    /// superseded chain read lazily from the newest end": an object
    /// revised twice before restart persists a three-revision chain
    /// (`ReviseAsync`'s successor supersedes its predecessor, `TD-…`'s own
    /// established rule); the second lifetime's <c>RehydrateAsync</c>
    /// registers it lazily — the revision content is not read at all until
    /// something asks — and the one read that does happen, through
    /// <c>FindAsync</c>, must land on the newest revision's own content,
    /// never the first or the middle one.
    /// </summary>
    [Fact]
    public async Task ARehydratedObjectWithARevisionChain_MaterialisesTheNewestRevisionsContent_NotAnEarlierOne()
    {
        var persistence = new InMemoryQueryablePersistenceStore();
        var (firstDomain, _) = NewLifetime(persistence);

        // `Assembly`, not `Part`: an unenforced Kind (`BusinessIdentifierScope.EnforcedKinds`
        // does not name it), so nothing about this object is materialised
        // as a side effect of `RehydrateAsync`'s own business-identifier
        // rebuild (the kill switch named in this Work Package's report) —
        // the only materialisation this test can observe is the one its
        // own `FindAsync` call below asks for.
        var factory = new EngineeringObjectFactory<Assembly>(
            MechanicalObjectFactoryRegistry.Assembly, firstDomain,
            (doc, rev) => new Assembly(doc, rev, firstDomain, "ASM-CHAIN-1", "Bracket Assembly", EngineeringObjectMetadata.Empty));
        var original = (Assembly)await factory.CreateAsync("Created for the revision-chain test.");

        var successor = (Assembly)await original.ReviseAsync("Revision 2 content.", "Second revision.");
        var newest = (Assembly)await successor.ReviseAsync("Revision 3 — the newest end.", "Third revision.");
        Assert.Equal(3, newest.CurrentRevisionNumber);

        var (secondDomain, secondService) = NewLifetime(persistence);
        var result = await secondService.RehydrateAsync();

        // Index-only so far: `RehydrateAsync` never read a revision's own
        // content (`TD-88`), only proved the object exists.
        Assert.Equal(1, result.ObjectCount);

        var recovered = Assert.IsType<Assembly>(await secondDomain.Repository.FindAsync(original.Id));
        Assert.Equal(3, recovered.CurrentRevisionNumber);
        Assert.Equal("Revision 3 — the newest end.", recovered.Content);

        // The identity map holds this one instance from here on — a second
        // read is not a second materialisation.
        Assert.Same(recovered, await secondDomain.Repository.FindAsync(original.Id));
    }
}

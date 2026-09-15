using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-27`: <see cref="InMemoryEngineeringObjectRepository"/>'s list methods
/// return objects in a stable, documented order — registration order —
/// rather than whatever bucket order the backing
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
/// happened to iterate in. Every test here talks to the repository
/// directly, with a minimal <see cref="IEngineeringObject"/> double: the
/// guarantee under test belongs to the repository alone, not to the wider
/// domain machinery.
/// </summary>
public class InMemoryEngineeringObjectRepositoryOrderTests
{
    private sealed class FakeObject(Guid id, string kind, Guid? parentId = null) : IEngineeringObject, IHasParent
    {
        public Guid Id { get; } = id;
        public string Kind { get; } = kind;
        public int CurrentRevisionNumber => 1;
        public DateTimeOffset CreatedAt => DateTimeOffset.UnixEpoch;
        public string BusinessIdentifier => Id.ToString();
        public Guid? ParentId { get; private set; } = parentId;

        public Task MoveAsync(Guid? newParentId, CancellationToken cancellationToken = default)
        {
            ParentId = newParentId;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ListAllAsync_ReturnsObjectsInRegistrationOrder()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var objects = Enumerable.Range(0, 5).Select(i => new FakeObject(Guid.NewGuid(), "Kind")).ToList();

        foreach (var o in objects)
            repository.Register(o);

        var listed = await repository.ListAllAsync();

        Assert.Equal(objects.Select(o => o.Id), listed.Select(o => o.Id));
    }

    [Fact]
    public async Task ListByKindAsync_ReturnsMatchingObjectsInRegistrationOrder()
    {
        var repository = new InMemoryEngineeringObjectRepository();

        // Interleaved on purpose: registration order and "Part" order are
        // not the same sequence, so a test that only ever registers one
        // kind at a time could pass by accident.
        var part1 = new FakeObject(Guid.NewGuid(), "Part");
        var assembly1 = new FakeObject(Guid.NewGuid(), "Assembly");
        var part2 = new FakeObject(Guid.NewGuid(), "Part");
        var assembly2 = new FakeObject(Guid.NewGuid(), "Assembly");
        var part3 = new FakeObject(Guid.NewGuid(), "Part");

        foreach (var o in new IEngineeringObject[] { part1, assembly1, part2, assembly2, part3 })
            repository.Register(o);

        var parts = await repository.ListByKindAsync("Part");

        Assert.Equal([part1.Id, part2.Id, part3.Id], parts.Select(o => o.Id));
    }

    [Fact]
    public async Task ListChildrenAsync_ReturnsChildrenInRegistrationOrder()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var parentId = Guid.NewGuid();

        var unrelated = new FakeObject(Guid.NewGuid(), "Part");
        var child1 = new FakeObject(Guid.NewGuid(), "Part", parentId);
        var child2 = new FakeObject(Guid.NewGuid(), "Part", parentId);
        var child3 = new FakeObject(Guid.NewGuid(), "Part", parentId);

        repository.Register(unrelated);
        repository.Register(child2);
        repository.Register(child1);
        repository.Register(child3);

        // Registered out of "child1, child2, child3" order deliberately —
        // the returned order must follow registration, not creation intent.
        var children = await repository.ListChildrenAsync(parentId);

        Assert.Equal([child2.Id, child1.Id, child3.Id], children.Select(o => o.Id));
    }

    [Fact]
    public async Task ListChildrenAsync_AfterAMove_ReflectsTheChildsOriginalRegistrationPosition()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var oldParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();

        var firstUnderNewParent = new FakeObject(Guid.NewGuid(), "Part", newParentId);
        var mover = new FakeObject(Guid.NewGuid(), "Part", oldParentId);
        var secondUnderNewParent = new FakeObject(Guid.NewGuid(), "Part", newParentId);

        repository.Register(firstUnderNewParent);
        repository.Register(mover);
        repository.Register(secondUnderNewParent);

        await mover.MoveAsync(newParentId);
        repository.ParentChanged(mover.Id, newParentId);

        var children = await repository.ListChildrenAsync(newParentId);

        // `mover` was registered between the other two, so it sits between
        // them in the order even though it joined this parent later.
        Assert.Equal([firstUnderNewParent.Id, mover.Id, secondUnderNewParent.Id], children.Select(o => o.Id));
    }

    [Fact]
    public async Task ReRegisteringAnExistingId_KeepsItsOriginalPosition_NotThePositionOfTheLatestCall()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var first = new FakeObject(Guid.NewGuid(), "Part");
        var second = new FakeObject(Guid.NewGuid(), "Part");

        repository.Register(first);
        repository.Register(second);

        // A revision re-registers the same id with a new instance — the
        // position earned at first registration must not move to the back.
        var firstRevised = new FakeObject(first.Id, "Part");
        repository.Register(firstRevised);

        var listed = await repository.ListAllAsync();

        Assert.Equal([first.Id, second.Id], listed.Select(o => o.Id));
        Assert.Same(firstRevised, listed[0]);
    }

    [Fact]
    public async Task ConcurrentRegistrations_AllLand_AndProduceAnOrderStableAcrossRepeatedReads()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var objects = Enumerable.Range(0, 200).Select(i => new FakeObject(Guid.NewGuid(), "Part")).ToList();

        await Task.WhenAll(objects.Select(o => Task.Run(() => repository.Register(o))));

        var firstRead = await repository.ListAllAsync();
        var secondRead = await repository.ListAllAsync();

        // No lost or duplicated registration under contention...
        Assert.Equal(objects.Count, firstRead.Count);
        Assert.Equal(objects.Select(o => o.Id).ToHashSet(), firstRead.Select(o => o.Id).ToHashSet());

        // ...and the order settled on is the same on every subsequent read —
        // the defect `TD-27` closes: rows reshuffling between renders.
        Assert.Equal(firstRead.Select(o => o.Id), secondRead.Select(o => o.Id));
    }

    [Fact]
    public async Task ConcurrentRegistrationsAcrossDifferentParents_ListChildrenAsyncIsAlsoStableAcrossRepeatedReads()
    {
        var repository = new InMemoryEngineeringObjectRepository();
        var parentId = Guid.NewGuid();
        var objects = Enumerable.Range(0, 100).Select(i => new FakeObject(Guid.NewGuid(), "Part", parentId)).ToList();

        await Task.WhenAll(objects.Select(o => Task.Run(() => repository.Register(o))));

        var firstRead = await repository.ListChildrenAsync(parentId);
        var secondRead = await repository.ListChildrenAsync(parentId);

        Assert.Equal(objects.Count, firstRead.Count);
        Assert.Equal(firstRead.Select(o => o.Id), secondRead.Select(o => o.Id));
    }
}

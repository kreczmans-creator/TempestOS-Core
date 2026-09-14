using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;
using Tempest.Workspace.Mechanical;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// <see cref="IWorkspaceChanges"/>/<see cref="WorkspaceChangeFeed"/>
/// (`WP 18.1A`): raised once per committed transaction, after the commit,
/// carrying the store sequence and every object touched — never on a
/// rollback, never split across more than one event for one transaction.
/// </summary>
public sealed class WorkspaceChangeFeedTests
{
    private static (EngineeringDomainContext Context, WorkspaceChangeFeed Feed) Build()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var feed = new WorkspaceChangeFeed();
        return (TestEngineeringDomain.NewContextOver(store, store, feed), feed);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            MechanicalObjectFactoryRegistry.Part, context,
            (d, r) => new Part(d, r, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"Part {identifier}.");
    }

    private static async Task<Project> CreateProjectAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, context,
            (d, r) => new Project(d, r, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Project)await factory.CreateAsync($"Project {identifier}.");
    }

    [Fact]
    public async Task ACommittedCreate_RaisesChanged_ExactlyOnce_WithOneEntry()
    {
        var (context, feed) = Build();
        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var change = Assert.Single(changes);
        var entry = Assert.Single(change.Entries);
        Assert.Equal(part.Id, entry.ObjectId);
        Assert.Equal(MechanicalObjectFactoryRegistry.Part, entry.Kind);
        Assert.Equal(WorkspaceChangeType.Created, entry.ChangeType);
    }

    [Fact]
    public async Task ARename_RaisesUpdated()
    {
        var (context, feed) = Build();
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        await part.RenameAsync("Renamed bracket");

        var entry = Assert.Single(Assert.Single(changes).Entries);
        Assert.Equal(WorkspaceChangeType.Updated, entry.ChangeType);
    }

    [Fact]
    public async Task AMove_RaisesMoved()
    {
        var (context, feed) = Build();
        var project = await CreateProjectAsync(context, "P-1", "Apollo");
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        await ((IHasParent)part).MoveAsync(project.Id);

        var entry = Assert.Single(Assert.Single(changes).Entries);
        Assert.Equal(part.Id, entry.ObjectId);
        Assert.Equal(WorkspaceChangeType.Moved, entry.ChangeType);
    }

    [Fact]
    public async Task ADelete_RaisesDeleted()
    {
        var (context, feed) = Build();
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        await ((IDeletable)part).DeleteAsync();

        Assert.Equal(WorkspaceChangeType.Deleted, Assert.Single(Assert.Single(changes).Entries).ChangeType);
    }

    [Fact]
    public async Task AttachContent_RaisesAttachmentAdded()
    {
        var (context, feed) = Build();
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        await ((IHasAttachments)part).AttachContentAsync("drawing.pdf", "application/pdf", new byte[] { 1, 2, 3 });

        Assert.Equal(WorkspaceChangeType.AttachmentAdded, Assert.Single(Assert.Single(changes).Entries).ChangeType);
    }

    [Fact]
    public async Task ALifecycleTransition_RaisesStatusChanged()
    {
        var (context, feed) = Build();
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        await part.TransitionAsync(LifecycleState.InReview);

        Assert.Equal(WorkspaceChangeType.StatusChanged, Assert.Single(Assert.Single(changes).Entries).ChangeType);
    }

    [Fact]
    public async Task ARolledBackWrite_RaisesNothing()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var failing = new CommitFailingPersistenceStore(store) { FailNextCommit = true };
        var feed = new WorkspaceChangeFeed();
        var context = TestEngineeringDomain.NewContextOver(failing, store, feed);

        var changes = new List<WorkspaceChange>();
        feed.Changed += changes.Add;

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => CreatePartAsync(context, "PRT-1", "Bracket"));

        Assert.Empty(changes);
    }

    [Fact]
    public async Task SeveralCommits_RaiseOneEventEach_InCommitOrder()
    {
        var (context, feed) = Build();
        var sequences = new List<long>();
        feed.Changed += change => sequences.Add(change.Sequence);

        await CreatePartAsync(context, "PRT-1", "One");
        await CreatePartAsync(context, "PRT-2", "Two");
        await CreatePartAsync(context, "PRT-3", "Three");

        Assert.Equal(3, sequences.Count);
        Assert.Equal(sequences.OrderBy(s => s), sequences);
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);
    }

    [Fact]
    public void NoPublisher_IsALegitimateNoOp()
    {
        // TestEngineeringDomain.NewContext() (no feed argument) must not
        // throw merely because nothing is listening.
        var context = TestEngineeringDomain.NewContext();
        Assert.NotNull(context);
    }
}

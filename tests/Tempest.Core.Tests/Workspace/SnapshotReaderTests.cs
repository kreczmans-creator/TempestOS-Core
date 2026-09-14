using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// <see cref="WorkspaceSnapshotReader"/> (`WP 18.1A`): every snapshot is
/// read inside one <see cref="IQueryablePersistenceStore.ExecuteInReadTransactionAsync{T}"/>
/// call, against the real durable store, so it never straddles a commit.
/// </summary>
public sealed class SnapshotReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-snapshot-" + Guid.NewGuid().ToString("N"));
    private SqlitePersistenceStore? _store;

    public void Dispose()
    {
        _store?.Dispose();

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private (EngineeringDomainContext Context, IWorkspaceSnapshotReader Snapshots) Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, _root),
            ]))
            .Build();

        _store = new SqlitePersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var documents = new EngineeringDocumentStore(_store, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        var context = new EngineeringDomainContext(
            _store, documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, new EngineeringObjectStateStore(_store));

        return (context, new WorkspaceSnapshotReader(_store));
    }

    private static async Task<Project> CreateProjectAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, context,
            (d, r) => new Project(d, r, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Project)await factory.CreateAsync($"Project {identifier}.");
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            MechanicalObjectFactoryRegistry.Part, context,
            (d, r) => new Part(d, r, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"Part {identifier}.");
    }

    [Fact]
    public async Task ExplorerTree_ListsEveryLiveObject_WithItsParentAndSequence()
    {
        var (context, snapshots) = Build();

        var project = await CreateProjectAsync(context, "P-1", "Apollo");
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");
        await ((IHasParent)part).MoveAsync(project.Id);

        var snapshot = await snapshots.ReadAsync(WorkspaceSnapshotRequest.ExplorerTree());

        Assert.Equal(WorkspaceSnapshotKind.ExplorerTree, snapshot.Kind);
        Assert.Equal(_store!.CurrentSequence, snapshot.Sequence);

        var partNode = Assert.Single(snapshot.ExplorerTree!, n => n.Id == part.Id);
        Assert.Equal(MechanicalObjectFactoryRegistry.Part, partNode.Kind);
        Assert.Equal("Bracket", partNode.DisplayName);
        Assert.Equal(project.Id, partNode.ParentId);

        var projectNode = Assert.Single(snapshot.ExplorerTree!, n => n.Id == project.Id);
        Assert.Null(projectNode.ParentId);
    }

    [Fact]
    public async Task ExplorerTree_OmitsADeletedObject()
    {
        var (context, snapshots) = Build();
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");
        await ((IDeletable)part).DeleteAsync();

        var snapshot = await snapshots.ReadAsync(WorkspaceSnapshotRequest.ExplorerTree());

        Assert.DoesNotContain(snapshot.ExplorerTree!, n => n.Id == part.Id);
    }

    [Fact]
    public async Task Cockpit_CountsLiveAndDeletedObjectsSeparately()
    {
        var (context, snapshots) = Build();
        var live = await CreatePartAsync(context, "PRT-1", "Bracket");
        var toDelete = await CreatePartAsync(context, "PRT-2", "Doomed bracket");
        await ((IDeletable)toDelete).DeleteAsync();

        var snapshot = await snapshots.ReadAsync(WorkspaceSnapshotRequest.Cockpit());

        Assert.Equal(WorkspaceSnapshotKind.Cockpit, snapshot.Kind);
        Assert.NotNull(snapshot.Cockpit);
        Assert.Equal(1, snapshot.Cockpit!.LiveObjectCount);
        Assert.Equal(1, snapshot.Cockpit.DeletedObjectCount);
        Assert.True(live.Id != Guid.Empty); // the live object exists; named for clarity above
    }

    [Fact]
    public async Task ObjectEditorState_ReadsTheRequestedObjectsOwnFields()
    {
        var (context, snapshots) = Build();
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var snapshot = await snapshots.ReadAsync(WorkspaceSnapshotRequest.ObjectEditorState(part.Id, MechanicalObjectFactoryRegistry.Part));

        Assert.Equal(WorkspaceSnapshotKind.ObjectEditorState, snapshot.Kind);
        Assert.Equal("Bracket", snapshot.ObjectState!["DisplayName"]);
        Assert.Equal(MechanicalObjectFactoryRegistry.Part, snapshot.ObjectState["Kind"]);
    }

    [Fact]
    public async Task ObjectEditorState_ForAnObjectThatNeverExisted_IsAnEmptyDictionary_NotNull()
    {
        var (_, snapshots) = Build();

        var snapshot = await snapshots.ReadAsync(WorkspaceSnapshotRequest.ObjectEditorState(Guid.NewGuid(), "Part"));

        Assert.NotNull(snapshot.ObjectState);
        Assert.Empty(snapshot.ObjectState!);
    }

    [Fact]
    public async Task FacetSet_ReadsTheObjectsOwnTypeState()
    {
        var (context, snapshots) = Build();
        var part = (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, context,
                (d, r) => new Part(d, r, context, "PRT-1", "Bracket", EngineeringObjectMetadata.Empty, "AL-7075"))
            .CreateAsync("Part.");

        var snapshot = await snapshots.ReadAsync(WorkspaceSnapshotRequest.FacetSet(part.Id, MechanicalObjectFactoryRegistry.Part));

        Assert.Equal(WorkspaceSnapshotKind.FacetSet, snapshot.Kind);
        Assert.Equal("AL-7075", snapshot.Facets!["MaterialId"]);
    }

    // ----------------------------------------------------------------
    // Coherence: acceptance §3
    // ----------------------------------------------------------------

    [Fact]
    public async Task ASnapshotReadInFlight_NeverObservesATransactionThatCommitsDuringIt_ParentAndChildAgree()
    {
        var (context, snapshots) = Build();
        var project = await CreateProjectAsync(context, "P-1", "Apollo");
        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        var beforeSequence = _store!.CurrentSequence;

        // The read's own snapshot is fixed by the store_sequence read at
        // ExecuteInReadTransactionAsync's very first statement — before the
        // move below (a transaction touching both the parent, via the
        // relationship it records, and the child, via its own ParentId)
        // even begins.
        var readTask = snapshots.ReadAsync(WorkspaceSnapshotRequest.ExplorerTree());

        await ((IHasParent)part).MoveAsync(project.Id);

        var snapshot = await readTask;

        var partNode = Assert.Single(snapshot.ExplorerTree!, n => n.Id == part.Id);

        // Either the read is entirely from before the move (its own
        // sequence unchanged, the child not yet reparented) or entirely
        // from after it (both moved forward together) - never the sequence
        // from before with the child's new parent, or the reverse.
        if (snapshot.Sequence == beforeSequence)
            Assert.Null(partNode.ParentId);
        else
            Assert.Equal(project.Id, partNode.ParentId);

        // A read taken after both are certainly durable always agrees.
        var after = await snapshots.ReadAsync(WorkspaceSnapshotRequest.ExplorerTree());
        Assert.Equal(_store.CurrentSequence, after.Sequence);
        Assert.Equal(project.Id, Assert.Single(after.ExplorerTree!, n => n.Id == part.Id).ParentId);
    }
}

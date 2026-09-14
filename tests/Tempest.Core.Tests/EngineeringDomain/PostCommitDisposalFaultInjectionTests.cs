using Tempest.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-150`'s acceptance facts through the domain: a creation and a
/// mutation whose commit lands but whose connection-close step afterwards
/// fails still register in memory and read back from disk exactly as a
/// fully clean run — the caller is never told a write failed that in fact
/// landed (`ADR-0145` addendum, `WP 19.10J`).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PostCommitFailingPersistenceStore"/> wraps a real
/// <see cref="SqlitePersistenceStore"/> rather than any in-memory double:
/// the failure these facts prove against exists only in that store's own
/// connection-close step, so nothing short of the real store has it to
/// fail. Every fact here therefore runs a real SQLite database under a
/// temporary root — the same reason <see cref="SqlitePersistenceStoreTests"/>
/// does, and why this class cannot reuse <c>TestEngineeringDomain</c>: that
/// helper's <c>backing</c> parameter is typed to the in-memory store so a
/// fault-injecting wrapper's reads still see the real committed state
/// (see its own remarks), which is not this double's shape — here the
/// wrapped store <em>is</em> the one to read back through, unwrapped.
/// </para>
/// <para>
/// Every fact also reads the shared <see cref="RecordingLogger"/> both
/// stores log through, to confirm the swallowed close failure was not
/// merely absorbed but actually recorded (`SqlitePersistenceStore
/// .ExecuteInTransactionAsync`'s own remarks) — a fix that silently ate
/// the exception without leaving a trace would pass every other assertion
/// here and still be worse than the defect.
/// </para>
/// </remarks>
public sealed class PostCommitDisposalFaultInjectionTests : IDisposable
{
    private const string CloseFailureFragment = "could not close its connection";

    private readonly TempDirectory _root = new();
    private readonly List<SqlitePersistenceStore> _stores = [];

    public void Dispose()
    {
        for (var i = _stores.Count - 1; i >= 0; i--)
            _stores[i].Dispose();

        _root.Dispose();
    }

    private (EngineeringDomainContext Context, RecordingLogger Logger) BuildContext()
    {
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, _root.Path),
        ])).Build();

        var logger = new RecordingLogger();
        var store = new SqlitePersistenceStore(configuration, logger);
        _stores.Add(store);

        // Fails the close step of every transaction this store runs from
        // here on, after each one's own COMMIT has already returned.
        var failingClose = new PostCommitFailingPersistenceStore(store);

        var principalAccessor = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

        var context = new EngineeringDomainContext(
            failingClose,
            new EngineeringDocumentStore(store, principalAccessor),
            repository,
            relationshipRepository,
            new LifecycleTransitionTable(),
            new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository),
            principalAccessor,
            new EngineeringObjectStateStore(store),
            new AttachmentContentStore(store),
            logger: logger);

        return (context, logger);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name) =>
        (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, context,
                (d, r) => new Part(d, r, context, identifier, name, EngineeringObjectMetadata.Empty))
            .CreateAsync($"{name} — for test purposes.");

    // ================================================================
    // (a) A creation whose commit lands and whose close fails still
    //     registers the object and returns it.
    // ================================================================

    [Fact]
    public async Task ACreation_WhoseCloseFailsAfterCommit_StillRegistersTheObject_AndReturnsIt()
    {
        var (context, logger) = BuildContext();

        var part = await CreatePartAsync(context, "PRT-1", "Bracket");

        Assert.NotNull(part);
        Assert.Same(part, await context.Repository.FindAsync(part.Id));

        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Bracket", state.DisplayName);

        Assert.Contains(logger.Messages, m => m.Contains(CloseFailureFragment, StringComparison.Ordinal));
    }

    // ================================================================
    // (b) A mutation through MutateAndPersistAsync whose commit lands and
    //     whose close fails still applies in memory and is on disk.
    // ================================================================

    [Fact]
    public async Task AMutation_WhoseCloseFailsAfterCommit_StillAppliesInMemory_AndIsOnDisk()
    {
        var (context, logger) = BuildContext();

        var part = await CreatePartAsync(context, "PRT-2", "Original");

        await part.RenameAsync("Renamed");

        // Applied in memory — the instance's own field, not a re-read.
        Assert.Equal("Renamed", part.DisplayName);

        // On disk — read back through the state store directly.
        var state = await context.ObjectStateStore.FindAsync(part.Id);
        Assert.NotNull(state);
        Assert.Equal("Renamed", state.DisplayName);

        // Two transactions ran (create, rename); both committed and both
        // had their close step fail and swallowed.
        Assert.True(
            logger.Messages.Count(m => m.Contains(CloseFailureFragment, StringComparison.Ordinal)) >= 2,
            "Expected both the creation and the rename to log a swallowed close failure.");
    }
}

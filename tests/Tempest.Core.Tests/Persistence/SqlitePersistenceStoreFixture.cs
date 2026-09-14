using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The shared fixture every store-contract test derives from: one
/// temporary root, one <see cref="SqlitePersistenceStore"/> over it, and
/// disposal of both in the right order.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the dual-backend <c>PersistenceStoreBackendFixture&lt;TBackend&gt;</c>
/// (`ADR-0144`, `WP 17.1A`), deleted by `WP 18.1A` along with the
/// file-per-key store it ran a second time against. Every claim the store
/// classes derived from it make — round-trip, overwrite, deletion,
/// listing, collection isolation, byte fidelity, hostile names,
/// transactional atomicity — is a claim about <em>the store</em>, and is
/// unchanged; it is asserted once now, against the one backend that
/// exists, rather than once per backend.
/// </para>
/// <para>
/// The store is disposed before the directory is deleted, which is not
/// decoration: <see cref="SqlitePersistenceStore"/> holds the database
/// file and the root's instance lock until it is disposed, so a fixture
/// that deleted the directory first would be testing its own teardown
/// rather than the store.
/// </para>
/// </remarks>
public abstract class SqlitePersistenceStoreFixture : IDisposable
{
    private readonly TempDirectory _temporaryRoot = new();
    private readonly List<SqlitePersistenceStore> _stores = [];

    private SqlitePersistenceStore? _store;

    /// <summary>This fixture's own persistence root. Deleted when the test finishes.</summary>
    protected string RootPath => _temporaryRoot.Path;

    /// <summary>The store under test, created over <see cref="RootPath"/> on first use.</summary>
    protected SqlitePersistenceStore Store => _store ??= NewStore();

    /// <summary>The same store, in its byte shape.</summary>
    protected IBinaryPersistenceStore BinaryStore => Store;

    /// <summary>The same store, in its query shape (`ADR-0144`).</summary>
    protected IQueryablePersistenceStore QueryableStore => Store;

    /// <summary>
    /// Builds a configuration naming <paramref name="rootPath"/> as the
    /// persistence root.
    /// </summary>
    protected static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    /// <summary>
    /// Creates an additional store over <see cref="RootPath"/>, disposed
    /// with this fixture. Most tests want <see cref="Store"/>; this exists
    /// for the few that need a second one — after <see cref="ReleaseStores"/>,
    /// never alongside a still-live one, since <see cref="SqlitePersistenceStore"/>
    /// holds its root's instance lock exclusively.
    /// </summary>
    protected SqlitePersistenceStore NewStore()
    {
        var store = new SqlitePersistenceStore(BuildConfiguration(_temporaryRoot.Path));
        _stores.Add(store);
        return store;
    }

    /// <summary>
    /// Disposes every store this fixture has created, leaving the root
    /// directory and its contents in place — so that a test may then open
    /// the same root with a new store, which is the only way to assert
    /// that what was written is genuinely durable rather than merely
    /// cached.
    /// </summary>
    protected void ReleaseStores()
    {
        for (var i = _stores.Count - 1; i >= 0; i--)
            _stores[i].Dispose();

        _stores.Clear();
        _store = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ReleaseStores();
        _temporaryRoot.Dispose();
        GC.SuppressFinalize(this);
    }
}

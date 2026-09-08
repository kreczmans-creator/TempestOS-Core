using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// One of the platform's persistence backends, as a thing a test fixture
/// can construct (`ADR-0144`, `WP 17.1A`).
/// </summary>
/// <remarks>
/// Exists so that the store contract is written once and run once per
/// backend, rather than copied. Before `ADR-0144` there was one backend
/// and the three store test classes named it directly; there are now two,
/// and every claim those classes make — round-trip, overwrite, deletion,
/// listing, collection isolation, byte fidelity, hostile names,
/// concurrency — is a claim about <em>the store</em>, not about a file
/// tree, so it must hold for both. What is genuinely about a file tree
/// stays in a file-store-only class and says so at the class.
/// </remarks>
public interface IPersistenceStoreBackend
{
    /// <summary>This backend's name, for a test message that needs to say which one failed.</summary>
    string Name { get; }

    /// <summary>
    /// Whether records in this backend are files, so a test may assert on
    /// the tree. False for SQLite, where the root holds one database.
    /// </summary>
    bool RecordsAreFiles { get; }

    /// <summary>Creates a store of this backend over <paramref name="configuration"/>'s root.</summary>
    IPersistenceStore Create(IConfigurationProvider configuration);
}

/// <summary>The file-per-key <see cref="PersistenceStore"/>. Deleted in <c>v0.18.0</c> (`ADR-0144`).</summary>
public sealed class FileStoreBackend : IPersistenceStoreBackend
{
    /// <inheritdoc />
    public string Name => "file-per-key";

    /// <inheritdoc />
    public bool RecordsAreFiles => true;

    /// <inheritdoc />
    public IPersistenceStore Create(IConfigurationProvider configuration) => new PersistenceStore(configuration);
}

/// <summary>The <see cref="SqlitePersistenceStore"/>. The default (`ADR-0144`).</summary>
public sealed class SqliteStoreBackend : IPersistenceStoreBackend
{
    /// <inheritdoc />
    public string Name => "sqlite";

    /// <inheritdoc />
    public bool RecordsAreFiles => false;

    /// <inheritdoc />
    public IPersistenceStore Create(IConfigurationProvider configuration) => new SqlitePersistenceStore(configuration);
}

/// <summary>
/// The shared fixture every backend-agnostic store test derives from: one
/// temporary root, one store over it, and disposal of both in the right
/// order.
/// </summary>
/// <typeparam name="TBackend">The backend under test.</typeparam>
/// <remarks>
/// The store is disposed before the directory is deleted, which is not
/// decoration: <see cref="SqlitePersistenceStore"/> holds the database
/// file and the root's instance lock until it is disposed, so a fixture
/// that deleted the directory first would be testing its own teardown
/// rather than the store.
/// </remarks>
public abstract class PersistenceStoreBackendFixture<TBackend> : IDisposable
    where TBackend : IPersistenceStoreBackend, new()
{
    private readonly TempDirectory _temporaryRoot = new();
    private readonly List<IPersistenceStore> _stores = [];

    private IPersistenceStore? _store;

    /// <summary>The backend under test.</summary>
    protected TBackend Backend { get; } = new();

    /// <summary>This fixture's own persistence root. Deleted when the test finishes.</summary>
    protected string RootPath => _temporaryRoot.Path;

    /// <summary>The store under test, created over <see cref="RootPath"/> on first use.</summary>
    protected IPersistenceStore Store => _store ??= NewStore();

    /// <summary>The same store, in its byte shape. Both backends satisfy both shapes from one instance.</summary>
    protected IBinaryPersistenceStore BinaryStore => (IBinaryPersistenceStore)Store;

    /// <summary>The same store, in its query shape (`ADR-0144`).</summary>
    protected IQueryablePersistenceStore QueryableStore => (IQueryablePersistenceStore)Store;

    /// <summary>
    /// Builds a configuration naming <paramref name="rootPath"/> as the
    /// persistence root.
    /// </summary>
    protected static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    /// <summary>
    /// Creates an additional store of the backend under test over
    /// <see cref="RootPath"/>, disposed with this fixture. Most tests want
    /// <see cref="Store"/>; this exists for the few that need a second one.
    /// </summary>
    protected IPersistenceStore NewStore()
    {
        var store = Backend.Create(BuildConfiguration(_temporaryRoot.Path));
        _stores.Add(store);
        return store;
    }

    /// <summary>
    /// Disposes every store this fixture has created, leaving the root
    /// directory and its contents in place — so that a test may then open
    /// the same root with a new store, which is the only way to assert
    /// that what was written is genuinely durable rather than merely
    /// cached. Required rather than optional on the SQLite backend, which
    /// holds the root's instance lock until the store is disposed.
    /// </summary>
    protected void ReleaseStores()
    {
        for (var i = _stores.Count - 1; i >= 0; i--)
            (_stores[i] as IDisposable)?.Dispose();

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

using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing;

/// <summary>One repeating bill together with the category <see cref="AccountsCategoriser"/> resolved it to, at the moment it was read (`WP 19.8B`).</summary>
public sealed record CategorisedRepeatingBill(RepeatingBill Bill, AccountsCategory Category);

/// <summary>
/// The last successful draw from the accounting package — bills, repeating
/// bills (each already categorised), the cash position, when it was read
/// and which connector read it (`WP 19.8B`, po-comments.md item 8, scope
/// §2). The only thing this platform ever writes about accounts data:
/// <see cref="AccountsRefreshService"/> replaces this whole record on
/// every successful refresh; nothing in TempestOS computes or edits any
/// field on it.
/// </summary>
public sealed record AccountsReading(
    IReadOnlyList<BillDue> Bills,
    IReadOnlyList<CategorisedRepeatingBill> RepeatingBills,
    IReadOnlyList<CashAccountBalance> Cash,
    DateTimeOffset ReadAt,
    string Connector);

/// <summary>
/// Where the last <see cref="AccountsReading"/> is kept between runs — not
/// the engineering persistence database, and never a secret (`WP 19.8B`
/// scope §2): a connector read is neither engineering state (`ADR-0144`)
/// nor a credential (`ADR-0151`), so it lives in its own small file
/// alongside them under the persistence root, exactly as
/// <see cref="Secrets.ISecretStore"/>'s own file-backed implementations do
/// for tokens.
/// </summary>
public interface IAccountsReadingStore
{
    /// <summary>Reads the last saved reading, or <see langword="null"/> when nothing has ever been saved (or the file cannot be read as one — treated the same as "nothing yet", never a thrown exception).</summary>
    Task<AccountsReading?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the saved reading with <paramref name="reading"/>.</summary>
    Task SaveAsync(AccountsReading reading, CancellationToken cancellationToken = default);
}

/// <summary>
/// The only <see cref="IAccountsReadingStore"/> implementation: one JSON
/// file, <c>&lt;persistence root&gt;/accounts/last-reading.json</c>
/// (`WP 19.8B`).
/// </summary>
public sealed class FileAccountsReadingStore : IAccountsReadingStore
{
    /// <summary>The directory name under the persistence root the reading file lives beneath.</summary>
    public const string AccountsDirectoryName = "accounts";

    /// <summary>The file name the last reading is written to.</summary>
    public const string ReadingFileName = "last-reading.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() }, WriteIndented = true };

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly string _filePath;

    /// <summary>Initialises a new instance of the <see cref="FileAccountsReadingStore"/> class, resolving the accounts directory from <paramref name="configuration"/> the identical way <see cref="Secrets.WindowsDpapiSecretStore"/>'s own file sibling resolves the secrets directory.</summary>
    public FileAccountsReadingStore(IConfigurationProvider configuration)
        : this(ResolveAccountsDirectory(configuration))
    {
    }

    /// <summary>Initialises a new instance of the <see cref="FileAccountsReadingStore"/> class over an explicit accounts directory.</summary>
    /// <remarks>Internal test seam — lets a test point this store at a temporary directory directly, mirroring every other host-fixture's own explicit-root convention.</remarks>
    internal FileAccountsReadingStore(string accountsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountsDirectory);

        _filePath = Path.Combine(accountsDirectory, ReadingFileName);
    }

    /// <inheritdoc />
    public async Task<AccountsReading?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);

            return JsonSerializer.Deserialize<AccountsReading>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // A corrupt or hand-edited file reads as "nothing saved yet" —
            // the same honest-unavailability discipline as a missing
            // attachment (`AttachmentContentStore`'s own remarks), never a
            // thrown exception this far from the accounting package.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AccountsReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(reading, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static string ResolveAccountsDirectory(IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var root = configuration.TryGetValue(SqlitePersistenceStore.RootPathConfigurationKey, out var configuredRoot) && !string.IsNullOrWhiteSpace(configuredRoot)
            ? configuredRoot
            : SqlitePersistenceStore.DefaultRootPath;

        return Path.Combine(root, AccountsDirectoryName);
    }
}

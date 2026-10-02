using System.Text.Json;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Settings;

/// <summary>
/// Where the last <see cref="XeroSettingsReading"/> is kept between runs
/// (`v0.24.0` X1, D6), so PDFs, Settings and line validation work offline.
/// </summary>
public interface IXeroSettingsCache
{
    /// <summary>
    /// The saved reading, or <see langword="null"/> when there is none — also
    /// when the saved file is corrupt, incomplete, or written by a newer
    /// build (a schema version above <see cref="XeroSettingsReading.CurrentSchemaVersion"/>):
    /// "no reading", never an exception.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<XeroSettingsReading?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the saved reading with <paramref name="reading"/>, atomically (a reader never sees half a file).</summary>
    /// <param name="reading">The reading to keep.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SaveAsync(XeroSettingsReading reading, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IXeroSettingsCache"/> TempestOS uses: one JSON file,
/// <c>&lt;persistence root&gt;/accounts/xero-settings.json</c>, beside the
/// `WP 19.8B` <c>last-reading.json</c> (design §8). Not the persistence
/// database: a reading is a copy of Xero's own data, disposable and
/// re-readable, exactly like <see cref="FileAccountsReadingStore"/>'s.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema-versioned.</b> The file carries
/// <see cref="XeroSettingsReading.SchemaVersion"/>. A file whose version is
/// missing, below 1, or above <see cref="XeroSettingsReading.CurrentSchemaVersion"/>
/// reads as "no reading"; so does one that is not JSON or lacks a field the
/// reading cannot do without (tenant, organisation id and name, the lists).
/// Unknown properties are ignored, so a newer build's additive fields at the
/// same version do not break this one.
/// </para>
/// <para>
/// <b>Overwritten, not merged.</b> A save replaces the file — including a
/// newer build's file this build could not read: the cache is a copy of
/// Xero, so the newer build simply reads Xero again.
/// </para>
/// </remarks>
public sealed class FileXeroSettingsCache : IXeroSettingsCache
{
    /// <summary>The file name under the accounts directory.</summary>
    public const string FileName = "xero-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly string _filePath;

    /// <summary>Initialises a new instance of the <see cref="FileXeroSettingsCache"/> class under the configured persistence root (<see cref="SqlitePersistenceStore.RootPathConfigurationKey"/>, else <see cref="SqlitePersistenceStore.DefaultRootPath"/>) — the same root <see cref="FileAccountsReadingStore"/> resolves.</summary>
    /// <param name="configuration">The host's configuration.</param>
    public FileXeroSettingsCache(IConfigurationProvider configuration)
        : this(ResolveAccountsDirectory(configuration))
    {
    }

    /// <summary>Initialises a new instance of the <see cref="FileXeroSettingsCache"/> class over an explicit accounts directory (test seam).</summary>
    /// <param name="accountsDirectory">The directory the file lives in.</param>
    internal FileXeroSettingsCache(string accountsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountsDirectory);

        _filePath = Path.Combine(accountsDirectory, FileName);
    }

    /// <summary>The full path of the cache file.</summary>
    public string FilePath => _filePath;

    /// <inheritdoc />
    public async Task<XeroSettingsReading?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return null;

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return Parse(json);
    }

    /// <inheritdoc />
    public async Task SaveAsync(XeroSettingsReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var json = JsonSerializer.Serialize(reading, JsonOptions);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporary = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, json, cancellationToken).ConfigureAwait(false);
                File.Move(temporary, _filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Reads <paramref name="json"/> as a reading this build understands, or <see langword="null"/> (see the type's remarks).</summary>
    /// <param name="json">The file's text.</param>
    internal static XeroSettingsReading? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !TryGetVersion(document.RootElement, out var version)
                    || version < 1
                    || version > XeroSettingsReading.CurrentSchemaVersion
                    || !HasRequiredValues(document.RootElement))
                {
                    return null;
                }
            }

            var reading = JsonSerializer.Deserialize<XeroSettingsReading>(json, JsonOptions);
            return reading is not null && IsComplete(reading) ? reading : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryGetVersion(JsonElement root, out int version)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(XeroSettingsReading.SchemaVersion), StringComparison.OrdinalIgnoreCase))
                return property.Value.TryGetInt32(out version);
        }

        version = 0;
        return false;
    }

    /// <summary>
    /// Whether the value-typed fields a consumer relies on are actually in the
    /// file: the deserialiser fills a missing one with its default (a reading
    /// "read at 1 Jan 0001", a demo flag of <see langword="false"/>), which
    /// <see cref="IsComplete"/> cannot tell from a real value.
    /// </summary>
    private static bool HasRequiredValues(JsonElement root)
    {
        if (!TryGetProperty(root, nameof(XeroSettingsReading.ReadAtUtc), out var readAt) || readAt.ValueKind != JsonValueKind.String)
            return false;

        if (TryGetProperty(root, nameof(XeroSettingsReading.Organisation), out var organisation) && organisation.ValueKind == JsonValueKind.Object
            && !(IsBoolean(organisation, nameof(XeroOrganisationProfile.PaysTax)) && IsBoolean(organisation, nameof(XeroOrganisationProfile.IsDemoCompany))))
        {
            return false;
        }

        if (TryGetProperty(root, nameof(XeroSettingsReading.TaxRates), out var rates) && rates.ValueKind == JsonValueKind.Array)
        {
            foreach (var rate in rates.EnumerateArray())
            {
                if (rate.ValueKind == JsonValueKind.Object
                    && !(TryGetProperty(rate, nameof(XeroTaxRate.EffectiveRate), out var effective) && effective.ValueKind == JsonValueKind.Number
                        && IsBoolean(rate, nameof(XeroTaxRate.CanApplyToRevenue))
                        && IsBoolean(rate, nameof(XeroTaxRate.CanApplyToExpenses))))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsBoolean(JsonElement owner, string name) =>
        TryGetProperty(owner, name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool TryGetProperty(JsonElement owner, string name, out JsonElement value)
    {
        foreach (var property in owner.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>Whether every field a consumer relies on being present is present (the deserialiser fills a missing one with <see langword="null"/>).</summary>
    private static bool IsComplete(XeroSettingsReading reading)
    {
        var organisation = reading.Organisation;

        return !string.IsNullOrWhiteSpace(reading.TenantId)
            && reading.ReadAtUtc != default
            && organisation is not null
            && !string.IsNullOrWhiteSpace(organisation.OrganisationId)
            && !string.IsNullOrWhiteSpace(organisation.Name)
            && organisation.BaseCurrency is not null
            && organisation.CountryCode is not null
            && organisation.BankAccounts is not null && organisation.BankAccounts.All(b => b is not null && b.Name is not null)
            && (organisation.Address is null || (organisation.Address.Lines is not null && organisation.Address.AddressType is not null))
            && reading.TaxRates is not null && reading.TaxRates.All(r => r is not null && r.TaxType is not null && r.Name is not null && r.Status is not null)
            && reading.Accounts is not null && reading.Accounts.All(a => a is not null && a.AccountId is not null && a.Name is not null && a.Type is not null && a.Status is not null);
    }

    private static string ResolveAccountsDirectory(IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var root = configuration.TryGetValue(SqlitePersistenceStore.RootPathConfigurationKey, out var configuredRoot) && !string.IsNullOrWhiteSpace(configuredRoot)
            ? configuredRoot
            : SqlitePersistenceStore.DefaultRootPath;

        return Path.Combine(root, FileAccountsReadingStore.AccountsDirectoryName);
    }
}

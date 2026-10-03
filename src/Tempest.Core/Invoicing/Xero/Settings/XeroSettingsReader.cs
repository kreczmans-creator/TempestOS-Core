using System.Globalization;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Secrets;

namespace Tempest.Core.Invoicing.Xero.Settings;

/// <summary>
/// Reads the company's own details, tax rates and chart of accounts from
/// Xero (`v0.24.0` X1, D6; design §8) and keeps the last reading in an
/// <see cref="IXeroSettingsCache"/>, stamped with the time it was read, so
/// PDFs, Settings and line validation work offline from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three calls per refresh</b>: <c>GET Organisation</c>,
/// <c>GET TaxRates</c>, <c>GET Accounts</c>. Accounts are read with
/// <c>If-Modified-Since</c> = the previous reading's time when a reading for
/// the same tenant exists and is younger than
/// <see cref="FullAccountsReadInterval"/>; the changed accounts are merged
/// into the previous ones by <c>AccountID</c>. Otherwise (first reading,
/// another tenant, or a stale one) every account is read.
/// </para>
/// <para>
/// <b>All or nothing.</b> If any call fails, nothing is cached and the
/// outcome says which read failed and why — the previous reading stays
/// usable. A reading is stamped with the time the refresh <i>started</i>,
/// so a change made in Xero while it ran is caught by the next
/// <c>If-Modified-Since</c>. If the cache file cannot be written the reading
/// is still returned and kept in memory for this session.
/// </para>
/// <para>
/// <b>Tenant.</b> The connected tenant is the one <c>OAuthAuthoriser</c>
/// stored at <see cref="TenantIdSecretKey"/> — read locally, so
/// <see cref="ReadCachedAsync"/> needs no network; a cached reading for any
/// other tenant is never answered.
/// </para>
/// <para>
/// <b>Read-only towards Xero.</b> Every call is a <c>GET</c>; the safety
/// handler's write allow-list refuses any write to these resources.
/// Audited as <see cref="AuditAction"/> (never a token).
/// </para>
/// </remarks>
public sealed class XeroSettingsReader : IXeroSettingsReader
{
    /// <summary>The <c>ISecretStore</c> key under which <c>OAuthAuthoriser</c> keeps the connected Xero tenant id (<c>Invoicing:{Provider}:TenantId</c>).</summary>
    public const string TenantIdSecretKey = "Invoicing:Xero:TenantId";

    /// <summary>The audit action recorded for every successful reading (design §6.7).</summary>
    public const string AuditAction = "xero.settings.read";

    /// <summary>How old the previous reading may be for accounts to be read incrementally; older, and every account is read again.</summary>
    public static readonly TimeSpan FullAccountsReadInterval = TimeSpan.FromDays(7);

    private readonly XeroAccountingApi _api;
    private readonly IXeroSettingsCache _cache;
    private readonly ISecretStore _secretStore;
    private readonly IAuditRecorder? _auditRecorder;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _sync = new();
    private XeroSettingsReading? _last;

    /// <summary>Initialises a new instance of the <see cref="XeroSettingsReader"/> class on <paramref name="api"/>'s clock — the one the host composed the Xero client with.</summary>
    /// <param name="api">The typed Xero client.</param>
    /// <param name="cache">Where the last reading is kept.</param>
    /// <param name="secretStore">Where the connected tenant id is kept.</param>
    /// <param name="auditRecorder">Records <see cref="AuditAction"/>.</param>
    public XeroSettingsReader(XeroAccountingApi api, IXeroSettingsCache cache, ISecretStore secretStore, IAuditRecorder auditRecorder)
        : this(api, cache, secretStore, auditRecorder, (api ?? throw new ArgumentNullException(nameof(api))).Time)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="XeroSettingsReader"/> class (test seam: an explicit clock, audit optional).</summary>
    /// <param name="api">The typed Xero client.</param>
    /// <param name="cache">Where the last reading is kept.</param>
    /// <param name="secretStore">Where the connected tenant id is kept.</param>
    /// <param name="auditRecorder">Records <see cref="AuditAction"/>; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock a reading is stamped with.</param>
    internal XeroSettingsReader(XeroAccountingApi api, IXeroSettingsCache cache, ISecretStore secretStore, IAuditRecorder? auditRecorder, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _api = api;
        _cache = cache;
        _secretStore = secretStore;
        _auditRecorder = auditRecorder;
        _time = timeProvider;
    }

    /// <inheritdoc />
    public async Task<XeroSettingsReading?> ReadCachedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await _secretStore.GetAsync(TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;

        var reading = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return reading is not null && SameTenant(reading.TenantId, tenantId) ? reading : null;
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<XeroSettingsReading>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tenantId = await _secretStore.GetAsync(TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tenantId))
                return ConnectorResult<XeroSettingsReading>.Reauthorise("No Xero organisation is connected; re-authorise Xero to select one.");

            var startedAt = _time.GetUtcNow();
            var previous = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (previous is not null && !SameTenant(previous.TenantId, tenantId))
                previous = null;

            var organisation = await _api.GetOrganisationAsync(cancellationToken).ConfigureAwait(false);
            if (organisation.Outcome != ConnectorOutcome.Ok)
                return Failed("organisation", organisation);

            var taxRates = await _api.GetTaxRatesAsync(cancellationToken).ConfigureAwait(false);
            if (taxRates.Outcome != ConnectorOutcome.Ok)
                return Failed("tax rates", taxRates);

            var since = previous is not null && previous.ReadAtUtc <= startedAt && startedAt - previous.ReadAtUtc < FullAccountsReadInterval
                ? previous.ReadAtUtc
                : (DateTimeOffset?)null;

            var accounts = await _api.GetAccountsAsync(since, cancellationToken).ConfigureAwait(false);
            if (accounts.Outcome != ConnectorOutcome.Ok)
                return Failed("chart of accounts", accounts);

            var reading = Build(tenantId, organisation.Value!, taxRates.Value!, accounts.Value!, since is null ? null : previous, startedAt);
            if (reading is null)
                return ConnectorResult<XeroSettingsReading>.Unknown("Xero answered GET Organisation without an organisation id or name; the reading was not kept.");

            await SaveAsync(reading, cancellationToken).ConfigureAwait(false);
            await AuditAsync(reading, since, cancellationToken).ConfigureAwait(false);

            return ConnectorResult<XeroSettingsReading>.Ok(reading);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Builds a reading from Xero's three answers (pure). With
    /// <paramref name="previous"/>, <paramref name="accounts"/> are only the
    /// accounts changed since it and are merged into its accounts by
    /// <c>AccountID</c>; a bank account unchanged since keeps the number the
    /// previous reading held for it.
    /// </summary>
    /// <param name="tenantId">The tenant read.</param>
    /// <param name="organisation">The <c>GET Organisation</c> answer.</param>
    /// <param name="taxRates">The <c>GET TaxRates</c> answer.</param>
    /// <param name="accounts">The <c>GET Accounts</c> answer.</param>
    /// <param name="previous">The reading the accounts are changes to; <see langword="null"/> when they are the full chart.</param>
    /// <param name="readAtUtc">The time the reading is stamped with.</param>
    /// <returns>The reading; <see langword="null"/> when the organisation has no id or name.</returns>
    internal static XeroSettingsReading? Build(
        string tenantId, XeroWireOrganisation organisation, IReadOnlyList<XeroWireTaxRate> taxRates, IReadOnlyList<XeroWireAccount> accounts,
        XeroSettingsReading? previous, DateTimeOffset readAtUtc)
    {
        if (string.IsNullOrWhiteSpace(organisation.OrganisationID) || string.IsNullOrWhiteSpace(organisation.Name))
            return null;

        var freshAccounts = accounts.Where(a => !string.IsNullOrWhiteSpace(a.AccountID)).ToList();
        var mergedAccounts = MergeAccounts(previous?.Accounts, freshAccounts.Select(ToAccount));

        var profile = new XeroOrganisationProfile(
            organisation.OrganisationID,
            organisation.Name,
            Blank(organisation.LegalName),
            Blank(organisation.TaxNumber),
            Blank(organisation.RegistrationNumber),
            ChooseAddress(organisation.Addresses),
            ChoosePhone(organisation.Phones),
            organisation.ExternalLinks?.FirstOrDefault(l => string.Equals(l.LinkType, "Website", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(l.Url))?.Url,
            organisation.BaseCurrency ?? string.Empty,
            organisation.CountryCode ?? string.Empty,
            organisation.PaysTax ?? false,
            organisation.IsDemoCompany ?? false,
            BankAccounts(mergedAccounts, freshAccounts, previous?.Organisation.BankAccounts));

        return new XeroSettingsReading(
            XeroSettingsReading.CurrentSchemaVersion,
            tenantId,
            profile,
            [.. taxRates.Where(r => !string.IsNullOrWhiteSpace(r.TaxType)).Select(ToTaxRate)],
            mergedAccounts,
            readAtUtc);
    }

    private async Task<XeroSettingsReading?> LoadAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_last is not null)
                return _last;
        }

        var stored = await _cache.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null)
            return null;

        lock (_sync)
            return _last ??= stored;
    }

    private async Task SaveAsync(XeroSettingsReading reading, CancellationToken cancellationToken)
    {
        lock (_sync)
            _last = reading;

        try
        {
            await _cache.SaveAsync(reading, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kept in memory for this session; the next refresh writes it again.
        }
    }

    private async Task AuditAsync(XeroSettingsReading reading, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        if (_auditRecorder is null)
            return;

        var detail = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenantId"] = reading.TenantId,
            ["organisation"] = reading.Organisation.Name,
            ["isDemoCompany"] = reading.Organisation.IsDemoCompany ? "true" : "false",
            ["taxRates"] = reading.TaxRates.Count.ToString(CultureInfo.InvariantCulture),
            ["accounts"] = reading.Accounts.Count.ToString(CultureInfo.InvariantCulture),
            ["accountsRead"] = since is { } s ? $"changed since {s.ToString("O", CultureInfo.InvariantCulture)}" : "full",
            ["readAtUtc"] = reading.ReadAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };

        try
        {
            await _auditRecorder.RecordAsync(AuditAction, detail, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // An audit failure never loses a reading Xero already answered.
        }
    }

    private static ConnectorResult<XeroSettingsReading> Failed<T>(string what, XeroApiResult<T> result)
    {
        var reason = $"Reading Xero's {what} failed: {result.Reason ?? "no reason was given"}";

        return result.Outcome switch
        {
            ConnectorOutcome.Rejected => ConnectorResult<XeroSettingsReading>.Rejected(reason),
            ConnectorOutcome.Reauthorise => ConnectorResult<XeroSettingsReading>.Reauthorise(reason),
            ConnectorOutcome.Unavailable => ConnectorResult<XeroSettingsReading>.Unavailable(reason),
            _ => ConnectorResult<XeroSettingsReading>.Unknown(reason),
        };
    }

    private static bool SameTenant(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static XeroTaxRate ToTaxRate(XeroWireTaxRate rate) => new(
        rate.TaxType!,
        Blank(rate.Name) ?? rate.TaxType!,
        rate.EffectiveRate ?? 0m,
        rate.Status ?? string.Empty,
        rate.CanApplyToRevenue ?? false,
        rate.CanApplyToExpenses ?? false);

    private static XeroAccount ToAccount(XeroWireAccount account) => new(
        account.AccountID!,
        Blank(account.Code),
        Blank(account.Name) ?? Blank(account.Code) ?? account.AccountID!,
        account.Type ?? string.Empty,
        Blank(account.Class),
        account.Status ?? string.Empty,
        Blank(account.TaxType));

    private static List<XeroAccount> MergeAccounts(IReadOnlyList<XeroAccount>? previous, IEnumerable<XeroAccount> fresh)
    {
        var freshAccounts = fresh.ToList();

        // An earlier account whose code a freshly read one now carries is stale: Xero gave the code to
        // another account (the earlier one deleted or renumbered), and If-Modified-Since never returns it.
        var freshIds = new HashSet<string>(freshAccounts.Select(a => a.AccountId), StringComparer.OrdinalIgnoreCase);
        var freshCodes = new HashSet<string>(freshAccounts.Where(a => a.Code is not null).Select(a => a.Code!), StringComparer.OrdinalIgnoreCase);
        var merged = new List<XeroAccount>((previous ?? []).Where(a => freshIds.Contains(a.AccountId) || a.Code is null || !freshCodes.Contains(a.Code)));
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < merged.Count; i++)
            index[merged[i].AccountId] = i;

        foreach (var account in freshAccounts)
        {
            if (index.TryGetValue(account.AccountId, out var at))
            {
                merged[at] = account;
            }
            else
            {
                index[account.AccountId] = merged.Count;
                merged.Add(account);
            }
        }

        return merged;
    }

    private static List<XeroBankAccount> BankAccounts(
        IReadOnlyList<XeroAccount> merged, IReadOnlyList<XeroWireAccount> fresh, IReadOnlyList<XeroBankAccount>? previous)
    {
        var freshById = fresh.ToDictionary(a => a.AccountID!, StringComparer.OrdinalIgnoreCase);
        var result = new List<XeroBankAccount>();

        foreach (var account in merged.Where(a => IsWord(a.Type, "BANK") && IsWord(a.Status, "ACTIVE")))
        {
            if (freshById.TryGetValue(account.AccountId, out var wire))
                result.Add(new XeroBankAccount(account.Name, Blank(wire.BankAccountNumber), Blank(wire.CurrencyCode), Blank(wire.BankAccountType)));
            else
                result.Add(previous?.FirstOrDefault(b => string.Equals(b.Name, account.Name, StringComparison.Ordinal)) ?? new XeroBankAccount(account.Name, null, null));
        }

        return result;
    }

    private static XeroAddress? ChooseAddress(IReadOnlyList<XeroWireAddress>? addresses)
    {
        var usable = (addresses ?? []).Where(a => a is not null && !string.IsNullOrWhiteSpace(a.AddressType)).ToList();
        var chosen = usable.FirstOrDefault(a => IsWord(a.AddressType, "STREET") && HasContent(a))
            ?? usable.FirstOrDefault(a => IsWord(a.AddressType, "POBOX") && HasContent(a))
            ?? usable.FirstOrDefault(HasContent);

        if (chosen is null)
            return null;

        string?[] lines = [chosen.AddressLine1, chosen.AddressLine2, chosen.AddressLine3, chosen.AddressLine4];
        return new XeroAddress(
            chosen.AddressType!.ToUpperInvariant(),
            [.. lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!.Trim())],
            Blank(chosen.City),
            Blank(chosen.Region),
            Blank(chosen.PostalCode),
            Blank(chosen.Country));
    }

    private static bool HasContent(XeroWireAddress address) =>
        new[] { address.AddressLine1, address.AddressLine2, address.AddressLine3, address.AddressLine4, address.City, address.PostalCode }
            .Any(v => !string.IsNullOrWhiteSpace(v));

    private static string? ChoosePhone(IReadOnlyList<XeroWirePhone>? phones)
    {
        string[] preference = ["DEFAULT", "OFFICE", "DDI", "MOBILE"];
        var usable = (phones ?? []).Where(p => p is not null && !string.IsNullOrWhiteSpace(p.PhoneNumber)).ToList();

        var chosen = preference
            .Select(type => usable.FirstOrDefault(p => IsWord(p.PhoneType, type)))
            .FirstOrDefault(p => p is not null);

        return chosen is null
            ? null
            : string.Join(' ', new[] { chosen.PhoneAreaCode, chosen.PhoneNumber }.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
    }

    private static bool IsWord(string? value, string word) => string.Equals(value, word, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The company's own details as a TempestOS PDF and the Settings page show
/// them (`v0.24.0` X1, design §8 "PDF identity"), taken from a
/// <see cref="XeroSettingsReading"/>. Without a reading the caller uses its
/// own Settings → Organisation values instead (offline first run).
/// </summary>
/// <param name="Name">The name printed: Xero's <c>LegalName</c>, else its <c>Name</c>.</param>
/// <param name="RegistrationNumber">The company number; <see langword="null"/> when Xero holds none.</param>
/// <param name="VatNumber">The VAT number (Xero <c>TaxNumber</c>); <see langword="null"/> when Xero holds none.</param>
/// <param name="AddressLines">The address, one printable line each (street lines, town, region and postcode, country); empty when none.</param>
/// <param name="Phone">The phone number; <see langword="null"/> when none.</param>
/// <param name="Website">The website; <see langword="null"/> when none.</param>
/// <param name="BankAccounts">The active bank accounts a customer can pay into, for payment details (<see cref="XeroBankAccount.IsPayableTo"/>: never a credit card or PayPal account).</param>
/// <param name="ReadAtUtc">When Xero was read.</param>
public sealed record XeroCompanyDetails(
    string Name,
    string? RegistrationNumber,
    string? VatNumber,
    IReadOnlyList<string> AddressLines,
    string? Phone,
    string? Website,
    IReadOnlyList<XeroBankAccount> BankAccounts,
    DateTimeOffset ReadAtUtc)
{
    /// <summary>The company details in <paramref name="reading"/>.</summary>
    /// <param name="reading">A reading.</param>
    public static XeroCompanyDetails From(XeroSettingsReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var organisation = reading.Organisation;
        var lines = new List<string>();
        if (organisation.Address is { } address)
        {
            lines.AddRange(address.Lines);
            if (!string.IsNullOrWhiteSpace(address.City))
                lines.Add(address.City);
            var regionAndPostcode = string.Join(' ', new[] { address.Region, address.PostalCode }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (regionAndPostcode.Length > 0)
                lines.Add(regionAndPostcode);
            if (!string.IsNullOrWhiteSpace(address.Country))
                lines.Add(address.Country);
        }

        return new XeroCompanyDetails(
            string.IsNullOrWhiteSpace(organisation.LegalName) ? organisation.Name : organisation.LegalName,
            organisation.RegistrationNumber,
            organisation.TaxNumber,
            lines,
            organisation.Phone,
            organisation.Website,
            [.. organisation.BankAccounts.Where(b => b.IsPayableTo)],
            reading.ReadAtUtc);
    }

    /// <summary>The note Settings shows beside the details: <c>"from Xero, read at 2 Oct 2026 10:00"</c>, in <paramref name="zone"/>.</summary>
    /// <param name="zone">The time zone to show the time in; <see langword="null"/> for the machine's own.</param>
    public string SourceNote(TimeZoneInfo? zone = null)
    {
        var local = TimeZoneInfo.ConvertTime(ReadAtUtc, zone ?? TimeZoneInfo.Local);
        return $"from Xero, read at {local.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture)}";
    }
}

/// <summary>Reading Xero's settings at the cadence design §8 sets: daily, and before the first write of a session.</summary>
public static class XeroSettingsReaderExtensions
{
    /// <summary>How old a reading may be before <see cref="ReadFreshAsync"/> reads Xero again by default (design §8: daily).</summary>
    public static readonly TimeSpan DailyRefreshInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// The cached reading when it is younger than <paramref name="maxAge"/>;
    /// otherwise a fresh one from Xero — and when Xero cannot be read
    /// (offline, re-authorisation needed), the last reading after all, so
    /// work carries on from it. <see langword="null"/> only when there has
    /// never been a reading for the connected tenant and Xero cannot be read.
    /// </summary>
    /// <param name="reader">The settings reader.</param>
    /// <param name="timeProvider">The clock the reading's age is measured on.</param>
    /// <param name="maxAge">The oldest acceptable reading; <see langword="null"/> for <see cref="DailyRefreshInterval"/>. <see cref="TimeSpan.Zero"/> always tries Xero first (the first write of a session).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<XeroSettingsReading?> ReadFreshAsync(
        this IXeroSettingsReader reader, TimeProvider timeProvider, TimeSpan? maxAge = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var cached = await reader.ReadCachedAsync(cancellationToken).ConfigureAwait(false);
        var limit = maxAge ?? DailyRefreshInterval;
        if (cached is not null && limit > TimeSpan.Zero && timeProvider.GetUtcNow() - cached.ReadAtUtc < limit)
            return cached;

        var refreshed = await reader.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return refreshed.Outcome == ConnectorOutcome.Ok && refreshed.Value is { } fresh ? fresh : cached;
    }
}

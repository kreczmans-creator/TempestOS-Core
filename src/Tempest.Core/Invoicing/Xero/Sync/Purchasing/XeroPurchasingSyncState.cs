using System.Globalization;
using System.Text.Json;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Persistence;
using Tempest.Core.Secrets;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>How the purchase-order and expense-bill planners decide which records are synced automatically.</summary>
public sealed record XeroPurchasingPlannerOptions
{
    /// <summary>
    /// Purchase orders issued on or after this moment's day (UTC), and
    /// expenses recorded at or after it, are synced automatically; older
    /// ones — made before `v0.24.0`'s Xero sync existed, and already in the
    /// accounts by hand — only after the Product Owner's explicit
    /// <em>Send to Xero</em> (the same rule Q8 sets for quotes and
    /// invoices), so the first start-up scan never floods Xero with history.
    /// <see langword="null"/> (the default) keeps the moment <b>per Xero
    /// organisation</b> (F1, M1; <see cref="XeroPurchasingSyncState.AutomaticFromAsync(string?, CancellationToken)"/>):
    /// for the first organisation, the moment a planner first ran in this
    /// workspace; for every later one, the moment TempestOS first saw it
    /// connected — each recorded once in
    /// <see cref="XeroPurchasingSyncState.StateCollection"/> and never moved.
    /// A configured moment applies to every organisation.
    /// </summary>
    public DateTimeOffset? AutomaticFromUtc { get; init; }
}

/// <summary>What a <em>Send to Xero</em> on one purchase order or expense did.</summary>
/// <param name="Queued">Whether the record is now synced; <see langword="false"/> with <paramref name="Reason"/> otherwise.</param>
/// <param name="Entries">The outbox entries queued or found already queued, in order.</param>
/// <param name="Reason">Why nothing was queued; <see langword="null"/> when <paramref name="Queued"/>.</param>
public sealed record XeroPurchasingSendRequest(bool Queued, IReadOnlyList<XeroOutboxEntry> Entries, string? Reason = null);

/// <summary>
/// The purchasing planners' own small, durable state (`v0.24.0` X5) in
/// <see cref="IPersistenceStore"/>: when automatic sync began, and each
/// record's explicit <em>Send to Xero</em> — both per Xero organisation
/// since F1 (M1), so records issued while the Demo Company was connected are
/// not sent to the live organisation unless the Product Owner asks. Also the
/// tenant-id read every planner needs. Never a network call.
/// </summary>
public sealed class XeroPurchasingSyncState
{
    /// <summary>The <see cref="IPersistenceStore"/> collection.</summary>
    public const string StateCollection = "Xero.PurchasingSync";

    /// <summary>The key of the moment automatic sync began in this workspace (round-trip ISO-8601): the first organisation's start (<see cref="AutomaticFromKeyFor"/>), also recorded while no organisation is connected.</summary>
    public const string AutomaticFromKey = "automatic-from";

    /// <summary>`v0.24.0` F1 (M1): the key naming the organisation (tenant id) that took over <see cref="AutomaticFromKey"/> and the opt-ins recorded under <see cref="OptInKey(XeroDocumentRef)"/> — the first organisation connected since F1.</summary>
    public const string AutomaticFromOwnerKey = "automatic-from-owner";

    /// <summary>The audit action for a purchase order or expense the Product Owner sent to Xero on demand.</summary>
    public const string AuditSendToXero = "xero.purchasing.send-to-xero";

    private readonly IPersistenceStore _store;
    private readonly ISecretStore _secretStore;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;
    private readonly XeroPurchasingPlannerOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initialises a new instance of the <see cref="XeroPurchasingSyncState"/> class.</summary>
    /// <param name="store">Where the state is kept.</param>
    /// <param name="secretStore">Where the connected Xero organisation's tenant id is kept (read only).</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    /// <param name="options">The automatic-sync choice; <see langword="null"/> for the default.</param>
    public XeroPurchasingSyncState(
        IPersistenceStore store, ISecretStore secretStore, IAuditRecorder? audit = null, TimeProvider? timeProvider = null,
        XeroPurchasingPlannerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(secretStore);

        _store = store;
        _secretStore = secretStore;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
        _options = options ?? new XeroPurchasingPlannerOptions();
    }

    /// <summary>The key of <paramref name="document"/>'s <em>Send to Xero</em> opt-in recorded before F1 or with no organisation connected; it counts only for the organisation named by <see cref="AutomaticFromOwnerKey"/>.</summary>
    /// <param name="document">The record.</param>
    public static string OptInKey(XeroDocumentRef document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return $"send-to-xero/{document.Kind}/{document.TempestKey}";
    }

    /// <summary>`v0.24.0` F1 (M1): the key of <paramref name="document"/>'s <em>Send to Xero</em> opt-in for the organisation <paramref name="tenantId"/>.</summary>
    /// <param name="document">The record.</param>
    /// <param name="tenantId">The organisation.</param>
    public static string OptInKey(XeroDocumentRef document, string tenantId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return $"send-to-xero/{tenantId}/{document.Kind}/{document.TempestKey}";
    }

    /// <summary>`v0.24.0` F1 (M1): the key of the moment automatic sync began for the organisation <paramref name="tenantId"/>.</summary>
    /// <param name="tenantId">The organisation.</param>
    public static string AutomaticFromKeyFor(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return $"{AutomaticFromKey}/{tenantId}";
    }

    /// <summary>The connected Xero organisation's tenant id, from the secret store; <see langword="null"/> when none is connected.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<string?> ReadTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await _secretStore.GetAsync(XeroContactLinker.TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }

    /// <summary>When automatic sync began for the organisation connected now (<see cref="AutomaticFromAsync(string?, CancellationToken)"/>).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<DateTimeOffset> AutomaticFromAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AutomaticFromUtc is { } configured)
            return configured;

        return await AutomaticFromAsync(await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// `v0.24.0` F1 (M1): when automatic sync began for the organisation
    /// <paramref name="tenantId"/> — <see cref="XeroPurchasingPlannerOptions.AutomaticFromUtc"/>
    /// when configured; else the moment recorded for it; else, recorded now
    /// and never moved: for the first organisation connected since F1, the
    /// workspace's own start (<see cref="AutomaticFromKey"/>, written before
    /// F1 or with no organisation connected, or now when there is none); for
    /// any later organisation, now — the moment TempestOS first sees it
    /// connected. With no organisation connected (<see langword="null"/>), the
    /// workspace's own start (written once, then read back).
    /// </summary>
    /// <param name="tenantId">The organisation; <see langword="null"/> when none is connected.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<DateTimeOffset> AutomaticFromAsync(string? tenantId, CancellationToken cancellationToken = default)
    {
        if (_options.AutomaticFromUtc is { } configured)
            return configured;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            var workspaceStart = await ReadTimeAsync(AutomaticFromKey, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                if (workspaceStart is { } recorded)
                    return recorded;

                await WriteTimeAsync(AutomaticFromKey, now, cancellationToken).ConfigureAwait(false);
                return now;
            }

            if (await ReadTimeAsync(AutomaticFromKeyFor(tenantId), cancellationToken).ConfigureAwait(false) is { } own)
                return own;

            // The owner is written before the organisation's own moment, so a
            // crash between the two still finds this organisation the owner.
            var owner = await _store.ReadAsync(StateCollection, AutomaticFromOwnerKey, cancellationToken).ConfigureAwait(false);
            DateTimeOffset start;
            if (owner is null || string.Equals(owner, tenantId, StringComparison.Ordinal))
            {
                start = workspaceStart ?? now;
                if (workspaceStart is null)
                    await WriteTimeAsync(AutomaticFromKey, start, cancellationToken).ConfigureAwait(false);
                if (owner is null)
                    await _store.WriteAsync(StateCollection, AutomaticFromOwnerKey, tenantId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                start = now;
            }

            await WriteTimeAsync(AutomaticFromKeyFor(tenantId), start, cancellationToken).ConfigureAwait(false);
            return start;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Whether <paramref name="document"/> was explicitly sent to Xero for the organisation connected now: its own opt-in, or one recorded before F1 or with no organisation connected when this organisation is the first since F1 (<see cref="AutomaticFromOwnerKey"/>).</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> IsOptedInAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is not null)
        {
            if (await _store.ReadAsync(StateCollection, OptInKey(document, tenantId), cancellationToken).ConfigureAwait(false) is not null)
                return true;

            var owner = await _store.ReadAsync(StateCollection, AutomaticFromOwnerKey, cancellationToken).ConfigureAwait(false);
            if (owner is not null && !string.Equals(owner, tenantId, StringComparison.Ordinal))
                return false;
        }

        return await _store.ReadAsync(StateCollection, OptInKey(document), cancellationToken).ConfigureAwait(false) is not null;
    }

    private async Task<DateTimeOffset?> ReadTimeAsync(string key, CancellationToken cancellationToken)
    {
        var stored = await _store.ReadAsync(StateCollection, key, cancellationToken).ConfigureAwait(false);
        return stored is not null && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var recorded)
            ? recorded
            : null;
    }

    private Task WriteTimeAsync(string key, DateTimeOffset at, CancellationToken cancellationToken) =>
        _store.WriteAsync(StateCollection, key, at.ToString("O", CultureInfo.InvariantCulture), cancellationToken);


    /// <summary>
    /// Whether the person unlinked <paramref name="document"/> from Xero in the
    /// connected organisation (<see cref="XeroDocumentLinkActions.Collection"/>)
    /// and has not chosen Send again: nothing is planned for it until they do
    /// (design §6.7). <see langword="false"/> while no organisation is connected.
    /// </summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> IsUnlinkedByPersonAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is { } tenantId
        && await _store.ReadAsync(XeroDocumentLinkActions.Collection, PersistenceXeroLinkStore.KeyFor(tenantId, document), cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>Records the Product Owner's <em>Send to Xero</em> on <paramref name="document"/> (audited), for the organisation connected now (F1, M1).</summary>
    /// <param name="document">The record.</param>
    /// <param name="number">The record's own number, for the audit row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task OptInAsync(XeroDocumentRef document, string number, CancellationToken cancellationToken = default)
    {
        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        await _store.WriteAsync(
            StateCollection, tenantId is null ? OptInKey(document) : OptInKey(document, tenantId),
            JsonSerializer.Serialize(new { requestedAtUtc = _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture) }),
            cancellationToken).ConfigureAwait(false);

        if (_audit is not null)
        {
            await _audit.RecordAsync(AuditSendToXero, new Dictionary<string, string>
            {
                ["document"] = document.TempestKey,
                ["kind"] = document.Kind.ToString(),
                ["number"] = number,
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}

using System.Text.Json;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// The durable record of every purchasing create that may have reached Xero
/// (`v0.24.0` X5): written immediately before
/// <c>CreatePurchaseOrderAsync</c> or <c>CreateBillAsync</c> goes, so the
/// handlers can recover the record a create made whose answer was lost — by
/// its id, recorded here as soon as any answer reveals it, or, while none is
/// known and Xero still holds the key, by re-sending that create's body under
/// its own <c>Idempotency-Key</c> (Xero replays its first answer, the
/// record's id) — and read that id back (<see cref="XeroPurchasingOwnership"/>),
/// never by matching a number. A record found gone is kept as a tombstone.
/// </summary>
/// <remarks>
/// <para>
/// Outbox attempts and states are not that proof: a push Rejected or Blocked
/// before any create was sent is still counted as an attempt, and Retry or a
/// later amend makes a new attempt or entry over it. Only this log says a
/// create was sent.
/// </para>
/// <para>
/// One record per document in <see cref="Collection"/>, keyed
/// <c>{tenant}/{kind}/{TempestOS key}</c>, listing each create sent: the
/// number, contact and body it carried, its <c>Idempotency-Key</c>, when it
/// was first sent, and — once known — the record's id and whether it is gone
/// from Xero (<see cref="XeroPurchasingSentCreate"/>). A create
/// Xero refused outright (<see cref="ConnectorOutcome.Rejected"/> or
/// <see cref="ConnectorOutcome.Reauthorise"/>) made nothing and is struck
/// off again; one whose answer was lost, or that succeeded, stays.
/// </para>
/// </remarks>
public sealed class XeroPurchasingCreateLog
{
    /// <summary>The <see cref="IPersistenceStore"/> collection.</summary>
    public const string Collection = "Xero.PurchasingCreates";

    private readonly IPersistenceStore _store;

    /// <summary>Initialises a new instance of the <see cref="XeroPurchasingCreateLog"/> class.</summary>
    /// <param name="store">Where the log is kept.</param>
    public XeroPurchasingCreateLog(IPersistenceStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Records, before it is sent, a create for <paramref name="document"/> under <paramref name="number"/> to <paramref name="contactId"/> with <paramref name="idempotencyKey"/>.</summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="number">The Xero number the create carries.</param>
    /// <param name="contactId">The Xero <c>ContactID</c> the create carries.</param>
    /// <param name="idempotencyKey">The create's <c>Idempotency-Key</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <param name="reference">The <c>Reference</c> the create carries (a purchase order's project code), if any. Kept for the record only: a bookkeeper may edit it, so the ownership rule never reads it.</param>
    /// <param name="value">The value-bearing content the create carries (<see cref="XeroPurchasingOwnership.ValueOf(XeroWirePurchaseOrderWrite)"/> or <see cref="XeroPurchasingOwnership.ValueOf(XeroWireBillWrite)"/>). Only ever used to word a refusal for another document (<see cref="ListSentForOthersAsync"/>), never to call a record ours.</param>
    /// <param name="body">The create's body exactly as sent (<see cref="XeroPurchasingSentCreate.Body"/>): what recovery re-sends under the same <c>Idempotency-Key</c> so Xero replays its first answer, the record's id.</param>
    /// <param name="sentAtUtc">When the create is sent (<see cref="XeroPurchasingSentCreate.SentAtUtc"/>): how recovery tells whether Xero still holds its key. A create already logged with the same key and body keeps its first time (Xero's key lifetime runs from the first call) unless <paramref name="restampSameSend"/>. <see langword="null"/> when unknown: its key is then never relied on.</param>
    /// <param name="restampSameSend">
    /// `v0.24.0` F2 follow-up (invoices only): a create already logged with the
    /// same key and body whose record's id is not yet known takes
    /// <paramref name="sentAtUtc"/> as its send time. Sound only for a caller
    /// that re-sends a create solely after finding nothing under its number:
    /// then no earlier send of that key made anything Xero still shows, so the
    /// key's lifetime that matters runs from this send — a later recovery
    /// replays it while Xero still holds it, never by the first, older send.
    /// </param>
    public async Task RecordSendingAsync(
        string tenantId, XeroDocumentRef document, string number, string contactId, string idempotencyKey, CancellationToken cancellationToken = default,
        string? reference = null, string? value = null, string? body = null, DateTimeOffset? sentAtUtc = null, bool restampSameSend = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var sent = new XeroPurchasingSentCreate(
            number.Trim(), contactId.Trim(), idempotencyKey, string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(), string.IsNullOrWhiteSpace(value) ? null : value,
            string.IsNullOrWhiteSpace(body) ? null : body, SentAtUtc: sentAtUtc);
        await UpdateAsync(
            tenantId, document,
            list =>
            {
                if (!list.Any(s => s.SameSend(sent)))
                    return [.. list, sent];

                return restampSameSend && sentAtUtc is not null
                    ? [.. list.Select(s => s.SameSend(sent) && s.XeroId is null && s.GoneStatus is null ? s with { SentAtUtc = sentAtUtc } : s)]
                    : list;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records Xero's answer to the create sent with <paramref name="idempotencyKey"/>:
    /// a definite refusal (nothing was made) strikes it off; an
    /// <see cref="ConnectorOutcome.Ok"/> answer keeps it with the record's id
    /// (<paramref name="xeroId"/>), so it is never re-sent again, only read
    /// back by that id; any other outcome keeps it as it is.
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="idempotencyKey">The create's <c>Idempotency-Key</c>.</param>
    /// <param name="outcome">What Xero answered.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <param name="xeroId">The record's id when Xero answered <see cref="ConnectorOutcome.Ok"/>.</param>
    public async Task RecordAnswerAsync(
        string tenantId, XeroDocumentRef document, string idempotencyKey, ConnectorOutcome outcome, CancellationToken cancellationToken = default,
        string? xeroId = null)
    {
        if (outcome == ConnectorOutcome.Ok)
        {
            if (!string.IsNullOrWhiteSpace(xeroId))
                await RecordXeroIdAsync(tenantId, document, idempotencyKey, xeroId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (outcome is not (ConnectorOutcome.Rejected or ConnectorOutcome.Reauthorise))
            return;

        await UpdateAsync(tenantId, document, list => [.. list.Where(s => !string.Equals(s.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Records the id of the record the create sent with <paramref name="idempotencyKey"/>
    /// made, as soon as any answer (the create's own, or a replay of it)
    /// reveals it. From then on recovery only reads that id back; the body is
    /// never re-sent. An id already recorded is kept.
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="idempotencyKey">The create's <c>Idempotency-Key</c>.</param>
    /// <param name="xeroId">The record's Xero id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public Task RecordXeroIdAsync(string tenantId, XeroDocumentRef document, string idempotencyKey, string xeroId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xeroId);
        return UpdateAsync(
            tenantId, document,
            list => list.Any(s => IsKey(s, idempotencyKey) && s.XeroId is null)
                ? [.. list.Select(s => IsKey(s, idempotencyKey) && s.XeroId is null ? s with { XeroId = xeroId.Trim() } : s)]
                : list,
            cancellationToken);
    }

    /// <summary>
    /// Records that the record the create sent with <paramref name="idempotencyKey"/>
    /// made, read back by its id, is no longer live in Xero (deleted, or a
    /// bill voided): a tombstone. Recovery then reports it gone without asking
    /// Xero again, and nothing — Retry, a cancel or a delete — ever re-sends
    /// that create. Only <see cref="XeroPurchasingSendAgain"/>, the person's
    /// deliberate <em>Send again</em>, releases it.
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="idempotencyKey">The create's <c>Idempotency-Key</c>.</param>
    /// <param name="xeroId">The record's Xero id.</param>
    /// <param name="status">Its status in Xero (<c>DELETED</c> or <c>VOIDED</c>).</param>
    /// <param name="number">Its number in Xero, if known.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public Task RecordGoneAsync(
        string tenantId, XeroDocumentRef document, string idempotencyKey, string xeroId, string status, string? number, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xeroId);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        return UpdateAsync(
            tenantId, document,
            list => list.Any(s => IsKey(s, idempotencyKey) && s.GoneStatus is null)
                ? [.. list.Select(s => IsKey(s, idempotencyKey) && s.GoneStatus is null
                    ? s with { XeroId = s.XeroId ?? xeroId.Trim(), GoneStatus = status.Trim().ToUpperInvariant(), XeroNumber = string.IsNullOrWhiteSpace(number) ? s.XeroNumber : number.Trim() }
                    : s)]
                : list,
            cancellationToken);
    }

    /// <summary>
    /// The person's deliberate <em>Send again</em> (<see cref="XeroPurchasingSendAgain"/>):
    /// releases every tombstone (<see cref="RecordGoneAsync"/>) not yet
    /// released for <paramref name="document"/>. A released create is no longer
    /// recovered, and the push that follows sends the document as a new
    /// record under a new key (<see cref="XeroPurchasingOwnership.CreateKey"/>).
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="releasedAtUtc">When the person asked.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many tombstones were released.</returns>
    public async Task<int> ReleaseGoneAsync(string tenantId, XeroDocumentRef document, DateTimeOffset releasedAtUtc, CancellationToken cancellationToken = default)
    {
        var released = 0;
        await UpdateAsync(
            tenantId, document,
            list =>
            {
                released = list.Count(s => s.IsTombstone);
                return released == 0 ? list : [.. list.Select(s => s.IsTombstone ? s with { ReleasedAtUtc = releasedAtUtc } : s)];
            },
            cancellationToken).ConfigureAwait(false);
        return released;
    }

    /// <summary>Whether a create for <paramref name="document"/> under <paramref name="number"/> to <paramref name="contactId"/> may have reached Xero.</summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="number">The Xero number.</param>
    /// <param name="contactId">The Xero <c>ContactID</c>.</param>
    /// <param name="idempotencyKey">When given, only the create sent with this key counts.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> WasSentAsync(
        string tenantId, XeroDocumentRef document, string number, string contactId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var list = await ReadAsync(Key(tenantId, document), cancellationToken).ConfigureAwait(false);
        return list.Any(s => string.Equals(s.Number, number.Trim(), StringComparison.OrdinalIgnoreCase)
                             && string.Equals(s.ContactId, contactId.Trim(), StringComparison.OrdinalIgnoreCase)
                             && (idempotencyKey is null || string.Equals(s.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Whether the person unlinked <paramref name="document"/> from Xero
    /// (<see cref="XeroDocumentLinkActions.Collection"/>) and has not chosen
    /// Send again: no create goes for it until they do (design §6.7).
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> IsUnlinkedByPersonAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        await _store.ReadAsync(XeroDocumentLinkActions.Collection, PersistenceXeroLinkStore.KeyFor(tenantId, document), cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// Every create for <paramref name="document"/> that may have reached Xero,
    /// in the order sent: the numbers and contacts to look Xero up by when the
    /// document's own number or contact has changed since (a bill found under
    /// one of them is the document's own).
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Task<IReadOnlyList<XeroPurchasingSentCreate>> ListSentAsync(
        string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        ReadAsync(Key(tenantId, document), cancellationToken);

    /// <summary>
    /// Every create logged for a document of <paramref name="document"/>'s kind
    /// other than <paramref name="document"/> in the tenant: what tells a record
    /// another TempestOS document's create made (its answer lost, not yet
    /// linked) from one keyed by hand. Only ever used to word a refusal — never
    /// to link, change or delete anything — so a record that cannot be read is
    /// passed over.
    /// </summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document being pushed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<IReadOnlyList<XeroPurchasingSentCreate>> ListSentForOthersAsync(
        string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        var own = Key(tenantId, document);
        var prefix = $"{tenantId}/{document.Kind}/";
        var keys = await _store.ListKeysAsync(Collection, cancellationToken).ConfigureAwait(false);
        var sent = new List<XeroPurchasingSentCreate>();
        foreach (var key in keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(k, own, StringComparison.Ordinal)))
        {
            try
            {
                sent.AddRange(await ReadAsync(key, cancellationToken).ConfigureAwait(false));
            }
            catch (PersistenceException)
            {
                // Unreadable: passed over (this list only words a refusal).
            }
        }

        return sent;
    }

    private async Task UpdateAsync(string tenantId, XeroDocumentRef document, Func<IReadOnlyList<XeroPurchasingSentCreate>, IReadOnlyList<XeroPurchasingSentCreate>> change, CancellationToken cancellationToken)
    {
        var key = Key(tenantId, document);
        var gate = XeroStoreSupport.GateFor(_store, Collection);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var before = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
            var after = change(before);
            if (ReferenceEquals(after, before))
                return;

            if (after.Count == 0)
                await _store.DeleteAsync(Collection, key, cancellationToken).ConfigureAwait(false);
            else
                await _store.WriteAsync(Collection, key, JsonSerializer.Serialize(after, XeroStoreSupport.JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<XeroPurchasingSentCreate>> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var json = await _store.ReadAsync(Collection, key, cancellationToken).ConfigureAwait(false);
        if (json is null)
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<XeroPurchasingSentCreate>>(json, XeroStoreSupport.JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new PersistenceException($"The Xero create log '{key}' in '{Collection}' cannot be read.", ex);
        }
    }

    private static bool IsKey(XeroPurchasingSentCreate sent, string idempotencyKey) => string.Equals(sent.IdempotencyKey, idempotencyKey, StringComparison.Ordinal);

    private static string Key(string tenantId, XeroDocumentRef document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(document);
        return $"{tenantId}/{document.Kind}/{document.TempestKey}";
    }

}

/// <summary>One purchasing create sent to Xero (<see cref="XeroPurchasingCreateLog"/>): the number, contact, value-bearing content and body it carried, its <c>Idempotency-Key</c>, when it was sent, and what is known of the record it made.</summary>
/// <param name="Number">The Xero number the create carried.</param>
/// <param name="ContactId">The Xero <c>ContactID</c> the create carried.</param>
/// <param name="IdempotencyKey">The create's <c>Idempotency-Key</c>.</param>
/// <param name="Reference">The <c>Reference</c> the create carried; <see langword="null"/> when none (a bill carries none).</param>
/// <param name="Value">The value-bearing content the create carried (<see cref="XeroPurchasingOwnership"/>); only ever words a refusal, never proves a record ours.</param>
/// <param name="Body">The create's body exactly as sent, serialised with <see cref="XeroWire.JsonOptions"/> (a <see cref="XeroWirePurchaseOrderWrite"/> or <see cref="XeroWireBillWrite"/>); re-sent under <see cref="IdempotencyKey"/> to recover the record's id while no id is known and Xero still holds the key. <see langword="null"/> when unknown: that create cannot be recovered, so TempestOS cannot tell.</param>
/// <param name="SentAtUtc">When the create was first sent; <see langword="null"/> when unknown (its key is then never relied on: <see cref="XeroPurchasingOwnership.IdempotencyKeyLifetime"/>).</param>
/// <param name="XeroId">The id of the record it made, once any answer (its own, or a replay) revealed it; from then on it is only ever read back by this id, never re-sent.</param>
/// <param name="GoneStatus">A tombstone: the record, read back by <see cref="XeroId"/>, is no longer live in Xero (<c>DELETED</c>, or a bill <c>VOIDED</c>); <see langword="null"/> while not known to be gone.</param>
/// <param name="XeroNumber">The record's number in Xero when last read back (a bookkeeper may renumber it); <see langword="null"/> when not read.</param>
/// <param name="ReleasedAtUtc">When the person chose <em>Send again</em> for this tombstone (<see cref="XeroPurchasingSendAgain"/>); a released create is no longer recovered.</param>
public sealed record XeroPurchasingSentCreate(
    string Number, string ContactId, string IdempotencyKey, string? Reference = null, string? Value = null, string? Body = null,
    DateTimeOffset? SentAtUtc = null, string? XeroId = null, string? GoneStatus = null, string? XeroNumber = null, DateTimeOffset? ReleasedAtUtc = null)
{
    /// <summary>A tombstone not yet released: the record is gone from Xero, and the person has not chosen <em>Send again</em>.</summary>
    public bool IsTombstone => GoneStatus is not null && ReleasedAtUtc is null;

    /// <summary>Whether this is the same send as <paramref name="other"/> (key, number, contact and content), whatever is known since of the record it made.</summary>
    /// <param name="other">Another logged create.</param>
    public bool SameSend(XeroPurchasingSentCreate other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Strip(this) == Strip(other);

        static XeroPurchasingSentCreate Strip(XeroPurchasingSentCreate s) =>
            s with { SentAtUtc = null, XeroId = null, GoneStatus = null, XeroNumber = null, ReleasedAtUtc = null };
    }

    /// <summary>A create's body as <see cref="Body"/> holds it: serialised with <see cref="XeroWire.JsonOptions"/>, as the typed client sends it.</summary>
    /// <param name="body">The create's body (a <see cref="XeroWirePurchaseOrderWrite"/> or <see cref="XeroWireBillWrite"/>).</param>
    public static string Serialise(object body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return JsonSerializer.Serialize(body, body.GetType(), XeroWire.JsonOptions);
    }

    /// <summary>The create's body, to re-send under <see cref="IdempotencyKey"/>; <see langword="null"/> when not recorded or unreadable (that create cannot be recovered).</summary>
    /// <typeparam name="T">The write model (<see cref="XeroWirePurchaseOrderWrite"/> or <see cref="XeroWireBillWrite"/>).</typeparam>
    public T? BodyAs<T>()
        where T : class
    {
        if (string.IsNullOrWhiteSpace(Body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(Body, XeroWire.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

using System.Text.Json;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// The durable record of every purchasing create that may have reached Xero
/// (`v0.24.0` X5): written immediately before
/// <c>CreatePurchaseOrderAsync</c> or <c>CreateBillAsync</c> goes, so the
/// handlers can recover the record a create made whose answer was lost: by
/// re-sending that create's body under its own <c>Idempotency-Key</c> (Xero
/// replays its first answer, the record's id) and reading that id back
/// (<see cref="XeroPurchasingOwnership"/>) — never by matching a number.
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
/// number, contact and body it carried and its <c>Idempotency-Key</c>. A create
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
    public async Task RecordSendingAsync(
        string tenantId, XeroDocumentRef document, string number, string contactId, string idempotencyKey, CancellationToken cancellationToken = default,
        string? reference = null, string? value = null, string? body = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var sent = new XeroPurchasingSentCreate(
            number.Trim(), contactId.Trim(), idempotencyKey, string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(), string.IsNullOrWhiteSpace(value) ? null : value,
            string.IsNullOrWhiteSpace(body) ? null : body);
        await UpdateAsync(tenantId, document, list => list.Any(s => s == sent) ? list : [.. list, sent], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Strikes off the create sent with <paramref name="idempotencyKey"/> when Xero's answer was a definite refusal (nothing was made); keeps it for any other outcome.</summary>
    /// <param name="tenantId">The Xero tenant.</param>
    /// <param name="document">The TempestOS document.</param>
    /// <param name="idempotencyKey">The create's <c>Idempotency-Key</c>.</param>
    /// <param name="outcome">What Xero answered.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task RecordAnswerAsync(
        string tenantId, XeroDocumentRef document, string idempotencyKey, ConnectorOutcome outcome, CancellationToken cancellationToken = default)
    {
        if (outcome is not (ConnectorOutcome.Rejected or ConnectorOutcome.Reauthorise))
            return;

        await UpdateAsync(tenantId, document, list => [.. list.Where(s => !string.Equals(s.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))], cancellationToken)
            .ConfigureAwait(false);
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

    private static string Key(string tenantId, XeroDocumentRef document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(document);
        return $"{tenantId}/{document.Kind}/{document.TempestKey}";
    }

}

/// <summary>One purchasing create sent to Xero (<see cref="XeroPurchasingCreateLog"/>): the number, contact, value-bearing content and body it carried and its <c>Idempotency-Key</c>.</summary>
/// <param name="Number">The Xero number the create carried.</param>
/// <param name="ContactId">The Xero <c>ContactID</c> the create carried.</param>
/// <param name="IdempotencyKey">The create's <c>Idempotency-Key</c>.</param>
/// <param name="Reference">The <c>Reference</c> the create carried; <see langword="null"/> when none (a bill carries none).</param>
/// <param name="Value">The value-bearing content the create carried (<see cref="XeroPurchasingOwnership"/>); only ever words a refusal, never proves a record ours.</param>
/// <param name="Body">The create's body exactly as sent, serialised with <see cref="XeroWire.JsonOptions"/> (a <see cref="XeroWirePurchaseOrderWrite"/> or <see cref="XeroWireBillWrite"/>); re-sent under <see cref="IdempotencyKey"/> to recover the record's id. <see langword="null"/> when unknown: that create cannot be recovered, so TempestOS cannot tell.</param>
public sealed record XeroPurchasingSentCreate(string Number, string ContactId, string IdempotencyKey, string? Reference = null, string? Value = null, string? Body = null)
{
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

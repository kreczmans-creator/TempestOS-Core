using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// The one ownership rule the purchase-order and expense-bill handlers apply,
/// on every path, before they update, delete or link a Xero record
/// (`v0.24.0` X5).
/// </summary>
/// <remarks>
/// <para><b>The rule: identity, never matching.</b></para>
/// <list type="number">
/// <item><b>Ours.</b> A Xero purchase order or bill is TempestOS's own only
/// when it is linked to the TempestOS document, or when it is the record a
/// create the <see cref="XeroPurchasingCreateLog"/> recorded for this
/// document made, read back by its id. That id is recorded in the log as soon
/// as any answer reveals it — the create's own, or a replay of it — and from
/// then on the create is only ever read back by that id, never re-sent. While
/// no id is known, and only while Xero still holds the create's
/// <c>Idempotency-Key</c> (<see cref="IdempotencyKeyLifetime"/> from when it
/// was first sent), its body is re-sent under that key (Xero replays its first
/// answer, which carries the id). Nothing else proves a record ours — not its
/// number, contact, amounts, reference or dates, all of which a bookkeeper may
/// key or edit. A record found only by matching is never adopted: every
/// TempestOS purchasing create carries a key and is logged first (the
/// <em>Send to Xero</em> of a document from before v0.24.0 queues the same
/// keyed create), so there is no keyless create whose record matching could
/// ever be needed to recover.</item>
/// <item><b>Deleted in Xero.</b> When the record read back by its id is no
/// longer live (deleted, or a bill voided), the log keeps a tombstone for it:
/// it is never linked, its create is never sent again by Retry, a cancel or a
/// delete, and nothing else under its number is touched for it. A live push
/// whose own create it was is Rejected; a cancel or delete ends NothingToDo.
/// Only the person's deliberate <em>Send again</em>
/// (<see cref="XeroPurchasingSendAgain"/>) releases the tombstone, and the
/// push then sends the document as a new record under a new key
/// (<see cref="CreateKey"/>).</item>
/// <item><b>Cannot tell.</b> When no id is known and the create cannot be
/// re-sent safely — it was sent longer ago than Xero keeps its key (a re-send
/// would then be a fresh create, a second record), TempestOS holds no copy of
/// it, or Xero refuses its replay — TempestOS cannot tell where that create's
/// record is: it is Rejected with the reason, and nothing is linked, sent or
/// deleted.</item>
/// <item><b>Only ours is touched.</b> Only ours is ever updated, deleted or
/// linked. Any other live record under the number a create would use is in
/// the way: it is reported once, with a message that never asks anyone to
/// destroy the bookkeeper's record, and the entry ends
/// <see cref="XeroPushOutcome.NothingToDo"/> when its source is cancelled,
/// deleted or gone and <see cref="XeroPushOutcome.Rejected"/> (Retry) for a
/// live push.</item>
/// </list>
/// <para>
/// Residual risk, by design: a create whose answer was lost and which is not
/// recovered within <see cref="IdempotencyKeyLifetime"/> of being sent (the
/// drain did not run in time) stays CannotTell — TempestOS never re-sends it,
/// for a purchase order or a bill, so it never makes a second record, but the
/// person must check Xero with whoever keeps the books. Once its id is known
/// it is only ever read back, whatever then happens to the key.
/// </para>
/// </remarks>
public static class XeroPurchasingOwnership
{
    /// <summary>
    /// How long after a create was first sent TempestOS relies on Xero still
    /// holding its <c>Idempotency-Key</c>: five minutes.
    /// </summary>
    /// <remarks>
    /// Xero documents that an idempotency key is stored for 6 minutes from the
    /// first call, and that a key repeated after that is processed as a new
    /// request (Xero Developer, "Idempotent requests",
    /// https://developer.xero.com/documentation/guides/idempotent-requests/idempotency/,
    /// as quoted by search on 2026-10-02; the page itself renders client-side and
    /// was not read verbatim — re-confirm before release). One minute is kept in
    /// hand for the time between logging a create and Xero receiving it. A
    /// create older than this with no known id is never re-sent: CannotTell.
    /// </remarks>
    public static readonly TimeSpan IdempotencyKeyLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Why a create with no known id is not re-sent once Xero may have forgotten its key (<see cref="XeroRecovery.Unrecoverable"/>).</summary>
    public const string KeyExpiredProblem =
        "it was sent longer ago than Xero keeps its Idempotency-Key, so sending it again could make a second record instead of replaying the first answer";

    /// <summary>Why a create TempestOS holds no copy of cannot be recovered.</summary>
    public const string NoBodyProblem = "TempestOS holds no copy of what it sent";

    /// <summary>How one logged create is recovered (<see cref="RecoveryFor"/>).</summary>
    public enum RecoveryStep
    {
        /// <summary>A tombstone: the record is gone from Xero; nothing is asked of Xero.</summary>
        Tombstoned,

        /// <summary>Its record's id is known: read it back by that id, never re-send.</summary>
        ReadById,

        /// <summary>No id known, and Xero still holds its key: re-send its body under that key, then read the answered id back.</summary>
        Replay,

        /// <summary>No id known, and Xero may have forgotten its key (or when it was sent is unknown): never re-send — cannot tell.</summary>
        KeyExpired,

        /// <summary>No id known, and no copy of its body: cannot tell.</summary>
        NoBody,
    }

    /// <summary>How <paramref name="create"/> is recovered at <paramref name="now"/>: by its id once known; by its key only while no id is known and Xero still holds the key.</summary>
    /// <param name="create">The logged create.</param>
    /// <param name="now">The time now.</param>
    public static RecoveryStep RecoveryFor(XeroPurchasingSentCreate create, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (create.GoneStatus is not null)
            return RecoveryStep.Tombstoned;
        if (!string.IsNullOrWhiteSpace(create.XeroId))
            return RecoveryStep.ReadById;
        if (string.IsNullOrWhiteSpace(create.Body))
            return RecoveryStep.NoBody;
        return create.SentAtUtc is { } sent && now - sent < IdempotencyKeyLifetime && now >= sent
            ? RecoveryStep.Replay
            : RecoveryStep.KeyExpired;
    }

    /// <summary>
    /// The <c>Idempotency-Key</c> a create for the entry keyed
    /// <paramref name="entryKey"/> is sent under: the entry's own key, unless
    /// the person chose <em>Send again</em> for the record a create under that
    /// key made (its tombstone released) — then a new key derived from it, one
    /// per release, so Xero cannot replay the deleted record's answer and the
    /// document goes as a new record.
    /// </summary>
    /// <param name="entryKey">The outbox entry's key.</param>
    /// <param name="sent">The creates the log recorded for the document.</param>
    public static string CreateKey(string entryKey, IEnumerable<XeroPurchasingSentCreate> sent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryKey);
        ArgumentNullException.ThrowIfNull(sent);
        var released = sent.Where(s => s.ReleasedAtUtc is not null).Select(s => s.IdempotencyKey).ToHashSet(StringComparer.Ordinal);
        var key = entryKey;
        for (var generation = 1; released.Contains(key); generation++)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{entryKey}|again|{generation}")));
            key = XeroIdempotencyKey.Prefix + "again:" + Convert.ToHexStringLower(hash);
        }

        return key;
    }

    /// <summary>The value-bearing content a purchase-order create carries: currency and net total. Xero computes its tax, so tax is not part of it.</summary>
    /// <param name="order">The create's body.</param>
    public static string ValueOf(XeroWirePurchaseOrderWrite order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Format("PO", order.CurrencyCode, order.LineItems.Sum(LineNet), tax: null);
    }

    /// <summary>The value-bearing content of a purchase order Xero holds, comparable with <see cref="ValueOf(XeroWirePurchaseOrderWrite)"/>; <see langword="null"/> when Xero's answer does not carry it (which proves nothing).</summary>
    /// <param name="order">The order as Xero answered it.</param>
    public static string? ValueOf(XeroWirePurchaseOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var net = order.SubTotal ?? order.LineItems?.Sum(LineNet);
        return net is null || string.IsNullOrWhiteSpace(order.CurrencyCode) ? null : Format("PO", order.CurrencyCode, net.Value, tax: null);
    }

    /// <summary>The value-bearing content a bill create carries: currency, net total and the recorded VAT.</summary>
    /// <param name="bill">The create's body.</param>
    public static string ValueOf(XeroWireBillWrite bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        return Format("BILL", bill.CurrencyCode, bill.LineItems.Sum(LineNet), bill.LineItems.Sum(l => l.TaxAmount ?? 0m));
    }

    /// <summary>The value-bearing content of a bill Xero holds, comparable with <see cref="ValueOf(XeroWireBillWrite)"/>; <see langword="null"/> when Xero's answer does not carry it (which proves nothing).</summary>
    /// <param name="bill">The bill as Xero answered it.</param>
    public static string? ValueOf(XeroWireBill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        var net = bill.SubTotal ?? bill.LineItems?.Sum(LineNet);
        var tax = bill.TotalTax
                  ?? (bill.LineItems is { } lines && lines.All(l => l.TaxAmount is not null) ? lines.Sum(l => l.TaxAmount!.Value) : null);
        return net is null || tax is null || string.IsNullOrWhiteSpace(bill.CurrencyCode) ? null : Format("BILL", bill.CurrencyCode, net.Value, tax.Value);
    }

    /// <summary>
    /// Applies the rule to what recovering each logged create found, then to
    /// the live records under the number a create would use. The one place a
    /// purchasing record is judged ours or not.
    /// </summary>
    /// <remarks>
    /// In order:
    /// <list type="number">
    /// <item>A create's record read back live — <see cref="XeroOwnershipVerdict.Ours"/>
    /// (the entry's own create's first, else the latest).</item>
    /// <item>A create that could not be recovered (no id known, and its key possibly forgotten, its body unknown or its replay refused) — <see cref="XeroOwnershipVerdict.CannotTell"/>.</item>
    /// <item>The entry's own create's record no longer live (or, when the
    /// source is gone, every recovered record no longer live) —
    /// <see cref="XeroOwnershipVerdict.DeletedInXero"/>.</item>
    /// <item>A live record in the way: carrying what TempestOS sent for another
    /// document of the kind — <see cref="XeroOwnershipVerdict.AnotherDocuments"/>;
    /// otherwise <see cref="XeroOwnershipVerdict.NotOurs"/>.</item>
    /// <item>Otherwise <see cref="XeroOwnershipVerdict.NothingLive"/>.</item>
    /// </list>
    /// A create recovered no longer live that is neither the entry's own nor
    /// (the source live) the only evidence is an earlier entry's: it blocks
    /// nothing, and a newer entry (a new key) may send the document anew.
    /// </remarks>
    /// <typeparam name="T">The wire record.</typeparam>
    /// <param name="recovered">One per create the log recorded for this document: what re-sending it under its key and reading the answered id back found.</param>
    /// <param name="resendKey">The <c>Idempotency-Key</c> of the entry being pushed when it would send a create (a live, current push); <see langword="null"/> otherwise.</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    /// <param name="inTheWay">The live records under the number a create would use that no other TempestOS document is linked to.</param>
    /// <param name="valueOf">The record's value-bearing content.</param>
    /// <param name="sentForOthersUnderPair">The creates the log recorded for other documents of the kind under that number and contact (only words the refusal).</param>
    public static XeroOwnershipJudgement<T> Judge<T>(
        IReadOnlyList<XeroRecoveredCreate<T>> recovered,
        string? resendKey,
        bool sourceGone,
        IReadOnlyList<T> inTheWay,
        Func<T, string?> valueOf,
        IReadOnlyList<XeroPurchasingSentCreate>? sentForOthersUnderPair = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(recovered);
        ArgumentNullException.ThrowIfNull(inTheWay);
        ArgumentNullException.ThrowIfNull(valueOf);

        bool Own(XeroRecoveredCreate<T> r) => resendKey is not null && string.Equals(r.Create.IdempotencyKey, resendKey, StringComparison.Ordinal);

        var live = recovered.Where(r => r.Recovery == XeroRecovery.Live && r.Record is not null).ToList();
        if (live.Count > 0)
            return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.Ours, (live.FirstOrDefault(Own) ?? live[^1]).Record, From: live.FirstOrDefault(Own) ?? live[^1]);

        if (recovered.FirstOrDefault(r => r.Recovery == XeroRecovery.Unrecoverable) is { } unknown)
            return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.CannotTell, From: unknown);

        var gone = recovered.Where(r => r.Recovery == XeroRecovery.Gone).ToList();
        if ((gone.FirstOrDefault(Own) ?? (sourceGone ? gone.LastOrDefault() : null)) is { } deleted)
            return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.DeletedInXero, deleted.Record, From: deleted);

        if (inTheWay.Count == 0)
            return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.NothingLive);

        var sentForOthers = (sentForOthersUnderPair ?? [])
            .Where(s => s.Value is not null)
            .Select(s => s.Value!)
            .ToHashSet(StringComparer.Ordinal);
        return inTheWay.Any(r => valueOf(r) is { } v && sentForOthers.Contains(v))
            ? new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.AnotherDocuments)
            : new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.NotOurs);
    }

    /// <summary>The creates in <paramref name="sent"/> made under <paramref name="number"/> to <paramref name="contactId"/>.</summary>
    /// <param name="sent">The creates the log recorded.</param>
    /// <param name="number">The number.</param>
    /// <param name="contactId">The contact.</param>
    public static IReadOnlyList<XeroPurchasingSentCreate> SentUnder(IEnumerable<XeroPurchasingSentCreate> sent, string number, string contactId) =>
        [.. sent.Where(s => SamePair((s.Number, s.ContactId), (number, contactId)))];

    /// <summary>The creates to recover: each <c>Idempotency-Key</c> once, with the body it was first logged with (the one Xero holds it for); never one whose tombstone the person released with <em>Send again</em>.</summary>
    /// <param name="sent">The creates the log recorded for the document.</param>
    public static IReadOnlyList<XeroPurchasingSentCreate> ToRecover(IEnumerable<XeroPurchasingSentCreate> sent)
    {
        ArgumentNullException.ThrowIfNull(sent);
        return [.. sent.Where(s => s.ReleasedAtUtc is null).DistinctBy(s => s.IdempotencyKey, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Rule 3, not ours: a live record under <paramref name="number"/> is
    /// someone else's (keyed in Xero, another contact's, or changed in Xero
    /// since TempestOS sent it). It is left as it is.
    /// </summary>
    /// <param name="noun">"Purchase order" or "Bill".</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="number">The number.</param>
    /// <param name="liveAdvice">What a live push's message ends with (never advice to destroy the record).</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    public static XeroPushResult NotOurs(string noun, string document, string number, string liveAdvice, bool sourceGone) => sourceGone
        ? new XeroPushResult(
            XeroPushOutcome.NothingToDo,
            $"No {noun.ToLowerInvariant()} TempestOS sent for this {document} is live in Xero; the {noun.ToLowerInvariant()} numbered {number} there is not TempestOS's, and is left as it is. Nothing is deleted in Xero.")
        : new XeroPushResult(
            XeroPushOutcome.Rejected,
            $"{noun} number {number} is already used in Xero by another {noun.ToLowerInvariant()}, which TempestOS did not send (keyed in Xero, another contact's, or changed there since); "
            + $"TempestOS never changes, links or deletes a record it did not send, so that one is left as it is and nothing is sent. {liveAdvice}");

    /// <summary>
    /// Cannot tell: a create the log recorded for this document could not be
    /// recovered — no id is known and it was sent longer ago than Xero keeps
    /// its key (<see cref="IdempotencyKeyLifetime"/>), Xero refused its
    /// replay, or TempestOS no longer holds what it sent. That create's record may be in Xero under any number, so
    /// nothing is linked, sent or deleted, and the entry is Rejected (Retry)
    /// whatever its source's state.
    /// </summary>
    /// <param name="noun">"Purchase order" or "Bill".</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="number">The number the create carried.</param>
    /// <param name="problem">Why it could not be recovered (Xero's refusal, verbatim where it gave one).</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    public static XeroPushResult CannotTell(string noun, string document, string number, string? problem, bool sourceGone) => new(
        XeroPushOutcome.Rejected,
        $"The answer to the {noun.ToLowerInvariant()} TempestOS sent to Xero for this {document} under number {number} was lost, and it cannot be recovered "
        + $"({(string.IsNullOrWhiteSpace(problem) ? "no reason given" : problem.Trim().TrimEnd('.'))}), so TempestOS cannot tell whether that {noun.ToLowerInvariant()} is in Xero. "
        + $"It leaves every {noun.ToLowerInvariant()} in Xero as it is: nothing is sent{(sourceGone ? ", and nothing is deleted" : string.Empty)}. "
        + $"Retrying will not change this: TempestOS will not send this {document} to Xero automatically. "
        + $"Whoever keeps the books should check Xero for {number}: if it is there, leave it (key it against this {document} by hand if needed); if it is not, key it into Xero by hand.");

    /// <summary>
    /// The record a create the log recorded for this document made — found by
    /// replaying its <c>Idempotency-Key</c> and reading its id back — is no
    /// longer live in Xero (deleted, or a bill voided), however it was changed
    /// there first. It is never linked and its create never sent again; nothing
    /// else under the number is touched. A live push is Rejected; a cancel or
    /// delete ends NothingToDo.
    /// </summary>
    /// <param name="noun">"Purchase order" or "Bill".</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="number">The record's number in Xero (or the number its create carried).</param>
    /// <param name="status">The record's status in Xero (<c>DELETED</c> or <c>VOIDED</c>).</param>
    /// <param name="advice">What a live push's message ends with: how to send it as a new record, if that is possible.</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    public static XeroPushResult DeletedInXero(string noun, string document, string number, string? status, string advice, bool sourceGone)
    {
        var act = string.Equals(status?.Trim(), "VOIDED", StringComparison.OrdinalIgnoreCase) ? "voided" : "deleted";
        return sourceGone
            ? new XeroPushResult(
                XeroPushOutcome.NothingToDo,
                $"{noun} {number}, the one TempestOS sent for this {document}, was already {act} in Xero; nothing is deleted there, and nothing else under the number is touched.")
            : new XeroPushResult(
                XeroPushOutcome.Rejected,
                $"{noun} {number}, the one TempestOS sent for this {document} (its answer from Xero was lost), was {act} in Xero. "
                + $"TempestOS does not send it again, so it is not live in Xero; nothing is sent. {advice}");
    }

    /// <summary>
    /// Rule 3, another TempestOS document's: a live record under
    /// <paramref name="number"/> carries what TempestOS sent for another
    /// document of the kind, whose create's answer was lost and which is not
    /// linked yet. It is left as it is (that document's own push links it).
    /// </summary>
    /// <param name="noun">"Purchase order" or "Bill".</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="number">The number.</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    public static XeroPushResult AnotherDocuments(string noun, string document, string number, bool sourceGone) => sourceGone
        ? new XeroPushResult(
            XeroPushOutcome.NothingToDo,
            $"No {noun.ToLowerInvariant()} TempestOS sent for this {document} is live in Xero; the {noun.ToLowerInvariant()} numbered {number} there carries what TempestOS sent for another {document}, and is left as it is. Nothing is deleted in Xero.")
        : new XeroPushResult(
            XeroPushOutcome.Rejected,
            $"{noun} number {number} is in use in Xero by a {noun.ToLowerInvariant()} carrying what TempestOS sent for another {document}, which is still being reconciled (its answer from Xero was lost); "
            + $"it is left as it is and nothing is sent. Retry once that {document} has synced.");

    private static bool SamePair((string Number, string ContactId) a, (string Number, string ContactId) b) =>
        string.Equals(a.Number.Trim(), b.Number.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ContactId.Trim(), b.ContactId.Trim(), StringComparison.OrdinalIgnoreCase);

    private static decimal LineNet(XeroWireLineItem line) => line.LineAmount ?? Round(line.Quantity * line.UnitAmount);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Format(string kind, string? currency, decimal net, decimal? tax) => string.Join(
        '|',
        kind,
        string.IsNullOrWhiteSpace(currency) ? "?" : currency.Trim().ToUpperInvariant(),
        Round(net).ToString("0.00", CultureInfo.InvariantCulture),
        tax is null ? "-" : Round(tax.Value).ToString("0.00", CultureInfo.InvariantCulture));
}

/// <summary>What <see cref="XeroPurchasingOwnership.Judge{T}"/> found.</summary>
public enum XeroOwnershipVerdict
{
    /// <summary>Nothing of TempestOS's is live, and nothing is in the way of a create.</summary>
    NothingLive,

    /// <summary>A create's record, recovered by its key and read back live: link it.</summary>
    Ours,

    /// <summary>Live records in the way, none TempestOS's: touch none.</summary>
    NotOurs,

    /// <summary>The record the entry's own create made (recovered by its key, read back by its id) is deleted or voided in Xero: never link it, never resend, touch nothing else.</summary>
    DeletedInXero,

    /// <summary>Live records in the way, none TempestOS's for this document, one carrying what TempestOS sent for another document of the kind (its create's answer lost, not yet linked): touch none.</summary>
    AnotherDocuments,

    /// <summary>A create could not be recovered (its replay refused, or its body unknown): TempestOS cannot tell, so it links, deletes and sends nothing.</summary>
    CannotTell,
}

/// <summary>What recovering one logged create found.</summary>
public enum XeroRecovery
{
    /// <summary>Its record, read back by its id (known, or answered by its replay), is live in Xero.</summary>
    Live,

    /// <summary>Its record, read back by its id, is deleted or voided in Xero (or no longer there at all), or its tombstone says so.</summary>
    Gone,

    /// <summary>No id is known, and its replay was refused or it could not safely be re-sent (key possibly forgotten, body unknown): where its record is cannot be told.</summary>
    Unrecoverable,
}

/// <summary>One logged create and what recovering it (<see cref="XeroPurchasingRecovery"/>) found.</summary>
/// <typeparam name="T">The wire record.</typeparam>
/// <param name="Create">The logged create, with what recovering it learned (its id, its tombstone).</param>
/// <param name="Recovery">What was found.</param>
/// <param name="Record">The record read back (or, when Xero no longer holds it at all, as the replay answered it); <see langword="null"/> when unrecoverable.</param>
/// <param name="Problem">Why it could not be recovered (<see cref="XeroRecovery.Unrecoverable"/> only).</param>
public sealed record XeroRecoveredCreate<T>(XeroPurchasingSentCreate Create, XeroRecovery Recovery, T? Record = null, string? Problem = null)
    where T : class;

/// <summary>The verdict, the record it is about, and the recovered create it came from.</summary>
/// <typeparam name="T">The wire record.</typeparam>
/// <param name="Verdict">The verdict.</param>
/// <param name="Ours">The record: ours (<see cref="XeroOwnershipVerdict.Ours"/>) or ours and gone (<see cref="XeroOwnershipVerdict.DeletedInXero"/>).</param>
/// <param name="From">The recovered create the verdict rests on (<see cref="XeroOwnershipVerdict.Ours"/>, <see cref="XeroOwnershipVerdict.DeletedInXero"/>, <see cref="XeroOwnershipVerdict.CannotTell"/>).</param>
public sealed record XeroOwnershipJudgement<T>(XeroOwnershipVerdict Verdict, T? Ours = null, XeroRecoveredCreate<T>? From = null)
    where T : class;

using System.Globalization;
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
/// document made: that create's body re-sent under its own
/// <c>Idempotency-Key</c> (Xero replays its first answer, which carries the
/// record's id), and that id read back. Nothing else proves a record ours —
/// not its number, contact, amounts, reference or dates, all of which a
/// bookkeeper may key or edit. A record found only by matching is never
/// adopted: every TempestOS purchasing create carries a key and is logged
/// first (the <em>Send to Xero</em> of a document from before v0.24.0 queues
/// the same keyed create), so there is no keyless create whose record
/// matching could ever be needed to recover.</item>
/// <item><b>Deleted in Xero.</b> When the record read back by its id is no
/// longer live (deleted, or a bill voided), it stays TempestOS's — it is
/// never linked, its create is never sent again (the same key would only
/// replay it), and nothing else under its number is touched for it. A live
/// push whose own create it was is Rejected; a cancel or delete ends
/// NothingToDo.</item>
/// <item><b>Cannot tell.</b> When the replay is refused (the key expired and
/// Xero reports the number already used, or any other refusal) or the create
/// cannot be re-sent, TempestOS cannot tell where that create's record is:
/// it is Rejected with the reason, and nothing is linked, sent or deleted.</item>
/// <item><b>Only ours is touched.</b> Only ours is ever updated, deleted or
/// linked. Any other live record under the number a create would use is in
/// the way: it is reported once, with a message that never asks anyone to
/// destroy the bookkeeper's record, and the entry ends
/// <see cref="XeroPushOutcome.NothingToDo"/> when its source is cancelled,
/// deleted or gone and <see cref="XeroPushOutcome.Rejected"/> (Retry) for a
/// live push.</item>
/// </list>
/// <para>
/// Residual risk, by design: Xero keeps an <c>Idempotency-Key</c> for a
/// limited time. A purchase order's replay after that is refused (its number
/// is unique) and so is CannotTell; a bill's number is not unique in Xero, so
/// a bill create recovered only after its key expired is made again. Recovery
/// runs on the next drain, normally minutes after the lost answer.
/// </para>
/// </remarks>
public static class XeroPurchasingOwnership
{
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
    /// <item>A create that could not be recovered — <see cref="XeroOwnershipVerdict.CannotTell"/>.</item>
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

    /// <summary>The creates to recover: each <c>Idempotency-Key</c> once, with the body it was first logged with (the one Xero holds it for).</summary>
    /// <param name="sent">The creates the log recorded for the document.</param>
    public static IReadOnlyList<XeroPurchasingSentCreate> ToRecover(IEnumerable<XeroPurchasingSentCreate> sent)
    {
        ArgumentNullException.ThrowIfNull(sent);
        return [.. sent.DistinctBy(s => s.IdempotencyKey, StringComparer.Ordinal)];
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
    /// recovered — Xero refused its replay (its key expired and the number is
    /// already used, or any other refusal), or TempestOS no longer holds what
    /// it sent. That create's record may be in Xero under any number, so
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
        $"The answer to the {noun.ToLowerInvariant()} TempestOS sent to Xero for this {document} under number {number} was lost, and Xero did not replay it "
        + $"({(string.IsNullOrWhiteSpace(problem) ? "no reason given" : problem.Trim().TrimEnd('.'))}), so TempestOS cannot tell whether that {noun.ToLowerInvariant()} is in Xero. "
        + $"It leaves every {noun.ToLowerInvariant()} in Xero as it is: nothing is sent{(sourceGone ? ", and nothing is deleted" : string.Empty)}. "
        + "Check in Xero with whoever keeps the books, then Retry.");

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
    /// <summary>Its replay answered an id, and Xero holds that record live.</summary>
    Live,

    /// <summary>Its replay answered an id, and Xero holds that record deleted or voided (or no longer at all).</summary>
    Gone,

    /// <summary>Its replay was refused (or it could not be re-sent): where its record is cannot be told.</summary>
    Unrecoverable,
}

/// <summary>One logged create, re-sent under its own <c>Idempotency-Key</c>, and what reading the answered id back found.</summary>
/// <typeparam name="T">The wire record.</typeparam>
/// <param name="Create">The logged create.</param>
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

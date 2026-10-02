using System.Globalization;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// The one ownership rule the purchase-order and expense-bill handlers apply,
/// on every path, before they update, delete or link a Xero record
/// (`v0.24.0` X5).
/// </summary>
/// <remarks>
/// <para><b>The rule.</b></para>
/// <list type="number">
/// <item><b>Ours.</b> A Xero purchase order or bill is TempestOS's own only
/// when it is linked to the TempestOS document, or when its number
/// <em>and</em> contact match a create the <see cref="XeroPurchasingCreateLog"/>
/// recorded for this document <em>and</em> its value-bearing content (see
/// <see cref="ValueOf(XeroWirePurchaseOrderWrite)"/>) equals what that create
/// carried. Free text a bookkeeper may edit (reference, descriptions, dates)
/// is never part of the test. Every record under the number and contact is
/// evidence, deleted ones included: when more records carry a sent content
/// than creates were sent with it, someone keyed one by hand and TempestOS
/// cannot tell which is its own (<see cref="XeroOwnershipVerdict.Ambiguous"/>).</item>
/// <item><b>Only ours is touched.</b> Only ours is ever updated, deleted or
/// linked. A cancel or delete never deletes a record that is not provably
/// ours.</item>
/// <item><b>Anything else is left alone.</b> It is reported once, with a
/// message that never asks anyone to destroy the bookkeeper's record. The
/// entry ends <see cref="XeroPushOutcome.NothingToDo"/> when its source is
/// cancelled, deleted or gone (nothing is left to send, and the queue moves
/// on), and <see cref="XeroPushOutcome.Rejected"/> (Retry) for a live push.</item>
/// </list>
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
    /// Applies the rule to the records Xero holds under one number and contact.
    /// The one place a purchasing record is judged ours or not.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Nothing live, and a deleted (or voided) record carries what the
    /// entry's own create (<paramref name="entryKey"/>) sent: that create may
    /// have landed and been deleted in Xero, and a resend under the same key
    /// would only replay Xero's answer about the deleted record —
    /// <see cref="XeroOwnershipVerdict.DeletedInXero"/>.</item>
    /// <item>Every amount group is weighed: a group with more records than
    /// creates is ambiguous, but never hides a live record another group
    /// proves ours. <see cref="XeroOwnershipVerdict.Ambiguous"/> only when no
    /// group proves one.</item>
    /// <item>Live records, none ours, one carrying what TempestOS sent for
    /// another document of the kind under the pair (that document's create
    /// answer lost, not yet linked) — <see cref="XeroOwnershipVerdict.AnotherDocuments"/>.</item>
    /// <item>Evidence not read in full (<paramref name="evidenceComplete"/>
    /// false) never yields <see cref="XeroOwnershipVerdict.Ours"/>,
    /// <see cref="XeroOwnershipVerdict.DeletedInXero"/> or a resend: it is
    /// <see cref="XeroOwnershipVerdict.CannotTell"/>.</item>
    /// </list>
    /// </remarks>
    /// <typeparam name="T">The wire record.</typeparam>
    /// <param name="underPair">Every record Xero holds under the number for the contact, every status — less any linked to another TempestOS document (never this one's).</param>
    /// <param name="sentUnderPair">The creates the log recorded for this document under the number to the contact.</param>
    /// <param name="valueOf">The record's value-bearing content.</param>
    /// <param name="isLive">Whether Xero still holds the record live (not deleted or voided).</param>
    /// <param name="entryKey">The <c>Idempotency-Key</c> of the entry being pushed, whose create a resend would repeat; <see langword="null"/> when nothing would be resent.</param>
    /// <param name="sentForOthersUnderPair">The creates the log recorded for other documents of the kind under the number to the contact.</param>
    /// <param name="evidenceComplete">Whether every record under the pair was read (deleted ones included).</param>
    public static XeroOwnershipJudgement<T> Judge<T>(
        IReadOnlyList<T> underPair,
        IReadOnlyList<XeroPurchasingSentCreate> sentUnderPair,
        Func<T, string?> valueOf,
        Func<T, bool> isLive,
        string? entryKey = null,
        IReadOnlyList<XeroPurchasingSentCreate>? sentForOthersUnderPair = null,
        bool evidenceComplete = true)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(underPair);
        ArgumentNullException.ThrowIfNull(sentUnderPair);
        ArgumentNullException.ThrowIfNull(valueOf);
        ArgumentNullException.ThrowIfNull(isLive);

        if (!underPair.Any(isLive))
        {
            var sentByEntry = sentUnderPair
                .Where(s => s.Value is not null && string.Equals(s.IdempotencyKey, entryKey, StringComparison.Ordinal))
                .Select(s => s.Value!)
                .ToHashSet(StringComparer.Ordinal);
            if (sentByEntry.Count == 0)
                return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.NothingLive);

            // The entry's own create may have landed and been deleted: a resend under its key would replay that answer.
            var deleted = underPair.FirstOrDefault(r => valueOf(r) is { } v && sentByEntry.Contains(v));
            if (deleted is not null)
                return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.DeletedInXero, deleted);

            return evidenceComplete
                ? new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.NothingLive)
                : new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.CannotTell);
        }

        T? ours = null;
        var ambiguous = 0;
        var sentByValue = sentUnderPair
            .Where(s => s.Value is not null)
            .GroupBy(s => s.Value!, StringComparer.Ordinal)
            .Select(g => (Value: g.Key, Creates: g.Select(s => s.IdempotencyKey).Distinct(StringComparer.Ordinal).Count()));
        foreach (var (value, creates) in sentByValue)
        {
            var carrying = underPair.Where(r => string.Equals(valueOf(r), value, StringComparison.Ordinal)).ToList();
            var live = carrying.Where(isLive).ToList();
            if (live.Count == 0)
                continue;

            // More records carry what was sent than creates sent it: one was keyed by someone else.
            // Weigh the other groups still: one of them may prove a live record ours.
            if (carrying.Count > creates)
            {
                ambiguous = Math.Max(ambiguous, carrying.Count);
                continue;
            }

            ours ??= live[0];
        }

        if (ours is not null)
        {
            return evidenceComplete
                ? new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.Ours, ours)
                : new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.CannotTell);
        }

        if (ambiguous > 0)
            return new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.Ambiguous, Count: ambiguous);

        var sentForOthers = (sentForOthersUnderPair ?? [])
            .Where(s => s.Value is not null)
            .Select(s => s.Value!)
            .ToHashSet(StringComparer.Ordinal);
        return underPair.Any(r => isLive(r) && valueOf(r) is { } v && sentForOthers.Contains(v))
            ? new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.AnotherDocuments)
            : new XeroOwnershipJudgement<T>(XeroOwnershipVerdict.NotOurs);
    }

    /// <summary>The (number, contact) pairs to look Xero up by: <paramref name="current"/> first (when known), then every pair the log says a create was sent with, each once.</summary>
    /// <param name="current">The pairs the document carries now.</param>
    /// <param name="sent">The creates the log recorded for the document.</param>
    public static IReadOnlyList<(string Number, string ContactId)> Pairs(
        IEnumerable<(string Number, string ContactId)> current, IEnumerable<XeroPurchasingSentCreate> sent)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(sent);

        var pairs = new List<(string Number, string ContactId)>();
        foreach (var (number, contactId) in current.Concat(sent.Select(s => (s.Number, s.ContactId))))
        {
            var trimmed = (number.Trim(), contactId.Trim());
            if (!pairs.Any(p => SamePair(p, trimmed)))
                pairs.Add(trimmed);
        }

        return pairs;
    }

    /// <summary>The creates in <paramref name="sent"/> made under <paramref name="number"/> to <paramref name="contactId"/>.</summary>
    /// <param name="sent">The creates the log recorded for the document.</param>
    /// <param name="number">The number.</param>
    /// <param name="contactId">The contact.</param>
    public static IReadOnlyList<XeroPurchasingSentCreate> SentUnder(IEnumerable<XeroPurchasingSentCreate> sent, string number, string contactId) =>
        [.. sent.Where(s => SamePair((s.Number, s.ContactId), (number, contactId)))];

    /// <summary>
    /// Whether the record linked as ours has landed with exactly what
    /// <paramref name="entry"/> itself sent (its own create's answer lost):
    /// then the link records the entry's content as pushed, and nothing is
    /// sent again.
    /// </summary>
    /// <param name="entry">The entry being pushed.</param>
    /// <param name="sentUnderPair">The creates logged under the pair the record was found under.</param>
    /// <param name="value">The record's value-bearing content.</param>
    public static bool SentByThisEntry(XeroOutboxEntry entry, IEnumerable<XeroPurchasingSentCreate> sentUnderPair, string? value)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(sentUnderPair);
        return value is not null
               && sentUnderPair.Any(s => string.Equals(s.IdempotencyKey, entry.IdempotencyKey, StringComparison.Ordinal)
                                         && string.Equals(s.Value, value, StringComparison.Ordinal));
    }

    /// <summary>
    /// Rule 3, ambiguous: several records under <paramref name="number"/>
    /// carry what TempestOS sent, more than it sent. None is touched.
    /// </summary>
    /// <param name="records">"purchase orders" or "bills".</param>
    /// <param name="number">The number.</param>
    /// <param name="count">How many records carry what was sent.</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    public static XeroPushResult Ambiguous(string records, string number, int count, string document, bool sourceGone) => new(
        sourceGone ? XeroPushOutcome.NothingToDo : XeroPushOutcome.Rejected,
        $"Xero holds {count} {records} numbered {number} for this contact with the amounts TempestOS sent, more than TempestOS sent, "
        + $"so it cannot tell which is this {document}'s own and leaves every one of them as it is. "
        + (sourceGone
            ? "Nothing is deleted in Xero."
            : "Nothing is sent; check them in Xero with whoever keeps the books, then Retry."));

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
    /// Rule 3, cannot tell: Xero holds more records under
    /// <paramref name="number"/> than TempestOS reads, so the evidence is
    /// incomplete. None is touched, and nothing is sent.
    /// </summary>
    /// <param name="records">"purchase orders" or "bills".</param>
    /// <param name="number">The number.</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="sourceGone">The TempestOS document is cancelled, deleted or gone (or the entry is a delete).</param>
    public static XeroPushResult CannotTell(string records, string number, string document, bool sourceGone) => new(
        sourceGone ? XeroPushOutcome.NothingToDo : XeroPushOutcome.Rejected,
        $"Xero holds more deleted {records} than TempestOS can read, so it cannot tell whether one numbered {number} is this {document}'s own; "
        + "it leaves every one of them as it is. "
        + (sourceGone
            ? "Nothing is deleted in Xero."
            : "Nothing is sent; check in Xero with whoever keeps the books, then Retry."));

    /// <summary>
    /// The entry's own create may have landed and been deleted (or voided) in
    /// Xero: a record under <paramref name="number"/> that is no longer live
    /// carries exactly what it sent. Sending it again under the same
    /// <c>Idempotency-Key</c> would only replay Xero's answer about that
    /// record, so nothing is sent and nothing is reported as synced.
    /// </summary>
    /// <param name="noun">"Purchase order" or "Bill".</param>
    /// <param name="document">"order" or "expense".</param>
    /// <param name="number">The number.</param>
    /// <param name="advice">What the message ends with: how to send it as a new record, if that is possible.</param>
    public static XeroPushResult DeletedInXero(string noun, string document, string number, string advice) => new(
        XeroPushOutcome.Rejected,
        $"{noun} {number}, carrying what TempestOS sent for this {document}, was deleted in Xero (it may be the one TempestOS sent, whose answer was lost). "
        + $"TempestOS does not send it again, so it is not in Xero; nothing is sent. {advice}");

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

/// <summary>What <see cref="XeroPurchasingOwnership.Judge{T}"/> found under one number and contact.</summary>
public enum XeroOwnershipVerdict
{
    /// <summary>Nothing live under the pair (deleted or voided records only, or none).</summary>
    NothingLive,

    /// <summary>A live record provably TempestOS's own: link it.</summary>
    Ours,

    /// <summary>More records carry what TempestOS sent than it sent: touch none.</summary>
    Ambiguous,

    /// <summary>Live records, none provably TempestOS's: touch none.</summary>
    NotOurs,

    /// <summary>Nothing live, and a deleted or voided record carries what the entry's own create sent: it may be that create's, deleted in Xero. Never resend (the same key would replay the deleted record); touch none.</summary>
    DeletedInXero,

    /// <summary>Live records, none provably TempestOS's, one carrying what TempestOS sent for another document of the kind (its create's answer lost, not yet linked): touch none.</summary>
    AnotherDocuments,

    /// <summary>The evidence under the pair could not be read in full: TempestOS cannot tell, so it links, deletes and resends nothing.</summary>
    CannotTell,
}

/// <summary>The verdict, the record that is ours, and (when ambiguous) how many records carry what was sent.</summary>
/// <typeparam name="T">The wire record.</typeparam>
/// <param name="Verdict">The verdict.</param>
/// <param name="Ours">The record that is ours (<see cref="XeroOwnershipVerdict.Ours"/> only).</param>
/// <param name="Count">How many records carry what was sent (<see cref="XeroOwnershipVerdict.Ambiguous"/> only).</param>
public sealed record XeroOwnershipJudgement<T>(XeroOwnershipVerdict Verdict, T? Ours = null, int Count = 0)
    where T : class;

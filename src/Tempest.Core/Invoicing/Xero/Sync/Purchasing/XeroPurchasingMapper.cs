using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// Maps TempestOS purchase orders and expenses to Xero's purchase orders and
/// draft bills (`v0.24.0` X5; design §3, §4.3, §4.4): pure functions, no
/// I/O — the planners' content hashes, the wire bodies, the bill number,
/// the attachment name.
/// </summary>
public static class XeroPurchasingMapper
{
    /// <summary>The <see cref="XeroLink.LinkedBy"/> for a purchase order or bill TempestOS created in Xero.</summary>
    public const string LinkedByCreated = "created";

    /// <summary>The <see cref="XeroLink.LinkedBy"/> for a purchase order or bill found in Xero by its number (and, for a bill, its contact) instead of created again.</summary>
    public const string LinkedByReconciled = "reconciled";

    /// <summary>The prefix of the bill number an expense with no supplier invoice number carries (Q4): <c>EXP-</c> and the expense id.</summary>
    public const string ExpenseBillNumberPrefix = "EXP-";

    /// <summary>The expense category a purchase order's lines post to in Xero (X1's per-category map) — the same category <c>PurchaseOrderService.RecordLinesAsExpensesAsync</c> records them as.</summary>
    public const ExpenseCategory PurchaseOrderLineCategory = ExpenseCategory.Materials;

    /// <summary>Xero's <c>DRAFT</c>.</summary>
    public const string StatusDraft = "DRAFT";

    /// <summary>Xero's <c>DELETED</c>.</summary>
    public const string StatusDeleted = "DELETED";

    /// <summary>Xero's <c>VOIDED</c> (a bill voided in Xero).</summary>
    public const string StatusVoided = "VOIDED";

    /// <summary>Xero's <c>BILLED</c> (a purchase order copied to a bill in Xero; it can no longer be deleted).</summary>
    public const string StatusBilled = "BILLED";

    /// <summary>The content hash of a <see cref="XeroOperation.DeletePurchaseOrder"/> or <see cref="XeroOperation.DeleteExpenseBill"/> — the same for every attempt, so the outbox never queues the same delete twice.</summary>
    public static readonly string DeleteHash = Sha256Hex("purchasing-delete:v1");

    private static readonly JsonSerializerOptions HashOptions = new() { WriteIndented = false };

    /// <summary>
    /// The hash of everything a <see cref="XeroOperation.PushPurchaseOrder"/>
    /// carries from TempestOS — number, project, supplier, dates, currency and
    /// lines — lower-case hex SHA-256 (safe in an <c>Idempotency-Key</c>).
    /// </summary>
    /// <param name="order">The purchase order.</param>
    public static string ContentHash(XeroPurchaseOrderSnapshot order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var canonical = new
        {
            v = 1,
            number = order.Reference,
            project = order.ProjectCode,
            supplier = order.SupplierOrganisationReference,
            date = order.IssuedDate is { } issued ? XeroWire.FormatDate(issued) : null,
            delivery = order.ExpectedDelivery is { } delivery ? XeroWire.FormatDate(delivery) : null,
            currency = order.CurrencyCode,
            lines = order.Lines.Select(l => new
            {
                d = l.Description,
                q = l.Quantity.ToString(CultureInfo.InvariantCulture),
                u = l.UnitPrice.ToString(CultureInfo.InvariantCulture),
                t = l.VatRate.ToString(),
            }),
        };

        return Sha256Hex(JsonSerializer.Serialize(canonical, HashOptions));
    }

    /// <summary>
    /// The hash of everything a <see cref="XeroOperation.PushExpenseBill"/>
    /// carries from TempestOS — the bill number, supplier, date, description,
    /// category, net, VAT and currency. An amended expense hashes
    /// differently, so its draft bill is updated; an unchanged one is never
    /// pushed twice.
    /// </summary>
    /// <param name="expense">The expense.</param>
    public static string ContentHash(XeroExpenseSnapshot expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        var canonical = new
        {
            v = 1,
            number = BillNumber(expense),
            project = expense.ProjectCode,
            supplier = expense.SupplierOrganisationReference ?? expense.SupplierOrganisationIdUnresolved,
            date = XeroWire.FormatDate(expense.Date),
            d = expense.Description,
            c = expense.Category.ToString(),
            net = expense.NetAmount.ToString(CultureInfo.InvariantCulture),
            vat = expense.VatAmount.ToString(CultureInfo.InvariantCulture),
            currency = expense.CurrencyCode,
        };

        return Sha256Hex(JsonSerializer.Serialize(canonical, HashOptions));
    }

    /// <summary>The bill number (Q4): the supplier's own invoice number when recorded, otherwise <c>EXP-{expense id}</c> (32 hex digits).</summary>
    /// <param name="expense">The expense.</param>
    public static string BillNumber(XeroExpenseSnapshot expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return string.IsNullOrWhiteSpace(expense.SupplierInvoiceNumber)
            ? $"{ExpenseBillNumberPrefix}{expense.Id:N}"
            : expense.SupplierInvoiceNumber.Trim();
    }

    /// <summary>
    /// The VAT rate a receipt's figures show — an expense records amounts,
    /// not a rate: no VAT is <see cref="VatRate.OutOfScope"/> (Xero's
    /// <c>NONE</c>, "No VAT"); otherwise whichever of the reduced (5%) and
    /// standard (20%) rates the VAT is nearer to. Only the input tax type
    /// follows from it: the bill line carries the recorded VAT itself as its
    /// <c>TaxAmount</c>, never a recomputed figure.
    /// </summary>
    /// <param name="netAmount">The net amount.</param>
    /// <param name="vatAmount">The VAT amount.</param>
    public static VatRate InferVatRate(decimal netAmount, decimal vatAmount)
    {
        if (vatAmount <= 0m)
            return VatRate.OutOfScope;

        if (netAmount <= 0m)
            return VatRate.Standard;

        var ratio = vatAmount / netAmount;
        return Math.Abs(ratio - VatRate.Reduced.Percentage()) < Math.Abs(ratio - VatRate.Standard.Percentage())
            ? VatRate.Reduced
            : VatRate.Standard;
    }

    /// <summary>
    /// The file name an issued purchase order's PDF carries in Xero: the
    /// order's own reference plus <c>.pdf</c> (§3). Characters a file name
    /// cannot hold become <c>-</c>.
    /// </summary>
    /// <param name="reference">The purchase order's reference.</param>
    public static string PurchaseOrderAttachmentFileName(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return SafeFileName(reference.Trim()) + ".pdf";
    }

    /// <summary>The file name a receipt carries in Xero: its own name (§3), with characters a file name cannot hold replaced by <c>-</c>; <c>receipt</c> when blank.</summary>
    /// <param name="fileName">The receipt's own file name.</param>
    public static string ReceiptFileName(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? "receipt" : SafeFileName(Path.GetFileName(fileName.Trim()));

    /// <summary>
    /// The wire body for <paramref name="order"/> (§3): number, project code as
    /// reference, the supplier by <c>ContactID</c>, issue and delivery dates,
    /// lines with the input tax type for their VAT rate and the account code
    /// of <see cref="PurchaseOrderLineCategory"/>. <paramref name="blockedReason"/>
    /// is set (and the body <see langword="null"/>) when a line's tax type or
    /// the account is Blocked (§6.8: never sent half-formed).
    /// </summary>
    /// <param name="order">The purchase order.</param>
    /// <param name="contact">The supplier's linked contact.</param>
    /// <param name="taxTypeFor">The X1 tax-type resolution for a VAT rate on a purchase line.</param>
    /// <param name="account">The X1 account resolution for <see cref="PurchaseOrderLineCategory"/>.</param>
    /// <param name="blockedReason">Why the body cannot be built.</param>
    public static XeroWirePurchaseOrderWrite? BuildPurchaseOrder(
        XeroPurchaseOrderSnapshot order, XeroWireContactRef contact, Func<VatRate, XeroCodeResolution> taxTypeFor, XeroCodeResolution account,
        out string? blockedReason)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(taxTypeFor);
        ArgumentNullException.ThrowIfNull(account);

        if (order.IssuedDate is not { } issued)
        {
            blockedReason = $"Purchase order {order.Reference} has not been issued; only an issued order goes to Xero.";
            return null;
        }

        if (order.Lines.Count == 0)
        {
            blockedReason = $"Purchase order {order.Reference} has no lines; Xero needs at least one.";
            return null;
        }

        if (order.Reference.Trim().Length > XeroAccountingApi.MaximumPurchaseOrderNumberLength)
        {
            blockedReason = $"Purchase order reference {order.Reference} is longer than Xero's {XeroAccountingApi.MaximumPurchaseOrderNumberLength} characters.";
            return null;
        }

        if (account.IsBlocked)
        {
            blockedReason = account.BlockedReason;
            return null;
        }

        var lines = new List<XeroWireLineItem>(order.Lines.Count);
        foreach (var line in order.Lines)
        {
            var tax = taxTypeFor(line.VatRate);
            if (tax.IsBlocked)
            {
                blockedReason = tax.BlockedReason;
                return null;
            }

            lines.Add(new XeroWireLineItem(
                string.IsNullOrWhiteSpace(line.Description) ? "(no description)" : line.Description,
                line.Quantity, line.UnitPrice, account.Code, tax.Code));
        }

        blockedReason = null;
        return new XeroWirePurchaseOrderWrite(
            PurchaseOrderNumber: order.Reference.Trim(),
            Reference: PurchaseOrderReference(order),
            Contact: contact,
            Date: XeroWire.FormatDate(issued),
            DeliveryDate: order.ExpectedDelivery is { } delivery ? XeroWire.FormatDate(delivery) : null,
            CurrencyCode: order.CurrencyCode,
            LineAmountTypes: XeroWire.LineAmountTypesExclusive,
            LineItems: lines);
    }

    /// <summary>
    /// The wire body for <paramref name="expense"/>'s draft bill (§3): the bill
    /// number (Q4), the contact by <c>ContactID</c> (Q3), the expense date, and
    /// one line — the description (with the project code), quantity 1, the
    /// net amount, the category's account code (X1), the input tax type and
    /// the recorded VAT as <c>TaxAmount</c>. <paramref name="blockedReason"/>
    /// is set (and the body <see langword="null"/>) when the tax type or the
    /// account is Blocked, or the number is longer than Xero holds.
    /// </summary>
    /// <param name="expense">The expense.</param>
    /// <param name="contact">The supplier's (or the "General expenses") linked contact.</param>
    /// <param name="taxType">The X1 tax-type resolution for <see cref="InferVatRate"/>'s rate on a purchase line.</param>
    /// <param name="account">The X1 account resolution for the expense's category.</param>
    /// <param name="blockedReason">Why the body cannot be built.</param>
    public static XeroWireBillWrite? BuildBill(
        XeroExpenseSnapshot expense, XeroWireContactRef contact, XeroCodeResolution taxType, XeroCodeResolution account, out string? blockedReason)
    {
        ArgumentNullException.ThrowIfNull(expense);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(taxType);
        ArgumentNullException.ThrowIfNull(account);

        var number = BillNumber(expense);
        if (number.Length > XeroAccountingApi.MaximumBillNumberLength)
        {
            blockedReason = $"The supplier invoice number '{number}' is longer than Xero's {XeroAccountingApi.MaximumBillNumberLength} characters; shorten it on the expense.";
            return null;
        }

        if (account.IsBlocked)
        {
            blockedReason = account.BlockedReason;
            return null;
        }

        if (taxType.IsBlocked)
        {
            blockedReason = taxType.BlockedReason;
            return null;
        }

        var description = string.IsNullOrWhiteSpace(expense.Description) ? "(no description)" : expense.Description.Trim();
        if (!string.IsNullOrWhiteSpace(expense.ProjectCode))
            description = $"{expense.ProjectCode} · {description}";

        blockedReason = null;
        return new XeroWireBillWrite(
            InvoiceNumber: number,
            Contact: contact,
            Date: XeroWire.FormatDate(expense.Date),
            CurrencyCode: expense.CurrencyCode,
            LineAmountTypes: XeroWire.LineAmountTypesExclusive,
            LineItems:
            [
                new XeroWireLineItem(
                    Truncate(description, XeroAccountingApi.MaximumBillLineDescriptionLength)!,
                    1m, expense.NetAmount, account.Code, taxType.Code, TaxAmount: expense.VatAmount),
            ]);
    }

    /// <summary>Xero's status word, trimmed and upper-cased; <see langword="null"/> when absent.</summary>
    /// <param name="status">The status as Xero sent it.</param>
    public static string? Word(string? status) => string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();

    /// <summary>Maps a failed call to the push outcome the engine acts on (§6.3, §6.6).</summary>
    /// <typeparam name="T">The call's answer type.</typeparam>
    /// <param name="result">A result whose outcome is not Ok.</param>
    public static XeroPushResult Failed<T>(XeroApiResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome switch
        {
            ConnectorOutcome.Rejected => new XeroPushResult(XeroPushOutcome.Rejected, result.Reason),
            ConnectorOutcome.Reauthorise => new XeroPushResult(XeroPushOutcome.Reauthorise, result.Reason),
            ConnectorOutcome.Unavailable => new XeroPushResult(XeroPushOutcome.RetryLater, result.Reason, result.RetryAfter),
            _ => new XeroPushResult(XeroPushOutcome.Unknown, result.Reason),
        };
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
            builder.Append(invalid.Contains(c) || c is '/' or '\\' or '?' or '#' or '%' || char.IsControl(c) ? '-' : c);
        return builder.Length == 0 ? "file" : builder.ToString();
    }

    /// <summary>The <c>Reference</c> a purchase order is written to Xero with — its project's code — and so the mark by which an existing Xero order is known as TempestOS's own (§6.4 item 4).</summary>
    public static string? PurchaseOrderReference(XeroPurchaseOrderSnapshot order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Truncate(order.ProjectCode, XeroAccountingApi.MaximumPurchaseOrderReferenceLength);
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];

    private static string Sha256Hex(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

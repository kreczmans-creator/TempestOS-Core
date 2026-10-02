using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

// State inspection and the back-office acts a person performs in Xero by
// hand (design §10.1): never reachable through the HTTP surface, never
// recorded as requests, never violations. A back-office act Xero itself
// would not allow throws InvalidOperationException — a test set-up error.
internal sealed partial class XeroApiSimulator
{
    /// <summary>Adds an existing contact, as if entered in Xero by hand; returns its <c>ContactID</c>.</summary>
    public string SeedContact(string name, string? taxNumber = null, string? contactNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_sync)
        {
            if (ActiveContactNamed(name, null) is not null)
                throw new InvalidOperationException($"Xero already has an active contact named '{name}'.");
            return CreateContact(name.Trim(), taxNumber, contactNumber).Id;
        }
    }

    /// <summary>The document <paramref name="id"/> in <paramref name="resource"/>, or <see langword="null"/>.</summary>
    /// <remarks>Deleted documents (contacts included) are found here, with status <c>DELETED</c>, though the HTTP surface hides a deleted contact.</remarks>
    public XeroSimulatedDocument? Find(string resource, string id)
    {
        var kind = KindOf(resource);
        lock (_sync)
            return FindById(kind, id)?.Snapshot();
    }

    /// <summary>Every document in <paramref name="resource"/>.</summary>
    public IReadOnlyList<XeroSimulatedDocument> All(string resource)
    {
        var kind = KindOf(resource);
        lock (_sync)
            return [.. _documents[kind].Select(d => d.Snapshot())];
    }

    /// <summary>The Product Owner approves the invoice or bill in Xero (<c>DRAFT</c> → <c>AUTHORISED</c>) — a back-office act, never reachable through the HTTP surface by TempestOS.</summary>
    /// <remarks>Also approves a purchase order (<c>DRAFT</c>/<c>SUBMITTED</c> → <c>AUTHORISED</c>) when <paramref name="invoiceId"/> names one.</remarks>
    public void ApproveInXero(string invoiceId)
    {
        lock (_sync)
        {
            var doc = FindById(InvoicesKind, invoiceId) ?? FindById(PurchaseOrdersKind, invoiceId)
                ?? throw new InvalidOperationException($"No invoice, bill or purchase order {invoiceId}.");
            Require(doc, "DRAFT", "SUBMITTED");
            SetStatus(doc, "AUTHORISED");
        }
    }

    /// <summary>The invoice or bill is paid in full in Xero (<c>AUTHORISED</c> → <c>PAID</c>, <c>FullyPaidOnDate</c> set).</summary>
    public void PayInXero(string invoiceId, DateOnly paidOn)
    {
        lock (_sync)
        {
            var doc = Existing(InvoicesKind, invoiceId);
            Require(doc, "AUTHORISED");
            XeroWire.TryNumber(doc.Body["Total"], out var total);
            doc.Body["AmountPaid"] = total;
            doc.Body["AmountDue"] = 0m;
            SetDate(doc.Body, "FullyPaidOnDate", paidOn);
            SetStatus(doc, "PAID");
        }
    }

    /// <summary>The invoice is voided in Xero (<c>AUTHORISED</c> → <c>VOIDED</c>).</summary>
    public void VoidInXero(string invoiceId)
    {
        lock (_sync)
        {
            var doc = Existing(InvoicesKind, invoiceId);
            Require(doc, "AUTHORISED");
            SetStatus(doc, "VOIDED");
        }
    }

    /// <summary>The document is deleted in Xero by hand (status <c>DELETED</c>; a later GET by id answers 404 for a contact, the record with <c>DELETED</c> otherwise).</summary>
    public void DeleteInXero(string resource, string id)
    {
        var kind = KindOf(resource);
        lock (_sync)
        {
            var doc = Existing(kind, id);
            if (kind == InvoicesKind)
                Require(doc, "DRAFT", "SUBMITTED");
            else if (kind == PurchaseOrdersKind)
                Require(doc, "DRAFT", "SUBMITTED", "AUTHORISED");
            else if (doc.Status == "DELETED")
                throw new InvalidOperationException($"{resource} {id} is already DELETED.");
            SetStatus(doc, "DELETED");
        }
    }

    /// <summary>The quote is turned into an invoice in Xero by hand (quote <c>ACCEPTED</c> → <c>INVOICED</c>; a new <c>ACCREC</c> draft exists) — the double-invoicing risk X3 must surface.</summary>
    /// <returns>The new invoice's <c>InvoiceID</c>.</returns>
    public string ConvertQuoteToInvoiceInXero(string quoteId)
    {
        lock (_sync)
        {
            var quote = Existing(QuotesKind, quoteId);
            Require(quote, "ACCEPTED");
            var invoice = CopyToNew(quote, InvoicesKind, "ACCREC", reference: quote.Number);
            SetStatus(quote, "INVOICED");
            return invoice.Id;
        }
    }

    /// <summary>The purchase order is turned into a bill in Xero by hand ("Copy to bill": PO <c>AUTHORISED</c> → <c>BILLED</c>; a new <c>ACCPAY</c> draft exists, Q6).</summary>
    /// <returns>The new bill's <c>InvoiceID</c>.</returns>
    public string BillPurchaseOrderInXero(string purchaseOrderId)
    {
        lock (_sync)
        {
            var order = Existing(PurchaseOrdersKind, purchaseOrderId);
            Require(order, "AUTHORISED");
            var bill = CopyToNew(order, InvoicesKind, "ACCPAY", reference: order.Number);
            SetStatus(order, "BILLED");
            return bill.Id;
        }
    }

    /// <summary>The contact is archived in Xero by hand (<c>ACTIVE</c> → <c>ARCHIVED</c>): hidden from lists unless <c>includeArchived=true</c>, refused on a new document.</summary>
    public void ArchiveContactInXero(string contactId)
    {
        lock (_sync)
        {
            var doc = Existing(ContactsKind, contactId);
            Require(doc, "ACTIVE");
            SetStatus(doc, "ARCHIVED");
        }
    }

    /// <summary>The account is archived in Xero by hand: lines may no longer use it, and <c>GET Accounts</c> with <c>If-Modified-Since</c> returns it.</summary>
    public void ArchiveAccountInXero(string code)
    {
        lock (_sync)
        {
            var index = _accounts.FindIndex(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                throw new InvalidOperationException($"No account {code}.");
            _accounts[index] = _accounts[index] with { Status = "ARCHIVED", UpdatedUtc = Time.GetUtcNow() };
        }
    }

    private static DocumentKind KindOf(string resource) =>
        Kinds.TryGetValue(resource, out var kind) ? kind : throw new ArgumentException($"'{resource}' is not Contacts, Quotes, Invoices or PurchaseOrders.", nameof(resource));

    private StoredDocument Existing(DocumentKind kind, string id) =>
        FindById(kind, id) ?? throw new InvalidOperationException($"No {kind.Resource} {id}.");

    private static void Require(StoredDocument doc, params string[] statuses)
    {
        if (!statuses.Contains(doc.Status))
            throw new InvalidOperationException($"{doc.Kind.Resource} {doc.Id} is {doc.Status}; Xero allows this only from {string.Join(" or ", statuses)}.");
    }

    private void SetStatus(StoredDocument doc, string status)
    {
        doc.Body[doc.Kind.StatusField] = status;
        Stamp(doc);
    }

    private StoredDocument CopyToNew(StoredDocument source, DocumentKind kind, string type, string? reference)
    {
        var lines = new JsonArray();
        foreach (var line in (source.Body["LineItems"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var copy = line.DeepClone().AsObject();
            copy.Remove("LineItemID");
            lines.Add(copy);
        }

        var element = new JsonObject
        {
            ["Type"] = type,
            ["Contact"] = new JsonObject { ["ContactID"] = XeroWire.TextAt(source.Body, "Contact.ContactID") },
            ["LineItems"] = lines,
            ["LineAmountTypes"] = XeroWire.Str(source.Body, "LineAmountTypes"),
            ["CurrencyCode"] = XeroWire.Str(source.Body, "CurrencyCode"),
            ["Reference"] = reference,
        };
        return Apply(kind, element, null);
    }
}

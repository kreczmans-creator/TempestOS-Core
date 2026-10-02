using System.Globalization;
using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

// Document storage, required-field and status-rule validation, and the
// write endpoints for Contacts, Quotes, Invoices (ACCREC/ACCPAY) and
// PurchaseOrders (design §3, §4, §10.1).
internal sealed partial class XeroApiSimulator
{
    private static readonly DocumentKind ContactsKind = new("Contacts", "ContactID", "ContactNumber", "ContactStatus", NumberInPath: true, NumberPrefix: null);
    private static readonly DocumentKind QuotesKind = new("Quotes", "QuoteID", "QuoteNumber", "Status", NumberInPath: false, NumberPrefix: "QU-");
    private static readonly DocumentKind InvoicesKind = new("Invoices", "InvoiceID", "InvoiceNumber", "Status", NumberInPath: true, NumberPrefix: "INV-");
    private static readonly DocumentKind PurchaseOrdersKind = new("PurchaseOrders", "PurchaseOrderID", "PurchaseOrderNumber", "Status", NumberInPath: true, NumberPrefix: "PO-");

    private static readonly Dictionary<string, DocumentKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [ContactsKind.Resource] = ContactsKind,
        [QuotesKind.Resource] = QuotesKind,
        [InvoicesKind.Resource] = InvoicesKind,
        [PurchaseOrdersKind.Resource] = PurchaseOrdersKind,
    };

    /// <summary>Xero's quote transitions (design §4.1), minus <c>INVOICED</c> as a target: that is reached only by invoicing in Xero.</summary>
    private static readonly Dictionary<string, string[]> QuoteTransitions = new(StringComparer.Ordinal)
    {
        ["DRAFT"] = ["SENT", "DELETED"],
        ["SENT"] = ["ACCEPTED", "DECLINED", "DELETED"],
        ["ACCEPTED"] = ["SENT", "DELETED"],
        ["DECLINED"] = ["SENT", "DELETED"],
        ["INVOICED"] = ["SENT", "DELETED"],
        ["DELETED"] = [],
    };

    /// <summary>The invoice/bill status changes Xero accepts through the API (design §10.1); <c>PAID</c> is never set by a write.</summary>
    private static readonly Dictionary<string, string[]> InvoiceTransitions = new(StringComparer.Ordinal)
    {
        ["DRAFT"] = ["SUBMITTED", "AUTHORISED", "DELETED"],
        ["SUBMITTED"] = ["DRAFT", "AUTHORISED", "DELETED"],
        ["AUTHORISED"] = ["VOIDED"],
        ["PAID"] = [],
        ["VOIDED"] = [],
        ["DELETED"] = [],
    };

    /// <summary>The purchase order status changes Xero accepts through the API; <c>DELETED</c> is refused once <c>BILLED</c>.</summary>
    private static readonly Dictionary<string, string[]> PurchaseOrderTransitions = new(StringComparer.Ordinal)
    {
        ["DRAFT"] = ["SUBMITTED", "AUTHORISED", "DELETED"],
        ["SUBMITTED"] = ["DRAFT", "AUTHORISED", "DELETED"],
        ["AUTHORISED"] = ["BILLED", "DELETED"],
        ["BILLED"] = [],
        ["DELETED"] = [],
    };

    private static readonly string[] DateFields = ["Date", "DueDate", "ExpiryDate", "DeliveryDate", "FullyPaidOnDate"];

    private static readonly HashSet<string> ManagedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Contact", "LineItems", "UpdatedDateUTC", "HasAttachments", "Attachments", "SubTotal", "TotalTax", "Total", "AmountDue", "AmountPaid",
        "AmountCredited", "IsCustomer", "IsSupplier", "HasErrors", "ValidationErrors", "StatusAttributeString", "Warnings",
        "Date", "DateString", "DueDate", "DueDateString", "ExpiryDate", "ExpiryDateString", "DeliveryDate", "DeliveryDateString",
        "FullyPaidOnDate", "FullyPaidOnDateString",
    };

    private readonly Dictionary<DocumentKind, List<StoredDocument>> _documents = new()
    {
        [ContactsKind] = [],
        [QuotesKind] = [],
        [InvoicesKind] = [],
        [PurchaseOrdersKind] = [],
    };

    private readonly Dictionary<DocumentKind, int> _numberCounters = [];
    private readonly List<SimulatedTaxRate> _taxRates;
    private readonly List<SimulatedAccount> _accounts;
    private long _idCounter;

    private enum LedgerSide
    {
        Sales,
        Purchases,
    }

    private sealed record DocumentKind(string Resource, string IdField, string NumberField, string StatusField, bool NumberInPath, string? NumberPrefix);

    private sealed record StoredAttachment(string AttachmentId, string FileName, byte[] Content, string MimeType, bool IncludeOnline);

    private sealed class StoredDocument(DocumentKind kind, string id, long sequence)
    {
        public DocumentKind Kind { get; } = kind;

        public string Id { get; } = id;

        public long Sequence { get; } = sequence;

        public JsonObject Body { get; } = XeroWire.NewObject();

        public List<StoredAttachment> Attachments { get; } = [];

        public DateTimeOffset Updated { get; set; }

        public string Status => XeroWire.Str(Body, Kind.StatusField) ?? string.Empty;

        public string? Number => XeroWire.Str(Body, Kind.NumberField);

        public XeroSimulatedDocument Snapshot() => new(
            Id, Kind.Resource, Number, Status, Body.DeepClone().AsObject(), [.. Attachments.Select(a => (a.FileName, a.Content.LongLength, a.IncludeOnline))]);
    }

    private sealed class ElementCheck
    {
        public List<(string Rule, string Message)> Errors { get; } = [];

        public bool Ok => Errors.Count == 0;

        public void Fail(string rule, string message) => Errors.Add((rule, message));
    }

    private string NextId(string kind) => SimulatorSeed.DeterministicId(kind, (++_idCounter).ToString(CultureInfo.InvariantCulture));

    private StoredDocument? FindById(DocumentKind kind, string id) =>
        _documents[kind].FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The document a path names: by id, or — where Xero allows it — by number (a live record preferred to a deleted one). A deleted contact is never found.</summary>
    private StoredDocument? FindByKey(DocumentKind kind, string key)
    {
        var doc = FindById(kind, key);
        if (doc is null && kind.NumberInPath)
        {
            var matches = _documents[kind].Where(d => string.Equals(d.Number, key, StringComparison.OrdinalIgnoreCase)).ToList();
            doc = matches.FirstOrDefault(d => d.Status != "DELETED") ?? matches.FirstOrDefault();
        }

        return kind == ContactsKind && doc?.Status == "DELETED" ? null : doc;
    }

    private SimResponse Write(DocumentKind kind, RequestContext context, string? pathKey)
    {
        var elements = context.BodyMalformed ? null : ExtractElements(kind, context.Json);
        if (elements is null || elements.Count == 0)
        {
            context.Violate(XeroSimulatorRules.MalformedBody, $"{context.Method} {context.Path}: the body is missing, not JSON, or carries no {kind.Resource}.");
            return SimResponse.ValidationException($"The request body must be a {kind.Resource} object or a {{\"{kind.Resource}\": [...]}} envelope.");
        }

        StoredDocument? pathDoc = null;
        if (pathKey is not null)
        {
            pathDoc = FindByKey(kind, pathKey);
            if (pathDoc is null)
                return SimResponse.NotFound();
            if (elements.Count != 1)
            {
                context.Violate(XeroSimulatorRules.MalformedBody, $"{context.Method} {context.Path} carries {elements.Count} elements; an update by id takes one.");
                return SimResponse.ValidationException("An update of one document must carry exactly one element.");
            }
        }

        var plans = new List<(JsonObject Element, StoredDocument? Existing, ElementCheck Check)>();
        foreach (var element in elements)
        {
            var check = new ElementCheck();
            var existing = pathDoc;
            if (existing is null && XeroWire.Str(element, kind.IdField) is { } bodyId)
            {
                existing = FindById(kind, bodyId);
                if (existing is null || (kind == ContactsKind && existing.Status == "DELETED"))
                    check.Fail(XeroSimulatorRules.RequiredField, $"{kind.IdField} {bodyId} does not exist.");
                else if (context.Method == HttpMethod.Put)
                    check.Fail(XeroSimulatorRules.Method, $"PUT creates; use POST to update {kind.Resource} {bodyId}.");
            }

            if (check.Ok)
            {
                if (existing is null)
                    ValidateCreate(kind, element, check, context);
                else
                    ValidateUpdate(kind, existing, element, check, context);
            }

            plans.Add((element, existing, check));
        }

        foreach (var (_, _, check) in plans)
        {
            foreach (var (rule, message) in check.Errors)
                context.Violate(rule, $"{context.Method} {context.Path}: {message}");
        }

        var summarize = !string.Equals(context.QueryValue("summarizeErrors"), "false", StringComparison.OrdinalIgnoreCase);
        if (summarize)
        {
            var failed = plans.Where(p => !p.Check.Ok).ToList();
            if (failed.Count > 0)
            {
                return SimResponse.ValidationException(
                    "A validation exception occurred",
                    new JsonArray([.. failed.Select(p => (JsonNode)ErrorElement(p.Element, p.Check))]));
            }

            var saved = plans.Select(p => Apply(kind, p.Element, p.Existing).Body.DeepClone()).ToList();
            return SimResponse.Ok(Envelope(kind.Resource, saved));
        }

        var results = new List<JsonNode>();
        foreach (var (element, existing, check) in plans)
        {
            if (!check.Ok)
            {
                results.Add(ErrorElement(element, check));
                continue;
            }

            var body = Apply(kind, element, existing).Body.DeepClone().AsObject();
            body["StatusAttributeString"] = "OK";
            results.Add(body);
        }

        return SimResponse.Ok(Envelope(kind.Resource, results));
    }

    private static List<JsonObject>? ExtractElements(DocumentKind kind, JsonNode? json)
    {
        if (json is not JsonObject root)
            return null;

        if (root.TryGetPropertyValue(kind.Resource, out var list))
            return list is JsonArray array && array.All(e => e is JsonObject) ? [.. array.Select(e => e!.AsObject())] : null;

        return [root];
    }

    private static JsonObject ErrorElement(JsonObject element, ElementCheck check)
    {
        var echo = element.DeepClone().AsObject();
        echo["HasErrors"] = true;
        echo["StatusAttributeString"] = "ERROR";
        echo["ValidationErrors"] = new JsonArray([.. check.Errors.Select(e => (JsonNode)new JsonObject { ["Message"] = e.Message })]);
        return echo;
    }

    // ---------------------------------------------------------------- validation

    private void ValidateCreate(DocumentKind kind, JsonObject element, ElementCheck check, RequestContext context)
    {
        if (kind == ContactsKind)
        {
            ValidateContactFields(element, null, check);
            return;
        }

        ValidateCommonDocumentFields(element, check);
        var status = Upper(XeroWire.Str(element, "Status")) ?? "DRAFT";

        if (kind == InvoicesKind)
        {
            var type = Upper(XeroWire.Str(element, "Type"));
            if (type is null)
                check.Fail(XeroSimulatorRules.RequiredField, "Type is required (ACCREC or ACCPAY).");
            else if (type is not ("ACCREC" or "ACCPAY"))
                check.Fail(XeroSimulatorRules.FieldFormat, $"Type '{type}' is not ACCREC or ACCPAY.");

            ValidateContactReference(element, check, context, required: true);
            ValidateLines(element, check, type == "ACCPAY" ? LedgerSide.Purchases : LedgerSide.Sales, required: false);

            if (status is "SUBMITTED" or "AUTHORISED")
                context.Violate(XeroSimulatorRules.InvoiceStatusNotDraft, $"{type} invoice created as {status}; TempestOS creates only DRAFT (D3).");
            else if (status != "DRAFT")
                check.Fail(XeroSimulatorRules.InvoiceTransition, $"An invoice cannot be created as {status}.");

            if (type == "ACCREC" && XeroWire.Str(element, "InvoiceNumber") is { } number)
                ValidateUniqueNumber(InvoicesKind, number, null, d => XeroWire.Str(d.Body, "Type") == "ACCREC", "Invoice # must be unique.", check);
            return;
        }

        if (kind == QuotesKind)
        {
            ValidateContactReference(element, check, context, required: true);
            if (!XeroWire.Has(element, "Date"))
                check.Fail(XeroSimulatorRules.RequiredField, "Date is required.");
            ValidateLines(element, check, LedgerSide.Sales, required: true);

            if (status is not ("DRAFT" or "SENT"))
                check.Fail(XeroSimulatorRules.QuoteTransition, $"A quote cannot be created as {status}.");

            if (XeroWire.Str(element, "QuoteNumber") is { } number)
                ValidateUniqueNumber(QuotesKind, number, null, _ => true, "Quote number must be unique.", check);
            return;
        }

        ValidateContactReference(element, check, context, required: true);
        ValidateLines(element, check, LedgerSide.Purchases, required: true);

        if (status is "SUBMITTED" or "AUTHORISED")
            context.Violate(XeroSimulatorRules.PurchaseOrderStatusNotDraft, $"Purchase order created as {status}; TempestOS creates only DRAFT (D3, Q2).");
        else if (status != "DRAFT")
            check.Fail(XeroSimulatorRules.PurchaseOrderTransition, $"A purchase order cannot be created as {status}.");

        if (XeroWire.Str(element, "PurchaseOrderNumber") is { } poNumber)
            ValidateUniqueNumber(PurchaseOrdersKind, poNumber, null, _ => true, "Purchase order number must be unique.", check);
    }

    private void ValidateUpdate(DocumentKind kind, StoredDocument existing, JsonObject element, ElementCheck check, RequestContext context)
    {
        if (kind == ContactsKind)
        {
            ValidateContactFields(element, existing, check);
            return;
        }

        var current = existing.Status;
        var requested = Upper(XeroWire.Str(element, "Status"));
        var changes = ContentChanges(existing, element);

        if (kind == InvoicesKind)
        {
            if (current is "DELETED" or "VOIDED" or "PAID")
            {
                check.Fail(XeroSimulatorRules.NotEditable, $"This document cannot be edited as it has a status of {current}.");
                return;
            }

            if (Upper(XeroWire.Str(element, "Type")) is { } type && type != XeroWire.Str(existing.Body, "Type"))
                check.Fail(XeroSimulatorRules.NotEditable, $"An invoice's Type cannot change (it is {XeroWire.Str(existing.Body, "Type")}).");

            if (requested is not null && requested != current)
            {
                if (!InvoiceTransitions[current].Contains(requested))
                    check.Fail(XeroSimulatorRules.InvoiceTransition, $"An invoice cannot move from {current} to {requested}.");
                else if (requested is not ("DRAFT" or "DELETED"))
                    context.Violate(XeroSimulatorRules.InvoiceStatusNotDraft, $"Invoice {existing.Id} moved {current} → {requested}; TempestOS writes only DRAFT or DELETED (D3).");
            }

            if (changes.Count > 0 && current is not ("DRAFT" or "SUBMITTED"))
                check.Fail(XeroSimulatorRules.NotEditable, $"The invoice is {current}; its {string.Join(", ", changes)} can only change while DRAFT or SUBMITTED.");

            var side = XeroWire.Str(existing.Body, "Type") == "ACCPAY" ? LedgerSide.Purchases : LedgerSide.Sales;
            ValidateCommonDocumentFields(element, check);
            ValidateContactReference(element, check, context, required: false);
            ValidateLines(element, check, side, required: false);
            if (side == LedgerSide.Sales && changes.Contains("InvoiceNumber") && XeroWire.Str(element, "InvoiceNumber") is { } number)
                ValidateUniqueNumber(InvoicesKind, number, existing, d => XeroWire.Str(d.Body, "Type") == "ACCREC", "Invoice # must be unique.", check);
            return;
        }

        if (kind == QuotesKind)
        {
            if (current == "DELETED")
            {
                check.Fail(XeroSimulatorRules.NotEditable, "This quote cannot be edited as it has a status of DELETED.");
                return;
            }

            if (!XeroWire.Has(element, "Contact") || !XeroWire.Has(element, "Date"))
                check.Fail(XeroSimulatorRules.RequiredField, "A quote update must carry Contact and Date.");

            if (requested is not null && requested != current && !QuoteTransitions[current].Contains(requested))
                check.Fail(XeroSimulatorRules.QuoteTransition, $"A quote cannot move from {current} to {requested}.");

            if (changes.Count > 0 && current != "DRAFT")
                check.Fail(XeroSimulatorRules.NotEditable, $"The quote is {current}; its {string.Join(", ", changes)} can only change while DRAFT.");

            ValidateCommonDocumentFields(element, check);
            ValidateContactReference(element, check, context, required: false);
            ValidateLines(element, check, LedgerSide.Sales, required: false);
            if (changes.Contains("QuoteNumber") && XeroWire.Str(element, "QuoteNumber") is { } number)
                ValidateUniqueNumber(QuotesKind, number, existing, _ => true, "Quote number must be unique.", check);
            return;
        }

        if (current == "DELETED")
        {
            check.Fail(XeroSimulatorRules.NotEditable, "This purchase order cannot be edited as it has a status of DELETED.");
            return;
        }

        if (requested is not null && requested != current)
        {
            if (!PurchaseOrderTransitions[current].Contains(requested))
                check.Fail(XeroSimulatorRules.PurchaseOrderTransition, current == "BILLED" && requested == "DELETED"
                    ? "A purchase order cannot be deleted once it is BILLED."
                    : $"A purchase order cannot move from {current} to {requested}.");
            else if (requested is not ("DRAFT" or "DELETED"))
                context.Violate(XeroSimulatorRules.PurchaseOrderStatusNotDraft, $"Purchase order {existing.Id} moved {current} → {requested}; TempestOS writes only DRAFT or DELETED (D3, Q2).");
        }

        if (changes.Count > 0 && current == "BILLED")
            check.Fail(XeroSimulatorRules.NotEditable, $"The purchase order is BILLED; its {string.Join(", ", changes)} can no longer change.");

        ValidateCommonDocumentFields(element, check);
        ValidateContactReference(element, check, context, required: false);
        ValidateLines(element, check, LedgerSide.Purchases, required: false);
        if (changes.Contains("PurchaseOrderNumber") && XeroWire.Str(element, "PurchaseOrderNumber") is { } poNumber)
            ValidateUniqueNumber(PurchaseOrdersKind, poNumber, existing, _ => true, "Purchase order number must be unique.", check);
    }

    private void ValidateContactFields(JsonObject element, StoredDocument? existing, ElementCheck check)
    {
        var name = XeroWire.Str(element, "Name");
        if (existing is null && string.IsNullOrWhiteSpace(name))
            check.Fail(XeroSimulatorRules.RequiredField, "The contact Name is required.");
        else if (name is not null && string.IsNullOrWhiteSpace(name))
            check.Fail(XeroSimulatorRules.RequiredField, "The contact Name cannot be blank.");

        ValidateLength(element, "Name", 255, check);
        ValidateLength(element, "ContactNumber", 50, check);
        ValidateLength(element, "EmailAddress", 255, check);
        ValidateLength(element, "TaxNumber", 50, check);

        if (!string.IsNullOrWhiteSpace(name) && ActiveContactNamed(name, existing) is not null)
            check.Fail(XeroSimulatorRules.Duplicate, $"The contact name {name.Trim()} is already assigned to another contact. The contact name must be unique across all active contacts.");

        if (Upper(XeroWire.Str(element, "ContactStatus")) is { } status && status is not ("ACTIVE" or "ARCHIVED"))
            check.Fail(XeroSimulatorRules.FieldFormat, $"ContactStatus '{status}' cannot be set through the API.");
    }

    private StoredDocument? ActiveContactNamed(string name, StoredDocument? except) =>
        _documents[ContactsKind].FirstOrDefault(d =>
            d != except && d.Status == "ACTIVE" && string.Equals(XeroWire.Str(d.Body, "Name")?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    private void ValidateCommonDocumentFields(JsonObject element, ElementCheck check)
    {
        foreach (var field in DateFields)
        {
            if (!element.TryGetPropertyValue(field, out var node) || node is null)
                continue;
            if (node is not JsonValue v || !v.TryGetValue<string>(out var text) || !XeroWire.TryParseDate(text, out _))
                check.Fail(XeroSimulatorRules.FieldFormat, $"{field} '{node.ToJsonString()}' is not a date.");
        }

        if (XeroWire.Str(element, "LineAmountTypes") is { } amountTypes && !new[] { "Exclusive", "Inclusive", "NoTax" }.Contains(amountTypes, StringComparer.OrdinalIgnoreCase))
            check.Fail(XeroSimulatorRules.FieldFormat, $"LineAmountTypes '{amountTypes}' is not Exclusive, Inclusive or NoTax.");

        if (XeroWire.Str(element, "CurrencyCode") is { } currency && (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)))
            check.Fail(XeroSimulatorRules.FieldFormat, $"CurrencyCode '{currency}' is not an ISO 4217 code.");

        if (element.TryGetPropertyValue("Status", out var statusNode) && statusNode is not null && XeroWire.Str(element, "Status") is null)
            check.Fail(XeroSimulatorRules.FieldFormat, "Status must be a string.");

        ValidateLength(element, "InvoiceNumber", 255, check);
        ValidateLength(element, "QuoteNumber", 255, check);
        ValidateLength(element, "PurchaseOrderNumber", 255, check);
        ValidateLength(element, "Reference", 255, check);
        ValidateLength(element, "Title", 100, check);
        ValidateLength(element, "Summary", 3000, check);
        ValidateLength(element, "Terms", 4000, check);
    }

    private static void ValidateLength(JsonObject element, string field, int max, ElementCheck check)
    {
        if (XeroWire.Str(element, field) is { } value && value.Length > max)
            check.Fail(XeroSimulatorRules.FieldLength, $"{field} is {value.Length} characters; Xero allows {max}.");
    }

    private void ValidateUniqueNumber(DocumentKind kind, string number, StoredDocument? except, Func<StoredDocument, bool> scope, string message, ElementCheck check)
    {
        if (_documents[kind].Any(d => d != except && d.Status != "DELETED" && scope(d) && string.Equals(d.Number, number, StringComparison.OrdinalIgnoreCase)))
            check.Fail(XeroSimulatorRules.Duplicate, $"{message} ({number})");
    }

    private void ValidateContactReference(JsonObject element, ElementCheck check, RequestContext context, bool required)
    {
        if (!element.TryGetPropertyValue("Contact", out var node) || node is null)
        {
            if (required)
                check.Fail(XeroSimulatorRules.RequiredField, "A Contact must be specified.");
            return;
        }

        if (node is not JsonObject contact)
        {
            check.Fail(XeroSimulatorRules.FieldFormat, "Contact must be an object.");
            return;
        }

        if (XeroWire.Str(contact, "ContactID") is { } id)
        {
            var doc = FindById(ContactsKind, id);
            if (doc is null || doc.Status == "DELETED")
                check.Fail(XeroSimulatorRules.UnknownContact, $"The contact {id} does not exist.");
            else if (doc.Status == "ARCHIVED")
                check.Fail(XeroSimulatorRules.UnknownContact, $"The contact '{XeroWire.Str(doc.Body, "Name")}' is archived and cannot be used.");
            return;
        }

        if (XeroWire.Str(contact, "Name") is { } name && !string.IsNullOrWhiteSpace(name))
        {
            context.Violate(XeroSimulatorRules.ContactByName, $"Contact given by Name '{name}' only; Xero matched or created it silently.");
            return;
        }

        check.Fail(XeroSimulatorRules.RequiredField, "The Contact needs a ContactID.");
    }

    private void ValidateLines(JsonObject element, ElementCheck check, LedgerSide side, bool required)
    {
        if (!element.TryGetPropertyValue("LineItems", out var node) || node is null)
        {
            if (required)
                check.Fail(XeroSimulatorRules.RequiredField, "At least one line item is required.");
            return;
        }

        if (node is not JsonArray lines)
        {
            check.Fail(XeroSimulatorRules.FieldFormat, "LineItems must be an array.");
            return;
        }

        if (lines.Count == 0 && required)
            check.Fail(XeroSimulatorRules.RequiredField, "At least one line item is required.");

        for (var i = 0; i < lines.Count; i++)
        {
            var at = $"Line {i + 1}";
            if (lines[i] is not JsonObject line)
            {
                check.Fail(XeroSimulatorRules.FieldFormat, $"{at} is not an object.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(XeroWire.Str(line, "Description")))
                check.Fail(XeroSimulatorRules.RequiredField, $"{at}: Description is required.");

            foreach (var numeric in new[] { "Quantity", "UnitAmount", "TaxAmount", "LineAmount", "DiscountRate" })
            {
                if (line.TryGetPropertyValue(numeric, out var n) && n is not null && !XeroWire.TryNumber(n, out _))
                    check.Fail(XeroSimulatorRules.FieldFormat, $"{at}: {numeric} is not a number.");
            }

            if (XeroWire.Str(line, "TaxType") is { Length: > 0 } taxType)
                ValidateTaxType(taxType, side, at, check);

            if (XeroWire.Str(line, "AccountCode") is { Length: > 0 } accountCode)
                ValidateAccountCode(accountCode, at, check);
        }
    }

    private void ValidateTaxType(string code, LedgerSide side, string at, ElementCheck check)
    {
        var rate = _taxRates.FirstOrDefault(r => string.Equals(r.TaxType, code, StringComparison.OrdinalIgnoreCase));
        if (rate is null)
            check.Fail(XeroSimulatorRules.TaxType, $"{at}: the TaxType code '{code}' does not exist.");
        else if (rate.Status != "ACTIVE")
            check.Fail(XeroSimulatorRules.TaxType, $"{at}: the TaxType code '{code}' is {rate.Status}.");
        else if (side == LedgerSide.Sales && !rate.CanApplyToRevenue)
            check.Fail(XeroSimulatorRules.TaxType, $"{at}: the TaxType code '{code}' cannot be used on a sales document.");
        else if (side == LedgerSide.Purchases && !rate.CanApplyToExpenses)
            check.Fail(XeroSimulatorRules.TaxType, $"{at}: the TaxType code '{code}' cannot be used on a purchase document.");
    }

    private void ValidateAccountCode(string code, string at, ElementCheck check)
    {
        var account = _accounts.FirstOrDefault(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));
        if (account is null)
            check.Fail(XeroSimulatorRules.AccountCode, $"{at}: Account code '{code}' is not a valid code for this document.");
        else if (account.Status != "ACTIVE")
            check.Fail(XeroSimulatorRules.AccountCode, $"{at}: Account code '{code}' is {account.Status}.");
        else if (account.Type == "BANK")
            check.Fail(XeroSimulatorRules.AccountCode, $"{at}: Account code '{code}' is a bank account and cannot be used on a line item.");
    }

    /// <summary>The content fields <paramref name="element"/> would change on <paramref name="existing"/> — anything but its id, status, <c>Type</c> and <c>SentToContact</c>; <c>LineItems</c> always counts.</summary>
    private static List<string> ContentChanges(StoredDocument existing, JsonObject element)
    {
        var changes = new List<string>();
        foreach (var (name, value) in element)
        {
            if (string.Equals(name, existing.Kind.IdField, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, existing.Kind.StatusField, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Type", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "SentToContact", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(name, "LineItems", StringComparison.OrdinalIgnoreCase))
            {
                changes.Add("LineItems");
                continue;
            }

            if (string.Equals(name, "Contact", StringComparison.OrdinalIgnoreCase))
            {
                var given = value as JsonObject;
                var givenId = XeroWire.Str(given, "ContactID");
                var storedId = XeroWire.TextAt(existing.Body, "Contact.ContactID");
                var same = givenId is not null
                    ? string.Equals(givenId, storedId, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(XeroWire.Str(given, "Name"), XeroWire.TextAt(existing.Body, "Contact.Name"), StringComparison.OrdinalIgnoreCase);
                if (!same)
                    changes.Add("Contact");
                continue;
            }

            if (DateFields.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                var givenOk = XeroWire.TryParseDate((value as JsonValue)?.TryGetValue<string>(out var g) == true ? g : null, out var givenDate);
                var storedOk = XeroWire.TryParseDate(XeroWire.Str(existing.Body, name), out var storedDate);
                if (givenOk != storedOk || givenDate != storedDate)
                    changes.Add(name);
                continue;
            }

            existing.Body.TryGetPropertyValue(name, out var stored);
            if (!JsonEquivalent(value, stored))
                changes.Add(name);
        }

        return changes;
    }

    private static bool JsonEquivalent(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (a is JsonValue && b is JsonValue && XeroWire.TryNumber(a, out var x) && XeroWire.TryNumber(b, out var y) && a.GetValueKind() == b.GetValueKind())
            return x == y;
        return JsonNode.DeepEquals(a, b);
    }

    private static string? Upper(string? value) => value?.Trim().ToUpperInvariant();

    // ---------------------------------------------------------------- apply

    private StoredDocument NewDocument(DocumentKind kind)
    {
        var doc = new StoredDocument(kind, NextId(kind.Resource), _idCounter);
        _documents[kind].Add(doc);
        return doc;
    }

    /// <summary>Applies a validated element: creates the document when <paramref name="existing"/> is <see langword="null"/>, otherwise updates it.</summary>
    private StoredDocument Apply(DocumentKind kind, JsonObject element, StoredDocument? existing)
    {
        var doc = existing ?? NewDocument(kind);
        var body = doc.Body;
        var creating = existing is null;

        foreach (var (name, value) in element.ToList())
        {
            if (ManagedFields.Contains(name) || string.Equals(name, kind.IdField, StringComparison.OrdinalIgnoreCase) || string.Equals(name, kind.StatusField, StringComparison.OrdinalIgnoreCase))
                continue;
            body[name] = value?.DeepClone();
        }

        body[kind.IdField] = doc.Id;
        var requested = Upper(XeroWire.Str(element, kind.StatusField));

        if (kind == ContactsKind)
        {
            body["ContactStatus"] = requested ?? (creating ? "ACTIVE" : doc.Status);
            body["IsCustomer"] ??= false;
            body["IsSupplier"] ??= false;
            Stamp(doc);
            return doc;
        }

        body[kind.StatusField] = requested ?? (creating ? "DRAFT" : doc.Status);

        if (XeroWire.Obj(element, "Contact") is { } contactRef)
            body["Contact"] = ResolveContact(contactRef);

        foreach (var field in DateFields)
        {
            if (XeroWire.Str(element, field) is { } text && XeroWire.TryParseDate(text, out var date))
                SetDate(body, field, date);
        }

        if (creating && !XeroWire.Has(body, "Date"))
            SetDate(body, "Date", DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime));

        if (element.TryGetPropertyValue("LineItems", out var linesNode) && linesNode is JsonArray lines)
            body["LineItems"] = NormaliseLines(lines);

        body["LineAmountTypes"] ??= "Exclusive";
        body["CurrencyCode"] ??= "GBP";
        body["HasAttachments"] ??= false;

        if (creating && XeroWire.Str(body, kind.NumberField) is null && AutoNumbered(kind, body))
            body[kind.NumberField] = NextNumber(kind);

        if (kind == InvoicesKind)
        {
            body["SentToContact"] ??= false;
            body["AmountPaid"] ??= 0m;
        }

        Recompute(body);

        if (kind == InvoicesKind && XeroWire.TextAt(body, "Contact.ContactID") is { } contactId && FindById(ContactsKind, contactId) is { } contact)
        {
            if (XeroWire.Str(body, "Type") == "ACCPAY")
                contact.Body["IsSupplier"] = true;
            else
                contact.Body["IsCustomer"] = true;
        }

        Stamp(doc);
        return doc;
    }

    private static bool AutoNumbered(DocumentKind kind, JsonObject body) =>
        kind.NumberPrefix is not null && (kind != InvoicesKind || XeroWire.Str(body, "Type") == "ACCREC");

    private string NextNumber(DocumentKind kind)
    {
        while (true)
        {
            _numberCounters[kind] = _numberCounters.GetValueOrDefault(kind) + 1;
            var candidate = $"{kind.NumberPrefix}{_numberCounters[kind]:0000}";
            if (!_documents[kind].Any(d => string.Equals(d.Number, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private void Stamp(StoredDocument doc)
    {
        var now = Time.GetUtcNow();
        doc.Updated = now;
        doc.Body["UpdatedDateUTC"] = XeroWire.MsDate(now);
    }

    private static void SetDate(JsonObject body, string field, DateOnly date)
    {
        body[field] = XeroWire.MsDate(date);
        body[field + "String"] = XeroWire.DateString(date);
    }

    /// <summary>A document's contact as Xero answers it; a contact given by name is matched (active, case-insensitive) or created, as Xero does.</summary>
    private JsonObject ResolveContact(JsonObject contactRef)
    {
        var doc = XeroWire.Str(contactRef, "ContactID") is { } id
            ? FindById(ContactsKind, id)!
            : ActiveContactNamed(XeroWire.Str(contactRef, "Name")!, null) ?? CreateContact(XeroWire.Str(contactRef, "Name")!.Trim(), null, null);
        return new JsonObject
        {
            ["ContactID"] = doc.Id,
            ["Name"] = XeroWire.Str(doc.Body, "Name"),
            ["ContactNumber"] = XeroWire.Str(doc.Body, "ContactNumber"),
        };
    }

    private StoredDocument CreateContact(string name, string? taxNumber, string? contactNumber)
    {
        var element = new JsonObject { ["Name"] = name };
        if (taxNumber is not null)
            element["TaxNumber"] = taxNumber;
        if (contactNumber is not null)
            element["ContactNumber"] = contactNumber;
        return Apply(ContactsKind, element, null);
    }

    private JsonArray NormaliseLines(JsonArray lines)
    {
        var result = new JsonArray();
        foreach (var node in lines)
        {
            var line = node!.DeepClone().AsObject();
            if (XeroWire.Str(line, "LineItemID") is null)
                line["LineItemID"] = NextId("line");
            line["Quantity"] = XeroWire.TryNumber(line["Quantity"], out var quantity) ? quantity : 1m;
            line["UnitAmount"] = XeroWire.TryNumber(line["UnitAmount"], out var unit) ? unit : 0m;
            if (line["TaxAmount"] is not null && XeroWire.TryNumber(line["TaxAmount"], out var taxAmount))
                line["TaxAmount"] = taxAmount;
            line["TaxType"] ??= "NONE";
            result.Add(line);
        }

        return result;
    }

    /// <summary>Recomputes line amounts and totals. A line's sent <c>TaxAmount</c> is kept (a bill records the receipt's VAT, design §3); otherwise it is computed from the tax rate.</summary>
    private void Recompute(JsonObject body)
    {
        var mode = XeroWire.Str(body, "LineAmountTypes") ?? "Exclusive";
        decimal subTotal = 0m, totalTax = 0m;
        foreach (var line in (body["LineItems"] as JsonArray ?? []).OfType<JsonObject>())
        {
            XeroWire.TryNumber(line["Quantity"], out var quantity);
            XeroWire.TryNumber(line["UnitAmount"], out var unit);
            var amount = Math.Round(quantity * unit, 2, MidpointRounding.AwayFromZero);
            line["LineAmount"] = amount;

            var rate = _taxRates.FirstOrDefault(r => string.Equals(r.TaxType, XeroWire.Str(line, "TaxType"), StringComparison.OrdinalIgnoreCase))?.EffectiveRate ?? 0m;
            if (!XeroWire.TryNumber(line["TaxAmount"], out var tax))
            {
                tax = mode.ToUpperInvariant() switch
                {
                    "INCLUSIVE" => Math.Round(amount - (amount / (1 + (rate / 100m))), 2, MidpointRounding.AwayFromZero),
                    "NOTAX" => 0m,
                    _ => Math.Round(amount * rate / 100m, 2, MidpointRounding.AwayFromZero),
                };
                line["TaxAmount"] = tax;
            }

            subTotal += string.Equals(mode, "Inclusive", StringComparison.OrdinalIgnoreCase) ? amount - tax : amount;
            totalTax += tax;
        }

        body["SubTotal"] = subTotal;
        body["TotalTax"] = totalTax;
        body["Total"] = subTotal + totalTax;
        if (body.ContainsKey("AmountPaid"))
        {
            XeroWire.TryNumber(body["AmountPaid"], out var paid);
            body["AmountDue"] = subTotal + totalTax - paid;
        }
    }
}

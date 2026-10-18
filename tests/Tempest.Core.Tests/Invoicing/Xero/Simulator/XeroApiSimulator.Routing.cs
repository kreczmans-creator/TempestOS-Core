using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

// Routing, scopes per endpoint (design §2, §10.1) and the read-side
// endpoints: Organisation, TaxRates, Accounts, Reports/BankSummary,
// RepeatingInvoices, document lists and lookups, attachments.
internal sealed partial class XeroApiSimulator
{
    private static readonly string[] InvoicesRead = [XeroScopes.Invoices, "accounting.invoices.read", "accounting.transactions", "accounting.transactions.read"];
    private static readonly string[] InvoicesWrite = [XeroScopes.Invoices, "accounting.transactions"];
    private static readonly string[] ContactsRead = [XeroScopes.Contacts, "accounting.contacts.read"];
    private static readonly string[] ContactsWrite = [XeroScopes.Contacts];
    private static readonly string[] SettingsRead = ["accounting.settings", XeroScopes.SettingsRead];
    private static readonly string[] SettingsWrite = ["accounting.settings"];
    private static readonly string[] AttachmentsRead = [XeroScopes.Attachments, "accounting.attachments.read"];
    private static readonly string[] AttachmentsWrite = [XeroScopes.Attachments];
    private static readonly string[] BankSummaryRead = [XeroScopes.BankSummaryReportRead, "accounting.reports.read"];

    /// <summary>Xero's per-file attachment limit (design §3: up to 25 MB).</summary>
    public const long MaximumAttachmentBytes = 25L * 1024 * 1024;

    /// <summary>Xero's per-document attachment limit (design §3: up to 10).</summary>
    public const int MaximumAttachmentsPerDocument = 10;

    /// <summary>Xero's limit on a line item's <c>Description</c> (4,000 characters; `v0.24.0` review m19).</summary>
    public const int MaximumLineDescriptionLength = 4000;

    private long _responseCounter;

    private sealed record RouteMatch(IReadOnlyList<string> ReadScopes, IReadOnlyList<string> WriteScopes, Func<RequestContext, SimResponse>? Handler, bool AnswersJson = true);

    private RouteMatch? Route(RequestContext context)
    {
        var s = context.Segments;
        var read = context.IsRead;
        var put = context.Method == HttpMethod.Put;
        var post = context.Method == HttpMethod.Post;

        if (s.Count == 0)
            return null;

        if (string.Equals(s[^1], "Email", StringComparison.OrdinalIgnoreCase) && s.Count > 1)
            return new RouteMatch(InvoicesRead, InvoicesWrite, SendEmail);

        // Read-only endpoints: a write to any of them is outside TempestOS's
        // write allow-list (it never changes Xero's settings), so it routes
        // nowhere and the pipeline records safety.write-allow-list.
        var head = s[0];
        if (s.Count == 1 && Is(head, "Organisation"))
            return read ? new RouteMatch(SettingsRead, SettingsWrite, GetOrganisation) : null;
        if (s.Count == 1 && Is(head, "TaxRates"))
            return read ? new RouteMatch(SettingsRead, SettingsWrite, GetTaxRates) : null;
        if (s.Count == 1 && Is(head, "Accounts"))
            return read ? new RouteMatch(SettingsRead, SettingsWrite, GetAccounts) : null;
        if (s.Count == 2 && Is(head, "Reports") && Is(s[1], "BankSummary"))
            return read ? new RouteMatch(BankSummaryRead, BankSummaryRead, GetBankSummary) : null;
        if (s.Count == 1 && Is(head, "RepeatingInvoices"))
            return read ? new RouteMatch(InvoicesRead, InvoicesWrite, GetRepeatingInvoices) : null;

        if (!Kinds.TryGetValue(head, out var kind))
            return null;

        var (readScopes, writeScopes) = kind == ContactsKind ? (ContactsRead, ContactsWrite) : (InvoicesRead, InvoicesWrite);
        switch (s.Count)
        {
            case 1:
                return new RouteMatch(readScopes, writeScopes, read ? c => ListDocuments(kind, c) : put || post ? c => Write(kind, c, null) : null);
            case 2:
                return new RouteMatch(readScopes, writeScopes, read ? c => GetDocument(kind, c, s[1]) : post ? c => Write(kind, c, s[1]) : null);
        }

        if (kind == ContactsKind || !Is(s[2], "Attachments"))
            return null;

        if (s.Count == 3)
            return new RouteMatch(AttachmentsRead, AttachmentsWrite, read ? c => ListAttachments(kind, c, s[1]) : null);
        if (s.Count == 4)
        {
            return read
                ? new RouteMatch(AttachmentsRead, AttachmentsWrite, c => GetAttachment(kind, s[1], s[3]), AnswersJson: false)
                : new RouteMatch(AttachmentsRead, AttachmentsWrite, put || post ? c => Upload(kind, c, s[1], s[3]) : null);
        }

        return null;

        static bool Is(string segment, string name) => string.Equals(segment, name, StringComparison.OrdinalIgnoreCase);
    }

    private JsonObject Envelope(string name, IEnumerable<JsonNode> items, JsonObject? pagination = null)
    {
        var envelope = new JsonObject
        {
            ["Id"] = SimulatorSeed.DeterministicId("response", (++_responseCounter).ToString(CultureInfo.InvariantCulture)),
            ["Status"] = "OK",
            ["ProviderName"] = "TempestOS Xero simulator",
            ["DateTimeUTC"] = XeroWire.MsDate(Time.GetUtcNow()),
        };
        if (pagination is not null)
            envelope["pagination"] = pagination;
        envelope[name] = new JsonArray([.. items]);
        return envelope;
    }

    private SimResponse SendEmail(RequestContext context)
    {
        context.Violate(XeroSimulatorRules.EmailEndpoint, $"{context.Method} {context.Path}: TempestOS never emails a client (D4).");
        if (context.IsRead)
            return SimResponse.NotFound();

        var target = context.Segments.Count == 3 && Kinds.TryGetValue(context.Segments[0], out var kind) ? FindByKey(kind, context.Segments[1]) : null;
        return target is null ? SimResponse.NotFound() : new SimResponse(HttpStatusCode.NoContent, null, "text/plain");
    }

    private SimResponse GetOrganisation(RequestContext context) =>
        SimResponse.Ok(Envelope("Organisations", [SimulatorSeed.UkDemoOrganisation(Options, SimulatorSeed.SeededAtUtc)]));

    private SimResponse GetTaxRates(RequestContext context)
    {
        IEnumerable<JsonObject> rates = _taxRates.Select(SimulatorSeed.ToJson);
        if (context.QueryValue("TaxType") is { Length: > 0 } taxType)
            rates = rates.Where(r => string.Equals(XeroWire.Str(r, "TaxType"), taxType, StringComparison.OrdinalIgnoreCase));
        return FilterWhere(context, rates, out var filtered) is { } refused ? refused : SimResponse.Ok(Envelope("TaxRates", filtered));
    }

    private SimResponse GetAccounts(RequestContext context)
    {
        var since = context.Request.Headers.IfModifiedSince;
        IEnumerable<JsonObject> accounts = _accounts
            .Where(a => since is null || ModifiedSince(a.UpdatedUtc, since.Value))
            .Select(SimulatorSeed.ToJson);
        return FilterWhere(context, accounts, out var filtered) is { } refused ? refused : SimResponse.Ok(Envelope("Accounts", filtered));
    }

    private SimResponse GetRepeatingInvoices(RequestContext context) =>
        FilterWhere(context, [], out var filtered) is { } refused ? refused : SimResponse.Ok(Envelope("RepeatingInvoices", filtered));

    private SimResponse GetBankSummary(RequestContext context)
    {
        var today = DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime);
        var bank = _accounts.First(a => a.Type == "BANK");
        var report = new JsonObject
        {
            ["ReportID"] = "BankSummary",
            ["ReportName"] = "Bank Summary",
            ["ReportType"] = "BankSummary",
            ["ReportDate"] = today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
            ["UpdatedDateUTC"] = XeroWire.MsDate(Time.GetUtcNow()),
            ["Rows"] = new JsonArray
            {
                new JsonObject
                {
                    ["RowType"] = "Header",
                    ["Cells"] = Cells("Bank Accounts", "Opening Balance", "Cash Received", "Cash Spent", "Closing Balance"),
                },
                new JsonObject
                {
                    ["RowType"] = "Section",
                    ["Title"] = string.Empty,
                    ["Rows"] = new JsonArray
                    {
                        new JsonObject { ["RowType"] = "Row", ["Cells"] = Cells(bank.Name, "0.00", "0.00", "0.00", "0.00") },
                        new JsonObject { ["RowType"] = "SummaryRow", ["Cells"] = Cells("Total", "0.00", "0.00", "0.00", "0.00") },
                    },
                },
            },
        };
        return SimResponse.Ok(new JsonObject { ["Reports"] = new JsonArray { report } });

        static JsonArray Cells(params string[] values) => new([.. values.Select(v => (JsonNode)new JsonObject { ["Value"] = v })]);
    }

    private static SimResponse? FilterWhere(RequestContext context, IEnumerable<JsonObject> items, out List<JsonObject> filtered)
    {
        filtered = [.. items];
        if (context.QueryValue("where") is not { Length: > 0 } where)
            return null;

        var predicate = XeroWire.CompileWhere(where, out var error);
        if (predicate is null)
        {
            context.Violate(XeroSimulatorRules.Where, $"where={where}: {error}.");
            return SimResponse.ValidationException($"The where clause could not be parsed: {error}.");
        }

        filtered = [.. filtered.Where(predicate)];
        return null;
    }

    /// <summary><c>If-Modified-Since</c> is second-precision: a record counts as modified when its update time, truncated to the second, is at or after the header's.</summary>
    private static bool ModifiedSince(DateTimeOffset updated, DateTimeOffset since) =>
        updated.AddTicks(-(updated.Ticks % TimeSpan.TicksPerSecond)) >= since;

    private SimResponse ListDocuments(DocumentKind kind, RequestContext context)
    {
        IEnumerable<StoredDocument> docs = _documents[kind];
        if (kind == ContactsKind)
        {
            var includeArchived = string.Equals(context.QueryValue("includeArchived"), "true", StringComparison.OrdinalIgnoreCase);
            docs = docs.Where(d => d.Status != "DELETED" && (includeArchived || d.Status != "ARCHIVED"));
        }

        if (Csv(context.QueryValue("IDs")) is { } ids)
            docs = docs.Where(d => ids.Contains(d.Id));

        if (kind == InvoicesKind)
        {
            if (Csv(context.QueryValue("InvoiceNumbers")) is { } numbers)
                docs = docs.Where(d => d.Number is not null && numbers.Contains(d.Number));
            if (Csv(context.QueryValue("ContactIDs")) is { } contactIds)
                docs = docs.Where(d => XeroWire.TextAt(d.Body, "Contact.ContactID") is { } c && contactIds.Contains(c));
            if (Csv(context.QueryValue("Statuses")) is { } statuses)
                docs = docs.Where(d => statuses.Contains(d.Status));
        }
        else if (kind == QuotesKind)
        {
            if (context.QueryValue("QuoteNumber") is { Length: > 0 } number)
                docs = docs.Where(d => string.Equals(d.Number, number, StringComparison.OrdinalIgnoreCase));
            if (context.QueryValue("ContactID") is { Length: > 0 } contactId)
                docs = docs.Where(d => string.Equals(XeroWire.TextAt(d.Body, "Contact.ContactID"), contactId, StringComparison.OrdinalIgnoreCase));
            if (context.QueryValue("Status") is { Length: > 0 } status)
                docs = docs.Where(d => string.Equals(d.Status, status, StringComparison.OrdinalIgnoreCase));
        }
        else if (kind == PurchaseOrdersKind)
        {
            if (context.QueryValue("Status") is { Length: > 0 } status)
                docs = docs.Where(d => string.Equals(d.Status, status, StringComparison.OrdinalIgnoreCase));
        }
        else if (context.QueryValue("searchTerm") is { Length: > 0 } term)
        {
            string[] searched = ["Name", "FirstName", "LastName", "ContactNumber", "EmailAddress"];
            docs = docs.Where(d => searched.Any(f => XeroWire.Str(d.Body, f) is { } v && v.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (context.Request.Headers.IfModifiedSince is { } since)
            docs = docs.Where(d => ModifiedSince(d.Updated, since));

        if (FilterWhere(context, docs.Select(d => d.Body), out var bodies) is { } refused)
            return refused;

        JsonObject? pagination = null;
        if (context.QueryValue("page") is { } pageText)
        {
            var page = int.TryParse(pageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : 1;
            var pageSize = int.TryParse(context.QueryValue("pageSize"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ps) && ps > 0 ? Math.Min(ps, 1000) : 100;
            pagination = new JsonObject
            {
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["pageCount"] = (bodies.Count + pageSize - 1) / pageSize,
                ["itemCount"] = bodies.Count,
            };
            bodies = [.. bodies.Skip((page - 1) * pageSize).Take(pageSize)];
        }

        var unitDp = UnitDecimalPlaces(context);
        return SimResponse.Ok(Envelope(kind.Resource, bodies.Select(b => Present(b.DeepClone(), unitDp)), pagination));

        static HashSet<string>? Csv(string? value) => string.IsNullOrWhiteSpace(value)
            ? null
            : new HashSet<string>(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
    }

    private SimResponse GetDocument(DocumentKind kind, RequestContext context, string key)
    {
        var doc = FindByKey(kind, key);
        return doc is null ? SimResponse.NotFound() : SimResponse.Ok(Envelope(kind.Resource, [Present(doc.Body.DeepClone(), UnitDecimalPlaces(context))]));
    }

    private SimResponse ListAttachments(DocumentKind kind, RequestContext context, string key)
    {
        var doc = FindByKey(kind, key);
        return doc is null ? SimResponse.NotFound() : SimResponse.Ok(new JsonObject { ["Attachments"] = new JsonArray([.. doc.Attachments.Select(a => (JsonNode)AttachmentJson(kind, doc, a))]) });
    }

    private SimResponse GetAttachment(DocumentKind kind, string key, string fileNameOrId)
    {
        var attachment = FindByKey(kind, key)?.Attachments.LastOrDefault(a =>
            string.Equals(a.FileName, fileNameOrId, StringComparison.OrdinalIgnoreCase) || string.Equals(a.AttachmentId, fileNameOrId, StringComparison.OrdinalIgnoreCase));
        return attachment is null ? SimResponse.NotFound() : SimResponse.Bytes(attachment.Content, attachment.MimeType);
    }

    private SimResponse Upload(DocumentKind kind, RequestContext context, string key, string fileName)
    {
        var doc = FindByKey(kind, key);
        if (doc is null)
            return SimResponse.NotFound();

        if (context.Json is not null || context.BodyMalformed || context.Binary is not { Length: > 0 } content)
        {
            context.Violate(XeroSimulatorRules.Attachment, $"{context.Method} {context.Path}: the attachment body is empty or not binary.");
            return SimResponse.ValidationException("The attachment must be sent as a non-empty binary body.");
        }

        if (content.LongLength > MaximumAttachmentBytes)
        {
            context.Violate(XeroSimulatorRules.Attachment, $"{fileName} is {content.LongLength} bytes; Xero allows {MaximumAttachmentBytes}.");
            return SimResponse.Text(HttpStatusCode.RequestEntityTooLarge, "The attachment is larger than Xero allows.");
        }

        var includeOnline = kind == InvoicesKind && string.Equals(context.QueryValue("IncludeOnline"), "true", StringComparison.OrdinalIgnoreCase);
        if (context.Method == HttpMethod.Post)
            doc.Attachments.RemoveAll(a => string.Equals(a.FileName, fileName, StringComparison.OrdinalIgnoreCase));

        if (doc.Attachments.Count >= MaximumAttachmentsPerDocument)
        {
            context.Violate(XeroSimulatorRules.Attachment, $"{kind.Resource} {doc.Id} already has {doc.Attachments.Count} attachments; Xero allows {MaximumAttachmentsPerDocument}.");
            return SimResponse.ValidationException($"A document can have at most {MaximumAttachmentsPerDocument} attachments.");
        }

        var mimeType = context.ContentType ?? (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream");
        var attachment = new StoredAttachment(NextId("attachment"), fileName, content, mimeType, includeOnline);
        doc.Attachments.Add(attachment);
        doc.Body["HasAttachments"] = true;
        return SimResponse.Ok(new JsonObject { ["Attachments"] = new JsonArray { AttachmentJson(kind, doc, attachment) } });
    }

    private static JsonObject AttachmentJson(DocumentKind kind, StoredDocument doc, StoredAttachment attachment) => new()
    {
        ["AttachmentID"] = attachment.AttachmentId,
        ["FileName"] = attachment.FileName,
        ["Url"] = new Uri(BaseAddress, $"{kind.Resource}/{doc.Id}/Attachments/{Uri.EscapeDataString(attachment.FileName)}").ToString(),
        ["MimeType"] = attachment.MimeType,
        ["ContentLength"] = attachment.Content.LongLength,
        ["IncludeOnline"] = attachment.IncludeOnline,
    };
}

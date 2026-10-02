using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>What one simulated call answered, read fully so the response can be disposed.</summary>
internal sealed record SimulatorReply(HttpStatusCode Status, JsonNode? Json, string Text, IReadOnlyDictionary<string, string> Headers, TimeSpan? RetryAfter)
{
    /// <summary>The header <paramref name="name"/>, or <see langword="null"/>.</summary>
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    /// <summary>The first element of the <paramref name="resource"/> envelope.</summary>
    public JsonObject First(string resource) => Json![resource]![0]!.AsObject();

    /// <summary>Every element of the <paramref name="resource"/> envelope.</summary>
    public IReadOnlyList<JsonObject> Items(string resource) => [.. Json![resource]!.AsArray().Select(n => n!.AsObject())];

    /// <summary>Every <c>ValidationErrors[].Message</c> in a <c>ValidationException</c> body.</summary>
    public IReadOnlyList<string> ValidationMessages() =>
        [.. (Json?["Elements"]?.AsArray() ?? []).SelectMany(e => e!["ValidationErrors"]?.AsArray() ?? []).Select(v => v!["Message"]!.GetValue<string>())];
}

/// <summary>
/// A simulator on a hand-moved clock, a client over it, and helpers that
/// send well-formed TempestOS-style requests (bearer, tenant, Accept, an
/// Idempotency-Key on every write) unless a test deliberately breaks one.
/// </summary>
internal sealed class SimulatorTestKit : IDisposable
{
    private int _keys;

    public SimulatorTestKit(XeroSimulatorOptions? options = null)
    {
        Clock = new XeroSimulatorClock();
        Simulator = new XeroApiSimulator(options, Clock);
        Client = Simulator.CreateClient();
    }

    public XeroSimulatorClock Clock { get; }

    public XeroApiSimulator Simulator { get; }

    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        Simulator.Dispose();
    }

    /// <summary>A request with every header a well-behaved caller sends.</summary>
    public HttpRequestMessage Request(HttpMethod method, string path, JsonNode? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", Simulator.Options.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        if (method != HttpMethod.Get)
            request.Headers.Add("Idempotency-Key", idempotencyKey ?? $"tos:test:{++_keys}");
        return request;
    }

    public async Task<SimulatorReply> SendAsync(HttpRequestMessage request)
    {
        using (request)
        {
            using var response = await Client.SendAsync(request);
            return await ReadAsync(response);
        }
    }

    public static async Task<SimulatorReply> ReadAsync(HttpResponseMessage response)
    {
        var text = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        JsonNode? json = null;
        if (response.Content?.Headers.ContentType?.MediaType == "application/json" && text.Length > 0)
            json = JsonNode.Parse(text);

        var headers = response.Headers.Concat(response.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        return new SimulatorReply(response.StatusCode, json, text, headers, response.Headers.RetryAfter?.Delta);
    }

    public Task<SimulatorReply> GetAsync(string path, Action<HttpRequestMessage>? configure = null)
    {
        var request = Request(HttpMethod.Get, path);
        configure?.Invoke(request);
        return SendAsync(request);
    }

    public Task<SimulatorReply> PutAsync(string path, JsonNode body, string? idempotencyKey = null) => SendAsync(Request(HttpMethod.Put, path, body, idempotencyKey));

    public Task<SimulatorReply> PostAsync(string path, JsonNode body, string? idempotencyKey = null) => SendAsync(Request(HttpMethod.Post, path, body, idempotencyKey));

    public Task<SimulatorReply> UploadAsync(HttpMethod method, string path, byte[] content, string mediaType = "application/pdf")
    {
        var request = Request(method, path);
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return SendAsync(request);
    }

    // ------------------------------------------------------------ payloads

    public static JsonObject Line(string description = "Design review", decimal quantity = 2m, decimal unitAmount = 150m, string? taxType = "OUTPUT2", string? accountCode = "200")
    {
        var line = new JsonObject { ["Description"] = description, ["Quantity"] = quantity, ["UnitAmount"] = unitAmount };
        if (taxType is not null)
            line["TaxType"] = taxType;
        if (accountCode is not null)
            line["AccountCode"] = accountCode;
        return line;
    }

    public static JsonObject Invoice(string contactId, string? number = "P0012-INV-001", string type = "ACCREC", string? status = "DRAFT", JsonObject? line = null)
    {
        var invoice = new JsonObject
        {
            ["Type"] = type,
            ["Contact"] = new JsonObject { ["ContactID"] = contactId },
            ["Date"] = "2026-10-02",
            ["DueDate"] = "2026-11-01",
            ["LineAmountTypes"] = "Exclusive",
            ["CurrencyCode"] = "GBP",
            ["LineItems"] = new JsonArray { line ?? (type == "ACCPAY" ? Line("Train fare", 1m, 100m, "INPUT2", "493") : Line()) },
        };
        if (number is not null)
            invoice["InvoiceNumber"] = number;
        if (status is not null)
            invoice["Status"] = status;
        return new JsonObject { ["Invoices"] = new JsonArray { invoice } };
    }

    public static JsonObject Quote(string contactId, string? number = "P0012-Q-001", string? status = "DRAFT")
    {
        var quote = new JsonObject
        {
            ["Contact"] = new JsonObject { ["ContactID"] = contactId },
            ["Date"] = "2026-10-02",
            ["ExpiryDate"] = "2026-11-01",
            ["Title"] = "Bracket redesign",
            ["Summary"] = "P0012",
            ["Reference"] = "R1",
            ["LineAmountTypes"] = "Exclusive",
            ["CurrencyCode"] = "GBP",
            ["LineItems"] = new JsonArray { Line() },
        };
        if (number is not null)
            quote["QuoteNumber"] = number;
        if (status is not null)
            quote["Status"] = status;
        return new JsonObject { ["Quotes"] = new JsonArray { quote } };
    }

    public static JsonObject PurchaseOrder(string contactId, string? number = "P0012-PO-001", string? status = "DRAFT", JsonObject? line = null)
    {
        var order = new JsonObject
        {
            ["Contact"] = new JsonObject { ["ContactID"] = contactId },
            ["Date"] = "2026-10-02",
            ["DeliveryDate"] = "2026-10-16",
            ["Reference"] = "P0012",
            ["CurrencyCode"] = "GBP",
            ["LineItems"] = new JsonArray { line ?? Line("Aluminium bar", 4m, 25m, "INPUT2", "310") },
        };
        if (number is not null)
            order["PurchaseOrderNumber"] = number;
        if (status is not null)
            order["Status"] = status;
        return new JsonObject { ["PurchaseOrders"] = new JsonArray { order } };
    }

    /// <summary>A status-only quote update as TempestOS sends it (design §4.1): id, number, contact, date, status — no lines.</summary>
    public static JsonObject QuoteStatus(string quoteId, string contactId, string status, string number = "P0012-Q-001") => new()
    {
        ["Quotes"] = new JsonArray
        {
            new JsonObject
            {
                ["QuoteID"] = quoteId,
                ["QuoteNumber"] = number,
                ["Contact"] = new JsonObject { ["ContactID"] = contactId },
                ["Date"] = "2026-10-02",
                ["Status"] = status,
            },
        },
    };

    // ------------------------------------------------------------ journeys

    public async Task<string> CreateInvoiceAsync(string contactId, string? number = "P0012-INV-001", string type = "ACCREC")
    {
        var reply = await PutAsync("Invoices?summarizeErrors=true", Invoice(contactId, number, type));
        Assert.Equal(HttpStatusCode.OK, reply.Status);
        return reply.First("Invoices")["InvoiceID"]!.GetValue<string>();
    }

    public async Task<string> CreateQuoteAsync(string contactId, string number = "P0012-Q-001")
    {
        var reply = await PutAsync("Quotes?summarizeErrors=true", Quote(contactId, number));
        Assert.Equal(HttpStatusCode.OK, reply.Status);
        return reply.First("Quotes")["QuoteID"]!.GetValue<string>();
    }

    public async Task<string> CreatePurchaseOrderAsync(string contactId, string number = "P0012-PO-001")
    {
        var reply = await PutAsync("PurchaseOrders?summarizeErrors=true", PurchaseOrder(contactId, number));
        Assert.Equal(HttpStatusCode.OK, reply.Status);
        return reply.First("PurchaseOrders")["PurchaseOrderID"]!.GetValue<string>();
    }

    public async Task<SimulatorReply> SetQuoteStatusAsync(string quoteId, string contactId, string status, string number = "P0012-Q-001") =>
        await PostAsync($"Quotes/{quoteId}?summarizeErrors=true", QuoteStatus(quoteId, contactId, status, number));

    /// <summary>The rules recorded since <paramref name="before"/> violations had been seen.</summary>
    public IReadOnlyList<string> RulesSince(int before) => [.. Simulator.Violations.Skip(before).Select(v => v.Rule)];
}

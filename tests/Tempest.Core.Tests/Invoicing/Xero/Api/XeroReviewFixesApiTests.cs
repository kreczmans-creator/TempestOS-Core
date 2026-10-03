using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Api;

/// <summary>
/// `v0.24.0` review-board fixes F2, at the transport: M2 (a token endpoint
/// that cannot be reached is an outage, not a refused grant), M7 (unit
/// amounts at four places, <c>?unitdp=4</c>), m1 (content updates pin
/// <c>DRAFT</c>), m19 (line descriptions fit Xero's 4,000 characters), n1
/// (contact writes carry only the allowed keys), n2 (the log names the path
/// only) and n3 (an attachment name never carries a path separator).
/// </summary>
public sealed class XeroReviewFixesApiTests
{
    private const string Root = "https://api.xero.com/api.xro/2.0/";

    // ------------------------------------------------------------------ M2

    [Fact]
    public async Task M2_AnExpiredToken_AndTheTokenEndpointOffline_IsUnavailable_NotReauthorise()
    {
        var (authoriser, secrets) = await XeroTestAuthoriser.CreateAsync(tokenEndpoint: new TerminalHandler { Throw = new HttpRequestException("offline") });
        await ExpireAsync(secrets);

        var access = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.Unavailable, access.Outcome);
        Assert.Null(access.AccessToken);
        Assert.Contains("could not be reached", access.Reason, StringComparison.Ordinal);

        // The grant is untouched: the refresh token is still there for when the endpoint is back.
        Assert.Equal("seeded-refresh-token", await secrets.GetAsync("Invoicing:Xero:RefreshToken"));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":"temporarily_unavailable"}""")]
    [InlineData(HttpStatusCode.TooManyRequests, "")]
    [InlineData(HttpStatusCode.BadRequest, "<html>captive portal</html>")]
    [InlineData(HttpStatusCode.BadGateway, "")]
    public async Task M2_ATokenEndpointAnswerThatIsNotAnOAuthRefusal_IsUnavailable(HttpStatusCode status, string body)
    {
        var endpoint = new TerminalHandler { Respond = _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/html") } };
        var (authoriser, secrets) = await XeroTestAuthoriser.CreateAsync(tokenEndpoint: endpoint);
        await ExpireAsync(secrets);

        Assert.Equal(AccessTokenOutcome.Unavailable, (await authoriser.EnsureAccessTokenAsync()).Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")]
    public async Task M2_AnOAuthRefusalFromTheTokenEndpoint_IsStillReauthorise(HttpStatusCode status, string body)
    {
        var endpoint = new TerminalHandler { Respond = _ => TerminalHandler.Json(status, body) };
        var (authoriser, secrets) = await XeroTestAuthoriser.CreateAsync(tokenEndpoint: endpoint);
        await ExpireAsync(secrets);

        var access = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.Reauthorise, access.Outcome);
        Assert.Contains("refused", access.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task M2_EveryConsumer_MapsTheOutageToUnavailable()
    {
        var (authoriser, secrets) = await XeroTestAuthoriser.CreateAsync(tokenEndpoint: new TerminalHandler { Throw = new HttpRequestException("offline") });
        await ExpireAsync(secrets);
        var network = new TerminalHandler();
        using var client = new HttpClient(network) { BaseAddress = new Uri(Root) };

        // The typed client (every X1–X7 call).
        var api = new XeroAccountingApi(client, authoriser);
        var read = await api.GetAsync<JsonObject>("Organisation");
        Assert.Equal(ConnectorOutcome.Unavailable, read.Outcome);

        // The WP 19.1A connector: a call, and the Settings authorisation state.
        var connector = new XeroConnector(client, authoriser);
        Assert.Equal(ConnectorOutcome.Unavailable, (await connector.ReadStatusAsync("inv-1")).Outcome);
        var state = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.Authorised, state.Status);
        Assert.Contains("could not be reached", state.Detail, StringComparison.Ordinal);

        Assert.Empty(network.Received); // nothing reached Xero without a token
    }

    // ------------------------------------------------------------------ M7

    [Theory]
    [InlineData("Invoices", true)]
    [InlineData("Invoices/inv-1", true)]
    [InlineData("Quotes", true)]
    [InlineData("PurchaseOrders/po-1", true)]
    [InlineData("CreditNotes", true)]
    [InlineData("Invoices/inv-1/Attachments/a.pdf", false)]
    [InlineData("Organisation", false)]
    [InlineData("Contacts", false)]
    [InlineData("TaxRates", false)]
    public void M7_UnitDp_IsSentOnEveryCallOnADocumentWithLines(string path, bool expected) =>
        Assert.Equal(expected, XeroAccountingApi.CarriesUnitAmounts(path));

    [Fact]
    public async Task M7_AWriteAndARead_CarryUnitDp4_AndTheUnitAmountAtFullPrecision()
    {
        var (api, network) = await BuildAsync();
        var order = new XeroWirePurchaseOrderWrite(
            "PO-1", "P0012", new XeroWireContactRef("c-1"), "2026-10-02", null, "GBP", Tempest.Core.Invoicing.Xero.Api.XeroWire.LineAmountTypesExclusive,
            [new XeroWireLineItem("Washers", 1000m, 0.125m, "310", "INPUT2")]);

        await api.CreatePurchaseOrderAsync(order, "tos:po:1:create:abc");
        await api.GetAsync<JsonObject>("PurchaseOrders/po-1");

        Assert.All(network.Received, r => Assert.Contains("unitdp=4", r.Request.RequestUri!.Query, StringComparison.Ordinal));
        Assert.Contains("\"UnitAmount\":0.125", network.Received[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task M7_TheSimulator_RoundsLikeXero_TwoPlacesWithoutUnitDp_FourWithIt()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var line = SimulatorTestKit.Line("Washers", 1000m, 0.125m);

        var rounded = (await kit.PutAsync("Invoices?summarizeErrors=true", SimulatorTestKit.Invoice(contactId, "INV-2DP", line: line))).First("Invoices");
        Assert.Equal(130m, rounded["SubTotal"]!.GetValue<decimal>());
        Assert.Equal(0.13m, rounded["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());

        var kept = (await kit.PutAsync("Invoices?summarizeErrors=true&unitdp=4", SimulatorTestKit.Invoice(contactId, "INV-4DP", line: SimulatorTestKit.Line("Washers", 1000m, 0.125m)))).First("Invoices");
        Assert.Equal(125m, kept["SubTotal"]!.GetValue<decimal>());

        // Read back without unitdp, Xero shows two places; with it, four.
        var id = kept["InvoiceID"]!.GetValue<string>();
        Assert.Equal(0.13m, (await kit.GetAsync($"Invoices/{id}")).First("Invoices")["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal(0.125m, (await kit.GetAsync($"Invoices/{id}?unitdp=4")).First("Invoices")["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData(1000, 0.125, 0.125)]       // four places or fewer: sent as is
    [InlineData(3, 33.333, 33.333)]
    [InlineData(3, 0.333333333, 0.3333)]   // 0.3333 × 3 and 0.333333333 × 3 are both 1.00: rounded harmlessly
    public void M7_FitUnitAmount_KeepsOrRoundsHarmlessly(decimal quantity, decimal unit, decimal expected)
    {
        Assert.Equal(expected, XeroLineRules.FitUnitAmount(quantity, unit, out var problem));
        Assert.Null(problem);
        Assert.Equal(XeroLineRules.LineAmount(quantity, unit), XeroLineRules.LineAmount(quantity, expected));
    }

    [Fact]
    public void M7_FitUnitAmount_RefusesAPriceXeroWouldTotalDifferently()
    {
        Assert.Null(XeroLineRules.FitUnitAmount(1000m, 0.33333m, out var problem));
        Assert.Contains("more than 4 decimal places", problem, StringComparison.Ordinal);
        Assert.Contains("333.30 instead of 333.33", problem, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ m19

    [Fact]
    public void M19_ALongDescription_IsShortenedToXerosLimit_AndSaysSo()
    {
        var fitted = XeroLineRules.FitDescription(new string('a', 5000), out var shortened);

        Assert.True(shortened);
        Assert.Equal(XeroLineRules.MaximumDescriptionLength, fitted.Length);
        Assert.EndsWith(XeroLineRules.ShortenedMarker, fitted, StringComparison.Ordinal);
        Assert.Equal("(no description)", XeroLineRules.FitDescription("  "));
        Assert.Equal("Design review", XeroLineRules.FitDescription("Design review", out var untouched));
        Assert.False(untouched);
    }

    [Fact]
    public async Task M19_TheSimulator_RefusesALineDescriptionOverXerosLimit()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");

        var reply = await kit.PutAsync("Invoices?summarizeErrors=true", SimulatorTestKit.Invoice(contactId, line: SimulatorTestKit.Line(new string('d', 4001))));

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Contains(XeroSimulatorRules.FieldLength, kit.RulesSince(0));
    }

    // ------------------------------------------------------------------ m1

    [Fact]
    public async Task M1_AQuoteOrBillContentUpdate_StatesDraft_NeverLeavesTheStatusOut()
    {
        var (api, network) = await BuildAsync();
        var quote = new XeroWireQuoteWrite(
            "Q-1", "R2", null, null, new XeroWireContactRef("c-1"), "2026-10-02", null, null, "GBP", Tempest.Core.Invoicing.Xero.Api.XeroWire.LineAmountTypesExclusive,
            [new XeroWireLineItem("Design", 1m, 100m, "200", "OUTPUT2")]);
        var bill = new XeroWireBillWrite("EXP-1", new XeroWireContactRef("c-1"), "2026-10-02", "GBP", Tempest.Core.Invoicing.Xero.Api.XeroWire.LineAmountTypesExclusive,
            [new XeroWireLineItem("Train", 1m, 100m, "493", "INPUT2", TaxAmount: 20m)], Status: null);

        await api.UpdateQuoteContentAsync("q-1", quote with { Status = null }, "tos:quote:1:update:abc");
        await api.UpdateBillAsync("inv-1", bill, "tos:bill:1:update:abc");

        Assert.Equal("DRAFT", JsonNode.Parse(network.Received[0].Body!)!["Quotes"]![0]!["Status"]!.GetValue<string>());
        Assert.Equal("DRAFT", JsonNode.Parse(network.Received[1].Body!)!["Invoices"]![0]!["Status"]!.GetValue<string>());
    }

    [Fact]
    public async Task M1_AQuoteThePoSentInXeroMeanwhile_IsRefused_ItsStatusAndLinesStay()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var quoteId = await kit.CreateQuoteAsync(contactId);
        Assert.Equal(HttpStatusCode.OK, (await kit.SetQuoteStatusAsync(quoteId, contactId, "SENT")).Status);
        var before = kit.Simulator.Violations.Count;

        // The update TempestOS sends, as the API now builds it (Status: DRAFT).
        var update = SimulatorTestKit.Quote(contactId);
        update["Quotes"]![0]!["QuoteID"] = quoteId;
        update["Quotes"]![0]!["LineItems"] = new JsonArray { SimulatorTestKit.Line("Changed", 9m, 9m) };
        var reply = await kit.PostAsync($"Quotes/{quoteId}?summarizeErrors=true&unitdp=4", update);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        var held = kit.Simulator.Find("Quotes", quoteId)!;
        Assert.Equal("SENT", held.Status);
        Assert.Equal("Design review", held.Body["LineItems"]![0]!["Description"]!.GetValue<string>());
        Assert.All(kit.RulesSince(before), rule => Assert.Contains(rule, new[] { XeroSimulatorRules.QuoteTransition, XeroSimulatorRules.NotEditable }));
    }

    // ------------------------------------------------------------------ n1

    [Theory]
    [InlineData("PUT", "Contacts", """{"Contacts":[{"Name":"Acme","ContactStatus":"ARCHIVED"}]}""")]
    [InlineData("POST", "Contacts/c-1", """{"Contacts":[{"ContactID":"c-1","BankAccountDetails":"12-34-56 12345678"}]}""")]
    [InlineData("POST", "Contacts/c-1", """{"ContactID":"c-1","PaymentTerms":{"Sales":{"Day":1,"Type":"OFFOLLOWINGMONTH"}}}""")]
    [InlineData("POST", "Contacts/c-1", """{"Contacts":[{"ContactID":"c-1","ContactNumber":{"nested":"x"}}]}""")]
    public async Task N1_AContactWriteCarryingAKeyOutsideTheAllowList_IsBlocked(string method, string path, string json)
    {
        var (client, network, audit) = SafetyRig();

        using var response = await client.SendAsync(Json(new HttpMethod(method), path, json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(XeroWriteSafetyHandler.RuleContactFields, response.Headers.GetValues(XeroWriteSafetyHandler.BlockedHeader).Single());
        Assert.Empty(network.Received);
        Assert.Equal(XeroWriteSafetyHandler.RuleContactFields, Assert.Single(audit.Rows).Detail!["rule"]);
    }

    [Theory]
    [InlineData("PUT", "Contacts", """{"Contacts":[{"Name":"Acme Ltd","ContactNumber":"ACME1","TaxNumber":"GB123456789","CompanyNumber":"01234567","EmailAddress":"accounts@acme.example"}]}""")]
    [InlineData("POST", "Contacts/c-1", """{"Contacts":[{"ContactID":"c-1","ContactNumber":"ACME1"}]}""")]
    public async Task N1_WhatTempestOsWritesToAContact_Passes(string method, string path, string json)
    {
        var (client, network, audit) = SafetyRig();

        using var response = await client.SendAsync(Json(new HttpMethod(method), path, json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(network.Received);
        Assert.Empty(audit.Rows);
    }

    // ------------------------------------------------------------------ n2

    [Fact]
    public async Task N2_TheHttpLog_NamesThePathOnly_NeverTheQueryString()
    {
        var logger = new RecordingLogger();
        var network = new TerminalHandler { Throw = new HttpRequestException("Connection refused (api.xero.com:443)") };
        using var client = new HttpClient(new InvoicingHttpLoggingHandler(logger, network));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync(new Uri($"{Root}Contacts?where=TaxNumber%3D%22GB123456789%22&searchTerm=Acme%20Engineering")));

        Assert.NotEmpty(logger.Lines);
        Assert.All(logger.Lines, line =>
        {
            Assert.DoesNotContain("GB123456789", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Acme", line, StringComparison.Ordinal);
            Assert.DoesNotContain("?", line, StringComparison.Ordinal);
        });
        Assert.Contains(logger.Lines, line => line.Contains("api.xero.com/api.xro/2.0/Contacts", StringComparison.Ordinal));
        Assert.Equal("Invoices", InvoicingHttpLoggingHandler.DescribeTarget(new Uri("Invoices?InvoiceNumbers=INV-1", UriKind.Relative)));
    }

    // ------------------------------------------------------------------ n3

    [Theory]
    [InlineData("INV/2026/7.pdf", "INV-2026-7.pdf")]
    [InlineData(@"INV\7.pdf", "INV-7.pdf")]
    [InlineData("Invoice%20A.pdf", "Invoice_20A.pdf")]
    [InlineData("50% off.jpg", "50% off.jpg")]
    public void N3_AnAttachmentName_NeverCarriesAPathSeparator(string name, string sent) =>
        Assert.Equal(sent, XeroAccountingApi.XeroFileName(name));

    [Fact]
    public async Task N3_AnInvoicePdfNamedAfterANumberWithSlashes_IsUploaded_NotRefused()
    {
        var (client, network, audit) = SafetyRig();
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync();
        var api = new XeroAccountingApi(client, authoriser);
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, """{"Attachments":[{"AttachmentID":"a-1","FileName":"INV-2026-7.pdf"}]}""");

        var result = await api.UploadAttachmentAsync(
            XeroAttachableResource.Invoices, "inv-1", new XeroDocumentFile("INV/2026/7.pdf", "application/pdf", new byte[16], new string('0', 64)), "tos:inv:1:upload:abc");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Empty(audit.Rows);
        Assert.EndsWith("/Attachments/INV-2026-7.pdf", Assert.Single(network.Received).Request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ fixtures

    private static Task ExpireAsync(Tempest.Core.Secrets.ISecretStore secrets) =>
        secrets.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));

    private static async Task<(XeroAccountingApi Api, TerminalHandler Network)> BuildAsync()
    {
        var network = new TerminalHandler { Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, "{}") };
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync();
        var client = new HttpClient(network) { BaseAddress = new Uri(Root) };
        return (new XeroAccountingApi(client, authoriser), network);
    }

    private static (HttpClient Client, TerminalHandler Network, RecordingAuditRecorder Audit) SafetyRig()
    {
        var reader = new FakeSettingsReader { Cached = FakeSettingsReader.Reading(XeroTestAuthoriser.TenantId, isDemoCompany: true) };
        var audit = new RecordingAuditRecorder();
        var network = new TerminalHandler();
        var handler = new XeroWriteSafetyHandler(() => reader, _ => Task.FromResult(false), () => audit) { InnerHandler = network };
        return (new HttpClient(handler) { BaseAddress = new Uri(Root) }, network, audit);
    }

    private static HttpRequestMessage Json(HttpMethod method, string path, string json)
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("xero-tenant-id", XeroTestAuthoriser.TenantId);
        request.Headers.Authorization = new("Bearer", XeroTestAuthoriser.AccessToken);
        return request;
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Lines.Add(formatter(state, exception));
            if (exception is not null)
                Lines.Add(exception.ToString());
        }
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Api;

namespace Tempest.Core.Tests.Invoicing.Xero.Safety;

/// <summary>
/// `v0.24.0` §7.3 item 1: every rule of <see cref="XeroWriteSafetyHandler"/>,
/// positive and negative, at the HTTP level — a hand-built forbidden request
/// is answered with a synthetic 400 and never reaches the network (the
/// terminal handler), an allowed one passes, and every block writes one
/// audit row.
/// </summary>
public sealed class XeroWriteSafetyHandlerTests
{
    private const string Root = "https://api.xero.com/api.xro/2.0/";

    // ------------------------------------------------------------------
    // D3 — invoices and bills
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AUTHORISED")]
    [InlineData("SUBMITTED")]
    [InlineData("PAID")]
    [InlineData("VOIDED")]
    [InlineData("authorised")]
    public async Task InvoiceCreate_WithAnyStatusButDraft_IsBlocked(string status)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Invoices", $$"""{"Invoices":[{"Type":"ACCREC","Status":"{{status}}"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleInvoiceStatus);
    }

    [Theory]
    [InlineData("Invoices", """{"Invoices":[{"Type":"ACCREC","Status":"DRAFT"}]}""")]
    [InlineData("Invoices", """{"Invoices":[{"Type":"ACCPAY","Status":"DRAFT"}]}""")]
    [InlineData("Invoices", """{"Invoices":[{"Type":"ACCREC"}]}""")]
    [InlineData("Invoices/inv-1", """{"InvoiceID":"inv-1","Status":"DELETED"}""")]
    [InlineData("Invoices", """{"Invoices":[{"InvoiceID":"inv-1","Status":"DELETED"}]}""")]
    [InlineData("Invoices/inv-1", """{"InvoiceID":"inv-1","LineItems":[]}""")]
    public async Task InvoiceWrite_AsDraftOrDeletingAnExistingDraft_Passes(string path, string body)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, path, body);

        AssertPassed(rig, response);
    }

    [Fact]
    public async Task InvoiceCreate_AsDeleted_IsBlocked_OnlyAnExistingDraftCanBeDeleted()
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Invoices", """{"Invoices":[{"Type":"ACCREC","Status":"DELETED"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleInvoiceStatus);
    }

    [Fact]
    public async Task InvoiceUpdate_ToAuthorised_IsBlocked_EvenAsTheSecondDocumentOfABatch()
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "Invoices", """{"Invoices":[{"Status":"DRAFT"},{"InvoiceID":"x","Status":"AUTHORISED"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleInvoiceStatus);
    }

    [Fact]
    public async Task InvoiceWrite_WithANonWordStatus_IsBlocked()
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "Invoices/inv-1", """{"Status":3}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleInvoiceStatus);
    }

    // ------------------------------------------------------------------
    // D3 / Q2 — purchase orders
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AUTHORISED")]
    [InlineData("SUBMITTED")]
    [InlineData("BILLED")]
    public async Task PurchaseOrderWrite_WithAStatusOtherThanDraftOrDeleted_IsBlocked(string status)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Put, "PurchaseOrders", $$"""{"PurchaseOrders":[{"Status":"{{status}}"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RulePurchaseOrderStatus);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("DELETED")]
    public async Task PurchaseOrderWrite_AsDraftOrDeleted_Passes(string status)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "PurchaseOrders/po-1", $$"""{"PurchaseOrders":[{"Status":"{{status}}"}]}""");

        AssertPassed(rig, response);
    }

    // ------------------------------------------------------------------
    // quote-status
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("SENT")]
    [InlineData("ACCEPTED")]
    [InlineData("DECLINED")]
    public async Task QuoteWrite_AlongTempestOsOwnWalk_Passes(string status)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "Quotes/q-1", $$"""{"Quotes":[{"QuoteID":"q-1","Status":"{{status}}"}]}""");

        AssertPassed(rig, response);
    }

    [Theory]
    [InlineData("INVOICED")]
    [InlineData("DELETED")]
    [InlineData("AUTHORISED")]
    public async Task QuoteWrite_OutsideTempestOsOwnWalk_IsBlocked(string status)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "Quotes/q-1", $$"""{"Quotes":[{"QuoteID":"q-1","Status":"{{status}}"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleQuoteStatus);
    }

    // ------------------------------------------------------------------
    // D4 — never email, never "sent to contact"
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("POST", "Invoices/inv-1/Email")]
    [InlineData("POST", "invoices/inv-1/email")]
    [InlineData("GET", "Invoices/inv-1/Email")]
    [InlineData("POST", "PurchaseOrders/po-1/Email")]
    public async Task AnyRequestToAnEmailEndpoint_IsBlocked(string method, string path)
    {
        var rig = Rig.Demo();

        var response = method == "GET"
            ? await rig.Client.GetAsync(path)
            : await rig.SendJsonAsync(new HttpMethod(method), path, "{}");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleEmail);
    }

    [Theory]
    [InlineData("""{"Invoices":[{"Status":"DRAFT","SentToContact":true}]}""")]
    [InlineData("""{"Invoices":[{"Status":"DRAFT","sentToContact":"true"}]}""")]
    [InlineData("""{"InvoiceID":"inv-1","Nested":{"SentToContact":true}}""")]
    public async Task AnyBodyMarkingSentToContact_IsBlocked(string body)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "Invoices/inv-1", body);

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleSentToContact);
    }

    [Fact]
    public async Task SentToContactFalse_Passes()
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Post, "Invoices/inv-1", """{"Status":"DRAFT","SentToContact":false}""");

        AssertPassed(rig, response);
    }

    // ------------------------------------------------------------------
    // write-allow-list
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("PUT", "Payments")]
    [InlineData("PUT", "BankTransactions")]
    [InlineData("PUT", "ManualJournals")]
    [InlineData("PUT", "CreditNotes")]
    [InlineData("POST", "Organisation")]
    [InlineData("POST", "TaxRates")]
    [InlineData("PUT", "Accounts")]
    [InlineData("POST", "Invoices/inv-1/History")]
    [InlineData("POST", "Invoices/inv-1/OnlineInvoice")]
    [InlineData("PUT", "Contacts/c-1/Attachments/x.pdf")]
    [InlineData("DELETE", "Invoices/inv-1")]
    [InlineData("PATCH", "Invoices/inv-1")]
    public async Task AWriteOutsideTheAllowList_IsBlocked(string method, string path)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(new HttpMethod(method), path, """{"Status":"DRAFT"}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleWriteAllowList);
    }

    [Theory]
    [InlineData("PUT", "Contacts")]
    [InlineData("POST", "Contacts/c-1")]
    [InlineData("PUT", "Quotes")]
    [InlineData("PUT", "PurchaseOrders")]
    public async Task AWriteToAnAllowedDocument_Passes(string method, string path)
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(new HttpMethod(method), path, """{"Name":"Acme"}""");

        AssertPassed(rig, response);
    }

    [Theory]
    [InlineData("PUT", "Quotes/q-1/Attachments/P0012-Q-001.pdf")]
    [InlineData("POST", "Invoices/inv-1/Attachments/INV-1.pdf?IncludeOnline=false")]
    [InlineData("PUT", "PurchaseOrders/po-1/Attachments/PO%201.pdf")]
    public async Task AnAttachmentUpload_Passes(string method, string path)
    {
        var rig = Rig.Demo();
        using var content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]);
        content.Headers.ContentType = new("application/pdf");
        using var request = rig.Request(new HttpMethod(method), path, content);

        var response = await rig.Client.SendAsync(request);

        AssertPassed(rig, response);
    }

    [Fact]
    public async Task AnXmlBodyToAnAllowedDocument_IsBlocked_ItsStatusCannotBeChecked()
    {
        var rig = Rig.Demo();
        using var request = rig.Request(HttpMethod.Put, "Invoices", new StringContent("<Invoices><Invoice><Status>AUTHORISED</Status></Invoice></Invoices>", Encoding.UTF8, "application/xml"));

        var response = await rig.Client.SendAsync(request);

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleWriteAllowList);
    }

    [Fact]
    public async Task AWriteOutsideTheAccountingApiRoot_IsBlocked()
    {
        var rig = Rig.Demo();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.xero.com/payroll.xro/1.0/PayRuns") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Add("xero-tenant-id", XeroTestAuthoriser.TenantId);

        var response = await rig.Client.SendAsync(request);

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleWriteAllowList);
    }

    [Theory]
    [InlineData("Invoices?Statuses=AUTHORISED")]
    [InlineData("Payments")]
    [InlineData("Organisation")]
    public async Task Reads_AlwaysPass(string path)
    {
        var rig = Rig.Live(allowLive: false);

        var response = await rig.Client.GetAsync(path);

        AssertPassed(rig, response);
        Assert.Equal(0, rig.Reader.Refreshes);
    }

    // ------------------------------------------------------------------
    // D7 — the Demo Company first
    // ------------------------------------------------------------------

    [Fact]
    public async Task AWrite_ToTheLiveOrganisation_IsBlocked_WhileNotAllowed()
    {
        var rig = Rig.Live(allowLive: false);

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Invoices", """{"Invoices":[{"Status":"DRAFT"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleLiveOrganisation);
    }

    [Fact]
    public async Task AWrite_ToTheLiveOrganisation_Passes_OnceAllowed()
    {
        var rig = Rig.Live(allowLive: true);

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Invoices", """{"Invoices":[{"Status":"DRAFT"}]}""");

        AssertPassed(rig, response);
    }

    [Fact]
    public async Task AllowingTheLiveOrganisation_NeverRelaxesD3()
    {
        var rig = Rig.Live(allowLive: true);

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Invoices", """{"Invoices":[{"Status":"AUTHORISED"}]}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleInvoiceStatus);
    }

    [Fact]
    public async Task AnUnknownOrganisation_IsReadFirst_AndAWriteToTheDemoCompanyThenPasses()
    {
        var rig = Rig.Demo();
        rig.Reader.OnRefresh = rig.Reader.Cached;
        rig.Reader.Cached = null;

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Contacts", """{"Name":"Acme"}""");

        AssertPassed(rig, response);
        Assert.Equal(1, rig.Reader.Refreshes);
    }

    [Fact]
    public async Task AnOrganisationStillUnknownAfterReading_IsBlocked()
    {
        var rig = Rig.Demo();
        rig.Reader.Cached = null;
        rig.Reader.OnRefresh = null;

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Contacts", """{"Name":"Acme"}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleLiveOrganisation);
        Assert.Equal(1, rig.Reader.Refreshes);
    }

    [Fact]
    public async Task AReadingOfAnotherTenant_IsNeverUsed()
    {
        var rig = Rig.Demo();
        rig.Reader.Cached = FakeSettingsReader.Reading("another-tenant", isDemoCompany: true);
        rig.Reader.OnRefresh = FakeSettingsReader.Reading("another-tenant", isDemoCompany: true);

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Contacts", """{"Name":"Acme"}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleLiveOrganisation);
    }

    [Fact]
    public async Task NoSettingsReaderAtAll_BlocksEveryWrite()
    {
        var rig = new Rig(cached: null, _ => Task.FromResult(false), readerPresent: false);

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Contacts", """{"Name":"Acme"}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleLiveOrganisation);
    }

    [Fact]
    public async Task ASettingThatCannotBeRead_IsTreatedAsOff()
    {
        var rig = new Rig(FakeSettingsReader.Reading(XeroTestAuthoriser.TenantId, isDemoCompany: false), allowLiveOrganisation: _ => throw new InvalidOperationException("settings store unavailable"));

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Contacts", """{"Name":"Acme"}""");

        await AssertBlockedAsync(rig, response, XeroWriteSafetyHandler.RuleLiveOrganisation);
    }

    // ------------------------------------------------------------------
    // The synthetic answer
    // ------------------------------------------------------------------

    [Fact]
    public async Task ABlock_AnswersAXeroShapedValidationException_ThatTheTypedClientReportsAsRejected()
    {
        var rig = Rig.Demo();

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Invoices", """{"Invoices":[{"Status":"AUTHORISED"}]}""");
        var body = await response.Content.ReadAsStringAsync();
        var result = XeroAccountingApi.Interpret<JsonElement>(response, body, DateTimeOffset.UnixEpoch);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("ValidationException", json.RootElement.GetProperty("Type").GetString());
        Assert.Equal(Tempest.Core.Invoicing.ConnectorOutcome.Rejected, result.Outcome);
        Assert.Equal(400, result.HttpStatus);
        Assert.Contains(XeroWriteSafetyHandler.RuleInvoiceStatus, result.Reason, StringComparison.Ordinal);
        Assert.Single(result.ValidationErrors);
    }

    [Fact]
    public async Task ABlockWhoseAuditFails_IsStillABlock()
    {
        var rig = new Rig(FakeSettingsReader.Reading(XeroTestAuthoriser.TenantId, isDemoCompany: true), allowLiveOrganisation: _ => Task.FromResult(false), throwingAudit: true);

        var response = await rig.SendJsonAsync(HttpMethod.Put, "Payments", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(rig.Network.Received);
    }

    [Fact]
    public async Task TheAuditRow_NeverCarriesTheAccessToken()
    {
        var rig = Rig.Demo();

        await rig.SendJsonAsync(HttpMethod.Post, "Invoices/inv-1/Email", "{}");

        var row = Assert.Single(rig.Audit.Rows);
        Assert.DoesNotContain(row.Detail!.Values, value => value.Contains(XeroTestAuthoriser.AccessToken, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------

    private static async Task AssertBlockedAsync(Rig rig, HttpResponseMessage response, string rule)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(rule, response.Headers.GetValues(XeroWriteSafetyHandler.BlockedHeader).Single());
        Assert.Empty(rig.Network.Received);

        var row = Assert.Single(rig.Audit.Rows);
        Assert.Equal(XeroWriteSafetyHandler.BlockedAuditAction, row.Action);
        Assert.Equal(rule, row.Detail!["rule"]);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(rule, body, StringComparison.Ordinal);
    }

    private static void AssertPassed(Rig rig, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(XeroWriteSafetyHandler.BlockedHeader));
        Assert.Single(rig.Network.Received);
        Assert.Empty(rig.Audit.Rows);
    }

    private sealed class Rig
    {
        public Rig(
            Tempest.Core.Invoicing.Xero.Settings.XeroSettingsReading? cached,
            Func<CancellationToken, Task<bool>> allowLiveOrganisation,
            bool throwingAudit = false,
            bool readerPresent = true)
        {
            Reader = new FakeSettingsReader { Cached = cached };
            Audit = new RecordingAuditRecorder();
            var handler = new XeroWriteSafetyHandler(
                () => readerPresent ? Reader : null,
                allowLiveOrganisation,
                () => throwingAudit ? new ThrowingAuditRecorder() : Audit)
            {
                InnerHandler = Network,
            };
            Client = new HttpClient(handler) { BaseAddress = new Uri(Root) };
        }

        public TerminalHandler Network { get; } = new();

        public FakeSettingsReader Reader { get; }

        public RecordingAuditRecorder Audit { get; }

        public HttpClient Client { get; }

        public static Rig Demo() => new(FakeSettingsReader.Reading(XeroTestAuthoriser.TenantId, isDemoCompany: true), _ => Task.FromResult(false));

        public static Rig Live(bool allowLive) =>
            new(FakeSettingsReader.Reading(XeroTestAuthoriser.TenantId, isDemoCompany: false, name: "Tempest Engineering Ltd"), _ => Task.FromResult(allowLive));

        public HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content)
        {
            var request = new HttpRequestMessage(method, path) { Content = content };
            request.Headers.Add("xero-tenant-id", XeroTestAuthoriser.TenantId);
            request.Headers.Authorization = new("Bearer", XeroTestAuthoriser.AccessToken);
            return request;
        }

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string json) =>
            Client.SendAsync(Request(method, path, new StringContent(json, Encoding.UTF8, "application/json")));
    }

    private sealed class ThrowingAuditRecorder : Tempest.Core.Audit.IAuditRecorder
    {
        public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("audit store unavailable");
    }
}

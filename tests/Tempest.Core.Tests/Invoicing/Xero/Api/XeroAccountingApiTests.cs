using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Tests.Invoicing.Xero.Api;

/// <summary>
/// `v0.24.0` task B1: the typed client's transport — the headers every call
/// carries, and every result mapping (§6.6): 200, 200 with per-element
/// errors, 400, 401, 403 (scope and otherwise), 404, 429, 5xx, transport
/// failure, garbage — plus the attachments resource. A local terminal
/// handler stands in for Xero; nothing touches a network.
/// </summary>
public sealed class XeroAccountingApiTests
{
    private const string Envelope = """{"Invoices":[{"InvoiceID":"inv-1","Status":"DRAFT"}]}""";

    // ------------------------------------------------------------------
    // Headers and the request line
    // ------------------------------------------------------------------

    [Fact]
    public async Task AGet_CarriesBearerTenantAndAccept_AndIfModifiedSince_ButNoIdempotencyKeyOrSummarizeErrors()
    {
        var (api, network) = await BuildAsync();
        var since = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await api.GetAsync<Probe>("Invoices", [new("IDs", "a,b"), new("page", "2"), new("skipped", null)], since);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var request = Assert.Single(network.Received).Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.xero.com/api.xro/2.0/Invoices?IDs=a%2Cb&page=2&unitdp=4", request.RequestUri!.AbsoluteUri);
        Assert.Equal($"Bearer {XeroTestAuthoriser.AccessToken}", request.Headers.Authorization!.ToString());
        Assert.Equal(XeroTestAuthoriser.TenantId, request.Headers.GetValues("xero-tenant-id").Single());
        Assert.Contains(request.Headers.Accept, accept => accept.MediaType == "application/json");
        Assert.Equal(since, request.Headers.IfModifiedSince);
        Assert.False(request.Headers.Contains("Idempotency-Key"));
    }

    [Fact]
    public async Task AWrite_CarriesTheIdempotencyKey_SummarizeErrors_AndAJsonBodyWithoutNulls()
    {
        var (api, network) = await BuildAsync();

        var result = await api.PutJsonAsync<Probe>("Invoices", new { Invoices = new[] { new { Type = "ACCREC", Status = XeroInvoiceWriteStatus.Draft, Reference = (string?)null } } }, "tos:invoice:1:create:abc");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var (request, body) = Assert.Single(network.Received);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://api.xero.com/api.xro/2.0/Invoices?summarizeErrors=true&unitdp=4", request.RequestUri!.AbsoluteUri);
        Assert.Equal("tos:invoice:1:create:abc", request.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("""{"Invoices":[{"Type":"ACCREC","Status":"DRAFT"}]}""", body);
    }

    [Fact]
    public async Task APost_IsSentAsAPost()
    {
        var (api, network) = await BuildAsync();

        await api.PostJsonAsync<Probe>("Invoices/inv-1", new { InvoiceID = "inv-1" }, "key-1");

        Assert.Equal(HttpMethod.Post, Assert.Single(network.Received).Request.Method);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AWriteWithoutAnIdempotencyKey_IsRefused_BeforeSending(string? key)
    {
        var (api, network) = await BuildAsync();

        var result = await api.PutJsonAsync<Probe>("Invoices", new { }, key!);

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Null(result.HttpStatus);
        Assert.Empty(network.Received);
    }

    [Fact]
    public async Task AnIdempotencyKeyLongerThan128_IsRefused_BeforeSending_And128IsAccepted()
    {
        var (api, network) = await BuildAsync();

        var tooLong = await api.PutJsonAsync<Probe>("Invoices", new { }, new string('k', 129));
        var longest = await api.PutJsonAsync<Probe>("Invoices", new { }, new string('k', 128));

        Assert.Equal(ConnectorOutcome.Rejected, tooLong.Outcome);
        Assert.Contains("128", tooLong.Reason, StringComparison.Ordinal);
        Assert.Equal(ConnectorOutcome.Ok, longest.Outcome);
        Assert.Single(network.Received);
    }

    [Fact]
    public async Task NeverAuthorised_IsReauthorise_WithNoCall()
    {
        var (api, network) = await BuildAsync(authorised: false);

        var result = await api.GetAsync<Probe>("Organisation");

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Null(result.HttpStatus);
        Assert.Empty(network.Received);
    }

    [Fact]
    public async Task NoTenantConnected_IsReauthorise_WithNoCall()
    {
        var (api, network) = await BuildAsync(tenantId: null);

        var result = await api.GetAsync<Probe>("Organisation");

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Contains("organisation", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(network.Received);
    }

    // ------------------------------------------------------------------
    // Result mapping
    // ------------------------------------------------------------------

    [Fact]
    public async Task Ok_ParsesTheAnswer()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, Envelope);

        var result = await api.GetAsync<Probe>("Invoices/inv-1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(200, result.HttpStatus);
        Assert.Null(result.Reason);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal("inv-1", Assert.Single(result.Value!.Invoices!).InvoiceID);
    }

    [Fact]
    public async Task OkWithPerElementErrors_IsRejected_WithXerosMessages()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK,
            """{"Invoices":[{"InvoiceID":"00000000-0000-0000-0000-000000000000","HasErrors":true,"ValidationErrors":[{"Message":"Account code '999' is not a valid code."},{"Message":"Contact is required."}]}]}""");

        var result = await api.PutJsonAsync<Probe>("Invoices", new { }, "key");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Equal(200, result.HttpStatus);
        Assert.Equal(["Account code '999' is not a valid code.", "Contact is required."], result.ValidationErrors);
        Assert.Contains("Contact is required.", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BadRequest_IsRejected_WithEveryValidationMessage()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.BadRequest,
            """{"ErrorNumber":10,"Type":"ValidationException","Message":"A validation exception occurred","Elements":[{"ValidationErrors":[{"Message":"Invoice # must be unique."}]},{"ValidationErrors":[{"Message":"Second."}]}]}""");

        var result = await api.PutJsonAsync<Probe>("Invoices", new { }, "key");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Equal(400, result.HttpStatus);
        Assert.Equal(["Invoice # must be unique.", "Second."], result.ValidationErrors);
        Assert.Equal("Invoice # must be unique.; Second.", result.Reason);
        Assert.False(result.NotFound);
    }

    [Fact]
    public async Task BadRequestWithOnlyAMessage_IsRejected_WithThatMessage()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.BadRequest, """{"Type":"ValidationException","Message":"The Idempotency-Key was reused"}""");

        var result = await api.PutJsonAsync<Probe>("Invoices", new { }, "key");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal("The Idempotency-Key was reused", result.Reason);
    }

    [Fact]
    public async Task Unauthorised_IsReauthorise_NotAMissingScope()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.Unauthorized, """{"Type":null,"Title":"Unauthorized","Status":401,"Detail":"TokenExpired: token expired"}""");

        var result = await api.GetAsync<Probe>("Invoices");

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Equal(401, result.HttpStatus);
        Assert.False(result.MissingScope);
    }

    [Fact]
    public async Task ForbiddenNamingAuthorisation_IsReauthorise_WithMissingScope()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.Forbidden, """{"Type":"AuthorizationUnsuccessful","Title":"Forbidden","Status":403,"Detail":"AuthorizationUnsuccessful"}""");

        var result = await api.GetAsync<Probe>("TaxRates");

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Equal(403, result.HttpStatus);
        Assert.True(result.MissingScope);
        Assert.Contains("scope", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForbiddenForTheTenant_IsReauthorise_WithoutMissingScope()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.Forbidden, """{"Type":"AuthenticationUnsuccessful","Status":403}""");

        var result = await api.GetAsync<Probe>("Invoices");

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.False(result.MissingScope);
    }

    [Fact]
    public async Task NotFound_IsRejected_WithNotFound()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("The resource you're looking for cannot be found") };

        var result = await api.GetAsync<Probe>("Quotes/q-gone");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Equal(404, result.HttpStatus);
        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task TooManyRequests_IsUnavailable_WithRetryAfterAndTheProblem()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            response.Headers.Add("X-Rate-Limit-Problem", "minute");
            return response;
        };

        var result = await api.GetAsync<Probe>("Invoices");

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Equal(429, result.HttpStatus);
        Assert.Equal(TimeSpan.FromSeconds(42), result.RetryAfter);
        Assert.Equal("minute", result.RateLimitProblem);
    }

    [Fact]
    public void TooManyRequests_WithARetryAfterDate_IsMeasuredFromNow()
    {
        var now = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(90));

        var result = XeroAccountingApi.Interpret<Probe>(response, string.Empty, now);

        Assert.Equal(TimeSpan.FromSeconds(90), result.RetryAfter);
        Assert.Null(result.RateLimitProblem);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ServerError_IsUnavailable(HttpStatusCode status)
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => new HttpResponseMessage(status);

        var result = await api.GetAsync<Probe>("Invoices");

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Equal((int)status, result.HttpStatus);
        Assert.Null(result.RetryAfter);
    }

    [Fact]
    public async Task ATransportFailure_IsUnavailable_WithNoStatus()
    {
        var (api, network) = await BuildAsync();
        network.Throw = new HttpRequestException("No such host is known.");

        var result = await api.PutJsonAsync<Probe>("Invoices", new { }, "key");

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Null(result.HttpStatus);
        Assert.Contains("No such host", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATimeout_IsUnavailable_ButTheCallersOwnCancellationIsRethrown()
    {
        var (api, network) = await BuildAsync();
        network.Throw = new TaskCanceledException("timed out");

        var timedOut = await api.GetAsync<Probe>("Invoices");
        Assert.Equal(ConnectorOutcome.Unavailable, timedOut.Outcome);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.GetAsync<Probe>("Invoices", cancellationToken: cancelled.Token));
    }

    [Theory]
    [InlineData("<html>Gateway</html>")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"Invoices":"not-a-list"}""")]
    public async Task AGarbled2xx_IsUnknown_NeverOk(string body)
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

        var result = await api.PutJsonAsync<Probe>("Invoices", new { }, "key");

        Assert.Equal(ConnectorOutcome.Unknown, result.Outcome);
        Assert.Equal(200, result.HttpStatus);
        Assert.NotNull(result.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotModified)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task AnUnexpectedStatus_IsUnknown(HttpStatusCode status)
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => new HttpResponseMessage(status);

        var result = await api.GetAsync<Probe>("Invoices");

        Assert.Equal(ConnectorOutcome.Unknown, result.Outcome);
        Assert.Equal((int)status, result.HttpStatus);
    }

    [Fact]
    public void Retype_CarriesEveryFactAcross()
    {
        var original = new XeroApiResult<int>(ConnectorOutcome.Unavailable, 0, 429, "slow down", ["x"], TimeSpan.FromSeconds(3), "day", NotFound: true, MissingScope: true);

        var retyped = XeroAccountingApi.Retype<int, string>(original);

        Assert.Equal(new XeroApiResult<string>(ConnectorOutcome.Unavailable, null, 429, "slow down", original.ValidationErrors, TimeSpan.FromSeconds(3), "day", true, true), retyped);
    }

    // ------------------------------------------------------------------
    // Attachments
    // ------------------------------------------------------------------

    [Fact]
    public async Task UploadAttachment_First_PutsTheBytesUnderTheFileName_AndReturnsTheAttachment()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, """{"Attachments":[{"AttachmentID":"att-1","FileName":"P0012-Q-001.pdf","MimeType":"application/pdf","ContentLength":4}]}""");

        var result = await api.UploadAttachmentAsync(XeroAttachableResource.Quotes, "q-1", Pdf("P0012-Q-001.pdf", 4), "key-att");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal("att-1", result.Value!.AttachmentID);
        var request = Assert.Single(network.Received).Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://api.xero.com/api.xro/2.0/Quotes/q-1/Attachments/P0012-Q-001.pdf?summarizeErrors=true", request.RequestUri!.AbsoluteUri);
        Assert.Equal("application/pdf", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("key-att", request.Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task UploadAttachment_ToAnInvoice_StatesIncludeOnline_AndReplacingPosts()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, """{"Attachments":[{"AttachmentID":"att-2","FileName":"INV 7.pdf"}]}""");

        var result = await api.UploadAttachmentAsync(XeroAttachableResource.Invoices, "inv-1", Pdf("INV 7.pdf", 10), "key", replaceExisting: true);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var request = Assert.Single(network.Received).Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api.xro/2.0/Invoices/inv-1/Attachments/INV%207.pdf", request.RequestUri!.AbsolutePath);
        Assert.Contains("IncludeOnline=false", request.RequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAttachment_ToAPurchaseOrder_HasNoIncludeOnline()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, """{"Attachments":[{"AttachmentID":"att-3"}]}""");

        await api.UploadAttachmentAsync(XeroAttachableResource.PurchaseOrders, "po-1", Pdf("PO.pdf", 1), "key", includeOnline: true);

        Assert.DoesNotContain("IncludeOnline", Assert.Single(network.Received).Request.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAttachment_LargerThanTheCap_IsRefused_BeforeSending()
    {
        var (api, network) = await BuildAsync();

        var result = await api.UploadAttachmentAsync(XeroAttachableResource.Invoices, "inv-1", Pdf("big.pdf", (int)XeroDocumentFile.MaximumSizeInBytes + 1), "key");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Empty(network.Received);
    }

    [Fact]
    public async Task UploadAttachment_AnsweredWithNoAttachment_IsUnknown()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, """{"Attachments":[]}""");

        var result = await api.UploadAttachmentAsync(XeroAttachableResource.Quotes, "q-1", Pdf("a.pdf", 1), "key");

        Assert.Equal(ConnectorOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public async Task UploadAttachment_Rejected_CarriesTheReasonAcross()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.BadRequest, """{"Elements":[{"ValidationErrors":[{"Message":"Too many attachments."}]}]}""");

        var result = await api.UploadAttachmentAsync(XeroAttachableResource.Quotes, "q-1", Pdf("a.pdf", 1), "key");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Equal(["Too many attachments."], result.ValidationErrors);
    }

    [Fact]
    public async Task ListAttachments_ReadsTheEnvelope()
    {
        var (api, network) = await BuildAsync();
        network.Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, """{"Attachments":[{"AttachmentID":"a","FileName":"x.pdf"},{"AttachmentID":"b","FileName":"y.pdf"}]}""");

        var result = await api.ListAttachmentsAsync(XeroAttachableResource.Invoices, "inv-1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(["x.pdf", "y.pdf"], result.Value!.Select(a => a.FileName));
        Assert.Equal("/api.xro/2.0/Invoices/inv-1/Attachments", Assert.Single(network.Received).Request.RequestUri!.AbsolutePath);
    }

    // ------------------------------------------------------------------
    // Through the safety handler
    // ------------------------------------------------------------------

    [Fact]
    public async Task AWriteTheSafetyHandlerBlocks_ArrivesAsRejected_NamingTheRule_AndNeverReachesXero()
    {
        var network = new TerminalHandler();
        var handler = new XeroWriteSafetyHandler(
            () => new FakeSettingsReader { Cached = FakeSettingsReader.Reading(XeroTestAuthoriser.TenantId, isDemoCompany: true) },
            _ => Task.FromResult(false),
            () => null)
        { InnerHandler = network };
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync();
        var api = new XeroAccountingApi(new HttpClient(handler) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") }, authoriser);

        var result = await api.PostJsonAsync<Probe>("Invoices/inv-1", new { InvoiceID = "inv-1", Status = "AUTHORISED" }, "key");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains(XeroWriteSafetyHandler.RuleInvoiceStatus, result.Reason, StringComparison.Ordinal);
        Assert.Empty(network.Received);
    }

    // ------------------------------------------------------------------
    // Wire models
    // ------------------------------------------------------------------

    [Fact]
    public void WriteStatuses_SerialiseAsXerosUpperCaseWords_AndCanExpressNothingElse()
    {
        Assert.Equal(["DRAFT", "DELETED"], Enum.GetValues<XeroInvoiceWriteStatus>().Select(s => JsonSerializer.Serialize(s, XeroWire.JsonOptions).Trim('"')));
        Assert.Equal(["DRAFT", "SENT", "ACCEPTED", "DECLINED"], Enum.GetValues<XeroQuoteWriteStatus>().Select(s => JsonSerializer.Serialize(s, XeroWire.JsonOptions).Trim('"')));
        Assert.Equal(["DRAFT", "DELETED"], Enum.GetValues<XeroPurchaseOrderWriteStatus>().Select(s => JsonSerializer.Serialize(s, XeroWire.JsonOptions).Trim('"')));

        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((XeroInvoiceWriteStatus)7, XeroWire.JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<XeroInvoiceWriteStatus>("\"AUTHORISED\"", XeroWire.JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<XeroInvoiceWriteStatus>("\"1\"", XeroWire.JsonOptions));
        Assert.Equal(XeroQuoteWriteStatus.Sent, JsonSerializer.Deserialize<XeroQuoteWriteStatus>("\"sent\"", XeroWire.JsonOptions));
    }

    [Fact]
    public void LineItem_OmitsUnsetFields_OnWrite()
    {
        var json = JsonSerializer.Serialize(new XeroWireLineItem("Design review", 2m, 95m, AccountCode: "200", TaxType: "OUTPUT2"), XeroWire.JsonOptions);

        Assert.Equal("""{"Description":"Design review","Quantity":2,"UnitAmount":95,"AccountCode":"200","TaxType":"OUTPUT2"}""", json);
    }

    [Theory]
    [InlineData("/Date(1735689600000+0000)/", 2025, 1, 1)]
    [InlineData("/Date(1735689600000)/", 2025, 1, 1)]
    [InlineData("2025-01-01T00:00:00", 2025, 1, 1)]
    [InlineData("2026-10-02", 2026, 10, 2)]
    public void Dates_ReadBothXeroForms(string raw, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), XeroWire.ParseDate(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/Date()/")]
    [InlineData("not a date")]
    public void Dates_Unreadable_AreNull(string? raw)
    {
        Assert.Null(XeroWire.ParseDateTime(raw));
    }

    [Fact]
    public void Dates_AreWrittenAsIsoDates()
    {
        Assert.Equal("2026-10-02", XeroWire.FormatDate(new DateOnly(2026, 10, 2)));
    }

    // ------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------

    private static async Task<(XeroAccountingApi Api, TerminalHandler Network)> BuildAsync(string? tenantId = XeroTestAuthoriser.TenantId, bool authorised = true)
    {
        var network = new TerminalHandler { Respond = _ => TerminalHandler.Json(HttpStatusCode.OK, Envelope) };
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync(tenantId, authorised);
        var client = new HttpClient(network) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") };

        return (new XeroAccountingApi(client, authoriser), network);
    }

    private static XeroDocumentFile Pdf(string name, int length) =>
        new(name, "application/pdf", new byte[length], new string('0', 64));

    /// <summary>A minimal answer shape for transport tests.</summary>
    public sealed record Probe(IReadOnlyList<ProbeInvoice>? Invoices);

    /// <summary>One invoice of <see cref="Probe"/>.</summary>
    public sealed record ProbeInvoice(string? InvoiceID, string? Status);
}

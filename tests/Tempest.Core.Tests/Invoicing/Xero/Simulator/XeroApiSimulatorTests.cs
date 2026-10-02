using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>
/// The simulator's own request pipeline (design §10.1, task S1): auth
/// headers, scope per endpoint, routing, the UK Demo Company seed,
/// Idempotency-Key, rate limits on a fake clock, paging,
/// <c>If-Modified-Since</c>, every fault kind and the request log. Document
/// rules are in <see cref="XeroApiSimulatorDocumentTests"/>.
/// </summary>
public sealed class XeroApiSimulatorTests
{
    // ------------------------------------------------------------ auth

    [Fact]
    public async Task A_request_with_every_header_right_is_answered()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Organisation");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-token")]
    public async Task A_missing_or_wrong_bearer_token_is_401(string? token)
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Organisation", r => r.Headers.Authorization = token is null ? null : new("Bearer", token));

        Assert.Equal(HttpStatusCode.Unauthorized, reply.Status);
        Assert.Equal(401, kit.Simulator.Requests.Single().StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task A_missing_or_wrong_tenant_is_403(string? tenant)
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Invoices", r =>
        {
            r.Headers.Remove("xero-tenant-id");
            if (tenant is not null)
                r.Headers.Add("xero-tenant-id", tenant);
        });

        Assert.Equal(HttpStatusCode.Forbidden, reply.Status);
        Assert.Contains("AuthenticationUnsuccessful", reply.Json!["Detail"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ scopes

    [Theory]
    [InlineData("required", "GET", "Organisation", 200)]
    [InlineData("required", "GET", "TaxRates", 200)]
    [InlineData("required", "GET", "Accounts", 200)]
    [InlineData("required", "GET", "Contacts", 200)]
    [InlineData("required", "GET", "Invoices", 200)]
    [InlineData("required", "GET", "Quotes", 200)]
    [InlineData("required", "GET", "PurchaseOrders", 200)]
    [InlineData("required", "GET", "RepeatingInvoices", 200)]
    [InlineData("required", "GET", "Reports/BankSummary", 200)]
    [InlineData("accounting.contacts.read", "GET", "Contacts", 200)]
    [InlineData("accounting.contacts.read", "PUT", "Contacts", 403)]
    [InlineData("accounting.invoices.read", "GET", "Invoices", 200)]
    [InlineData("accounting.invoices.read", "PUT", "Invoices", 403)]
    [InlineData("accounting.transactions", "GET", "Quotes", 200)]
    [InlineData("accounting.contacts", "GET", "Invoices", 403)]
    [InlineData("accounting.contacts", "GET", "Quotes", 403)]
    [InlineData("accounting.contacts", "GET", "PurchaseOrders", 403)]
    [InlineData("accounting.invoices", "GET", "Contacts", 403)]
    [InlineData("accounting.invoices", "GET", "TaxRates", 403)]
    [InlineData("accounting.invoices", "GET", "Organisation", 403)]
    [InlineData("accounting.invoices", "GET", "Reports/BankSummary", 403)]
    [InlineData("accounting.invoices", "PUT", "Invoices/00000000-0000-0000-0000-000000000001/Attachments/a.pdf", 403)]
    [InlineData("accounting.attachments.read", "PUT", "Invoices/00000000-0000-0000-0000-000000000001/Attachments/a.pdf", 403)]
    [InlineData("accounting.attachments", "GET", "Invoices/00000000-0000-0000-0000-000000000001/Attachments", 404)]
    [InlineData("accounting.settings", "GET", "Accounts", 200)]
    public async Task Each_endpoint_needs_one_of_its_scopes(string granted, string method, string path, int expected)
    {
        var scopes = granted == "required" ? null : granted.Split(',');
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(GrantedScopes: scopes));
        var request = kit.Request(new HttpMethod(method), path, method == "GET" ? null : new JsonObject { ["Name"] = "x" });

        var reply = await kit.SendAsync(request);

        Assert.Equal(expected, (int)reply.Status);
        if (expected == 403)
        {
            Assert.Contains("AuthorizationUnsuccessful", reply.Json!["Detail"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal(XeroSimulatorRules.Scope, Assert.Single(kit.Simulator.Violations).Rule);
        }
    }

    [Fact]
    public async Task The_default_grant_is_exactly_the_required_scopes()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(GrantedScopes: XeroScopes.Required));
        foreach (var path in new[] { "Organisation", "TaxRates", "Accounts", "Contacts", "Invoices", "Quotes", "PurchaseOrders", "Reports/BankSummary" })
            Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync(path)).Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    // ------------------------------------------------------------ routing

    [Fact]
    public async Task An_unknown_endpoint_is_404_and_a_read_of_it_is_no_violation()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Nonsense/Thing");

        Assert.Equal(HttpStatusCode.NotFound, reply.Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData("PUT", "Payments")]
    [InlineData("PUT", "BankTransactions")]
    [InlineData("PUT", "ManualJournals")]
    [InlineData("PUT", "CreditNotes")]
    [InlineData("POST", "Organisation")]
    [InlineData("POST", "TaxRates")]
    [InlineData("PUT", "Accounts")]
    [InlineData("POST", "Contacts/00000000-0000-0000-0000-000000000001/Attachments/a.pdf")]
    public async Task A_write_outside_the_allow_list_is_recorded(string method, string path)
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.SendAsync(kit.Request(new HttpMethod(method), path, new JsonObject { ["Amount"] = 1 }));

        Assert.Equal(HttpStatusCode.NotFound, reply.Status);
        Assert.Equal(XeroSimulatorRules.WriteAllowList, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task An_unsupported_method_is_405_and_recorded()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var invoiceId = await kit.CreateInvoiceAsync(contactId);

        var delete = await kit.SendAsync(kit.Request(HttpMethod.Delete, $"Invoices/{invoiceId}"));
        var putById = await kit.PutAsync($"Invoices/{invoiceId}", SimulatorTestKit.Invoice(contactId));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.Status);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, putById.Status);
        Assert.All(kit.Simulator.Violations, v => Assert.Equal(XeroSimulatorRules.Method, v.Rule));
        Assert.Equal(2, kit.Simulator.Violations.Count);
    }

    [Fact]
    public async Task A_request_without_Accept_json_is_answered_but_recorded()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Organisation", r => r.Headers.Accept.Clear());

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(XeroSimulatorRules.AcceptJson, Assert.Single(kit.Simulator.Violations).Rule);
    }

    // ------------------------------------------------------------ UK Demo Company seed

    [Fact]
    public async Task The_organisation_is_the_UK_Demo_Company()
    {
        using var kit = new SimulatorTestKit();

        var org = (await kit.GetAsync("Organisation")).First("Organisations");

        Assert.True(org["IsDemoCompany"]!.GetValue<bool>());
        Assert.Equal("Demo Company (UK)", org["Name"]!.GetValue<string>());
        Assert.Equal("Demo Company (UK)", org["LegalName"]!.GetValue<string>());
        Assert.Equal("GBP", org["BaseCurrency"]!.GetValue<string>());
        Assert.Equal("GB", org["CountryCode"]!.GetValue<string>());
        Assert.True(org["PaysTax"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(org["TaxNumber"]!.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(org["RegistrationNumber"]!.GetValue<string>()));
        Assert.Contains(org["Addresses"]!.AsArray(), a => a!["AddressType"]!.GetValue<string>() == "STREET");
        Assert.Contains(org["ExternalLinks"]!.AsArray(), l => l!["LinkType"]!.GetValue<string>() == "Website");
        Assert.Single(org["Phones"]!.AsArray());
    }

    [Fact]
    public async Task A_live_organisation_reports_IsDemoCompany_false()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(OrganisationName: "Tempest Engineering Ltd", IsDemoCompany: false));

        var org = (await kit.GetAsync("Organisation")).First("Organisations");

        Assert.False(org["IsDemoCompany"]!.GetValue<bool>());
        Assert.Equal("Tempest Engineering Ltd", org["Name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("OUTPUT2", 20.0, true, false)]
    [InlineData("RROUTPUT", 5.0, true, false)]
    [InlineData("ZERORATEDOUTPUT", 0.0, true, false)]
    [InlineData("EXEMPTOUTPUT", 0.0, true, false)]
    [InlineData("INPUT2", 20.0, false, true)]
    [InlineData("RRINPUT", 5.0, false, true)]
    [InlineData("ZERORATEDINPUT", 0.0, false, true)]
    [InlineData("EXEMPTINPUT", 0.0, false, true)]
    [InlineData("NONE", 0.0, true, true)]
    public async Task The_nine_UK_tax_types_are_seeded_active(string taxType, double rate, bool revenue, bool expenses)
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync($"TaxRates?TaxType={taxType}");

        var item = Assert.Single(reply.Items("TaxRates"));
        Assert.Equal("ACTIVE", item["Status"]!.GetValue<string>());
        Assert.Equal((decimal)rate, item["EffectiveRate"]!.GetValue<decimal>());
        Assert.Equal(revenue, item["CanApplyToRevenue"]!.GetValue<bool>());
        Assert.Equal(expenses, item["CanApplyToExpenses"]!.GetValue<bool>());
    }

    [Fact]
    public async Task An_inactive_tax_type_exists_for_refusal_tests()
    {
        using var kit = new SimulatorTestKit();

        var rates = (await kit.GetAsync("TaxRates")).Items("TaxRates");

        Assert.Equal(10, rates.Count);
        Assert.Equal("DELETED", rates.Single(r => r["TaxType"]!.GetValue<string>() == "OUTPUT")["Status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("200", "Sales", "REVENUE", "REVENUE")]
    [InlineData("310", "Cost of Goods Sold", "DIRECTCOSTS", "EXPENSE")]
    [InlineData("493", "Travel - National", "OVERHEADS", "EXPENSE")]
    [InlineData("429", "General Expenses", "OVERHEADS", "EXPENSE")]
    [InlineData("090", "Business Bank Account", "BANK", "ASSET")]
    public async Task The_chart_of_accounts_subset_is_seeded(string code, string name, string type, string cls)
    {
        using var kit = new SimulatorTestKit();

        var accounts = (await kit.GetAsync("Accounts")).Items("Accounts");

        var account = accounts.Single(a => a["Code"]!.GetValue<string>() == code);
        Assert.Equal(name, account["Name"]!.GetValue<string>());
        Assert.Equal(type, account["Type"]!.GetValue<string>());
        Assert.Equal(cls, account["Class"]!.GetValue<string>());
        Assert.Equal("ACTIVE", account["Status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Accounts_filter_by_where_and_bank_accounts_carry_their_number()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Accounts?where=" + Uri.EscapeDataString("Type==\"BANK\"&&Status==\"ACTIVE\""));

        var bank = Assert.Single(reply.Items("Accounts"));
        Assert.Equal("090", bank["Code"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(bank["BankAccountNumber"]!.GetValue<string>()));
    }

    [Fact]
    public async Task The_bank_summary_report_has_a_header_and_a_bank_row()
    {
        using var kit = new SimulatorTestKit();

        var report = (await kit.GetAsync("Reports/BankSummary")).First("Reports");

        var rows = report["Rows"]!.AsArray();
        Assert.Equal("Header", rows[0]!["RowType"]!.GetValue<string>());
        Assert.Contains("Closing Balance", rows[0]!.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("Business Bank Account", rows[1]!.ToJsonString(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ Idempotency-Key

    [Fact]
    public async Task A_repeat_with_the_same_key_and_body_replays_the_first_response_and_creates_nothing()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId);

        var first = await kit.PutAsync("Invoices?summarizeErrors=true", body, "tos:invoice:1:create:abc");
        kit.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await kit.PutAsync("Invoices?summarizeErrors=true", body, "tos:invoice:1:create:abc");

        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(first.Text, second.Text);
        Assert.Single(kit.Simulator.All("Invoices"));
        Assert.Equal(2, kit.Simulator.Requests.Count);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task A_key_reused_with_a_different_body_is_400_and_recorded()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, "INV-A"), "tos:k");
        var reuse = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, "INV-B"), "tos:k");

        Assert.Equal(HttpStatusCode.BadRequest, reuse.Status);
        Assert.Equal("ValidationException", reuse.Json!["Type"]!.GetValue<string>());
        Assert.Single(kit.Simulator.All("Invoices"));
        Assert.Equal(XeroSimulatorRules.IdempotencyKeyReused, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Theory]
    [InlineData(128, HttpStatusCode.OK)]
    [InlineData(129, HttpStatusCode.BadRequest)]
    public async Task A_key_longer_than_128_characters_is_400(int length, HttpStatusCode expected)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var reply = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId), new string('k', length));

        Assert.Equal(expected, reply.Status);
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, kit.Simulator.All("Invoices").Count);
        if (expected == HttpStatusCode.BadRequest)
            Assert.Equal(XeroSimulatorRules.IdempotencyKeyTooLong, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task A_write_without_a_key_is_applied_but_recorded()
    {
        using var kit = new SimulatorTestKit();
        var request = kit.Request(HttpMethod.Put, "Contacts", new JsonObject { ["Name"] = "Acme Ltd" });
        request.Headers.Remove("Idempotency-Key");

        var reply = await kit.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(XeroSimulatorRules.IdempotencyKeyMissing, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task A_lost_response_retried_with_the_same_key_makes_one_record()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.DropResponseAfterCommit, PathContains: "Invoices"));

        await Assert.ThrowsAsync<HttpRequestException>(() => kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId), "tos:lost"));
        var retry = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId), "tos:lost");

        var created = Assert.Single(kit.Simulator.All("Invoices"));
        Assert.Equal(created.Id, retry.First("Invoices")["InvoiceID"]!.GetValue<string>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task A_refused_write_is_replayed_as_refused_and_not_recorded_twice()
    {
        using var kit = new SimulatorTestKit();
        var bad = new JsonObject { ["Contacts"] = new JsonArray { new JsonObject { ["EmailAddress"] = "a@b.c" } } };

        var first = await kit.PutAsync("Contacts", bad, "tos:bad");
        var second = await kit.PutAsync("Contacts", bad, "tos:bad");

        Assert.Equal(HttpStatusCode.BadRequest, first.Status);
        Assert.Equal(first.Text, second.Text);
        Assert.Single(kit.Simulator.Violations);
    }

    // ------------------------------------------------------------ rate limits

    [Fact]
    public async Task The_61st_call_in_a_minute_is_429_with_Retry_After()
    {
        using var kit = new SimulatorTestKit();

        for (var i = 1; i <= 60; i++)
        {
            var ok = await kit.GetAsync("Organisation");
            Assert.Equal(HttpStatusCode.OK, ok.Status);
            Assert.Equal((60 - i).ToString(CultureInfo.InvariantCulture), ok.Header("X-MinLimit-Remaining"));
        }

        var refused = await kit.GetAsync("Organisation");

        Assert.Equal((HttpStatusCode)429, refused.Status);
        Assert.Equal("minute", refused.Header("X-Rate-Limit-Problem"));
        Assert.Equal(TimeSpan.FromSeconds(60), refused.RetryAfter);
        Assert.Equal("0", refused.Header("X-MinLimit-Remaining"));

        kit.Clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal((HttpStatusCode)429, (await kit.GetAsync("Organisation")).Status);

        kit.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task The_minute_window_rolls_and_Retry_After_counts_to_the_oldest_call()
    {
        using var kit = new SimulatorTestKit();
        for (var i = 0; i < 60; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
            kit.Clock.Advance(TimeSpan.FromMilliseconds(500));
        }

        // 60 calls over 30 s; the oldest leaves the window 30 s from now.
        var refused = await kit.GetAsync("Organisation");
        Assert.Equal(TimeSpan.FromSeconds(30), refused.RetryAfter);

        kit.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
    }

    [Fact]
    public async Task The_5001st_call_in_a_day_is_429_day()
    {
        using var kit = new SimulatorTestKit();
        var start = kit.Clock.GetUtcNow();

        for (var i = 0; i < 5000; i++)
        {
            var reply = await kit.GetAsync("Organisation");
            Assert.Equal(HttpStatusCode.OK, reply.Status);
            kit.Clock.Advance(TimeSpan.FromSeconds(1.2)); // 50 a minute: never the minute limit
        }

        var refused = await kit.GetAsync("Organisation");

        Assert.Equal((HttpStatusCode)429, refused.Status);
        Assert.Equal("day", refused.Header("X-Rate-Limit-Problem"));
        Assert.Equal("0", refused.Header("X-DayLimit-Remaining"));
        var expected = TimeSpan.FromSeconds(Math.Ceiling((start + TimeSpan.FromDays(1) - kit.Clock.GetUtcNow()).TotalSeconds));
        Assert.Equal(expected, refused.RetryAfter);

        kit.Clock.Advance(expected);
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
    }

    [Fact]
    public async Task Limits_follow_the_options_and_refusals_do_not_count()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(DayLimit: 3));

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
        for (var i = 0; i < 5; i++)
            Assert.Equal("day", (await kit.GetAsync("Organisation")).Header("X-Rate-Limit-Problem"));

        kit.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal("2", (await kit.GetAsync("Organisation")).Header("X-DayLimit-Remaining"));
    }

    [Fact]
    public async Task Every_response_carries_the_limit_headers()
    {
        using var kit = new SimulatorTestKit();

        var notFound = await kit.GetAsync("Invoices/00000000-0000-0000-0000-000000000009");

        Assert.Equal(HttpStatusCode.NotFound, notFound.Status);
        Assert.Equal("59", notFound.Header("X-MinLimit-Remaining"));
        Assert.Equal("4999", notFound.Header("X-DayLimit-Remaining"));
        Assert.Equal("9999", notFound.Header("X-AppMinLimit-Remaining"));
    }

    [Fact]
    public async Task A_sixth_concurrent_call_is_429_concurrent()
    {
        using var kit = new SimulatorTestKit();
        using var invoker = new HttpMessageInvoker(kit.Simulator, disposeHandler: false);
        var held = new List<Task<HttpResponseMessage>>();

        using (kit.Simulator.HoldRequests())
        {
            for (var i = 0; i < 5; i++)
                held.Add(invoker.SendAsync(kit.Request(HttpMethod.Get, XeroApiSimulator.BaseAddress + "Organisation"), CancellationToken.None));

            Assert.Equal(5, kit.Simulator.InFlight);
            Assert.All(held, t => Assert.False(t.IsCompleted));

            using var sixth = await invoker.SendAsync(kit.Request(HttpMethod.Get, XeroApiSimulator.BaseAddress + "Organisation"), CancellationToken.None);
            Assert.Equal((HttpStatusCode)429, sixth.StatusCode);
            Assert.Equal("concurrent", sixth.Headers.GetValues("X-Rate-Limit-Problem").Single());
        }

        foreach (var task in held)
        {
            using var response = await task;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(0, kit.Simulator.InFlight);
    }

    // ------------------------------------------------------------ paging and If-Modified-Since

    [Fact]
    public async Task Lists_page_100_at_a_time_and_all_without_a_page()
    {
        using var kit = new SimulatorTestKit();
        for (var i = 0; i < 250; i++)
            kit.Simulator.SeedContact($"Contact {i:000}");

        var page1 = await kit.GetAsync("Contacts?page=1");
        var page3 = await kit.GetAsync("Contacts?page=3");
        var page4 = await kit.GetAsync("Contacts?page=4");
        var all = await kit.GetAsync("Contacts");
        var small = await kit.GetAsync("Contacts?page=2&pageSize=50");

        Assert.Equal(100, page1.Items("Contacts").Count);
        Assert.Equal("Contact 000", page1.Items("Contacts")[0]["Name"]!.GetValue<string>());
        Assert.Equal(3, page1.Json!["pagination"]!["pageCount"]!.GetValue<int>());
        Assert.Equal(250, page1.Json!["pagination"]!["itemCount"]!.GetValue<int>());
        Assert.Equal(50, page3.Items("Contacts").Count);
        Assert.Equal("Contact 200", page3.Items("Contacts")[0]["Name"]!.GetValue<string>());
        Assert.Empty(page4.Items("Contacts"));
        Assert.Equal(250, all.Items("Contacts").Count);
        Assert.Null(all.Json!["pagination"]);
        Assert.Equal("Contact 050", small.Items("Contacts")[0]["Name"]!.GetValue<string>());
        Assert.Equal(50, small.Items("Contacts").Count);
    }

    [Fact]
    public async Task If_Modified_Since_returns_only_records_changed_since()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var t0 = kit.Clock.GetUtcNow();
        var a = await kit.CreateInvoiceAsync(contactId, "INV-A");
        kit.Clock.Advance(TimeSpan.FromMinutes(10));
        var b = await kit.CreateInvoiceAsync(contactId, "INV-B");

        var sinceFive = await kit.GetAsync("Invoices", r => r.Headers.IfModifiedSince = t0.AddMinutes(5));
        Assert.Equal([b], sinceFive.Items("Invoices").Select(i => i["InvoiceID"]!.GetValue<string>()));

        kit.Clock.Advance(TimeSpan.FromMinutes(10));
        kit.Simulator.ApproveInXero(a);
        var sinceFifteen = await kit.GetAsync("Invoices", r => r.Headers.IfModifiedSince = t0.AddMinutes(15));
        Assert.Equal([a], sinceFifteen.Items("Invoices").Select(i => i["InvoiceID"]!.GetValue<string>()));

        var all = await kit.GetAsync("Invoices", r => r.Headers.IfModifiedSince = t0);
        Assert.Equal(2, all.Items("Invoices").Count);
    }

    [Fact]
    public async Task If_Modified_Since_on_accounts_returns_only_changed_accounts()
    {
        using var kit = new SimulatorTestKit();
        var readAt = kit.Clock.GetUtcNow();

        var none = await kit.GetAsync("Accounts", r => r.Headers.IfModifiedSince = readAt);
        kit.Clock.Advance(TimeSpan.FromHours(1));
        kit.Simulator.ArchiveAccountInXero("400");
        var changed = await kit.GetAsync("Accounts", r => r.Headers.IfModifiedSince = readAt);

        Assert.Empty(none.Items("Accounts"));
        var account = Assert.Single(changed.Items("Accounts"));
        Assert.Equal("400", account["Code"]!.GetValue<string>());
        Assert.Equal("ARCHIVED", account["Status"]!.GetValue<string>());
    }

    // ------------------------------------------------------------ faults

    [Fact]
    public async Task ServiceUnavailable_answers_503_and_changes_nothing()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable));

        var reply = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, reply.Status);
        Assert.Empty(kit.Simulator.All("Invoices"));
        Assert.Equal(HttpStatusCode.OK, (await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId))).Status);
    }

    [Theory]
    [InlineData(nameof(XeroFaultKind.RateLimitedMinute), "minute")]
    [InlineData(nameof(XeroFaultKind.RateLimitedDay), "day")]
    public async Task An_injected_429_carries_its_problem_and_Retry_After(string kind, string problem)
    {
        using var kit = new SimulatorTestKit();
        kit.Simulator.Inject(new XeroFault(Enum.Parse<XeroFaultKind>(kind), RetryAfter: TimeSpan.FromSeconds(17)));

        var reply = await kit.GetAsync("Invoices");

        Assert.Equal((HttpStatusCode)429, reply.Status);
        Assert.Equal(problem, reply.Header("X-Rate-Limit-Problem"));
        Assert.Equal(TimeSpan.FromSeconds(17), reply.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Invoices")).Status);
    }

    [Fact]
    public async Task Unauthorised_answers_401()
    {
        using var kit = new SimulatorTestKit();
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.Unauthorised));

        Assert.Equal(HttpStatusCode.Unauthorized, (await kit.GetAsync("Organisation")).Status);
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
    }

    [Fact]
    public async Task TransportFailure_throws_and_changes_nothing()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.TransportFailure));

        await Assert.ThrowsAsync<HttpRequestException>(() => kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId)));

        Assert.Empty(kit.Simulator.All("Invoices"));
        Assert.Equal(0, kit.Simulator.Requests.Single().StatusCode);
    }

    [Fact]
    public async Task DropResponseAfterCommit_creates_the_record_then_throws()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.DropResponseAfterCommit));

        await Assert.ThrowsAsync<HttpRequestException>(() => kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId)));

        Assert.Single(kit.Simulator.All("Invoices"));
        Assert.Equal(200, kit.Simulator.Requests.Single().StatusCode);
        var found = await kit.GetAsync("Invoices?InvoiceNumbers=P0012-INV-001");
        Assert.Single(found.Items("Invoices"));
    }

    [Fact]
    public async Task A_fault_applies_only_to_matching_paths_and_only_Times_times()
    {
        using var kit = new SimulatorTestKit();
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, PathContains: "Invoices", Times: 2));

        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await kit.GetAsync("Invoices")).Status);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await kit.GetAsync("Invoices")).Status);
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Invoices")).Status);
    }

    // ------------------------------------------------------------ request log and wire format

    [Fact]
    public async Task Every_request_is_logged_with_what_was_answered()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var invoiceId = await kit.CreateInvoiceAsync(contactId);
        kit.Clock.Advance(TimeSpan.FromSeconds(3));
        await kit.UploadAsync(HttpMethod.Put, $"Invoices/{invoiceId}/Attachments/P0012-INV-001.pdf?IncludeOnline=false", new byte[1234]);

        var create = kit.Simulator.Requests[0];
        var upload = kit.Simulator.Requests[1];

        Assert.Equal(HttpMethod.Put, create.Method);
        Assert.Equal("Invoices", create.Path);
        Assert.Equal("true", create.Query["summarizeErrors"]);
        Assert.Equal("ACCREC", create.JsonBody!["Invoices"]![0]!["Type"]!.GetValue<string>());
        Assert.Null(create.BinaryBodyLength);
        Assert.Equal("tos:test:1", create.IdempotencyKey);
        Assert.Equal(200, create.StatusCode);
        Assert.Equal(kit.Clock.GetUtcNow().AddSeconds(-3), create.AtUtc);

        Assert.Equal($"Invoices/{invoiceId}/Attachments/P0012-INV-001.pdf", upload.Path);
        Assert.Null(upload.JsonBody);
        Assert.Equal(1234, upload.BinaryBodyLength);
        Assert.Equal(kit.Clock.GetUtcNow(), upload.AtUtc);
    }

    [Fact]
    public async Task Dates_are_answered_as_Microsoft_JSON_dates()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var invoice = (await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId))).First("Invoices");

        var oct2 = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Assert.Equal($"/Date({oct2}+0000)/", invoice["Date"]!.GetValue<string>());
        Assert.Equal("2026-10-02T00:00:00", invoice["DateString"]!.GetValue<string>());
        Assert.Equal($"/Date({kit.Clock.GetUtcNow().ToUnixTimeMilliseconds()}+0000)/", invoice["UpdatedDateUTC"]!.GetValue<string>());
        Assert.True(XeroWire.TryParseDate(invoice["DueDate"]!.GetValue<string>(), out var due));
        Assert.Equal(new DateOnly(2026, 11, 1), due);
    }

    [Theory]
    [InlineData("Type==\"ACCREC\"", true)]
    [InlineData("Type!=\"ACCREC\"", true)]
    [InlineData("Contact.Name.StartsWith(\"Ac\")", true)]
    [InlineData("Reference==\"R1\" AND Status==\"DRAFT\"", true)]
    [InlineData("Contact.ContactID==Guid(\"00000000-0000-0000-0000-000000000001\")", true)]
    [InlineData("Type==\"ACCREC\" || Type==\"ACCPAY\"", false)]
    [InlineData("Date>=DateTime(2026,01,01)", false)]
    [InlineData("Total>100", false)]
    public void The_where_grammar_accepts_what_TempestOS_sends_and_refuses_the_rest(string where, bool understood)
    {
        Assert.Equal(understood, XeroWire.CompileWhere(where, out _) is not null);
    }
}

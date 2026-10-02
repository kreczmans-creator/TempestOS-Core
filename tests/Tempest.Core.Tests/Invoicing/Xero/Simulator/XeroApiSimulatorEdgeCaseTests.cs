using System.Net;
using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>
/// Edge cases of the simulator's own fidelity (task S1 verification):
/// uniqueness inside one batch, quoted text in <c>where=</c>, D7 and
/// <c>SentToContact</c> on writes refused early, D3 on refused creates,
/// faults not used up by a refused request, strict date parsing, and tax
/// recomputed when only <c>LineAmountTypes</c> changes.
/// </summary>
public sealed class XeroApiSimulatorEdgeCaseTests
{
    // ------------------------------------------------------------ uniqueness inside one batch

    public static TheoryData<string> BatchKinds() => ["Contacts", "Invoices", "Quotes", "PurchaseOrders"];

    [Theory]
    [MemberData(nameof(BatchKinds))]
    public async Task Two_elements_of_one_batch_cannot_claim_the_same_unique_key(string resource)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var summarised = await kit.PutAsync(resource, Batch(resource, contactId, "Same Key"));

        Assert.Equal(HttpStatusCode.BadRequest, summarised.Status);
        Assert.Contains(summarised.ValidationMessages(), m => m.Contains("Same Key", StringComparison.Ordinal));
        Assert.Equal(XeroSimulatorRules.Duplicate, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.DoesNotContain(kit.Simulator.All(resource), d => d.Id != contactId);

        var perElement = await kit.PutAsync(resource + "?summarizeErrors=false", Batch(resource, contactId, "Same Key"));

        Assert.Equal(HttpStatusCode.OK, perElement.Status);
        var items = perElement.Items(resource);
        Assert.Equal("OK", items[0]["StatusAttributeString"]!.GetValue<string>());
        Assert.Equal("ERROR", items[1]["StatusAttributeString"]!.GetValue<string>());
        Assert.Single(kit.Simulator.All(resource), d => d.Id != contactId);
    }

    [Fact]
    public async Task Two_bills_in_one_batch_may_share_a_supplier_reference()
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Rail Co");
        var bill = SimulatorTestKit.Invoice(supplier, "EXP-1", "ACCPAY")["Invoices"]![0]!;
        var body = new JsonObject { ["Invoices"] = new JsonArray { bill.DeepClone(), bill.DeepClone() } };

        var reply = await kit.PutAsync("Invoices", body);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(2, kit.Simulator.All("Invoices").Count);
        Assert.Empty(kit.Simulator.Violations);
    }

    private static JsonObject Batch(string resource, string contactId, string key)
    {
        JsonNode element = resource switch
        {
            "Contacts" => new JsonObject { ["Name"] = key },
            "Invoices" => SimulatorTestKit.Invoice(contactId, key)["Invoices"]![0]!,
            "Quotes" => SimulatorTestKit.Quote(contactId, key)["Quotes"]![0]!,
            _ => SimulatorTestKit.PurchaseOrder(contactId, key)["PurchaseOrders"]![0]!,
        };
        return new JsonObject { [resource] = new JsonArray { element.DeepClone(), element.DeepClone() } };
    }

    // ------------------------------------------------------------ where= with quoted text

    [Theory]
    [InlineData("Barnes AND Noble")]
    [InlineData("Barnes && Noble")]
    [InlineData("Barnes OR Noble")]
    [InlineData("Barnes || Noble")]
    public async Task A_where_value_in_quotes_is_never_split_or_read_as_OR(string name)
    {
        using var kit = new SimulatorTestKit();
        var id = kit.Simulator.SeedContact(name);
        kit.Simulator.SeedContact("Barnes");

        var reply = await kit.GetAsync("Contacts?where=" + Uri.EscapeDataString($"Name==\"{name}\""));

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(id, Assert.Single(reply.Items("Contacts"))["ContactID"]!.GetValue<string>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData("Name==\"A\" OR Name==\"B\"")]
    [InlineData("Name==\"A\"||Name==\"B\"")]
    public async Task An_OR_outside_quotes_is_still_refused(string where)
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Contacts?where=" + Uri.EscapeDataString(where));

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(XeroSimulatorRules.Where, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public void SplitWhere_splits_on_AND_and_ampersands_only_outside_quotes()
    {
        var clauses = XeroWire.SplitWhere("Name==\"Barnes AND Noble\" AND Status==\"ACTIVE\"&&Name.Contains(\"a && b\")", out var hasOr);

        Assert.False(hasOr);
        Assert.Equal(["Name==\"Barnes AND Noble\"", "Status==\"ACTIVE\"", "Name.Contains(\"a && b\")"], clauses.Select(c => c.Trim()));
    }

    // ------------------------------------------------------------ D7 / SentToContact on writes refused early

    [Fact]
    public async Task A_write_to_a_live_organisation_refused_early_is_still_a_D7_breach()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(IsDemoCompany: false, MinuteLimit: 6));
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var notFound = await kit.SendAsync(kit.Request(HttpMethod.Put, "Payments", new JsonObject { ["Amount"] = 1 }));
        var notAllowed = await kit.SendAsync(kit.Request(HttpMethod.Delete, "Invoices/00000000-0000-0000-0000-000000000001"));
        var tooLong = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId), new string('k', 129));
        for (var i = 0; i < 3; i++)
            await kit.GetAsync("Organisation");
        var limited = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId));

        Assert.Equal(HttpStatusCode.NotFound, notFound.Status);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, notAllowed.Status);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.Status);
        Assert.Equal((HttpStatusCode)429, limited.Status);
        Assert.Equal(
            [
                XeroSimulatorRules.LiveOrganisationWrite, XeroSimulatorRules.WriteAllowList,
                XeroSimulatorRules.LiveOrganisationWrite, XeroSimulatorRules.Method,
                XeroSimulatorRules.LiveOrganisationWrite, XeroSimulatorRules.IdempotencyKeyTooLong,
                XeroSimulatorRules.LiveOrganisationWrite,
            ],
            kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task A_write_to_a_live_organisation_refused_for_scope_is_still_a_D7_breach()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(IsDemoCompany: false, GrantedScopes: ["accounting.contacts.read"]));

        var reply = await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "New Ltd" });

        Assert.Equal(HttpStatusCode.Forbidden, reply.Status);
        Assert.Equal([XeroSimulatorRules.LiveOrganisationWrite, XeroSimulatorRules.Scope], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task SentToContact_on_a_write_refused_early_is_still_recorded()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId);
        body["Invoices"]![0]!["SentToContact"] = true;

        var reply = await kit.PutAsync("Invoices", body, new string('k', 129));

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal([XeroSimulatorRules.SentToContact, XeroSimulatorRules.IdempotencyKeyTooLong], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task An_exact_replay_to_a_live_organisation_is_not_recorded_twice()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(IsDemoCompany: false));
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId), "tos:replay");
        await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId), "tos:replay");

        Assert.Equal(XeroSimulatorRules.LiveOrganisationWrite, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Single(kit.Simulator.All("Invoices"));
    }

    // ------------------------------------------------------------ D3 on refused creates

    [Theory]
    [InlineData("BILLED")]
    [InlineData("DELETED")]
    public async Task A_purchase_order_created_as_a_status_Xero_refuses_is_still_a_D3_breach(string status)
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Metals Ltd");

        var reply = await kit.PutAsync("PurchaseOrders", SimulatorTestKit.PurchaseOrder(supplier, status: status));

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Empty(kit.Simulator.All("PurchaseOrders"));
        Assert.Equal(
            [XeroSimulatorRules.PurchaseOrderStatusNotDraft, XeroSimulatorRules.PurchaseOrderTransition],
            kit.Simulator.Violations.Select(v => v.Rule));
    }

    // ------------------------------------------------------------ faults are not used up by refused requests

    [Fact]
    public async Task A_fault_is_not_used_up_by_a_request_refused_429_concurrent()
    {
        using var kit = new SimulatorTestKit();
        using var invoker = new HttpMessageInvoker(kit.Simulator, disposeHandler: false);
        var held = new List<Task<HttpResponseMessage>>();

        using (kit.Simulator.HoldRequests())
        {
            for (var i = 0; i < 5; i++)
                held.Add(invoker.SendAsync(kit.Request(HttpMethod.Get, XeroApiSimulator.BaseAddress + "Organisation"), CancellationToken.None));

            kit.Simulator.Inject(new XeroFault(XeroFaultKind.DropResponseAfterCommit));

            using var sixth = await invoker.SendAsync(kit.Request(HttpMethod.Get, XeroApiSimulator.BaseAddress + "Organisation"), CancellationToken.None);
            Assert.Equal((HttpStatusCode)429, sixth.StatusCode);
        }

        foreach (var task in held)
        {
            using var response = await task;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => kit.GetAsync("Organisation"));
        Assert.Equal(HttpStatusCode.OK, (await kit.GetAsync("Organisation")).Status);
    }

    // ------------------------------------------------------------ strict dates

    [Theory]
    [InlineData("2026-10-02", true)]
    [InlineData("2026-10-02T00:00:00", true)]
    [InlineData("2026-10-02T23:59:59", true)]
    [InlineData("/Date(1790899200000+0000)/", true)]
    [InlineData("10/02/2026", false)]
    [InlineData("02/10/2026", false)]
    [InlineData("2026-10-02T23:30:00-05:00", false)]
    [InlineData("2026-10-02T00:00:00Z", false)]
    [InlineData("2 October 2026", false)]
    [InlineData("2026-10-2", false)]
    public void Only_ISO_and_MS_JSON_dates_parse(string raw, bool ok)
    {
        Assert.Equal(ok, XeroWire.TryParseDate(raw, out var date));
        if (ok)
            Assert.Equal(new DateOnly(2026, 10, 2), date);
    }

    [Fact]
    public async Task An_invoice_with_a_culture_formatted_date_is_refused()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId);
        body["Invoices"]![0]!["Date"] = "10/02/2026";

        var reply = await kit.PutAsync("Invoices", body);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(XeroSimulatorRules.FieldFormat, Assert.Single(kit.Simulator.Violations).Rule);
    }

    // ------------------------------------------------------------ tax follows LineAmountTypes

    [Fact]
    public async Task Computed_tax_is_recomputed_when_only_LineAmountTypes_changes()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);
        Assert.Equal(60m, kit.Simulator.Find("Invoices", id)!.Body["TotalTax"]!.GetValue<decimal>());

        var noTax = await kit.PostAsync($"Invoices/{id}", new JsonObject { ["InvoiceID"] = id, ["LineAmountTypes"] = "NoTax" });
        var inclusive = await kit.PostAsync($"Invoices/{id}", new JsonObject { ["InvoiceID"] = id, ["LineAmountTypes"] = "Inclusive" });

        var afterNoTax = noTax.First("Invoices");
        Assert.Equal(0m, afterNoTax["TotalTax"]!.GetValue<decimal>());
        Assert.Equal(0m, afterNoTax["LineItems"]![0]!["TaxAmount"]!.GetValue<decimal>());
        Assert.Equal(300m, afterNoTax["Total"]!.GetValue<decimal>());

        var afterInclusive = inclusive.First("Invoices");
        Assert.Equal(50m, afterInclusive["TotalTax"]!.GetValue<decimal>());
        Assert.Equal(250m, afterInclusive["SubTotal"]!.GetValue<decimal>());
        Assert.Equal(300m, afterInclusive["Total"]!.GetValue<decimal>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task A_sent_TaxAmount_survives_a_change_of_LineAmountTypes()
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Rail Co");
        var line = SimulatorTestKit.Line("Train to Leeds", 1m, 100m, "INPUT2", "493");
        line["TaxAmount"] = 19.99m;
        var id = (await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(supplier, "EXP-1", "ACCPAY", line: line))).First("Invoices")["InvoiceID"]!.GetValue<string>();

        var updated = (await kit.PostAsync($"Invoices/{id}", new JsonObject { ["InvoiceID"] = id, ["LineAmountTypes"] = "Inclusive" })).First("Invoices");

        Assert.Equal(19.99m, updated["TotalTax"]!.GetValue<decimal>());
        Assert.Equal(19.99m, updated["LineItems"]![0]!["TaxAmount"]!.GetValue<decimal>());
    }
}

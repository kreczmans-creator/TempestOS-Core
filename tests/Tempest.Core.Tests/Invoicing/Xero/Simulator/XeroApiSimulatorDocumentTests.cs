using System.Net;
using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>
/// The simulator's document rules (design §3, §4, §10.1, task S1): required
/// fields, tax types and account codes, unique numbers, the quote
/// transition table (exhaustively), invoice and purchase order status
/// rules, attachments, lookups, the D3/D4/D7 detectors, and every
/// back-office act.
/// </summary>
public sealed class XeroApiSimulatorDocumentTests
{
    // ------------------------------------------------------------ contacts

    [Fact]
    public async Task A_contact_is_created_and_found_by_id_number_and_search()
    {
        using var kit = new SimulatorTestKit();
        var body = new JsonObject
        {
            ["Contacts"] = new JsonArray
            {
                new JsonObject { ["Name"] = "Acme Engineering Ltd", ["ContactNumber"] = "CUST-0042", ["TaxNumber"] = "GB999999973", ["EmailAddress"] = "accounts@acme.example" },
            },
        };

        var created = (await kit.PutAsync("Contacts?summarizeErrors=true", body)).First("Contacts");
        var id = created["ContactID"]!.GetValue<string>();

        Assert.Equal("ACTIVE", created["ContactStatus"]!.GetValue<string>());
        Assert.False(created["IsCustomer"]!.GetValue<bool>());
        Assert.Equal(id, (await kit.GetAsync($"Contacts/{id}")).First("Contacts")["ContactID"]!.GetValue<string>());
        Assert.Equal(id, (await kit.GetAsync("Contacts/CUST-0042")).First("Contacts")["ContactID"]!.GetValue<string>());
        foreach (var term in new[] { "acme", "CUST-0042", "accounts@acme" })
            Assert.Equal(id, Assert.Single((await kit.GetAsync($"Contacts?searchTerm={Uri.EscapeDataString(term)}")).Items("Contacts"))["ContactID"]!.GetValue<string>());
        Assert.Empty((await kit.GetAsync("Contacts?searchTerm=nobody")).Items("Contacts"));
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task A_contact_needs_a_name_and_the_refusal_is_in_Xeros_error_shape()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.PutAsync("Contacts", new JsonObject { ["Contacts"] = new JsonArray { new JsonObject { ["EmailAddress"] = "x@y.z" } } });

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(10, reply.Json!["ErrorNumber"]!.GetValue<int>());
        Assert.Equal("ValidationException", reply.Json!["Type"]!.GetValue<string>());
        Assert.Equal("The contact Name is required.", Assert.Single(reply.ValidationMessages()));
        Assert.Equal(XeroSimulatorRules.RequiredField, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Empty(kit.Simulator.All("Contacts"));
    }

    [Fact]
    public async Task A_contact_name_is_unique_case_insensitively_among_active_contacts()
    {
        using var kit = new SimulatorTestKit();
        var archived = kit.Simulator.SeedContact("Old Supplier Ltd");
        kit.Simulator.ArchiveContactInXero(archived);
        kit.Simulator.SeedContact("Acme Ltd");

        var duplicate = await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "ACME LTD" });
        var reuseOfArchived = await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "Old Supplier Ltd" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.Status);
        Assert.Contains("must be unique", Assert.Single(duplicate.ValidationMessages()), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, reuseOfArchived.Status);
        Assert.Equal(XeroSimulatorRules.Duplicate, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task A_contact_number_longer_than_50_is_refused()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "Acme Ltd", ["ContactNumber"] = new string('9', 51) });

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(XeroSimulatorRules.FieldLength, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task A_contact_update_renames_it_but_not_onto_another_contacts_name()
    {
        using var kit = new SimulatorTestKit();
        var acme = kit.Simulator.SeedContact("Acme Ltd");
        kit.Simulator.SeedContact("Beta Ltd");

        var renamed = await kit.PostAsync($"Contacts/{acme}", new JsonObject { ["ContactID"] = acme, ["Name"] = "Acme Engineering Ltd" });
        var clash = await kit.PostAsync($"Contacts/{acme}", new JsonObject { ["ContactID"] = acme, ["Name"] = "beta ltd" });

        Assert.Equal("Acme Engineering Ltd", renamed.First("Contacts")["Name"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, clash.Status);
        Assert.Equal("Acme Engineering Ltd", kit.Simulator.Find("Contacts", acme)!.Body["Name"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_archived_contact_is_hidden_from_lists_and_refused_on_a_document()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Gone Ltd");
        kit.Simulator.ArchiveContactInXero(contactId);

        Assert.Empty((await kit.GetAsync("Contacts")).Items("Contacts"));
        Assert.Single((await kit.GetAsync("Contacts?includeArchived=true")).Items("Contacts"));
        var invoice = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId));
        Assert.Equal(HttpStatusCode.BadRequest, invoice.Status);
        Assert.Contains("archived", Assert.Single(invoice.ValidationMessages()), StringComparison.Ordinal);
        Assert.Equal(XeroSimulatorRules.UnknownContact, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task A_contact_deleted_in_Xero_answers_404_by_id()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Gone Ltd", contactNumber: "CUST-1");

        kit.Simulator.DeleteInXero("Contacts", contactId);

        Assert.Equal(HttpStatusCode.NotFound, (await kit.GetAsync($"Contacts/{contactId}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await kit.GetAsync("Contacts/CUST-1")).Status);
        Assert.Empty((await kit.GetAsync("Contacts?includeArchived=true")).Items("Contacts"));
        Assert.Equal("DELETED", kit.Simulator.Find("Contacts", contactId)!.Status);
    }

    [Fact]
    public void SeedContact_refuses_a_duplicate_active_name()
    {
        using var kit = new SimulatorTestKit();
        var id = kit.Simulator.SeedContact("Acme Ltd", taxNumber: "GB1", contactNumber: "C1");

        Assert.Throws<InvalidOperationException>(() => kit.Simulator.SeedContact("acme ltd"));
        var seeded = kit.Simulator.Find("Contacts", id)!;
        Assert.Equal("C1", seeded.Number);
        Assert.Equal("GB1", seeded.Body["TaxNumber"]!.GetValue<string>());
        Assert.Empty(kit.Simulator.Requests);
    }

    // ------------------------------------------------------------ invoices and bills

    [Fact]
    public async Task A_sales_invoice_is_created_as_a_draft_with_totals()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var invoice = (await kit.PutAsync("Invoices?summarizeErrors=true", SimulatorTestKit.Invoice(contactId))).First("Invoices");

        Assert.Equal("DRAFT", invoice["Status"]!.GetValue<string>());
        Assert.Equal("ACCREC", invoice["Type"]!.GetValue<string>());
        Assert.Equal("P0012-INV-001", invoice["InvoiceNumber"]!.GetValue<string>());
        Assert.Equal(300m, invoice["SubTotal"]!.GetValue<decimal>());
        Assert.Equal(60m, invoice["TotalTax"]!.GetValue<decimal>());
        Assert.Equal(360m, invoice["Total"]!.GetValue<decimal>());
        Assert.Equal(360m, invoice["AmountDue"]!.GetValue<decimal>());
        Assert.False(invoice["SentToContact"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(invoice["LineItems"]![0]!["LineItemID"]!.GetValue<string>()));
        Assert.Equal(contactId, invoice["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.True(kit.Simulator.Find("Contacts", contactId)!.Body["IsCustomer"]!.GetValue<bool>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task A_sales_invoice_without_a_number_is_auto_numbered()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var invoice = (await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, number: null))).First("Invoices");

        Assert.Equal("INV-0001", invoice["InvoiceNumber"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_bill_keeps_the_receipts_VAT_and_marks_the_contact_a_supplier()
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Rail Co");
        var line = SimulatorTestKit.Line("Train to Leeds", 1m, 100m, "INPUT2", "493");
        line["TaxAmount"] = 19.99m;

        var bill = (await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(supplier, "EXP-1", "ACCPAY", line: line))).First("Invoices");

        Assert.Equal("ACCPAY", bill["Type"]!.GetValue<string>());
        Assert.Equal("DRAFT", bill["Status"]!.GetValue<string>());
        Assert.Equal(119.99m, bill["Total"]!.GetValue<decimal>());
        Assert.True(kit.Simulator.Find("Contacts", supplier)!.Body["IsSupplier"]!.GetValue<bool>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData("no-type", XeroSimulatorRules.RequiredField)]
    [InlineData("bad-type", XeroSimulatorRules.FieldFormat)]
    [InlineData("no-contact", XeroSimulatorRules.RequiredField)]
    [InlineData("empty-contact", XeroSimulatorRules.RequiredField)]
    [InlineData("unknown-contact", XeroSimulatorRules.UnknownContact)]
    [InlineData("no-description", XeroSimulatorRules.RequiredField)]
    [InlineData("bad-quantity", XeroSimulatorRules.FieldFormat)]
    [InlineData("bad-date", XeroSimulatorRules.FieldFormat)]
    [InlineData("bad-amount-types", XeroSimulatorRules.FieldFormat)]
    [InlineData("long-reference", XeroSimulatorRules.FieldLength)]
    public async Task An_invoice_breaking_a_required_field_rule_is_refused(string breakage, string rule)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId);
        var invoice = body["Invoices"]![0]!.AsObject();
        var line = invoice["LineItems"]![0]!.AsObject();
        switch (breakage)
        {
            case "no-type": invoice.Remove("Type"); break;
            case "bad-type": invoice["Type"] = "RECEIPT"; break;
            case "no-contact": invoice.Remove("Contact"); break;
            case "empty-contact": invoice["Contact"] = new JsonObject(); break;
            case "unknown-contact": invoice["Contact"] = new JsonObject { ["ContactID"] = "00000000-0000-0000-0000-0000000000aa" }; break;
            case "no-description": line.Remove("Description"); break;
            case "bad-quantity": line["Quantity"] = "two"; break;
            case "bad-date": invoice["Date"] = "not-a-date"; break;
            case "bad-amount-types": invoice["LineAmountTypes"] = "Gross"; break;
            case "long-reference": invoice["Reference"] = new string('r', 256); break;
        }

        var reply = await kit.PutAsync("Invoices?summarizeErrors=true", body);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.NotEmpty(reply.ValidationMessages());
        Assert.True(reply.Json!["Elements"]![0]!["HasErrors"]!.GetValue<bool>());
        Assert.Equal(rule, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Empty(kit.Simulator.All("Invoices"));
    }

    [Theory]
    [InlineData("ACCREC", "OUTPUT2", true)]
    [InlineData("ACCREC", "RROUTPUT", true)]
    [InlineData("ACCREC", "NONE", true)]
    [InlineData("ACCREC", "INPUT2", false)]
    [InlineData("ACCREC", "OUTPUT", false)]
    [InlineData("ACCREC", "NOSUCH", false)]
    [InlineData("ACCPAY", "INPUT2", true)]
    [InlineData("ACCPAY", "EXEMPTINPUT", true)]
    [InlineData("ACCPAY", "NONE", true)]
    [InlineData("ACCPAY", "OUTPUT2", false)]
    public async Task A_line_tax_type_must_exist_be_active_and_apply_to_the_side(string type, string taxType, bool accepted)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var line = SimulatorTestKit.Line(taxType: taxType, accountCode: type == "ACCREC" ? "200" : "429");

        var reply = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, type: type, line: line));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, reply.Status);
        if (!accepted)
            Assert.Equal(XeroSimulatorRules.TaxType, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Theory]
    [InlineData("200", true)]
    [InlineData("493", true)]
    [InlineData("999", false)]
    [InlineData("499", false)]
    [InlineData("090", false)]
    public async Task A_line_account_code_must_exist_be_active_and_not_a_bank(string code, bool accepted)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var reply = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, line: SimulatorTestKit.Line(accountCode: code)));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, reply.Status);
        if (!accepted)
            Assert.Equal(XeroSimulatorRules.AccountCode, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task An_archived_account_is_refused_from_then_on()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        kit.Simulator.ArchiveAccountInXero("200");

        var reply = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId));

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Contains("ARCHIVED", Assert.Single(reply.ValidationMessages()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sales_invoice_number_is_unique_until_deleted_and_bill_numbers_need_not_be()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var first = await kit.CreateInvoiceAsync(contactId, "INV-77");

        var duplicate = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, "inv-77"));
        await kit.PostAsync($"Invoices/{first}", new JsonObject { ["InvoiceID"] = first, ["Status"] = "DELETED" });
        var afterDelete = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, "INV-77"));
        var bill1 = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, "EXP-1", "ACCPAY"));
        var bill2 = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, "EXP-1", "ACCPAY"));

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.Status);
        Assert.Contains("Invoice # must be unique", Assert.Single(duplicate.ValidationMessages()), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, afterDelete.Status);
        Assert.Equal(HttpStatusCode.OK, bill1.Status);
        Assert.Equal(HttpStatusCode.OK, bill2.Status);
        Assert.Equal([XeroSimulatorRules.Duplicate], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Theory]
    [InlineData("SUBMITTED", true)]
    [InlineData("AUTHORISED", true)]
    [InlineData("PAID", false)]
    [InlineData("VOIDED", false)]
    [InlineData("DELETED", false)]
    public async Task An_invoice_created_other_than_DRAFT_is_a_D3_breach_or_refused(string status, bool appliedAsXeroWould)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var reply = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(contactId, status: status));

        if (appliedAsXeroWould)
        {
            Assert.Equal(HttpStatusCode.OK, reply.Status);
            Assert.Equal(status, Assert.Single(kit.Simulator.All("Invoices")).Status);
            Assert.Equal(XeroSimulatorRules.InvoiceStatusNotDraft, Assert.Single(kit.Simulator.Violations).Rule);
        }
        else
        {
            // Refused by Xero, but still a D3 breach: TempestOS creates only DRAFT.
            Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
            Assert.Empty(kit.Simulator.All("Invoices"));
            Assert.Equal(
                [XeroSimulatorRules.InvoiceStatusNotDraft, XeroSimulatorRules.InvoiceTransition],
                kit.Simulator.Violations.Select(v => v.Rule));
        }
    }

    public static TheoryData<string, string, bool, bool> InvoiceTransitionCases() => new()
    {
        { "DRAFT", "DRAFT", true, false },
        { "DRAFT", "SUBMITTED", true, true },
        { "DRAFT", "AUTHORISED", true, true },
        { "DRAFT", "DELETED", true, false },
        { "DRAFT", "PAID", false, false },
        { "DRAFT", "VOIDED", false, false },
        { "SUBMITTED", "DRAFT", true, false },
        { "SUBMITTED", "DELETED", true, false },
        { "SUBMITTED", "AUTHORISED", true, true },
        { "AUTHORISED", "VOIDED", true, true },
        { "AUTHORISED", "DELETED", false, false },
        { "AUTHORISED", "DRAFT", false, false },
        { "AUTHORISED", "PAID", false, false },
        { "PAID", "VOIDED", false, false },
        { "VOIDED", "DELETED", false, false },
        { "DELETED", "DRAFT", false, false },
    };

    [Theory]
    [MemberData(nameof(InvoiceTransitionCases))]
    public async Task Invoice_status_changes_follow_Xeros_rules_and_any_beyond_DRAFT_or_DELETED_is_D3(string from, string to, bool allowed, bool d3)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);
        switch (from)
        {
            case "SUBMITTED": await kit.PostAsync($"Invoices/{id}", new JsonObject { ["InvoiceID"] = id, ["Status"] = "SUBMITTED" }); break;
            case "AUTHORISED": kit.Simulator.ApproveInXero(id); break;
            case "PAID": kit.Simulator.ApproveInXero(id); kit.Simulator.PayInXero(id, new DateOnly(2026, 10, 20)); break;
            case "VOIDED": kit.Simulator.ApproveInXero(id); kit.Simulator.VoidInXero(id); break;
            case "DELETED": kit.Simulator.DeleteInXero("Invoices", id); break;
        }

        Assert.Equal(from, kit.Simulator.Find("Invoices", id)!.Status);
        var before = kit.Simulator.Violations.Count;

        var reply = await kit.PostAsync($"Invoices/{id}?summarizeErrors=true", new JsonObject { ["Invoices"] = new JsonArray { new JsonObject { ["InvoiceID"] = id, ["Status"] = to } } });

        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(allowed ? to : from, kit.Simulator.Find("Invoices", id)!.Status);
        var rules = kit.RulesSince(before);
        if (allowed)
            Assert.Equal(d3 ? [XeroSimulatorRules.InvoiceStatusNotDraft] : [], rules);
        else
            Assert.Equal([from is "PAID" or "VOIDED" or "DELETED" ? XeroSimulatorRules.NotEditable : XeroSimulatorRules.InvoiceTransition], rules);
    }

    [Fact]
    public async Task Invoice_content_changes_only_while_DRAFT_or_SUBMITTED()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);
        JsonObject Update() => new()
        {
            ["Invoices"] = new JsonArray
            {
                new JsonObject { ["InvoiceID"] = id, ["LineItems"] = new JsonArray { SimulatorTestKit.Line("Revised", 1m, 500m) } },
            },
        };

        var whileDraft = await kit.PostAsync($"Invoices/{id}", Update());
        kit.Simulator.ApproveInXero(id);
        var afterApproval = await kit.PostAsync($"Invoices/{id}", Update());

        Assert.Equal(HttpStatusCode.OK, whileDraft.Status);
        Assert.Equal(600m, whileDraft.First("Invoices")["Total"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.BadRequest, afterApproval.Status);
        Assert.Equal(XeroSimulatorRules.NotEditable, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Equal("AUTHORISED", kit.Simulator.Find("Invoices", id)!.Status);
    }

    [Fact]
    public async Task An_update_carrying_unchanged_fields_is_not_a_content_change()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);
        kit.Simulator.ApproveInXero(id);

        var reply = await kit.PostAsync($"Invoices/{id}", new JsonObject
        {
            ["InvoiceID"] = id,
            ["InvoiceNumber"] = "P0012-INV-001",
            ["Type"] = "ACCREC",
            ["Contact"] = new JsonObject { ["ContactID"] = contactId },
            ["Date"] = "2026-10-02T00:00:00",
            ["CurrencyCode"] = "GBP",
        });

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task An_update_to_an_unknown_invoice_is_404()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.PostAsync("Invoices/00000000-0000-0000-0000-0000000000ff", new JsonObject { ["Status"] = "DELETED" });

        Assert.Equal(HttpStatusCode.NotFound, reply.Status);
    }

    [Fact]
    public async Task Invoice_lookups_TempestOS_uses_find_the_right_records()
    {
        using var kit = new SimulatorTestKit();
        var acme = kit.Simulator.SeedContact("Acme Ltd");
        var rail = kit.Simulator.SeedContact("Rail Co");
        var sales = await kit.PutAsync("Invoices", SimulatorTestKit.Invoice(acme, "INV-1"));
        var salesId = sales.First("Invoices")["InvoiceID"]!.GetValue<string>();
        await kit.PostAsync($"Invoices/{salesId}", new JsonObject { ["InvoiceID"] = salesId, ["Reference"] = "P0012 · Stage 1" });
        var other = await kit.CreateInvoiceAsync(acme, "INV-2");
        var bill = await kit.CreateInvoiceAsync(rail, "EXP-9", "ACCPAY");
        kit.Simulator.ApproveInXero(bill);

        string[] Ids(SimulatorReply reply) => [.. reply.Items("Invoices").Select(i => i["InvoiceID"]!.GetValue<string>())];

        Assert.Equal([salesId], Ids(await kit.GetAsync("Invoices?InvoiceNumbers=INV-1")));
        Assert.Equal([salesId, other], Ids(await kit.GetAsync($"Invoices?IDs={salesId},{other}")));
        Assert.Equal([bill], Ids(await kit.GetAsync($"Invoices?InvoiceNumbers=EXP-9&ContactIDs={rail}")));
        Assert.Equal([bill], Ids(await kit.GetAsync("Invoices?Statuses=AUTHORISED")));
        Assert.Equal([salesId], Ids(await kit.GetAsync("Invoices?where=" + Uri.EscapeDataString("Reference==\"P0012 · Stage 1\""))));
        Assert.Equal([bill], Ids(await kit.GetAsync("Invoices?where=" + Uri.EscapeDataString("Type==\"ACCPAY\"&&Status==\"AUTHORISED\""))));
        Assert.Equal([bill], Ids(await kit.GetAsync("Invoices?where=" + Uri.EscapeDataString($"Contact.ContactID==Guid(\"{rail}\")"))));
        Assert.Equal([other], Ids(await kit.GetAsync("Invoices/INV-2")));
        Assert.Equal([other], Ids(await kit.GetAsync($"Invoices/{other}")));
        Assert.Equal(HttpStatusCode.NotFound, (await kit.GetAsync("Invoices/INV-404")).Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task A_where_clause_the_simulator_does_not_understand_is_400_and_recorded()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.GetAsync("Invoices?where=" + Uri.EscapeDataString("Date>=DateTime(2026,01,01)"));

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(XeroSimulatorRules.Where, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task With_summarizeErrors_true_one_bad_element_refuses_the_batch()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId, "INV-1");
        var bad = SimulatorTestKit.Invoice(contactId, "INV-2")["Invoices"]![0]!.DeepClone().AsObject();
        bad.Remove("Type");
        body["Invoices"]!.AsArray().Add(bad);

        var reply = await kit.PutAsync("Invoices?summarizeErrors=true", body);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Single(reply.Json!["Elements"]!.AsArray());
        Assert.Empty(kit.Simulator.All("Invoices"));
    }

    [Fact]
    public async Task With_summarizeErrors_false_errors_come_back_per_element_in_a_200()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId, "INV-1");
        var bad = SimulatorTestKit.Invoice(contactId, "INV-2")["Invoices"]![0]!.DeepClone().AsObject();
        bad.Remove("Type");
        body["Invoices"]!.AsArray().Add(bad);

        var reply = await kit.PutAsync("Invoices?summarizeErrors=false", body);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        var items = reply.Items("Invoices");
        Assert.Equal("OK", items[0]["StatusAttributeString"]!.GetValue<string>());
        Assert.True(items[1]["HasErrors"]!.GetValue<bool>());
        Assert.Equal("ERROR", items[1]["StatusAttributeString"]!.GetValue<string>());
        Assert.NotEmpty(items[1]["ValidationErrors"]!.AsArray());
        Assert.Single(kit.Simulator.All("Invoices"));
    }

    [Fact]
    public async Task A_body_that_is_not_the_expected_JSON_is_400()
    {
        using var kit = new SimulatorTestKit();
        var request = kit.Request(HttpMethod.Put, "Invoices");
        request.Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var malformed = await kit.SendAsync(request);
        var empty = await kit.PutAsync("Invoices", new JsonObject { ["Invoices"] = new JsonArray() });

        Assert.Equal(HttpStatusCode.BadRequest, malformed.Status);
        Assert.Equal(HttpStatusCode.BadRequest, empty.Status);
        Assert.All(kit.Simulator.Violations, v => Assert.Equal(XeroSimulatorRules.MalformedBody, v.Rule));
    }

    [Fact]
    public async Task A_contact_given_by_name_alone_is_matched_or_created_as_Xero_does_but_recorded()
    {
        using var kit = new SimulatorTestKit();
        var existing = kit.Simulator.SeedContact("Acme Ltd");
        JsonObject ByName(string name, string number)
        {
            var body = SimulatorTestKit.Invoice("unused", number);
            body["Invoices"]![0]!["Contact"] = new JsonObject { ["Name"] = name };
            return body;
        }

        var matched = (await kit.PostAsync("Invoices", ByName("acme ltd", "INV-1"))).First("Invoices");
        var created = (await kit.PostAsync("Invoices", ByName("Brand New Ltd", "INV-2"))).First("Invoices");

        Assert.Equal(existing, matched["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal(2, kit.Simulator.All("Contacts").Count);
        Assert.Equal("Brand New Ltd", created["Contact"]!["Name"]!.GetValue<string>());
        Assert.All(kit.Simulator.Violations, v => Assert.Equal(XeroSimulatorRules.ContactByName, v.Rule));
        Assert.Equal(2, kit.Simulator.Violations.Count);
    }

    // ------------------------------------------------------------ D3 / D4 / D7 detectors

    [Fact]
    public async Task SentToContact_true_is_a_D4_breach()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId);
        body["Invoices"]![0]!["SentToContact"] = true;

        var reply = await kit.PutAsync("Invoices", body);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(XeroSimulatorRules.SentToContact, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.True(Assert.Single(kit.Simulator.All("Invoices")).Body["SentToContact"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SentToContact_false_is_no_breach()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Invoice(contactId);
        body["Invoices"]![0]!["SentToContact"] = false;

        await kit.PutAsync("Invoices", body);

        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData("Invoices/{id}/Email", HttpStatusCode.NoContent)]
    [InlineData("Invoices/00000000-0000-0000-0000-0000000000ee/Email", HttpStatusCode.NotFound)]
    [InlineData("Quotes/{id}/Email", HttpStatusCode.NotFound)]
    public async Task Any_Email_endpoint_is_a_D4_breach(string path, HttpStatusCode expected)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);

        var reply = await kit.PostAsync(path.Replace("{id}", id, StringComparison.Ordinal), new JsonObject());

        Assert.Equal(expected, reply.Status);
        Assert.Equal(XeroSimulatorRules.EmailEndpoint, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task Every_write_to_a_live_organisation_is_a_D7_breach_and_reads_are_not()
    {
        using var kit = new SimulatorTestKit(new XeroSimulatorOptions(IsDemoCompany: false));
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        await kit.GetAsync("Organisation");
        await kit.GetAsync("Invoices");
        Assert.Empty(kit.Simulator.Violations);

        var id = await kit.CreateInvoiceAsync(contactId);
        await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "New Ltd" });
        await kit.UploadAsync(HttpMethod.Put, $"Invoices/{id}/Attachments/a.pdf", [1, 2, 3]);

        Assert.Equal(3, kit.Simulator.Violations.Count);
        Assert.All(kit.Simulator.Violations, v => Assert.Equal(XeroSimulatorRules.LiveOrganisationWrite, v.Rule));
    }

    // ------------------------------------------------------------ quotes

    [Theory]
    [InlineData("no-contact")]
    [InlineData("no-date")]
    [InlineData("no-lines")]
    [InlineData("empty-lines")]
    public async Task A_quote_needs_a_contact_a_date_and_lines(string breakage)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var body = SimulatorTestKit.Quote(contactId);
        var quote = body["Quotes"]![0]!.AsObject();
        switch (breakage)
        {
            case "no-contact": quote.Remove("Contact"); break;
            case "no-date": quote.Remove("Date"); break;
            case "no-lines": quote.Remove("LineItems"); break;
            case "empty-lines": quote["LineItems"] = new JsonArray(); break;
        }

        var reply = await kit.PutAsync("Quotes", body);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(XeroSimulatorRules.RequiredField, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Fact]
    public async Task A_quote_is_created_as_a_draft_with_a_unique_number_and_sales_tax_only()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var created = (await kit.PutAsync("Quotes", SimulatorTestKit.Quote(contactId))).First("Quotes");
        var duplicate = await kit.PutAsync("Quotes", SimulatorTestKit.Quote(contactId, "p0012-q-001"));
        var inputTax = SimulatorTestKit.Quote(contactId, "Q-2");
        inputTax["Quotes"]![0]!["LineItems"]![0]!["TaxType"] = "INPUT2";
        var refusedTax = await kit.PutAsync("Quotes", inputTax);
        var autoNumbered = (await kit.PutAsync("Quotes", SimulatorTestKit.Quote(contactId, number: null))).First("Quotes");

        Assert.Equal("DRAFT", created["Status"]!.GetValue<string>());
        Assert.Equal("Bracket redesign", created["Title"]!.GetValue<string>());
        Assert.Equal(360m, created["Total"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.Status);
        Assert.Equal(HttpStatusCode.BadRequest, refusedTax.Status);
        Assert.Equal("QU-0001", autoNumbered["QuoteNumber"]!.GetValue<string>());
        Assert.Equal([XeroSimulatorRules.Duplicate, XeroSimulatorRules.TaxType], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Theory]
    [InlineData("DRAFT", true)]
    [InlineData("SENT", true)]
    [InlineData("ACCEPTED", false)]
    [InlineData("DECLINED", false)]
    [InlineData("INVOICED", false)]
    [InlineData("DELETED", false)]
    public async Task A_quote_can_be_created_only_as_DRAFT_or_SENT(string status, bool accepted)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");

        var reply = await kit.PutAsync("Quotes", SimulatorTestKit.Quote(contactId, status: status));

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, reply.Status);
        if (!accepted)
            Assert.Equal(XeroSimulatorRules.QuoteTransition, Assert.Single(kit.Simulator.Violations).Rule);
    }

    private static readonly string[] QuoteStatuses = ["DRAFT", "SENT", "ACCEPTED", "DECLINED", "INVOICED", "DELETED"];

    /// <summary>Design §4.1, written out independently of the simulator's own table; <c>INVOICED</c> is never a target through the API.</summary>
    private static readonly Dictionary<string, string[]> ExpectedQuoteTransitions = new()
    {
        ["DRAFT"] = ["SENT", "DELETED"],
        ["SENT"] = ["ACCEPTED", "DECLINED", "DELETED"],
        ["ACCEPTED"] = ["SENT", "DELETED"],
        ["DECLINED"] = ["SENT", "DELETED"],
        ["INVOICED"] = ["SENT", "DELETED"],
        ["DELETED"] = [],
    };

    public static TheoryData<string, string, bool> QuoteTransitionCases()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var from in QuoteStatuses)
        {
            foreach (var to in QuoteStatuses)
                data.Add(from, to, from == to ? from != "DELETED" : ExpectedQuoteTransitions[from].Contains(to));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(QuoteTransitionCases))]
    public async Task The_quote_transition_table_is_enforced_exhaustively(string from, string to, bool allowed)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateQuoteAsync(contactId);
        await ReachQuoteStatusAsync(kit, id, contactId, from);
        Assert.Equal(from, kit.Simulator.Find("Quotes", id)!.Status);
        Assert.Empty(kit.Simulator.Violations);

        var reply = await kit.SetQuoteStatusAsync(id, contactId, to);

        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(allowed ? to : from, kit.Simulator.Find("Quotes", id)!.Status);
        if (allowed)
            Assert.Empty(kit.Simulator.Violations);
        else
            Assert.Equal(from == "DELETED" ? XeroSimulatorRules.NotEditable : XeroSimulatorRules.QuoteTransition, Assert.Single(kit.Simulator.Violations).Rule);
    }

    private static async Task ReachQuoteStatusAsync(SimulatorTestKit kit, string id, string contactId, string status)
    {
        string[] walk = status switch
        {
            "SENT" => ["SENT"],
            "ACCEPTED" or "INVOICED" => ["SENT", "ACCEPTED"],
            "DECLINED" => ["SENT", "DECLINED"],
            _ => [],
        };
        foreach (var step in walk)
            Assert.Equal(HttpStatusCode.OK, (await kit.SetQuoteStatusAsync(id, contactId, step)).Status);
        if (status == "INVOICED")
            kit.Simulator.ConvertQuoteToInvoiceInXero(id);
        if (status == "DELETED")
            kit.Simulator.DeleteInXero("Quotes", id);
    }

    [Fact]
    public async Task A_quote_update_must_carry_contact_and_date()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateQuoteAsync(contactId);

        var reply = await kit.PostAsync($"Quotes/{id}", new JsonObject { ["QuoteID"] = id, ["Status"] = "SENT" });

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(XeroSimulatorRules.RequiredField, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Equal("DRAFT", kit.Simulator.Find("Quotes", id)!.Status);
    }

    [Fact]
    public async Task Quote_content_changes_only_while_DRAFT_and_a_status_only_update_keeps_the_lines()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateQuoteAsync(contactId);
        JsonObject Revision(string status)
        {
            var body = SimulatorTestKit.Quote(contactId, status: status);
            var quote = body["Quotes"]![0]!.AsObject();
            quote["QuoteID"] = id;
            quote["Reference"] = "R2";
            quote["LineItems"] = new JsonArray { SimulatorTestKit.Line("Revised scope", 3m, 150m) };
            return body;
        }

        var revisedAndSent = await kit.PostAsync($"Quotes/{id}", Revision("SENT"));
        var revisedAfterSent = await kit.PostAsync($"Quotes/{id}", Revision("SENT"));
        var accepted = await kit.SetQuoteStatusAsync(id, contactId, "ACCEPTED");

        Assert.Equal(HttpStatusCode.OK, revisedAndSent.Status);
        Assert.Equal(HttpStatusCode.BadRequest, revisedAfterSent.Status);
        Assert.Equal(XeroSimulatorRules.NotEditable, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        var quote = kit.Simulator.Find("Quotes", id)!;
        Assert.Equal("ACCEPTED", quote.Status);
        Assert.Equal("R2", quote.Body["Reference"]!.GetValue<string>());
        Assert.Equal("Revised scope", quote.Body["LineItems"]![0]!["Description"]!.GetValue<string>());
    }

    [Fact]
    public async Task Quotes_are_looked_up_by_number_query_and_by_id_but_not_by_number_in_the_path()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateQuoteAsync(contactId);
        await kit.CreateQuoteAsync(contactId, "P0013-Q-001");

        Assert.Equal(id, Assert.Single((await kit.GetAsync("Quotes?QuoteNumber=P0012-Q-001")).Items("Quotes"))["QuoteID"]!.GetValue<string>());
        Assert.Equal(id, (await kit.GetAsync($"Quotes/{id}")).First("Quotes")["QuoteID"]!.GetValue<string>());
        Assert.Equal(2, (await kit.GetAsync($"Quotes?ContactID={contactId}")).Items("Quotes").Count);
        Assert.Equal(HttpStatusCode.NotFound, (await kit.GetAsync("Quotes/P0012-Q-001")).Status);
    }

    // ------------------------------------------------------------ purchase orders

    [Fact]
    public async Task A_purchase_order_is_created_as_a_draft_and_found_by_number()
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Metals Ltd");

        var created = (await kit.PutAsync("PurchaseOrders?summarizeErrors=true", SimulatorTestKit.PurchaseOrder(supplier))).First("PurchaseOrders");
        var id = created["PurchaseOrderID"]!.GetValue<string>();

        Assert.Equal("DRAFT", created["Status"]!.GetValue<string>());
        Assert.Equal(120m, created["Total"]!.GetValue<decimal>());
        Assert.Equal("2026-10-16T00:00:00", created["DeliveryDateString"]!.GetValue<string>());
        Assert.Equal(id, (await kit.GetAsync("PurchaseOrders/P0012-PO-001")).First("PurchaseOrders")["PurchaseOrderID"]!.GetValue<string>());
        Assert.Equal(id, (await kit.GetAsync($"PurchaseOrders/{id}")).First("PurchaseOrders")["PurchaseOrderID"]!.GetValue<string>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData("no-contact", XeroSimulatorRules.RequiredField)]
    [InlineData("no-lines", XeroSimulatorRules.RequiredField)]
    [InlineData("sales-tax", XeroSimulatorRules.TaxType)]
    [InlineData("duplicate", XeroSimulatorRules.Duplicate)]
    public async Task A_purchase_order_breaking_a_rule_is_refused(string breakage, string rule)
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Metals Ltd");
        if (breakage == "duplicate")
            await kit.CreatePurchaseOrderAsync(supplier);
        var body = SimulatorTestKit.PurchaseOrder(supplier);
        var order = body["PurchaseOrders"]![0]!.AsObject();
        switch (breakage)
        {
            case "no-contact": order.Remove("Contact"); break;
            case "no-lines": order.Remove("LineItems"); break;
            case "sales-tax": order["LineItems"]![0]!["TaxType"] = "OUTPUT2"; break;
        }

        var reply = await kit.PutAsync("PurchaseOrders", body);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(rule, Assert.Single(kit.Simulator.Violations).Rule);
    }

    [Theory]
    [InlineData("SUBMITTED")]
    [InlineData("AUTHORISED")]
    public async Task A_purchase_order_written_beyond_DRAFT_is_a_D3_breach(string status)
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Metals Ltd");
        var created = await kit.PutAsync("PurchaseOrders", SimulatorTestKit.PurchaseOrder(supplier, "PO-A", status));
        var draft = await kit.CreatePurchaseOrderAsync(supplier, "PO-B");
        var moved = await kit.PostAsync($"PurchaseOrders/{draft}", new JsonObject { ["PurchaseOrderID"] = draft, ["Status"] = status });

        Assert.Equal(HttpStatusCode.OK, created.Status);
        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(
            [XeroSimulatorRules.PurchaseOrderStatusNotDraft, XeroSimulatorRules.PurchaseOrderStatusNotDraft],
            kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task A_draft_purchase_order_can_be_deleted_but_not_once_billed()
    {
        using var kit = new SimulatorTestKit();
        var supplier = kit.Simulator.SeedContact("Metals Ltd");
        var cancelled = await kit.CreatePurchaseOrderAsync(supplier, "PO-1");
        var billed = await kit.CreatePurchaseOrderAsync(supplier, "PO-2");
        kit.Simulator.ApproveInXero(billed);
        var billId = kit.Simulator.BillPurchaseOrderInXero(billed);

        var deleteDraft = await kit.PostAsync($"PurchaseOrders/{cancelled}", new JsonObject { ["PurchaseOrderID"] = cancelled, ["Status"] = "DELETED" });
        var deleteBilled = await kit.PostAsync($"PurchaseOrders/{billed}", new JsonObject { ["PurchaseOrderID"] = billed, ["Status"] = "DELETED" });

        Assert.Equal(HttpStatusCode.OK, deleteDraft.Status);
        Assert.Equal("DELETED", kit.Simulator.Find("PurchaseOrders", cancelled)!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, deleteBilled.Status);
        Assert.Contains("BILLED", Assert.Single(deleteBilled.ValidationMessages()), StringComparison.Ordinal);
        Assert.Equal(XeroSimulatorRules.PurchaseOrderTransition, Assert.Single(kit.Simulator.Violations).Rule);
        Assert.Equal("BILLED", kit.Simulator.Find("PurchaseOrders", billed)!.Status);
        var bill = kit.Simulator.Find("Invoices", billId)!;
        Assert.Equal("ACCPAY", bill.Body["Type"]!.GetValue<string>());
        Assert.Equal("DRAFT", bill.Status);
        Assert.Throws<InvalidOperationException>(() => kit.Simulator.DeleteInXero("PurchaseOrders", billed));
    }

    // ------------------------------------------------------------ attachments

    [Fact]
    public async Task A_PDF_is_attached_listed_and_read_back()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 1, 2, 3];

        var upload = await kit.UploadAsync(HttpMethod.Put, $"Invoices/{id}/Attachments/P0012-INV-001.pdf?IncludeOnline=false", pdf);

        var attachment = upload.First("Attachments");
        Assert.Equal("P0012-INV-001.pdf", attachment["FileName"]!.GetValue<string>());
        Assert.Equal(pdf.Length, attachment["ContentLength"]!.GetValue<long>());
        Assert.False(attachment["IncludeOnline"]!.GetValue<bool>());
        Assert.Equal("application/pdf", attachment["MimeType"]!.GetValue<string>());
        Assert.Equal(("P0012-INV-001.pdf", (long)pdf.Length, false), Assert.Single(kit.Simulator.Find("Invoices", id)!.Attachments));
        Assert.True(kit.Simulator.Find("Invoices", id)!.Body["HasAttachments"]!.GetValue<bool>());
        Assert.Single((await kit.GetAsync($"Invoices/{id}/Attachments")).Items("Attachments"));
        using var content = await kit.Client.SendAsync(kit.Request(HttpMethod.Get, $"Invoices/{id}/Attachments/P0012-INV-001.pdf"));
        Assert.Equal(pdf, await content.Content.ReadAsByteArrayAsync());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Theory]
    [InlineData("Invoices", true, true)]
    [InlineData("Quotes", true, false)]
    [InlineData("PurchaseOrders", true, false)]
    [InlineData("Invoices", false, false)]
    public async Task IncludeOnline_applies_to_invoices_only(string resource, bool requested, bool expected)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = resource switch
        {
            "Quotes" => await kit.CreateQuoteAsync(contactId),
            "PurchaseOrders" => await kit.CreatePurchaseOrderAsync(contactId),
            _ => await kit.CreateInvoiceAsync(contactId),
        };

        await kit.UploadAsync(HttpMethod.Put, $"{resource}/{id}/Attachments/doc.pdf?IncludeOnline={(requested ? "true" : "false")}", [1]);

        Assert.Equal(expected, Assert.Single(kit.Simulator.Find(resource, id)!.Attachments).IncludeOnline);
    }

    [Fact]
    public async Task POST_replaces_an_attachment_of_the_same_name_and_PUT_adds_another()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateQuoteAsync(contactId);

        await kit.UploadAsync(HttpMethod.Put, $"Quotes/{id}/Attachments/P0012-Q-001.pdf", new byte[10]);
        await kit.UploadAsync(HttpMethod.Post, $"Quotes/{id}/Attachments/P0012-Q-001.pdf", new byte[20]);
        Assert.Equal(20, Assert.Single(kit.Simulator.Find("Quotes", id)!.Attachments).Length);

        await kit.UploadAsync(HttpMethod.Put, $"Quotes/{id}/Attachments/P0012-Q-001.pdf", new byte[30]);
        Assert.Equal([20L, 30L], kit.Simulator.Find("Quotes", id)!.Attachments.Select(a => a.Length));
    }

    [Fact]
    public async Task An_empty_oversized_eleventh_or_JSON_attachment_is_refused()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreatePurchaseOrderAsync(contactId);

        var empty = await kit.UploadAsync(HttpMethod.Put, $"PurchaseOrders/{id}/Attachments/empty.pdf", []);
        var tooLarge = await kit.UploadAsync(HttpMethod.Put, $"PurchaseOrders/{id}/Attachments/big.pdf", new byte[XeroApiSimulator.MaximumAttachmentBytes + 1]);
        var asJson = await kit.PutAsync($"PurchaseOrders/{id}/Attachments/doc.pdf", new JsonObject { ["x"] = 1 });
        for (var i = 0; i < XeroApiSimulator.MaximumAttachmentsPerDocument; i++)
            Assert.Equal(HttpStatusCode.OK, (await kit.UploadAsync(HttpMethod.Put, $"PurchaseOrders/{id}/Attachments/doc{i}.pdf", [1])).Status);
        var eleventh = await kit.UploadAsync(HttpMethod.Put, $"PurchaseOrders/{id}/Attachments/doc10.pdf", [1]);

        Assert.Equal(HttpStatusCode.BadRequest, empty.Status);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.Status);
        Assert.Equal(HttpStatusCode.BadRequest, asJson.Status);
        Assert.Equal(HttpStatusCode.BadRequest, eleventh.Status);
        Assert.Equal(4, kit.Simulator.Violations.Count);
        Assert.All(kit.Simulator.Violations, v => Assert.Equal(XeroSimulatorRules.Attachment, v.Rule));
        Assert.Equal(10, kit.Simulator.Find("PurchaseOrders", id)!.Attachments.Count);
    }

    [Fact]
    public async Task An_attachment_to_an_unknown_document_is_404()
    {
        using var kit = new SimulatorTestKit();

        var reply = await kit.UploadAsync(HttpMethod.Put, "Invoices/00000000-0000-0000-0000-0000000000ab/Attachments/a.pdf", [1]);

        Assert.Equal(HttpStatusCode.NotFound, reply.Status);
        Assert.Empty(kit.Simulator.Violations);
    }

    // ------------------------------------------------------------ back-office acts

    [Fact]
    public async Task Approve_pay_and_void_move_invoices_as_Xero_does_and_are_read_back()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var paid = await kit.CreateInvoiceAsync(contactId, "INV-1");
        var voided = await kit.CreateInvoiceAsync(contactId, "INV-2");
        var requests = kit.Simulator.Requests.Count;

        kit.Simulator.ApproveInXero(paid);
        kit.Simulator.PayInXero(paid, new DateOnly(2026, 10, 20));
        kit.Simulator.ApproveInXero(voided);
        kit.Simulator.VoidInXero(voided);

        Assert.Equal(requests, kit.Simulator.Requests.Count);
        var paidRead = (await kit.GetAsync($"Invoices/{paid}")).First("Invoices");
        Assert.Equal("PAID", paidRead["Status"]!.GetValue<string>());
        Assert.Equal(0m, paidRead["AmountDue"]!.GetValue<decimal>());
        Assert.Equal(360m, paidRead["AmountPaid"]!.GetValue<decimal>());
        Assert.Equal("2026-10-20T00:00:00", paidRead["FullyPaidOnDateString"]!.GetValue<string>());
        Assert.True(XeroWire.TryParseDate(paidRead["FullyPaidOnDate"]!.GetValue<string>(), out var paidOn));
        Assert.Equal(new DateOnly(2026, 10, 20), paidOn);
        Assert.Equal("VOIDED", (await kit.GetAsync($"Invoices/{voided}")).First("Invoices")["Status"]!.GetValue<string>());
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task Back_office_acts_Xero_would_not_allow_throw()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = await kit.CreateInvoiceAsync(contactId);
        var quote = await kit.CreateQuoteAsync(contactId);

        Assert.Throws<InvalidOperationException>(() => kit.Simulator.PayInXero(id, new DateOnly(2026, 10, 20)));
        Assert.Throws<InvalidOperationException>(() => kit.Simulator.VoidInXero(id));
        Assert.Throws<InvalidOperationException>(() => kit.Simulator.ConvertQuoteToInvoiceInXero(quote));
        kit.Simulator.ApproveInXero(id);
        Assert.Throws<InvalidOperationException>(() => kit.Simulator.ApproveInXero(id));
        Assert.Throws<InvalidOperationException>(() => kit.Simulator.DeleteInXero("Invoices", id));
        Assert.Throws<InvalidOperationException>(() => kit.Simulator.ApproveInXero("00000000-0000-0000-0000-000000000000"));
        Assert.Throws<ArgumentException>(() => kit.Simulator.All("Payments"));
    }

    [Theory]
    [InlineData("Invoices")]
    [InlineData("Quotes")]
    [InlineData("PurchaseOrders")]
    public async Task A_document_deleted_in_Xero_is_read_back_as_DELETED(string resource)
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var id = resource switch
        {
            "Quotes" => await kit.CreateQuoteAsync(contactId),
            "PurchaseOrders" => await kit.CreatePurchaseOrderAsync(contactId),
            _ => await kit.CreateInvoiceAsync(contactId),
        };

        kit.Simulator.DeleteInXero(resource, id);

        Assert.Equal("DELETED", (await kit.GetAsync($"{resource}/{id}")).First(resource)["Status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Converting_an_accepted_quote_in_Xero_invoices_it_and_makes_a_draft()
    {
        using var kit = new SimulatorTestKit();
        var contactId = kit.Simulator.SeedContact("Acme Ltd");
        var quote = await kit.CreateQuoteAsync(contactId);
        await kit.SetQuoteStatusAsync(quote, contactId, "SENT");
        await kit.SetQuoteStatusAsync(quote, contactId, "ACCEPTED");

        var invoiceId = kit.Simulator.ConvertQuoteToInvoiceInXero(quote);

        Assert.Equal("INVOICED", kit.Simulator.Find("Quotes", quote)!.Status);
        var invoice = kit.Simulator.Find("Invoices", invoiceId)!;
        Assert.Equal("DRAFT", invoice.Status);
        Assert.Equal("ACCREC", invoice.Body["Type"]!.GetValue<string>());
        Assert.Equal("P0012-Q-001", invoice.Body["Reference"]!.GetValue<string>());
        Assert.Equal(contactId, invoice.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal(360m, invoice.Body["Total"]!.GetValue<decimal>());
        Assert.Equal("INV-0001", invoice.Number);
        Assert.Empty(kit.Simulator.Violations);
    }

    // ------------------------------------------------------------ a clean journey

    [Fact]
    public async Task A_journey_TempestOS_is_allowed_to_make_records_no_violation()
    {
        using var kit = new SimulatorTestKit();
        var client = (await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "Acme Ltd", ["ContactNumber"] = "CUST-1" })).First("Contacts")["ContactID"]!.GetValue<string>();
        var supplier = (await kit.PutAsync("Contacts", new JsonObject { ["Name"] = "Metals Ltd" })).First("Contacts")["ContactID"]!.GetValue<string>();

        var quote = await kit.CreateQuoteAsync(client);
        await kit.UploadAsync(HttpMethod.Post, $"Quotes/{quote}/Attachments/P0012-Q-001.pdf", [1, 2]);
        await kit.SetQuoteStatusAsync(quote, client, "SENT");
        await kit.SetQuoteStatusAsync(quote, client, "ACCEPTED");

        var invoice = await kit.CreateInvoiceAsync(client);
        await kit.UploadAsync(HttpMethod.Put, $"Invoices/{invoice}/Attachments/P0012-INV-001.pdf?IncludeOnline=false", [1, 2]);

        var order = await kit.CreatePurchaseOrderAsync(supplier);
        await kit.UploadAsync(HttpMethod.Put, $"PurchaseOrders/{order}/Attachments/P0012-PO-001.pdf", [1, 2]);

        var bill = await kit.CreateInvoiceAsync(supplier, "EXP-1", "ACCPAY");
        await kit.UploadAsync(HttpMethod.Put, $"Invoices/{bill}/Attachments/receipt.jpg", [1, 2], "image/jpeg");
        await kit.PostAsync($"Invoices/{bill}", new JsonObject { ["InvoiceID"] = bill, ["Status"] = "DELETED" });

        Assert.All(kit.Simulator.Requests, r => Assert.True(r.StatusCode is 200, $"{r.Method} {r.Path} answered {r.StatusCode}"));
        Assert.Empty(kit.Simulator.Violations);
        Assert.DoesNotContain(kit.Simulator.All("Invoices"), d => d.Status is "AUTHORISED" or "SUBMITTED");
    }
}

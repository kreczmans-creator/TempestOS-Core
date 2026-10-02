using System.Text.Json;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>
/// `v0.24.0` X2 acceptance (design §5, §11) end to end over the Xero
/// simulator: candidates ranked VAT &gt; ContactNumber &gt; exact &gt;
/// similar; linking an existing contact writes nothing (or, under Q7, only
/// an empty <c>ContactNumber</c>); a create looks <c>ContactNumber</c> up
/// first; a lost create makes one contact; an archived contact is never
/// linked; details are read, never pushed; links are per Xero organisation.
/// </summary>
public sealed class XeroContactLinkerTests
{
    private static readonly XeroContactLinkerOptions WriteNothing = new() { WriteContactNumberWhenEmpty = false };

    // ------------------------------------------------------------ candidates

    [Fact]
    public async Task FindCandidates_RanksVatThenContactNumberThenExactThenSimilar_ReadingOnly()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: "GB123456789");
        var byVat = kit.Simulator.SeedContact("Northwind Trading", taxNumber: "GB123456789");
        var byNumber = kit.Simulator.SeedContact("A.E. Holdings", contactNumber: "ACME1");
        var exact = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var similar = kit.Simulator.SeedContact("Acme Engineering (UK) Limited");
        kit.Simulator.SeedContact("Borealis Fabrication");
        kit.Simulator.ArchiveContactInXero(kit.Simulator.SeedContact("Acme Engineering Limited"));
        var mark = kit.Mark;

        var result = await kit.Linker.FindCandidatesAsync("acme1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal([byVat, byNumber, exact, similar], result.Value!.Select(c => c.ContactId));
        Assert.Equal(["vat-number", "contact-number", "exact-name", "similar-name"], result.Value!.Select(c => c.MatchedOn));
        Assert.All(result.Value!, c => Assert.Equal("ACTIVE", c.ContactStatus));
        Assert.Empty(kit.WritesSince(mark));

        // VAT as recorded and without its "GB" prefix, ContactNumber, name.
        Assert.Equal(4, kit.RequestsSince(mark).Count);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task FindCandidates_FindsAVatNumberStoredInItsPlainForm()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: "GB 123 4567 89");
        var plain = kit.Simulator.SeedContact("Northwind Trading", taxNumber: "GB123456789");

        var result = await kit.Linker.FindCandidatesAsync("ACME1");

        var candidate = Assert.Single(result.Value!);
        Assert.Equal((plain, "vat-number"), (candidate.ContactId, candidate.MatchedOn));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task FindCandidates_WidensTheNameSearch_OnlyWhenNoAlikeNameWasFound()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync(name: "Acme Precision Engineering Ltd", vat: null);
        var abbreviated = kit.Simulator.SeedContact("Precision Engineering Acme");
        var mark = kit.Mark;

        var result = await kit.Linker.FindCandidatesAsync("ACME1");

        Assert.Equal(abbreviated, Assert.Single(result.Value!).ContactId);
        Assert.Equal(
            ["where", "searchTerm", "searchTerm"],
            kit.RequestsSince(mark).Select(r => r.Query.ContainsKey("where") ? "where" : "searchTerm"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task FindCandidates_ForAnUnknownOrganisation_IsRejected_WithoutCallingXero()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        var mark = kit.Mark;

        var result = await kit.Linker.FindCandidatesAsync("NOPE1");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("NOPE1", result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.RequestsSince(mark));
    }

    // ------------------------------------------------------------ link existing

    [Fact]
    public async Task LinkExisting_WritesNothingToXero_WhenQ7IsOff()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var before = kit.Contact(contactId).ToJsonString();
        var mark = kit.Mark;

        var result = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(contactId, result.Value!.XeroId);
        Assert.Equal(XeroContactLinker.LinkedByLinked, result.Value.LinkedBy);
        Assert.Equal(ContactLinkerTestKit.TenantId, result.Value.TenantId);
        Assert.Equal(new XeroDocumentRef(XeroDocumentKind.Contact, "ACME1"), result.Value.Document);
        Assert.Equal("ACTIVE", result.Value.LastKnownXeroStatus);
        Assert.Equal(kit.Clock.GetUtcNow(), result.Value.LinkedAtUtc);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Equal(before, kit.Contact(contactId).ToJsonString());
        Assert.Equal(result.Value, await kit.Linker.FindLinkAsync("acme1"));
        Assert.Equal(XeroContactLinker.AuditLinkLinked, Assert.Single(kit.Audit.Rows).Action);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_UnderQ7_WritesTheCustomerCodeIntoAnEmptyContactNumber_AndNothingElse()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", taxNumber: "GB999999973");
        var mark = kit.Mark;

        var result = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal("ACME1", result.Value!.XeroNumber);
        var write = Assert.Single(kit.WritesSince(mark));
        Assert.Equal(HttpMethod.Post, write.Method);
        Assert.Equal($"Contacts/{contactId}", write.Path);
        var element = write.JsonBody!["Contacts"]![0]!.AsObject();
        Assert.Equal(["ContactID", "ContactNumber"], element.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal("ACME1", kit.Contact(contactId)["ContactNumber"]!.GetValue<string>());
        Assert.Equal("GB999999973", kit.Contact(contactId)["TaxNumber"]!.GetValue<string>());
        Assert.Equal("ACME1", (await kit.Linker.FindLinkAsync("ACME1"))!.XeroNumber);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_UnderQ7_NeverChangesAContactNumberAlreadySet()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "SAGE-0042");
        var mark = kit.Mark;

        var result = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal("SAGE-0042", result.Value!.XeroNumber);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Equal("SAGE-0042", kit.Contact(contactId)["ContactNumber"]!.GetValue<string>());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_UnderQ7_SkipsTheWrite_WhenAnotherContactAlreadyCarriesTheCode()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Simulator.SeedContact("Old Acme record", contactNumber: "ACME1");
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var mark = kit.Mark;

        var result = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Null(result.Value!.XeroNumber);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Contains("another Xero contact", kit.Audit.Rows.Single().Detail!["note"], StringComparison.Ordinal);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_AnArchivedContact_IsRefused_AndNothingIsLinked()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        kit.Simulator.ArchiveContactInXero(contactId);
        var mark = kit.Mark;

        var result = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("ARCHIVED", result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Empty(kit.WritesSince(mark));
        Assert.Empty(kit.Audit.Rows);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_AContactXeroDoesNotHave_IsRefused()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();

        var result = await kit.Linker.LinkExistingAsync("ACME1", "00000000-0000-0000-0000-000000000bad");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("no contact", result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_IsIdempotent_ButNeverSilentlyRelinksToAnotherContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        var first = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var second = kit.Simulator.SeedContact("Acme Engineering (Scotland) Ltd");
        var linked = (await kit.Linker.LinkExistingAsync("ACME1", first)).Value!;
        var mark = kit.Mark;

        var again = await kit.Linker.LinkExistingAsync("ACME1", first);
        var other = await kit.Linker.LinkExistingAsync("ACME1", second);

        Assert.Equal(linked, again.Value);
        Assert.Empty(kit.RequestsSince(mark));
        Assert.Equal(ConnectorOutcome.Rejected, other.Outcome);
        Assert.Contains("unlink it first", other.Reason, StringComparison.Ordinal);
        Assert.Equal(first, (await kit.Linker.FindLinkAsync("ACME1"))!.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkExisting_OneXeroContact_ServesOneOrganisation()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "ACME2", name: "Acme Engineering North", customerCode: "ACME2");
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);

        var result = await kit.Linker.LinkExistingAsync("ACME2", contactId);

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("ACME1", result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME2"));
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ create

    [Fact]
    public async Task Create_LooksTheContactNumberUpFirst_ThenCreatesWithIdentifiersOnly()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: "GB123456789", type: Tempest.Core.BusinessOperations.Crm.OrganisationTradingType.Both);
        var mark = kit.Mark;

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(XeroContactLinker.LinkedByCreated, result.Value!.LinkedBy);
        Assert.Equal("ACME1", result.Value.XeroNumber);

        var requests = kit.RequestsSince(mark);
        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Equal("ContactNumber==\"ACME1\"", requests[0].Query["where"]);
        Assert.Equal("true", requests[0].Query["includeArchived"]);
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal("Contacts", requests[1].Path);
        Assert.StartsWith("tos:Contact:CreateContact:", requests[1].IdempotencyKey, StringComparison.Ordinal);

        var sent = requests[1].JsonBody!["Contacts"]![0]!.AsObject();
        Assert.Equal(["CompanyNumber", "ContactNumber", "EmailAddress", "Name", "TaxNumber"], sent.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal("Acme Engineering Ltd", sent["Name"]!.GetValue<string>());

        var contact = Assert.Single(kit.LiveContacts);
        Assert.Equal(result.Value.XeroId, contact.Id);
        Assert.Equal("ACME1", contact.Number);
        Assert.Equal("GB123456789", contact.Body["TaxNumber"]!.GetValue<string>());
        Assert.Equal(XeroContactLinker.AuditLinkCreated, Assert.Single(kit.Audit.Rows).Action);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WhenXeroAlreadyHasTheContactNumber_LinksIt_AndCreatesNothing()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var existing = kit.Simulator.SeedContact("Acme Eng", contactNumber: "ACME1");
        var mark = kit.Mark;

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal((existing, XeroContactLinker.LinkedByReconciled), (result.Value!.XeroId, result.Value.LinkedBy));
        Assert.Empty(kit.WritesSince(mark));
        Assert.Single(kit.LiveContacts);
        Assert.Equal(XeroContactLinker.AuditLinkReconciled, Assert.Single(kit.Audit.Rows).Action);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WhenAlreadyLinked_AnswersTheLink_WithoutCallingXero()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var mark = kit.Mark;

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(contactId, result.Value!.XeroId);
        Assert.Empty(kit.RequestsSince(mark));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WhenTheAnswerIsLost_FindsTheContactXeroMade_AndMakesOnlyOne()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Lost.LoseWrites = 1;

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(XeroContactLinker.LinkedByReconciled, result.Value!.LinkedBy);
        var contact = Assert.Single(kit.LiveContacts);
        Assert.Equal(contact.Id, result.Value.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WhenTheAnswerAndTheLookUpAreBothLost_ReportsUnavailable_AndTheRetryLinksTheOneContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Lost.LoseWrites = 1;
        kit.Lost.FailReadsAfterLoss = 1;

        var first = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Unavailable, first.Outcome);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Single(kit.LiveContacts);

        var mark = kit.Mark;
        var retry = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal(XeroContactLinker.LinkedByReconciled, retry.Value!.LinkedBy);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Equal(Assert.Single(kit.LiveContacts).Id, retry.Value.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_NeverResends_WhileALookUpCannotConfirmWhetherXeroMadeTheContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Lost.LoseWrites = 1;
        kit.Lost.FailReadsAfterLoss = 2;

        await kit.Linker.CreateAsync("ACME1");

        // The pre-create look-up of the retry fails too, so nothing is sent
        // then; the retry after that finds the contact. Xero saw one PUT.
        Assert.Single(kit.Simulator.Requests, r => r.Method == HttpMethod.Put);
        Assert.Equal(ConnectorOutcome.Unavailable, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        Assert.Single(kit.Simulator.Requests, r => r.Method == HttpMethod.Put);
        Assert.Single(kit.LiveContacts);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WhenAnArchivedContactCarriesTheCode_IsRefused_RatherThanMakingASecond()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Simulator.ArchiveContactInXero(kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1"));
        var mark = kit.Mark;

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("archived", result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WhenXeroHasAContactOfTheSameName_IsRefusedWithAHintToLinkIt()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Simulator.SeedContact("Acme Engineering Ltd");

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("link that contact instead", result.Reason, StringComparison.Ordinal);
        Assert.Single(kit.LiveContacts);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));

        // Xero's own refusal of a duplicate name — the expected outcome here,
        // not a TempestOS defect; nothing else was recorded.
        Assert.Equal([XeroSimulatorRules.Duplicate], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task Create_WithoutTheContactsWriteScope_AsksForReauthorisation()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(grantedScopes: ["accounting.contacts.read", XeroScopes.Invoices, XeroScopes.SettingsRead]);
        await kit.AddOrganisationAsync();

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Contains("scope", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(kit.LiveContacts);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Equal([XeroSimulatorRules.Scope], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task Create_AgainstALiveOrganisation_IsBlockedBeforeXero_D7()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(isDemoCompany: false);
        await kit.AddOrganisationAsync();
        var mark = kit.Mark;

        var result = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains(XeroWriteSafetyHandler.RuleLiveOrganisation, result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Empty(kit.LiveContacts);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ details

    [Fact]
    public async Task ReadDetails_ReadsBillingAddressVatAndPaymentTerms_AndPushesNothing()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        var contactId = await kit.SeedContactAsync(new JsonObject
        {
            ["Name"] = "Acme Engineering Ltd",
            ["TaxNumber"] = "GB123456789",
            ["EmailAddress"] = "ap@acme.example",
            ["Addresses"] = new JsonArray
            {
                new JsonObject { ["AddressType"] = "STREET", ["AddressLine1"] = "Unit 4, Works Lane", ["City"] = "Sheffield", ["PostalCode"] = "S1 1AA" },
                new JsonObject { ["AddressType"] = "POBOX", ["AddressLine1"] = "Accounts Payable", ["AddressLine2"] = "PO Box 12", ["City"] = "Leeds", ["PostalCode"] = "LS1 1AA", ["Country"] = "United Kingdom" },
            },
            ["PaymentTerms"] = new JsonObject { ["Sales"] = new JsonObject { ["Day"] = 30, ["Type"] = "DAYSAFTERBILLDATE" } },
        });
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        kit.Clock.Advance(TimeSpan.FromHours(2));
        var mark = kit.Mark;

        var result = await kit.Linker.ReadDetailsAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var details = result.Value!;
        Assert.Equal(contactId, details.ContactId);
        Assert.Equal("GB123456789", details.TaxNumber);
        Assert.Equal(["Accounts Payable", "PO Box 12", "Leeds", "LS1 1AA", "United Kingdom"], details.BillingAddress);
        Assert.Equal("ap@acme.example", details.EmailAddress);
        Assert.Equal((30, "DAYSAFTERBILLDATE"), (details.SalesPaymentTermsDays, details.SalesPaymentTermsType));
        Assert.Equal(kit.Clock.GetUtcNow(), details.ReadAtUtc);
        Assert.Equal(kit.Clock.GetUtcNow(), (await kit.Linker.FindLinkAsync("ACME1"))!.LastReadAtUtc);
        Assert.Equal(HttpMethod.Get, Assert.Single(kit.RequestsSince(mark)).Method);
        kit.AssertNoViolations();
    }

    [Fact]
    public void ToDetails_FallsBackToTheStreetAddress_AndGivesDaysOnlyForADaysAfterTerm()
    {
        var contact = new XeroWireContact(
            "c-1", Name: "Acme",
            Addresses: [new XeroWireContactAddress("POBOX", " "), new XeroWireContactAddress("STREET", "1 Works Lane", City: "Sheffield")],
            PaymentTerms: new XeroWirePaymentTerms(new XeroWirePaymentTerm(20, "OFFOLLOWINGMONTH")));

        var details = XeroContactLinker.ToDetails(contact, "c-1", DateTimeOffset.UnixEpoch);

        Assert.Equal(["1 Works Lane", "Sheffield"], details.BillingAddress);
        Assert.Null(details.SalesPaymentTermsDays);
        Assert.Equal("OFFOLLOWINGMONTH", details.SalesPaymentTermsType);
        Assert.Null(details.TaxNumber);
    }

    [Fact]
    public async Task ReadDetails_OfAContactDeletedInXero_SaysSo_AndKeepsTheLinkForThePoToUnlink()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        kit.Simulator.DeleteInXero("Contacts", contactId);

        var result = await kit.Linker.ReadDetailsAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("deleted in Xero", result.Reason, StringComparison.Ordinal);
        Assert.NotNull(await kit.Linker.FindLinkAsync("ACME1"));

        Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.False(await kit.Linker.UnlinkAsync("ACME1"));
        Assert.Equal(XeroContactLinker.AuditLinkUnlinked, kit.Audit.Rows[^1].Action);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ReadDetails_OfAnUnlinkedOrganisation_IsRefused_WithoutCallingXero()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var mark = kit.Mark;

        var result = await kit.Linker.ReadDetailsAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Empty(kit.RequestsSince(mark));
    }

    // ------------------------------------------------------------ tenants and pushes

    [Fact]
    public async Task Links_BelongToOneXeroOrganisation_D7()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);

        await kit.SecretStore.SetAsync(XeroContactLinker.TenantIdSecretKey, "live-organisation");

        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.False((await kit.Linker.ResolveForPushAsync("live-organisation", "ACME1")).IsLinked);
        Assert.True((await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "ACME1")).IsLinked);
    }

    [Fact]
    public async Task ResolveForPush_GivesTheContactIdOnly_OrTheBlockedReason()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(WriteNothing);
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BORE1", name: "Borealis Fabrication", customerCode: "BORE1");
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var mark = kit.Mark;

        var linked = await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "acme1");
        var unlinked = await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "BORE1");
        var none = await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, null);

        Assert.Equal(contactId, linked.ContactId);
        Assert.Null(linked.BlockedReason);
        Assert.Equal($"{{\"ContactID\":\"{contactId}\"}}", JsonSerializer.Serialize(linked.ToContactRef(), Tempest.Core.Invoicing.Xero.Api.XeroWire.JsonOptions));
        Assert.False(unlinked.IsLinked);
        Assert.Contains("not linked to a Xero contact", unlinked.BlockedReason, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => unlinked.ToContactRef());
        Assert.False(none.IsLinked);
        Assert.Empty(kit.RequestsSince(mark));
    }

    [Fact]
    public async Task WithNoXeroOrganisationConnected_NothingIsLinkedOrSent()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.SecretStore.RemoveAsync(XeroContactLinker.TenantIdSecretKey);
        var mark = kit.Mark;

        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Equal(ConnectorOutcome.Reauthorise, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        Assert.Equal(ConnectorOutcome.Reauthorise, (await kit.Linker.LinkExistingAsync("ACME1", "c-1")).Outcome);
        Assert.Equal(ConnectorOutcome.Reauthorise, (await kit.Linker.ReadDetailsAsync("ACME1")).Outcome);
        Assert.Empty(kit.RequestsSince(mark));
    }
}

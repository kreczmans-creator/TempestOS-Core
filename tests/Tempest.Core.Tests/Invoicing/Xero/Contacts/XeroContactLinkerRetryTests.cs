using System.Text.Json.Nodes;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>
/// `v0.24.0` X2 verifier fixes: a write's <c>Idempotency-Key</c> is reused
/// only to retry an attempt whose answer was uncertain, so a refusal fixed
/// in Xero is not replayed from Xero's cache (S7); the VAT look-up asks for
/// the number with and without its country prefix; a link to a contact
/// archived in Xero is Blocked for a push.
/// </summary>
public sealed class XeroContactLinkerRetryTests
{
    // ------------------------------------------------------------ item 1: create after a definite refusal

    [Fact]
    public async Task Create_RefusedAsADuplicateName_ThenFixedInXero_TheRetryCreatesTheContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var clash = kit.Simulator.SeedContact("Acme Engineering Ltd");

        var refused = await kit.Linker.CreateAsync("ACME1");
        Assert.Equal(ConnectorOutcome.Rejected, refused.Outcome);

        // The Product Owner archives the clashing contact in Xero, then retries.
        kit.Simulator.ArchiveContactInXero(clash);
        var retry = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal(XeroContactLinker.LinkedByCreated, retry.Value!.LinkedBy);
        var created = Assert.Single(kit.LiveContacts, c => c.Status == "ACTIVE");
        Assert.Equal(created.Id, retry.Value.XeroId);
        Assert.Equal("ACME1", created.Number);

        var puts = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put && r.Path == "Contacts").ToList();
        Assert.Equal(2, puts.Count);
        Assert.NotEqual(puts[0].IdempotencyKey, puts[1].IdempotencyKey);
        Assert.All(puts, p => Assert.StartsWith("tos:Contact:CreateContact:", p.IdempotencyKey, StringComparison.Ordinal));
        Assert.All(puts, p => Assert.True(p.IdempotencyKey!.Length <= 128));
        Assert.Equal([XeroSimulatorRules.Duplicate], kit.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task Create_RefusedThenFixedInXero_TheRetryAfterARestartCreatesTheContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var clash = kit.Simulator.SeedContact("Acme Engineering Ltd");

        Assert.Equal(ConnectorOutcome.Rejected, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        kit.Simulator.ArchiveContactInXero(clash);

        var retry = await kit.NewLinker().CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal(XeroContactLinker.LinkedByCreated, retry.Value!.LinkedBy);
    }

    [Fact]
    public async Task Create_WithNoContactNumber_WhenTheAnswerIsLost_TheRetryReplaysTheSameKey_AndMakesOneContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        var reference = "ACME-" + new string('7', 50);
        await kit.AddOrganisationAsync(reference: reference, customerCode: null);
        kit.Lost.LoseWrites = 1;

        var first = await kit.Linker.CreateAsync(reference);
        Assert.NotEqual(ConnectorOutcome.Ok, first.Outcome);
        Assert.Single(kit.LiveContacts);

        // Even after a restart the uncertain attempt's key is reused.
        var retry = await kit.NewLinker().CreateAsync(reference);

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal(Assert.Single(kit.LiveContacts).Id, retry.Value!.XeroId);
        var puts = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put).ToList();
        Assert.Equal(2, puts.Count);
        Assert.Equal(puts[0].IdempotencyKey, puts[1].IdempotencyKey);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_WithAnUnreadableAttemptRecord_FailsLoudly_AndSendsNothing()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
        kit.Simulator.DeleteInXero("Contacts", Assert.Single(kit.LiveContacts).Id);
        foreach (var key in await kit.AttemptStore.ListKeysAsync(XeroContactLinker.AttemptsCollection))
            await kit.AttemptStore.WriteAsync(XeroContactLinker.AttemptsCollection, key, "{ not json");
        var mark = kit.Mark;

        await Assert.ThrowsAsync<PersistenceException>(() => kit.Linker.CreateAsync("ACME1"));
        Assert.Empty(kit.WritesSince(mark));
    }

    // ------------------------------------------------------------ item 2: Q7 ContactNumber written again

    [Fact]
    public async Task LinkExisting_AfterTheContactNumberWasClearedInXero_RelinkingWritesItAgain()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");

        Assert.Equal("ACME1", (await kit.Linker.LinkExistingAsync("ACME1", contactId)).Value!.XeroNumber);
        Assert.Equal("ACME1", kit.Contact(contactId)["ContactNumber"]!.GetValue<string>());

        // Someone clears ContactNumber in Xero; the Product Owner unlinks and links again.
        await kit.EditContactInXeroAsync(contactId, new JsonObject { ["ContactNumber"] = string.Empty });
        Assert.True(string.IsNullOrEmpty(kit.Contact(contactId)["ContactNumber"]?.GetValue<string>()));
        Assert.True(await kit.Linker.UnlinkAsync("ACME1"));

        var relinked = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal(ConnectorOutcome.Ok, relinked.Outcome);
        Assert.Equal("ACME1", relinked.Value!.XeroNumber);
        Assert.Equal("ACME1", kit.Contact(contactId)["ContactNumber"]!.GetValue<string>());
        var posts = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Post && r.IdempotencyKey!.StartsWith("tos:", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, posts.Count);
        Assert.NotEqual(posts[0].IdempotencyKey, posts[1].IdempotencyKey);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ item 3: VAT with and without the country prefix

    [Theory]
    [InlineData("GB 123 4567 89", "123456789")]
    [InlineData("GB123456789", "123456789")]
    [InlineData("123456789", "GB123456789")]
    [InlineData("123 4567 89", "GB123456789")]
    public async Task FindCandidates_FindsTheVatNumberWhetherOrNotXeroHoldsTheCountryPrefix(string recorded, string inXero)
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: recorded);
        var byVat = kit.Simulator.SeedContact("Northwind Trading", taxNumber: inXero);

        var result = await kit.Linker.FindCandidatesAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var candidate = Assert.Single(result.Value!);
        Assert.Equal((byVat, XeroContactMatcher.MatchedOnVatNumber), (candidate.ContactId, candidate.MatchedOn));
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ item 6: archived link Blocked for a push

    [Fact]
    public async Task ResolveForPush_OfAContactArchivedInXero_IsBlockedWithTheReason()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        Assert.True((await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "ACME1")).IsLinked);

        kit.Simulator.ArchiveContactInXero(contactId);
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.ReadDetailsAsync("ACME1")).Outcome);

        var resolution = await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "ACME1");

        Assert.False(resolution.IsLinked);
        Assert.Null(resolution.ContactId);
        Assert.Contains("archived", resolution.BlockedReason, StringComparison.Ordinal);
        Assert.Equal(contactId, resolution.Link!.XeroId);
        Assert.Throws<InvalidOperationException>(() => resolution.ToContactRef());
    }
}

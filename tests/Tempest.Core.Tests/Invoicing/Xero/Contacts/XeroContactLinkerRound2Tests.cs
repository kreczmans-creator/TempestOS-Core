using System.Text.Json.Nodes;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.BusinessOperations;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>
/// `v0.24.0` X2 round-2 verifier fixes: a create left uncertain is settled
/// once a <c>ContactNumber</c> look-up gives a definite answer; a lost answer
/// to the Q7 write is followed by a read; an uncertain create is retried
/// with its original body; a create is settled only after its link is kept;
/// a VAT recorded as just "GB" is not asked for as "GBGB".
/// </summary>
public sealed class XeroContactLinkerRound2Tests
{
    // ------------------------------------------------------------ defect 1: reconciled create settles the attempt

    [Fact]
    public async Task Create_AfterALostCreateWasReconciled_AndTheContactRepurposedInXero_MakesANewContact_NotAStaleReplay()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Lost.LoseWrites = 1;

        var first = await kit.Linker.CreateAsync("ACME1");
        Assert.Equal((ConnectorOutcome.Ok, XeroContactLinker.LinkedByReconciled), (first.Outcome, first.Value!.LinkedBy));
        var old = first.Value.XeroId;

        Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
        await kit.EditContactInXeroAsync(old, new JsonObject { ["Name"] = "Acme Old", ["ContactNumber"] = "OTHER9" });

        var second = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal((ConnectorOutcome.Ok, XeroContactLinker.LinkedByCreated), (second.Outcome, second.Value!.LinkedBy));
        Assert.NotEqual(old, second.Value.XeroId);
        Assert.Equal(2, kit.LiveContacts.Count);
        var puts = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put && r.IdempotencyKey!.StartsWith("tos:", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, puts.Count);
        Assert.NotEqual(puts[0].IdempotencyKey, puts[1].IdempotencyKey);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_ReconciledBeforeThePut_AfterALostCreate_SettlesTheAttempt()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();

        // The answer is lost and the look-up after it fails too: uncertain.
        kit.Lost.LoseWrites = 1;
        kit.Lost.FailReadsAfterLoss = 10;
        Assert.NotEqual(ConnectorOutcome.Ok, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        kit.Lost.FailReadsAfterLoss = 0;
        await DrainFailedReadsAsync(kit);

        // The next create finds it by ContactNumber before any PUT.
        var reconciled = await kit.Linker.CreateAsync("ACME1");
        Assert.Equal(XeroContactLinker.LinkedByReconciled, reconciled.Value!.LinkedBy);
        var old = reconciled.Value.XeroId;

        Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
        await kit.EditContactInXeroAsync(old, new JsonObject { ["Name"] = "Acme Old", ["ContactNumber"] = "OTHER9" });

        var created = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal((ConnectorOutcome.Ok, XeroContactLinker.LinkedByCreated), (created.Outcome, created.Value!.LinkedBy));
        Assert.NotEqual(old, created.Value.XeroId);
        Assert.Equal(2, kit.LiveContacts.Count);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ defect 2: lost answer to the Q7 write

    [Fact]
    public async Task LinkExisting_WhenTheAnswerToTheContactNumberWriteIsLost_ReadsItBack_AndRecordsTheNumber()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        kit.Lost.LoseWrites = 1;

        var linked = await kit.Linker.LinkExistingAsync("ACME1", contactId);

        Assert.Equal(ConnectorOutcome.Ok, linked.Outcome);
        Assert.Equal("ACME1", kit.Contact(contactId)["ContactNumber"]!.GetValue<string>());
        Assert.Equal("ACME1", linked.Value!.XeroNumber);
        Assert.Equal("ACME1", (await kit.Linker.FindLinkAsync("ACME1"))!.XeroNumber);
        var audit = Assert.Single(kit.Audit.Rows);
        Assert.Equal("ContactNumber set to 'ACME1'", audit.Detail!["note"]);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ defect 3: uncertain create retried with its original body

    [Fact]
    public async Task Create_WithNoContactNumber_LostThenTheOrganisationEdited_TheRetryReplaysTheOriginal_AndMakesOneContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        var reference = "ACME-" + new string('7', 50);
        var organisation = await kit.AddOrganisationAsync(reference: reference, customerCode: null);
        kit.Lost.LoseWrites = 1;

        Assert.NotEqual(ConnectorOutcome.Ok, (await kit.Linker.CreateAsync(reference)).Outcome);
        Assert.Single(kit.LiveContacts);

        // The Product Owner edits the name and email before retrying.
        await kit.Organisations.ReviseAsync(
            reference, organisation with { Name = "Acme Engineering Group Ltd", EmailAddress = "ledger@acme.example" }, OperationsFixtures.Verified(), "Renamed.");

        var retry = await kit.NewLinker().CreateAsync(reference);

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal(Assert.Single(kit.LiveContacts).Id, retry.Value!.XeroId);
        var puts = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put).ToList();
        Assert.Equal(2, puts.Count);
        Assert.Equal(puts[0].IdempotencyKey, puts[1].IdempotencyKey);
        Assert.Equal(puts[0].JsonBody!.ToJsonString(), puts[1].JsonBody!.ToJsonString());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Create_AfterADefiniteAnswer_UsesTheOrganisationAsItIsNow()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        var organisation = await kit.AddOrganisationAsync();
        var clash = kit.Simulator.SeedContact("Acme Engineering Ltd");
        Assert.Equal(ConnectorOutcome.Rejected, (await kit.Linker.CreateAsync("ACME1")).Outcome);

        await kit.Organisations.ReviseAsync(
            "ACME1", organisation with { Name = "Acme Engineering (Leeds) Ltd" }, OperationsFixtures.Verified(), "Renamed.");
        var retry = await kit.Linker.CreateAsync("ACME1");

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal("Acme Engineering (Leeds) Ltd", kit.Contact(retry.Value!.XeroId)["Name"]!.GetValue<string>());
        Assert.NotEqual(clash, retry.Value.XeroId);
    }

    // ------------------------------------------------------------ defect 4: settled only after the link is saved

    [Fact]
    public async Task Create_WithNoContactNumber_WhenSavingTheLinkFails_TheRetryReplaysTheSameKey_AndMakesOneContact()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        var reference = "ACME-" + new string('7', 50);
        await kit.AddOrganisationAsync(reference: reference, customerCode: null);
        var failing = new FailFirstSaveLinkStore(kit.Links);
        var linker = new XeroContactLinker(kit.Api, failing, kit.Organisations, kit.SecretStore, kit.Audit, kit.Clock, kit.Options, attemptStore: kit.AttemptStore);

        await Assert.ThrowsAsync<IOException>(() => linker.CreateAsync(reference));
        Assert.Single(kit.LiveContacts);
        Assert.Null(await kit.Linker.FindLinkAsync(reference));

        var retry = await kit.NewLinker().CreateAsync(reference);

        Assert.Equal(ConnectorOutcome.Ok, retry.Outcome);
        Assert.Equal(Assert.Single(kit.LiveContacts).Id, retry.Value!.XeroId);
        var puts = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put).ToList();
        Assert.Equal(2, puts.Count);
        Assert.Equal(puts[0].IdempotencyKey, puts[1].IdempotencyKey);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ defect 5: VAT recorded as just "GB"

    [Fact]
    public async Task FindCandidates_WithAVatOfJustTheCountryPrefix_DoesNotAskForGbGb()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: "GB");
        var mark = kit.Mark;

        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.FindCandidatesAsync("ACME1")).Outcome);

        var vatQueries = kit.RequestsSince(mark)
            .Select(r => r.Query.GetValueOrDefault("where") ?? r.Query.GetValueOrDefault("Where"))
            .Where(w => w is not null && w.Contains("TaxNumber", StringComparison.Ordinal))
            .ToList();
        Assert.Single(vatQueries);
        Assert.DoesNotContain(vatQueries, w => w!.Contains("GBGB", StringComparison.Ordinal));
    }

    /// <summary>Spends any reads the lost-response handler would still fail, so later look-ups are not affected.</summary>
    private static async Task DrainFailedReadsAsync(ContactLinkerTestKit kit)
    {
        for (var i = 0; i < 20; i++)
        {
            if ((await kit.Api.SearchContactsAsync("drain")).Outcome == ConnectorOutcome.Ok)
                return;
        }
    }

    /// <summary>A link store whose first save fails, as a crash or a full disk between the create and keeping its link.</summary>
    private sealed class FailFirstSaveLinkStore(IXeroLinkStore inner) : IXeroLinkStore
    {
        private bool _failed;

        public Task<XeroLink?> FindAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default) =>
            inner.FindAsync(tenantId, document, cancellationToken);

        public Task<IReadOnlyList<XeroLink>> ListAsync(string tenantId, XeroDocumentKind? kind = null, CancellationToken cancellationToken = default) =>
            inner.ListAsync(tenantId, kind, cancellationToken);

        public Task SaveAsync(XeroLink link, CancellationToken cancellationToken = default)
        {
            if (_failed)
                return inner.SaveAsync(link, cancellationToken);

            _failed = true;
            throw new IOException("Simulated: the link store could not be written.");
        }

        public Task UnlinkAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default) =>
            inner.UnlinkAsync(tenantId, document, cancellationToken);
    }
}

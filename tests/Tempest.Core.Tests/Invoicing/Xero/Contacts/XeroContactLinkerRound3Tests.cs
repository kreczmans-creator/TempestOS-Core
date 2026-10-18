using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Tests.BusinessOperations;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>
/// `v0.24.0` X2 round-3 verifier fixes: refreshing a contact's details never
/// brings back a link removed (or replaced) while Xero was being read; a
/// retried create is checked against the <c>ContactNumber</c> in the body it
/// resends, not only today's customer code (§6.4.3).
/// </summary>
public sealed class XeroContactLinkerRound3Tests
{
    // ------------------------------------------------------------ defect 1: read details racing an unlink

    [Fact]
    public async Task ReadDetails_WhileTheLinkIsRemoved_DoesNotBringTheLinkBack()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        await kit.AddOrganisationAsync();
        var id = kit.Simulator.SeedContact("Acme Engineering Ltd");
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync("ACME1", id)).Outcome);

        Task<ConnectorResult<XeroContactDetails>> read;
        using (kit.Simulator.HoldRequests())
        {
            read = kit.Linker.ReadDetailsAsync("ACME1");
            await WaitForInFlightAsync(kit);
            Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
        }

        var details = await read;

        Assert.Equal(ConnectorOutcome.Ok, details.Outcome);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        var resolution = await kit.Linker.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "ACME1");
        Assert.Null(resolution.ContactId);
    }

    [Fact]
    public async Task ReadDetails_WhileTheOrganisationIsRelinkedToAnotherContact_DoesNotThrow_AndKeepsTheNewLink()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        await kit.AddOrganisationAsync();
        var id = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var other = kit.Simulator.SeedContact("Acme Engineering Two Ltd");
        var original = (await kit.Linker.LinkExistingAsync("ACME1", id)).Value!;

        Task<ConnectorResult<XeroContactDetails>> read;
        using (kit.Simulator.HoldRequests())
        {
            read = kit.Linker.ReadDetailsAsync("ACME1");
            await WaitForInFlightAsync(kit);
            Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
            await kit.Links.SaveAsync(original with { XeroId = other, LastReadAtUtc = null });
        }

        var details = await read;

        Assert.Equal(ConnectorOutcome.Ok, details.Outcome);
        var now = await kit.Linker.FindLinkAsync("ACME1");
        Assert.Equal(other, now!.XeroId);
        Assert.Null(now.LastReadAtUtc);
    }

    // Backlog X2-3: a read overlapping an unlink and relink to the same contact overwrote the newer link's fields.
    [Fact]
    public async Task ReadDetails_WhileTheOrganisationIsRelinkedToTheSameContact_KeepsTheNewerLinksFields()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        await kit.AddOrganisationAsync();
        var id = kit.Simulator.SeedContact("Acme Engineering Ltd");
        var original = (await kit.Linker.LinkExistingAsync("ACME1", id)).Value!;

        Task<ConnectorResult<XeroContactDetails>> read;
        using (kit.Simulator.HoldRequests())
        {
            read = kit.Linker.ReadDetailsAsync("ACME1");
            await WaitForInFlightAsync(kit);
            kit.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.True(await kit.Linker.UnlinkAsync("ACME1"));
            await kit.Links.SaveAsync(original with { LinkedAtUtc = kit.Clock.GetUtcNow(), XeroNumber = "RELINKED", LastReadAtUtc = null });
        }

        Assert.Equal(ConnectorOutcome.Ok, (await read).Outcome);
        var now = await kit.Linker.FindLinkAsync("ACME1");
        Assert.Equal(id, now!.XeroId);
        Assert.Equal("RELINKED", now.XeroNumber);
        Assert.Null(now.LastReadAtUtc);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ReadDetails_WithNoRace_StillRefreshesTheLink()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        await kit.AddOrganisationAsync();
        var id = kit.Simulator.SeedContact("Acme Engineering Ltd");
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync("ACME1", id)).Outcome);

        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.ReadDetailsAsync("ACME1")).Outcome);

        var link = await kit.Linker.FindLinkAsync("ACME1");
        Assert.Equal(id, link!.XeroId);
        Assert.NotNull(link.LastReadAtUtc);
    }

    // ------------------------------------------------------------ defect 2: a retry looks up the resent body's ContactNumber

    [Fact]
    public async Task Create_RetriedAfterTheCustomerCodeChanged_IsReconciledByTheResentContactNumber_WithoutAnotherPut()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        var organisation = await kit.AddOrganisationAsync();

        // The answer is lost and the look-up after it fails too: uncertain.
        kit.Lost.LoseWrites = 1;
        kit.Lost.FailReadsAfterLoss = 10;
        Assert.NotEqual(ConnectorOutcome.Ok, (await kit.Linker.CreateAsync("ACME1")).Outcome);
        kit.Lost.FailReadsAfterLoss = 0;
        for (var i = 0; i < 20; i++)
        {
            if ((await kit.Api.SearchContactsAsync("drain")).Outcome == ConnectorOutcome.Ok)
                break;
        }

        var made = Assert.Single(kit.LiveContacts).Id;
        await kit.Organisations.ReviseAsync("ACME1", organisation with { CustomerCode = "ACME2" }, OperationsFixtures.Verified(), "Code changed.");
        var mark = kit.Mark;

        var retry = await kit.NewLinker().CreateAsync("ACME1");

        // Found by its own natural key: no reliance on Xero still holding the Idempotency-Key.
        Assert.Equal((ConnectorOutcome.Ok, XeroContactLinker.LinkedByReconciled), (retry.Outcome, retry.Value!.LinkedBy));
        Assert.Equal(made, retry.Value.XeroId);
        Assert.Empty(kit.WritesSince(mark));
        Assert.Single(kit.LiveContacts);
        kit.AssertNoViolations();
    }

    // Backlog X2-2: the look-up after an uncertain PUT used today's customer code when the resent body carried none.
    [Fact]
    public async Task Create_ResendingABodyWithNoContactNumber_LooksUpNothingAfterAnUncertainPut()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync();
        // A reference too long for ContactNumber and no customer code: the first body carries no ContactNumber.
        var reference = "ACME-" + new string('7', 50);
        var organisation = await kit.AddOrganisationAsync(reference: reference, customerCode: null);
        kit.Lost.LoseWrites = 1;
        Assert.NotEqual(ConnectorOutcome.Ok, (await kit.Linker.CreateAsync(reference)).Outcome);

        await kit.Organisations.ReviseAsync(reference, organisation with { CustomerCode = "ACME2" }, OperationsFixtures.Verified(), "Code added.");
        kit.Lost.LoseWrites = 1;
        var retry = await kit.NewLinker().CreateAsync(reference);

        Assert.NotEqual(ConnectorOutcome.Ok, retry.Outcome);
        var requests = kit.Simulator.Requests.ToList();
        var lastPut = requests.FindLastIndex(r => r.Method == HttpMethod.Put);
        Assert.Null(requests[lastPut].JsonBody!["Contacts"]![0]!["ContactNumber"]);
        Assert.DoesNotContain(requests.Skip(lastPut + 1), r => r.Method == HttpMethod.Get);
        Assert.Single(kit.LiveContacts);
        kit.AssertNoViolations();
    }

    /// <summary>Waits for the simulator's signal that the held read reached it (review board n9: no fixed poll); the timeout only stops a broken test hanging.</summary>
    private static async Task WaitForInFlightAsync(ContactLinkerTestKit kit)
    {
        await kit.Simulator.WhenInFlightAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(1, kit.Simulator.InFlight);
    }
}

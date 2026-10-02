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

    private static async Task WaitForInFlightAsync(ContactLinkerTestKit kit)
    {
        for (var i = 0; i < 500 && kit.Simulator.InFlight == 0; i++)
            await Task.Delay(10);
        Assert.Equal(1, kit.Simulator.InFlight);
    }
}

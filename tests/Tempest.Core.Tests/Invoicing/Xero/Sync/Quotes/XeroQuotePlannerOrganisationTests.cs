using System.Globalization;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>
/// `v0.24.0` F1 (review board M1): Q8's "automatic from" moment and the
/// <em>Send to Xero</em> opt-ins are kept per Xero organisation, so a
/// quotation issued while the Demo Company was connected is not sent to the
/// organisation connected later unless the Product Owner asks.
/// </summary>
public sealed class XeroQuotePlannerOrganisationTests
{
    private const string Second = "tenant-2";

    [Fact]
    public async Task TheFirstOrganisation_StartsWithTheWorkspace_ALaterOne_WhenFirstSeen()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var startedAt = kit.Clock.GetUtcNow();
        Assert.Equal(startedAt, await kit.Planner.AutomaticFromAsync());
        Assert.Equal(QuoteSyncTestKit.TenantId, await kit.Store.ReadAsync(XeroQuotePlanner.StateCollection, XeroQuotePlanner.AutomaticFromOwnerKey));

        kit.Clock.Advance(TimeSpan.FromHours(3));
        var demoEra = Guid.NewGuid();
        kit.FakeQuotes[demoEra] = QuoteSyncTestKit.Quote(demoEra, QuotationStatus.Sent, issuedAt: kit.Clock.GetUtcNow());
        Assert.True(await kit.Planner.IsAutomaticAsync(kit.FakeQuotes[demoEra]));

        kit.Clock.Advance(TimeSpan.FromDays(1));
        await kit.Secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, Second);
        var secondFrom = kit.Clock.GetUtcNow();
        Assert.Equal(secondFrom, await kit.Planner.AutomaticFromAsync());
        Assert.False(await kit.Planner.IsAutomaticAsync(kit.FakeQuotes[demoEra]));
        Assert.Empty(await kit.Planner.PlanAsync(demoEra, null));

        // Recorded once, never moved — also across restarts — and per organisation.
        kit.Clock.Advance(TimeSpan.FromDays(2));
        var restarted = new XeroQuotePlanner(kit.Quotes, kit.Links, kit.Outbox, kit.Store, kit.Secrets, kit.Files, timeProvider: kit.Clock);
        Assert.Equal(secondFrom, await restarted.AutomaticFromAsync());
        Assert.Equal(startedAt, await restarted.AutomaticFromAsync(QuoteSyncTestKit.TenantId));
        Assert.Equal(startedAt, await restarted.AutomaticFromAsync((string?)null));
    }

    [Fact]
    public async Task AStartRecordedBeforeF1_IsTakenOverByTheFirstOrganisationOnly()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var recordedBeforeF1 = kit.Clock.GetUtcNow().AddDays(-10);
        await kit.Store.WriteAsync(XeroQuotePlanner.StateCollection, XeroQuotePlanner.AutomaticFromKey, recordedBeforeF1.ToString("O", CultureInfo.InvariantCulture));

        Assert.Equal(recordedBeforeF1, await kit.Planner.AutomaticFromAsync());

        await kit.Secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, Second);
        Assert.Equal(kit.Clock.GetUtcNow(), await kit.Planner.AutomaticFromAsync());
    }

    [Fact]
    public async Task ASendToXero_CountsOnlyForTheOrganisationItWasMadeFor()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        await kit.Planner.AutomaticFromAsync(); // the first organisation is tenant-1
        var old = Guid.NewGuid();
        kit.FakeQuotes[old] = QuoteSyncTestKit.Quote(old, QuotationStatus.Sent, reference: "Q-OLD", issuedAt: kit.Clock.GetUtcNow().AddDays(-5));
        Assert.Empty(await kit.Planner.PlanAsync(old, null));

        Assert.True((await kit.Planner.SendToXeroAsync(old)).Queued);
        Assert.NotNull(await kit.Store.ReadAsync(XeroQuotePlanner.StateCollection, XeroQuotePlanner.OptInKey(old, QuoteSyncTestKit.TenantId)));
        Assert.NotEmpty(await kit.Planner.PlanAsync(old, null));

        await kit.Secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, Second);
        Assert.Empty(await kit.Planner.PlanAsync(old, null));

        Assert.True((await kit.Planner.SendToXeroAsync(old)).Queued);
        Assert.NotEmpty(await kit.Planner.PlanAsync(old, null));
    }

    [Fact]
    public async Task AnOptInRecordedBeforeF1_CountsForTheFirstOrganisationOnly()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var old = Guid.NewGuid();
        kit.FakeQuotes[old] = QuoteSyncTestKit.Quote(old, QuotationStatus.Sent, reference: "Q-OLD", issuedAt: kit.Clock.GetUtcNow().AddDays(-5));
        await kit.Store.WriteAsync(XeroQuotePlanner.StateCollection, XeroQuotePlanner.OptInKey(old), """{"requestedAtUtc":"2026-10-01T09:00:00.0000000+00:00","contentHash":null}""");

        Assert.NotEmpty(await kit.Planner.PlanAsync(old, null));

        await kit.Secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, Second);
        Assert.Empty(await kit.Planner.PlanAsync(old, null));
    }
}

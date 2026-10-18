using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// Verifier F3 defect 4: a badge's <em>Check Xero now</em> reads the record it
/// was clicked on first (<see cref="XeroSyncService.ReadBackNowAsync(XeroDocumentRef?, CancellationToken)"/>),
/// however many other links are due before it in the pass's budget.
/// </summary>
public sealed class XeroReadBackFirstTests
{
    [Fact]
    public async Task ReadBackNow_ForAClickedRecord_ReadsItFirst_EvenWhenOthersAreDueBeforeIt()
    {
        using var kit = await EngineTestKit.CreateAsync(new XeroSyncOptions { Jitter = () => 0.5, ReadBackBudget = 1 });
        var older = EngineTestKit.OrderRef(kit.IssueOrder("PO-2026-001"));
        var clicked = EngineTestKit.OrderRef(kit.IssueOrder("PO-2026-002"));
        await kit.SettleAsync();
        foreach (var order in kit.LiveOrders)
            kit.Simulator.ApproveInXero(order.Id);

        // The clicked record was read most recently, so the budget of one would go to the other.
        var link = (await kit.LinkAsync(clicked))!;
        await kit.Links.SaveAsync(link with { LastReadAtUtc = kit.Clock.GetUtcNow().AddHours(1) });

        kit.Clock.Advance(TimeSpan.FromMinutes(1)); // this pass's reading is newer than the other's
        var report = await kit.Engine.ReadBackNowAsync(clicked);

        Assert.NotNull(report);
        Assert.Equal(1, report!.Read);
        Assert.Equal([clicked], report.Changed);
        Assert.Equal("AUTHORISED", (await kit.LinkAsync(clicked))!.LastKnownXeroStatus);
        Assert.NotEqual("AUTHORISED", (await kit.LinkAsync(older))!.LastKnownXeroStatus);

        // Without a clicked record the pass keeps its rotation: oldest reading first.
        var rotation = await kit.Engine.ReadBackNowAsync();
        Assert.Equal([older], rotation!.Changed);
        kit.AssertNoViolations();
    }
}

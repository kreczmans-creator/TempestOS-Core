using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>`v0.24.0` X5 — the durable log of purchasing creates sent to Xero, the handlers' only proof a found record is their own.</summary>
public sealed class XeroPurchasingCreateLogTests
{
    private static readonly XeroDocumentRef Document = new(XeroDocumentKind.ExpenseBill, Guid.NewGuid().ToString("D"));

    [Fact]
    public async Task ACreateRecordedBeforeSending_IsSent_UnderItsNumberAndContactOnly()
    {
        var log = new XeroPurchasingCreateLog(new YieldingInMemoryPersistenceStore());
        Assert.False(await log.WasSentAsync("t1", Document, "NS-1", "c1"));

        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k1");

        Assert.True(await log.WasSentAsync("t1", Document, "ns-1", "C1"));
        Assert.True(await log.WasSentAsync("t1", Document, "NS-1", "c1", "k1"));
        Assert.False(await log.WasSentAsync("t1", Document, "NS-1", "c1", "k2"));
        Assert.False(await log.WasSentAsync("t1", Document, "NS-2", "c1"));
        Assert.False(await log.WasSentAsync("t1", Document, "NS-1", "c2"));
        Assert.False(await log.WasSentAsync("t2", Document, "NS-1", "c1"));
    }

    [Theory]
    [InlineData(ConnectorOutcome.Rejected, false)]
    [InlineData(ConnectorOutcome.Reauthorise, false)]
    [InlineData(ConnectorOutcome.Ok, true)]
    [InlineData(ConnectorOutcome.Unknown, true)]
    [InlineData(ConnectorOutcome.Unavailable, true)]
    public async Task OnlyADefiniteRefusal_StrikesTheCreateOff(ConnectorOutcome outcome, bool stillSent)
    {
        var store = new YieldingInMemoryPersistenceStore();
        var log = new XeroPurchasingCreateLog(store);
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k1");

        await log.RecordAnswerAsync("t1", Document, "k1", outcome);

        Assert.Equal(stillSent, await new XeroPurchasingCreateLog(store).WasSentAsync("t1", Document, "NS-1", "c1"));
    }

    [Fact]
    public async Task AnEarlierLostCreate_SurvivesALaterRefusedOne()
    {
        var log = new XeroPurchasingCreateLog(new YieldingInMemoryPersistenceStore());
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k1");
        await log.RecordAnswerAsync("t1", Document, "k1", ConnectorOutcome.Unknown);
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k2");
        await log.RecordAnswerAsync("t1", Document, "k2", ConnectorOutcome.Rejected);

        Assert.True(await log.WasSentAsync("t1", Document, "NS-1", "c1"));
        Assert.False(await log.WasSentAsync("t1", Document, "NS-1", "c1", "k2"));
    }

    [Fact]
    public async Task ListSent_GivesEveryNumberAndContactStillOnTheLog_ForThatDocumentOnly()
    {
        var log = new XeroPurchasingCreateLog(new YieldingInMemoryPersistenceStore());
        Assert.Empty(await log.ListSentAsync("t1", Document));

        await log.RecordSendingAsync("t1", Document, "NS-5", "c1", "k1");
        await log.RecordSendingAsync("t1", Document, "NS-6", "c2", "k2");
        await log.RecordSendingAsync("t1", Document, "NS-7", "c2", "k3");
        await log.RecordAnswerAsync("t1", Document, "k3", ConnectorOutcome.Rejected);

        Assert.Equal(
            [new XeroPurchasingSentCreate("NS-5", "c1", "k1"), new XeroPurchasingSentCreate("NS-6", "c2", "k2")],
            await log.ListSentAsync("t1", Document));
        Assert.Empty(await log.ListSentAsync("t2", Document));
        Assert.Empty(await log.ListSentAsync("t1", new XeroDocumentRef(XeroDocumentKind.ExpenseBill, Guid.NewGuid().ToString("D"))));
    }
}

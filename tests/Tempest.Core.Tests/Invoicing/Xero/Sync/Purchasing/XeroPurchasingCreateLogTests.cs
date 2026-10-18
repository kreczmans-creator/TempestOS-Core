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

    [Fact]
    public async Task TheReferenceACreateCarried_IsKept_Trimmed()
    {
        var log = new XeroPurchasingCreateLog(new YieldingInMemoryPersistenceStore());
        await log.RecordSendingAsync("t1", Document, "PO-1", "c1", "k1", reference: " P0012 ");
        await log.RecordSendingAsync("t1", Document, "PO-1", "c1", "k2", reference: "  ");

        Assert.Equal(
            [new XeroPurchasingSentCreate("PO-1", "c1", "k1", "P0012"), new XeroPurchasingSentCreate("PO-1", "c1", "k2")],
            await log.ListSentAsync("t1", Document));
    }

    [Fact]
    public async Task CreatesForOtherDocuments_OfTheKindInTheTenant_AreListed_NeverThisOnesOrAnotherKindsOrTenants()
    {
        var store = new YieldingInMemoryPersistenceStore();
        var log = new XeroPurchasingCreateLog(store);
        var other = new XeroDocumentRef(XeroDocumentKind.ExpenseBill, Guid.NewGuid().ToString("D"));
        var order = new XeroDocumentRef(XeroDocumentKind.PurchaseOrder, Guid.NewGuid().ToString("D"));
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "own", value: "v");
        await log.RecordSendingAsync("t1", other, "NS-1", "c1", "other", value: "v");
        await log.RecordSendingAsync("t1", order, "NS-1", "c1", "order", value: "v");
        await log.RecordSendingAsync("t2", other, "NS-1", "c1", "other-tenant", value: "v");
        await store.WriteAsync(XeroPurchasingCreateLog.Collection, $"t1/{XeroDocumentKind.ExpenseBill}/{Guid.NewGuid():D}", "not json");

        var listed = await log.ListSentForOthersAsync("t1", Document);

        Assert.Equal([new XeroPurchasingSentCreate("NS-1", "c1", "other", null, "v")], listed);
    }

    [Fact]
    public async Task AnOkAnswer_RecordsTheRecordsId_AndARepeatOfTheSameSendKeepsItsFirstTime()
    {
        var log = new XeroPurchasingCreateLog(new YieldingInMemoryPersistenceStore());
        var first = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k1", body: "{}", sentAtUtc: first);
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k1", body: "{}", sentAtUtc: first.AddMinutes(3));

        var sent = Assert.Single(await log.ListSentAsync("t1", Document));
        Assert.Equal(first, sent.SentAtUtc); // Xero's key lifetime runs from the first call.
        Assert.Null(sent.XeroId);

        await log.RecordAnswerAsync("t1", Document, "k1", ConnectorOutcome.Ok, xeroId: "x-1");
        await log.RecordXeroIdAsync("t1", Document, "k1", "x-2"); // A known id is kept.
        Assert.Equal("x-1", Assert.Single(await log.ListSentAsync("t1", Document)).XeroId);
    }

    [Fact]
    public async Task ATombstone_IsKept_UntilSendAgainReleasesIt()
    {
        var log = new XeroPurchasingCreateLog(new YieldingInMemoryPersistenceStore());
        await log.RecordSendingAsync("t1", Document, "NS-1", "c1", "k1", body: "{}");
        Assert.Equal(0, await log.ReleaseGoneAsync("t1", Document, DateTimeOffset.UnixEpoch));

        await log.RecordGoneAsync("t1", Document, "k1", "x-1", "voided", "NS-1A");
        var gone = Assert.Single(await log.ListSentAsync("t1", Document));
        Assert.True(gone.IsTombstone);
        Assert.Equal(("x-1", "VOIDED", "NS-1A"), (gone.XeroId, gone.GoneStatus, gone.XeroNumber));

        Assert.Equal(1, await log.ReleaseGoneAsync("t1", Document, DateTimeOffset.UnixEpoch));
        var released = Assert.Single(await log.ListSentAsync("t1", Document));
        Assert.False(released.IsTombstone);
        Assert.Equal(DateTimeOffset.UnixEpoch, released.ReleasedAtUtc);
        Assert.Equal(0, await log.ReleaseGoneAsync("t1", Document, DateTimeOffset.UnixEpoch));
    }
}

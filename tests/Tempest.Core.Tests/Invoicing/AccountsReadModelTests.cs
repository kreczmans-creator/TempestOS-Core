using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// <see cref="AccountsReadModel"/>'s own acceptance (`WP 19.8B` scope §3):
/// <see cref="AccountsSnapshot.Unavailable"/> when there has never been a
/// reading, and — from a scripted reading plus two real sent
/// <see cref="InvoiceRequest"/>s, one overdue — the tiles and the first two
/// weeks of the 12-week cash-flow series compute to hand-checked figures.
/// The two requests are built directly through
/// <see cref="EngineeringObjectFactory{T}"/> and moved to
/// <see cref="InvoiceRequestStatus.Sent"/> through <c>InvoiceRequest</c>'s
/// own internal mutators — this read model cares only about a request's
/// own <c>Status</c>/<c>IssuedDate</c>/<c>Total</c>, never how it was
/// raised, so the full timesheet/deliverable/rate-card machinery
/// <c>InvoicingJourneyTests</c> exercises is not needed here.
/// </summary>
public sealed class AccountsReadModelTests
{
    private static readonly DateOnly AsOf = new(2026, 3, 10);

    [Fact]
    public async Task ReadAsync_NoReadingEverSaved_IsUnavailable()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        try
        {
            var domain = InvoicingTestHost.Domain(host);
            var store = new FileAccountsReadingStore(Path.Combine(temp.Path, "accounts"));
            var readModel = new AccountsReadModel(store, domain, refreshService: null, timeProvider: new FixedTimeProvider(AsOf));

            var snapshot = await readModel.ReadAsync();

            Assert.False(snapshot.IsAvailable);
            Assert.Equal("No accounts reading yet.", snapshot.UnavailableReason);
            Assert.Null(snapshot.UnavailableSince);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReadAsync_AScriptedReadingPlusTwoSentInvoiceRequests_OneOverdue_ComputesTheTilesAndTheCashFlowSeries_ToHandCheckedFigures()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        try
        {
            var domain = InvoicingTestHost.Domain(host);
            var store = new FileAccountsReadingStore(Path.Combine(temp.Path, "accounts"));

            // The reading — as if the connector had answered it a moment
            // ago (`WP 19.8B` scope §2's own "a scripted reading").
            var reading = new AccountsReading(
                Bills: [new BillDue("Acme Ltd", "INV-1", AsOf.AddDays(-2), AsOf.AddDays(5), new Money(300m, CurrencyCode.Gbp), "AUTHORISED")],
                RepeatingBills:
                [
                    new CategorisedRepeatingBill(
                        new RepeatingBill("Contoso Cloud", "Software licence", new Money(99m, CurrencyCode.Gbp), "MONTHLY", AsOf.AddDays(5), "Software Subscription"),
                        AccountsCategory.Software),
                ],
                Cash: [new CashAccountBalance("Business Current Account", new Money(5000m, CurrencyCode.Gbp), AsOf)],
                ReadAt: new DateTimeOffset(AsOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                Connector: "Fake");
            await store.SaveAsync(reading);

            // Two sent requests, each `Up front` terms (0 days), so its own
            // `DueOn` is the date it was sent (`TD-180`, `WP 20.1B`): one
            // sent 10 days before `AsOf` (due 10 days before `AsOf`, so
            // overdue), issued (a separate, later-read fact) 40 days
            // before `AsOf`; one sent 10 days after `AsOf` (so Due30),
            // issued 20 days before `AsOf`.
            var overdue = await CreateSentRequestAsync(domain, "ORG-A", new Money(1000m, CurrencyCode.Gbp), AsOf.AddDays(-40), AsOf.AddDays(-10));
            var due30 = await CreateSentRequestAsync(domain, "ORG-B", new Money(500m, CurrencyCode.Gbp), AsOf.AddDays(-20), AsOf.AddDays(10));

            var readModel = new AccountsReadModel(store, domain, refreshService: null, timeProvider: new FixedTimeProvider(AsOf));

            var snapshot = await readModel.ReadAsync();

            Assert.True(snapshot.IsAvailable);
            Assert.Equal("Fake", snapshot.Connector);
            Assert.Equal(AsOf, snapshot.AsOf);

            // ---- tiles ----
            Assert.Equal(new Money(1500m, CurrencyCode.Gbp), snapshot.InvoicedTotal);
            Assert.Equal(new Money(1000m, CurrencyCode.Gbp), snapshot.OverdueTotal);
            Assert.Equal(new Money(500m, CurrencyCode.Gbp), snapshot.Due30Total);
            Assert.Equal(Money.Zero(CurrencyCode.Gbp), snapshot.Due90Total);

            var overdueRow = Assert.Single(snapshot.Overdue);
            Assert.Equal(overdue.Id, overdueRow.RequestId);
            var due30Row = Assert.Single(snapshot.Due30);
            Assert.Equal(due30.Id, due30Row.RequestId);
            Assert.Empty(snapshot.Due90);

            // ---- payable ----
            Assert.Single(snapshot.BillsDueWithin30);
            Assert.Single(snapshot.SubscriptionsDueWithin60);
            Assert.Equal(new Money(99m, CurrencyCode.Gbp), snapshot.SubscriptionTotalsByCategory[AccountsCategory.Software]);
            Assert.Equal(Money.Zero(CurrencyCode.Gbp), snapshot.SubscriptionTotalsByCategory[AccountsCategory.Hardware]);

            // ---- cash and the 12-week series ----
            Assert.Equal(new Money(5000m, CurrencyCode.Gbp), snapshot.TotalCash);
            Assert.Equal(12, snapshot.CashFlowSeries.Count);

            // Week 1 (AsOf..AsOf+6): the bill and subscription (both due
            // AsOf+5) land here; the Due30 receivable (due AsOf+10) does
            // not yet. 5000 + 0 - (300 + 99) = 4601.
            var week1 = snapshot.CashFlowSeries[0];
            Assert.Equal(AsOf, week1.WeekStart);
            Assert.Equal(Money.Zero(CurrencyCode.Gbp), week1.ReceivableIn);
            Assert.Equal(new Money(399m, CurrencyCode.Gbp), week1.PayableOut);
            Assert.Equal(new Money(4601m, CurrencyCode.Gbp), week1.ClosingCash);

            // Week 2 (AsOf+7..AsOf+13): the Due30 receivable's own due date
            // (AsOf+10) lands here, nothing payable. 4601 + 500 - 0 = 5101.
            var week2 = snapshot.CashFlowSeries[1];
            Assert.Equal(AsOf.AddDays(7), week2.WeekStart);
            Assert.Equal(new Money(500m, CurrencyCode.Gbp), week2.ReceivableIn);
            Assert.Equal(Money.Zero(CurrencyCode.Gbp), week2.PayableOut);
            Assert.Equal(new Money(5101m, CurrencyCode.Gbp), week2.ClosingCash);

            // Nothing more is scripted — every remaining week carries the
            // running total flat.
            Assert.All(snapshot.CashFlowSeries.Skip(2), week =>
            {
                Assert.Equal(Money.Zero(CurrencyCode.Gbp), week.ReceivableIn);
                Assert.Equal(Money.Zero(CurrencyCode.Gbp), week.PayableOut);
                Assert.Equal(new Money(5101m, CurrencyCode.Gbp), week.ClosingCash);
            });
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Builds and sends a request — `Up front` terms throughout (the
    /// constructor's own default), so <paramref name="dueDate"/> is passed
    /// straight through as <c>MarkSentAsync</c>'s own <c>sentAtUtc</c>
    /// (0 days out, `InvoiceRequest.DueOn`'s own remarks). <paramref name="issuedDate"/>
    /// is a separate, later-read fact — the connector's own report, read
    /// back through <c>RecordStatusReadingAsync</c> exactly as before —
    /// and no longer drives the due-date arithmetic at all (`TD-180`,
    /// `WP 20.1B`).
    /// </summary>
    private static async Task<InvoiceRequest> CreateSentRequestAsync(
        EngineeringDomainContext domain, string organisationId, Money total, DateOnly issuedDate, DateOnly dueDate)
    {
        var lines = new List<InvoiceRequestLine> { new("TestSource", Guid.NewGuid(), "Test line", 1m, total, total) };

        var created = (InvoiceRequest)await new EngineeringObjectFactory<InvoiceRequest>(
            InvoiceRequest.CanonicalKind, domain,
            (doc, rev) => new InvoiceRequest(
                doc, rev, domain, identifier: null, $"Test request {organisationId}", EngineeringObjectMetadata.Empty,
                organisationId, purchaseOrderReference: null, total.Currency, lines, total))
            .CreateAsync("Accounts read model test fixture.")
            .ConfigureAwait(false);

        var sentAtUtc = new DateTimeOffset(dueDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await created.MarkSentAsync($"ext-{organisationId}", $"INV-{organisationId}", sentAtUtc).ConfigureAwait(false);
        await created.RecordStatusReadingAsync(InvoiceRequestStatus.Sent, "AUTHORISED", null, issuedDate, null).ConfigureAwait(false);

        return created;
    }

    private sealed class FixedTimeProvider(DateOnly date) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}

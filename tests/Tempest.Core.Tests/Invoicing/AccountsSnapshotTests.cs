using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// <see cref="AccountsSnapshot.From"/>'s own bucket boundaries (`WP 19.8B`
/// scope §3) — the pure-function edges <c>AccountsReadModelTests</c>'s own
/// larger, end-to-end scenario does not itself exercise one at a time.
/// </summary>
public sealed class AccountsSnapshotTests
{
    private static readonly DateOnly AsOf = new(2026, 3, 10);

    [Theory]
    [InlineData(-1, "Overdue")]
    [InlineData(0, "Due30")]
    [InlineData(30, "Due30")]
    [InlineData(31, "Due90")]
    [InlineData(90, "Due90")]
    [InlineData(91, "None")]
    public void From_DueDateBoundaries_SortIntoExactlyOneBucket(int daysFromAsOf, string expectedBucket)
    {
        var receivable = new ReceivableInvoice(Guid.NewGuid(), "ORG-1", AsOf, AsOf.AddDays(daysFromAsOf), new Money(100m, CurrencyCode.Gbp));
        var reading = EmptyReading();

        var snapshot = AccountsSnapshot.From(reading, [receivable], AsOf);

        Assert.Equal(expectedBucket == "Overdue" ? 1 : 0, snapshot.Overdue.Count);
        Assert.Equal(expectedBucket == "Due30" ? 1 : 0, snapshot.Due30.Count);
        Assert.Equal(expectedBucket == "Due90" ? 1 : 0, snapshot.Due90.Count);

        // Every bucket total agrees with its own count, and the invoiced
        // total counts the request regardless of which bucket (or none)
        // it landed in.
        Assert.Equal(new Money(100m, CurrencyCode.Gbp), snapshot.InvoicedTotal);
    }

    [Fact]
    public void From_NoReceivablesAndAnEmptyReading_EveryTotalIsZero_NoThrow()
    {
        var snapshot = AccountsSnapshot.From(EmptyReading(), [], AsOf);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(Money.Zero(CurrencyCode.Gbp), snapshot.InvoicedTotal);
        Assert.Equal(Money.Zero(CurrencyCode.Gbp), snapshot.TotalCash);
        Assert.Equal(12, snapshot.CashFlowSeries.Count);
        Assert.All(snapshot.CashFlowSeries, w => Assert.Equal(Money.Zero(CurrencyCode.Gbp), w.ClosingCash));
    }

    [Fact]
    public void From_BillsAndSubscriptionsBeyondTheirOwnWindow_AreExcludedFromThePayableTiles()
    {
        var reading = new AccountsReading(
            Bills: [new BillDue("Supplier", "R1", AsOf, AsOf.AddDays(31), new Money(50m, CurrencyCode.Gbp), "AUTHORISED")],
            RepeatingBills: [new CategorisedRepeatingBill(new RepeatingBill("Supplier", "Sub", new Money(10m, CurrencyCode.Gbp), "MONTHLY", AsOf.AddDays(61), "Other"), AccountsCategory.Other)],
            Cash: [],
            ReadAt: DateTimeOffset.UtcNow,
            Connector: "Fake");

        var snapshot = AccountsSnapshot.From(reading, [], AsOf);

        Assert.Empty(snapshot.BillsDueWithin30);
        Assert.Empty(snapshot.SubscriptionsDueWithin60);
    }

    [Fact]
    public void Unavailable_EveryMoneyMemberIsZero_AndIsAvailableIsFalse()
    {
        var snapshot = AccountsSnapshot.Unavailable("No accounts reading yet.", null, AsOf);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("No accounts reading yet.", snapshot.UnavailableReason);
        Assert.Null(snapshot.ReadAt);
        Assert.Null(snapshot.Connector);
        Assert.Empty(snapshot.CashFlowSeries);
    }

    private static AccountsReading EmptyReading() => new([], [], [], DateTimeOffset.UtcNow, "Fake");
}

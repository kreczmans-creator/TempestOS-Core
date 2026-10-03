using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// `v0.24.0` X7: <see cref="ForwardCashProjection"/>'s arithmetic, by hand.
/// Every amount is gross (including VAT) — the projection never adds or
/// strips VAT itself; its inputs are already gross (<see cref="ForwardCashProjection.VatBasis"/>).
/// Today is 2026-10-02, so the six months are October 2026 (from the 2nd)
/// to March 2027.
/// </summary>
public sealed class AccountsForwardCashProjectionTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 2);
    private static readonly DateTimeOffset ReadAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InvoiceReadAt = new(2026, 10, 2, 7, 30, 0, TimeSpan.Zero);

    private static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    private static AccountsReading Reading(
        IReadOnlyList<BillDue>? bills = null, IReadOnlyList<RepeatingBill>? repeating = null, IReadOnlyList<CashAccountBalance>? cash = null) => new(
        bills ?? [],
        [.. (repeating ?? []).Select(r => new CategorisedRepeatingBill(r, AccountsCategory.Other))],
        cash ?? [new CashAccountBalance("Business Current Account", Gbp(10_000m), AsOf)],
        ReadAt,
        "Xero");

    private static ForwardCashInvoice Invoice(string number, DateOnly due, decimal gross, bool awaitingApproval = false) => new(
        Guid.NewGuid(), $"Invoice {number}", due, Gbp(gross), new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Xero invoice", InvoiceReadAt), awaitingApproval);

    private static ForwardCashPipelineLine Milestone(string name, DateOnly? date, decimal gross) =>
        new(Guid.NewGuid(), name, date, date is null ? "no planned or target date" : "milestone target date", Gbp(gross));

    private static ForwardCashInputs Fixture() => new(
        AsOf,
        Reading(
            bills:
            [
                new BillDue("Steel Ltd", "B-OVERDUE", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 25), Gbp(120m), "AUTHORISED"),
                new BillDue("Hire Co", "B-NOV", new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 15), Gbp(600m), "AUTHORISED"),
            ],
            repeating:
            [
                new RepeatingBill("Contoso Cloud", "Software licence", Gbp(99m), "MONTHLY", new DateOnly(2026, 10, 20), "Software"),
                new RepeatingBill("Insurer plc", "Professional indemnity", Gbp(300m), "QUARTERLY", new DateOnly(2026, 12, 1), null),
            ]),
        [
            Invoice("INV-1", new DateOnly(2026, 10, 30), 1_200m),
            Invoice("INV-OLD", new DateOnly(2026, 9, 1), 2_400m),
        ],
        [
            Milestone("Q-1: Detailed design", new DateOnly(2026, 12, 15), 3_000m),
            Milestone("Q-1: Survey (completed, not invoiced)", new DateOnly(2026, 9, 28), 500m),
            Milestone("Q-2: Undated stage", null, 800m),
            Milestone("Q-3: Next year", new DateOnly(2027, 5, 1), 1_000m),
        ]);

    [Fact]
    public void SixMonths_SumCorrectly_ToHandCheckedFigures()
    {
        var picture = ForwardCashProjection.Build(Fixture());

        Assert.Equal(6, picture.Months.Count);
        Assert.Equal(CurrencyCode.Gbp, picture.Currency);
        Assert.Equal([new(2026, 10, 1), new(2026, 11, 1), new(2026, 12, 1), new(2027, 1, 1), new(2027, 2, 1), new DateOnly(2027, 3, 1)], picture.Months.Select(m => m.MonthStart));
        Assert.Equal(AsOf, picture.Months[0].From);
        Assert.Equal(new DateOnly(2026, 10, 31), picture.Months[0].To);
        Assert.Equal(new DateOnly(2027, 3, 31), picture.Months[5].To);

        // October: opening 10,000 (bank); in = 1,200 + 2,400 (overdue, counted now) + 500 (completed, counted now);
        // out = 120 (overdue bill) + 99 (licence 20 Oct). 10,000 + 4,100 - 219 = 13,881.
        AssertMonth(picture.Months[0], opening: 10_000m, invoices: 3_600m, milestones: 500m, bills: 120m, repeating: 99m, closing: 13_881m);
        // November: bill 600, licence 99. 13,881 - 699 = 13,182.
        AssertMonth(picture.Months[1], opening: 13_881m, invoices: 0m, milestones: 0m, bills: 600m, repeating: 99m, closing: 13_182m);
        // December: milestone 3,000; licence 99 + quarterly 300. 13,182 + 3,000 - 399 = 15,783.
        AssertMonth(picture.Months[2], opening: 13_182m, invoices: 0m, milestones: 3_000m, bills: 0m, repeating: 399m, closing: 15_783m);
        AssertMonth(picture.Months[3], opening: 15_783m, invoices: 0m, milestones: 0m, bills: 0m, repeating: 99m, closing: 15_684m);
        AssertMonth(picture.Months[4], opening: 15_684m, invoices: 0m, milestones: 0m, bills: 0m, repeating: 99m, closing: 15_585m);
        // March: licence 99 + quarterly 300 (1 Dec + 3 months).
        AssertMonth(picture.Months[5], opening: 15_585m, invoices: 0m, milestones: 0m, bills: 0m, repeating: 399m, closing: 15_186m);

        // The whole: closing = opening + every month's in - every month's out.
        var totalIn = picture.Months.Sum(m => m.MoneyIn.Amount!.Value.Amount);
        var totalOut = picture.Months.Sum(m => m.MoneyOut.Amount!.Value.Amount);
        Assert.Equal(10_000m + totalIn - totalOut, picture.Months[^1].ClosingCash.Amount!.Value.Amount);

        // Undated and later are in no month's total.
        var undated = Assert.Single(picture.Undated);
        Assert.Equal(Gbp(800m), undated.Gross);
        Assert.Null(undated.Date);
        Assert.Contains("undated", undated.Note, StringComparison.Ordinal);
        Assert.Equal(Gbp(800m), picture.UndatedTotal);
        var later = Assert.Single(picture.Later);
        Assert.Equal(new DateOnly(2027, 5, 1), later.Date);
        Assert.DoesNotContain(picture.Months.SelectMany(m => m.Items), i => i.Gross == Gbp(800m) || i.Gross == Gbp(1_000m));
    }

    [Fact]
    public void EveryFigure_SaysItsSource()
    {
        var picture = ForwardCashProjection.Build(Fixture());
        var october = picture.Months[0];

        Assert.Equal("Xero bank balances, read at 2026-10-02 08:00 UTC", october.OpeningCash.Source.Describe());
        Assert.Equal(ForwardCashSourceKind.AccountingPackage, october.OpeningCash.Source.Kind);
        Assert.Equal("Xero bills, read at 2026-10-02 08:00 UTC", october.BillsDue.Source.Describe());
        Assert.Equal("Xero repeating bills, read at 2026-10-02 08:00 UTC", october.RepeatingBills.Source.Describe());
        // Invoices: read at the oldest read-back of the month's invoices.
        Assert.Equal("Xero invoices raised in TempestOS, status as read back, read at 2026-10-02 07:30 UTC", october.InvoicesDue.Source.Describe());
        Assert.Equal(ForwardCashSourceKind.TempestOS, october.ExpectedMilestones.Source.Kind);
        Assert.Equal("TempestOS — accepted quotes not yet invoiced", october.ExpectedMilestones.Source.Describe());
        Assert.Equal(ForwardCashSourceKind.Derived, october.MoneyIn.Source.Kind);
        Assert.Equal(ForwardCashSourceKind.Derived, october.ClosingCash.Source.Kind);
        Assert.Equal(ForwardCashSourceKind.Derived, picture.Months[1].OpeningCash.Source.Kind);

        // Items say their own source and why they are where they are.
        Assert.Contains(october.Items, i => i.Kind == ForwardCashItemKind.InvoiceDue && i.Note!.Contains("overdue (due 2026-09-01)", StringComparison.Ordinal));
        Assert.Contains(october.Items, i => i.Kind == ForwardCashItemKind.BillDue && i.Note == "overdue (due 2026-09-25)");
        Assert.Contains(october.Items, i => i.Kind == ForwardCashItemKind.ExpectedMilestone && i.Note!.Contains("date passed, counted now", StringComparison.Ordinal));
        Assert.All(picture.Months.SelectMany(m => m.Items).Where(i => i.Kind == ForwardCashItemKind.ExpectedMilestone),
            i => Assert.Equal(Quotation.CanonicalKind, i.ObjectKind));
        Assert.All(picture.Months.SelectMany(m => m.Items).Where(i => i.Kind == ForwardCashItemKind.InvoiceDue),
            i => Assert.Equal(InvoiceRequest.CanonicalKind, i.ObjectKind));
        Assert.Contains("gross, including VAT", picture.VatBasis, StringComparison.Ordinal);
        Assert.Equal(ReadAt, picture.AccountsReadAt);
        Assert.Equal("Xero", picture.AccountsConnector);
        Assert.Null(picture.AccountsUnavailableReason);
    }

    [Fact]
    public void NoAccountsReading_XeroFiguresAreUnavailableWithTheReason_NeverZero_TempestOSFiguresStillShow()
    {
        var inputs = Fixture() with { Reading = null };
        inputs = inputs with { ReadingUnavailableReason = "Could not read bills: Xero has not been authorised." };

        var picture = ForwardCashProjection.Build(inputs);

        Assert.Equal("Could not read bills: Xero has not been authorised.", picture.AccountsUnavailableReason);
        Assert.Null(picture.AccountsReadAt);
        foreach (var month in picture.Months)
        {
            Assert.False(month.OpeningCash.IsAvailable);
            Assert.False(month.BillsDue.IsAvailable);
            Assert.False(month.RepeatingBills.IsAvailable);
            Assert.False(month.MoneyOut.IsAvailable);
            Assert.False(month.ClosingCash.IsAvailable);
            Assert.Null(month.ClosingCash.Amount);
            Assert.Equal("Could not read bills: Xero has not been authorised.", month.ClosingCash.UnavailableReason);
            Assert.True(month.InvoicesDue.IsAvailable);
            Assert.True(month.ExpectedMilestones.IsAvailable);
            Assert.True(month.MoneyIn.IsAvailable);
        }

        Assert.Equal(Gbp(3_600m), picture.Months[0].InvoicesDue.Amount);
        Assert.Equal(Gbp(3_000m), picture.Months[2].ExpectedMilestones.Amount);
        Assert.DoesNotContain(picture.Months.SelectMany(m => m.Items), i => i.Kind is ForwardCashItemKind.BillDue or ForwardCashItemKind.RepeatingBill);
    }

    [Fact]
    public void Offline_TheLastReadingIsUsed_AndTheFailedRefreshIsReported()
    {
        var failedAt = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        var inputs = Fixture() with { RefreshFailureReason = "Could not read bills: the network is unreachable.", RefreshFailedAtUtc = failedAt };

        var picture = ForwardCashProjection.Build(inputs);

        Assert.Equal(ReadAt, picture.AccountsReadAt);
        Assert.Equal("Could not read bills: the network is unreachable.", picture.RefreshFailureReason);
        Assert.Equal(failedAt, picture.RefreshFailedAtUtc);
        Assert.Equal(Gbp(13_881m), picture.Months[0].ClosingCash.Amount);
    }

    [Fact]
    public void QuotesAlone_NeverMoveCash_OnlyThePipelineLinesGiven()
    {
        var picture = ForwardCashProjection.Build(new ForwardCashInputs(AsOf, Reading(), [], []));

        Assert.All(picture.Months, m =>
        {
            Assert.Equal(Gbp(0m), m.MoneyIn.Amount);
            Assert.Equal(Gbp(0m), m.MoneyOut.Amount);
            Assert.Equal(Gbp(10_000m), m.ClosingCash.Amount);
        });
        Assert.Empty(picture.Undated);
        Assert.Empty(picture.Later);
    }

    [Fact]
    public void AFigureInAnotherCurrency_IsLeftOutAndListed_NeverConverted()
    {
        var euroInvoice = new ForwardCashInvoice(
            Guid.NewGuid(), "Invoice EU-1", new DateOnly(2026, 10, 20), new Money(900m, new CurrencyCode("EUR")),
            new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Xero invoice"), AwaitingApproval: false);
        var inputs = new ForwardCashInputs(AsOf, Reading(), [euroInvoice, Invoice("INV-2", new DateOnly(2026, 10, 20), 100m)], []);

        var picture = ForwardCashProjection.Build(inputs);

        Assert.Equal(Gbp(100m), picture.Months[0].InvoicesDue.Amount);
        var excluded = Assert.Single(picture.Exclusions);
        Assert.Equal(1, excluded.Count);
        Assert.Equal(new Money(900m, new CurrencyCode("EUR")), excluded.Total);
        Assert.Contains("not converted", excluded.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatingBills_AreProjectedFromTheirSchedule_WithoutDrift_AndAnUnknownScheduleCountsOnce()
    {
        var source = new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Xero repeating bills", ReadAt);
        var horizonEnd = new DateOnly(2027, 3, 31);

        var monthEnd = ForwardCashProjection.Occurrences(
            new RepeatingBill("Landlord", "Rent", Gbp(1_000m), "MONTHLY", new DateOnly(2026, 10, 31), "Premises"), AsOf, horizonEnd, source).ToList();
        Assert.Equal(
            [new(2026, 10, 31), new(2026, 11, 30), new(2026, 12, 31), new(2027, 1, 31), new(2027, 2, 28), new DateOnly(2027, 3, 31)],
            monthEnd.Select(o => o.Date!.Value));
        Assert.Equal("next scheduled", monthEnd[0].Note);
        Assert.All(monthEnd.Skip(1), o => Assert.Equal("projected from the MONTHLY schedule", o.Note));

        var weekly = ForwardCashProjection.Occurrences(
            new RepeatingBill("Cleaner", "Office clean", Gbp(40m), "Weekly", new DateOnly(2026, 10, 5), null), AsOf, new DateOnly(2026, 10, 31), source).ToList();
        Assert.Equal([new(2026, 10, 5), new(2026, 10, 12), new(2026, 10, 19), new DateOnly(2026, 10, 26)], weekly.Select(o => o.Date!.Value));

        // Overdue next occurrence: counted (now), later ones only from today.
        var overdue = ForwardCashProjection.Occurrences(
            new RepeatingBill("Phone Co", "Line rental", Gbp(30m), "WEEKLY", new DateOnly(2026, 9, 20), null), AsOf, new DateOnly(2026, 10, 10), source).ToList();
        Assert.Equal([new(2026, 9, 20), new DateOnly(2026, 10, 4)], overdue.Select(o => o.Date!.Value));
        Assert.Contains("overdue", overdue[0].Note, StringComparison.Ordinal);

        var unknown = Assert.Single(ForwardCashProjection.Occurrences(
            new RepeatingBill("Odd Co", "Odd schedule", Gbp(10m), "LUNAR", new DateOnly(2026, 10, 9), null), AsOf, horizonEnd, source));
        Assert.Contains("not understood", unknown.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoReadingAndNoFigures_TheCurrencyIsGbp_AndTheMonthsStillShow()
    {
        var picture = ForwardCashProjection.Build(new ForwardCashInputs(AsOf, null, [], []));

        Assert.Equal(CurrencyCode.Gbp, picture.Currency);
        Assert.Equal(6, picture.Months.Count);
        Assert.Equal("No accounts reading yet.", picture.AccountsUnavailableReason);
    }

    private static void AssertMonth(ForwardCashMonth month, decimal opening, decimal invoices, decimal milestones, decimal bills, decimal repeating, decimal closing)
    {
        Assert.Equal(Gbp(opening), month.OpeningCash.Amount);
        Assert.Equal(Gbp(invoices), month.InvoicesDue.Amount);
        Assert.Equal(Gbp(milestones), month.ExpectedMilestones.Amount);
        Assert.Equal(Gbp(invoices + milestones), month.MoneyIn.Amount);
        Assert.Equal(Gbp(bills), month.BillsDue.Amount);
        Assert.Equal(Gbp(repeating), month.RepeatingBills.Amount);
        Assert.Equal(Gbp(bills + repeating), month.MoneyOut.Amount);
        Assert.Equal(Gbp(closing), month.ClosingCash.Amount);
        Assert.Equal(Gbp(opening + invoices + milestones - bills - repeating), month.ClosingCash.Amount);
    }
}

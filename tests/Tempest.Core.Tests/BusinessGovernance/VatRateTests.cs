using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Expenses;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.BusinessGovernance;

/// <summary>
/// `WP 21.3B`: the closed <see cref="VatRate"/> vocabulary's own
/// percentages, and the computed VAT arithmetic <see cref="QuotationLine"/>/
/// <see cref="InvoiceRequestLine"/> and their own owning
/// <see cref="Quotation"/>/<see cref="InvoiceRequest"/> carry — every
/// figure derived, never stored, so it can never drift from what the
/// lines actually carry.
/// </summary>
public sealed class VatRateTests
{
    [Theory]
    [InlineData(VatRate.OutOfScope, 0.00)]
    [InlineData(VatRate.Standard, 0.20)]
    [InlineData(VatRate.Reduced, 0.05)]
    [InlineData(VatRate.Zero, 0.00)]
    [InlineData(VatRate.Exempt, 0.00)]
    public void Percentage_MatchesTheDeclaredRate(VatRate rate, double expected) =>
        Assert.Equal((decimal)expected, rate.Percentage());

    /// <summary>The default value — declared first, deliberately, so a pre-`WP 21.3B` line with no stored rate at all reads back as this.</summary>
    [Fact]
    public void OutOfScope_IsTheEnumsOwnDefaultValue() =>
        Assert.Equal(VatRate.OutOfScope, default(VatRate));

    [Theory]
    [InlineData(VatRate.OutOfScope, "Out of scope")]
    [InlineData(VatRate.Standard, "Standard (20%)")]
    [InlineData(VatRate.Reduced, "Reduced (5%)")]
    [InlineData(VatRate.Zero, "Zero-rated")]
    [InlineData(VatRate.Exempt, "Exempt")]
    public void DisplayName_IsTheWordsAScreenShows(VatRate rate, string expected) =>
        Assert.Equal(expected, rate.DisplayName());

    [Fact]
    public void QuotationLine_VatAmountAndGrossAmount_AreComputedFromNetAndRate()
    {
        var line = new QuotationLine(
            Guid.NewGuid(), "Detailed design", Hours: 10m, Rate: new Money(150m, CurrencyCode.Gbp), FixedPrice: null,
            Amount: new Money(1_500m, CurrencyCode.Gbp), Basis: QuotationLineBasis.Hourly, VatRate: VatRate.Standard);

        Assert.Equal(new Money(300m, CurrencyCode.Gbp), line.VatAmount);
        Assert.Equal(new Money(1_800m, CurrencyCode.Gbp), line.GrossAmount);
    }

    /// <summary>A line with no explicit rate reads as <see cref="VatRate.OutOfScope"/> — no VAT is ever added to a figure already stored.</summary>
    [Fact]
    public void QuotationLine_WithNoExplicitRate_AddsNoVat()
    {
        var line = new QuotationLine(
            Guid.NewGuid(), "Fixed-price milestone", Hours: null, Rate: null, FixedPrice: new Money(5_000m, CurrencyCode.Gbp),
            Amount: new Money(5_000m, CurrencyCode.Gbp), Basis: QuotationLineBasis.FixedPrice);

        Assert.Equal(VatRate.OutOfScope, line.VatRate);
        Assert.Equal(Money.Zero(CurrencyCode.Gbp), line.VatAmount);
        Assert.Equal(new Money(5_000m, CurrencyCode.Gbp), line.GrossAmount);
    }

    [Fact]
    public void InvoiceRequestLine_VatAmountAndGrossAmount_AreComputedFromAmountAndRate()
    {
        var line = new InvoiceRequestLine(
            "TimesheetEntry", Guid.NewGuid(), "Design hours", Quantity: 8m, UnitRate: new Money(120m, CurrencyCode.Gbp),
            Amount: new Money(960m, CurrencyCode.Gbp), VatRate: VatRate.Standard);

        Assert.Equal(new Money(192m, CurrencyCode.Gbp), line.VatAmount);
        Assert.Equal(new Money(1_152m, CurrencyCode.Gbp), line.GrossAmount);
    }

    /// <summary>A line built with no explicit rate (the record's own default parameter) reads as <see cref="VatRate.OutOfScope"/>, exactly as <see cref="QuotationLine"/>'s own identical default does.</summary>
    [Fact]
    public void InvoiceRequestLine_WithNoExplicitRate_AddsNoVat()
    {
        var line = new InvoiceRequestLine(
            "DeliverableCompletion", Guid.NewGuid(), "Fixed-price milestone", Quantity: 1m, UnitRate: new Money(2_000m, CurrencyCode.Gbp),
            Amount: new Money(2_000m, CurrencyCode.Gbp));

        Assert.Equal(VatRate.OutOfScope, line.VatRate);
        Assert.Equal(Money.Zero(CurrencyCode.Gbp), line.VatAmount);
        Assert.Equal(new Money(2_000m, CurrencyCode.Gbp), line.GrossAmount);
    }

    /// <summary>The totals a real, service-built <see cref="Quotation"/> reports — never a hand-constructed one, so this exercises the identical path a consultant's own Quote tab does.</summary>
    [Fact]
    public async Task Quotation_Totals_SumNetVatAndGrossAcrossMixedRateLines()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);

        try
        {
            QuotationTestHost.SignIn(host);
            var projectId = await QuotationTestHost.CreateProjectAsync(host, "VAT-QUOTE");
            var quotations = QuotationTestHost.Quotations(host);

            var created = await quotations.CreateAsync(projectId);
            Assert.True(created.Succeeded, created.Reason);
            var quotationId = created.Quotation!.Id;

            var standard = await quotations.AddLineAsync(quotationId, "Standard-rated", 10m, new Money(100m, CurrencyCode.Gbp), null, vatRate: VatRate.Standard);
            Assert.True(standard.Succeeded, standard.Reason);
            var reduced = await quotations.AddLineAsync(quotationId, "Reduced-rated", null, null, new Money(500m, CurrencyCode.Gbp), vatRate: VatRate.Reduced);
            Assert.True(reduced.Succeeded, reduced.Reason);
            var zero = await quotations.AddLineAsync(quotationId, "Zero-rated", null, null, new Money(200m, CurrencyCode.Gbp), vatRate: VatRate.Zero);
            Assert.True(zero.Succeeded, zero.Reason);

            var quotation = zero.Quotation!;

            // Net: 1000 + 500 + 200 = 1700. VAT: 200 + 25 + 0 = 225. Gross: 1925.
            Assert.Equal(new Money(1_700m, CurrencyCode.Gbp), quotation.Total);
            Assert.Equal(new Money(225m, CurrencyCode.Gbp), quotation.VatTotal);
            Assert.Equal(new Money(1_925m, CurrencyCode.Gbp), quotation.GrossTotal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The totals a real, service-raised <see cref="InvoiceRequest"/>
    /// reports, over two expense-sourced lines at different inferred VAT
    /// rates (`InvoicingService.InferVatRate`, `WP 21.3B`) — the request
    /// both expenses land on together, since <see cref="IInvoicingService.RaiseFromExpenseAsync"/>
    /// sweeps every unbilled expense for the project, not only the one named.
    /// </summary>
    [Fact]
    public async Task InvoiceRequest_Totals_SumNetVatAndGrossAcrossItsLines()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);

        try
        {
            ExpenseTestHost.SignIn(host);

            const string organisationId = "VAT-CLIENT-1";
            const string rateCardId = "VAT-CARD-1";

            await ExpenseTestHost.Organisations(host).RegisterAsync(
                organisationId, Tempest.Core.Tests.BusinessOperations.OperationsFixtures.Organisation(organisationId),
                Tempest.Core.Tests.BusinessOperations.OperationsFixtures.Verified());

            var card = new Tempest.Core.BusinessGovernance.Pricing.RateCard
            {
                Code = rateCardId,
                Name = "VAT totals rate card",
                EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
                Currency = CurrencyCode.Gbp,
                Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
                Entries = [new Tempest.Core.BusinessGovernance.Pricing.RateCardEntry("ENG-1", "Engineer", Tempest.Core.BusinessGovernance.Pricing.PricingBasis.Hourly, new Money(100m, CurrencyCode.Gbp), new Money(60m, CurrencyCode.Gbp), Grade: "Engineer")],
            };
            await ExpenseTestHost.RateCards(host).RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
            await BusinessGovernanceFixtures.ReleaseAsync((Tempest.Core.BusinessGovernance.Pricing.RateCardCatalog)ExpenseTestHost.RateCards(host), rateCardId);

            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "VAT-INVREQ");
            var commercial = ExpenseTestHost.ProjectCommercial(host);
            Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
            Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

            var expenses = ExpenseTestHost.Expenses(host);

            // 20% exactly.
            var standardExpense = await expenses.RecordAsync(
                projectId, DateOnly.FromDateTime(DateTime.UtcNow), "Standard-rated expense", Tempest.Core.Expenses.ExpenseCategory.Materials,
                new Money(1_000m, CurrencyCode.Gbp), new Money(200m, CurrencyCode.Gbp), billable: true);
            Assert.True(standardExpense.Succeeded, standardExpense.Reason);

            // 0% exactly.
            var zeroExpense = await expenses.RecordAsync(
                projectId, DateOnly.FromDateTime(DateTime.UtcNow), "Zero-rated expense", Tempest.Core.Expenses.ExpenseCategory.Materials,
                new Money(300m, CurrencyCode.Gbp), new Money(0m, CurrencyCode.Gbp), billable: true);
            Assert.True(zeroExpense.Succeeded, zeroExpense.Reason);

            var invoicing = ExpenseTestHost.Invoicing(host);
            var raised = await invoicing.RaiseFromExpenseAsync(standardExpense.Expense!.Id);
            Assert.True(raised.Succeeded, raised.Reason);

            var request = raised.Request!;
            Assert.Equal(2, request.Lines.Count);

            // Net: 1000 + 300 = 1300. VAT: 200 + 0 = 200. Gross: 1500.
            Assert.Equal(new Money(1_300m, CurrencyCode.Gbp), request.Total);
            Assert.Equal(new Money(200m, CurrencyCode.Gbp), request.VatTotal);
            Assert.Equal(new Money(1_500m, CurrencyCode.Gbp), request.GrossTotal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}

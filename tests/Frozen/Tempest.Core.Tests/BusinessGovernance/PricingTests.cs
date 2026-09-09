using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

// WP 18.0C (D-028): split out of the live
// tests/Tempest.Core.Tests/BusinessGovernance/PricingTests.cs the same day
// PricingService itself was frozen; RateCard/RateCardCatalog tests stayed
// live with the kept kinds.
public class PricingServiceTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static Money Gbp(decimal amount) => BusinessGovernanceFixtures.Gbp_(amount);

    private static async Task<(RateCardCatalog Cards, PricingService Service)> BuildAsync(
        RateCard? card = null,
        bool release = true)
    {
        var cards = BusinessGovernanceFixtures.BuildRateCardCatalog();

        await cards.RegisterAsync("rc-1", card ?? BusinessGovernanceFixtures.Card(), BusinessGovernanceFixtures.Verified());

        if (release)
            await BusinessGovernanceFixtures.ReleaseAsync(cards, "rc-1");

        return (cards, new PricingService(cards));
    }

    [Fact]
    public async Task AQuotationPricesAtTheCardsPublishedRates()
    {
        var (_, service) = await BuildAsync();

        var quotation = await service.QuoteAsync("RC-2026", Today, [new QuotationRequest("ENG-SEN", 10m)]);

        Assert.Equal(Gbp(7_500m), quotation.Total);
        Assert.Equal(Gbp(750m), Assert.Single(quotation.Lines).ListRate);
    }

    [Fact]
    public async Task AMinimumChargeRaisesALineAndSaysSo()
    {
        var (_, service) = await BuildAsync();

        var quotation = await service.QuoteAsync("RC-2026", Today, [new QuotationRequest("ENG-HR", 1m)]);

        var line = Assert.Single(quotation.Lines);

        Assert.Equal(Gbp(330m), line.LineTotal);
        Assert.True(line.MinimumApplied);
        Assert.True(quotation.AnyMinimumApplied);
    }

    [Fact]
    public async Task PricingIsExact_NotFloatingPoint()
    {
        var card = BusinessGovernanceFixtures.Card() with
        {
            Entries = [new RateCardEntry("ENG-X", "Fixture service", PricingBasis.Hourly, Gbp(0.1m))],
        };

        var (_, service) = await BuildAsync(card);

        var quotation = await service.QuoteAsync("RC-2026", Today, [new QuotationRequest("ENG-X", 3m)]);

        Assert.Equal(0.3m, quotation.Total.Amount);
    }

    [Fact]
    public async Task AnUnapprovedCard_CannotBeQuotedFrom()
    {
        // A published price binds the organisation to whoever it is shown
        // to. Released says the record is accurate; approval says the
        // prices are the ones the organisation intends.
        var (_, service) = await BuildAsync(BusinessGovernanceFixtures.Card(approved: false));

        var exception = await Assert.ThrowsAsync<RateCardUnusableException>(
            () => service.QuoteAsync("RC-2026", Today, [new QuotationRequest("ENG-SEN", 1m)]));

        Assert.Contains("approved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUnreleasedCard_CannotBeQuotedFrom()
    {
        var (_, service) = await BuildAsync(release: false);

        await Assert.ThrowsAsync<RateCardUnusableException>(
            () => service.QuoteAsync("RC-2026", Today, [new QuotationRequest("ENG-SEN", 1m)]));
    }

    [Fact]
    public async Task ACardThatDidNotApplyOnTheDate_CannotBeQuotedFrom()
    {
        var (_, service) = await BuildAsync();

        await Assert.ThrowsAsync<RateCardUnusableException>(
            () => service.QuoteAsync("RC-2026", Today.AddYears(-3), [new QuotationRequest("ENG-SEN", 1m)]));
    }

    [Fact]
    public async Task AServiceTheCardDoesNotPrice_IsRefused()
    {
        var (_, service) = await BuildAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.QuoteAsync("RC-2026", Today, [new QuotationRequest("NOT-PRICED", 1m)]));
    }

    [Fact]
    public void ANegativeQuantityOfWork_CannotBeRequested()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuotationRequest("ENG-SEN", -1m));
    }

    [Fact]
    public void ANegativePublishedRate_CannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RateCardEntry("ENG-X", "Fixture", PricingBasis.Day, Gbp(-1m)));
    }

    [Fact]
    public async Task AnOldQuotationReproducesTheAnswerItGave_AfterTheCardHasMovedOn()
    {
        // The point of pinning. Re-running a quotation against today's
        // card would silently answer a different question.
        var (cards, service) = await BuildAsync();

        var original = await service.QuoteAsync("RC-2026", Today, [new QuotationRequest("ENG-SEN", 10m)]);

        var successor = BusinessGovernanceFixtures.Card("RC-2027", from: Today.AddMonths(11), to: Today.AddMonths(23)) with
        {
            Entries = [new RateCardEntry("ENG-SEN", "Senior engineer", PricingBasis.Day, Gbp(900m))],
        };

        await cards.RegisterAsync("rc-2", successor, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(cards, "rc-2");
        await cards.SupersedeAsync("rc-1", "rc-2", "Rates increased for 2027.");

        var reproduced = await service.ReproduceAsync(
            original.RateCardPin, Today, [new QuotationRequest("ENG-SEN", 10m)]);

        Assert.Equal(Gbp(7_500m), reproduced.Total);
        Assert.Equal(original.Total, reproduced.Total);
    }

    [Fact]
    public async Task ReproducingFromAPinOfAnotherLibrary_IsRefused()
    {
        var (_, service) = await BuildAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReproduceAsync(new ReferencePin("Materials", "mat-1", 1), Today, []));
    }
}

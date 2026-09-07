using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

// C4's claim is that a later price change never rewrites an earlier
// commercial decision, and that list, quoted, negotiated and realised
// stay four separate facts.
public class PricingTests
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

    [Fact]
    public void ListQuotedNegotiatedAndRealised_AreFourSeparateFacts()
    {
        var pin = new ReferencePin("BusinessRateCards", "rc-1", 1);

        var list = new QuotedRate("ENG-SEN", RateKind.List, PricingBasis.Day, Gbp(750m), pin, Today);
        var negotiated = new QuotedRate("ENG-SEN", RateKind.Negotiated, PricingBasis.Day, Gbp(675m), pin, Today,
            "Fictional Client Ltd", "Volume commitment over twelve months.");

        Assert.NotEqual(list.Kind, negotiated.Kind);
        Assert.Equal(0.1m, negotiated.DiscountFrom(list.Rate));
        Assert.True(negotiated.IsTraceable);
    }

    [Fact]
    public void ADiscountAcrossCurrencies_IsRefusedRatherThanComputed()
    {
        var negotiated = new QuotedRate(
            "ENG-SEN", RateKind.Negotiated, PricingBasis.Day, new Money(600m, new CurrencyCode("EUR")));

        Assert.Null(negotiated.DiscountFrom(Gbp(750m)));
    }

    [Fact]
    public void ADiscountFromAZeroListRate_IsRefusedRatherThanReportedAsInfinite()
    {
        var negotiated = new QuotedRate("ENG-SEN", RateKind.Negotiated, PricingBasis.Day, Gbp(100m));

        Assert.Null(negotiated.DiscountFrom(Gbp(0m)));
    }

    private static async Task<IValidationResult> ValidateAsync(RateCard card, RateCardCatalog? catalog = null)
    {
        var cards = catalog ?? BusinessGovernanceFixtures.BuildRateCardCatalog();
        var service = new RateCardValidationService(cards, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(card, BusinessGovernanceFixtures.Verified());
    }

    [Fact]
    public async Task ARateInAnotherCurrency_IsAnError()
    {
        var result = await ValidateAsync(BusinessGovernanceFixtures.Card() with
        {
            Entries =
            [
                new RateCardEntry("ENG-EU", "Fixture", PricingBasis.Day, new Money(800m, new CurrencyCode("EUR"))),
            ],
        });

        Assert.Contains(PricingValidationRules.CurrencyMustMatchCard, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ARateWithNoStatedBasis_IsAnError()
    {
        var result = await ValidateAsync(BusinessGovernanceFixtures.Card() with
        {
            Entries = [new RateCardEntry("ENG-X", "Fixture", PricingBasis.Unspecified, Gbp(750m))],
        });

        Assert.Contains(PricingValidationRules.PricingBasisMustBeStated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AMinimumChargeThatCanNeverBite_IsReported()
    {
        var result = await ValidateAsync(BusinessGovernanceFixtures.Card() with
        {
            Entries = [new RateCardEntry("ENG-X", "Fixture", PricingBasis.Day, Gbp(750m), MinimumCharge: Gbp(500m))],
        });

        Assert.Contains(PricingValidationRules.MinimumChargeIsIneffective, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AZeroRate_IsReportedBecauseAMissingOneLooksIdentical()
    {
        var result = await ValidateAsync(BusinessGovernanceFixtures.Card() with
        {
            Entries = [new RateCardEntry("ENG-X", "Fixture", PricingBasis.Day, Gbp(0m))],
        });

        Assert.Contains(PricingValidationRules.RateIsZero, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task TwoCardsClaimingTheSameDayForTheSameSegment_AreReported()
    {
        var cards = BusinessGovernanceFixtures.BuildRateCardCatalog();

        await cards.RegisterAsync(
            "rc-1",
            BusinessGovernanceFixtures.Card("RC-A", from: Today.AddMonths(-6), to: Today.AddMonths(6)),
            BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(cards, "rc-1");

        var result = await ValidateAsync(
            BusinessGovernanceFixtures.Card("RC-B", from: Today, to: Today.AddMonths(12)),
            cards);

        Assert.Contains(PricingValidationRules.OverlappingCardPeriods, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task FindApplicableReturnsEveryCandidate_RatherThanPickingOne()
    {
        // Two cards for one day is a governance failure the caller must
        // see, not one to resolve by silently picking the newer.
        var cards = BusinessGovernanceFixtures.BuildRateCardCatalog();

        await cards.RegisterAsync("rc-1", BusinessGovernanceFixtures.Card("RC-A"), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(cards, "rc-1");
        await cards.RegisterAsync("rc-2", BusinessGovernanceFixtures.Card("RC-B"), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(cards, "rc-2");

        var applicable = await cards.FindApplicableAsync(Today);

        Assert.Equal(2, applicable.Count);
    }

    [Fact]
    public async Task AnUnapprovedCardIsNeverApplicable()
    {
        var cards = BusinessGovernanceFixtures.BuildRateCardCatalog();

        await cards.RegisterAsync("rc-1", BusinessGovernanceFixtures.Card(approved: false), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(cards, "rc-1");

        Assert.Empty(await cards.FindApplicableAsync(Today));
    }
}

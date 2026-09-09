using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

// C4's claim is that a later price change never rewrites an earlier
// commercial decision, and that list, quoted, negotiated and realised
// stay four separate facts.
//
// WP 18.0C (D-028): the PricingService tests moved to
// tests/Frozen/Tempest.Core.Tests/BusinessGovernance/PricingTests.cs
// alongside PricingService itself (Pricing/PricingService.cs); RateCard
// and RateCardCatalog were the only kept kinds in Pricing/.
public class PricingTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static Money Gbp(decimal amount) => BusinessGovernanceFixtures.Gbp_(amount);

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

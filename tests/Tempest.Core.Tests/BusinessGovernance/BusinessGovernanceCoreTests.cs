using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Tests.BusinessGovernance;

// The shared core is where P07's honesty is enforced for all seven work
// packages at once, so these tests are mostly about what it refuses.
public class BusinessGovernanceCoreTests
{
    private static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    private static Money Eur(decimal amount) => new(amount, new CurrencyCode("EUR"));

    [Fact]
    public void MoneyIsExact_WhereBinaryFloatingPointIsNot()
    {
        // 0.1 + 0.2 is the canonical demonstration that double cannot
        // represent decimal fractions. An invoice line must not inherit
        // that.
        var total = Gbp(0.1m) + Gbp(0.2m);

        Assert.Equal(0.3m, total.Amount);
        Assert.Equal(Gbp(0.3m), total);
    }

    [Fact]
    public void AddingAcrossCurrencies_IsRefused_NotConverted()
    {
        // Converting needs a rate and a date, neither of which TempestOS
        // holds. Returning a number would be inventing a financial fact.
        var exception = Assert.Throws<CurrencyMismatchException>(() => Gbp(100m) + Eur(100m));

        Assert.Equal(CurrencyCode.Gbp, exception.Expected);
    }

    [Fact]
    public void ComparingAcrossCurrencies_IsRefused()
    {
        Assert.Throws<CurrencyMismatchException>(() => Gbp(100m) > Eur(50m));
    }

    [Fact]
    public void MoneyWithNoCurrency_CannotBeConstructed()
    {
        // The default CurrencyCode is unspecified, and an unqualified
        // number is not money.
        Assert.Throws<ArgumentException>(() => new Money(100m, default));
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("G8P")]
    [InlineData("   ")]
    public void AMalformedCurrencyCode_IsRefused(string code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CurrencyCode(code));
    }

    [Fact]
    public void SummingAnEmptySequence_StillAnswersInAStatedCurrency()
    {
        // The currency is supplied rather than taken from the first
        // element, so an empty pipeline totals to zero pounds rather than
        // throwing or guessing.
        Assert.Equal(Gbp(0m), Money.Sum([], CurrencyCode.Gbp));
    }

    [Fact]
    public void SummingAcrossCurrencies_IsRefused()
    {
        Assert.Throws<CurrencyMismatchException>(() => Money.Sum([Gbp(1m), Eur(1m)], CurrencyCode.Gbp));
    }

    [Fact]
    public void RoundingUsesBankersRounding_TheConventionAccountingSystemsUse()
    {
        Assert.Equal(2.22m, Gbp(2.225m).RoundTo(2).Amount);
        Assert.Equal(2.24m, Gbp(2.235m).RoundTo(2).Amount);
    }

    [Fact]
    public void AnEffectivePeriodCannotEndBeforeItStarts()
    {
        Assert.Throws<ArgumentException>(() =>
            new EffectivePeriod(new DateOnly(2026, 3, 1), new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void EffectivePeriodBoundariesAreInclusiveAtBothEnds()
    {
        var period = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.True(period.Contains(new DateOnly(2026, 1, 1)));
        Assert.True(period.Contains(new DateOnly(2026, 12, 31)));
        Assert.False(period.Contains(new DateOnly(2025, 12, 31)));
        Assert.False(period.Contains(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void AnOpenEndedPeriodNeverExpires()
    {
        var period = new EffectivePeriod(new DateOnly(2026, 1, 1), null);

        Assert.True(period.IsOpenEnded);
        Assert.False(period.HasExpiredBy(new DateOnly(2099, 1, 1)));
        Assert.Null(period.DayCount);
    }

    [Fact]
    public void PeriodsSharingASingleDay_Overlap()
    {
        // The boundary case that matters: two rate cards, one ending the
        // day the next begins, both claiming to be the applicable price.
        var first = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var second = new EffectivePeriod(new DateOnly(2026, 6, 30), new DateOnly(2026, 12, 31));
        var third = new EffectivePeriod(new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));

        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
        Assert.False(first.Overlaps(third));
    }

    [Fact]
    public void AnOpenEndedPeriodOverlapsEverythingAfterItStarts()
    {
        var openEnded = new EffectivePeriod(new DateOnly(2026, 1, 1), null);

        Assert.True(openEnded.Overlaps(new EffectivePeriod(new DateOnly(2030, 1, 1), new DateOnly(2030, 12, 31))));
        Assert.False(openEnded.Overlaps(new EffectivePeriod(new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31))));
    }

    [Fact]
    public void OnlyRecorded_CountsAsEstablished()
    {
        // An assumption is not a fact, and an open question is not an
        // answer. Exactly one state means "you may rely on this".
        Assert.True(DeterminationStates.IsEstablished(DeterminationState.Recorded));

        foreach (var state in DeterminationStates.All.Where(s => s != DeterminationState.Recorded))
            Assert.False(DeterminationStates.IsEstablished(state));
    }

    [Fact]
    public void ASetOfDeterminations_IsOnlyAsStrongAsItsWeakest()
    {
        Assert.Equal(
            DeterminationState.ReviewRequired,
            DeterminationStates.Weakest([DeterminationState.Recorded, DeterminationState.ReviewRequired, DeterminationState.Recorded]));
    }

    [Fact]
    public void AnEmptySetOfDeterminations_IsNotDetermined_NotEstablished()
    {
        Assert.Equal(DeterminationState.NotDetermined, DeterminationStates.Weakest([]));
    }

    [Fact]
    public void UnclassifiedIsTreatedAsRestrictive_NotAsPublic()
    {
        // "Nobody has assessed this" must never behave as "safe to
        // publish".
        Assert.True(
            ConfidentialityClassifications.Restrictiveness(ConfidentialityClassification.Unclassified)
            > ConfidentialityClassifications.Restrictiveness(ConfidentialityClassification.Public));
    }

    [Fact]
    public void ADerivedRecord_TakesTheMostRestrictiveClassificationOfItsSources()
    {
        // A forecast built from a client-confidential contract value is
        // client-confidential.
        Assert.Equal(
            ConfidentialityClassification.ClientConfidential,
            ConfidentialityClassifications.MostRestrictive(
            [
                ConfidentialityClassification.Public,
                ConfidentialityClassification.Internal,
                ConfidentialityClassification.ClientConfidential,
            ]));
    }

    [Fact]
    public void AnAuthorisationWithoutAPerson_CannotBeConstructed()
    {
        // TempestOS does not approve, accept or authorise anything.
        Assert.Throws<ArgumentException>(() => new BusinessAuthorisation(
            BusinessAuthorityKind.CommercialCommitment, "  ", "Director", BusinessGovernanceFixtures.Today, "Basis."));
    }

    [Fact]
    public void AnAuthorisationWithoutACapacity_CannotBeConstructed()
    {
        // Whether the person was entitled to act must be answerable from
        // the record.
        Assert.Throws<ArgumentException>(() => new BusinessAuthorisation(
            BusinessAuthorityKind.CommercialCommitment, "director-1", " ", BusinessGovernanceFixtures.Today, "Basis."));
    }

    [Fact]
    public void AnAuthorisationWithoutABasis_CannotBeConstructed()
    {
        Assert.Throws<ArgumentException>(() => new BusinessAuthorisation(
            BusinessAuthorityKind.CommercialCommitment, "director-1", "Director", BusinessGovernanceFixtures.Today, ""));
    }

    [Fact]
    public void EveryKindOfBusinessAuthority_IsReservedToAPerson()
    {
        foreach (var kind in Enum.GetValues<BusinessAuthorityKind>().Where(k => k != BusinessAuthorityKind.Unspecified))
            Assert.True(BusinessAuthorisation.IsReservedToAPerson(kind));
    }

    [Fact]
    public void ARecordOwnedByADepartment_CannotBeConstructed()
    {
        // A risk owned by "Operations" is a risk nobody has to answer for.
        Assert.Throws<ArgumentException>(() => new BusinessOwnership("   ", "Team"));
    }

    [Fact]
    public void EvidenceThatCannotBeRetrieved_IsReportedAsSuch()
    {
        var locatable = new BusinessEvidence(BusinessEvidenceKind.ExecutedDocument, "A signed deed.", Reference: "DEED-1");
        var unlocatable = new BusinessEvidence(BusinessEvidenceKind.Correspondence, "Somebody said so on a call.");

        Assert.True(locatable.IsLocatable);
        Assert.False(unlocatable.IsLocatable);
    }

    [Fact]
    public void AReviewCarriedOut_RollsTheScheduleForwardByItsOwnInterval()
    {
        var schedule = new ReviewSchedule(BusinessGovernanceFixtures.Today, IntervalMonths: 12);

        var reviewed = schedule.Reviewed(BusinessGovernanceFixtures.Today, "reviewer-1");

        Assert.Equal(BusinessGovernanceFixtures.Today.AddMonths(12), reviewed.NextReviewDue);
        Assert.Equal("reviewer-1", reviewed.LastReviewedByPrincipalId);
    }

    [Fact]
    public void AReviewOnAScheduleWithNoInterval_DoesNotInventOne()
    {
        var schedule = new ReviewSchedule(BusinessGovernanceFixtures.Today);

        var reviewed = schedule.Reviewed(BusinessGovernanceFixtures.Today, "reviewer-1");

        Assert.Null(reviewed.NextReviewDue);
        Assert.True(reviewed.HasBeenReviewed);
    }

    [Fact]
    public void MoneyRoundTripsThroughTheReferenceDataSerialiser()
    {
        // P07 records are persisted as JSON, so a monetary amount that
        // does not survive a round trip silently zeroes every contract
        // value, rate and forecast in the system.
        var original = Gbp(1234.56m);

        var json = System.Text.Json.JsonSerializer.Serialize(
            original, Tempest.Core.ReferenceData.ReferenceSerialisation.Options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Money>(
            json, Tempest.Core.ReferenceData.ReferenceSerialisation.Options);

        Assert.Equal(original, restored);
        Assert.Equal(CurrencyCode.Gbp, restored.Currency);
    }

    [Fact]
    public void AScheduleNobodySet_IsDistinguishableFromOneWithNoReview()
    {
        Assert.False(ReviewSchedule.NotScheduled.IsScheduled);
        Assert.False(ReviewSchedule.NotScheduled.HasBeenReviewed);
        Assert.False(ReviewSchedule.NotScheduled.IsOverdueAt(new DateOnly(2099, 1, 1)));
    }
}

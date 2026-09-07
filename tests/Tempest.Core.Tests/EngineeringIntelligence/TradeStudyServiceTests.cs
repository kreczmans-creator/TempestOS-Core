using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.TradeStudies;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// The trade-off framework's whole value is in what it refuses to do:
// score, rank, or decide. These tests hold those refusals in place.
public class TradeStudyServiceTests
{
    private static Quantity<Mass> Kg(double value) => new(value, MassUnits.Kilogram);

    private static Quantity<Pressure> Mpa(double value) => new(value, PressureUnits.Megapascal);

    private static TradeStudyDefinition Study(params TradeStudyConsideration[] considerations) => new()
    {
        Code = "TS-1",
        Name = "Fixture bracket trade study",
        Problem = "Which bracket approach to carry into detail design. A fixture study, not real engineering.",
        Objective = "A bracket that meets the mass budget without new tooling.",
        Considerations = considerations,
        Assumptions = [new TradeStudyAssumption("A1", "The duty cycle is as stated in the fixture brief.")],
        Rationale = "Framed around mass and strength because those are what differ between the approaches.",
    };

    private static TradeStudyConsideration MassLimit(double kilograms, ConsiderationKind kind = ConsiderationKind.Constraint) =>
        new("C-MASS",
            kind,
            $"The bracket must weigh no more than {kilograms} kg.",
            new QuantityComparisonExpression(
                "Mass",
                QuantityComparator.AtMost,
                EngineeringIntelligenceFixtures.Threshold(Kg(kilograms))));

    private static TradeStudyConsideration StrengthCriterion() =>
        new("C-STR",
            ConsiderationKind.Criterion,
            "Higher yield strength is preferred, for margin against the uncertain load case.",
            new QuantityComparisonExpression(
                "YieldStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

    private static async Task<TradeStudyService> ServiceAsync(TradeStudyDefinition definition, bool release = true)
    {
        var catalog = EngineeringIntelligenceFixtures.BuildTradeStudyCatalog();

        await catalog.RegisterAsync("study-1", definition, EngineeringIntelligenceFixtures.Verified());

        if (release)
            await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, "study-1");

        return new TradeStudyService(
            catalog,
            new CurrentPrincipalAccessor(),
            timeProvider: EngineeringIntelligenceFixtures.Clock());
    }

    private static TradeStudyCandidate Candidate(string code, FakeSubject? subject = null) =>
        new(new TradeStudyOption(code, $"Option {code}"), subject);

    [Fact]
    public async Task AnOptionViolatingAConstraint_IsEliminated()
    {
        var service = await ServiceAsync(Study(MassLimit(2.0)));

        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        Assert.Single(record.EliminatedOptions);
        Assert.Empty(record.AdmissibleOptions);
    }

    [Fact]
    public async Task AnOptionFailingACriterion_IsNotEliminated()
    {
        // A criterion discriminates between admissible options; it never
        // rules one out, however badly it does. That distinction is the
        // whole reason the framework refuses a single weighted score.
        var service = await ServiceAsync(Study(MassLimit(5.0), StrengthCriterion()));

        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)).With("YieldStrength", Mpa(200)))]);

        var option = Assert.Single(record.AdmissibleOptions);

        Assert.Equal(CandidateStanding.ConstraintsSatisfied, option.Standing);
        Assert.Equal(AssessmentOutcome.Concern, option.FindJudgement("C-STR")!.Outcome);
    }

    [Fact]
    public async Task AConsiderationWithNoRecordedValue_LeavesTheOptionUnresolved_NotAdmissible()
    {
        var service = await ServiceAsync(Study(MassLimit(2.0)));

        var record = await service.RunAsync("TS-1", [Candidate("A", new FakeSubject("opt-a"))]);

        Assert.Single(record.UnresolvedOptions);
        Assert.Empty(record.AdmissibleOptions);
        Assert.Empty(record.EliminatedOptions);
    }

    [Fact]
    public async Task AConsiderationNoConditionCanSettle_AwaitsEvidence_AndSaysWhatWouldSettleIt()
    {
        var service = await ServiceAsync(Study(new TradeStudyConsideration(
            "C-SUP",
            ConsiderationKind.Criterion,
            "The supplier can deliver within the programme.",
            EvidenceExpected: "A written lead-time quotation.")));

        var record = await service.RunAsync("TS-1", [Candidate("A", new FakeSubject("opt-a"))]);

        var judgement = Assert.Single(record.Options.Single().Judgements);

        Assert.Equal(AssessmentOutcome.EvidenceRequired, judgement.Outcome);
        Assert.Equal(JudgementSource.Outstanding, judgement.Source);
        Assert.Contains("lead-time quotation", judgement.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnOptionWithNoRecordBehindIt_IsStillAssessed_AsAwaitingEvidence()
    {
        // Comparing two architectures is a real trade study. The framework
        // must not force an option into a catalogue to be considered.
        var service = await ServiceAsync(Study(MassLimit(2.0)));

        var record = await service.RunAsync("TS-1", [Candidate("A")]);

        var judgement = Assert.Single(record.Options.Single().Judgements);

        Assert.Equal(AssessmentOutcome.EvidenceRequired, judgement.Outcome);
        Assert.Contains("no reference-data record", judgement.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStudyWithOneSurvivingOption_StillRequiresAnEngineersDecision()
    {
        // Narrowing the field is not deciding. An engineer may still
        // conclude that none of the options is acceptable.
        var service = await ServiceAsync(Study(MassLimit(5.0)));

        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        Assert.Single(record.AdmissibleOptions);
        Assert.True(record.RequiresHumanDecision);
        Assert.False(record.IsDecided);
    }

    [Fact]
    public void ADecisionWithoutARationale_CannotBeConstructed()
    {
        // A trade study whose reasoning was not written down has not been
        // done; it has been concluded.
        Assert.Throws<ArgumentException>(() => new TradeStudyDecision(
            "A",
            "   ",
            "engineer-1",
            EngineeringIntelligenceFixtures.FixedNow));
    }

    [Fact]
    public void ADecisionWithoutADecisionMaker_CannotBeConstructed()
    {
        Assert.Throws<ArgumentException>(() => new TradeStudyDecision(
            "A",
            "Option A carries the least programme risk.",
            "   ",
            EngineeringIntelligenceFixtures.FixedNow));
    }

    [Fact]
    public async Task RecordingADecision_AttachesItWithoutChangingWhatWasFound()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)));
        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        var decided = service.RecordDecision(record, new TradeStudyDecision(
            "A",
            "Option A meets the mass budget and reuses existing tooling.",
            "engineer-1",
            EngineeringIntelligenceFixtures.FixedNow));

        Assert.True(decided.IsDecided);
        Assert.False(decided.RequiresHumanDecision);
        Assert.False(decided.DecisionDepartsFromAssessment);
        Assert.Equal(record.Options.Single().Judgements, decided.Options.Single().Judgements);
    }

    [Fact]
    public async Task ADecisionSelectingAnEliminatedOption_IsRecorded_AndFlagged()
    {
        // An engineer may overrule the study. What must not happen is the
        // record quietly reading as though the study agreed.
        var service = await ServiceAsync(Study(MassLimit(2.0)));
        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        var decided = service.RecordDecision(record, new TradeStudyDecision(
            "A",
            "The mass budget is being renegotiated; option A is carried forward on that basis.",
            "engineer-1",
            EngineeringIntelligenceFixtures.FixedNow));

        Assert.True(decided.DecisionDepartsFromAssessment);
    }

    [Fact]
    public async Task ARecordedOverride_ClearsTheDeparture_BecauseItWasStatedRatherThanIgnored()
    {
        var service = await ServiceAsync(Study(MassLimit(2.0)));
        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        var decided = service.RecordDecision(record, new TradeStudyDecision(
            "A",
            "The mass budget is being renegotiated; option A is carried forward on that basis.",
            "engineer-1",
            EngineeringIntelligenceFixtures.FixedNow,
            Overrides:
            [
                new ConsiderationOverride(
                    "C-MASS",
                    "The 2 kg budget was provisional and has been raised to 3.5 kg by the systems lead.",
                    "engineer-2"),
            ]));

        Assert.False(decided.DecisionDepartsFromAssessment);
        Assert.True(decided.Decision!.HasOverrides);
    }

    [Fact]
    public async Task ADecisionIsNotOverwritten()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)));
        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        var decided = service.RecordDecision(record, new TradeStudyDecision(
            "A",
            "Option A meets the mass budget.",
            "engineer-1",
            EngineeringIntelligenceFixtures.FixedNow));

        Assert.Throws<InvalidOperationException>(() => service.RecordDecision(decided, new TradeStudyDecision(
            "A",
            "Reconsidered.",
            "engineer-2",
            EngineeringIntelligenceFixtures.FixedNow)));
    }

    [Fact]
    public async Task ADecisionNamingAnOptionTheStudyNeverAssessed_IsRefused()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)));
        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        Assert.Throws<ArgumentException>(() => service.RecordDecision(record, new TradeStudyDecision(
            "Z",
            "An option nobody assessed.",
            "engineer-1",
            EngineeringIntelligenceFixtures.FixedNow)));
    }

    [Fact]
    public async Task AnEngineersJudgement_ReplacesTheAssessedOne_ButTheAssessmentStaysAsEvidence()
    {
        var service = await ServiceAsync(Study(MassLimit(2.0)));
        var record = await service.RunAsync(
            "TS-1",
            [Candidate("A", new FakeSubject("opt-a").With("Mass", Kg(3.0)))]);

        var revised = service.RecordJudgement(
            record,
            "A",
            "C-MASS",
            AssessmentOutcome.Pass,
            "The 3 kg figure includes the fixture bracket, which is not part of this assembly.",
            comparison: "About 0.4 kg heavier than option B once the fixture is excluded.");

        var judgement = revised.Options.Single().Judgements.Single();

        Assert.Equal(AssessmentOutcome.Pass, judgement.Outcome);
        Assert.Equal(JudgementSource.Judged, judgement.Source);
        Assert.Contains(judgement.Evidence, e => e.Description.Contains("Superseded", StringComparison.Ordinal));
        Assert.Contains("0.4 kg heavier", judgement.Comparison!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreleasedStudy_IsRefusedRatherThanRun()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)), release: false);

        await Assert.ThrowsAsync<UnreleasedTradeStudyException>(
            () => service.RunAsync("TS-1", [Candidate("A")]));
    }

    [Fact]
    public async Task TwoOptionsCannotShareOneCode()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RunAsync("TS-1", [Candidate("A"), Candidate("a")]));
    }

    [Fact]
    public async Task TheRecordPinsTheStudyRevisionAndEveryOptionRevisionItRead()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)));
        var subject = new FakeSubject("opt-a")
        {
            Pin = new ReferencePin("Materials", "mat-1", 3),
        }.With("Mass", Kg(3.0));

        var record = await service.RunAsync("TS-1", [Candidate("A", subject)]);

        Assert.Contains(record.AllPins, pin => pin.Library == "EngineeringTradeStudies");
        Assert.Contains(record.AllPins, pin => pin is { Library: "Materials", RecordId: "mat-1", RevisionNumber: 3 });
    }

    [Fact]
    public async Task TheRecordSaysWhenTheStudyWasRunAndByWhom()
    {
        var service = await ServiceAsync(Study(MassLimit(5.0)));

        var record = await service.RunAsync("TS-1", [Candidate("A")]);

        Assert.Equal(EngineeringIntelligenceFixtures.FixedNow, record.AssessedAt);
        Assert.False(string.IsNullOrWhiteSpace(record.AssessedByPrincipalId));
    }

    [Fact]
    public void ARiskRecordedAsAccepted_MustNameWhoAcceptedIt()
    {
        // Accepting a risk is an act of engineering authority. The
        // validation service refuses an unattributed acceptance.
        var risk = new TradeStudyRisk("R1", "The supplier has not built this geometry before.")
        {
            Standing = RiskStanding.Accepted,
        };

        Assert.True(string.IsNullOrWhiteSpace(risk.AcceptedByPrincipalId));
        Assert.False(risk.IsOutstanding);
    }

    [Fact]
    public void ARiskIsDescribed_NotScored()
    {
        // There is deliberately no severity number, no likelihood number
        // and no product of the two anywhere on this type.
        var properties = typeof(TradeStudyRisk).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Score", properties);
        Assert.DoesNotContain("Likelihood", properties);
        Assert.DoesNotContain("Severity", properties);
    }

    [Fact]
    public void NothingInTheTradeStudyServiceReturnsARecommendation()
    {
        // A structural guard, not a style check: if somebody later adds a
        // "Recommend" or "Rank" method, this test is what stops it.
        var methods = typeof(ITradeStudyService).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(methods, name =>
            name.Contains("Recommend", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Rank", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Score", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Choose", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Select", StringComparison.OrdinalIgnoreCase));
    }
}

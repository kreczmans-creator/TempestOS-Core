using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.DesignRules;
using Tempest.Core.EngineeringIntelligence.Reviews;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// What a design-rule assessment and an engineering review must never do is
// let silence read as approval. These tests are mostly about what the
// result says it did *not* check.
public class DesignRuleAndReviewTests
{
    private static Quantity<Pressure> Mpa(double value) => new(value, PressureUnits.Megapascal);

    private static RuleDefinition YieldRule(string code = "STR-1", double megapascals = 300) => new()
    {
        Code = code,
        Name = "Minimum yield strength",
        Statement = $"The material's yield strength must be at least {megapascals} MPa.",
        Severity = RuleSeverity.Requirement,
        Domain = RuleDomain.Materials,
        Rationale = "Fixture rationale, not real engineering guidance.",
        Condition = new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(megapascals))),
    };

    private static async Task<RuleCatalog> ReleasedRuleCatalogAsync(params RuleDefinition[] rules)
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        for (var index = 0; index < rules.Length; index++)
        {
            var id = $"rule-{index + 1}";

            await catalog.RegisterAsync(id, rules[index], EngineeringIntelligenceFixtures.Verified());
            await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, id);
        }

        return catalog;
    }

    private static DesignRuleService DesignRules(RuleCatalog rules) =>
        new(rules, new CurrentPrincipalAccessor(), timeProvider: EngineeringIntelligenceFixtures.Clock());

    [Fact]
    public async Task ASubjectNoReleasedRuleAppliesTo_GetsAnAssessmentThatSaysItEstablishedNothing()
    {
        // The most dangerous possible result: a clean assessment of a
        // subject nothing was ever checked against.
        var rules = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        var assessment = await DesignRules(rules).AssessAsync(new FakeSubject());

        Assert.Equal(0, assessment.Scope.ApplicableRuleCount);
        Assert.Contains("establishes nothing", assessment.Scope.Describe(), StringComparison.Ordinal);
        Assert.Equal(AssessmentOutcome.NotEvaluated, assessment.Record.Outcome);
    }

    [Fact]
    public async Task AnApplicableRuleThatIsNotReleased_IsCountedAndReported_NotIgnored()
    {
        // Guidance that exists but is not yet trustworthy has not been
        // applied, and the assessment has to say so rather than reading
        // as though the library held nothing.
        var rules = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await rules.RegisterAsync("rule-1", YieldRule(), EngineeringIntelligenceFixtures.Verified());

        var assessment = await DesignRules(rules).AssessAsync(
            new FakeSubject().With("YieldStrength", Mpa(350)));

        Assert.Equal(1, assessment.Scope.UnreleasedRuleCount);
        Assert.Equal(0, assessment.Scope.RunRuleCount);
        Assert.Contains("not released", assessment.Scope.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NarrowingTheScope_IsRecordedOnTheResult()
    {
        var rules = await ReleasedRuleCatalogAsync(
            YieldRule(),
            YieldRule("FIT-1") with { Domain = RuleDomain.Tolerances });

        var assessment = await DesignRules(rules).AssessAsync(
            new FakeSubject().With("YieldStrength", Mpa(350)),
            new DesignRuleScope { Domains = [RuleDomain.Materials] });

        Assert.Equal(2, assessment.Scope.ApplicableRuleCount);
        Assert.Equal(1, assessment.Scope.RunRuleCount);
        Assert.False(assessment.Scope.Scope.IsUnrestricted);
    }

    [Fact]
    public async Task AFailedBindingRule_IsADefectAndTheAssessmentFails()
    {
        var rules = await ReleasedRuleCatalogAsync(YieldRule());

        var assessment = await DesignRules(rules).AssessAsync(
            new FakeSubject().With("YieldStrength", Mpa(250)));

        Assert.Equal(AssessmentOutcome.Fail, assessment.Record.Outcome);
        Assert.Single(assessment.Record.Defects);
    }

    [Fact]
    public async Task AnAssessmentIsReproducibleFromTheRulePinsItRecorded()
    {
        var rules = await ReleasedRuleCatalogAsync(YieldRule());
        var service = DesignRules(rules);
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));

        var original = await service.AssessAsync(subject);
        var pins = original.Record.Evaluations.Select(e => e.RulePin).ToList();

        var reproduced = await service.ReproduceAsync(subject, pins);

        Assert.Equal(original.Record.Outcome, reproduced.Record.Outcome);
        Assert.Equal(original.Record.AllPins, reproduced.Record.AllPins);
    }

    private static async Task<EngineeringReviewService> ReviewServiceAsync(
        ReviewDefinition definition,
        RuleCatalog rules,
        bool release = true)
    {
        var reviews = EngineeringIntelligenceFixtures.BuildReviewCatalog();

        await reviews.RegisterAsync("review-1", definition, EngineeringIntelligenceFixtures.Verified());

        if (release)
            await EngineeringIntelligenceFixtures.ReleaseAsync(reviews, "review-1");

        return new EngineeringReviewService(
            reviews,
            rules,
            new CurrentPrincipalAccessor(),
            timeProvider: EngineeringIntelligenceFixtures.Clock());
    }

    private static ReviewDefinition Review(params ReviewCriterion[] criteria) => new()
    {
        Code = "REV-1",
        Name = "Fixture design review",
        Purpose = "A fixture review. Not real review guidance.",
        Criteria = criteria,
    };

    [Fact]
    public async Task ACriterionARuleCanAnswer_IsAnsweredByTheRule()
    {
        var rules = await ReleasedRuleCatalogAsync(YieldRule());
        var service = await ReviewServiceAsync(
            Review(new ReviewCriterion("C1", "Is the yield strength adequate?", ReviewArea.MaterialSuitability, RuleCode: "STR-1")),
            rules);

        var record = await service.ConductAsync("REV-1", new FakeSubject().With("YieldStrength", Mpa(350)));

        var finding = Assert.Single(record.Findings);

        Assert.Equal(AssessmentOutcome.Pass, finding.Outcome);
        Assert.False(finding.IsManual);
        Assert.NotNull(finding.Evaluation);
    }

    [Fact]
    public async Task ACriterionNoRuleCanAnswer_AwaitsEvidence_AndNeverPassesByDefault()
    {
        // "Nothing failed" is not "everything was checked". A review whose
        // manual criteria silently passed would be worse than no review.
        var rules = EngineeringIntelligenceFixtures.BuildRuleCatalog();
        var service = await ReviewServiceAsync(
            Review(new ReviewCriterion(
                "C1",
                "Has the assembly sequence been confirmed with manufacturing?",
                ReviewArea.Manufacturability,
                EvidenceExpected: "A signed manufacturing review note.")),
            rules);

        var record = await service.ConductAsync("REV-1", new FakeSubject());

        var finding = Assert.Single(record.Findings);

        Assert.Equal(AssessmentOutcome.EvidenceRequired, finding.Outcome);
        Assert.True(finding.IsManual);
        Assert.False(record.IsComplete);
        Assert.Contains("signed manufacturing review note", finding.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnEngineersFinding_ReplacesTheRulesAnswer_ButTheRulesAnswerStaysOnTheRecord()
    {
        // Overruling a rule is a legitimate engineering act. Doing it
        // without the record showing what the rule said is not.
        var rules = await ReleasedRuleCatalogAsync(YieldRule());
        var service = await ReviewServiceAsync(
            Review(new ReviewCriterion("C1", "Is the yield strength adequate?", ReviewArea.MaterialSuitability, RuleCode: "STR-1")),
            rules);

        var record = await service.ConductAsync("REV-1", new FakeSubject().With("YieldStrength", Mpa(250)));

        Assert.Equal(AssessmentOutcome.Fail, record.Findings[0].Outcome);

        var revised = service.RecordFinding(
            record,
            "C1",
            AssessmentOutcome.Pass,
            "The load case the rule assumes does not arise here; confirmed against the duty cycle.");

        var finding = Assert.Single(revised.Findings);

        Assert.Equal(AssessmentOutcome.Pass, finding.Outcome);
        Assert.True(finding.IsManual);
        Assert.Contains(finding.Evidence, e => e.Description.Contains("Superseded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnreleasedReviewDefinition_IsRefusedRatherThanConducted()
    {
        var rules = EngineeringIntelligenceFixtures.BuildRuleCatalog();
        var service = await ReviewServiceAsync(
            Review(new ReviewCriterion("C1", "Anything?", ReviewArea.MaterialSuitability)),
            rules,
            release: false);

        await Assert.ThrowsAsync<UnreleasedReviewDefinitionException>(
            () => service.ConductAsync("REV-1", new FakeSubject()));
    }

    [Fact]
    public async Task AReviewWithAnOutstandingCriterion_IsNotComplete_EvenWithNoDefects()
    {
        var rules = await ReleasedRuleCatalogAsync(YieldRule());
        var service = await ReviewServiceAsync(
            Review(
                new ReviewCriterion("C1", "Is the yield strength adequate?", ReviewArea.MaterialSuitability, RuleCode: "STR-1"),
                new ReviewCriterion("C2", "Confirmed with manufacturing?", ReviewArea.Manufacturability, EvidenceExpected: "A note.")),
            rules);

        var record = await service.ConductAsync("REV-1", new FakeSubject().With("YieldStrength", Mpa(350)));

        Assert.Empty(record.Defects);
        Assert.False(record.IsComplete);
        Assert.Single(record.AwaitingEvidence);
        Assert.True(record.RequiresHumanDecision);
    }
}

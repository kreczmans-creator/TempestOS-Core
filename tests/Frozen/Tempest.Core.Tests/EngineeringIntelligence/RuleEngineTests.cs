using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// The rule engine is the one place in `P02` where a missing property could
// quietly become a pass, so these tests are about outcomes rather than
// mechanics: what a rule says when it does not know, and what it says when
// it does. Boundary cases are covered on both sides of every threshold.
public class RuleEngineTests
{
    private static readonly ReferencePin RulePin = new("EngineeringRules", "rule-1", 1);

    private static RuleDefinition Rule(RuleExpression condition, RuleSeverity severity = RuleSeverity.Requirement) => new()
    {
        Code = "TEST-1",
        Name = "Test rule",
        Statement = "The subject satisfies the condition.",
        Severity = severity,
        Condition = condition,
    };

    private static Quantity<Pressure> Mpa(double value) => new(value, PressureUnits.Megapascal);

    private static Quantity<Length> Mm(double value) => new(value, LengthUnits.Millimetre);

    private static RuleEvaluation Evaluate(RuleDefinition rule, IAssessmentSubject subject) =>
        RuleEngine.Evaluate(rule, RulePin, subject, ConstantResolutionSet.Empty);

    [Fact]
    public void ARecordedValueAboveTheThreshold_Passes()
    {
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        Assert.Equal(AssessmentOutcome.Pass, Evaluate(rule, subject).Outcome);
    }

    [Fact]
    public void ARecordedValueBelowTheThreshold_FailsABindingRule()
    {
        var subject = new FakeSubject().With("YieldStrength", Mpa(250));
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        var evaluation = Evaluate(rule, subject);

        Assert.Equal(AssessmentOutcome.Fail, evaluation.Outcome);
        Assert.True(evaluation.IsDefect);
    }

    [Theory]
    [InlineData(QuantityComparator.AtLeast, AssessmentOutcome.Pass)]
    [InlineData(QuantityComparator.AtMost, AssessmentOutcome.Pass)]
    [InlineData(QuantityComparator.GreaterThan, AssessmentOutcome.Fail)]
    [InlineData(QuantityComparator.LessThan, AssessmentOutcome.Fail)]
    [InlineData(QuantityComparator.EqualTo, AssessmentOutcome.Pass)]
    [InlineData(QuantityComparator.NotEqualTo, AssessmentOutcome.Fail)]
    public void ExactlyOnTheThreshold_IsDecidedByTheComparatorAndNothingElse(
        QuantityComparator comparator,
        AssessmentOutcome expected)
    {
        // The boundary is where a comparator earns its keep: "at least
        // 300 MPa" and "greater than 300 MPa" differ only here, and an
        // engineer who wrote one must not get the other.
        var subject = new FakeSubject().With("YieldStrength", Mpa(300));
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            comparator,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        Assert.Equal(expected, Evaluate(rule, subject).Outcome);
    }

    [Fact]
    public void JustInsideAndJustOutsideTheThreshold_AreDecidedTheOppositeWay()
    {
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        Assert.Equal(
            AssessmentOutcome.Pass,
            Evaluate(rule, new FakeSubject().With("YieldStrength", Mpa(300.001))).Outcome);

        Assert.Equal(
            AssessmentOutcome.Fail,
            Evaluate(rule, new FakeSubject().With("YieldStrength", Mpa(299.999))).Outcome);
    }

    [Fact]
    public void AMissingProperty_IsNotRecorded_AndNeverAPass()
    {
        // The single most important behaviour in `P02`. A material with no
        // recorded yield strength has not passed a strength rule.
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        var evaluation = Evaluate(rule, new FakeSubject());

        Assert.Equal(AssessmentOutcome.NotRecorded, evaluation.Outcome);
        Assert.False(AssessmentOutcomes.IsAffirmative(evaluation.Outcome));
        Assert.False(evaluation.IsDefect);
    }

    [Fact]
    public void APropertyRecordedAsNotApplicable_IsDistinctFromOneThatIsMissing()
    {
        var rule = Rule(new QuantityComparisonExpression(
            "CoreHardness",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        Assert.Equal(
            AssessmentOutcome.NotApplicable,
            Evaluate(rule, new FakeSubject().WithNotApplicable("CoreHardness")).Outcome);

        Assert.Equal(AssessmentOutcome.NotRecorded, Evaluate(rule, new FakeSubject()).Outcome);
    }

    [Fact]
    public void ComparingAcrossDimensions_IsIndeterminate_NotFalse()
    {
        // A rule comparing a length against a pressure is a defect in the
        // rule, not a failure of the subject, and saying "Fail" would blame
        // the wrong thing.
        var subject = new FakeSubject().With("OuterDiameter", Mm(50));
        var rule = Rule(new QuantityComparisonExpression(
            "OuterDiameter",
            QuantityComparator.AtMost,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        Assert.Equal(AssessmentOutcome.Indeterminate, Evaluate(rule, subject).Outcome);
    }

    [Fact]
    public void AnUnresolvedConstantSymbol_RequiresEvidence_RatherThanGuessingAValue()
    {
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            RuleThreshold.FromConstant("SIGMA_MIN")));

        var evaluation = Evaluate(rule, subject);

        Assert.Equal(AssessmentOutcome.EvidenceRequired, evaluation.Outcome);
        Assert.Contains("SIGMA_MIN", evaluation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonBindingRuleThatIsNotSatisfied_RaisesAConcern_NotAFailure()
    {
        // "Prefer" is not "must not". Flattening the two would make every
        // recommendation a defect and every defect ignorable.
        var subject = new FakeSubject().With("YieldStrength", Mpa(250));
        var rule = Rule(
            new QuantityComparisonExpression(
                "YieldStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            RuleSeverity.Recommendation);

        var evaluation = Evaluate(rule, subject);

        Assert.Equal(AssessmentOutcome.Concern, evaluation.Outcome);
        Assert.False(evaluation.IsDefect);
    }

    [Fact]
    public void AllOf_EvaluatesEveryOperand_SoAReviewerSeesEveryReason()
    {
        // Short-circuiting would report the first failure and hide the
        // other three, which is exactly the information an engineer needs.
        var subject = new FakeSubject()
            .With("YieldStrength", Mpa(250))
            .With("OuterDiameter", Mm(80));

        var rule = Rule(new AllOfExpression(
        [
            new QuantityComparisonExpression(
                "YieldStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            new QuantityComparisonExpression(
                "OuterDiameter",
                QuantityComparator.AtMost,
                EngineeringIntelligenceFixtures.Threshold(Mm(50))),
        ]));

        var evaluation = Evaluate(rule, subject);
        var leaves = evaluation.ConditionResult!.Flatten().Where(r => r.Children.Count == 0).ToList();

        Assert.Equal(AssessmentOutcome.Fail, evaluation.Outcome);
        Assert.Equal(2, leaves.Count);
        Assert.All(leaves, leaf => Assert.Equal(AssessmentOutcome.Fail, leaf.Outcome));
    }

    [Fact]
    public void AllOf_WithOneGapAndOnePass_DoesNotPass()
    {
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));

        var rule = Rule(new AllOfExpression(
        [
            new QuantityComparisonExpression(
                "YieldStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            new QuantityComparisonExpression(
                "FatigueStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(120))),
        ]));

        Assert.Equal(AssessmentOutcome.NotRecorded, Evaluate(rule, subject).Outcome);
    }

    [Fact]
    public void AnyOf_WithOnePassAndOneGap_Passes()
    {
        // One satisfied alternative is enough, and an unrecorded second
        // alternative does not take that away.
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));

        var rule = Rule(new AnyOfExpression(
        [
            new QuantityComparisonExpression(
                "YieldStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            new QuantityComparisonExpression(
                "FatigueStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(120))),
        ]));

        Assert.Equal(AssessmentOutcome.Pass, Evaluate(rule, subject).Outcome);
    }

    [Fact]
    public void AnyOf_WithEveryAlternativeUnrecorded_DoesNotPass()
    {
        var rule = Rule(new AnyOfExpression(
        [
            new QuantityComparisonExpression(
                "YieldStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            new QuantityComparisonExpression(
                "FatigueStrength",
                QuantityComparator.AtLeast,
                EngineeringIntelligenceFixtures.Threshold(Mpa(120))),
        ]));

        Assert.True(AssessmentOutcomes.IsGap(Evaluate(rule, new FakeSubject()).Outcome));
    }

    [Fact]
    public void Not_PreservesAGap_RatherThanInvertingItIntoAPass()
    {
        // Negating "not known" gives "not known". Inverting it would turn
        // every missing property into a satisfied prohibition.
        var rule = Rule(new NotExpression(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300)))));

        Assert.Equal(AssessmentOutcome.NotRecorded, Evaluate(rule, new FakeSubject()).Outcome);
    }

    [Fact]
    public void Not_InvertsAConclusiveResult()
    {
        var subject = new FakeSubject().With("YieldStrength", Mpa(250));
        var rule = Rule(new NotExpression(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300)))));

        Assert.Equal(AssessmentOutcome.Pass, Evaluate(rule, subject).Outcome);
    }

    [Fact]
    public void ARuleForAnotherSubjectKind_IsNotApplicable_AndIsNotAPass()
    {
        var subject = new FakeSubject(subjectKind: AssessmentSubjectKinds.Material);
        var rule = Rule(new StatedExpression(true, "Always holds.")) with
        {
            Applicability = new RuleApplicability { SubjectKinds = [AssessmentSubjectKinds.Bearing] },
        };

        var evaluation = Evaluate(rule, subject);

        Assert.Equal(AssessmentOutcome.NotApplicable, evaluation.Outcome);
        Assert.False(AssessmentOutcomes.IsAffirmative(evaluation.Outcome));
    }

    [Fact]
    public void ARuleRestrictedByFamily_AgainstASubjectWithNoFamily_IsUndecided()
    {
        // "I do not know whether this rule applies" is not "it does not
        // apply", and reporting the second would silently drop the rule.
        var subject = new FakeSubject { Family = null, IsApplicabilityKnown = false };
        var rule = Rule(new StatedExpression(true, "Always holds.")) with
        {
            Applicability = new RuleApplicability { Families = ["Stainless steel"] },
        };

        Assert.True(AssessmentOutcomes.IsGap(Evaluate(rule, subject).Outcome));
    }

    [Fact]
    public void ARuleWithNoCondition_IsNotEvaluated_RatherThanPassing()
    {
        var rule = new RuleDefinition
        {
            Code = "TEST-2",
            Name = "Unconditioned rule",
            Statement = "Something an engineer has not yet made testable.",
            Severity = RuleSeverity.Requirement,
        };

        Assert.Equal(AssessmentOutcome.NotEvaluated, Evaluate(rule, new FakeSubject()).Outcome);
    }

    [Fact]
    public void EvaluationIsPure_SoTheSameInputsAlwaysGiveTheSameResult()
    {
        // No clock, no principal, no catalogue read: an evaluation is a
        // function of the rule and the subject, which is what makes a
        // reproduced assessment meaningful.
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        var first = Evaluate(rule, subject);
        var second = Evaluate(rule, subject);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.AllPins, second.AllPins);
    }

    [Fact]
    public void AnEvaluation_RecordsTheRuleRevisionItUsed()
    {
        var subject = new FakeSubject().With("YieldStrength", Mpa(350));
        var rule = Rule(new QuantityComparisonExpression(
            "YieldStrength",
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))));

        Assert.Contains(RulePin, Evaluate(rule, subject).AllPins);
    }
}

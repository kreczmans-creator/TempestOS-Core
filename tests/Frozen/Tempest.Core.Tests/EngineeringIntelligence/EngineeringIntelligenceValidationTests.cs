using Tempest.Core.EngineeringDomain;
using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.Decisions;
using Tempest.Core.EngineeringIntelligence.Reviews;
using Tempest.Core.EngineeringIntelligence.TradeStudies;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// Validation here is about whether a piece of reasoning is *askable* —
// whether a finding can be attached to it, whether it can discriminate,
// whether an accepted risk has somebody's name on it. No amount of
// validation can tell whether the engineering is right.
public class EngineeringIntelligenceValidationTests
{
    private static Quantity<Pressure> Mpa(double value) => new(value, PressureUnits.Megapascal);

    private static IEnumerable<string> Codes(IValidationResult result) =>
        result.Errors.Concat(result.Warnings).Select(d => d.Code);

    [Fact]
    public async Task ARuleWithNoCondition_IsReported()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();
        var service = new RuleValidationService(catalog);

        var result = await service.ValidateDefinitionAsync(
            new RuleDefinition
            {
                Code = "STR-1",
                Name = "Minimum yield strength",
                Statement = "The material must be strong enough.",
                Severity = RuleSeverity.Requirement,
            },
            EngineeringIntelligenceFixtures.Verified());

        Assert.Contains(RuleValidationRules.ConditionMustBeStated, Codes(result));
    }

    [Fact]
    public async Task ARuleWithNoSeverity_IsAnError_BecauseMustNotAndPreferAreNotTheSame()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();
        var service = new RuleValidationService(catalog);

        var result = await service.ValidateDefinitionAsync(
            new RuleDefinition
            {
                Code = "STR-1",
                Name = "Minimum yield strength",
                Statement = "The material's yield strength must be at least 300 MPa.",
                Condition = new QuantityComparisonExpression(
                    "YieldStrength",
                    QuantityComparator.AtLeast,
                    EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            },
            EngineeringIntelligenceFixtures.Verified());

        Assert.Contains(RuleValidationRules.SeverityMustBeStated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ARuleReadingAPropertyNoLibraryRecords_IsReported()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();
        var service = new RuleValidationService(catalog);

        var result = await service.ValidateDefinitionAsync(
            new RuleDefinition
            {
                Code = "STR-1",
                Name = "Minimum sparkliness",
                Statement = "The material's sparkliness must be at least 300 MPa.",
                Severity = RuleSeverity.Requirement,
                Condition = new QuantityComparisonExpression(
                    "Sparkliness",
                    QuantityComparator.AtLeast,
                    EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
            },
            EngineeringIntelligenceFixtures.Verified());

        Assert.Contains(RuleValidationRules.UnknownPropertyName, Codes(result));
    }

    [Fact]
    public async Task ADecisionTreeThatLoops_IsReported()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildTreeCatalog();
        var service = new DecisionTreeValidationService(catalog);

        var result = await service.ValidateDefinitionAsync(
            new DecisionTree
            {
                Code = "MFG-1",
                Name = "Looping fixture tree",
                Purpose = "A fixture tree that loops. Not real process guidance.",
                RootNodeId = "a",
                Nodes =
                [
                    new DecisionNode
                    {
                        NodeId = "a",
                        Question = "Loop A.",
                        Branches = [new DecisionBranch("On", new StatedExpression(true, "Always."), "b")],
                    },
                    new DecisionNode
                    {
                        NodeId = "b",
                        Question = "Loop B.",
                        Branches = [new DecisionBranch("Back", new StatedExpression(true, "Always."), "a")],
                    },
                ],
            },
            EngineeringIntelligenceFixtures.Verified());

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task AReviewCriterionNamingARuleThatDoesNotExist_IsReported()
    {
        var reviews = EngineeringIntelligenceFixtures.BuildReviewCatalog();
        var service = new ReviewDefinitionValidationService(reviews, EngineeringIntelligenceFixtures.BuildRuleCatalog());

        var result = await service.ValidateDefinitionAsync(
            new ReviewDefinition
            {
                Code = "REV-1",
                Name = "Fixture review",
                Purpose = "A fixture review.",
                Criteria = [new ReviewCriterion("C1", "Is it strong enough?", ReviewArea.MaterialSuitability, RuleCode: "NOPE-1")],
            },
            EngineeringIntelligenceFixtures.Verified());

        Assert.Contains(ReviewValidationRules.RuleCodeMustResolve, Codes(result));
    }

    [Fact]
    public async Task AManualReviewCriterionThatDoesNotSayWhatWouldSettleIt_IsReported()
    {
        var reviews = EngineeringIntelligenceFixtures.BuildReviewCatalog();
        var service = new ReviewDefinitionValidationService(reviews);

        var result = await service.ValidateDefinitionAsync(
            new ReviewDefinition
            {
                Code = "REV-1",
                Name = "Fixture review",
                Purpose = "A fixture review.",
                Criteria = [new ReviewCriterion("C1", "Has manufacturing been consulted?", ReviewArea.Manufacturability)],
            },
            EngineeringIntelligenceFixtures.Verified());

        Assert.Contains(ReviewValidationRules.ManualCriterionShouldStateEvidence, Codes(result));
    }

    private static async Task<IValidationResult> ValidateStudyAsync(TradeStudyDefinition definition)
    {
        var catalog = EngineeringIntelligenceFixtures.BuildTradeStudyCatalog();

        return await new TradeStudyValidationService(catalog)
            .ValidateDefinitionAsync(definition, EngineeringIntelligenceFixtures.Verified());
    }

    private static TradeStudyDefinition Study(params TradeStudyConsideration[] considerations) => new()
    {
        Code = "TS-1",
        Name = "Fixture trade study",
        Problem = "A fixture question. Not a real study.",
        Considerations = considerations,
        Assumptions = [new TradeStudyAssumption("A1", "The fixture brief is current.")],
        Rationale = "Framed for the fixture.",
    };

    [Fact]
    public async Task AConsiderationThatDoesNotSayWhatKindItIs_IsAnError()
    {
        // Without that, the framework cannot tell whether it eliminates,
        // and it will not guess.
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Unspecified, "Something matters.")));

        Assert.Contains(TradeStudyValidationRules.ConsiderationKindMustBeStated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AStudyOfNothingButConstraints_IsReportedAsAScreeningExercise()
    {
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Constraint, "It must fit the envelope.")));

        Assert.Contains(TradeStudyValidationRules.StudyIsAllConstraints, Codes(result));
    }

    [Fact]
    public async Task AStudyWithNoAssumptionsRecorded_IsReported()
    {
        // Every trade study rests on assumptions; one with none recorded
        // has not identified them rather than not having any.
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Criterion, "Lighter is better.")) with
        {
            Assumptions = [],
        });

        Assert.Contains(TradeStudyValidationRules.AssumptionsShouldBeRecorded, Codes(result));
    }

    [Fact]
    public async Task AnAcceptedRiskWithNobodyNamed_IsAnError()
    {
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Criterion, "Lighter is better.")) with
        {
            Risks =
            [
                new TradeStudyRisk("R1", "The supplier has not built this geometry before.")
                {
                    Standing = RiskStanding.Accepted,
                },
            ],
        });

        Assert.Contains(TradeStudyValidationRules.AcceptedRiskMustNameAcceptor, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AMitigatedRiskThatDoesNotSayWhatIsBeingDone_IsAnError()
    {
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Criterion, "Lighter is better.")) with
        {
            Risks =
            [
                new TradeStudyRisk("R1", "The lead time may slip") { Standing = RiskStanding.Mitigated },
            ],
        });

        Assert.Contains(TradeStudyValidationRules.MitigatedRiskMustStateMitigation, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ALoadBearingAssumptionWithNoOwner_IsReported()
    {
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Criterion, "Lighter is better.")) with
        {
            Assumptions =
            [
                new TradeStudyAssumption("A1", "The load case is quasi-static.", AssumptionConfidence.Critical),
            ],
        });

        Assert.Contains(TradeStudyValidationRules.CriticalAssumptionShouldHaveOwner, Codes(result));
    }

    [Fact]
    public async Task AWellFormedStudy_ValidatesCleanly()
    {
        var result = await ValidateStudyAsync(Study(
            new TradeStudyConsideration("C1", ConsiderationKind.Constraint, "It must fit the 120 mm envelope."),
            new TradeStudyConsideration(
                "C2",
                ConsiderationKind.Criterion,
                "Lighter is better, within the envelope.",
                EvidenceExpected: "A mass estimate from the CAD model.")));

        Assert.True(result.IsValid);
    }
}

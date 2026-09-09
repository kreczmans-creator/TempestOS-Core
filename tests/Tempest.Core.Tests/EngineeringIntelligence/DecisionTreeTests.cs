using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.Decisions;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// A decision tree that quietly picks a branch it could not actually
// evaluate would produce a confident-looking manufacturing recommendation
// with nothing behind it. These tests are mostly about the walk stopping
// when it should.
public class DecisionTreeTests
{
    private static readonly ReferencePin TreePin = new("EngineeringDecisionTrees", "tree-1", 1);

    private static Quantity<Length> Mm(double value) => new(value, LengthUnits.Millimetre);

    private static DecisionTree SizeTree() => new()
    {
        Code = "MFG-1",
        Name = "Fixture process screening",
        Purpose = "Narrows candidate process families by part size. Fixture data, not real process guidance.",
        RootNodeId = "size",
        Nodes =
        [
            new DecisionNode
            {
                NodeId = "size",
                Question = "Is the largest dimension over 500 mm?",
                Branches =
                [
                    new DecisionBranch(
                        "Large",
                        new QuantityComparisonExpression(
                            "OverallLength",
                            QuantityComparator.GreaterThan,
                            EngineeringIntelligenceFixtures.Threshold(Mm(500))),
                        "large"),
                    new DecisionBranch(
                        "Small",
                        new QuantityComparisonExpression(
                            "OverallLength",
                            QuantityComparator.AtMost,
                            EngineeringIntelligenceFixtures.Threshold(Mm(500))),
                        "small"),
                ],
            },
            new DecisionNode
            {
                NodeId = "large",
                Question = "Large parts.",
                Outcome = new DecisionOutcome("Fabrication is the candidate family.", ["Fabrication"]),
            },
            new DecisionNode
            {
                NodeId = "small",
                Question = "Small parts.",
                Outcome = new DecisionOutcome("Machining is the candidate family.", ["Machining"]),
            },
        ],
    };

    private static DecisionWalk Walk(DecisionTree tree, IAssessmentSubject subject) =>
        DecisionTreeWalker.Walk(tree, TreePin, subject, ConstantResolutionSet.Empty);

    [Fact]
    public void AWalkThatReachesATerminalNode_ReportsTheOutcomeAndThePathToIt()
    {
        var subject = new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(800));

        var walk = Walk(SizeTree(), subject);

        Assert.True(walk.Concluded);
        Assert.Equal(DecisionWalkTermination.ReachedOutcome, walk.Termination);
        Assert.Equal("Fabrication", Assert.Single(walk.Outcome!.CandidateProcessFamilies));
        Assert.Contains("Large", walk.DescribePath(), StringComparison.Ordinal);
    }

    [Fact]
    public void AWalkStopsWhereTheDataRunsOut_RatherThanTakingABranchItCouldNotEvaluate()
    {
        // The single most dangerous failure mode for a decision tree: a
        // part with no recorded length must not be routed anywhere.
        var walk = Walk(SizeTree(), new FakeSubject(subjectKind: AssessmentSubjectKinds.Component));

        Assert.False(walk.Concluded);
        Assert.Equal(DecisionWalkTermination.InformationMissing, walk.Termination);
        Assert.Null(walk.Outcome);
        Assert.True(walk.RequiresHumanDecision);
    }

    [Fact]
    public void EveryOutcomeReached_StillRequiresAnEngineersDecision()
    {
        // Reaching a terminal node narrows the field. It does not choose a
        // process: cost, lead time and tooling already owned are not in
        // the tree.
        var subject = new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(100));

        Assert.True(Walk(SizeTree(), subject).RequiresHumanDecision);
    }

    [Fact]
    public void AWalkWithNoApplicableBranch_StopsAndSaysSo()
    {
        var tree = SizeTree() with
        {
            Nodes =
            [
                new DecisionNode
                {
                    NodeId = "size",
                    Question = "Is the largest dimension over 500 mm?",
                    Branches =
                    [
                        new DecisionBranch(
                            "Large",
                            new QuantityComparisonExpression(
                                "OverallLength",
                                QuantityComparator.GreaterThan,
                                EngineeringIntelligenceFixtures.Threshold(Mm(500))),
                            "large"),
                    ],
                },
                new DecisionNode
                {
                    NodeId = "large",
                    Question = "Large parts.",
                    Outcome = new DecisionOutcome("Fabrication is the candidate family.", ["Fabrication"]),
                },
            ],
        };

        var subject = new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(100));

        Assert.Equal(DecisionWalkTermination.NoBranchApplied, Walk(tree, subject).Termination);
    }

    [Fact]
    public void ACycleTerminatesTheWalk_RatherThanHangingTheCaller()
    {
        var tree = SizeTree() with
        {
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
        };

        var walk = Walk(tree, new FakeSubject(subjectKind: AssessmentSubjectKinds.Component));

        Assert.Equal(DecisionWalkTermination.CycleDetected, walk.Termination);
        Assert.False(walk.Concluded);
    }

    [Fact]
    public void ABranchLeadingNowhere_StopsTheWalkAsABrokenTree()
    {
        var tree = SizeTree() with
        {
            Nodes =
            [
                new DecisionNode
                {
                    NodeId = "size",
                    Question = "Is the largest dimension over 500 mm?",
                    Branches = [new DecisionBranch("On", new StatedExpression(true, "Always."), "missing")],
                },
            ],
        };

        Assert.Equal(
            DecisionWalkTermination.TreeIsBroken,
            Walk(tree, new FakeSubject(subjectKind: AssessmentSubjectKinds.Component)).Termination);
    }

    [Fact]
    public void AWalkRecordsEveryBranchItRejected_NotOnlyTheOneItTook()
    {
        // Branches are tried in order and the first satisfied one is
        // taken, so what the record must show is every branch considered
        // up to that point: why the tree did not go the other way is as
        // much a part of the reasoning as where it did go.
        var small = new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(100));

        var step = Walk(SizeTree(), small).Path[0];

        Assert.Equal(2, step.EvaluatedBranches.Count);
        Assert.Equal(AssessmentOutcome.Fail, step.EvaluatedBranches[0].Outcome);
        Assert.Equal("Small", step.BranchLabel);
    }

    [Fact]
    public void AWalkStopsConsideringBranchesOnceOneIsSatisfied()
    {
        var large = new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(800));

        var step = Walk(SizeTree(), large).Path[0];

        Assert.Equal("Large", step.BranchLabel);
        Assert.Equal(AssessmentOutcome.Pass, Assert.Single(step.EvaluatedBranches).Outcome);
    }

    [Fact]
    public void AWalkIsPure_SoTheSameTreeAndSubjectAlwaysGiveTheSamePath()
    {
        var subject = new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(800));
        var tree = SizeTree();

        Assert.Equal(Walk(tree, subject).DescribePath(), Walk(tree, subject).DescribePath());
    }

    [Fact]
    public void AnUnreachableNode_IsReportedByTheTreeItself()
    {
        var tree = SizeTree() with
        {
            Nodes =
            [
                .. SizeTree().Nodes,
                new DecisionNode
                {
                    NodeId = "orphan",
                    Question = "Nothing leads here.",
                    Outcome = new DecisionOutcome("Unreachable."),
                },
            ],
        };

        Assert.Equal("orphan", Assert.Single(tree.UnreachableNodeIds));
    }

    [Fact]
    public async Task AnUnreleasedTree_IsRefusedRatherThanWalked()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildTreeCatalog();

        await catalog.RegisterAsync("tree-1", SizeTree(), EngineeringIntelligenceFixtures.Verified());

        var service = new ManufacturingDecisionService(
            Tempest.Core.Tests.Manufacturing.ProcessFixtures.BuildCatalog(),
            catalog,
            new Tempest.Core.Identity.CurrentPrincipalAccessor(),
            timeProvider: EngineeringIntelligenceFixtures.Clock());

        await Assert.ThrowsAsync<UnreleasedDecisionTreeException>(
            () => service.WalkAsync(
                "MFG-1",
                new FakeSubject(subjectKind: AssessmentSubjectKinds.Component).With("OverallLength", Mm(800))));
    }
}

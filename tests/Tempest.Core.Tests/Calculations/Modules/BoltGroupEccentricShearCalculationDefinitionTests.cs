using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.bolt-group-eccentric-shear</c> against its specification's vectors.</summary>
public class BoltGroupEccentricShearCalculationDefinitionTests
{
    private static BoltGroupEccentricShearInput ToInput(BoltGroupEccentricShearVectors.Vector v) => new(
        FastenerGrade,
        v.Bolts.Select(b => new BoltPosition(b.X, b.Y)).ToList(),
        v.LoadX, v.LoadY, v.LoadPointX, v.LoadPointY, v.AllowableShearPerBolt);

    private static BoltGroupEccentricShearResult Run(BoltGroupEccentricShearVectors.Vector v, CalculationContext? context = null) =>
        new BoltGroupEccentricShearCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    private static void AssertComputed(BoltGroupEccentricShearVectors.Vector v, BoltGroupEccentricShearResult result)
    {
        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(v.ExpectedCentroidX, result.CentroidX, v.RelativeTolerance, "CentroidX");
        AssertClose(v.ExpectedCentroidY, result.CentroidY, v.RelativeTolerance, "CentroidY");
        AssertClose(v.ExpectedMoment, result.MomentAboutCentroid, v.RelativeTolerance, "MomentAboutCentroid");
        AssertClose(v.ExpectedUnitPolarMoment, result.UnitPolarMoment, v.RelativeTolerance, "UnitPolarMoment");
        Assert.NotNull(result.BoltForces);
        Assert.Equal(v.ExpectedBoltForces!.Count, result.BoltForces.Count);
        for (var i = 0; i < v.ExpectedBoltForces.Count; i++)
            AssertClose(v.ExpectedBoltForces[i], result.BoltForces[i], v.RelativeTolerance, $"BoltForces[{i}]");
        Assert.Equal(v.ExpectedGoverningBoltIndex, result.GoverningBoltIndex);
        AssertClose(v.ExpectedUtilisation, result.Utilisation, v.RelativeTolerance, "Utilisation");
    }

    [Theory]
    [MemberData(nameof(BoltGroupEccentricShearVectors.WorkedExamples), MemberType = typeof(BoltGroupEccentricShearVectors))]
    public void WorkedExamples_MatchTheHandCalculation(BoltGroupEccentricShearVectors.Vector v) => AssertComputed(v, Run(v));

    [Theory]
    [MemberData(nameof(BoltGroupEccentricShearVectors.SingleBoltDirectShear), MemberType = typeof(BoltGroupEccentricShearVectors))]
    public void ASingleBoltWithTheLoadThroughIt_IsOrdinaryDirectShear(BoltGroupEccentricShearVectors.Vector v) => AssertComputed(v, Run(v));

    [Theory]
    [MemberData(nameof(BoltGroupEccentricShearVectors.InvalidInputs), MemberType = typeof(BoltGroupEccentricShearVectors))]
    public void MalformedGroupsAndLoads_AreRejectedAsInvalid(BoltGroupEccentricShearVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(BoltGroupEccentricShearVectors.Refusals), MemberType = typeof(BoltGroupEccentricShearVectors))]
    public void ASingleBoltOffTheLoadLine_IsRefusedInTheResult_NotThrown(BoltGroupEccentricShearVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.Null(result.BoltForces);
        Assert.Equal(0.0, result.UnitPolarMoment.BaseValue);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackEveryBoltForce()
    {
        var example = ((IEnumerable<object[]>)BoltGroupEccentricShearVectors.WorkedExamples).Select(r => (BoltGroupEccentricShearVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<BoltGroupEccentricShearInput, BoltGroupEccentricShearResult>(BoltGroupEccentricShearCalculationDefinition.Id, ToInput(example));

        Assert.NotNull(readBack.Result.BoltForces);
        Assert.Equal(example.Bolts.Count, readBack.Result.BoltForces.Count);
        AssertClose(example.ExpectedBoltForces![0], readBack.Result.BoltForces[0], example.RelativeTolerance, "BoltForces[0] after read-back");
        Assert.Equal(executed.Result.GoverningBoltIndex, readBack.Result.GoverningBoltIndex);
        AssertHasIntermediate(executed, "Bolt forces");
        AssertHasIntermediate(executed, "Unit polar moment");
    }
}

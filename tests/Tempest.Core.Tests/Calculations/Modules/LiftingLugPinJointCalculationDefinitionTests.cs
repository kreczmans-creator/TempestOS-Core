using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.lifting-lug-pin-joint</c> against its specification's vectors.</summary>
public class LiftingLugPinJointCalculationDefinitionTests
{
    private static LiftingLugPinJointInput ToInput(LiftingLugPinJointVectors.Vector v) => new(
        SteelPin, PinSteelPin, v.Load, v.LugThickness, v.LugWidth, v.HoleDiameter, v.PinDiameter, v.EdgeDistance, v.CheekPlateThickness, v.Clearance,
        v.AllowableTensileStress, v.AllowableBearingStress, v.AllowableShearStress, v.PinAllowableBendingStress, v.PinAllowableShearStress);

    private static LiftingLugPinJointResult Run(LiftingLugPinJointVectors.Vector v, CalculationContext? context = null) =>
        new LiftingLugPinJointCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(LiftingLugPinJointVectors.WorkedExamples), MemberType = typeof(LiftingLugPinJointVectors))]
    public void WorkedExamples_MatchTheHandCalculation(LiftingLugPinJointVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);
        var e = v.ExpectedResult!;
        var t = v.RelativeTolerance;

        Assert.Equal(OutcomeFor(e.MeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(e.NetSectionStress, result.NetSectionStress, t, "NetSectionStress");
        AssertClose(e.NetSectionUtilisation, result.NetSectionUtilisation, t, "NetSectionUtilisation");
        AssertClose(e.BearingStress, result.BearingStress, t, "BearingStress");
        AssertClose(e.BearingUtilisation, result.BearingUtilisation, t, "BearingUtilisation");
        AssertClose(e.TearOutStress, result.TearOutStress, t, "TearOutStress");
        AssertClose(e.TearOutUtilisation, result.TearOutUtilisation, t, "TearOutUtilisation");
        AssertClose(e.PinShearStress, result.PinShearStress, t, "PinShearStress");
        AssertClose(e.PinShearUtilisation, result.PinShearUtilisation, t, "PinShearUtilisation");
        AssertClose(e.PinBendingStress, result.PinBendingStress, t, "PinBendingStress");
        AssertClose(e.PinBendingUtilisation, result.PinBendingUtilisation, t, "PinBendingUtilisation");
        Assert.Equal(e.GoverningCheck, result.GoverningCheck!.Value.ToString());

        // Both materials are on the record.
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
        Assert.Contains(PinSteelPin.RecordId, context.ReferencedMaterialIds);
    }

    [Theory]
    [MemberData(nameof(LiftingLugPinJointVectors.InvalidInputs), MemberType = typeof(LiftingLugPinJointVectors))]
    public void MalformedGeometryOrAllowables_AreRejectedAsInvalid(LiftingLugPinJointVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(LiftingLugPinJointVectors.Refusals), MemberType = typeof(LiftingLugPinJointVectors))]
    public void ALoosePin_IsRefusedInTheResult_NotThrown(LiftingLugPinJointVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.PinToHoleRatio < LiftingLugPinJointCalculationDefinition.MinimumPinToHoleRatio);
        Assert.Null(result.BearingStress);
        Assert.Null(result.GoverningCheck);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheGoverningCheck()
    {
        var example = ((IEnumerable<object[]>)LiftingLugPinJointVectors.WorkedExamples).Select(r => (LiftingLugPinJointVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingLugPinJointCalculationDefinition.Id, ToInput(example));

        Assert.Equal(LugCheck.PinBending, readBack.Result.GoverningCheck);
        AssertClose(example.ExpectedResult!.PinBendingStress, readBack.Result.PinBendingStress, example.RelativeTolerance, "PinBendingStress after read-back");
        Assert.Equal(2, executed.ReferencedMaterialIds.Count);
        AssertHasIntermediate(executed, "Pin bending moment");
        AssertHasIntermediate(executed, "Governing check");
    }
}

using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.thick-walled-cylinder</c> against its specification's vectors.</summary>
public class ThickWalledCylinderCalculationDefinitionTests
{
    private static ThickWalledCylinderInput ToInput(ThickWalledCylinderVectors.Vector v) => new(
        SteelPin, v.InnerRadius, v.OuterRadius, v.InternalPressure, v.ExternalPressure, v.ClosedEnds, v.AllowableStress);

    private static ThickWalledCylinderResult Run(ThickWalledCylinderVectors.Vector v, CalculationContext? context = null) =>
        new ThickWalledCylinderCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(ThickWalledCylinderVectors.WorkedExamples), MemberType = typeof(ThickWalledCylinderVectors))]
    public void WorkedExamples_MatchTheHandCalculation(ThickWalledCylinderVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);
        var e = v.ExpectedResult!;
        var t = v.RelativeTolerance;

        Assert.Equal(OutcomeFor(e.MeetsCriteria), result.Outcome);
        AssertClose(e.BoreRadial, result.BoreRadialStress, t, "BoreRadialStress");
        AssertClose(e.BoreHoop, result.BoreHoopStress, t, "BoreHoopStress");
        AssertClose(e.OuterRadial, result.OuterRadialStress, t, "OuterRadialStress");
        AssertClose(e.OuterHoop, result.OuterHoopStress, t, "OuterHoopStress");
        AssertClose(e.Axial, result.AxialStress, t, "AxialStress");
        AssertClose(e.BoreVonMises, result.BoreVonMisesStress, t, "BoreVonMisesStress");
        AssertClose(e.BoreTresca, result.BoreTrescaStress, t, "BoreTrescaStress");
        AssertClose(e.OuterVonMises, result.OuterVonMisesStress, t, "OuterVonMisesStress");
        AssertClose(e.MaximumVonMises, result.MaximumVonMisesStress, t, "MaximumVonMisesStress");
        Assert.Equal(CylinderSurface.Bore, result.MaximumVonMisesSurface);
        AssertClose(e.Utilisation, result.Utilisation, t, "Utilisation");
        AssertClose(e.ThinWallHoopEstimate, result.ThinWallHoopEstimate, t, "ThinWallHoopEstimate");
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
    }

    [Theory]
    [MemberData(nameof(ThickWalledCylinderVectors.InvalidInputs), MemberType = typeof(ThickWalledCylinderVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(ThickWalledCylinderVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AThinWall_ApproachesTheThinWallEstimate()
    {
        // b/a = 1.02: Lame's bore hoop stress is within a few percent of
        // p r_mean / t, which is what the comparison figure exists to show.
        var example = ((IEnumerable<object[]>)ThickWalledCylinderVectors.WorkedExamples).Select(r => (ThickWalledCylinderVectors.Vector)r[0]).First();
        var input = ToInput(example) with { OuterRadius = new(51, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre) };

        var result = new ThickWalledCylinderCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.InRange(result.LameToThinWallRatio, 1.0, 1.03);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)ThickWalledCylinderVectors.WorkedExamples).Select(r => (ThickWalledCylinderVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<ThickWalledCylinderInput, ThickWalledCylinderResult>(ThickWalledCylinderCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedResult!.MaximumVonMises, readBack.Result.MaximumVonMisesStress, example.RelativeTolerance, "MaximumVonMisesStress after read-back");
        Assert.Equal(executed.Result.MaximumVonMisesSurface, readBack.Result.MaximumVonMisesSurface);
        AssertHasIntermediate(executed, "Bore von Mises stress");
        AssertHasIntermediate(executed, "Thin-wall hoop estimate");
    }
}

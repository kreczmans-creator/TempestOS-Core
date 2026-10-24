using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// ADR-0169 (Addendum A, A-3; revised after the Opus review): the v0.25.2 bolt-shear module multiplies by the gross
/// area, which EN 1993-1-8 Table 3.4 pairs with the unthreaded shank in the shear plane and α_v = 0.6 for every class.
/// Its picker therefore offers 0.6·f_ub, labelled for that case; threads in the plane (A_s, α_v 0.5 for 4.8–6.8 and
/// 10.9) belong to the S4 worksheet. These tests pin the values and the stated scope.
/// </summary>
public sealed class BoltShearReconciliationTests
{
    private static IReadOnlyList<StandardValue> Strengths =>
        CalculationModuleDescriptors.For(BoltShearCapacityCalculationDefinition.Id)!.Inputs.Single(i => i.Name == "UltimateShearStrength").StandardValues!;

    [Theory]
    [InlineData("Class 4.6", 400)]
    [InlineData("Class 4.8", 400)]
    [InlineData("Class 5.6", 500)]
    [InlineData("Class 5.8", 500)]
    [InlineData("Class 6.8", 600)]
    [InlineData("Class 8.8", 800)]
    [InlineData("Class 10.9", 1000)]
    public void Each_Class_Offers_The_Shank_Value_0_6_F_ub(string label, double fub) =>
        Assert.Equal(0.6 * fub, Strengths.Single(v => v.Label.StartsWith(label + " ", StringComparison.Ordinal)).Value, 9);

    [Fact]
    public void The_Module_States_It_Covers_The_Shank_In_The_Shear_Plane_Only()
    {
        var metadata = new BoltShearCapacityCalculationDefinition().Metadata;
        Assert.Contains(metadata.Assumptions, a => a.Description.Contains("unthreaded shank", StringComparison.Ordinal) && a.Justification!.Contains("ADR-0169", StringComparison.Ordinal));
        Assert.Contains("unthreaded shank", CalculationModuleDescriptors.For(BoltShearCapacityCalculationDefinition.Id)!.MethodReference, StringComparison.Ordinal);
    }

    [Fact]
    public void An_M20_10_9_Single_Shear_Capacity_Is_The_Shank_Case()
    {
        var strength = Strengths.Single(v => v.Label.StartsWith("Class 10.9 ", StringComparison.Ordinal)).Value;
        var result = new BoltShearCapacityCalculationDefinition().Calculate(
            new BoltShearCapacityInput(new Quantity<Length>(20, LengthUnits.Millimetre), new Quantity<Pressure>(strength, PressureUnits.Megapascal), 1, 1.0),
            new CalculationContext());
        Assert.Equal(0.6 * 1000 * Math.PI / 4 * 400, result.AllowableShearCapacity.ConvertTo(ForceUnits.Newton).Value, 6);
    }
}

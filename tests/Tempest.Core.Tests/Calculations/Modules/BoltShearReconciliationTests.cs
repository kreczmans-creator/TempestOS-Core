using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// ADR-0169 (Addendum A, A-3): the v0.25.2 bolt-shear picker offered 0.6·f_ub for every property class. EN 1993-1-8
/// Table 3.4 gives α_v = 0.5 for 4.8, 5.8, 6.8 and 10.9 with threads in the shear plane; these tests pin the reconciled values.
/// </summary>
public sealed class BoltShearReconciliationTests
{
    private static IReadOnlyList<StandardValue> Strengths =>
        CalculationModuleDescriptors.For(BoltShearCapacityCalculationDefinition.Id)!.Inputs.Single(i => i.Name == "UltimateShearStrength").StandardValues!;

    [Theory]
    [InlineData("Class 4.6", 400, 0.6)]
    [InlineData("Class 4.8", 400, 0.5)]
    [InlineData("Class 5.6", 500, 0.6)]
    [InlineData("Class 5.8", 500, 0.5)]
    [InlineData("Class 6.8", 600, 0.5)]
    [InlineData("Class 8.8", 800, 0.6)]
    [InlineData("Class 10.9", 1000, 0.5)]
    public void Each_Class_Offers_Alpha_V_Times_F_ub(string label, double fub, double alphaV)
    {
        var value = Strengths.Single(v => v.Label.StartsWith(label + " ", StringComparison.Ordinal));
        Assert.Equal(alphaV * fub, value.Value, 9);
    }

    [Fact]
    public void Class_10_9_No_Longer_Takes_The_Blanket_0_6()
    {
        var value = Strengths.Single(v => v.Label.StartsWith("Class 10.9 ", StringComparison.Ordinal));
        Assert.NotEqual(600, value.Value);
        Assert.Contains("αv 0.5", value.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void An_M20_10_9_Single_Shear_Capacity_Falls_By_A_Sixth()
    {
        var definition = new BoltShearCapacityCalculationDefinition();
        var strength = Strengths.Single(v => v.Label.StartsWith("Class 10.9 ", StringComparison.Ordinal)).Value;
        var result = definition.Calculate(
            new BoltShearCapacityInput(new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Length>(20, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre), new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Pressure>(strength, Tempest.Core.UnitsAndQuantities.PressureUnits.Megapascal), 1, 1.0),
            new CalculationContext());
        Assert.Equal(Math.PI / 4 * 400 * 500, result.AllowableShearCapacity.ConvertTo(Tempest.Core.UnitsAndQuantities.ForceUnits.Newton).Value, 6);
    }
}

using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E-containment 1.2.0, elec.containment-01: a bend used the capstan equation with no cable-weight term. Expected values from python3 (Rifenburg's horizontal-bend
/// form T2 = T1 cosh(mu theta) + sqrt(T1^2 + (w g R)^2) sinh(mu theta)).
/// </summary>
public sealed class ContainmentBendWeightTests
{
    private static readonly ContainmentCalculator Contain = new();

    /// <summary>One 20 mm cable at 10 kg/m in a 100 mm conduit, mu 0.3, one 90 degree bend of 3 m radius.</summary>
    private static CalcRunResult Heavy(double t1, params CalcInputValue[] more) =>
        CalcRunner.Run(Contain, more.Aggregate(new CalcInputSet(
        [
            ContainmentCalculator.Cables(("Heavy", 20, 1, 10, 5000)), CalcInputValue.Choice("cont_type", ContainmentCalculator.Conduit), CalcInputValue.Quantity("D_c", 100, "mm"), CalcInputValue.Of("f_max", 0.5),
            ContainmentCalculator.Route(ContainmentCalculator.RB(90, 3000)), CalcInputValue.Quantity("T_1", t1, "N"), CalcInputValue.Of("mu", 0.3),
        ]), (set, v) => set.With(v)));

    [Fact]
    public void A_Heavy_Cable_Round_A_Large_Horizontal_Bend_Gains_The_Weight_Term_That_The_Capstan_Equation_Leaves_Out()
    {
        // 100 N start: weighted 263.2185 N (the capstan equation gave 160.1978 N, 64 % low); sidewall pressure 87.7395 against 53.3993 N/m (python3).
        var weighted = Heavy(100);
        Assert.Equal(263.21847892, weighted.OutputSi("T_max_a"), 6);
        Assert.Equal(87.73949297, weighted.OutputSi("P_max_a"), 6);

        var capstan = Heavy(100, CalcInputValue.Choice("bend_model", ContainmentCalculator.BendCapstan));
        Assert.Equal(160.19776513, capstan.OutputSi("T_max_a"), 6);
        Assert.True(weighted.OutputSi("T_max_a") > 1.6 * capstan.OutputSi("T_max_a"));
    }

    [Fact]
    public void A_Pull_That_Starts_At_A_Bend_From_Zero_Tension_Still_Carries_The_Cable_Weight()
    {
        Assert.Equal(143.82666548, Heavy(0).OutputSi("T_max_a"), 6);
        Assert.Equal(0, Heavy(0, CalcInputValue.Choice("bend_model", ContainmentCalculator.BendCapstan)).OutputSi("T_max_a"), 12);
    }

    [Fact]
    public void A_Light_Bundle_On_A_Small_Radius_Differs_From_The_Capstan_Equation_By_About_A_Tenth_Of_A_Percent()
    {
        // 2 x 12 mm at 0.6 kg/m and 1 x 8 mm at 0.15 kg/m (1.35 kg/m), mu 0.4, T1 100 N, 90 degree bend R 0.5 m: 187.5923 N against 187.4456 N (python3).
        CalcRunResult Bundle(params CalcInputValue[] more) => CalcRunner.Run(Contain, more.Aggregate(new CalcInputSet(
        [
            ContainmentCalculator.Cables(("Power", 12, 2, 0.6, 700), ("Control", 8, 1, 0.15, 150)), CalcInputValue.Choice("cont_type", ContainmentCalculator.Trunking), CalcInputValue.Quantity("W_c", 100, "mm"),
            CalcInputValue.Quantity("H_c", 50, "mm"), CalcInputValue.Of("f_max", 0.5), ContainmentCalculator.Route(ContainmentCalculator.RB(90, 500)), CalcInputValue.Quantity("T_1", 100, "N"), CalcInputValue.Of("mu", 0.4),
        ]), (set, v) => set.With(v)));

        Assert.Equal(187.59234330, Bundle().OutputSi("T_max_a"), 6);
        Assert.Equal(187.44560876, Bundle(CalcInputValue.Choice("bend_model", ContainmentCalculator.BendCapstan)).OutputSi("T_max_a"), 6);
    }

    [Fact]
    public void The_Weight_Correction_Factor_Scales_Both_The_Weight_And_The_Friction_At_The_Bend()
    {
        // w_c 1.5: mu_e = 0.45, weight 147.09975 N/m, w g w_c R = 441.2993 N: T2 = 100 cosh(0.706858) + sqrt(100^2 + 441.2993^2) sinh(0.706858) (python3).
        var r = Heavy(100, CalcInputValue.Of("w_c", 1.5));
        var me = 0.45 * Math.PI / 2;
        var wr = 10 * 9.80665 * 1.5 * 3;
        Assert.Equal(100 * Math.Cosh(me) + Math.Sqrt(100 * 100 + wr * wr) * Math.Sinh(me), r.OutputSi("T_max_a"), 6);
    }
}

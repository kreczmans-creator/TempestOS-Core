using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>
/// F2 1.5.1, fp.accumulator-03: the cold-gas scale was T_max/T_min for every exponent. With the pre-charge defined at T_max and the gas cooled at constant volume, the pre-charge at T_min is p0' = p0·T_min/T_max;
/// a gas charged as fast as it is discharged (one exponent n) then needs V0 = ΔV/[(p0'/p1)^(1/n) − (p0'/p2)^(1/n)] = V0_ideal·(T_max/T_min)^(1/n), while a held accumulator (isothermal charge, then n) needs
/// V0 = V0_ideal·(T_max/T_min), the first power. Expected values from python3: ΔV 10 L, p0 90, p1 100, p2 200 bar gauge (+ 1.01325), n 1.4, 60 °C to −20 °C: V0_ideal 27.74411 L, T_max/T_min 1.316018,
/// the product gave 36.51175 L (+8.2 %), the exact scale gives 33.75653 L, equal to the direct formula with p0' (33.75653 L).
/// </summary>
public sealed class AccumulatorColdScaleTests
{
    private static readonly AccumulatorCalculator Accumulator = new();

    private static CalcInputSet Case(params CalcInputValue[] overrides) => overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Accumulator.Template)
        .With(CalcInputValue.Quantity("dV", 10, "L")).With(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge)).With(CalcInputValue.Quantity("p_0", 90, "bar")).With(CalcInputValue.Quantity("p_1", 100, "bar"))
        .With(CalcInputValue.Quantity("p_2", 200, "bar")).With(CalcInputValue.Of("n_gas", 1.4)).With(CalcInputValue.Quantity("T_max", 60, "degC")).With(CalcInputValue.Quantity("T_min", -20, "degC")).With(CalcInputValue.Of("f_rg", 1)), (set, v) => set.With(v));

    [Fact]
    public void One_Exponent_Both_Ways_Scales_The_Cold_Gas_By_The_Temperature_Ratio_To_The_Power_One_Over_n()
    {
        var r = CalcRunner.Run(Accumulator, Case(CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSame)));

        Assert.Equal(Math.Pow(333.15 / 253.15, 1 / 1.4), r.OutputSi("f_T"), 9);
        Assert.Equal(33.75653, r.OutputSi("V0") * 1e3, 4);

        // The same volume from the definition: the pre-charge cooled to p0' = p0·T_min/T_max with the one-exponent form.
        var p0Cold = (90 + 1.01325) * 253.15 / 333.15;
        var direct = 10 / (Math.Pow(p0Cold / (100 + 1.01325), 1 / 1.4) - Math.Pow(p0Cold / (200 + 1.01325), 1 / 1.4));
        Assert.Equal(direct, r.OutputSi("V0") * 1e3, 6);
    }

    [Fact]
    public void A_Held_Accumulator_Keeps_The_First_Power_Which_Is_Exact_For_The_Isothermal_Charge()
    {
        var r = CalcRunner.Run(Accumulator, Case(CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSlowCharge)));

        // Isothermal charge to p2, then discharge at n: V0_ideal = ΔV (p2/p0)/[(p2/p1)^(1/n) − 1] on absolute pressures, scaled by the first power of the temperature ratio.
        var ideal = 10 * (201.01325 / 91.01325) / (Math.Pow(201.01325 / 101.01325, 1 / 1.4) - 1);
        Assert.Equal(333.15 / 253.15, r.OutputSi("f_T"), 9);
        Assert.Equal(ideal * 333.15 / 253.15, r.OutputSi("V0") * 1e3, 6);
    }

    [Fact]
    public void The_Pre_Charge_At_The_Cold_End_Still_Follows_The_Plain_Temperature_Ratio()
    {
        var same = CalcRunner.Run(Accumulator, Case(CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSame)));

        Assert.Equal((90 + 1.01325) * 253.15 / 333.15 * 1e5, same.OutputSi("p0_min"), 3);
    }

    [Fact]
    public void An_Isothermal_Gas_Is_Unchanged()
    {
        var r = CalcRunner.Run(Accumulator, Case(CalcInputValue.Of("n_gas", 1), CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSame)));

        Assert.Equal(333.15 / 253.15, r.OutputSi("f_T"), 9);
    }
}

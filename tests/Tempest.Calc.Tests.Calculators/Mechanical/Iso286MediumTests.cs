using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M2 1.4.0: mech.iso286-fit-03 (hole K at IT8 lost the k deviation, because the shaft k formula returns 0 for grade 8) and -04 (the t shaft silently returned the s value for a size of 24 mm or less,
/// and for 24 to 30 mm too, because the step mean 23.24 mm is below 24). Expected values from python3: 18 to 30 mm step, D = √(18·30) = 23.2379 mm, i = 0.45·D^(1/3) + 0.001·D = 1.30738 µm,
/// IT7 = 16 i = 20.918 µm, IT8 = 25 i = 32.684 µm, k = 0.6·D^(1/3) = 1.71218 µm: K8 ES = −k + (IT8 − IT7) = −1.71218 + 11.76638 = 10.054 µm (the table value for 25 mm is +10), EI = −22.630 µm.
/// t from 24 mm: ei = IT7 + 0.63·√(24·30) = 20.918 + 16.904 = 37.823 µm at 28 mm (the s value 33.684 was returned before).
/// </summary>
public sealed class Iso286MediumTests
{
    private static readonly Iso286FitCalculator Fit = new();

    private static CalcRunResult Fit286(double dMm, string holeLetter, int holeGrade, string shaftLetter, int shaftGrade) =>
        CalcRunner.Run(Fit, CalcTemplateHasher.DefaultInputs(Fit.Template).With(CalcInputValue.Quantity("D", dMm, "mm")).With(CalcInputValue.Choice("hole_letter", holeLetter)).With(CalcInputValue.Of("hole_grade", holeGrade))
            .With(CalcInputValue.Choice("shaft_letter", shaftLetter)).With(CalcInputValue.Of("shaft_grade", shaftGrade)));

    [Fact]
    public void A_K8_Hole_Keeps_The_k_Deviation_Of_The_Shaft_One_Grade_Finer()
    {
        Assert.Equal(10.054194, Iso286.HoleFundamentalDeviation("K", 25, 8)!.Value, 5);

        var r = Fit286(25, "K", 8, "h", 7);
        Assert.Equal(10.054194, r.OutputSi("ES_hole") * 1e6, 4);
        Assert.Equal(-22.630187, r.OutputSi("EI_hole") * 1e6, 4);
    }

    [Fact]
    public void A_K3_Hole_Keeps_The_k_Deviation_Too_Not_Only_K8()
    {
        // mech.iso286-fit-03 (verification): the K row's "-ei + delta" form holds for every grade up to IT8, but only grade 8 was mapped to the k deviation, so K3 (k is 0 below IT4 in the shaft
        // formula) came out 0.6 D^(1/3) = 1.712 um too high. python3, 18 to 30 mm step: D = sqrt(18 x 30) = 23.2379, i = 1.307375, k = 0.6 D^(1/3) = 1.712183 um,
        // IT1 = 0.8 + 0.02 D = 1.264758, IT5 = 7 i = 9.151627, IT2 = IT1 (IT5/IT1)^(1/4), IT3 = IT1 (IT5/IT1)^(1/2) = 3.402145, delta(3) = IT3 - IT2 = 1.327804:
        // K3 ES = -1.712183 + 1.327804 = -0.384379 (it was +1.327804), EI = ES - IT3 = -3.786524; K5 ES = -k + (IT5 - IT4) = -1.712183 + 3.571737 = 1.859554, EI = -7.292072 (unchanged).
        Assert.Equal(-0.384379, Iso286.HoleFundamentalDeviation("K", 25, 3)!.Value, 5);
        Assert.Equal(1.859554, Iso286.HoleFundamentalDeviation("K", 25, 5)!.Value, 5);

        var k3 = Fit286(25, "K", 3, "h", 3);
        Assert.Equal(-0.384379, k3.OutputSi("ES_hole") * 1e6, 4);
        Assert.Equal(-3.786524, k3.OutputSi("EI_hole") * 1e6, 4);

        var k5 = Fit286(25, "K", 5, "h", 5);
        Assert.Equal(1.859554, k5.OutputSi("ES_hole") * 1e6, 4);
        Assert.Equal(-7.292072, k5.OutputSi("EI_hole") * 1e6, 4);
    }

    [Fact]
    public void Delta_Is_Zero_Below_IT3()
    {
        // The standard gives delta for IT3 to IT8 only. K2 and K1 keep the k deviation and no delta: ES = -1.712183 um at 25 mm (python3, k = 0.6 D^(1/3)); N2's delta is 0 too: N2 ES = -5 D^0.34.
        Assert.Equal(-1.712183, Iso286.HoleFundamentalDeviation("K", 25, 2)!.Value, 5);
        Assert.Equal(-1.712183, Iso286.HoleFundamentalDeviation("K", 25, 1)!.Value, 5);
        Assert.Equal(-5 * Math.Pow(Math.Sqrt(18.0 * 30.0), 0.34), Iso286.HoleFundamentalDeviation("N", 25, 2)!.Value, 6);
    }

    [Fact]
    public void K7_K6_And_M8_Are_Unchanged_And_K9_Stays_At_Zero()
    {
        // 25 mm (python3): K7 ES = -k + (IT7 - IT6) = -1.71218 + 7.84478 = 6.13207 (the table has +6); K6 ES = -1.71218 + (IT6 - IT5) = 2.20994; M8 ES = -(IT7 - IT6) + (IT8 - IT7) = 3.92213.
        Assert.Equal(6.132068, Iso286.HoleFundamentalDeviation("K", 25, 7)!.Value, 4);
        Assert.Equal(2.209943, Iso286.HoleFundamentalDeviation("K", 25, 6)!.Value, 4);
        Assert.Equal(3.922126, Iso286.HoleFundamentalDeviation("M", 25, 8)!.Value, 4);
        Assert.Equal(0.0, Iso286.HoleFundamentalDeviation("K", 25, 9)!.Value, 9);
    }

    [Fact]
    public void The_t_Shaft_Below_24_mm_Is_Refused_Not_Replaced_By_The_s_Value()
    {
        var r = Fit286(10, "H", 7, "t", 6);

        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks);
        Assert.StartsWith("shaft_letter:", block.Message, StringComparison.Ordinal);
        Assert.Contains("24 mm", block.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_t_Shaft_From_24_mm_Is_Evaluated_At_The_24_To_30_Sub_Step()
    {
        var r = Fit286(28, "H", 7, "t", 6);

        Assert.False(r.IsBlocked);
        Assert.Equal(37.822678, r.OutputSi("ei_shaft") * 1e6, 4);
        Assert.Equal(37.822678 + 13.073752, r.OutputSi("es_shaft") * 1e6, 4);
    }

    [Fact]
    public void The_t_Shaft_Above_30_mm_Is_Unchanged()
    {
        // 45 mm: step 30 to 50, D = 38.730 mm, IT7 = 24.97989 um, t = IT7 + 0.63 D = 49.37968 um (python3).
        var r = Fit286(45, "H", 7, "t", 6);

        Assert.Equal(49.379683, r.OutputSi("ei_shaft") * 1e6, 4);
    }
}

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
        Assert.Equal(10.054194, r.OutputSi("ES") * 1e6, 4);
        Assert.Equal(-22.630187, r.OutputSi("EI") * 1e6, 4);
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
        Assert.Equal(37.822678, r.OutputSi("ei") * 1e6, 4);
        Assert.Equal(37.822678 + 13.073752, r.OutputSi("es") * 1e6, 4);
    }

    [Fact]
    public void The_t_Shaft_Above_30_mm_Is_Unchanged()
    {
        // 45 mm: step 30 to 50, D = 38.730 mm, IT7 = 24.97989 um, t = IT7 + 0.63 D = 49.37968 um (python3).
        var r = Fit286(45, "H", 7, "t", 6);

        Assert.Equal(49.379683, r.OutputSi("ei") * 1e6, 4);
    }
}

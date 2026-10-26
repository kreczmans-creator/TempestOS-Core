using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M8 1.3.1, mech.snap-fit-01: beam theory is linear, so the strain and the force of a cantilever are under-predicted when the deflection is a large share of the length, and the sheet raised no notice.
/// Expected values from python3 (a fixed-direction tip load on a constant-section cantilever, the elastica integrated by RK4 and a bisection on the tip angle, compared at the same deflection):
/// y/l 0.2: strain +1.6 %, force +4.0 %; y/l 0.3: +4.2 %, +10 %; y/l 0.41: +8.5 %, +21.7 %. The warning limit is y/l 0.3, where the error reaches about 4 % in the strain.
/// </summary>
public sealed class SnapFitLargeDeflectionTests
{
    private static readonly SnapFitCalculator Snap = new();

    private static CalcRunResult Run(double l, double y) =>
        CalcRunner.Run(Snap, new CalcInputSet([.. Snap.Template.TestCases[0].Inputs]).With(CalcInputValue.Quantity("l", l, "mm")).With(CalcInputValue.Quantity("y", y, "mm")));

    [Fact]
    public void A_Deflection_Of_41_Percent_Of_The_Length_Is_Warned_With_The_Size_Of_The_Error()
    {
        // l 20 mm, h 2 mm (the ka-constant section), y 8.2 mm: y/l = 0.41.
        var r = Run(20, 8.2);

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "large-deflection");
        Assert.StartsWith("y:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("0.41", warning.Message, StringComparison.Ordinal);
        Assert.Contains("under", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(20, 6.0, false)]
    [InlineData(20, 6.4, true)]
    [InlineData(15, 1.0, false)]
    public void The_Warning_Starts_Above_A_Deflection_Of_0_3_Of_The_Length(double l, double y, bool warned)
    {
        Assert.Equal(warned, Run(l, y).Warnings.Any(w => w.RuleId == "large-deflection"));
    }
}

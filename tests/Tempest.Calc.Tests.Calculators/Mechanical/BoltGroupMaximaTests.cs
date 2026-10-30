using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M3 1.4.0, mech.bolt-group-04: v_max and t_max were the values at the governing (circular-interaction) bolt but were labelled and checked as the maxima, so "Shear alone" and "Tension alone" read the
/// wrong bolt. Expected values from python3 (elastic method, 3 x 2 grid x 0, 80, 160 and y 0, 100, A 84.3 mm2, F_x 20, F_y -80 kN at (250, 30), T_z 2 kN.m, N 30 kN, M_x 15, M_y -8 kN.m, V_R 60, T_R 90 kN):
/// centroid (80, 50), T = -11.2 kN.m, J = 3 422 580 mm4; bolt shear 13.628, 19.226, 16.947, 21.705, 36.915 and 39.327 kN (bolts 1 to 6), bolt tension -20, 80, -45, 55, -70 and 30 kN.
/// The circular interaction governs at bolt 2 (v 19.226, t 80); the group maxima are v 39.327 kN at bolt 6 and t 80 kN at bolt 2.
/// </summary>
public sealed class BoltGroupMaximaTests
{
    private static readonly BoltGroupCalculator Group = new();

    private static CalcInputValue Bolts() =>
        CalcInputValue.Table("bolts", new[] { (0.0, 0.0), (0.0, 100.0), (80.0, 0.0), (80.0, 100.0), (160.0, 0.0), (160.0, 100.0) }.Select(p => CalcRow.Of(("x", new CalcCell(p.Item1, "mm")), ("y", new CalcCell(p.Item2, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    private static CalcRunResult Run() => CalcRunner.Run(Group, CalcTemplateHasher.DefaultInputs(Group.Template).With(Bolts())
        .With(CalcInputValue.Quantity("F_x", 20, "kN")).With(CalcInputValue.Quantity("F_y", -80, "kN")).With(CalcInputValue.Quantity("e_x", 250, "mm")).With(CalcInputValue.Quantity("e_y", 30, "mm"))
        .With(CalcInputValue.Quantity("M_z", 2, "kN*m")).With(CalcInputValue.Quantity("N", 30, "kN")).With(CalcInputValue.Quantity("M_x", 15, "kN*m")).With(CalcInputValue.Quantity("M_y", -8, "kN*m"))
        .With(CalcInputValue.Quantity("F_v_Rd", 60, "kN")).With(CalcInputValue.Quantity("F_t_Rd", 90, "kN")));

    [Fact]
    public void The_Shear_And_Tension_Maxima_Are_The_Group_Maxima_Not_The_Governing_Bolts_Values()
    {
        var r = Run();

        Assert.False(r.IsBlocked);
        Assert.Equal(39.327, r.OutputSi("F_v_Ed_max") / 1e3, 3);
        Assert.Equal(80, r.OutputSi("F_t_Ed_max") / 1e3, 6);
        Assert.Equal(39.327 / 60, r.Checks.Single(c => c.Name.StartsWith("Largest bolt shear", StringComparison.Ordinal)).Utilisation, 3);
        Assert.Equal(80.0 / 90, r.Checks.Single(c => c.Name.StartsWith("Largest bolt tension", StringComparison.Ordinal)).Utilisation, 6);
    }

    [Fact]
    public void Each_Maximum_Names_Its_Bolt_And_The_Governing_Bolt_Is_Unchanged()
    {
        var r = Run();

        Assert.Equal("bolt 2", ((TextValue)r.FindOutput("governing")!.Value).Value);
        Assert.Equal("bolt 6", ((TextValue)r.FindOutput("bolt_v")!.Value).Value);
        Assert.Equal("bolt 2", ((TextValue)r.FindOutput("bolt_t")!.Value).Value);
    }

    [Fact]
    public void The_Labels_Say_Maximum_Over_The_Group()
    {
        var r = Run();

        Assert.Contains("any bolt", r.FindOutput("F_v_Ed_max")!.Description, StringComparison.Ordinal);
        Assert.Contains("any bolt", r.FindOutput("F_t_Ed_max")!.Description, StringComparison.Ordinal);
    }
}

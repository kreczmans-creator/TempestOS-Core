using System.Globalization;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 verification finding F3: a failing check's demand and capacity rows in Results must not print on the passing side of the limit. The check line already rounded its demand up and its
/// capacity down; the Results rows of the same two quantities were rounded to the nearest, so a demand of 350.00042 MPa against 350 MPa printed 350.0 against 350 while the check failed.
/// python3: 1.0000012 × 350 = 350.00042; ceil(350.00042 × 10)/10 = 350.1. Spring: with L₀ = 99.92359756428925 mm (ν 1, d 2, D 20, E 206, G 80 GPa) y_cr = 11.99999 mm
/// (bisection of y_cr = L₀·C₁′·[1 − √(1 − C₂′/λ²)], C₁′ = 0.8174603, C₂′ = 6.7954653), 12/11.99999 = 1.00000083; floor(11.99999 × 100)/100 = 11.99.
/// </summary>
public sealed class FailingCheckRowsTests
{
    private static double Number(string text) => double.Parse(text.Split(' ')[0].TrimEnd('°'), CultureInfo.InvariantCulture);

    [Fact]
    public void The_Press_Fit_Hub_Stress_Is_Not_Printed_At_Its_Yield_Strength_While_The_Check_Fails()
    {
        var calculator = new PressFitCalculator();
        var inputs = new CalcInputSet([.. calculator.Template.TestCases.Single(t => t.Id == "ka-steel-steel-solid").Inputs]);
        var first = CalcRunner.Run(calculator, inputs.With(CalcInputValue.Quantity("S_y_h", 350, "MPa")));
        var sigma = first.OutputSi("sigma_vm_hub");

        // The hub stress is proportional to the interference (no roughness smoothing): scale it to u = 1.0000012 against S_y = 350 MPa.
        var delta = 40 * 350e6 * 1.0000012 / sigma;
        var run = CalcRunner.Run(calculator, inputs.With(CalcInputValue.Quantity("S_y_h", 350, "MPa")).With(CalcInputValue.Quantity("delta", delta, "µm")));

        var check = Assert.Single(run.Checks, c => c.Name == "Hub von Mises stress against yield strength");
        Assert.False(check.Passed);
        Assert.Equal(1.0000012, check.Utilisation, 7);
        Assert.Equal("350.1 MPa", check.DemandText);
        Assert.Equal("350 MPa", check.CapacityText);

        // The Results row says what the check line says; it printed "350.0 MPa" beside the 350 MPa capacity.
        var row = run.FindOutput("sigma_vm_hub")!.ValueText;
        Assert.Equal("350.1 MPa", row);
        Assert.True(Number(row) > 350);
    }

    [Fact]
    public void The_Spring_Buckling_Deflection_Is_Not_Printed_At_The_Working_Deflection_While_The_Check_Fails()
    {
        var calculator = new SpringCalculator();
        var run = CalcRunner.Run(calculator, new CalcInputSet([.. calculator.Template.TestCases.Single(t => t.Id == "ka-wahl-slender").Inputs]).With(CalcInputValue.Quantity("L0", 99.92359756428925, "mm")));

        Assert.Equal(11.99999, run.OutputSi("y_cr") * 1e3, 6);
        var check = Assert.Single(run.Checks, c => c.Name == "Working deflection against the buckling deflection");
        Assert.False(check.Passed);
        Assert.Equal("12 mm", check.DemandText);
        Assert.Equal("11.99 mm", check.CapacityText);

        // Results: y_cr 11.99999 mm against s_2 = 12 mm printed 12.00 mm, equal to the working deflection while the spring buckles.
        var row = run.FindOutput("y_cr")!.ValueText;
        Assert.Equal("11.99 mm", row);
        Assert.True(Number(row) < 12);
        Assert.False(run.Passed);
    }

    [Fact]
    public void A_Passing_Check_Keeps_The_Nearest_Text_In_Results()
    {
        var calculator = new SpringCalculator();
        var run = CalcRunner.Run(calculator, new CalcInputSet([.. calculator.Template.TestCases.Single(t => t.Id == "ka-wahl-slender").Inputs]));

        Assert.True(run.Checks.Single(c => c.Name == "Working deflection against the buckling deflection").Passed);
        Assert.Equal("15.80 mm", run.FindOutput("y_cr")!.ValueText);
    }
}

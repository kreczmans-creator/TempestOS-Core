using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class BeamFormulasTests
{
    private static readonly BeamFormulasCalculator Beam = new();

    private static CalcRunResult Run(string kase, double l, double w) =>
        CalcRunner.Run(Beam, CalcTemplateHasher.DefaultInputs(Beam.Template)
            .With(CalcInputValue.Choice("case", kase))
            .With(CalcInputValue.Quantity("L", l, "m"))
            .With(CalcInputValue.Quantity("w", w, "kN/m")));

    [Fact]
    public void Propped_Cantilever_Reports_The_Deflection_Position_From_The_Fixed_End()
    {
        // struct.beam-formulas-01: fixed at x = 0, so y is proportional to ξ²(1 − ξ)(3 − 2ξ) and peaks at ξ = (15 − √33)/16 = 0.5785 (L 8 m: 4.628 m), not 0.4215 (3.372 m).
        var r = Run(BeamFormulasCalculator.ProppedUdl, 8, 12);
        Assert.False(r.IsBlocked);
        Assert.Equal(4.6277, r.OutputSi("x_delta"), 3);
    }
}

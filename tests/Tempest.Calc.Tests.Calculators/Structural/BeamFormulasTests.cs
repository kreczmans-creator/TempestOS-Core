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

public sealed class BeamFormulasDeepBeamTests
{
    private static readonly BeamFormulasCalculator Beam = new();

    private static CalcRunResult RunCase(string id)
    {
        var testCase = Beam.Template.TestCases.Single(t => t.Id == id);
        var set = CalcTemplateHasher.NewSheetInputs(Beam.Template);

        foreach (var v in testCase.Inputs)
            set.Set(v);

        return CalcRunner.Run(Beam, set);
    }

    [Fact]
    public void The_Deep_Beam_Geometry_Check_Has_No_Ratio_So_The_Deflection_Check_Governs_The_Legacy_Udl_Case()
    {
        // C-2 integration: the geometry check (a span against twenty times the extreme-fibre distance) is a yes/no check; as a ratio it governed the
        // simply supported UDL case at u = 0.5 over the deflection check at 0.255, and the sheet's governing line then named no output row.
        var r = RunCase("ka-legacy-ss-udl");
        Assert.False(r.IsBlocked);
        var geometry = Assert.Single(r.Checks, c => c.Name == "Shortest span of a beam that is not deep against the span");
        Assert.True(geometry.Passed);
        Assert.False(geometry.HasUtilisation);
        Assert.Equal(0.0, geometry.Utilisation);
        Assert.Equal("Deflection against the deflection limit", r.Governing!.Name);
        Assert.Equal("u_deflection", r.Governing.StepSymbol);
        Assert.NotNull(r.FindOutput("u_deflection"));
        Assert.Equal(r.Governing.Utilisation, r.OutputSi("u_deflection"), 9);
    }

    [Fact]
    public void A_Deep_Beam_Still_Fails_The_Geometry_Check_And_Warns()
    {
        var r = CalcRunner.Run(Beam, Consistency.ProbeInputs.DeepBeam());
        Assert.False(r.IsBlocked);
        var geometry = Assert.Single(r.Checks, c => c.Name == "Shortest span of a beam that is not deep against the span");
        Assert.False(geometry.Passed);
        Assert.False(geometry.HasUtilisation);
        Assert.Contains(r.Notices, n => n.RuleId == "deep-beam" && n.Severity == CalcNoticeSeverity.Warning);
        Assert.False(r.Passed);
    }
}

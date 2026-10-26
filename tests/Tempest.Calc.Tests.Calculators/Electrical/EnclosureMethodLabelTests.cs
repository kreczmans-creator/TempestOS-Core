using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E2 1.5.1, elec.enclosure-heat-01: the A_eff step was labelled "IEC TR 60890 approach" but uses plain face areas, neither the surface factors nor the standard's temperature-rise
/// correlation, so the result could be read as an IEC TR 60890 verification. The step now carries the plain exposed-area label and the run states that it is not that verification.
/// </summary>
public sealed class EnclosureMethodLabelTests
{
    private static readonly EnclosureHeatCalculator Enclosure = new();

    [Fact]
    public void The_Effective_Area_Step_Is_Not_Labelled_As_An_IEC_TR_60890_Method()
    {
        var r = CalcRunner.Run(Enclosure, CalcTemplateHasher.DefaultInputs(Enclosure.Template));

        var step = r.Steps.Single(s => s.Symbol == "A_eff");
        Assert.DoesNotContain("60890 approach", step.Clause ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("not an IEC TR 60890 verification", step.Clause!, StringComparison.Ordinal);
        Assert.Contains("plain exposed-area method", step.Clause!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Run_States_That_It_Is_Not_A_Temperature_Rise_Verification()
    {
        var r = CalcRunner.Run(Enclosure, CalcTemplateHasher.DefaultInputs(Enclosure.Template));

        var assumption = Assert.Single(r.RunAssumptions, a => a.Statement.Contains("plain exposed-area", StringComparison.Ordinal));
        Assert.Contains("not a verification", assumption.Statement, StringComparison.Ordinal);
        Assert.Contains("IEC TR 60890", assumption.Statement, StringComparison.Ordinal);
        Assert.Contains("BS EN 61439-1", assumption.Statement, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Assumption_Or_Method_Text_Calls_The_Area_Method_An_IEC_TR_60890_Approach()
    {
        Assert.DoesNotContain("TR 60890 approach", Enclosure.Template.Method, StringComparison.Ordinal);
        Assert.DoesNotContain(Enclosure.Template.Assumptions, a => (a.Statement + a.Basis).Contains("60890-style", StringComparison.Ordinal));
    }
}

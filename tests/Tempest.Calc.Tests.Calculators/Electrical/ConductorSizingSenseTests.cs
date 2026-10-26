using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E4 1.3.0: elec.conductor-sizing-02 (C_g and C_i are at most 1), -03 (the derived C_a names its reference ambient), -05 (the ampacity record is checked for sense).
/// Expected values from python3: linked I_b 25 A on type C MCB → I_n 25 A, factors 1, I_t,req 25 A; rows 4 mm² 24.1 A and 4 mm² 30 A: the old code took 30 A and
/// returned 4 mm² (I_z 30 A); the lower capacity 24.1 A does not carry 25 A, so the next row, 6 mm² (31.9 A), is selected.
/// </summary>
public sealed class ConductorSizingSenseTests
{
    private static readonly ConductorSizingCalculator Sizing = new();

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Sizing, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Sizing.Template).With(CalcInputValue.Of("C_a", 1)).With(CalcInputValue.Of("C_g", 1)).With(CalcInputValue.Of("N_g", 1)), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Theory]
    [InlineData("C_g", 1.5)]
    [InlineData("C_i", 1.5)]
    [InlineData("C_g", 1.01)]
    public void A_Grouping_Or_Insulation_Factor_Above_One_Is_Refused(string symbol, double value)
    {
        var r = Run(CalcInputValue.Of(symbol, value));

        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith(symbol + ":", StringComparison.Ordinal));
    }

    [Fact]
    public void Grouping_And_Insulation_Factors_Of_One_And_Below_Are_Accepted()
    {
        Assert.False(Run(CalcInputValue.Of("C_g", 1), CalcInputValue.Of("C_i", 1)).IsBlocked);
        Assert.False(Run(CalcInputValue.Of("C_g", 0.8), CalcInputValue.Of("C_i", 0.5)).IsBlocked);
    }

    [Fact]
    public void The_Derived_Ambient_Factor_Names_The_Reference_It_Was_Taken_Against()
    {
        var r = Run(Q("theta_a", 40, "degC"), CalcInputValue.Switch("derive_Ca", true));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "C_a");
        Assert.Contains("θ_ref", warning.Message, StringComparison.Ordinal);
        Assert.Contains("30", warning.Message, StringComparison.Ordinal);
        Assert.Contains("reference ambient of the table", warning.Message, StringComparison.Ordinal);
        Assert.Equal(Math.Sqrt(30.0 / 40.0), r.OutputSi("C_a"), 9);
    }

    [Fact]
    public void A_Cross_Section_Entered_Twice_Uses_The_Lower_Capacity_And_Warns()
    {
        var r = Run(Q("I_b_in", 25, "A"), ConductorSizingCalculator.Ampacity((2.5, 17.7), (4, 24.1), (4, 30), (6, 31.9)));

        Assert.False(r.IsBlocked);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "ampacity-sense");
        Assert.Contains("more than once", warning.Message, StringComparison.Ordinal);
        Assert.Equal(6, r.OutputSi("CSA") * 1e6, 9);
        Assert.Equal(31.9, r.OutputSi("I_z"), 9);
    }

    [Fact]
    public void A_Larger_Cross_Section_With_A_Lower_Capacity_Is_Warned()
    {
        var r = Run(Q("I_b_in", 12, "A"), ConductorSizingCalculator.Ampacity((1.5, 12.3), (2.5, 17.7), (4, 14.0), (6, 31.9)));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "ampacity-sense");
        Assert.Contains("falls", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Sensible_Record_Raises_No_Sense_Warning()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "ampacity-sense");
    }
}

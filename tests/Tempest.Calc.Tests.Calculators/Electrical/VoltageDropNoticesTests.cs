using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E3 1.2.0: elec.voltage-drop-02 (a blank cross-section or resistivity became 35 mm² and the default resistivity with no notice) and -03 (the mV/A/m route ignored I_z, so 60 A on a 50 A
/// cable passed with only the drop check). Expected values from python3: the mV/A/m figure 2.4 over 50 m at 60 A is 2.4 × 60 × 50/1000 = 7.2 V = 1.8 % of 400 V; I/I_z = 60/50 = 1.2;
/// T_c_est = 30 + (70 − 30) × 1.2² = 87.6 °C.
/// </summary>
public sealed class VoltageDropNoticesTests
{
    private static readonly VoltageDropCalculator Drop = new();

    private static CalcInputSet Case(int index, params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Drop.Template.TestCases[index].Inputs]), (set, v) => set.With(v));

    private static CalcInputSet Without(int index, params string[] symbols) => new([.. Drop.Template.TestCases[index].Inputs.Where(v => !symbols.Contains(v.Symbol))]);

    [Fact]
    public void A_Blank_Cross_Section_Is_Warned_Because_It_Becomes_35_mm2()
    {
        var r = CalcRunner.Run(Drop, Without(1, "A_c"));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "default-conductor" && w.Message.StartsWith("A_c:", StringComparison.Ordinal));
        Assert.Contains("35 mm", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Blank_Resistivity_Is_Warned_Because_It_Becomes_The_Cable_Standard_Default()
    {
        var r = CalcRunner.Run(Drop, Without(1, "rho_20"));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "default-conductor" && w.Message.StartsWith("rho_20:", StringComparison.Ordinal));
        Assert.Contains("0.0194", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Cross_Section_And_Resistivity_That_Were_Entered_Raise_No_Default_Notice()
    {
        Assert.DoesNotContain(CalcRunner.Run(Drop, Case(1)).Warnings, w => w.RuleId == "default-conductor");
    }

    [Fact]
    public void The_mV_A_m_Route_Checks_The_Current_Against_The_Capacity_When_It_Is_Entered()
    {
        var r = CalcRunner.Run(Drop, Case(2, CalcInputValue.Quantity("I", 60, "A"), CalcInputValue.Quantity("I_z", 50, "A")));

        Assert.Equal(7.2, r.OutputSi("dV"), 9);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("I ≤ I_z", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(r.Warnings, w => w.RuleId == "overload");
        Assert.Equal(273.15 + 87.6, r.OutputSi("T_c_est"), 6);
    }

    [Fact]
    public void The_mV_A_m_Route_Without_A_Capacity_Is_As_Before()
    {
        var r = CalcRunner.Run(Drop, Case(2));

        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("I ≤ I_z", StringComparison.Ordinal));
    }
}

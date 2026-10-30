using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 phase C (building services and fluid power): an input or a port renamed by the sheet standard still loads under its old name.
/// An inputs file, a CSV, a saved draft and a pack binding written before the rename name the old symbol; each reads as the new one with the same value.
/// </summary>
public sealed class OldFormInputsTests
{
    private static readonly (string, string, string)[] InputRows =
    [
        ("bs.duct-sizing", "T", "T_air"), ("bs.duct-sizing", "eps", "eps_rough"),
        ("bs.glaser", "theta_int", "T_int"),
        ("bs.heat-exchanger", "U", "U_clean"),
        ("bs.insulation", "theta_lambda", "T_lambda"), ("bs.insulation", "theta_i", "T_proc"), ("bs.insulation", "theta_a", "T_amb"), ("bs.insulation", "theta_dew", "T_dew"),
        ("bs.pipe-pump", "T", "T_fluid"), ("bs.pipe-pump", "v_max", "v_limit"),
        ("bs.psychrometrics", "t_db", "T_db"), ("bs.psychrometrics", "p", "p_atm"), ("bs.psychrometrics", "t_wb", "T_wb"), ("bs.psychrometrics", "t_dp", "T_dp"),
        ("bs.psychrometrics", "h", "h_air"), ("bs.psychrometrics", "t_2", "T_2"), ("bs.psychrometrics", "t_db_b", "T_db_b"),
        ("bs.room-heat-loss", "theta_int", "T_int"), ("bs.room-heat-loss", "theta_supply", "T_supply"),
        ("bs.sealed-system", "t_f", "T_flow"), ("bs.sealed-system", "t_r", "T_return"), ("bs.sealed-system", "t_i", "T_room"), ("bs.sealed-system", "t_fill", "T_fill"), ("bs.sealed-system", "t_max", "T_max"),
        ("fp.accumulator", "n", "n_gas"),
        ("fp.cylinder", "p", "p_sys"),
    ];

    private static readonly (string, string, string)[] PortRows =
    [
        ("bs.heat-exchanger", "Phi", "Q"),
        ("bs.insulation", "T_se", "theta_se"),
        ("bs.plane-wall", "Phi", "Q"), ("bs.plane-wall", "U_wall", "U"),
        ("bs.psychrometrics", "T_1_out", "t_1_out"), ("bs.psychrometrics", "T_dp_1", "t_dp_1"), ("bs.psychrometrics", "T_wb_1", "t_wb_1"), ("bs.psychrometrics", "T_2_out", "t_2_out"), ("bs.psychrometrics", "Phi", "Q"),
        ("bs.room-heat-loss", "T_int", "theta_int"),
        ("bs.sealed-system", "T_m", "t_m"),
        ("fp.cylinder", "p_sys", "p"),
    ];

    public static IEnumerable<object[]> RenamedInputs => InputRows.Select(r => new object[] { r.Item1, r.Item2, r.Item3 });

    public static IEnumerable<object[]> RenamedPorts => PortRows.Select(r => new object[] { r.Item1, r.Item2, r.Item3 });

    [Theory]
    [MemberData(nameof(RenamedInputs))]
    public void An_Input_Saved_Under_Its_Old_Symbol_Reads_As_The_New_One_With_The_Same_Value(string templateId, string oldSymbol, string newSymbol)
    {
        var template = AllCalculators.Get(templateId).Template;
        var spec = template.FindInput(newSymbol)!;
        Assert.Contains(oldSymbol, spec.Aliases ?? []);
        Assert.Null(template.FindInput(oldSymbol));

        // The value the sheet opens on, or the middle of its usual range for an input with no default.
        var number = spec.Default ?? ((spec.Min ?? spec.HardMin ?? 0) + (spec.Max ?? spec.HardMax ?? 2)) / 2;
        var unit = spec.Kind == CalcInputKind.Quantity ? spec.DefaultUnit : null;
        var saved = unit is null ? CalcInputValue.Of(oldSymbol, number) : CalcInputValue.Quantity(oldSymbol, number, unit);

        var canonical = CalcInputAliases.Canonicalise(template, [saved]);
        var read = Assert.Single(canonical);
        Assert.Equal(newSymbol, read.Symbol);
        Assert.Equal(saved.Number, read.Number);
        Assert.Equal(saved.Unit, read.Unit);
    }

    [Theory]
    [MemberData(nameof(RenamedInputs))]
    public void A_Run_On_The_Old_Symbol_Gives_The_Same_Result_As_A_Run_On_The_New_One(string templateId, string oldSymbol, string newSymbol)
    {
        var calculator = AllCalculators.Get(templateId);
        var defaults = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var current = defaults.Find(newSymbol);

        if (current is null)
            return;

        var oldForm = new CalcInputSet([.. defaults.Values.Where(v => v.Symbol != newSymbol), current with { Symbol = oldSymbol }]);
        var before = CalcRunner.Run(calculator, defaults);
        var after = CalcRunner.Run(calculator, oldForm);

        Assert.Equal(before.Outputs.Select(o => $"{o.Symbol}={o.ValueText}"), after.Outputs.Select(o => $"{o.Symbol}={o.ValueText}"));
        Assert.Equal(before.Checks.Select(c => $"{c.Name}:{c.Passed}"), after.Checks.Select(c => $"{c.Name}:{c.Passed}"));
        Assert.Equal(before.Blocks.Select(b => b.RuleId), after.Blocks.Select(b => b.RuleId));
        Assert.Equal(before.Warnings.Select(w => w.RuleId), after.Warnings.Select(w => w.RuleId));
    }

    [Theory]
    [MemberData(nameof(RenamedPorts))]
    public void A_Link_Or_Binding_Naming_The_Old_Output_Symbol_Still_Finds_The_Port(string templateId, string symbol, string oldName)
    {
        var port = AllCalculators.Get(templateId).Template.OutputPorts!.Single(p => p.Symbol == symbol);
        Assert.True(port.Matches(oldName));
        Assert.True(port.Matches(symbol));
        Assert.True(port.Matches(port.PublicName));
    }
}

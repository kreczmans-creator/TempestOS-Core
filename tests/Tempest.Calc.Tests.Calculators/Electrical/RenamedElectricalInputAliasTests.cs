using Tempest.Calc.Electrical;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// C-2 phase C (C2-10): an electrical input renamed to the standard's symbol keeps its old symbol as an alias. A calculation saved before the
/// rename, an inputs file and a pack binding that name the old symbol load, give the same values and are stored under the new symbol.
/// </summary>
public sealed class RenamedElectricalInputAliasTests
{
    /// <summary>One rename: the template, the symbol it had, the symbol it has, and the old-form value to give it with the other inputs the case needs.</summary>
    internal sealed record Rename(string TemplateId, string Old, string Current, CalcInputValue Value, params CalcInputValue[] With)
    {
        public override string ToString() => $"{TemplateId}: {Old} -> {Current}";
    }

    internal static readonly IReadOnlyList<Rename> Renames =
    [
        new("elec.conductor-sizing", "theta_a", "T_amb", CalcInputValue.Quantity("theta_a", 35, "degC")),
        new("elec.conductor-sizing", "theta_p", "T_user", CalcInputValue.Quantity("theta_p", 105, "degC"), CalcInputValue.Choice("insulation", ConductorSizingCalculator.UserInsulation), CalcInputValue.Switch("derive_Ca", true), CalcInputValue.Quantity("T_amb", 40, "degC")),
        new("elec.conductor-sizing", "theta_ref", "T_ref", CalcInputValue.Quantity("theta_ref", 25, "degC"), CalcInputValue.Switch("derive_Ca", true), CalcInputValue.Quantity("T_amb", 40, "degC")),
        new("elec.fault-protection", "theta", "T_op", CalcInputValue.Quantity("theta", 80, "degC")),
        new("elec.load-budget", "margin", "SF", CalcInputValue.Of("margin", 1.3)),
        new("elec.load-budget", "theta", "T_amb", CalcInputValue.Quantity("theta", 45, "degC")),
    ];

    public static TheoryData<string> Cases => [.. Renames.Select(r => r.ToString())];

    private static Rename Find(string name) => Renames.Single(r => r.ToString() == name);

    private static ICalcCalculator Calculator(string id) => AllCalculators.All.Single(c => c.Template.Id == id);

    /// <summary>The first known-answer case's inputs, then the rename's own inputs under their current names, then the renamed value under its old or its new symbol.</summary>
    private static CalcInputSet Inputs(Rename rename, bool oldForm)
    {
        var template = Calculator(rename.TemplateId).Template;
        var set = new CalcInputSet(template.TestCases[0].Inputs);
        set = rename.With.Aggregate(set, (s, v) => s.With(v));
        var value = oldForm ? rename.Value : rename.Value with { Symbol = rename.Current };
        return set.With(value);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_Renamed_Input_Keeps_Its_Old_Symbol_As_An_Alias(string name)
    {
        var rename = Find(name);
        var spec = Calculator(rename.TemplateId).Template.FindInput(rename.Current);

        Assert.NotNull(spec);
        Assert.Contains(rename.Old, spec!.Aliases!);
        Assert.Null(Calculator(rename.TemplateId).Template.FindInput(rename.Old));
        Assert.Same(spec, Calculator(rename.TemplateId).Template.ResolveInput(rename.Old));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_Calculation_Saved_Under_The_Old_Symbol_Gives_Every_Value_Of_The_New_Form(string name)
    {
        var rename = Find(name);
        var calculator = Calculator(rename.TemplateId);

        var fresh = CalcRunner.Run(calculator, Inputs(rename, oldForm: false));
        var saved = CalcRunner.Run(calculator, Inputs(rename, oldForm: true));

        Assert.Equal(fresh.Outputs.Select(o => o.Symbol), saved.Outputs.Select(o => o.Symbol));
        Assert.Equal(fresh.Checks.Select(c => (c.Name, c.Passed)), saved.Checks.Select(c => (c.Name, c.Passed)));
        Assert.Equal(fresh.Notices.Select(n => (n.RuleId, n.Severity)), saved.Notices.Select(n => (n.RuleId, n.Severity)));

        foreach (var output in fresh.Outputs.Where(o => o.Value is ScalarValue))
            Assert.Equal(((ScalarValue)output.Value).Si, saved.OutputSi(output.Symbol));

        // The old symbol is not left among the steps: the run was made on the canonical input.
        Assert.DoesNotContain(saved.Steps, s => s.Symbol == rename.Old);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void An_Inputs_File_Written_Under_The_Old_Symbol_Is_Read_Under_The_New_One(string name)
    {
        var rename = Find(name);
        var template = Calculator(rename.TemplateId).Template;
        var json = CalcInputJson.Write(Inputs(rename, oldForm: true));

        Assert.Contains($"\"{rename.Old}\"", json, StringComparison.Ordinal);
        var (inputs, _) = CalcInputJson.Read(json, template);

        Assert.NotNull(inputs.Find(rename.Current));
        Assert.Null(inputs.Find(rename.Old));
        Assert.Equal(rename.Value.Number, inputs.Find(rename.Current)!.Number);
    }

    [Fact]
    public void The_Cable_Step_Operating_Temperature_Port_Answers_To_Its_Old_Symbol()
    {
        var template = ConductorTemplate();
        var port = template.FindPort("theta_op");

        Assert.NotNull(port);
        Assert.Equal("T_op", port!.Symbol);
        Assert.Equal("conductor.temperature_operating", port.Name);
        Assert.Same(port, template.FindPort("conductor.temperature_operating"));
    }

    private static CalcTemplate ConductorTemplate() => Calculator("elec.conductor-sizing").Template;
}

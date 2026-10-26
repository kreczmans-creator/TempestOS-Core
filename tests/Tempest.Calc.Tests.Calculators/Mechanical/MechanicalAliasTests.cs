using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// C-2 phase B (mechanical sheets): an inputs file, a CSV, a saved draft or a pack example that still gives an input under the symbol it had before the rename must run exactly as under the
/// current symbol, and a link that names an output port by its old symbol must still find the port. The rows are read from the templates' own aliases, so a rename that forgets its alias fails here.
/// </summary>
public sealed class MechanicalAliasTests
{
    public static TheoryData<string> TemplateIds => new()
    {
        MaterialMarginCalculator.Id, FatigueMinerCalculator.Id, Iso286FitCalculator.Id, PressFitCalculator.Id, BearingLifeBasicCalculator.Id, BearingLifeCalculator.Id, BoltGroupCalculator.Id, BoltedJointCalculator.Id,
    };

    private static ICalcCalculator Find(string id) => id switch
    {
        MaterialMarginCalculator.Id => new MaterialMarginCalculator(),
        FatigueMinerCalculator.Id => new FatigueMinerCalculator(),
        Iso286FitCalculator.Id => new Iso286FitCalculator(),
        PressFitCalculator.Id => new PressFitCalculator(),
        BearingLifeBasicCalculator.Id => new BearingLifeBasicCalculator(),
        BearingLifeCalculator.Id => new BearingLifeCalculator(),
        BoltGroupCalculator.Id => new BoltGroupCalculator(),
        BoltedJointCalculator.Id => new BoltedJointCalculator(),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "not a template of this test"),
    };

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Every_Renamed_Input_Given_Under_Its_Old_Symbol_Runs_Exactly_As_Under_The_Current_One(string id)
    {
        var calculator = Find(id);
        var defaults = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var reference = CalcRunner.Run(calculator, defaults);
        Assert.False(reference.IsBlocked, id);

        foreach (var spec in calculator.Template.Inputs.Where(s => s.Aliases is { Count: > 0 }))
        {
            var given = defaults.Find(spec.Symbol);

            if (given is null)
                continue;

            foreach (var old in spec.Aliases!)
            {
                var oldForm = new CalcInputSet(defaults.Values.Select(v => v.Symbol == spec.Symbol ? v with { Symbol = old } : v).ToList());
                var run = CalcRunner.Run(calculator, oldForm);

                Assert.False(run.IsBlocked, $"{id}: {old}");
                Assert.DoesNotContain(run.Notices, n => n.Message.Contains("not an input", StringComparison.Ordinal));
                Assert.Equal(reference.Notices.Select(n => n.Message), run.Notices.Select(n => n.Message));

                foreach (var output in reference.Outputs.Where(o => o.Value is ScalarValue))
                    Assert.Equal(reference.OutputSi(output.Symbol), run.OutputSi(output.Symbol));

                Assert.Equal(spec.Symbol, calculator.Template.ResolveInput(old)!.Symbol);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Every_Renamed_Port_Is_Found_By_Its_Old_Symbol(string id)
    {
        var template = Find(id).Template;

        foreach (var port in template.OutputPorts ?? [])
        foreach (var old in port.Aliases ?? [])
            Assert.Same(port, template.FindPort(old));
    }
}

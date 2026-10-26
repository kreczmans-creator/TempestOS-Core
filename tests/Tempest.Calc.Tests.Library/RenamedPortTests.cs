using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// C-2, row 43: the ports of the nine calculators the audit maps are now named <c>&lt;area&gt;.&lt;quantity&gt;</c>. Everything saved or written before the
/// rename names the output symbol (a link <c>calc:&lt;id&gt;#R_A</c>, a pack binding <c>("m4", "R_A", "m5", "F_r")</c>), and must keep working beside the new name.
/// </summary>
public sealed class RenamedPortTests
{
    private static readonly string[] Nine = ["mech.bearing-life", "mech.shaft", "mech.weld-group", "struct.connection-ec3", "struct.load-combinations", "elec.load-budget", "elec.fault-protection", "bs.heat-exchanger", "bs.sealed-system"];

    [Fact]
    public void Every_Port_Of_The_Nine_Calculators_Has_A_Well_Formed_Public_Name_From_Its_Area_And_Resolves_By_Name_And_By_Old_Symbol()
    {
        var calculators = ShippedCalculators.All.Where(c => Nine.Contains(c.Template.Id)).ToList();
        Assert.Equal(Nine.Length, calculators.Count);

        foreach (var calculator in calculators)
        {
            var template = calculator.Template;
            Assert.NotEmpty(template.OutputPorts!);

            foreach (var port in template.OutputPorts!)
            {
                Assert.Null(CalcPortNames.Problem(template.Id, port.Name));
                Assert.Same(port, template.FindPort(port.Symbol));
                Assert.Same(port, template.FindPort(port.Name!));
                Assert.Equal(port.Name, port.PublicName);
                Assert.DoesNotContain(" ", port.Name!, StringComparison.Ordinal);
            }

            // Names are unique within the template, and none repeats another port's symbol.
            Assert.Equal(template.OutputPorts!.Count, template.OutputPorts!.Select(p => p.Name).Distinct().Count());
        }
    }

    [Fact]
    public async Task A_Link_To_The_Shaft_Reaction_Resolves_By_The_Old_Symbol_And_By_The_New_Name_To_The_Same_Value()
    {
        var host = new LibraryTestHost(calculators: [new ShaftCalculator(), new BearingLifeCalculator()]);
        var project = await host.ProjectAsync();
        var shaft = await host.Service.CreateAsync(ShaftCalculator.Id, project, "Gearbox shaft", new ShaftCalculator().Template.TestCases[0].Inputs.Select(v => v with { Provenance = CalcProvenance.Drawing("DRG-SH-1", "A") }));
        Assert.False(shaft.Blocks.Count > 0, string.Join("; ", shaft.Blocks.Select(b => b.Message)));
        var shaftReaction = shaft.Outputs.Single(o => o.Symbol == "R_A").Si!.Value;

        var oldForm = await host.Service.CreateAsync(BearingLifeCalculator.Id, project, "Bearing A (old form)");
        var bound = await host.Service.BindAsync(oldForm.Id, "F_r", shaft.Id, "R_A");
        Assert.True(bound.Succeeded, bound.Reason);
        Assert.Equal(shaftReaction, bound.Instance!.Inputs.Single(i => i.Symbol == "F_r").Number!.Value * 1000, 6);
        Assert.Equal("R_A", bound.Instance.Links.Single().Link.OutputSymbol);

        var newForm = await host.Service.CreateAsync(BearingLifeCalculator.Id, project, "Bearing A (new form)");
        var byName = await host.Service.BindAsync(newForm.Id, "F_r", shaft.Id, "shaft.reaction_a");
        Assert.True(byName.Succeeded, byName.Reason);
        Assert.Equal(bound.Instance.Inputs.Single(i => i.Symbol == "F_r").Number, byName.Instance!.Inputs.Single(i => i.Symbol == "F_r").Number);
        Assert.Equal(bound.Instance.Outputs.Single(o => o.Symbol == "L_10h").Si, byName.Instance.Outputs.Single(o => o.Symbol == "L_10h").Si);

        // Both stay fresh against the upstream, and the port list shows the public names.
        Assert.False((await host.Service.StalenessAsync(bound.Instance)).IsStale);
        Assert.False((await host.Service.StalenessAsync(byName.Instance)).IsStale);
        Assert.Contains("shaft.reaction_a", host.Service.OutputPorts(shaft.TemplateReference).Select(p => p.PublicName));
    }

    [Fact]
    public void The_Shipped_Pack_Templates_Still_Bind_The_Old_Output_Symbols()
    {
        var host = new LibraryTestHost(calculators: ShippedCalculators.All, packTemplates: CalcPackTemplates.All);
        var withOldSymbols = CalcPackTemplates.All.SelectMany(t => t.Bindings.Select(b => (t, b))).ToList();

        Assert.NotEmpty(withOldSymbols);
        Assert.All(CalcPackTemplates.All, template => Assert.Null(host.Service.ValidatePackTemplate(template)));

        // At least one binding names a port the audit renamed, by its old symbol.
        Assert.Contains(withOldSymbols, x => x.b.Output is "R_A" or "I_3ph" or "Phi_tot" or "Q" or "F_v_Rd");
    }
}

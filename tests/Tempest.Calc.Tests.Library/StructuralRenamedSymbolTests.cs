using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// C-2 phase B (structural): the symbols renamed on the structural sheets keep working in everything saved or written before the rename: a link by an old output
/// symbol, an input given under its old symbol and a pack binding by an old name. The section sheet names its properties in the EN 1993 axes (y-y major, z-z minor),
/// the same names the member check and the LTB sheet use for their inputs.
/// </summary>
public sealed class StructuralRenamedSymbolTests
{
    private static IReadOnlyList<CalcInputValue> WithDrawing(IEnumerable<CalcInputValue> inputs) => [.. inputs.Select(v => v with { Provenance = CalcProvenance.Drawing("DRG-SEC-1", "A") })];

    private static async Task<(LibraryTestHost Host, Guid? Project, CalcInstance Section)> SectionAsync()
    {
        var host = new LibraryTestHost(calculators: [new SectionPropertiesCalculator(), new BeamFormulasCalculator()]);
        var project = await host.ProjectAsync();
        var inputs = WithDrawing(new SectionPropertiesCalculator().Template.TestCases[0].Inputs);
        var section = await host.Service.CreateAsync(SectionPropertiesCalculator.Id, project, "Floor beam section", inputs);
        Assert.Empty(section.Blocks);
        return (host, project, section);
    }

    [Theory]
    [InlineData("I_y")]
    [InlineData("I_xx")]
    [InlineData("section.second_moment_y")]
    public async Task A_Link_To_The_Major_Axis_Second_Moment_Resolves_By_The_Old_Symbol_The_New_Symbol_And_The_Public_Name(string output)
    {
        var (host, project, section) = await SectionAsync();
        var beam = await host.Service.CreateAsync(BeamFormulasCalculator.Id, project, "Floor beam");

        var bound = await host.Service.BindAsync(beam.Id, "I", section.Id, output);

        Assert.True(bound.Succeeded, bound.Reason);
        var input = bound.Instance!.Inputs.Single(i => i.Symbol == "I");
        Assert.Equal(section.Outputs.Single(o => o.Symbol == "I_y").Si!.Value * 1e8, input.Number!.Value, 4);
        Assert.Equal(45727.986, input.Number!.Value, 2);
        Assert.Equal(new CalcLink(section.Id, output, section.Revision), CalcLink.From(input.Provenance));
        Assert.False((await host.Service.StalenessAsync(bound.Instance)).IsStale);
    }

    [Fact]
    public async Task A_Link_Saved_Under_An_Old_Section_Symbol_Still_Updates_After_The_Section_Changes()
    {
        var (host, project, section) = await SectionAsync();

        // The form a link had before the rename: the old output symbol in the reference.
        var saved = CalcInputValue.Quantity("I", 45727.986, "cm^4", CalcProvenance.Link(section.Id, "I_xx", section.Revision));
        var beam = await host.Service.CreateAsync(BeamFormulasCalculator.Id, project, "Floor beam (old link)", [saved]);
        Assert.Equal("I_xx", beam.Links.Single().Link.OutputSymbol);
        Assert.False((await host.Service.StalenessAsync(beam)).IsStale);

        // The section is given a deeper web plate: the beam goes stale, and updating it takes the new value through the old symbol.
        var deeper = WithDrawing(new SectionPropertiesCalculator().Template.TestCases[0].Inputs.Select(v => v.Symbol == "h" ? CalcInputValue.Quantity("h", 500, "mm") : v));
        var changed = await host.Service.UpdateInputsAsync(section.Id, deeper);
        Assert.True(changed.Succeeded, changed.Reason);
        Assert.True((await host.Service.StalenessAsync(beam)).IsStale);

        var updated = await host.Service.UpdateLinksAsync(beam.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        var now = changed.Instance!.Outputs.Single(o => o.Symbol == "I_y").Si!.Value * 1e8;
        Assert.Equal(now, updated.Instance!.Inputs.Single(i => i.Symbol == "I").Number!.Value, 4);
        Assert.True(now > 45727.986);
        Assert.Equal("I_xx", updated.Instance.Links.Single().Link.OutputSymbol);
    }

    [Fact]
    public async Task The_Old_Wall_Thickness_Symbol_Is_Stored_Under_The_New_One_On_Create_And_On_Update()
    {
        var (host, project, _) = await SectionAsync();
        var oldForm = WithDrawing(new SectionPropertiesCalculator().Template.TestCases[1].Inputs.Select(v => v.Symbol == "t_w" ? CalcInputValue.Quantity("t", 5, "mm") : v));

        var shs = await host.Service.CreateAsync(SectionPropertiesCalculator.Id, project, "SHS (old form)", oldForm);

        Assert.Equal(5, shs.Inputs.Single(i => i.Symbol == "t_w").Number);
        Assert.DoesNotContain(shs.Inputs, i => i.Symbol == "t");
        Assert.Equal(279.0, shs.Outputs.Single(o => o.Symbol == "I_y").Si!.Value * 1e8, 0);

        var thicker = await host.Service.UpdateInputsAsync(shs.Id, [.. oldForm.Where(i => i.Symbol != "t"), CalcInputValue.Quantity("t", 6, "mm", CalcProvenance.Drawing("DRG-SEC-1", "B"))]);
        Assert.True(thicker.Succeeded, thicker.Reason);
        Assert.Equal(6, thicker.Instance!.Inputs.Single(i => i.Symbol == "t_w").Number);
        Assert.True(thicker.Instance.Outputs.Single(o => o.Symbol == "I_y").Si!.Value > shs.Outputs.Single(o => o.Symbol == "I_y").Si!.Value);
    }

    [Fact]
    public async Task A_Draft_Saved_On_The_Old_Section_Version_Carries_The_Wall_Thickness_Instead_Of_Dropping_It()
    {
        var (host, _, section) = await SectionAsync();
        var older = section with
        {
            TemplateReference = "struct.section-properties@1.1.1",
            Inputs = [CalcInputValue.Choice("shape", "I-section", CalcProvenance.Manual), CalcInputValue.Quantity("h", 300, "mm", CalcProvenance.Manual), CalcInputValue.Quantity("t", 8, "mm", CalcProvenance.Manual)],
        };

        var plan = host.Service.PlanDraftUpgrade(older);

        Assert.NotNull(plan);
        Assert.Empty(plan!.Dropped);
        Assert.Equal(8, plan.Inputs.Find("t_w")!.Number);
        Assert.Null(plan.Inputs.Find("t"));
    }

    [Fact]
    public async Task The_Minor_Axis_Properties_Are_Reached_By_Their_New_Symbols_And_The_Old_Major_Axis_Symbols_Name_The_Major_Axis()
    {
        var (host, _, section) = await SectionAsync();
        var template = host.Catalogue.Find(SectionPropertiesCalculator.Id)!.Template;

        // Old x-x is the major axis y-y of EN 1993, old y-y the minor axis z-z; the minor-axis moduli have new names that no old symbol can be mistaken for.
        Assert.Same(template.FindPort("I_y"), template.FindPort("I_xx"));
        Assert.Same(template.FindPort("I_z"), template.FindPort("I_yy"));
        Assert.Same(template.FindPort("W_el_y"), template.FindPort("W_el_x"));
        Assert.Same(template.FindPort("W_pl_y"), template.FindPort("W_pl_x"));
        Assert.Same(template.FindPort("r_g_y"), template.FindPort("i_x"));
        Assert.Same(template.FindPort("r_g_z"), template.FindPort("i_y"));
        Assert.NotNull(template.FindPort("W_el_z"));
        Assert.NotNull(template.FindPort("W_pl_z"));

        var major = section.Outputs.Single(o => o.Symbol == "W_el_y").Si!.Value;
        var minor = section.Outputs.Single(o => o.Symbol == "W_el_z").Si!.Value;
        Assert.True(major > minor * 5, "the major-axis modulus of an I-section is many times the minor-axis one");
    }
}

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

    [Fact]
    public async Task The_Old_Load_Symbol_P_Of_The_Beam_And_The_Column_Is_Stored_Under_F_And_The_Old_Column_Outputs_Resolve_By_Their_Old_Symbols()
    {
        var host = new LibraryTestHost(calculators: [new BeamFormulasCalculator(), new ColumnBucklingCalculator()]);
        var project = await host.ProjectAsync();

        // The inputs of the two known-answer cases as they were written before the rename: the load is P.
        var oldBeam = WithDrawing(new BeamFormulasCalculator().Template.TestCases[1].Inputs.Select(v => v.Symbol == "F" ? CalcInputValue.Quantity("P", 50, "kN") : v));
        var beam = await host.Service.CreateAsync(BeamFormulasCalculator.Id, project, "Beam (old form)", oldBeam);
        Assert.Equal(50, beam.Inputs.Single(i => i.Symbol == "F").Number);
        Assert.DoesNotContain(beam.Inputs, i => i.Symbol == "P");
        Assert.Equal(16.6667, beam.Outputs.Single(o => o.Symbol == "R_A").Si!.Value / 1e3, 3);

        var oldColumn = WithDrawing(new ColumnBucklingCalculator().Template.TestCases[0].Inputs.Select(v => v.Symbol == "F" ? CalcInputValue.Quantity("P", 150, "kN") : v));
        var column = await host.Service.CreateAsync(ColumnBucklingCalculator.Id, project, "Strut (old form)", oldColumn);
        Assert.Equal(150, column.Inputs.Single(i => i.Symbol == "F").Number);
        Assert.Equal(267.56, column.Outputs.Single(o => o.Symbol == "F_c_Rd").Si!.Value / 1e3, 1);

        // The resistance was P_c and the Euler load P_E: a link by either, or by the public name, resolves to the same value.
        foreach (var name in new[] { "F_c_Rd", "P_c", "column.resistance_compression" })
        {
            var downstream = await host.Service.CreateAsync(BeamFormulasCalculator.Id, project, $"Beam under {name}", WithDrawing(new BeamFormulasCalculator().Template.TestCases[1].Inputs));
            var bound = await host.Service.BindAsync(downstream.Id, "P", column.Id, name);
            Assert.True(bound.Succeeded, bound.Reason);
            Assert.Equal(column.Outputs.Single(o => o.Symbol == "F_c_Rd").Si!.Value / 1e3, bound.Instance!.Inputs.Single(i => i.Symbol == "F").Number!.Value, 6);
        }

        var template = host.Catalogue.Find(ColumnBucklingCalculator.Id)!.Template;
        Assert.Same(template.FindPort("F_E"), template.FindPort("P_E"));
    }

    [Fact]
    public async Task The_Old_Plate_Thickness_Symbol_Of_The_Connection_Is_Stored_Under_T_p_On_Create_And_On_Update()
    {
        var host = new LibraryTestHost(calculators: [new ConnectionCalculator()]);
        var project = await host.ProjectAsync();
        var baseline = WithDrawing([.. new ConnectionCalculator().Template.TestCases[2].Inputs.Where(v => v.Symbol != "t_p"), CalcInputValue.Quantity("t_p", 10, "mm")]);
        var current = await host.Service.CreateAsync(ConnectionCalculator.Id, project, "Splice", baseline);
        var thickness = 10.0;

        // The form a saved calculation had before the rename: the plate thickness is t.
        var oldForm = WithDrawing(baseline.Select(v => v.Symbol == "t_p" ? CalcInputValue.Quantity("t", thickness, "mm") : v));
        var created = await host.Service.CreateAsync(ConnectionCalculator.Id, project, "Splice (old form)", oldForm);

        Assert.Empty(created.Blocks);
        Assert.Equal(thickness, created.Inputs.Single(i => i.Symbol == "t_p").Number!.Value);
        Assert.DoesNotContain(created.Inputs, i => i.Symbol == "t");
        Assert.Equal(current.Outputs.Single(o => o.Symbol == "F_b_Rd").Si!.Value, created.Outputs.Single(o => o.Symbol == "F_b_Rd").Si!.Value, 6);

        var thicker = await host.Service.UpdateInputsAsync(created.Id, [.. oldForm.Where(i => i.Symbol != "t"), CalcInputValue.Quantity("t", thickness + 4, "mm", CalcProvenance.Drawing("DRG-SEC-1", "B"))]);
        Assert.True(thicker.Succeeded, thicker.Reason);
        Assert.Equal(thickness + 4, thicker.Instance!.Inputs.Single(i => i.Symbol == "t_p").Number!.Value);
        Assert.True(thicker.Instance.Outputs.Single(o => o.Symbol == "F_b_Rd").Si!.Value > created.Outputs.Single(o => o.Symbol == "F_b_Rd").Si!.Value);
    }

    [Fact]
    public async Task The_Old_Wall_Thickness_Symbol_Of_The_Base_Plate_Column_Is_Stored_Under_T_wall()
    {
        var host = new LibraryTestHost(calculators: [new BasePlateCalculator()]);
        var project = await host.ProjectAsync();
        var shared = new BasePlateCalculator().Template.TestCases[0].Inputs.Where(v => v.Symbol is not ("column" or "h" or "b" or "D" or "t_wall"));
        var oldForm = WithDrawing([.. shared, CalcInputValue.Choice("column", BasePlateCalculator.ColumnRhs), CalcInputValue.Quantity("h", 200, "mm"), CalcInputValue.Quantity("b", 200, "mm"), CalcInputValue.Quantity("t", 10, "mm")]);

        var created = await host.Service.CreateAsync(BasePlateCalculator.Id, project, "Base (old form)", oldForm);

        Assert.Empty(created.Blocks);
        Assert.Equal(10, created.Inputs.Single(i => i.Symbol == "t_wall").Number);
        Assert.DoesNotContain(created.Inputs, i => i.Symbol == "t");

        var thicker = await host.Service.UpdateInputsAsync(created.Id, [.. oldForm.Where(i => i.Symbol != "t"), CalcInputValue.Quantity("t", 12, "mm", CalcProvenance.Drawing("DRG-SEC-1", "B"))]);
        Assert.True(thicker.Succeeded, thicker.Reason);
        Assert.Equal(12, thicker.Instance!.Inputs.Single(i => i.Symbol == "t_wall").Number);
    }

    [Fact]
    public async Task The_Old_Thickness_And_Force_Symbols_Of_The_Plate_Are_Stored_Under_T_p_And_F_And_The_Old_Rigidity_Symbol_Still_Links()
    {
        var host = new LibraryTestHost(calculators: [new PlateBendingCalculator(), new BeamFormulasCalculator()]);
        var project = await host.ProjectAsync();
        var cases = new PlateBendingCalculator().Template.TestCases;

        // The inputs as a saved calculation had them before the rename: the thickness is t, and the ring force is P.
        var oldClamped = WithDrawing(cases[0].Inputs.Select(v => v.Symbol == "t_p" ? CalcInputValue.Quantity("t", 5, "mm") : v));
        var plate = await host.Service.CreateAsync(PlateBendingCalculator.Id, project, "Plate (old form)", oldClamped);
        Assert.Empty(plate.Blocks);
        Assert.Equal(5, plate.Inputs.Single(i => i.Symbol == "t_p").Number);
        Assert.DoesNotContain(plate.Inputs, i => i.Symbol == "t");
        Assert.Equal(0.533203125, plate.Outputs.Single(o => o.Symbol == "w_max").Si!.Value * 1e3, 9);

        var oldRing = WithDrawing(cases[3].Inputs.Select(v => v.Symbol == "F" ? CalcInputValue.Quantity("P", 4, "kN") : v));
        var ring = await host.Service.CreateAsync(PlateBendingCalculator.Id, project, "Ring (old form)", oldRing);
        Assert.Empty(ring.Blocks);
        Assert.Equal(4, ring.Inputs.Single(i => i.Symbol == "F").Number);
        Assert.DoesNotContain(ring.Inputs, i => i.Symbol == "P");

        // The flexural rigidity was the output D; a link by that symbol, by the new symbol or by the public name resolves to the same value.
        foreach (var name in new[] { "D", "D_flex", "plate.rigidity_flexural" })
        {
            var beam = await host.Service.CreateAsync(BeamFormulasCalculator.Id, project, $"Beam with a couple from {name}");
            var bound = await host.Service.BindAsync(beam.Id, "M_0", plate.Id, name);
            Assert.True(bound.Succeeded, bound.Reason);
            Assert.Equal(plate.Outputs.Single(o => o.Symbol == "D_flex").Si!.Value / 1e3, bound.Instance!.Inputs.Single(i => i.Symbol == "M_0").Number!.Value, 9);
        }
    }

    [Fact]
    public async Task The_Old_Thickness_And_Radius_Symbols_Of_The_Lifting_Lug_Are_Stored_Under_T_p_And_R_lug()
    {
        // The lifting sheet is behind the lifting feature flag; the flag is on in this test host only.
        var host = new LibraryTestHost(calculators: [new LiftingLugCalculator()], features: new CalcFeatureFlags([CalcFeatures.Lifting]));
        var project = await host.ProjectAsync();
        var baseline = new LiftingLugCalculator().Template.TestCases[0].Inputs;
        var current = await host.Service.CreateAsync(LiftingLugCalculator.Id, project, "Lug", baseline);
        Assert.Empty(current.Blocks);

        // The form a saved calculation had before the rename: the plate thickness is t and the end radius is R.
        var oldForm = baseline.Select(v => v.Symbol switch
        {
            "t_p" => CalcInputValue.Quantity("t", 20, "mm"),
            "r_lug" => CalcInputValue.Quantity("R", 65, "mm"),
            _ => v,
        }).ToList();
        var created = await host.Service.CreateAsync(LiftingLugCalculator.Id, project, "Lug (old form)", oldForm);

        Assert.Empty(created.Blocks);
        Assert.Equal(20, created.Inputs.Single(i => i.Symbol == "t_p").Number);
        Assert.Equal(65, created.Inputs.Single(i => i.Symbol == "r_lug").Number);
        Assert.DoesNotContain(created.Inputs, i => i.Symbol is "t" or "R");
        Assert.Equal(current.Outputs.Single(o => o.Symbol == "capacity").Si!.Value, created.Outputs.Single(o => o.Symbol == "capacity").Si!.Value, 9);
    }
}

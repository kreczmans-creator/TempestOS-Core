using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// C-2, rows 43 to 45: a port renamed to <c>&lt;area&gt;.&lt;quantity&gt;</c> and an input renamed by a later phase must not strand what was saved
/// before: a link saved under the old output symbol, a pack binding, an example input, a draft made on an older version and an
/// import still name the old symbol, and all of them still resolve to the same values.
/// <see cref="RenamedCalculator"/> stands for a template renamed that way: input <c>F_axial</c> (once <c>F</c> and <c>F_old</c>), port
/// <c>stress.axial</c> on output <c>sigma</c> (once <c>sigma_old</c>).
/// </summary>
public sealed class AliasCompatibilityTests
{
    private static LibraryTestHost Host(IReadOnlyList<CalcPackTemplate>? templates = null) =>
        new(calculators: [new ForceSourceCalculator(), new RenamedCalculator()], packTemplates: templates);

    private static readonly IReadOnlyList<CalcInputValue> Renamed =
    [
        CalcInputValue.Quantity("F_axial", 12, "kN", CalcProvenance.Drawing("DRG-1", "A")),
        CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("DRG-1", "A")),
        CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("SPEC-1")),
    ];

    private static async Task<(LibraryTestHost Host, Guid? Project, CalcInstance Stress)> StressAsync()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var stress = await host.Service.CreateAsync("test.renamed", project, "Stress", Renamed);
        return (host, project, stress);
    }

    [Theory]
    [InlineData("sigma")]
    [InlineData("sigma_old")]
    [InlineData("stress.axial")]
    public async Task A_Link_Binds_To_A_Port_By_Its_Symbol_Its_Old_Symbol_Or_Its_Public_Name_With_The_Same_Value(string output)
    {
        var (host, project, stress) = await StressAsync();
        var check = await host.Service.CreateAsync("test.direct-stress", project, "Check", LibraryTestHost.ProvenancedInputs());

        var bound = await host.Service.BindAsync(check.Id, "sigma_allow", stress.Id, output);

        Assert.True(bound.Succeeded, bound.Reason);
        var input = bound.Instance!.Inputs.Single(i => i.Symbol == "sigma_allow");
        Assert.Equal(60, input.Number!.Value, 9);
        Assert.Equal("MPa", input.Unit);
        Assert.Equal(new CalcLink(stress.Id, output, "A"), CalcLink.From(input.Provenance));
        Assert.False((await host.Service.StalenessAsync(bound.Instance)).IsStale);
    }

    [Fact]
    public async Task A_Link_Saved_Under_The_Old_Symbol_Still_Resolves_When_The_Upstream_Changes_And_Is_Updated()
    {
        var (host, project, stress) = await StressAsync();

        // The form a link had before the port was renamed: the output symbol in the reference.
        var saved = LibraryTestHost.ProvenancedInputs().Where(i => i.Symbol != "sigma_allow")
            .Append(CalcInputValue.Quantity("sigma_allow", 60, "MPa", CalcProvenance.Link(stress.Id, "sigma_old", stress.Revision)))
            .ToList();
        var check = await host.Service.CreateAsync("test.direct-stress", project, "Check", saved);
        Assert.Equal("sigma_old", check.Links.Single().Link.OutputSymbol);
        Assert.False((await host.Service.StalenessAsync(check)).IsStale);

        // The upstream moves on (a new load): the downstream is stale, and updating it takes the new value through the old reference.
        var changed = await host.Service.UpdateInputsAsync(stress.Id, [.. Renamed.Where(i => i.Symbol != "F_axial"), CalcInputValue.Quantity("F_axial", 24, "kN", CalcProvenance.Drawing("DRG-1", "B"))]);
        Assert.True(changed.Succeeded, changed.Reason);
        Assert.True((await host.Service.StalenessAsync(check)).IsStale);

        var updated = await host.Service.UpdateLinksAsync(check.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Equal(120, updated.Instance!.Inputs.Single(i => i.Symbol == "sigma_allow").Number!.Value, 9);
        Assert.Equal("sigma_old", updated.Instance.Links.Single().Link.OutputSymbol);
    }

    [Fact]
    public async Task A_Port_Name_That_Is_Not_Offered_Is_Refused_And_The_Refusal_Lists_The_Public_Names()
    {
        var (host, project, stress) = await StressAsync();
        var check = await host.Service.CreateAsync("test.direct-stress", project, "Check", LibraryTestHost.ProvenancedInputs());

        // "u" is an output of the run but not a declared port; "stress.nothing" is no name at all.
        var u = await host.Service.BindAsync(check.Id, "sigma_allow", stress.Id, "u");
        Assert.False(u.Succeeded);
        Assert.Contains("link to one of: stress.axial, stress.area_required", u.Reason, StringComparison.Ordinal);

        Assert.False((await host.Service.BindAsync(check.Id, "sigma_allow", stress.Id, "stress.nothing")).Succeeded);
    }

    [Fact]
    public async Task The_Port_List_Offers_The_Declared_Ports_With_Their_Names()
    {
        var (host, _, _) = await StressAsync();
        var ports = host.Service.OutputPorts("test.renamed@1.1.0");

        Assert.Equal(["stress.axial", "stress.area_required"], ports.Select(p => p.PublicName));
        Assert.Equal(["sigma", "A_req"], ports.Select(p => p.Symbol));
    }

    [Fact]
    public async Task An_Input_Given_Under_Its_Old_Symbol_Is_Stored_Under_The_Current_One_On_Create_And_On_Update()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var oldForm = new[]
        {
            CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-1", "A")),
            CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("DRG-1", "A")),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("SPEC-1")),
        };

        var created = await host.Service.CreateAsync("test.renamed", project, "Stress", oldForm);
        Assert.Equal(12, created.Inputs.Single(i => i.Symbol == "F_axial").Number);
        Assert.DoesNotContain(created.Inputs, i => i.Symbol == "F");
        Assert.Equal(60e6, created.Outputs.Single(o => o.Symbol == "sigma").Si!.Value, 3);

        var updated = await host.Service.UpdateInputsAsync(created.Id, [CalcInputValue.Quantity("F_old", 24, "kN", CalcProvenance.Drawing("DRG-1", "B")), .. oldForm.Where(i => i.Symbol != "F")]);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Equal(24, updated.Instance!.Inputs.Single(i => i.Symbol == "F_axial").Number);
        Assert.Equal(120e6, updated.Instance.Outputs.Single(o => o.Symbol == "sigma").Si!.Value, 3);
    }

    [Fact]
    public async Task An_Input_Can_Be_Bound_By_Its_Old_Symbol()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await host.Service.CreateAsync("test.force-source", project, "Load", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
        var stress = await host.Service.CreateAsync("test.renamed", project, "Stress", Renamed);

        var bound = await host.Service.BindAsync(stress.Id, "F", source.Id, "F_out");

        Assert.True(bound.Succeeded, bound.Reason);
        var link = Assert.Single(bound.Instance!.Links);
        Assert.Equal("F_axial", link.Input.Symbol);
        Assert.Equal(10, link.Input.Number);
    }

    [Fact]
    public async Task A_Draft_Made_On_An_Older_Version_Carries_A_Renamed_Input_Instead_Of_Dropping_It()
    {
        var (host, project, stress) = await StressAsync();
        var older = stress with
        {
            TemplateReference = "test.renamed@1.0.0",
            Inputs = [CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Manual), CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Manual), CalcInputValue.Quantity("gone", 1, "mm", CalcProvenance.Manual)],
        };

        var plan = host.Service.PlanDraftUpgrade(older);

        Assert.NotNull(plan);
        Assert.Equal(["gone"], plan!.Dropped);
        Assert.Equal(12, plan.Inputs.Find("F_axial")!.Number);
        Assert.Null(plan.Inputs.Find("F"));
        Assert.Equal(200, plan.Inputs.Find("A")!.Number);
    }

    [Fact]
    public async Task A_Pack_Template_Written_With_Old_Symbols_Validates_And_Instantiates()
    {
        var oldForm = new CalcPackTemplate("test.old-form", "Old form", "A binding written before the renames.",
            [new CalcPackStep("src", "test.force-source", "Load"), new CalcPackStep("chk", "test.renamed", "Stress")],
            [new CalcPackBinding("src", "F_out", "chk", "F")]);
        var host = Host([oldForm]);
        var project = await host.ProjectAsync();
        Assert.Null(host.Service.ValidatePackTemplate(oldForm));

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(oldForm, project, "Old-form pack",
            new Dictionary<string, IReadOnlyList<CalcInputValue>> { ["src"] = [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))], ["chk"] = Renamed.Where(i => i.Symbol != "F_axial").ToList() });

        Assert.Null(refusal);
        var link = Assert.Single(made!.Instances["chk"].Links);
        Assert.Equal("F_axial", link.Input.Symbol);
        Assert.Equal(10, link.Input.Number);
    }

    [Theory]
    [InlineData("sigma")]
    [InlineData("sigma_old")]
    [InlineData("stress.axial")]
    public async Task A_Pack_Binding_Can_Name_The_Output_By_Any_Of_Its_Names(string output)
    {
        var chain = new CalcPackTemplate("test.names", "Names", "A binding by each name of one port.",
            [new CalcPackStep("up", "test.renamed", "Stress", Example: Renamed), new CalcPackStep("down", "test.direct-stress", "Check", Example: LibraryTestHost.ProvenancedInputs())],
            [new CalcPackBinding("up", output, "down", "sigma_allow")]);
        var host = Host([chain]);
        Assert.Null(host.Service.ValidatePackTemplate(chain));

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(chain, await host.ProjectAsync(), "Names pack");

        Assert.Null(refusal);
        Assert.Equal(60, made!.Instances["down"].Inputs.Single(i => i.Symbol == "sigma_allow").Number!.Value, 9);
    }

    [Fact]
    public async Task A_Pack_Example_Written_With_An_Old_Input_Symbol_Opens_On_The_Current_One()
    {
        var chain = new CalcPackTemplate("test.example", "Example", "An example written before the rename.",
            [new CalcPackStep("up", "test.renamed", "Stress", Example: [CalcInputValue.Quantity("F", 30, "kN"), CalcInputValue.Quantity("A", 200, "mm^2")])],
            []);
        var host = Host([chain]);
        Assert.Null(host.Service.ValidatePackTemplate(chain));

        var example = host.Service.ExampleInputs(chain, chain.Steps[0])!;
        Assert.Equal(30, example.Single(i => i.Symbol == "F_axial").Number);
        Assert.DoesNotContain(example, i => i.Symbol == "F");
    }

    [Fact]
    public void A_Template_Whose_Alias_Names_Another_Input_Or_Port_Is_Refused_When_It_Is_Registered()
    {
        var clash = RenamedWith(inputs: [new CalcInputSpec("F_axial", "Load", CalcInputKind.Quantity, "kN", Default: 1, Aliases: ["A"]), new CalcInputSpec("A", "Area", CalcInputKind.Quantity, "mm^2", Default: 1)]);
        Assert.Contains("alias 'A'", Assert.Throws<ArgumentException>(() => new CalcCatalogue([clash])).Message, StringComparison.Ordinal);

        var twice = RenamedWith(inputs: [new CalcInputSpec("F_axial", "Load", CalcInputKind.Quantity, "kN", Default: 1, Aliases: ["F"]), new CalcInputSpec("A", "Area", CalcInputKind.Quantity, "mm^2", Default: 1, Aliases: ["F"])]);
        Assert.Contains("alias 'F'", Assert.Throws<ArgumentException>(() => new CalcCatalogue([twice])).Message, StringComparison.Ordinal);

        var port = RenamedWith(ports: [new CalcOutputPort("sigma", "MPa", "Stress", "stress.axial", ["A_req"]), new CalcOutputPort("A_req", "mm^2", "Area", "stress.area_required")]);
        Assert.Contains("port name 'A_req'", Assert.Throws<ArgumentException>(() => new CalcCatalogue([port])).Message, StringComparison.Ordinal);

        Assert.NotNull(new CalcCatalogue([new RenamedCalculator()]).Find("test.renamed"));
    }

    [Fact]
    public void A_Port_Name_Or_Alias_Equal_To_The_Symbol_Of_Another_Output_Of_The_Template_Is_Refused_When_It_Is_Registered()
    {
        // 'margin' and 'u' are outputs of the template that are not ports: a link by that symbol would name two outputs.
        var alias = RenamedWith(ports: [new CalcOutputPort("sigma", "MPa", "Stress", "stress.axial", ["sigma_old", "margin"]), new CalcOutputPort("A_req", "mm^2", "Area", "stress.area_required")]);
        var refused = Assert.Throws<ArgumentException>(() => new CalcCatalogue([alias]));
        Assert.Contains("'margin'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("another output", refused.Message, StringComparison.Ordinal);

        var name = RenamedWith(ports: [new CalcOutputPort("sigma", "MPa", "Stress", "u"), new CalcOutputPort("A_req", "mm^2", "Area", "stress.area_required")]);
        Assert.Contains("'u'", Assert.Throws<ArgumentException>(() => new CalcCatalogue([name])).Message, StringComparison.Ordinal);

        // The symbol of a port itself, and an alias that names no output, are fine.
        Assert.NotNull(new CalcCatalogue([RenamedWith(ports: [new CalcOutputPort("sigma", "MPa", "Stress", "stress.axial", ["sigma_old", "sigma_before"]), new CalcOutputPort("A_req", "mm^2", "Area", "stress.area_required")])]).Find("test.renamed"));
    }

    /// <summary>
    /// C-2 verification F9: the port-alias check searched a run on the defaults and the symbols the known-answer cases expect, so an alias that is an output of another mode
    /// (here <c>y_b</c>, which only mode B outputs and no case expects) went through. It now also searches the outputs of a run of every known-answer case.
    /// </summary>
    [Fact]
    public void A_Port_Alias_Equal_To_An_Output_Of_Another_Mode_That_A_Known_Answer_Case_Runs_Is_Refused()
    {
        var refused = Assert.Throws<ArgumentException>(() => new CalcCatalogue([new ModalCalculator(alias: "y_b")]));
        Assert.Contains("'y_b'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("another output", refused.Message, StringComparison.Ordinal);

        // A name that no mode outputs is fine, and so is the same template with nothing aliased.
        Assert.NotNull(new CalcCatalogue([new ModalCalculator(alias: "x_old")]).Find("test.modal"));
        Assert.NotNull(new CalcCatalogue([new ModalCalculator(alias: null)]).Find("test.modal"));
    }

    [Fact]
    public void Every_Shipped_Calculator_Registers_With_The_Port_Alias_Check_Over_All_Its_Known_Answer_Cases()
    {
        var catalogue = new CalcCatalogue(ShippedCalculators.All);
        Assert.Equal(ShippedCalculators.All.Count, catalogue.Current().Count + ShippedCalculators.All.Count(c => catalogue.IsWithheld(c.Template.Reference)));
    }

    /// <summary>A template whose outputs depend on its mode: mode A gives <c>x</c> and mode B gives <c>x</c> and <c>y_b</c>; the port is <c>x</c>.</summary>
    private sealed class ModalCalculator(string? alias) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new(
            "test.modal", "1.0.0", CalcDiscipline.Structural, "Modal (test)", "Calculates a value in two modes.", "Value: x = 2.",
            [new CalcInputSpec("mode", "Mode", CalcInputKind.Choice, Choices: ["A", "B"], DefaultChoice: "A")],
            [], [], [],
            [
                new CalcTestCase("ka-b", "Mode B", new CalcReferenceSource("Hand calculation", null, "x = 2"), [CalcInputValue.Choice("mode", "B")], [new CalcExpectedValue("x", 2, "", 1e-9)]),
            ],
            Category: "Members",
            OutputPorts: [new CalcOutputPort("x", "", "Value", "stress.axial", alias is null ? null : [alias])]);

        public void Run(CalcRunContext c)
        {
            c.Assign("x", "2", null, "Value");
            c.Output("x", "Value");

            if (c.InputText("mode") == "B")
            {
                c.Assign("y_b", "3", null, "Second value");
                c.Output("y_b", "Second value");
            }
        }
    }

    [Fact]
    public async Task An_Input_Given_Under_Its_Old_And_Its_Current_Symbol_With_Different_Values_Is_Refused_On_Create_And_On_Update()
    {
        var (host, project, stress) = await StressAsync();
        var conflicting = Renamed.Append(CalcInputValue.Quantity("F", 99, "kN", CalcProvenance.Drawing("DRG-1", "A"))).ToList();

        var created = await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync("test.renamed", project, "Twice", conflicting));
        Assert.StartsWith("F_axial:", created.Message, StringComparison.Ordinal);

        var updated = await host.Service.UpdateInputsAsync(stress.Id, conflicting);
        Assert.False(updated.Succeeded);
        Assert.StartsWith("F_axial:", updated.Reason, StringComparison.Ordinal);

        // The same value under both is one input.
        var same = Renamed.Append(CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-1", "A"))).ToList();
        var ok = await host.Service.UpdateInputsAsync(stress.Id, same);
        Assert.True(ok.Succeeded, ok.Reason);
        Assert.Single(ok.Instance!.Inputs, i => i.Symbol == "F_axial");
        Assert.DoesNotContain(ok.Instance.Inputs, i => i.Symbol == "F");
    }

    private static ICalcCalculator RenamedWith(IReadOnlyList<CalcInputSpec>? inputs = null, IReadOnlyList<CalcOutputPort>? ports = null)
    {
        var original = new RenamedCalculator();
        return new Replaced(original.Template with { Inputs = inputs ?? original.Template.Inputs, OutputPorts = ports ?? original.Template.OutputPorts }, original);
    }

    private sealed class Replaced(CalcTemplate template, ICalcCalculator inner) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = template;

        public void Run(CalcRunContext context) => inner.Run(context);
    }
}

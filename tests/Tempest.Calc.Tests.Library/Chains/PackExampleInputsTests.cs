using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>Pack templates carry example step inputs, so a chain can be created as it stands (except the motor circuit, whose cable tables no build ships).</summary>
public sealed class PackExampleInputsTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All],
        packTemplates: CalcPackTemplates.All);

    public static TheoryData<string> Templates => ["pack.drive-train", "pack.wet-heating", "pack.cooling", "pack.steel-support", "pack.envelope-moisture", "pack.cold-pipe", "pack.pipework-supports", "pack.electronics-enclosure"];

    private static CalcPackTemplate Template(string id) => CalcPackTemplates.All.Single(t => t.Id == id);

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task A_Chain_With_Example_Inputs_Is_Created_As_It_Stands_With_No_Blocked_Step(string id)
    {
        var host = Host();
        var template = Template(id);
        Assert.Null(host.Service.ValidatePackTemplate(template));
        Assert.All(template.Steps, s => Assert.NotEmpty(s.Example ?? []));

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, await host.ProjectAsync(), "Example", null);

        Assert.True(refusal is null, refusal);
        Assert.Equal(template.Steps.Count, made!.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.False(i.Blocks.Count > 0, $"{i.Name}: {string.Join("; ", i.Blocks.Select(b => b.Message))}"));

        // Every input is an example the user must replace, or a link: nothing reads as a project figure.
        Assert.All(made.Instances.Values, i => Assert.All(i.Inputs, input =>
        {
            Assert.NotNull(input.Provenance);
            Assert.True(input.Provenance!.Kind is CalcProvenanceKind.Link || (input.Provenance.Kind == CalcProvenanceKind.Assumption && input.Provenance.Reference == CalcInstanceService.ExampleBasis), $"{i.Name}.{input.Symbol}: {input.Provenance}");
        }));
        Assert.Equal("assumption: example input — replace with your own", CalcProvenance.Assumed(CalcInstanceService.ExampleBasis).ToString());
    }

    [Fact]
    public async Task The_Motor_Circuit_Without_Tables_Is_Refused_At_The_Cable_Step_And_Says_What_To_Supply()
    {
        var host = Host();
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, await host.ProjectAsync(), "Example", null);

        Assert.Null(made);
        Assert.StartsWith("Step 'e4' is blocked", refusal);
        Assert.Contains("ampacity:", refusal, StringComparison.Ordinal);
        Assert.Contains("none ship with the program", refusal, StringComparison.Ordinal);
        Assert.Contains("your own cable-capacity (ampacity)", refusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync(null));

        // Every step but E4 opens on its example, and E4's example holds no table.
        var e4 = host.Service.ExampleInputs(CalcPackTemplates.MotorCircuit, CalcPackTemplates.MotorCircuit.Steps.Single(s => s.Key == "e4"))!;
        Assert.DoesNotContain(e4, v => v.Symbol is "ampacity" or "ratings");
        Assert.All(CalcPackTemplates.MotorCircuit.Steps, s => Assert.NotEmpty(s.Example ?? []));
    }

    [Fact]
    public async Task The_Motor_Circuit_Is_Created_When_Only_The_Cable_Step_Is_Given_Its_Tables()
    {
        var host = Host();
        var tables = ChainInputs.MotorCircuit(host).Where(kv => kv.Key == "e4").ToDictionary(kv => kv.Key, kv => kv.Value);
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, await host.ProjectAsync(), "Example", tables);

        Assert.True(refusal is null, refusal);
        Assert.All(made!.Instances.Values, i => Assert.False(i.Blocks.Count > 0, i.Name));
    }

    [Fact]
    public async Task Stored_Pack_Templates_Stay_Backward_Compatible()
    {
        var host = Host();

        // A template saved before steps carried examples reads back with none.
        const string old = """{"Id":"old.chain","Title":"Old","Description":"d","Steps":[{"Key":"a","TemplateId":"test.direct-stress","Name":"A"}],"Bindings":[]}""";
        await host.Backing.ExecuteInTransactionAsync((tx, token) => tx.WriteAsync(CalcInstanceStore.PackTemplateCollection, "old.chain", old, token));
        var stored = (await host.Store.ListPackTemplatesAsync()).Single(t => t.Id == "old.chain");
        Assert.Null(stored.Steps[0].Example);
        Assert.Null(stored.Steps[0].Hint);

        // A step without an example writes exactly as before (nulls are not written).
        var json = CalcInstanceStore.Serialise(new CalcPackTemplate("new.chain", "New", "d", [new CalcPackStep("a", "test.direct-stress", "A")], []));
        Assert.DoesNotContain("Example", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Omit", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint", json, StringComparison.Ordinal);

        // A step with one survives a round trip.
        var withExample = new CalcPackTemplate("ex.chain", "Ex", "d", [new CalcPackStep("a", "test.direct-stress", "A", [CalcInputValue.Quantity("F", 12, "kN")], ["A"], "Say this.")], []);
        await host.Store.SavePackTemplateAsync(withExample);
        var back = (await host.Store.ListPackTemplatesAsync()).Single(t => t.Id == "ex.chain");
        Assert.Equal(12, back.Steps[0].Example!.Single().Number);
        Assert.Equal(["A"], back.Steps[0].Omit);
        Assert.Equal("Say this.", back.Steps[0].Hint);
    }

    [Fact]
    public void An_Example_For_An_Input_The_Calculator_Does_Not_Have_Is_Refused_When_The_Template_Is_Validated()
    {
        var host = Host();
        var bad = new CalcPackTemplate("bad.chain", "Bad", "d", [new CalcPackStep("a", "test.direct-stress", "A", [CalcInputValue.Quantity("nope", 1, "kN")])], []);
        var reason = host.Service.ValidatePackTemplate(bad);
        Assert.NotNull(reason);
        Assert.Contains("'nope'", reason, StringComparison.Ordinal);
    }
}

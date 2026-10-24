using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7 wet heating: B4 (two rooms) → B6 → B1 → E1 from the built-in pack template. Each room's heat loss enters a row of
/// B6's rooms table through a table-cell link; B6's heat and flow-to-return difference give B1 its design flow; B1's pump shaft torque
/// is E1's load torque.
/// </summary>
public sealed class WetHeatingChainTests
{
    private static readonly CalcProvenance Spec = CalcProvenance.Specification("SPEC-HTG-01 §3");

    private static LibraryTestHost Host() => new(
        calculators: [.. BuildingServicesCalculators.All, .. ElectricalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static double Si(CalcInstance instance, string symbol) => instance.Outputs.Single(o => o.Symbol == symbol).Si!.Value;

    private static double InputSi(CalcInstance instance, string symbol)
    {
        var input = instance.Inputs.Single(i => i.Symbol == symbol);
        return Tempest.Calc.Engine.Units.UnitCatalog.Parse(input.Unit ?? string.Empty).ToSi(input.Number!.Value);
    }

    private static double CellSi(CalcInstance instance, string table, int row, string column)
    {
        var cell = instance.Inputs.Single(i => i.Symbol == table).Rows![row].Get(column)!;
        return Tempest.Calc.Engine.Units.UnitCatalog.Parse(cell.Unit ?? string.Empty).ToSi(cell.Number!.Value);
    }

    private static List<CalcInputValue> From(CalcTemplate template, Func<CalcInputValue, bool>? keep = null, params CalcInputValue[] overrides)
    {
        var set = new CalcInputSet(CalcTemplateHasher.NewSheetInputs(template).Values.Where(v => keep?.Invoke(v) ?? true));

        foreach (var value in overrides)
            set.Set(value);

        return [.. set.Values.Select(v => v with { Provenance = v.Provenance ?? Spec })];
    }

    /// <summary>The inputs of every step: two rooms from the B4 worked cases, B6 given those two rows, a pipe run, a pump speed and a rotary load.</summary>
    private static Dictionary<string, IReadOnlyList<CalcInputValue>> Steps()
    {
        var b4 = new RoomHeatLossCalculator().Template;
        var b6 = new SealedSystemCalculator().Template;
        var b1 = new PipeFlowCalculator().Template;
        var e1 = new MotorSizingCalculator().Template;

        return new(StringComparer.Ordinal)
        {
            ["b4a"] = [.. b4.TestCases[0].Inputs.Select(v => v with { Provenance = Spec })],
            ["b4b"] = [.. b4.TestCases[1].Inputs.Select(v => v with { Provenance = Spec })],

            // B6 is given its two rooms: instantiating never creates rows. Φ is a placeholder the links replace.
            ["b6"] = From(b6, v => v.Symbol != "rooms", SealedSystemCalculator.Rooms(("Room A", 1, 0, 4), ("Room B", 1, 0, 4))),

            // B1: heat load and ΔT come from B6, so no design flow is given; the pump runs at 2800 rpm.
            ["b1"] = From(b1, v => v.Symbol is not ("Q" or "Phi_load" or "dT_load"),
                CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidWater),
                CalcInputValue.Quantity("T", 70, "degC"),
                PipeFlowCalculator.Segments(("primary flow and return", 40, 28, 0.045, 12, 0, 3, "discharge")),
                CalcInputValue.Quantity("n_pump", 2800, "rpm"),
                CalcInputValue.Quantity("cp_load", 4186, "J/(kg*K)")),

            // E1: a circulator on a direct drive; B1's shaft torque is the constant load torque.
            ["e1"] = From(e1, null,
                CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechDirectRotary),
                CalcInputValue.Quantity("J_L", 0.002, "kg*m^2"),
                CalcInputValue.Quantity("J_mech", 0, "kg*m^2"),
                CalcInputValue.Of("s", 20),
                CalcInputValue.Quantity("t_move", 2, "s"),
                CalcInputValue.Quantity("t_dwell", 10, "s")),
        };
    }

    private static async Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host)
    {
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.WetHeating));
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.WetHeating, await host.ProjectAsync(), "Plant room heating", Steps());
        Assert.True(refusal is null, refusal);
        return made!;
    }

    private static async Task<CalcInstance> Fresh(LibraryTestHost host, CalcPackInstantiation made, string key) => (await host.Store.FindInstanceAsync(made.Instances[key].Id))!;

    [Fact]
    public async Task Room_Heat_Reaches_The_Sealed_System_The_Pipework_Flow_And_The_Motor_Load()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (b4a, b4b, b6, b1, e1) = (made.Instances["b4a"], made.Instances["b4b"], made.Instances["b6"], made.Instances["b1"], made.Instances["e1"]);

        // Each room's heat loss sits in its own row of B6's rooms table, as a cell link.
        Assert.Equal(Si(b4a, "Phi_HL"), CellSi(b6, "rooms", 0, "Phi"), 9);
        Assert.Equal(Si(b4b, "Phi_HL"), CellSi(b6, "rooms", 1, "Phi"), 9);
        Assert.Equal(350.6732, Si(b4a, "Phi_HL"), 3);
        Assert.Equal(243.8716, Si(b4b, "Phi_HL"), 3);
        Assert.Equal([(0, new CalcLink(b4a.Id, "Phi_HL", "A")), (1, new CalcLink(b4b.Id, "Phi_HL", "A"))], b6.CellLinks.Select(l => (l.Row, l.Link)));
        Assert.All(b6.CellLinks, l => Assert.Equal(("rooms", "Phi"), (l.Symbol, l.Column)));
        Assert.Empty(b6.Links);
        Assert.Equal(Si(b4a, "Phi_HL") + Si(b4b, "Phi_HL"), Si(b6, "Phi_tot"), 9);

        // B6 → B1: the heat and the flow-to-return difference (75 − 65 = 10 K), and the flow they give.
        Assert.Equal(Si(b6, "Phi_tot"), InputSi(b1, "Phi_load"), 9);
        Assert.Equal(10, Si(b6, "dT_fr"), 9);
        Assert.Equal(Si(b6, "dT_fr"), InputSi(b1, "dT_load"), 9);
        var rho = Water.Density(70);
        var flow = Si(b6, "Phi_tot") / (rho * 4186 * 10);
        Assert.Equal(flow, Si(b1, "Q_design"), 12);

        // B1 → E1: the pump shaft torque is P_hyd / η / ω at 2800 rpm.
        var omega = 2800 * 2 * Math.PI / 60;
        Assert.Equal(Si(b1, "P_hyd") / 0.7 / omega, Si(b1, "T_shaft"), 12);
        Assert.Equal(Si(b1, "T_shaft"), InputSi(e1, "T_L"), 12);
        Assert.True(Si(b1, "T_shaft") > 0);

        Assert.Equal(5, made.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.False(i.Blocks.Count > 0, $"{i.Name}: {string.Join("; ", i.Blocks.Select(b => b.Message))}"));

        foreach (var instance in made.Instances.Values)
            Assert.False((await host.Service.StalenessAsync(instance)).IsStale, instance.Name);
    }

    [Fact]
    public async Task A_Changed_Room_Makes_B6_B1_And_E1_Stale_And_Blocks_The_Pack_Until_Each_Is_Updated()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var b4a = made.Instances["b4a"];
        var before = Si(made.Instances["b1"], "T_shaft");

        // A bigger room: double the volume, which doubles its ventilation loss.
        var bigger = await host.Service.UpdateInputsAsync(b4a.Id, [.. b4a.Inputs.Select(i => i.Symbol == "V" ? i with { Number = i.Number!.Value * 2 } : i)]);
        Assert.True(bigger.Succeeded, bigger.Reason);
        Assert.NotEqual(Si(b4a, "Phi_HL"), Si(bigger.Instance!, "Phi_HL"));

        var b6Stale = await host.Service.StalenessAsync(await Fresh(host, made, "b6"));
        Assert.True(b6Stale.IsStale);
        Assert.Contains(b6Stale.Reasons, r => r.Contains("'rooms[1].Phi'", StringComparison.Ordinal) && r.Contains("different 'Phi_HL'", StringComparison.Ordinal));

        foreach (var key in new[] { "b1", "e1" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // The pack cannot be checked, and a step cannot be issued, while any of it is stale.
        var blocked = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(blocked.Succeeded);
        Assert.Contains("stale", blocked.Reason, StringComparison.Ordinal);

        // Updating in chain order clears the staleness one step at a time; the pack stays blocked until the last.
        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["b6"].Id)).Succeeded);
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b6"))).IsStale);
        Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, "b1"))).IsStale);
        Assert.False((await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["b1"].Id)).Succeeded);
        Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, "e1"))).IsStale);
        Assert.False((await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["e1"].Id)).Succeeded);

        var (b6, b1, e1) = (await Fresh(host, made, "b6"), await Fresh(host, made, "b1"), await Fresh(host, made, "e1"));
        Assert.Equal(Si(bigger.Instance!, "Phi_HL"), CellSi(b6, "rooms", 0, "Phi"), 9);
        Assert.Equal(Si(bigger.Instance!, "Phi_HL") + Si(made.Instances["b4b"], "Phi_HL"), Si(b6, "Phi_tot"), 9);
        Assert.Equal(Si(b6, "Phi_tot"), InputSi(b1, "Phi_load"), 9);
        Assert.Equal(Si(b1, "T_shaft"), InputSi(e1, "T_L"), 12);
        Assert.True(Si(b1, "T_shaft") > before);

        foreach (var key in made.Instances.Keys)
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be current");

        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK once re-pinned.", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // Full provenance on every input of every step (the links carry their own).
        foreach (var (key, instance) in made.Instances)
        {
            var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
            Assert.All(stored.Inputs, i => Assert.True(i.Provenance?.IsSufficientToIssue == true, $"{key}.{i.Symbol} lacks provenance"));
        }

        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "Checked against SPEC-HTG-01.", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var issued = await host.Service.IssuePackAsync(made.Pack.Id, "HTG-001", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var pack = (await host.Store.FindPackAsync(made.Pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, pack.Status);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(pack.EvidenceId!.Value))!;
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        var sheet = Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
        Assert.Contains("PACK INDEX", sheet, StringComparison.Ordinal);

        foreach (var step in CalcPackTemplates.WetHeating.Steps)
        {
            Assert.Contains(step.Name, sheet, StringComparison.Ordinal);
            Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(made.Instances[step.Key].Id))!.Status);
        }

        // The cell links are named on the issued sheet, row by row, and the top-level ones by input.
        Assert.Contains("Phi: link: Room A heat loss rev A · Phi_HL", sheet, StringComparison.Ordinal);
        Assert.Contains("Phi: link: Room B heat loss rev A · Phi_HL", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Sealed system rev A · Phi_tot", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Sealed system rev A · dT_fr", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Primary pipework and pump rev A · T_shaft", sheet, StringComparison.Ordinal);

        // Issued means immutable: a room cannot be changed afterwards.
        var frozen = await host.Service.UpdateInputsAsync(made.Instances["b4a"].Id, []);
        Assert.False(frozen.Succeeded);
    }

    [Fact]
    public async Task B6_Is_Refused_Rows_It_Was_Not_Given()
    {
        var host = Host();
        var steps = Steps();
        steps.Remove("b6");
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.WetHeating, await host.ProjectAsync(), "Plant room heating", steps);
        Assert.Null(made);
        Assert.StartsWith("Step 'b6'", refusal);
    }

    [Fact]
    public async Task The_Heat_Load_Flow_Replaces_A_Design_Flow_With_A_Notice_And_Needs_Its_Temperature_Difference()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var inputs = Steps()["b1"].Where(v => v.Symbol != "n_pump").ToList();

        // Φ alone: blocked with the reason.
        var alone = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework alone",
            [.. inputs, CalcInputValue.Quantity("Phi_load", 600, "W", Spec)]);
        Assert.Contains(alone.Blocks, b => b.Message.StartsWith("Phi_load:", StringComparison.Ordinal));

        // Neither a flow nor a heat load: blocked.
        var neither = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework neither", inputs);
        Assert.Contains(neither.Blocks, b => b.Message.StartsWith("Q:", StringComparison.Ordinal));

        // A flow as well as a heat load: the heat load wins and the sheet says so; 600 W at 10 K, 70 °C water.
        var both = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework both",
            [.. inputs, CalcInputValue.Quantity("Q", 20, "m^3/h", Spec), CalcInputValue.Quantity("Phi_load", 600, "W", Spec), CalcInputValue.Quantity("dT_load", 10, "K", Spec)]);
        Assert.Empty(both.Blocks);
        Assert.Contains(both.Warnings, w => w.RuleId == "heat-flow-q");
        Assert.Equal(600 / (Water.Density(70) * 4186 * 10), Si(both, "Q_design"), 12);
        Assert.DoesNotContain(both.Outputs, o => o.Symbol == "T_shaft");
    }
}

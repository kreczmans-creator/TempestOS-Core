using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

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

    private static Dictionary<string, IReadOnlyList<CalcInputValue>> Steps(LibraryTestHost host) => ChainInputs.WetHeating(host);

    private static async Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host)
    {
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.WetHeating));
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.WetHeating, await host.ProjectAsync(), "Plant room heating", Steps(host));
        Assert.True(refusal is null, refusal);
        return made!;
    }

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

        // B6 → B1: the mean water temperature is the fluid temperature the flow is worked out at.
        Assert.Equal(70, Si(b6, "t_m") - 273.15, 9);
        Assert.Equal(Si(b6, "t_m"), InputSi(b1, "T"), 9);
        Assert.Contains(b1.Inputs, i => i.Symbol == "T" && CalcLink.From(i.Provenance) is { OutputSymbol: "t_m" });
        Assert.DoesNotContain(b1.Warnings, w => w.RuleId == "heat-t");

        // B1 → E1: the pump shaft torque is P_hyd / η / ω at 2800 rpm, and its speed is E1's load speed.
        var omega = 2800 * 2 * Math.PI / 60;
        Assert.Equal(Si(b1, "P_hyd") / 0.7 / omega, Si(b1, "T_shaft"), 12);
        Assert.Equal(Si(b1, "T_shaft"), InputSi(e1, "T_L"), 12);
        Assert.Equal(Si(b1, "n_shaft"), InputSi(e1, "n_L"), 9);
        Assert.True(Si(b1, "T_shaft") > 0);

        // E1 sizes the motor for continuous running: the load torque (through η = 0.9), the speed and the run-up, not a move cycle.
        Assert.Equal(Si(b1, "T_shaft") / 0.9, Si(e1, "T_load"), 12);
        Assert.Equal(Si(e1, "T_load"), Si(e1, "T_rms"), 12);
        Assert.Equal(Si(e1, "T_load") * omega, Si(e1, "P_cont"), 9);
        Assert.Equal(2800, Si(e1, "n_pk") * 60 / (2 * Math.PI), 6);
        Assert.Equal(Si(e1, "T_acc") + Si(e1, "T_load"), Si(e1, "T_pk"), 12);
        Assert.Equal(3, e1.Checks.Count);
        Assert.All(e1.Checks, check => Assert.True(check.Passed, check.Name));
        Assert.Equal(1.25 * Si(e1, "T_rms") / 2.0, Si(e1, "u_rms"), 12);
        Assert.Equal(1.25 * Si(e1, "T_pk") / 6.0, Si(e1, "u_peak"), 12);
        // n_max is not stated for the pump motor, so the sheet opens on the template's declared 6000 rpm (a known-answer case's 10000 rpm is test data, not a default; Green G-A-02).
        Assert.Equal(2800.0 / 6000, Si(e1, "u_speed"), 9);
        Assert.Equal(Si(e1, "u_rms"), e1.Checks.Single(check => check.Name.StartsWith("RMS torque", StringComparison.Ordinal)).Utilisation, 12);
        Assert.Equal(Si(e1, "u_peak"), e1.Checks.Single(check => check.Name.StartsWith("Peak torque", StringComparison.Ordinal)).Utilisation, 12);
        Assert.Equal(Si(e1, "u_speed"), e1.Checks.Single(check => check.Name.StartsWith("Speed", StringComparison.Ordinal)).Utilisation, 12);
        Assert.Contains(e1.Warnings, w => w.RuleId == "continuous-ignored");

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
        var notIssued = await host.Service.IssuePackAsync(made.Pack.Id, "HTG-STALE", "Client Co");
        Assert.False(notIssued.Succeeded);
        Assert.Contains("check it before issuing", notIssued.Reason, StringComparison.Ordinal);

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
        var steps = Steps(host);
        steps["b6"] = [.. steps["b6"].Where(v => v.Symbol != "rooms")];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.WetHeating, await host.ProjectAsync(), "Plant room heating", steps);
        Assert.Null(made);
        Assert.StartsWith("Step 'b6'", refusal);
    }

    [Fact]
    public async Task The_Heat_Load_Flow_Replaces_A_Design_Flow_With_A_Notice_And_Needs_Its_Temperature_Difference()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var inputs = Steps(host)["b1"].Where(v => v.Symbol != "n_pump").ToList();
        var temperature = CalcInputValue.Quantity("T", 70, "degC", Spec);

        // Φ alone: blocked with the reason.
        var alone = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework alone",
            [.. inputs, temperature, CalcInputValue.Quantity("Phi_load", 600, "W", Spec)]);
        Assert.Contains(alone.Blocks, b => b.Message.StartsWith("Phi_load:", StringComparison.Ordinal));

        // Neither a flow nor a heat load: blocked.
        var neither = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework neither", [.. inputs, temperature]);
        Assert.Contains(neither.Blocks, b => b.Message.StartsWith("Q:", StringComparison.Ordinal));

        // A flow as well as a heat load: the heat load wins and the sheet says so; 600 W at 10 K, 70 °C water, c_p assumed.
        var both = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework both",
            [.. inputs, temperature, CalcInputValue.Quantity("Q", 20, "m^3/h", Spec), CalcInputValue.Quantity("Phi_load", 600, "W", Spec), CalcInputValue.Quantity("dT_load", 10, "K", Spec)]);
        Assert.Empty(both.Blocks);
        Assert.Contains(both.Warnings, w => w.RuleId == "heat-flow-q");
        Assert.DoesNotContain(both.Warnings, w => w.RuleId == "heat-t");
        Assert.Equal(600 / (Water.Density(70) * 4186 * 10), Si(both, "Q_design"), 12);
        Assert.DoesNotContain(both.Outputs, o => o.Symbol == "T_shaft");
        var sheet = (await host.Service.RunAsync(both.Id))!.SheetText;
        Assert.Contains("c_p of water 4186 J/(kg·K) (assumed)", sheet, StringComparison.Ordinal);

        // No temperature with a heat load: a notice, because 20 °C is not a heating loop's temperature.
        var noT = await host.Service.CreateAsync("bs.pipe-pump", project, "Pipework no T",
            [.. inputs, CalcInputValue.Quantity("Phi_load", 600, "W", Spec), CalcInputValue.Quantity("dT_load", 10, "K", Spec)]);
        Assert.Empty(noT.Blocks);
        Assert.Contains(noT.Warnings, w => w.RuleId == "heat-t" && w.Message.StartsWith("T:", StringComparison.Ordinal));
    }
}

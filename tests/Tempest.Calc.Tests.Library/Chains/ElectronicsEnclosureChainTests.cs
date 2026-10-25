using System.Text.RegularExpressions;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 10: the PCB power trace E7 → the board's load budget E6 → the sealed enclosure's heat E2 → the lid latch M8 and the lid plate S9, from the built-in pack template.
/// The trace's I²R loss is a row of the load table (a table-cell link, W into kW); the load budget's heat is the enclosure's additional load; the enclosure's internal temperature is the latch's temperature of use;
/// and the enclosure, sealed at a stated temperature, puts p0·(T_int/T_seal − 1) across its walls, which is the pressure on the lid plate. The plastic's and the alloy's values and the trace's board ambient are typed for the
/// enclosure's limit (60 °C), and the enclosure step's own check guards them at run time. Every expected number was recomputed in python3 (IPC-2221 legacy fit and the resistivity relation; the load-budget heat sum; the
/// effective-surface relation; the constant-volume gas relation; the cantilever beam relations; the Navier series).
/// </summary>
public sealed class ElectronicsEnclosureChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.ElectronicsEnclosure, "Controller box 7", ChainInputs.ElectronicsEnclosure(host));

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.ElectronicsEnclosure));
        var made = await InstantiateAsync(host);
        var (e7, e6, e2, m8, s9) = (made.Instances["e7"], made.Instances["e6"], made.Instances["e2"], made.Instances["m8"], made.Instances["s9"]);
        Assert.Equal(5, made.Pack.InstanceIds.Count);

        // E7: A = [8/(0.048·30^0.44)]^(1/0.725) = 147.285 mil², 2 oz = 2.756 mil, minimum width 1.35742 mm against 2 mm drawn (u 0.6787); R at 90 °C, ρ = 0.01724 (1 + 0.00393 × 70) = 0.0219827 Ω·mm²/m:
        // R = ρ·L/(w·t) = 23.5521 mΩ, 0.188417 V, P = 64 × R = 1.507335 W (python3).
        Assert.Equal(147.28522041733675 * 25.4e-6 * 25.4e-6, Si(e7, "A_req"), 15);
        Assert.Equal(1.3574182142962097e-3, Si(e7, "w_min"), 12);
        Assert.Equal(0.6787091071481048, Si(e7, "u_w"), 12);
        Assert.Equal(0.023552111070477583, Si(e7, "trace_R"), 12);
        Assert.Equal(0.18841688856382066, Si(e7, "vdrop"), 12);
        Assert.Equal(1.5073351085105653, Si(e7, "trace_Ploss"), 12);
        Assert.Equal(8 * 8 * Si(e7, "trace_R"), Si(e7, "trace_Ploss"), 12);
        Assert.All(e7.Checks, c => Assert.True(c.Passed, c.Name));

        // E7 → E6, a table-cell link: the loss (W) is row 0, column P (kW) of the load table, the only link of E6.
        Assert.Equal(Si(e7, "trace_Ploss"), CellSi(e6, "loads", 0, "P"), 9);
        Assert.Equal(1.5073351085105653e-3, e6.Inputs.Single(i => i.Symbol == "loads").Rows![0].Get("P")!.Number!.Value, 12);
        Assert.Equal("kW", e6.Inputs.Single(i => i.Symbol == "loads").Rows![0].Get("P")!.Unit);
        Assert.Equal([(0, new CalcLink(e7.Id, "trace_Ploss", "A"))], e6.CellLinks.Select(l => (l.Row, l.Link)));
        Assert.All(e6.CellLinks, l => Assert.Equal(("loads", "P"), (l.Symbol, l.Column)));
        Assert.Empty(e6.Links);

        // E6: the heat released inside, Φ = Σ h·P·d·k = 1.507335 + 25 + (1 − 0.96) × 200 + 12 + 26.5 W = 73.007335 W (the trace's whole loss, the drivers' and display's whole input, the power stage's loss, the supply's loss).
        Assert.Equal(1.5073351085105653 + 25 + 0.04 * 200 + 12 + 26.5, Si(e6, "Phi"), 9);
        Assert.Equal(73.00733510851057, Si(e6, "Phi"), 9);

        // E6 → E2, kW into W: the board's heat is the enclosure's additional load.
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(73.00733510851057, e2.Inputs.Single(i => i.Symbol == "P_add").Number!.Value, 9);
        Assert.Equal("W", e2.Inputs.Single(i => i.Symbol == "P_add").Unit);

        // E2: 500 × 600 × 200 against a wall: A_eff = top 0.1 + front 0.3 + two sides 0.24 = 0.64 m²; P = 4 + 73.007335 = 77.007335 W; ΔT = P/(5.5 × 0.64) = 21.8771 K; 56.8771 °C inside, 60 °C limit.
        Assert.Equal(0.64, Si(e2, "A_eff"), 12);
        Assert.Equal(77.00733510851057, Si(e2, "P_total"), 9);
        Assert.Equal(21.87708383764505, Si(e2, "dT_nat"), 9);
        Assert.Equal(56.877083837645046 + 273.15, Si(e2, "T_int"), 9);
        Assert.Equal(88, Si(e2, "P_nat_max"), 9);
        Assert.All(e2.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(e2.Warnings, w => w.RuleId == "cooling-unit");

        // E2, sealed at 25 °C with no breather: Δp = 101.325 kPa × (T_int/T_seal − 1) = 101.325 × (330.027084/298.15 − 1) = 10.83329 kPa.
        Assert.Equal(101325 * ((56.877083837645046 + 273.15) / 298.15 - 1), Si(e2, "dp_seal"), 6);
        Assert.Equal(10833.29035669759, Si(e2, "dp_seal"), 6);

        // E2 → M8, degC to degC: the enclosure's internal temperature is the latch's temperature of use.
        Assert.Equal(Si(e2, "T_int"), InputSi(m8, "T_use"), 9);
        Assert.Equal(56.877083837645046, m8.Inputs.Single(i => i.Symbol == "T_use").Number!.Value, 9);
        Assert.Equal("degC", m8.Inputs.Single(i => i.Symbol == "T_use").Unit);

        // M8: ε = y·h/((2/3)·l²) = 1.6667 %, P = b·h²·Es·ε/(6·l) = 4.1481 N, u = 0.5556; W = 4.4018 N; W′ = 71.925 N (α′ 70°, below the 73.3° that would lock).
        Assert.Equal(0.016666666666666666, Si(m8, "eps"), 12);
        Assert.Equal(4.148148148148149, Si(m8, "P"), 9);
        Assert.Equal(0.5555555555555556, Si(m8, "strain_util"), 12);
        Assert.Equal(4.401791556421135, Si(m8, "assembly_force"), 9);
        Assert.Equal(71.92546559978396, Si(m8, "retention_force"), 9);
        Assert.Equal(Math.Atan(1 / 0.3), Si(m8, "alpha_lock"), 12);
        Assert.All(m8.Checks, c => Assert.True(c.Passed, c.Name));

        // E2 → S9, kPa into kPa: the sealed enclosure's pressure difference is the uniform pressure on the lid.
        Assert.Equal(Si(e2, "dp_seal"), InputSi(s9, "q"), 6);
        Assert.Equal(10.833290356697589, s9.Inputs.Single(i => i.Symbol == "q").Number!.Value, 9);
        Assert.Equal("kPa", s9.Inputs.Single(i => i.Symbol == "q").Unit);

        // S9: 500 × 200 × 4 mm aluminium, E 69 GPa, ν 0.33: D = 412.973 N·m; w_max = 0.482501 mm (w/t 0.121; limit span/250 = 0.8 mm, u 0.603); σ_vM 16.0247 MPa (u 0.134 of 120); f₁₁ = 281.69 Hz (Navier series, python3).
        Assert.Equal(412.9727303332959, Si(s9, "D"), 8);
        Assert.Equal(4.8250074151890354e-4, Si(s9, "w_max"), 1e-9);
        Assert.Equal(16024735.25928269, Si(s9, "sigma_max"), 20.0);
        Assert.Equal(281.6869222669022, Si(s9, "f1"), 1e-6);
        Assert.All(s9.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.Empty(s9.CellLinks);
        Assert.Equal(["q"], s9.Links.Select(l => l.Input.Symbol));
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.ElectronicsEnclosure;

        Assert.Equal(["e7", "e6", "e2", "m8", "s9"], template.Steps.Select(s => s.Key));
        Assert.Equal(["elec.pcb-trace", "elec.load-budget", "elec.enclosure-heat", "mech.snap-fit", "struct.plate-bending"], template.Steps.Select(s => s.TemplateId));
        Assert.Equal(
            [("e7", "trace_Ploss", "e6", "loads"), ("e6", "Phi", "e2", "P_add"), ("e2", "T_int", "m8", "T_use"), ("e2", "dp_seal", "s9", "q")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal((0, "P"), (template.Bindings[0].Row, template.Bindings[0].Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the plastic's and the alloy's values and the trace's board ambient are typed.
        Assert.Equal(3, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(1, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.DoesNotContain(template.Bindings, b => b.ToStep == "e7");
    }

    [Fact]
    public async Task The_Typed_Values_Carry_The_Temperature_They_Are_For_And_The_Enclosure_Steps_Own_Check_Guards_It_At_Run_Time()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e7, e2, m8, s9) = (made.Instances["e7"], made.Instances["e2"], made.Instances["m8"], made.Instances["s9"]);
        var limit = e2.Inputs.Single(i => i.Symbol == "T_max").Number!.Value;

        // Exactly the material values carry "at NN C", and NN is the enclosure's limit; the geometry inputs carry no temperature.
        static double TemperatureOf(CalcInstance i, string symbol) => double.Parse(Regex.Match(i.Inputs.Single(x => x.Symbol == symbol).Provenance!.Reference!, @"at (\d+) C").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        foreach (var (instance, symbol) in new[] { (m8, "Es"), (m8, "eps_perm_once"), (s9, "E"), (s9, "nu"), (s9, "rho"), (s9, "f_y") })
        {
            Assert.Equal(limit, TemperatureOf(instance, symbol), 9);
            Assert.False(instance.Inputs.Single(i => i.Symbol == symbol).Provenance is { Kind: CalcProvenanceKind.Link });
        }

        foreach (var symbol in new[] { "l", "h", "b", "y" })
            Assert.DoesNotMatch(@"at \d+ C", m8.Inputs.Single(i => i.Symbol == symbol).Provenance!.Reference);

        Assert.Equal(ChainInputs.ElectronicsTypedForCelsius, limit, 9);

        // The trace's board ambient is typed (a link from the enclosure would be a cycle) at the same limit.
        Assert.Equal(limit, e7.Inputs.Single(i => i.Symbol == "T_amb").Number!.Value, 9);

        // The run-time guard: the enclosure step checks that natural dissipation holds T_max, so the typed values are good while it passes.
        var holds = Assert.Single(e2.Checks, c => c.Name.StartsWith("Natural dissipation holds T_max", StringComparison.Ordinal));
        Assert.True(holds.Passed);
        Assert.True(Si(e2, "T_int") - 273.15 <= limit);
    }

    [Fact]
    public async Task A_Trace_Current_That_Takes_The_Enclosure_Past_Its_Limit_Fails_The_Enclosure_Check_Which_Is_What_Guards_The_Typed_Material_Values()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // 8 A to 24 A: the trace loses 13.566 W, the enclosure reaches 60.303 °C against the 60 °C the plastic and the alloy were typed for, and its own check fails.
        var e7 = made.Instances["e7"];
        Assert.True((await host.Service.UpdateInputsAsync(e7.Id, Scale("I", 3)(e7.Inputs))).Succeeded);

        foreach (var key in new[] { "e6", "e2" })
            Assert.True((await host.Service.UpdateLinksAsync(made.Instances[key].Id)).Succeeded, key);

        var e2 = await Fresh(host, made, "e2");
        Assert.Equal(60.30284544789633 + 273.15, Si(e2, "T_int"), 6);
        Assert.Contains(e2.Checks, c => !c.Passed && c.Name.StartsWith("Natural dissipation holds T_max", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_Supply_Loss_Row_Matches_The_Loads_It_Serves()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var e6 = made.Instances["e6"];
        var rows = e6.Inputs.Single(i => i.Symbol == "loads").Rows!;

        // The 24 V supply (90 %) sits inside the enclosure: its loss is the DC loads' draw × (1/0.9 − 1), typed as a row because E6 counts it only if it is one.
        var dc = rows.Where(r => r.Get("supply")!.Text == "DC").Sum(r => r.Get("P")!.Number!.Value * (r.Get("d")?.Number ?? 1));
        Assert.Equal(0.23850733510851058, dc, 12);
        var supply = Assert.Single(rows, r => r.Get("name")!.Text == "24 V supply loss");
        Assert.Equal(1.0, supply.Get("heat")!.Number!.Value, 12);
        Assert.Equal(dc * (1 / 0.9 - 1), supply.Get("P")!.Number!.Value, 0.01 * dc * (1 / 0.9 - 1));
        Assert.Equal(0.9, e6.Inputs.Single(i => i.Symbol == "eta_psu").Number!.Value, 12);
    }

    [Fact]
    public async Task A_Higher_Trace_Current_Makes_Every_Downstream_Step_Stale_Including_The_Lid_Refuses_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var m8Before = made.Instances["m8"];
        var s9Before = made.Instances["s9"];

        await ChainKit.ReviseAndUpdateAsync(host, made, "e7", "I", 1.2, ["e6", "e2", "m8", "s9"]);

        var (e7, e6, e2, m8, s9) = (await Fresh(host, made, "e7"), await Fresh(host, made, "e6"), await Fresh(host, made, "e2"), await Fresh(host, made, "m8"), await Fresh(host, made, "s9"));

        // 8 A to 9.6 A: the same rise, so the same resistance; the loss goes with I²: 2.170563 W; the minimum width 1.7455 mm (u 0.8728).
        Assert.Equal(2.1705625562552138, Si(e7, "trace_Ploss"), 12);
        Assert.Equal(0.023552111070477583, Si(e7, "trace_R"), 12);
        Assert.Equal(0.8727687516920095, Si(e7, "u_w"), 12);

        // ... into the load table (row 0) ..., the budget's heat 73.670563 W, the enclosure's 77.670563 W and 57.065501 °C, and the latch's temperature of use.
        Assert.Equal(Si(e7, "trace_Ploss"), CellSi(e6, "loads", 0, "P"), 9);
        Assert.Equal(73.67056255625522, Si(e6, "Phi"), 9);
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(77.67056255625522, Si(e2, "P_total"), 9);
        Assert.Equal(57.06550072620887 + 273.15, Si(e2, "T_int"), 9);
        Assert.Equal(Si(e2, "T_int"), InputSi(m8, "T_use"), 9);
        Assert.NotEqual(InputSi(m8Before, "T_use"), InputSi(m8, "T_use"));

        // ... and the lid's pressure: 10.897323 kPa (10.833290 before), so the deflection rises to 0.485353 mm (0.482501).
        Assert.Equal(10897.323028955603, Si(e2, "dp_seal"), 6);
        Assert.Equal(Si(e2, "dp_seal"), InputSi(s9, "q"), 6);
        Assert.NotEqual(InputSi(s9Before, "q"), InputSi(s9, "q"));
        Assert.Equal(4.853526739262048e-4, Si(s9, "w_max"), 1e-9);
        Assert.Equal(16119453.169270223, Si(s9, "sigma_max"), 20.0);

        // The typed material values did not follow: the latch's strain, force and utilisation are those of its 60 °C values, unchanged, and the enclosure is still inside that temperature.
        Assert.Equal(1400, InputSi(m8, "Es") / 1e6, 9);
        Assert.Equal(0.5555555555555556, Si(m8, "strain_util"), 12);
        Assert.True(Si(e2, "T_int") - 273.15 <= 60);
        Assert.All(new[] { m8, s9 }, i => Assert.All(i.Checks, c => Assert.True(c.Passed, c.Name)));
    }

    [Fact]
    public async Task Revising_The_Lid_Leaves_The_Rest_Of_The_Chain_Current()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var s9 = made.Instances["s9"];

        // A thinner lid: nothing is downstream of it, so nothing goes stale.
        var thinner = await host.Service.UpdateInputsAsync(s9.Id, Scale("t", 0.8)(s9.Inputs));
        Assert.True(thinner.Succeeded, thinner.Reason);
        foreach (var key in new[] { "e7", "e6", "e2", "m8", "s9" })
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, key);

        // The revised lid (3.2 mm) deflects (4/3.2)³ = 1.953 times as much: 0.942384 mm against the 0.8 mm limit, so its check FAILS and the sheet says so.
        var lid = await Fresh(host, made, "s9");
        Assert.Equal(0.0009423842607791093, Si(lid, "w_max"), 1e-8);
        Assert.Contains(lid.Checks, c => !c.Passed);
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var e7 = made.Instances["e7"];

        var revised = await host.Service.UpdateInputsAsync(e7.Id, Scale("I", 1.2)(e7.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        foreach (var key in new[] { "e6", "e2", "m8", "s9" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // The load budget takes the loss by a cell link and says which cell; the others are stale only because the step they are linked to is.
        var e6Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e6"))).Reasons;
        Assert.Contains(e6Reasons, r => r.Contains("loads", StringComparison.Ordinal) && r.Contains("trace_Ploss", StringComparison.Ordinal));
        var e2Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e2"))).Reasons;
        Assert.Contains(e2Reasons, r => r.Contains("'Board load budget', which 'P_add' is linked to, is itself stale", StringComparison.Ordinal));
        var m8Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "m8"))).Reasons;
        Assert.Contains(m8Reasons, r => r.Contains("'Enclosure temperature', which 'T_use' is linked to, is itself stale", StringComparison.Ordinal));
        var s9Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "s9"))).Reasons;
        Assert.Contains(s9Reasons, r => r.Contains("'Enclosure temperature', which 'q' is linked to, is itself stale", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Board load budget' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "ELX-STALE", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("check it before issuing", issue.Reason, StringComparison.Ordinal);
        Assert.NotEqual(CalcInstanceStatus.Issued, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }

    [Fact]
    public async Task Issue_Is_Refused_At_The_Staleness_Gate_When_The_Trace_Step_Moves_After_The_Pack_Was_Checked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await IssueIsRefusedAtTheStalenessGateWhenTheFirstStepMovesAfterTheCheckAsync(host, made, CalcPackTemplates.ElectronicsEnclosure, "e7", "e6",
            (id, outside) => host.Service.BindCellAsync(id, "loads", 0, "P", outside, "trace_Ploss"), ["e6", "e2", "m8", "s9"]);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // Another engineer is recorded as the author of every step; full provenance, one check by an independent checker, one issue.
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.ElectronicsEnclosure, "ELX-ENC-001", "Client Co", "Checked against SPEC-ELX-02.");

        // The cell link is named under its row, the top-level ones by input.
        Assert.Contains("P: link: Power trace rev A · trace_Ploss", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Board load budget rev A · Phi", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure temperature rev A · T_int", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure temperature rev A · dp_seal", sheet, StringComparison.Ordinal);
    }
}
